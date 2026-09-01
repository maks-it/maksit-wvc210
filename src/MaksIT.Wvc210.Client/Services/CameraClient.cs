using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using MaksIT.Wvc210.Shared;


namespace MaksIT.Wvc210.Client;

public sealed class CameraClient : IDisposable
{
    private readonly object _gate = new();
    private HttpClient? _http;
    private string _host = "";
    private int _httpPort = 80;
    private int _rtspPort = 554;
    private string _username = "admin";
    private string _password = "admin";
    private string _colorBeforeNight = DayNightChoice.ColorAuto;

    public bool IsConfigured => _http is not null;
    public string Host => _host;
    public int HttpPort => _httpPort;
    public int RtspPort => _rtspPort;
    public string Username => _username;

    public Uri BaseUri
    {
        get
        {
            var builder = new UriBuilder("http", _host, _httpPort);
            return builder.Uri;
        }
    }

    public string WebUiUrl => BaseUri.ToString();

    public string AsfUrl => BuildMediaUrl("http", _httpPort, "/img/video.asf");
    public string RtspUrl => BuildMediaUrl("rtsp", _rtspPort, "/img/media.sav");
    public string RtspVideoUrl => BuildMediaUrl("rtsp", _rtspPort, "/img/video.sav");
    public string MjpegUrl => new Uri(BaseUri, "/img/video.mjpeg").ToString();
    public string SnapshotUrl => new Uri(BaseUri, "/img/snapshot.cgi").ToString();

    public void Configure(string host, int httpPort, int rtspPort, string username, string password)
    {
        lock (_gate)
        {
            _http?.Dispose();
            _host = host.Trim();
            _httpPort = httpPort <= 0 ? 80 : httpPort;
            _rtspPort = rtspPort <= 0 ? 554 : rtspPort;
            _username = username;
            _password = password;

            var credentials = new NetworkCredential(username, password);
            var cache = new CredentialCache
            {
                { BaseUri, "Basic", credentials },
                { BaseUri, "Digest", credentials }
            };

            var handler = new SocketsHttpHandler
            {
                Credentials = cache,
                PreAuthenticate = true,
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.None,
                ConnectTimeout = TimeSpan.FromSeconds(8),
                PooledConnectionLifetime = TimeSpan.FromSeconds(45),
                MaxConnectionsPerServer = 6
            };

            var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            _http = new HttpClient(handler, disposeHandler: true)
            {
                BaseAddress = BaseUri,
                Timeout = TimeSpan.FromSeconds(20),
                DefaultRequestVersion = HttpVersion.Version11,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
            };
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("MaksIT.Wvc210/1.0");
            _http.DefaultRequestHeaders.ExpectContinue = false;
        }
    }

    public async Task<Dictionary<string, string>> QueryAsync(CancellationToken ct = default)
        => ParsePairs(await GetStringAsync("/util/query.cgi", ct).ConfigureAwait(false));

    public Task<string> SysInfoAsync(CancellationToken ct = default)
        => GetStringAsync("/adm/sysinfo.cgi", ct);

    public Task<string> GetLogsAsync(CancellationToken ct = default)
        => GetStringAsync("/adm/log.cgi", ct);

    public Task<string> RebootAsync(CancellationToken ct = default)
        => GetStringAsync("/adm/reboot.cgi", ct);

    public Task<string> FactoryResetAsync(CancellationToken ct = default)
        => GetStringAsync("/adm/reset_to_default.cgi", ct);

    public Task<byte[]> DownloadConfigAsync(CancellationToken ct = default)
        => GetBytesAsync("/adm/admcfg.cfg", ct);

    public Task<byte[]> SnapshotAsync(CancellationToken ct = default)
        => GetBytesAsync("/img/snapshot.cgi", ct);

    public async Task<Dictionary<string, string>> GetDateAsync(CancellationToken ct = default)
        => ParsePairs(await GetStringAsync("/adm/date.cgi?action=get", ct).ConfigureAwait(false));

    public Task<string> SetDateAsync(DateTime dt, CancellationToken ct = default)
    {
        var q =
            $"action=set&year={dt.Year}&month={dt.Month}&day={dt.Day}" +
            $"&hour={dt.Hour}&minute={dt.Minute}&second={dt.Second}";
        return GetStringAsync("/adm/date.cgi?" + q, ct);
    }

    public async Task<Dictionary<string, string>> GetGroupAsync(string group, CancellationToken ct = default)
    {
        var body = await GetStringAsync($"/adm/get_group.cgi?group={Uri.EscapeDataString(group)}", ct)
            .ConfigureAwait(false);
        return ParsePairs(body);
    }

