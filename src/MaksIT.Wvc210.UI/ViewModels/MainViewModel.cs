using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaksIT.Wvc210.Shared;
using MaksIT.Wvc210.Client;

namespace MaksIT.Wvc210.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly CameraClient _client = new();

    [ObservableProperty] private string _host = "";
    [ObservableProperty] private string _httpPort = "80";
    [ObservableProperty] private string _rtspPort = "554";
    [ObservableProperty] private string _username = "admin";
    [ObservableProperty] private string _password = "admin";
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _connectionStatus = "Disconnected";
    [ObservableProperty] private string _deviceSummary = "Cisco WVC210";
    [ObservableProperty] private string _selectedNav = "Live";
    [ObservableProperty] private bool _connectionPanelOpen = true;

    public bool IsLiveSelected => SelectedNav == "Live";
    public bool IsSetupSelected => SelectedNav == "Setup";
    public bool IsStatusSelected => SelectedNav == "Status";
    public bool ShowConnectionFields => !IsConnected || ConnectionPanelOpen;
    public bool IsConnectionError => ConnectionStatus.StartsWith("Failed", StringComparison.OrdinalIgnoreCase);

    public MainViewModel()
    {
        Live = new LiveViewModel(_client, TalkSettings);
        Setup = new SetupViewModel(_client, TalkSettings);
        StatusPage = new StatusViewModel(_client);
        var settings = SettingsStore.Load();
        Host = settings.Host;
        HttpPort = settings.HttpPort.ToString();
        RtspPort = settings.RtspPort.ToString();
        Username = settings.Username;
        Password = settings.Password;
        Live.PanStep = Math.Clamp(settings.PanStep, 1, 30);
        Live.RestoreStream(settings.LiveStream);
        Live.RestoreDayNight(settings.DayNight);
        Live.RestorePresets(settings.Presets, settings.UserHome);
        TalkSettings.Restore(settings.MicrophoneId);
        TalkSettings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(LocalTalkSettings.SelectedMicrophone))
                Persist();
        };
        Live.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(LiveViewModel.SelectedStream)
                or nameof(LiveViewModel.SelectedDayNight))
                Persist();
        };
        Live.SettingsChanged += (_, _) => Persist();

        if (settings.AutoConnect && !string.IsNullOrWhiteSpace(Host))
            _ = ConnectAsync();
        else
            ConnectionPanelOpen = true;
    }

    [RelayCommand]
    private void ToggleConnectionPanel() => ConnectionPanelOpen = !ConnectionPanelOpen;

    public LiveViewModel Live { get; }
    public SetupViewModel Setup { get; }
    public StatusViewModel StatusPage { get; }
    public LocalTalkSettings TalkSettings { get; } = new();

    [RelayCommand]
    private async Task ConnectAsync()
    {
        IsBusy = true;
        ConnectionStatus = "Connecting…";
        try
        {
            var httpPort = int.TryParse(HttpPort, out var hp) ? hp : 80;
            var rtspPort = int.TryParse(RtspPort, out var rp) ? rp : 554;
            _client.Configure(Host, httpPort, rtspPort, Username, Password);
            var query = await _client.QueryAsync().ConfigureAwait(true);
            var info = await _client.SysInfoAsync().ConfigureAwait(true);
            query.TryGetValue("hostname", out var hostname);
            query.TryGetValue("model_number", out var model);
            var firmware = info.Split('\n').FirstOrDefault(l => l.StartsWith("Firmware", StringComparison.OrdinalIgnoreCase))?.Trim();
            DeviceSummary = $"{model ?? "WVC210"}  ·  {hostname ?? Host}  ·  {firmware ?? ""}".Trim(' ', '·');
            IsConnected = true;
            ConnectionPanelOpen = false;
            ConnectionStatus = "Connected";
            Persist();
            await Live.StartAsync().ConfigureAwait(true);
            await StatusPage.RefreshAsync().ConfigureAwait(true);
            if (SelectedNav == "Setup")
                await Setup.LoadSelectedAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ConnectionStatus = "Failed: " + ex.Message;
            Live.Stop();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Disconnect()
    {
        Live.Stop();
        IsConnected = false;
        ConnectionPanelOpen = true;
        ConnectionStatus = "Disconnected";
        DeviceSummary = "Cisco WVC210";
    }

    partial void OnSelectedNavChanged(string value)
    {
        OnPropertyChanged(nameof(IsLiveSelected));
        OnPropertyChanged(nameof(IsSetupSelected));
        OnPropertyChanged(nameof(IsStatusSelected));
    }

    partial void OnIsConnectedChanged(bool value)
        => OnPropertyChanged(nameof(ShowConnectionFields));

    partial void OnConnectionPanelOpenChanged(bool value)
        => OnPropertyChanged(nameof(ShowConnectionFields));

    partial void OnConnectionStatusChanged(string value)
        => OnPropertyChanged(nameof(IsConnectionError));

    [RelayCommand]
    private void ShowLive()
    {
        SelectedNav = "Live";
        if (!IsConnected)
            return;
        if (!Live.IsStreaming)
            _ = Live.StartAsync();
        else
        {
            Live.TryResumeMpegListen();
            _ = Live.RefreshLiveSettingsAsync();
        }
    }

    [RelayCommand]
    private void ShowSetup()
    {
        SelectedNav = "Setup";
        if (IsConnected)
            _ = Setup.LoadSelectedAsync();
    }

    [RelayCommand]
    private void ShowStatus()
    {
        SelectedNav = "Status";
        if (IsConnected)
            _ = StatusPage.RefreshAsync();
    }

    public void Shutdown()
    {
        Persist();
        Live.Stop();
        _client.Dispose();
    }

    public async Task HandlePtzKeyAsync(string direction)
    {
        if (IsLiveSelected && IsConnected)
            await Live.MoveAsync(direction).ConfigureAwait(true);
    }

    private void Persist()
    {
        SettingsStore.Save(new AppSettings
        {
            Host = Host,
            HttpPort = int.TryParse(HttpPort, out var hp) ? hp : 80,
            RtspPort = int.TryParse(RtspPort, out var rp) ? rp : 554,
            Username = Username,
            Password = Password,
            AutoConnect = true,
            PanStep = (int)Math.Clamp(Live.PanStep, 1, 30),
            MicrophoneId = TalkSettings.SelectedMicrophone?.Id ?? "",
            LiveStream = Live.SelectedStream.Kind.ToString(),
            DayNight = Live.SelectedDayNight.Mode.ToString(),
            UserHome = Live.UserHome,
            Presets = Live.ExportPresets()
        });
    }
}
