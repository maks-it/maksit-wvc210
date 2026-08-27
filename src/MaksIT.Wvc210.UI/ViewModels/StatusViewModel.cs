using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.Json;
using MaksIT.Wvc210.Shared;
using MaksIT.Wvc210.Client;

namespace MaksIT.Wvc210.UI.ViewModels;

public partial class StatusViewModel : ViewModelBase
{
    private readonly CameraClient _client;

    [ObservableProperty] private string _sysInfo = "";
    [ObservableProperty] private string _capabilities = "";
    [ObservableProperty] private string _logs = "";
    [ObservableProperty] private string _decodedConfig = "";
    [ObservableProperty] private string _cameraClock = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _confirmFactoryReset;
    [ObservableProperty] private bool _confirmFirmware;
    [ObservableProperty] private bool _confirmRestore;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _cloneHost = "";
    [ObservableProperty] private string _cloneHttpPort = "80";
    [ObservableProperty] private string _cloneUsername = "admin";
    [ObservableProperty] private string _clonePassword = "admin";
    [ObservableProperty] private bool _cloneInstallFirmware = true;
    [ObservableProperty] private bool _cloneInstallConfig = true;
    [ObservableProperty] private bool _cloneForceModel;
    [ObservableProperty] private string _lastFirmwarePath = "";

    public bool HasFirmwarePath => !string.IsNullOrWhiteSpace(LastFirmwarePath);

    public StatusViewModel(CameraClient client)
    {
        _client = client;
    }

    partial void OnLastFirmwarePathChanged(string value)
        => OnPropertyChanged(nameof(HasFirmwarePath));

