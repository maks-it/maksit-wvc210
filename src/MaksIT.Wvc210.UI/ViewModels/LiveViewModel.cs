using System.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using MaksIT.Wvc210.Client;
using MaksIT.Wvc210.Shared;


namespace MaksIT.Wvc210.UI.ViewModels;

public partial class LiveViewModel : ViewModelBase
{
    private readonly CameraClient _client;
    private readonly MjpegStreamService _stream = new();
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _patrolCts;
    private int _streamGeneration;

    [ObservableProperty] private Bitmap? _frame;
    [ObservableProperty] private bool _isStreaming;
    [ObservableProperty] private string _status = "Not connected.";
    [ObservableProperty] private double _panStep = 8;
    [ObservableProperty] private string _presetNames = "No presets loaded.";
    [ObservableProperty] private bool _isPatrolling;
    [ObservableProperty] private string? _streamMode;
    [ObservableProperty] private bool _isTalking;
    [ObservableProperty] private bool _isTalkLatched;
    [ObservableProperty] private bool _isTestingSpeaker;
    [ObservableProperty] private double _micLevel;
    [ObservableProperty] private string _audioStatus = "Speak buttons unlock when Setup → Audio is talk-ready.";

    public ObservableCollection<PresetSlotViewModel> Presets { get; } = [];
    public LocalTalkSettings TalkSettings { get; }
    public IReadOnlyList<LiveStreamChoice> StreamChoices => LiveStreamChoice.All;
    public bool CameraAllowsTalk =>
        _client.IsConfigured && _cameraSpeakerOn && SelectedAudioOperation.AllowsTalk;
    public bool CanTalk => CameraAllowsTalk
        && TalkSettings.SelectedMicrophone is not null
        && !IsTestingSpeaker;
    public bool SpeakerTestEnabled => IsTestingSpeaker || CameraAllowsTalk;
    public string SpeakerTestLabel => IsTestingSpeaker ? "Stop test" : "Test speaker";
    public bool ShowTalkMeter => IsTalking || IsTestingSpeaker;
    public bool HasFrame => Frame is not null;
    public bool HasPreview => UseMpegPlayer || Frame is not null;
    public string PanStepLabel => ((int)PanStep).ToString();
    public string TalkLatchLabel => IsTalkLatched ? "Stop latch" : "Speak latch";
    public string PatrolLabel => IsPatrolling ? "Stop patrol" : "Patrol";
    public string StreamHint => SelectedStream.Hint;
    public string AudioModeTitle => SelectedAudioOperation.Title;
    public string AudioModeHint => SelectedAudioOperation.Hint;
    public IReadOnlyList<DayNightChoice> DayNightChoices => DayNightChoice.All;
    public string DayNightHint => SelectedDayNight.Mode == DayNightMode.Auto && _dayNightAuto.IsNight == true
        ? "Auto is in night: scene stayed dark, picture is black & white. It will return to color when the scene stays bright."
        : SelectedDayNight.Hint;

    [ObservableProperty] private LiveStreamChoice _selectedStream = LiveStreamChoice.Default;
    [ObservableProperty] private AudioOperationChoice _selectedAudioOperation = AudioOperationChoice.Default;
    [ObservableProperty] private DayNightChoice _selectedDayNight = DayNightChoice.All[0];
    [ObservableProperty] private bool _useMpegPlayer;

    private int _displayQueued;
    private byte[]? _pendingJpeg;
    private bool _sessionActive;
    private bool _dayNightReady;
    private readonly DayNightAuto _dayNightAuto = new();
    private bool _autoDayNightBusy;
    private long _lastAutoLumaMs;
    private byte[]? _lumaScratch;
    private LibVLC? _libVlc;
    private MediaPlayer? _mpegPlayer;
    private Media? _mpegMedia;
    private int _mpegAlive;
    private IntPtr _mpegBuffer;
    private int _mpegPitch;
    private byte[]? _pendingBgra;
    private readonly MediaPlayer.LibVLCVideoLockCb _mpegLockCb;
    private readonly MediaPlayer.LibVLCVideoDisplayCb _mpegDisplayCb;
    private TalkbackSession? _talk;
    private TalkUploadStream? _heldTalkUpload;
    private readonly ConcurrentQueue<byte[]> _talkClips = new();
    private CancellationTokenSource? _talkHoldCts;
    private Task? _talkHoldTask;
    private bool _talkHoldBusy;
    private bool _cameraSpeakerOn;
    private bool _cameraAudioOn;
    private bool _cameraMicIn;
    private TalkCodec _talkCodec = TalkCodec.G711U;

