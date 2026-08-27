using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using MaksIT.Wvc210.Shared;
using MaksIT.Wvc210.Client;

namespace MaksIT.Wvc210.UI.ViewModels;

public partial class LiveViewModel : ViewModelBase
{
    private readonly CameraClient _client;
    private readonly MjpegStreamService _stream = new();
    private CancellationTokenSource? _cts;
    private int _streamGeneration;

    [ObservableProperty] private Bitmap? _frame;
    [ObservableProperty] private bool _isStreaming;
    [ObservableProperty] private string _status = "Not connected.";
    [ObservableProperty] private double _panStep = 8;
    [ObservableProperty] private string _presetNames = "No presets loaded.";
    [ObservableProperty] private string? _streamMode;
    [ObservableProperty] private bool _isTalking;
    [ObservableProperty] private bool _isTalkLatched;
    [ObservableProperty] private double _micLevel;
    [ObservableProperty] private string _audioStatus = "Speak buttons unlock when Setup → Audio is talk-ready.";

    public ObservableCollection<PresetSlotViewModel> Presets { get; } = [];
    public LocalTalkSettings TalkSettings { get; }
    public bool CanTalk => _client.IsConfigured
        && TalkSettings.SelectedMicrophone is not null
        && _cameraSpeakerOn
        && _cameraTalkMode;
    public bool HasFrame => Frame is not null;
    public string PanStepLabel => ((int)PanStep).ToString();
    public string TalkLatchLabel => IsTalkLatched ? "Stop latch" : "Speak latch";

    private readonly OcxLiveStreamService _ocx = new();
    private readonly G726Codec _g726 = new();
    private IPcmPlayer? _player;
    private int _heardCameraAudio;
    private bool _halfDuplex;
    private TalkCodec _listenCodec = TalkCodec.G711U;
    private TalkbackSession? _talk;
    private bool _cameraSpeakerOn;
    private bool _cameraTalkMode;
    private TalkCodec _talkCodec = TalkCodec.G711U;