    public async Task RefreshAsync()
    {
        if (!_client.IsConfigured) return;
        IsBusy = true;
        try
        {
            var info = await _client.SysInfoAsync().ConfigureAwait(true);
            var query = await _client.QueryAsync().ConfigureAwait(true);
            var date = await _client.GetDateAsync().ConfigureAwait(true);
            SysInfo = info.Trim();
            Capabilities = string.Join(Environment.NewLine, query.Select(kv => $"{kv.Key}={kv.Value}"));
            CameraClock = date.TryGetValue("year", out _)
                ? $"{Get(date, "year")}-{Get(date, "month")}-{Get(date, "day")} {Get(date, "hour")}:{Get(date, "minute")}:{Get(date, "second")}  (tz {Get(date, "timezone")})"
                : string.Join(" ", date.Select(kv => $"{kv.Key}={kv.Value}"));
            Status = "Status refreshed.";
        }
        catch (Exception ex)
        {
            Status = "Refresh failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task ReloadAsync() => RefreshAsync();

    [RelayCommand]
    private async Task LoadLogsAsync()
    {
        if (!_client.IsConfigured) return;
        try
        {
            Logs = (await _client.GetLogsAsync().ConfigureAwait(true)).Trim();
            Status = "Logs loaded.";
        }
        catch (Exception ex)
        {
            Status = "Log download failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task SyncClockAsync()
    {
        if (!_client.IsConfigured) return;
        try
        {
            var result = await _client.SetDateAsync(DateTime.Now).ConfigureAwait(true);
            Status = result.Contains("OK", StringComparison.OrdinalIgnoreCase)
                ? "Camera clock set from this PC."
                : result.Trim();
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Status = "Clock sync failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task RebootAsync()
    {
        if (!_client.IsConfigured) return;
        try
        {
            await _client.RebootAsync().ConfigureAwait(true);
            Status = "Reboot command sent. Wait about 60 seconds, then reconnect.";
        }
        catch (Exception ex)
        {
            Status = "Reboot failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private void RequestFactoryReset() => ConfirmFactoryReset = true;

    [RelayCommand]
    private void CancelFactoryReset() => ConfirmFactoryReset = false;

    [RelayCommand]
    private async Task FactoryResetAsync()
    {
        if (!_client.IsConfigured) return;
        try
        {
            await _client.FactoryResetAsync().ConfigureAwait(true);
            ConfirmFactoryReset = false;
            Status = "Factory reset sent. The camera will reboot with default settings.";
        }
        catch (Exception ex)
        {
            Status = "Factory reset failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        if (!_client.IsConfigured) return;
        try
        {
            var encoded = await _client.DownloadConfigAsync().ConfigureAwait(true);
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Wvc210");
            Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var cfgPath = Path.Combine(dir, $"wvc210-{stamp}.cfg");
            var txtPath = Path.Combine(dir, $"wvc210-{stamp}.txt");
            await File.WriteAllBytesAsync(cfgPath, encoded).ConfigureAwait(true);
            try
            {
                DecodedConfig = SercommBase64.DecodeToString(encoded);
                await File.WriteAllTextAsync(txtPath, DecodedConfig).ConfigureAwait(true);
                Status = $"Saved {cfgPath} and decoded {txtPath}";
            }
            catch (Exception decodeEx)
            {
                DecodedConfig = "(could not decode: " + decodeEx.Message + ")";
                Status = $"Saved {cfgPath}";
            }
        }
        catch (Exception ex)
        {
            Status = "Config download failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private void OpenWebUi()
    {
        if (!_client.IsConfigured) return;
        ExternalApps.OpenUrl(_client.WebUiUrl);
    }

    [RelayCommand]
    private void RequestRestore() => ConfirmRestore = true;

    [RelayCommand]
    private void CancelRestore() => ConfirmRestore = false;

    [RelayCommand]
    private async Task RestoreConfigAsync()
    {
        if (!_client.IsConfigured) return;
        var file = await PickFileAsync("Restore camera configuration", "cfg").ConfigureAwait(true);
        if (file is null)
        {
            ConfirmRestore = false;
            return;
        }

        try
        {
            await using var stream = await file.OpenReadAsync().ConfigureAwait(true);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(true);
            await _client.UploadConfigAsync(ms.ToArray()).ConfigureAwait(true);
            ConfirmRestore = false;
            Status = "Configuration uploaded. The camera will reboot; wait about a minute and reconnect.";
        }
        catch (Exception ex)
        {
            Status = "Config restore failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private void RequestFirmware() => ConfirmFirmware = true;

    [RelayCommand]
    private void CancelFirmware() => ConfirmFirmware = false;

    [RelayCommand]
    private async Task UpgradeFirmwareAsync()
    {
        if (!_client.IsConfigured) return;
        var file = await PickFileAsync("Upgrade firmware", "bin", "img", "fw").ConfigureAwait(true);
        if (file is null)
        {
            ConfirmFirmware = false;
            return;
        }

        try
        {
            Status = "Uploading firmware… keep the camera powered on.";
            await using var stream = await file.OpenReadAsync().ConfigureAwait(true);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(true);
            await _client.UpgradeFirmwareAsync(ms.ToArray()).ConfigureAwait(true);
            ConfirmFirmware = false;
            Status = "Firmware upload finished. Wait at least 5 minutes before reconnecting.";
        }
        catch (Exception ex)
        {
            Status = "Firmware upgrade failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task DumpFirmwareAsync()
    {
        if (!_client.IsConfigured) return;
        IsBusy = true;
        try
        {
            Status = "Requesting firmware image from the camera…";
            var progress = new Progress<string>(msg => Status = msg);
            var dump = await _client.DumpFirmwareAsync(progress).ConfigureAwait(true);
            var dir = CloneDir();
            var identity = await _client.IdentifyAsync().ConfigureAwait(true);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var safeFw = Sanitize(identity.Firmware);
            var path = Path.Combine(dir, $"wvc210-{safeFw}-{stamp}-{dump.SuggestedFileName}");
            await File.WriteAllBytesAsync(path, dump.Data).ConfigureAwait(true);
            LastFirmwarePath = path;
            await File.WriteAllTextAsync(path + ".json", JsonSerializer.Serialize(new
            {
                model = identity.Model,
                host = identity.HostName,
                firmware = identity.Firmware,
                serial = identity.Serial,
                source = dump.SourcePath,
                bytes = dump.Data.Length,
                dumpedAt = DateTime.Now.ToString("o")
            }, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(true);
            Status = $"Firmware dump saved ({dump.Data.Length:N0} bytes) from {dump.SourcePath} → {path}. Flash this only onto another WVC210.";
        }
        catch (Exception ex)
        {
            Status = "Firmware dump failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveCloneKitAsync()
    {
        if (!_client.IsConfigured) return;
        try
        {
            var identity = await _client.IdentifyAsync().ConfigureAwait(true);
            var cfg = await _client.DownloadConfigAsync().ConfigureAwait(true);
            var dir = CloneDir();
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var cfgPath = Path.Combine(dir, $"wvc210-clone-{stamp}.cfg");
            var metaPath = Path.Combine(dir, $"wvc210-clone-{stamp}.json");
            await File.WriteAllBytesAsync(cfgPath, cfg).ConfigureAwait(true);
            await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(new
            {
                model = identity.Model,
                host = identity.HostName,
                firmware = identity.Firmware,
                serial = identity.Serial,
                configFile = Path.GetFileName(cfgPath),
                firmwareDump = string.IsNullOrWhiteSpace(LastFirmwarePath) ? null : Path.GetFileName(LastFirmwarePath),
                dumpedAt = DateTime.Now.ToString("o"),
                note = "Config clone for another WVC210. Firmware image is separate — dump it if the camera exposes it, or use a matching WVC210 firmware file."
            }, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(true);
            try
            {
                DecodedConfig = SercommBase64.DecodeToString(cfg);
            }
            catch
            {
                // keep previous decoded text
            }
            Status = $"Clone kit saved: {cfgPath} (source {identity.Firmware}).";
        }
        catch (Exception ex)
        {
            Status = "Clone kit failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task InstallOnTargetAsync()
    {
        if (!_client.IsConfigured) return;
        if (string.IsNullOrWhiteSpace(CloneHost))
        {
            Status = "Enter the other camera’s host or IP.";
            return;
        }

        if (!CloneInstallFirmware && !CloneInstallConfig)
        {
            Status = "Choose firmware, config, or both.";
            return;
        }

        byte[]? firmware = null;
        if (CloneInstallFirmware)
        {
            if (!string.IsNullOrWhiteSpace(LastFirmwarePath) && File.Exists(LastFirmwarePath))
            {
                firmware = await File.ReadAllBytesAsync(LastFirmwarePath).ConfigureAwait(true);
            }
            else
            {
                var file = await PickFileAsync("Firmware image for the other WVC210", "bin", "img", "fw", "tgz").ConfigureAwait(true);
                if (file is null)
                {
                    Status = "No firmware file selected.";
                    return;
                }

                await using var stream = await file.OpenReadAsync().ConfigureAwait(true);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms).ConfigureAwait(true);
                firmware = ms.ToArray();
            }
        }

        IsBusy = true;
        CameraClient? target = null;
        try
        {
            var port = int.TryParse(CloneHttpPort, out var hp) ? hp : 80;
            target = new CameraClient();
            target.Configure(CloneHost, port, 554, CloneUsername, ClonePassword);
            Status = "Contacting target camera…";
            var identity = await target.IdentifyAsync().ConfigureAwait(true);
            var modelOk = identity.Model.Contains("WVC210", StringComparison.OrdinalIgnoreCase)
                          || identity.Model.Contains("WVC200", StringComparison.OrdinalIgnoreCase);
            if (!modelOk && !CloneForceModel)
            {
                Status = $"Target reports model '{identity.Model}'. Refusing to flash a non-WVC210. Tick “Allow other model” only if you are sure.";
                return;
            }

            if (firmware is { Length: > 0 })
            {
                Status = $"Uploading firmware to {identity.HostName} ({identity.Model} {identity.Firmware})… keep it powered on.";
                await target.UpgradeFirmwareAsync(firmware).ConfigureAwait(true);
                if (CloneInstallConfig)
                {
                    Status = "Firmware sent. Wait 5 minutes for the target to reboot, then click Install again with only “Copy settings” checked.";
                    return;
                }

                Status = "Firmware uploaded. Wait at least 5 minutes before using the other camera.";
                return;
            }

            Status = "Uploading configuration to the target…";
            var cfg = await _client.DownloadConfigAsync().ConfigureAwait(true);
            await target.UploadConfigAsync(cfg).ConfigureAwait(true);
            Status = $"Settings copied to {CloneHost}. The target will reboot; wait about a minute.";
        }
        catch (Exception ex)
        {
            Status = "Install on target failed: " + ex.Message;
        }
        finally
        {
            target?.Dispose();
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ChooseFirmwareFileAsync()
    {
        var file = await PickFileAsync("Remember firmware file for clone", "bin", "img", "fw", "tgz").ConfigureAwait(true);
        if (file is null)
            return;
        LastFirmwarePath = file.TryGetLocalPath() ?? file.Path.LocalPath;
        Status = "Will use firmware file " + LastFirmwarePath;
    }

    private static string CloneDir()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Wvc210");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Sanitize(string value)
    {
        var chars = Path.GetInvalidFileNameChars();
        var clean = new string((value ?? "fw").Select(c => chars.Contains(c) ? '-' : c).ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "fw" : clean.Replace(" ", "");
    }

    private static async Task<IStorageFile?> PickFileAsync(string title, params string[] extensions)
    {
        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (window is null)
            return null;
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(title)
                {
                    Patterns = extensions.Select(e => "*." + e).ToArray()
                },
                FilePickerFileTypes.All
            ]
        }).ConfigureAwait(true);
        return files.Count > 0 ? files[0] : null;
    }

    private static string Get(Dictionary<string, string> map, string key)
        => map.TryGetValue(key, out var value) ? value : "";
}