    public LiveViewModel(CameraClient client, LocalTalkSettings talkSettings)
    {
        _client = client;
        TalkSettings = talkSettings;
        _mpegLockCb = OnMpegLock;
        _mpegDisplayCb = OnMpegDisplay;
        TalkSettings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(LocalTalkSettings.SelectedMicrophone))
                UpdateTalkAvailability();
        };
        for (var i = 1; i <= 9; i++)
            Presets.Add(new PresetSlotViewModel(i));
    }

    partial void OnFrameChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(HasFrame));
        OnPropertyChanged(nameof(HasPreview));
    }

    partial void OnUseMpegPlayerChanged(bool value)
        => OnPropertyChanged(nameof(HasPreview));
    partial void OnPanStepChanged(double value) => OnPropertyChanged(nameof(PanStepLabel));
    partial void OnSelectedStreamChanged(LiveStreamChoice value)
    {
        OnPropertyChanged(nameof(StreamHint));
        if (!_sessionActive)
            return;
        ApplySelectedStream();
    }

    partial void OnSelectedAudioOperationChanged(AudioOperationChoice value)
    {
        OnPropertyChanged(nameof(AudioModeTitle));
        OnPropertyChanged(nameof(AudioModeHint));
        OnPropertyChanged(nameof(CameraAllowsTalk));
        OnPropertyChanged(nameof(CanTalk));
        OnPropertyChanged(nameof(SpeakerTestEnabled));
        ApplyListenMute();
    }

    partial void OnSelectedDayNightChanged(DayNightChoice value)
    {
        OnPropertyChanged(nameof(DayNightHint));
        _dayNightAuto.Reset();
        if (!_dayNightReady)
            return;
        if (value.Mode == DayNightMode.Auto)
            _ = SyncAutoFromCameraAsync();
        else
            _ = ApplyDayNightAsync();
    }

    public void RestoreStream(string? name)
        => SelectedStream = LiveStreamChoice.Find(name);

    public void RestoreDayNight(string? name)
        => SelectedDayNight = DayNightChoice.Find(name);

    public async Task StartAsync()
    {
        StopTalk(resetLatch: true, restoreListen: false);
        if (!_client.IsConfigured)
            return;

        IsStreaming = true;
        Status = "Starting live view…";
        _ = RefreshPresetsAsync();
        await RefreshLiveSettingsAsync().ConfigureAwait(true);
        _sessionActive = true;
        ApplySelectedStream();
    }

    public void Stop()
    {
        _sessionActive = false;
        _dayNightReady = false;
        StopTalk(resetLatch: true, restoreListen: false);
        CancelTalkHold();
        DropHeldTalkUpload();
        Interlocked.Increment(ref _streamGeneration);
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        Interlocked.Exchange(ref _pendingJpeg, null);
        Interlocked.Exchange(ref _pendingBgra, null);
        _dayNightAuto.Reset();
        StopPatrol(updateStatus: false);
        StopMpeg(dispose: true);
        UseMpegPlayer = false;
        IsStreaming = false;
    }

    private void ApplySelectedStream()
    {
        if (!_sessionActive || !_client.IsConfigured)
            return;

        var kind = SelectedStream.Kind;
        if (kind is LiveStreamKind.Asf or LiveStreamKind.Rtsp)
        {
            StartMpeg(kind);
            return;
        }

        StopMpeg(dispose: false);
        StartVideoPump(kind);
    }

    private const uint MpegFrameWidth = 640;
    private const uint MpegFrameHeight = 480;

    private static uint Align32(uint value) =>
        (value + 31u) & ~31u;

    private void EnsureMpeg()
    {
        if (_libVlc is not null && _mpegPlayer is not null)
            return;

        var libDir = Path.Combine(
            AppContext.BaseDirectory,
            "libvlc",
            RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64");
        if (Directory.Exists(libDir))
            LibVLCSharp.Shared.Core.Initialize(libDir);
        else
            LibVLCSharp.Shared.Core.Initialize();
        _libVlc?.Dispose();
        _libVlc = new LibVLC(
            "--no-video-title-show",
            "--quiet",
            "--network-caching=400");
        var player = new MediaPlayer(_libVlc)
        {
            EnableHardwareDecoding = false
        };
        player.EncounteredError += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_sessionActive)
                    Status = "MPEG-4 live failed. Try Snapshots, or check camera MPEG-4 is enabled.";
            });
        };
        player.Playing += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!_sessionActive)
                    return;
                IsStreaming = true;
                if (Status.StartsWith("Starting", StringComparison.Ordinal))
                    Status = "Live view running. Click the image to center, or use the pad / WASD.";
            });
        };
        EnsureMpegBuffer();
        player.SetVideoFormat("RV32", MpegFrameWidth, MpegFrameHeight, (uint)_mpegPitch);
        player.SetVideoCallbacks(_mpegLockCb, null, _mpegDisplayCb);
        _mpegPlayer = player;
    }

    private void EnsureMpegBuffer()
    {
        _mpegPitch = (int)Align32(MpegFrameWidth * 4);
        var bytes = _mpegPitch * (int)MpegFrameHeight;
        if (_mpegBuffer != IntPtr.Zero)
            return;
        _mpegBuffer = Marshal.AllocHGlobal(bytes);
    }

    private void FreeMpegBuffer()
    {
        if (_mpegBuffer == IntPtr.Zero)
            return;
        Marshal.FreeHGlobal(_mpegBuffer);
        _mpegBuffer = IntPtr.Zero;
    }

    private IntPtr OnMpegLock(IntPtr opaque, IntPtr planes)
    {
        Marshal.WriteIntPtr(planes, _mpegBuffer);
        return _mpegBuffer;
    }

    private void OnMpegDisplay(IntPtr opaque, IntPtr picture)
    {
        if (Volatile.Read(ref _mpegAlive) == 0 || _mpegBuffer == IntPtr.Zero)
            return;

        var pitch = _mpegPitch;
        var height = (int)MpegFrameHeight;
        var copy = new byte[pitch * height];
        Marshal.Copy(_mpegBuffer, copy, 0, copy.Length);
        Interlocked.Exchange(ref _pendingBgra, copy);
        if (Interlocked.Exchange(ref _displayQueued, 1) != 0)
            return;

        Dispatcher.UIThread.Post(FlushPendingMpegFrame);
    }

    private void FlushPendingMpegFrame()
    {
        try
        {
            var bgra = Interlocked.Exchange(ref _pendingBgra, null);
            if (bgra is null || !_sessionActive || !UseMpegPlayer)
                return;

            var width = (int)MpegFrameWidth;
            var height = (int)MpegFrameHeight;
            var pitch = _mpegPitch;
            var bmp = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);
            using (var fb = bmp.Lock())
            {
                var rowBytes = width * 4;
                for (var y = 0; y < height; y++)
                    Marshal.Copy(bgra, y * pitch, fb.Address + y * fb.RowBytes, rowBytes);
            }

            var old = Frame;
            Frame = bmp;
            old?.Dispose();
            if (!IsStreaming)
                IsStreaming = true;
            if (Status.StartsWith("Starting", StringComparison.Ordinal) ||
                Status.StartsWith("Not connected", StringComparison.Ordinal))
                Status = "Live view running. Click the image to center, or use the pad / WASD.";
            ConsiderAutoDayNight(bgra, width, height, pitch);
        }
        finally
        {
            Interlocked.Exchange(ref _displayQueued, 0);
            if (_pendingBgra is not null && _sessionActive && UseMpegPlayer)
                Dispatcher.UIThread.Post(FlushPendingMpegFrame);
        }
    }

    private void StartMpeg(LiveStreamKind kind)
    {
        StopVideoPump();
        try
        {
            EnsureMpeg();
            if (_libVlc is null || _mpegPlayer is null)
                throw new InvalidOperationException("libVLC did not start.");

            UseMpegPlayer = true;
            IsStreaming = true;
            StreamMode = kind == LiveStreamKind.Asf ? "ASF" : "RTSP";
            var url = kind == LiveStreamKind.Asf ? _client.AsfUrl : _client.RtspUrl;
            _mpegMedia?.Dispose();
            _mpegMedia = new Media(_libVlc, url, FromType.FromLocation);
            if (kind == LiveStreamKind.Rtsp)
                _mpegMedia.AddOption(":rtsp-tcp");
            ApplyListenMute();
            Volatile.Write(ref _mpegAlive, 1);
            if (!_mpegPlayer.Play(_mpegMedia))
                throw new InvalidOperationException("Could not start MPEG-4 playback.");
            Status = "Starting MPEG-4 live…";
        }
        catch (Exception ex)
        {
            Volatile.Write(ref _mpegAlive, 0);
            UseMpegPlayer = false;
            IsStreaming = false;
            Status = "MPEG-4 live failed: " + ex.Message;
        }
    }

    private void StopMpeg(bool dispose)
    {
        Volatile.Write(ref _mpegAlive, 0);
        try { _mpegPlayer?.Stop(); } catch { }
        UseMpegPlayer = false;
        try { _mpegMedia?.Dispose(); } catch { }
        _mpegMedia = null;
        Interlocked.Exchange(ref _pendingBgra, null);
        if (!dispose)
            return;
        try { _mpegPlayer?.Dispose(); } catch { }
        _mpegPlayer = null;
        FreeMpegBuffer();
        try { _libVlc?.Dispose(); } catch { }
        _libVlc = null;
    }

    private void StopVideoPump()
    {
        Interlocked.Increment(ref _streamGeneration);
        var old = _cts;
        _cts = null;
        try { old?.Cancel(); } catch { }
        old?.Dispose();
        Interlocked.Exchange(ref _pendingJpeg, null);
    }

    private void StartVideoPump(LiveStreamKind kind)
    {
        var generation = Interlocked.Increment(ref _streamGeneration);
        var cts = new CancellationTokenSource();
        var old = _cts;
        _cts = cts;
        try { old?.Cancel(); } catch { }
        old?.Dispose();

        StreamMode = kind == LiveStreamKind.Snapshot ? "Snapshot" : "MJPEG";

        _ = Task.Run(async () =>
        {
            try
            {
                if (kind == LiveStreamKind.Snapshot)
                {
                    await _stream.RunSnapshotsAsync(_client, jpeg => OnFrameAsync(jpeg, generation), cts.Token)
                        .ConfigureAwait(false);
                }
                else
                {
                    await _stream.RunAsync(_client, jpeg => OnFrameAsync(jpeg, generation), cts.Token)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                NotifyStoppedAsync(generation, "Live view stopped.");
            }
            catch (Exception ex)
            {
                if (!cts.IsCancellationRequested)
                    NotifyStoppedAsync(generation, "Live view failed: " + ex.Message);
            }
        }, cts.Token);
    }

    [RelayCommand]
    private Task NudgeAsync(string? direction)
        => string.IsNullOrWhiteSpace(direction) ? Task.CompletedTask : MoveAsync(direction);

    public async Task MoveAsync(string direction)
    {
        if (!_client.IsConfigured) return;
        try
        {
            StopPatrol();
            await _client.PanTiltAsync(direction, (int)Math.Clamp(PanStep, 1, 30)).ConfigureAwait(true);
            Status = $"Move {direction} (step {(int)PanStep})";
        }
        catch (Exception ex)
        {
            Status = "PTZ failed: " + ex.Message;
        }
    }

    public async Task ClickToCenterAsync(int x, int y)
    {
        if (!_client.IsConfigured) return;
        try
        {
            StopPatrol();
            await _client.MoveToPositionAsync(x, y).ConfigureAwait(true);
            Status = $"Center → ({x}, {y})";
        }
        catch (Exception ex)
        {
            Status = "PTZ failed: " + ex.Message;
        }
    }

    public bool TryGetPreviewPixelSize(out int width, out int height)
    {
        if (UseMpegPlayer)
        {
            width = (int)MpegFrameWidth;
            height = (int)MpegFrameHeight;
            return true;
        }

        if (Frame is not null)
        {
            width = Math.Max(1, Frame.PixelSize.Width);
            height = Math.Max(1, Frame.PixelSize.Height);
            return true;
        }

        width = 0;
        height = 0;
        return false;
    }

    [RelayCommand]
    private Task HomeAsync() => SafePtz(() => _client.HomeAsync(), "Home (calibration)");

    [RelayCommand]
    private Task UserHomeAsync() => SafePtz(() => _client.UserHomeAsync(), "User home");

    [RelayCommand]
    private Task SetUserHomeAsync() => SafePtz(() => _client.SetUserHomeAsync(), "User home saved");

    [RelayCommand]
    private Task RecalibrateAsync() => SafePtz(() => _client.RecalibrateAsync(), "Recalibrating…");

    [RelayCommand]
    private Task AutoPanAsync() => SafePtz(() => _client.AutoPanAsync(), "Auto-pan");

    [RelayCommand]
    private void Patrol()
    {
        if (IsPatrolling)
        {
            StopPatrol();
            return;
        }

        if (!_client.IsConfigured)
            return;

        var indices = new List<int>();
        foreach (var slot in Presets)
        {
            if (slot.HasPosition)
                indices.Add(slot.Index);
        }

        if (indices.Count < 2)
        {
            Status = "Save at least two presets, then Patrol loops the non-empty ones.";
            return;
        }

        var cts = new CancellationTokenSource();
        _patrolCts?.Dispose();
        _patrolCts = cts;
        IsPatrolling = true;
        _ = RunPatrolAsync(indices, cts.Token);
    }

    private async Task RunPatrolAsync(IReadOnlyList<int> indices, CancellationToken ct)
    {
        var dwell = await ReadPatrolDwellAsync(ct).ConfigureAwait(true);
        var step = 0;
        try
        {
            while (!ct.IsCancellationRequested && _sessionActive)
            {
                var index = indices[step % indices.Count];
                await _client.PresetMoveAsync(index, ct).ConfigureAwait(true);
                Status = $"Patrol → {PresetTitle(index)} ({dwell.TotalSeconds:0} s)";
                step++;
                await Task.Delay(dwell, ct).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            /* stopped */
        }
        catch (Exception ex)
        {
            Status = "Patrol failed: " + ex.Message;
        }
        finally
        {
            IsPatrolling = false;
        }
    }

    private string PresetTitle(int index)
    {
        foreach (var slot in Presets)
        {
            if (slot.Index == index)
                return slot.DisplayName;
        }

        return "Preset " + index;
    }

    private async Task<TimeSpan> ReadPatrolDwellAsync(CancellationToken ct)
    {
        const int fallback = 8;
        try
        {
            var ptz = await _client.GetGroupAsync("PTZ", ct).ConfigureAwait(true);
            if (int.TryParse(ptz.GetValueOrDefault("PatrolInterval"), out var seconds))
                return TimeSpan.FromSeconds(Math.Clamp(seconds, 5, 60));
        }
        catch
        {
            /* use default dwell */
        }

        return TimeSpan.FromSeconds(fallback);
    }

    private void StopPatrol(bool updateStatus = true)
    {
        var cts = _patrolCts;
        _patrolCts = null;
        try { cts?.Cancel(); } catch { }
        cts?.Dispose();
        if (!IsPatrolling)
            return;
        IsPatrolling = false;
        if (updateStatus && _sessionActive)
            Status = "Patrol stopped.";
    }

    [RelayCommand]
    private Task MotionPositionAsync() => SafePtz(() => _client.MotionPositionAsync(), "Motion position");

    [RelayCommand]
    private Task GoPresetAsync(string? index)
        => int.TryParse(index, out var n)
            ? SafePtz(() => _client.PresetMoveAsync(n), $"Preset {n}")
            : Task.CompletedTask;

    [RelayCommand]
    private async Task SetPresetAsync(string? index)
    {
        if (!int.TryParse(index, out var n))
            return;
        await SafePtz(() => _client.PresetSetAsync(n), $"Saved preset {n}").ConfigureAwait(true);
        await RefreshPresetsAsync().ConfigureAwait(true);
        PresetByIndex(n)?.MarkSaved();
    }

    [RelayCommand]
    private async Task DeletePresetAsync(string? index)
    {
        if (!int.TryParse(index, out var n))
            return;
        if (PresetByIndex(n) is not { HasPosition: true })
            return;

        StopPatrol();
        await SafePtz(
            () => _client.ClearPresetSlotAsync(n),
            $"Deleted preset {n}").ConfigureAwait(true);
        await RefreshPresetsAsync().ConfigureAwait(true);
    }

    private PresetSlotViewModel? PresetByIndex(int index)
    {
        foreach (var slot in Presets)
        {
            if (slot.Index == index)
                return slot;
        }

        return null;
    }

    [RelayCommand]
    private Task RefreshPresets() => RefreshPresetsAsync();

    [RelayCommand]
    private async Task SaveSnapshotAsync()
    {
        if (!_client.IsConfigured) return;
        try
        {
            var jpeg = await _client.SnapshotAsync().ConfigureAwait(true);
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "Wvc210");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"wvc210-{DateTime.Now:yyyyMMdd-HHmmss}.jpg");
            await File.WriteAllBytesAsync(path, jpeg).ConfigureAwait(true);
            Status = "Saved " + path;
        }
        catch (Exception ex)
        {
            Status = "Snapshot failed: " + ex.Message;
        }
    }

    public async Task RefreshPresetsAsync()
    {
        if (!_client.IsConfigured) return;
        try
        {
            var presets = await _client.GetPresetsAsync().ConfigureAwait(true);
            Dictionary<string, string> ptz;
            try
            {
                ptz = await _client.GetGroupAsync("PTZ").ConfigureAwait(true);
            }
            catch
            {
                ptz = [];
            }

            foreach (var slot in Presets)
            {
                presets.TryGetValue("PT" + slot.Index, out var ptName);
                ptz.TryGetValue("Preset" + slot.Index + "Name", out var groupName);
                ptz.TryGetValue("Preset" + slot.Index + "Position", out var position);
                var name = !string.IsNullOrWhiteSpace(ptName) ? ptName : groupName;
                slot.ApplyFromCamera(name, position);
            }
            PresetNames = "Presets loaded.";
        }
        catch (Exception ex)
        {
            PresetNames = "Presets unavailable: " + ex.Message;
        }
    }

    private async Task SafePtz(Func<Task> action, string ok)
    {
        if (!_client.IsConfigured) return;
        try
        {
            StopPatrol();
            await action().ConfigureAwait(true);
            Status = ok;
        }
        catch (Exception ex)
        {
            Status = "PTZ failed: " + ex.Message;
        }
    }

    public async Task LoadAudioSettingsAsync()
    {
        if (!_client.IsConfigured)
        {
            _cameraSpeakerOn = false;
            _cameraAudioOn = false;
            _cameraMicIn = false;
            UpdateTalkAvailability();
            ApplyListenMute();
            return;
        }

        try
        {
            var audio = await _client.GetGroupAsync("AUDIO").ConfigureAwait(true);
            _cameraAudioOn = audio.GetValueOrDefault("audio_mode") is not "0";
            _cameraMicIn = _cameraAudioOn && audio.GetValueOrDefault("audio_in") is "1";
            _cameraSpeakerOn = _cameraAudioOn && audio.GetValueOrDefault("audio_out") is "1";
            SelectedAudioOperation = AudioOperationChoice.Parse(audio.GetValueOrDefault("operation_mode", "0"));
            _talkCodec = audio.GetValueOrDefault("out_audio_type") is "0" ? TalkCodec.G711A : TalkCodec.G711U;
            ApplyListenMute();
            if (!SelectedAudioOperation.AllowsTalk)
            {
                CancelTalkHold();
                if (IsTalking)
                    StopTalk(resetLatch: true);
            }
            else if (!CanTalk && IsTalking)
            {
                StopTalk(resetLatch: true);
            }

            UpdateTalkAvailability();
        }
        catch (Exception ex)
        {
            _cameraSpeakerOn = false;
            _cameraAudioOn = false;
            _cameraMicIn = false;
            AudioStatus = "Could not read audio settings: " + ex.Message;
            OnPropertyChanged(nameof(CameraAllowsTalk));
            OnPropertyChanged(nameof(CanTalk));
            OnPropertyChanged(nameof(SpeakerTestEnabled));
        }
    }

    private bool ShouldMuteListen =>
        !_cameraAudioOn
        || !_cameraMicIn
        || !SelectedAudioOperation.AllowsListen
        || (SelectedAudioOperation.Mode == AudioOperationMode.HalfDuplex
            && (IsTalking || IsTestingSpeaker || TalkCgiHeld));

    private void ApplyListenMute()
    {
        if (_mpegPlayer is null)
            return;
        try { _mpegPlayer.Mute = ShouldMuteListen; } catch { }
    }

    public async Task LoadDayNightAsync()
    {
        if (!_client.IsConfigured)
            return;

        try
        {
            _dayNightReady = false;
            var mode = await _client.GetDayNightAsync().ConfigureAwait(true);
            if (SelectedDayNight.Mode == DayNightMode.Auto)
                _dayNightAuto.Assume(mode == DayNightMode.Night);
            else
                SelectedDayNight = DayNightChoice.Find(mode);
            OnPropertyChanged(nameof(DayNightHint));
        }
        catch (Exception ex)
        {
            Status = "Could not read day/night: " + ex.Message;
        }
        finally
        {
            _dayNightReady = true;
        }
    }

    public async Task RefreshLiveSettingsAsync()
    {
        await LoadAudioSettingsAsync().ConfigureAwait(true);
        await LoadDayNightAsync().ConfigureAwait(true);
    }

    private async Task SyncAutoFromCameraAsync()
    {
        if (!_client.IsConfigured)
            return;
        try
        {
            var mode = await _client.GetDayNightAsync().ConfigureAwait(true);
            _dayNightAuto.Assume(mode == DayNightMode.Night);
            OnPropertyChanged(nameof(DayNightHint));
        }
        catch
        {
            // Live frames still drive Auto.
        }
    }

    private async Task ApplyDayNightAsync()
    {
        if (!_client.IsConfigured)
            return;
        if (SelectedDayNight.Mode == DayNightMode.Auto)
            return;
        try
        {
            await _client.SetDayNightAsync(SelectedDayNight.Mode).ConfigureAwait(true);
            Status = "Day/night → " + SelectedDayNight.Title +
                     (SelectedDayNight.Mode == DayNightMode.Night
                         ? " (black & white)."
                         : " (color).");
        }
        catch (Exception ex)
        {
            Status = "Day/night failed: " + ex.Message;
        }
    }

    private void ConsiderAutoDayNight(Bitmap bitmap)
    {
        if (SelectedDayNight.Mode != DayNightMode.Auto || !_sessionActive)
            return;
        try
        {
            var size = bitmap.PixelSize;
            if (size.Width < 8 || size.Height < 8)
                return;
            var wb = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            using (wb)
            using (var fb = wb.Lock())
            {
                bitmap.CopyPixels(fb);
                var needed = fb.RowBytes * size.Height;
                if (_lumaScratch is null || _lumaScratch.Length < needed)
                    _lumaScratch = new byte[needed];
                Marshal.Copy(fb.Address, _lumaScratch, 0, needed);
                ConsiderAutoDayNight(_lumaScratch, size.Width, size.Height, fb.RowBytes);
            }
        }
        catch
        {
            // MPEG path still samples; JPEG sample is best-effort.
        }
    }

    private void ConsiderAutoDayNight(byte[] bgra, int width, int height, int pitch)
    {
        if (SelectedDayNight.Mode != DayNightMode.Auto || !_sessionActive || _autoDayNightBusy
            || TalkCgiHeld)
            return;
        var nowMs = Environment.TickCount64;
        if (nowMs - _lastAutoLumaMs < 400)
            return;
        _lastAutoLumaMs = nowMs;

        var luma = DayNightAuto.MeanLuma(bgra, width, height, pitch);
        var decision = _dayNightAuto.Observe(luma, DateTime.UtcNow);
        if (decision is null)
            return;
        _ = ApplyAutoDayNightAsync(decision.Value, luma);
    }

    private async Task ApplyAutoDayNightAsync(bool night, int luma)
    {
        if (_autoDayNightBusy || TalkCgiHeld)
            return;
        _autoDayNightBusy = true;
        try
        {
            await _client.SetDayNightAsync(night ? DayNightMode.Night : DayNightMode.Auto)
                .ConfigureAwait(true);
            OnPropertyChanged(nameof(DayNightHint));
            Status = night
                ? $"Auto → night (luma {luma}, black & white)."
                : $"Auto → day (luma {luma}, color).";
        }
        catch (Exception ex)
        {
            Status = "Auto day/night failed: " + ex.Message;
        }
        finally
        {
            _autoDayNightBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleTalkLatchAsync()
    {
        if (IsTalkLatched)
        {
            StopTalk(resetLatch: true);
            return;
        }

        if (IsTalking)
        {
            IsTalkLatched = true;
            return;
        }

        await StartTalkAsync().ConfigureAwait(true);
        if (IsTalking)
            IsTalkLatched = true;
    }

    private bool TalkCgiHeld => _talkHoldTask is { IsCompleted: false };

    [RelayCommand]
    private void TestSpeaker()
    {
        if (IsTestingSpeaker)
        {
            IsTestingSpeaker = false;
            MicLevel = 0;
            AudioStatus = SelectedAudioOperation.Mode == AudioOperationMode.HalfDuplex
                ? "Speaker CGI held — live listen is muted (half duplex). Click Test speaker to beep again."
                : "Speaker CGI held — live stays on. Click Test speaker to beep again.";
            UpdateTalkAvailability();
            return;
        }

        if (!CameraAllowsTalk)
        {
            UpdateTalkAvailability();
            return;
        }

        IsTestingSpeaker = true;
        Status = "Speaker test…";
        if (TalkCgiHeld)
        {
            AudioStatus = "Speaker test on (same CGI). Click again to stop beeps.";
            return;
        }

        _talkHoldTask = RunTalkCgiHoldAsync();
    }

    private async Task EnsureTalkCgiAsync()
    {
        if (_heldTalkUpload is not null && TalkCgiHeld)
            return;

        if (!TalkCgiHeld)
            _talkHoldTask = RunTalkCgiHoldAsync();

        var hold = _talkHoldTask;
        if (hold is null)
            return;

        var clock = Stopwatch.StartNew();
        while (_heldTalkUpload is null && !hold.IsCompleted && clock.ElapsedMilliseconds < 8000)
            await Task.Delay(40).ConfigureAwait(true);
    }

    private async Task RunTalkCgiHoldAsync()
    {
        var cts = new CancellationTokenSource();
        _talkHoldCts = cts;
        SetTalkHoldBusy(true);
        MicLevel = 8;
        try
        {
            var upload = await _client.OpenTalkUploadAsync(_talkCodec, cts.Token).ConfigureAwait(true);
            _heldTalkUpload = upload;
            SetTalkHoldBusy(false);
            ApplyListenMute();
            if (IsTestingSpeaker)
            {
                AudioStatus = SelectedAudioOperation.Mode == AudioOperationMode.FullDuplex
                    ? $"Speaker test on → {upload.Path} (held). Live listen stays on."
                    : $"Speaker test on → {upload.Path} (held).";
            }

            await PumpTalkCgiHoldAsync(upload, cts.Token).ConfigureAwait(true);
        }
        catch (Exception ex) when (cts.IsCancellationRequested || ex is OperationCanceledException)
        {
            /* leaving Live */
        }
        catch (Exception ex)
        {
            AudioStatus = "Talk CGI failed: " + ex.Message;
            Status = AudioStatus;
            IsTestingSpeaker = false;
        }
        finally
        {
            if (ReferenceEquals(_talkHoldCts, cts))
                _talkHoldCts = null;
            cts.Dispose();
            DropHeldTalkUpload();
            ClearTalkClips();
            IsTestingSpeaker = false;
            MicLevel = 0;
            SetTalkHoldBusy(false);
            ApplyListenMute();
            if (_sessionActive && _client.IsConfigured
                && !AudioStatus.StartsWith("Talk CGI failed", StringComparison.Ordinal)
                && !AudioStatus.StartsWith("Speaker test failed", StringComparison.Ordinal))
                UpdateTalkAvailability();
        }
    }

    private async Task PumpTalkCgiHoldAsync(TalkUploadStream upload, CancellationToken ct)
    {
        var pcm = new short[SpeakerTest.ClipSamples];
        var encoded = new byte[SpeakerTest.ClipSamples];
        var state = new SpeakerTest.PatternState();
        var clock = Stopwatch.StartNew();
        var posted = 0;
        while (!ct.IsCancellationRequested)
        {
            if (IsTalking && _talkClips.TryDequeue(out var micClip) && micClip.Length > 0)
            {
                await upload.WriteAsync(micClip, ct).ConfigureAwait(false);
            }
            else
            {
                var tone = IsTestingSpeaker && !IsTalking;
                var level = SpeakerTest.FillClip(pcm, encoded, _talkCodec, tone, ref state);
                if (tone)
                {
                    var bar = Math.Min(100, Math.Max(8, level * 250));
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (IsTestingSpeaker)
                            MicLevel = bar;
                    });
                }
                else if (!IsTalking)
                {
                    Dispatcher.UIThread.Post(() => MicLevel = 0);
                }

                await upload.WriteAsync(encoded.AsMemory(0, SpeakerTest.ClipSamples), ct).ConfigureAwait(false);
            }

            posted++;
            var wait = TimeSpan.FromMilliseconds(posted * SpeakerTest.ClipMilliseconds) - clock.Elapsed;
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, ct).ConfigureAwait(false);
        }
    }

    private void SetTalkHoldBusy(bool value)
    {
        if (_talkHoldBusy == value)
            return;
        _talkHoldBusy = value;
        OnPropertyChanged(nameof(SpeakerTestEnabled));
    }

    private void DropHeldTalkUpload()
    {
        var upload = _heldTalkUpload;
        _heldTalkUpload = null;
        try { upload?.Dispose(); } catch { }
    }

    private void EnqueueTalkClip(byte[] clip)
    {
        while (_talkClips.Count > 32 && _talkClips.TryDequeue(out _)) { }
        _talkClips.Enqueue(clip);
    }

    private void ClearTalkClips()
    {
        while (_talkClips.TryDequeue(out _)) { }
    }

    private void CancelTalkHold()
    {
        try { _talkHoldCts?.Cancel(); } catch { }
    }

    public void TryResumeMpegListen()
    {
        if (!_sessionActive || IsTalking)
            return;

        SetMpegPaused(false);
        if (_mpegPlayer is null && SelectedStream.Kind is LiveStreamKind.Asf or LiveStreamKind.Rtsp)
            ApplySelectedStream();
    }

    private void SetMpegPaused(bool paused)
    {
        try { _mpegPlayer?.SetPause(paused); } catch { }
    }

    public async Task StartTalkAsync()
    {
        if (IsTalking)
            return;
        if (!CanTalk)
        {
            UpdateTalkAvailability();
            return;
        }

        IsTestingSpeaker = false;
        var mic = TalkSettings.SelectedMicrophone;
        if (mic is null)
            return;

        try
        {
            await EnsureTalkCgiAsync().ConfigureAwait(true);
            if (_heldTalkUpload is null)
            {
                AudioStatus = "Speak failed: talk CGI did not open.";
                return;
            }

            var session = new TalkbackSession(_talkCodec);
            session.LevelChanged += level =>
            {
                Dispatcher.UIThread.Post(() => MicLevel = Math.Min(100, level * 250));
            };
            session.Encoded += EnqueueTalkClip;
            var capture = MicrophoneEnumerator.Open(mic);
            session.Attach(capture);
            _talk = session;
            IsTalking = true;
            ApplyListenMute();
            AudioStatus = $"Speaking via {mic.Name} → {_heldTalkUpload.Path}.";
            Status = "Speaking to camera.";
            _ = WatchMicLevelAsync();
        }
        catch (Exception ex)
        {
            StopTalk(resetLatch: true);
            AudioStatus = "Speak failed: " + ex.Message;
        }
    }

    private async Task WatchMicLevelAsync()
    {
        try
        {
            await Task.Delay(900).ConfigureAwait(true);
            if (IsTalking && MicLevel < 1)
            {
                AudioStatus = "Microphone is silent. Choose another device in Setup → Audio, and keep Speak latch on while you talk.";
            }
        }
        catch
        {
            /* view closed */
        }
    }

    public void StopTalk(bool resetLatch = false, bool restoreListen = true)
    {
        if (resetLatch)
            IsTalkLatched = false;
        else if (IsTalkLatched)
            return;

        var session = _talk;
        _talk = null;
        session?.Dispose();
        ClearTalkClips();
        IsTalking = false;
        MicLevel = 0;
        if (restoreListen)
            SetMpegPaused(false);
        if (_client.IsConfigured && AudioStatus.StartsWith("Speaking", StringComparison.Ordinal))
            UpdateTalkAvailability();
    }

    partial void OnIsTalkLatchedChanged(bool value)
        => OnPropertyChanged(nameof(TalkLatchLabel));

    partial void OnIsPatrollingChanged(bool value)
        => OnPropertyChanged(nameof(PatrolLabel));

    partial void OnIsTalkingChanged(bool value)
    {
        OnPropertyChanged(nameof(SpeakerTestEnabled));
        OnPropertyChanged(nameof(ShowTalkMeter));
        ApplyListenMute();
    }

    partial void OnIsTestingSpeakerChanged(bool value)
    {
        OnPropertyChanged(nameof(CanTalk));
        OnPropertyChanged(nameof(SpeakerTestEnabled));
        OnPropertyChanged(nameof(SpeakerTestLabel));
        OnPropertyChanged(nameof(ShowTalkMeter));
        ApplyListenMute();
    }

    private void UpdateTalkAvailability()
    {
        OnPropertyChanged(nameof(CameraAllowsTalk));
        OnPropertyChanged(nameof(CanTalk));
        OnPropertyChanged(nameof(SpeakerTestEnabled));
        if (IsTalking || IsTestingSpeaker)
            return;

        if (!_client.IsConfigured)
            AudioStatus = "Connect to the camera to speak.";
        else if (!_cameraAudioOn)
            AudioStatus = "Turn on Audio enabled in Setup → Audio.";
        else if (!SelectedAudioOperation.AllowsTalk)
            AudioStatus = "Simplex listen — Speak is off. Change Operation in Setup → Audio.";
        else if (!_cameraSpeakerOn)
            AudioStatus = "Turn on Speaker out in Setup → Audio.";
        else if (TalkCgiHeld)
            AudioStatus = SelectedAudioOperation.Mode == AudioOperationMode.HalfDuplex
                ? "Speaker CGI held — live listen is muted (half duplex). Click Test speaker to beep again."
                : "Speaker CGI held — live stays on. Click Test speaker to beep again.";
        else if (TalkSettings.SelectedMicrophone is null)
            AudioStatus = "Speaker test is ready (no local mic). Pick a microphone in Setup → Audio to speak.";
        else
            AudioStatus = $"Ready — {TalkSettings.SelectedMicrophone.Name}.";
    }

    private void NotifyStoppedAsync(int generation, string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (generation != _streamGeneration)
                return;
            IsStreaming = false;
            Status = message;
        });
    }

    private Task OnFrameAsync(byte[] jpeg, int generation)
    {
        if (generation != _streamGeneration)
            return Task.CompletedTask;

        Interlocked.Exchange(ref _pendingJpeg, jpeg);
        if (Interlocked.Exchange(ref _displayQueued, 1) != 0)
            return Task.CompletedTask;

        Dispatcher.UIThread.Post(() => FlushPendingFrame(generation));
        return Task.CompletedTask;
    }

    private void FlushPendingFrame(int generation)
    {
        try
        {
            var jpeg = Interlocked.Exchange(ref _pendingJpeg, null);
            if (jpeg is null)
                return;
            if (generation != _streamGeneration)
                return;

            Bitmap bitmap;
            try
            {
                using var ms = new MemoryStream(jpeg, writable: false);
                bitmap = new Bitmap(ms);
            }
            catch
            {
                return;
            }

            var old = Frame;
            Frame = bitmap;
            old?.Dispose();
            if (!IsStreaming)
                IsStreaming = true;
            if (Status.StartsWith("Starting", StringComparison.Ordinal) ||
                Status.StartsWith("Not connected", StringComparison.Ordinal))
                Status = "Live view running. Click the image to center, or use the pad / WASD.";
            ConsiderAutoDayNight(bitmap);
        }
        finally
        {
            Interlocked.Exchange(ref _displayQueued, 0);
            if (_pendingJpeg is not null && generation == _streamGeneration)
                Dispatcher.UIThread.Post(() => FlushPendingFrame(generation));
        }
    }
}