    public LiveViewModel(CameraClient client, LocalTalkSettings talkSettings)
    {
        _client = client;
        TalkSettings = talkSettings;
        TalkSettings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(LocalTalkSettings.SelectedMicrophone))
                UpdateTalkAvailability();
        };
        for (var i = 1; i <= 9; i++)
            Presets.Add(new PresetSlotViewModel(i));
    }

    partial void OnFrameChanged(Bitmap? value) => OnPropertyChanged(nameof(HasFrame));
    partial void OnPanStepChanged(double value) => OnPropertyChanged(nameof(PanStepLabel));

    public async Task StartAsync()
    {
        Stop();
        if (!_client.IsConfigured)
            return;

        IsStreaming = true;
        Status = "Starting live view…";
        _player?.Dispose();
        _player = PcmPlayer.TryCreate();
        _ = RefreshPresetsAsync();
        await LoadAudioSettingsAsync().ConfigureAwait(true);
        StartVideoPump(listenAudio: true);
    }

    public void Stop()
    {
        StopTalk(resetLatch: true, restoreListen: false);
        _player?.Dispose();
        _player = null;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        IsStreaming = false;
    }

    private void StartVideoPump(bool listenAudio)
    {
        var generation = Interlocked.Increment(ref _streamGeneration);
        var cts = new CancellationTokenSource();
        var old = _cts;
        _cts = cts;
        try { old?.Cancel(); } catch { }
        old?.Dispose();

        if (_player is not null)
            _player.Muted = !listenAudio;
        if (listenAudio)
        {
            _heardCameraAudio = 0;
            _g726.Reset();
        }
        else
            StreamMode = "MJPEG";

        _ = Task.Run(async () =>
        {
            try
            {
                if (listenAudio)
                {
                    await _ocx.RunAsync(
                        _client,
                        jpeg => OnFrameAsync(jpeg, generation),
                        (type, payload) => OnCameraAudio(type, payload, generation),
                        cts.Token).ConfigureAwait(false);
                }
                else
                {
                    await _stream.RunAsync(_client, jpeg => OnFrameAsync(jpeg, generation), cts.Token)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                await NotifyStoppedAsync(generation, "Live view stopped.").ConfigureAwait(false);
            }
            catch (Exception ocxEx)
            {
                if (cts.IsCancellationRequested || !listenAudio)
                {
                    if (!cts.IsCancellationRequested)
                        await NotifyStoppedAsync(generation, "Live view failed: " + ocxEx.Message).ConfigureAwait(false);
                    return;
                }

                try
                {
                    await _stream.RunAsync(_client, jpeg => OnFrameAsync(jpeg, generation), cts.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    await NotifyStoppedAsync(generation, "Live view stopped.").ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await NotifyStoppedAsync(generation, "Live view failed: " + ex.Message).ConfigureAwait(false);
                }
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
            await _client.MoveToPositionAsync(x, y).ConfigureAwait(true);
            Status = $"Center → ({x}, {y})";
        }
        catch (Exception ex)
        {
            Status = "PTZ failed: " + ex.Message;
        }
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
    private Task PatrolAsync() => SafePtz(() => _client.PatrolAsync(), "Patrol");

    [RelayCommand]
    private Task MotionPositionAsync() => SafePtz(() => _client.MotionPositionAsync(), "Motion position");

    [RelayCommand]
    private Task GoPresetAsync(string? index)
        => int.TryParse(index, out var n)
            ? SafePtz(() => _client.PresetMoveAsync(n), $"Preset {n}")
            : Task.CompletedTask;

    [RelayCommand]
    private Task SetPresetAsync(string? index)
        => int.TryParse(index, out var n)
            ? SafePtz(() => _client.PresetSetAsync(n), $"Saved preset {n}")
            : Task.CompletedTask;

    [RelayCommand]
    private Task RefreshPresetsCommand() => RefreshPresetsAsync();

    [RelayCommand]
    private void OpenAsf()
    {
        try
        {
            ExternalApps.OpenVlc(null, _client.AsfUrl, rtspTcp: false);
            Status = "Opened ASF stream in VLC.";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private void OpenRtsp()
    {
        try
        {
            ExternalApps.OpenVlc(null, _client.RtspUrl, rtspTcp: true);
            Status = "Opened RTSP (TCP) in VLC.";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

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
            foreach (var slot in Presets)
            {
                presets.TryGetValue("PT" + slot.Index, out var name);
                slot.Name = name?.Trim() ?? "";
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
            _cameraTalkMode = false;
            UpdateTalkAvailability();
            return;
        }

        try
        {
            var audio = await _client.GetGroupAsync("AUDIO").ConfigureAwait(true);
            var audioEnabled = audio.GetValueOrDefault("audio_mode") is not "0";
            var mode = audio.GetValueOrDefault("operation_mode", "0");
            _cameraSpeakerOn = audioEnabled && audio.GetValueOrDefault("audio_out") is "1";
            _cameraTalkMode = mode is "1" or "2" or "3";
            _halfDuplex = mode is "2";
            _talkCodec = audio.GetValueOrDefault("out_audio_type") is "0" ? TalkCodec.G711A : TalkCodec.G711U;
            _listenCodec = audio.GetValueOrDefault("in_audio_type") is "0" ? TalkCodec.G711A : TalkCodec.G711U;
            if (_player is not null)
                _player.Muted = IsTalking && _halfDuplex;
            if (!CanTalk && IsTalking)
                StopTalk(resetLatch: true);
            UpdateTalkAvailability();
        }
        catch (Exception ex)
        {
            _cameraSpeakerOn = false;
            _cameraTalkMode = false;
            AudioStatus = "Could not read audio settings: " + ex.Message;
            OnPropertyChanged(nameof(CanTalk));
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

    public async Task StartTalkAsync()
    {
        if (IsTalking)
            return;
        if (!CanTalk)
        {
            UpdateTalkAvailability();
            return;
        }

        var mic = TalkSettings.SelectedMicrophone;
        if (mic is null)
            return;

        try
        {
            StartVideoPump(listenAudio: false);
            await Task.Delay(250).ConfigureAwait(true);

            var session = new TalkbackSession(_client, _talkCodec);
            session.LevelChanged += level =>
            {
                Dispatcher.UIThread.Post(() => MicLevel = Math.Min(100, level * 250));
            };
            session.Failed += msg =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (IsTalking)
                        AudioStatus = "Speak upload failed: " + msg;
                });
            };
            var capture = MicrophoneEnumerator.Open(mic);
            session.Attach(capture);
            _talk = session;
            IsTalking = true;
            if (_player is not null)
                _player.Muted = true;
            AudioStatus = $"Speaking via {mic.Name} → {session.Path}.";
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
        IsTalking = false;
        MicLevel = 0;
        if (_player is not null)
            _player.Muted = false;
        if (restoreListen && _client.IsConfigured && IsStreaming)
            StartVideoPump(listenAudio: true);
        if (_client.IsConfigured && AudioStatus.StartsWith("Speaking", StringComparison.Ordinal))
            UpdateTalkAvailability();
    }

    partial void OnIsTalkLatchedChanged(bool value)
        => OnPropertyChanged(nameof(TalkLatchLabel));

    private void UpdateTalkAvailability()
    {
        OnPropertyChanged(nameof(CanTalk));
        if (IsTalking)
            return;

        if (!_client.IsConfigured)
            AudioStatus = "Connect to the camera to speak.";
        else if (TalkSettings.SelectedMicrophone is null)
            AudioStatus = "Pick a local microphone in Setup → Audio.";
        else if (!_cameraSpeakerOn)
            AudioStatus = "Turn on Audio enabled and Speaker out in Setup → Audio.";
        else if (!_cameraTalkMode)
            AudioStatus = "Set Operation to Talk, Half duplex, or Full duplex in Setup → Audio.";
        else
            AudioStatus = $"Ready — {TalkSettings.SelectedMicrophone.Name}.";
    }

    private void OnCameraAudio(byte type, byte[] payload, int generation)
    {
        if (generation != _streamGeneration || _player is null)
            return;
        if (payload.Length == 0)
            return;

        short[] pcm;
        if (type == OcxLiveStreamService.FrameG726)
            pcm = _g726.Decode(payload);
        else if (type == OcxLiveStreamService.FrameG711)
        {
            if (_heardCameraAudio == 0)
                _listenCodec = PickListenCodec(payload);
            pcm = G711Codec.Decode(_listenCodec, payload);
        }
        else
            return;

        if (Interlocked.Exchange(ref _heardCameraAudio, 1) == 0)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (generation == _streamGeneration)
                    StreamMode = "MJPEG + audio";
            });
        }

        _player.Write(pcm);
    }

    private TalkCodec PickListenCodec(byte[] payload)
    {
        var ulaw = G711Codec.Decode(TalkCodec.G711U, payload);
        var alaw = G711Codec.Decode(TalkCodec.G711A, payload);
        return PcmResampler.Rms(ulaw) >= PcmResampler.Rms(alaw) ? TalkCodec.G711U : TalkCodec.G711A;
    }

    private async Task NotifyStoppedAsync(int generation, string message)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (generation != _streamGeneration)
                return;
            IsStreaming = false;
            Status = message;
        });
    }

    private async Task OnFrameAsync(byte[] jpeg, int generation)
    {
        if (generation != _streamGeneration)
            return;

        Bitmap bitmap;
        try
        {
            await using var ms = new MemoryStream(jpeg, writable: false);
            bitmap = new Bitmap(ms);
        }
        catch
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (generation != _streamGeneration)
            {
                bitmap.Dispose();
                return;
            }

            var old = Frame;
            Frame = bitmap;
            old?.Dispose();
            StreamMode = _heardCameraAudio != 0 ? "MJPEG + audio" : "MJPEG";
            if (!IsStreaming)
                IsStreaming = true;
            if (Status.StartsWith("Starting", StringComparison.Ordinal) ||
                Status.StartsWith("Not connected", StringComparison.Ordinal))
                Status = "Live view running. Click the image to center, or use the pad / WASD.";
        });
    }
}