    public async Task<string> SetGroupAsync(string group, IReadOnlyDictionary<string, string> values, CancellationToken ct = default)
    {
        var sb = new StringBuilder("/adm/set_group.cgi?group=").Append(Uri.EscapeDataString(group));
        foreach (var (key, value) in values)
        {
            sb.Append('&').Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value ?? ""));
        }

        return await GetStringAsync(sb.ToString(), ct).ConfigureAwait(false);
    }

    public async Task<DayNightMode> GetDayNightAsync(CancellationToken ct = default)
    {
        var video = await GetGroupAsync("VIDEO", ct).ConfigureAwait(false);
        var color = video.GetValueOrDefault("color", DayNightChoice.ColorAuto);
        if (color is not DayNightChoice.ColorBlackWhite)
            _colorBeforeNight = color;
        return DayNightChoice.FromVideo(video);
    }

    public async Task SetDayNightAsync(DayNightMode mode, CancellationToken ct = default)
    {
        var video = await GetGroupAsync("VIDEO", ct).ConfigureAwait(false);
        DayNightChoice.ApplyToVideo(video, mode, ref _colorBeforeNight);
        await SetGroupAsync("VIDEO", new Dictionary<string, string>
        {
            ["color"] = video.GetValueOrDefault("color", DayNightChoice.ColorAuto)
        }, ct).ConfigureAwait(false);
    }

    public Task PanTiltAsync(string direction, int degree = 8, CancellationToken ct = default)
    {
        degree = Math.Clamp(degree, 1, 30);
        return GetStringAsync($"/pt/ptctrl.cgi?mv={Uri.EscapeDataString(direction)},{degree}", ct);
    }

    public Task MoveToPositionAsync(int x, int y, CancellationToken ct = default)
        => GetStringAsync($"/pt/ptctrl.cgi?position={x},{y}", ct);

    public Task HomeAsync(CancellationToken ct = default)
        => GetStringAsync("/pt/ptctrl.cgi?mv=H", ct);

    public Task RecalibrateAsync(CancellationToken ct = default)
        => GetStringAsync("/pt/ptctrl.cgi?mv=X", ct);

    public Task UserHomeAsync(CancellationToken ct = default)
        => GetStringAsync("/pt/ptctrl.cgi?preset=move,103", ct);

    public Task SetUserHomeAsync(CancellationToken ct = default)
        => GetStringAsync("/pt/ptctrl.cgi?preset=set,103", ct);

    public Task AutoPanAsync(CancellationToken ct = default)
        => GetStringAsync("/pt/ptctrl.cgi?preset=move,102", ct);

    public Task PatrolAsync(CancellationToken ct = default)
        => GetStringAsync("/pt/ptctrl.cgi?preset=move,101", ct);

    public Task MotionPositionAsync(CancellationToken ct = default)
        => GetStringAsync("/pt/ptctrl.cgi?preset=move,100", ct);

    public Task PresetMoveAsync(int index, CancellationToken ct = default)
        => GetStringAsync($"/pt/ptctrl.cgi?preset=move,{index}", ct);

    public Task PresetSetAsync(int index, CancellationToken ct = default)
        => GetStringAsync($"/pt/ptctrl.cgi?preset=set,{index}", ct);

    public async Task ClearPresetSlotAsync(int index, CancellationToken ct = default)
    {
        var ptz = await GetGroupAsync("PTZ", ct).ConfigureAwait(false);
        ptz["Preset" + index + "Name"] = "";
        ptz["Preset" + index + "Position"] = "";
        PresetOccupancy.SyncPatrolSequence(ptz);
        await SetGroupAsync("PTZ", ptz, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the already-loaded PTZ group only when local backup has
    /// coordinates the camera is missing (NVRAM wipe). No-op when the
    /// camera already holds those slots.
    /// </summary>
    /// <returns><see langword="true"/> when the PTZ group was written.</returns>
    public async Task<bool> RestoreMissingPresetsAsync(
        Dictionary<string, string> ptz,
        IReadOnlyList<SavedPreset> local,
        string? userHome,
        CancellationToken ct = default)
    {
        if (!PresetOccupancy.ApplyLocalBackup(ptz, local, userHome))
            return false;
        await SetGroupAsync("PTZ", ptz, ct).ConfigureAwait(false);
        return true;
    }

    public async Task<Dictionary<string, string>> GetPresetsAsync(CancellationToken ct = default)
        => ParsePairs(await GetStringAsync("/pt/ptctrl.cgi?preset=all", ct).ConfigureAwait(false));

    public async Task<string> GetStringAsync(string relativeUrl, CancellationToken ct = default)
    {
        var bytes = await GetBytesAsync(relativeUrl, ct).ConfigureAwait(false);
        return Encoding.ASCII.GetString(bytes);
    }

    public async Task<byte[]> GetBytesAsync(string relativeUrl, CancellationToken ct = default)
    {
        var http = RequireHttp();
        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        request.Version = HttpVersion.Version11;
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new CameraException($"{relativeUrl} failed ({(int)response.StatusCode}): {Trim(Encoding.ASCII.GetString(bytes))}");
        }

        return bytes;
    }

    public static Dictionary<string, string> ParsePairs(string body)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var reader = new StringReader(body ?? "");
        while (reader.ReadLine() is { } line)
        {
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith('[') || line.StartsWith('<'))
                continue;
            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..];
            map[key] = value;
        }

        return map;
    }

    public async Task<string> UploadFileAsync(string relativeUrl, string fileName, byte[] data, TimeSpan timeout, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(data);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", fileName);

        using var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(8),
            AllowAutoRedirect = true
        };
        using var http = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = BaseUri,
            Timeout = timeout,
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_username}:{_password}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        http.DefaultRequestHeaders.ExpectContinue = false;

        using var request = new HttpRequestMessage(HttpMethod.Post, relativeUrl) { Content = content };
        request.Version = HttpVersion.Version11;
        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new CameraException($"{relativeUrl} failed ({(int)response.StatusCode}): {Trim(body)}");
        return body;
    }

    public Task<string> UploadConfigAsync(byte[] cfg, CancellationToken ct = default)
        => UploadFileAsync("/adm/upload.cgi", "admcfg.cfg", cfg, TimeSpan.FromMinutes(2), ct);

    public Task<string> UpgradeFirmwareAsync(byte[] firmware, CancellationToken ct = default)
        => UploadFileAsync("/adm/upgrade.cgi", "firmware.bin", firmware, TimeSpan.FromMinutes(6), ct);

    public async Task<CameraIdentity> IdentifyAsync(CancellationToken ct = default)
    {
        var query = await QueryAsync(ct).ConfigureAwait(false);
        var info = await SysInfoAsync(ct).ConfigureAwait(false);
        query.TryGetValue("model_number", out var model);
        query.TryGetValue("hostname", out var hostname);
        var firmware = info.Split('\n')
            .FirstOrDefault(l => l.StartsWith("Firmware", StringComparison.OrdinalIgnoreCase))?.Trim() ?? "";
        var serial = info.Split('\n')
            .FirstOrDefault(l => l.Contains("Serial", StringComparison.OrdinalIgnoreCase))?.Trim() ?? "";
        return new CameraIdentity
        {
            Model = model ?? "",
            HostName = hostname ?? _host,
            Firmware = firmware,
            Serial = serial
        };
    }

    public async Task<FirmwareDumpResult> DumpFirmwareAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        string[] paths =
        [
            "/adm/flash_dumper.cgi",
            "/adm/fw.bin",
            "/adm/firmware.bin",
            "/adm/firmware.cgi"
        ];

        var errors = new List<string>();
        foreach (var path in paths)
        {
            progress?.Report("Trying " + path);
            try
            {
                var dump = await TryGetFirmwareImageAsync(path, TimeSpan.FromMinutes(3), ct).ConfigureAwait(false);
                if (dump is not null)
                    return dump;
                errors.Add(path + " — not a firmware image");
            }
            catch (Exception ex)
            {
                errors.Add(path + " — " + ex.Message);
            }
        }

        throw new CameraException(
            "This WVC210 firmware does not expose a downloadable image (Cisco only documented firmware upload, not dump). " +
            "Tried: " + string.Join("; ", errors) +
            ". You can still clone settings to another WVC210, and flash it with a matching WVC210 .bin / .img / .tgz firmware file.");
    }

    private async Task<FirmwareDumpResult?> TryGetFirmwareImageAsync(string relativeUrl, TimeSpan timeout, CancellationToken ct)
    {
        using var http = CreateLongClient(timeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        request.Version = HttpVersion.Version11;
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new CameraException($"{relativeUrl} failed ({(int)response.StatusCode})");
        if (!LooksLikeFirmware(bytes))
            return null;

        var fileName = "fw.bin";
        var disposition = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        if (!string.IsNullOrWhiteSpace(disposition))
            fileName = disposition;

        return new FirmwareDumpResult
        {
            Data = bytes,
            SourcePath = relativeUrl,
            ContentType = response.Content.Headers.ContentType?.MediaType ?? "",
            SuggestedFileName = fileName
        };
    }

    private HttpClient CreateLongClient(TimeSpan timeout)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(8),
            AllowAutoRedirect = true
        };
        var http = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = BaseUri,
            Timeout = timeout,
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_username}:{_password}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        http.DefaultRequestHeaders.ExpectContinue = false;
        http.DefaultRequestHeaders.UserAgent.ParseAdd("MaksIT.Wvc210/1.0");
        return http;
    }

    private static bool LooksLikeFirmware(byte[] data)
    {
        if (data.Length < 64 * 1024)
            return false;
        var start = Encoding.ASCII.GetString(data, 0, Math.Min(16, data.Length)).TrimStart();
        if (start.StartsWith("<", StringComparison.Ordinal) ||
            start.StartsWith("{", StringComparison.Ordinal) ||
            start.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    public Task<OcxLiveConnection> OpenOcxLiveAsync(CancellationToken ct)
        => OpenRawGetAsync("/img/mjpeg.cgi", ocxClient: true, ct);

    public async Task<OcxLiveConnection> OpenRawGetAsync(string path, bool ocxClient, CancellationToken ct)
    {
        if (!IsConfigured)
            throw new CameraException("Not connected.");

        var tcp = new TcpClient { NoDelay = true, ReceiveBufferSize = 256 * 1024 };
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            await tcp.ConnectAsync(_host, _httpPort, timeout.Token).ConfigureAwait(false);
            var stream = tcp.GetStream();
            var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_username}:{_password}"));
            var userAgent = ocxClient ? "CameraActiveX Cisco210Viewer" : "MaksIT.Wvc210/1.0";
            var header =
                $"GET {path} HTTP/1.0\r\n" +
                $"Host: {_host}\r\n" +
                $"Authorization: Basic {token}\r\n" +
                $"User-Agent: {userAgent}\r\n" +
                "Connection: close\r\n" +
                "\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(header), ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);

            using var headerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            headerCts.CancelAfter(TimeSpan.FromSeconds(8));
            var leftover = await ReadHttpHeadersAsync(stream, headerCts.Token).ConfigureAwait(false);
            return new OcxLiveConnection(tcp, stream, leftover);
        }
        catch
        {
            tcp.Dispose();
            throw;
        }
    }

    private static async Task<byte[]> ReadHttpHeadersAsync(NetworkStream stream, CancellationToken ct)
    {
        var acc = new MemoryStream();
        var buf = new byte[1024];
        while (acc.Length < 8192)
        {
            var n = await stream.ReadAsync(buf.AsMemory(0, buf.Length), ct).ConfigureAwait(false);
            if (n <= 0)
                throw new CameraException("OCX live stream closed before headers.");
            acc.Write(buf, 0, n);
            var data = acc.ToArray();
            var split = IndexOfCrLfCrLf(data);
            if (split < 0)
                continue;

            var text = Encoding.ASCII.GetString(data, 0, split);
            if (text.Contains(" 401 ", StringComparison.Ordinal))
                throw new CameraException("OCX live stream unauthorized.");
            if (!text.Contains("200", StringComparison.Ordinal))
                throw new CameraException("OCX live stream failed: " + Trim(text));

            var bodyStart = split + 4;
            if (bodyStart >= data.Length)
                return [];
            var leftover = new byte[data.Length - bodyStart];
            Buffer.BlockCopy(data, bodyStart, leftover, 0, leftover.Length);
            return leftover;
        }

        throw new CameraException("OCX live stream headers were too large.");
    }

    private static int IndexOfCrLfCrLf(byte[] data)
    {
        for (var i = 0; i <= data.Length - 4; i++)
        {
            if (data[i] == 13 && data[i + 1] == 10 && data[i + 2] == 13 && data[i + 3] == 10)
                return i;
        }

        return -1;
    }

    public async Task<TalkUploadStream> OpenTalkUploadAsync(TalkCodec codec, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new CameraException("Not connected.");

        var upload = new TalkUploadStream(_host, _httpPort, _username, _password, codec);
        await upload.OpenAsync(ct).ConfigureAwait(false);
        return upload;
    }

    public async Task PostTalkClipAsync(byte[] data, int count, TalkCodec codec, CancellationToken ct = default)
    {
        if (count <= 0)
            return;

        using var upload = await OpenTalkUploadAsync(codec, ct).ConfigureAwait(false);
        await upload.WriteAsync(data.AsMemory(0, count), ct).ConfigureAwait(false);
    }

    private string BuildMediaUrl(string scheme, int port, string path)
    {
        var user = Uri.EscapeDataString(_username);
        var pass = Uri.EscapeDataString(_password);
        return $"{scheme}://{user}:{pass}@{_host}:{port}{path}";
    }

    private HttpClient RequireHttp()
        => _http ?? throw new CameraException("Not connected.");

    private static string Trim(string text)
    {
        text = (text ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length > 240 ? text[..240] + "…" : text;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _http?.Dispose();
            _http = null;
        }
    }
}
