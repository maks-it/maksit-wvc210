using System.Diagnostics;
using MaksIT.Wvc210.Shared;

namespace MaksIT.Wvc210.Client;

public sealed class LinuxMicrophoneCapture : IMicrophoneCapture
{
    private readonly string _deviceId;
    private Process? _process;
    private CancellationTokenSource? _cts;

    public LinuxMicrophoneCapture(string deviceId)
    {
        _deviceId = deviceId;
    }

    public event Action<short[]>? Pcm8000Mono;

    public static IReadOnlyList<MicrophoneDevice> ListDevices()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "arecord",
                Arguments = "-l",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (proc is null)
                return [new MicrophoneDevice("default", "Default")];
            var text = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
            proc.WaitForExit(3000);
            var devices = new List<MicrophoneDevice> { new("default", "Default") };
            foreach (var line in text.Split('\n'))
            {
                // card 0: PCH [HDA Intel PCH], device 0: ALC256 Analog [ALC256 Analog]
                if (!line.TrimStart().StartsWith("card ", StringComparison.Ordinal))
                    continue;
                var cardIdx = line.IndexOf("card ", StringComparison.Ordinal);
                var devIdx = line.IndexOf("device ", StringComparison.Ordinal);
                if (cardIdx < 0 || devIdx < 0)
                    continue;
                var card = new string(line[(cardIdx + 5)..].TakeWhile(char.IsDigit).ToArray());
                var device = new string(line[(devIdx + 7)..].TakeWhile(char.IsDigit).ToArray());
                if (card.Length == 0 || device.Length == 0)
                    continue;
                var id = $"plughw:{card},{device}";
                devices.Add(new MicrophoneDevice(id, $"{id} {line.Trim()}"));
            }

            return devices;
        }
        catch
        {
            return [new MicrophoneDevice("default", "Default")];
        }
    }

    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        var args = $"-D \"{_deviceId}\" -r 8000 -f S16_LE -c 1 -t raw -q";
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "arecord",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        _process.Start();
        var ct = _cts.Token;
        var stream = _process.StandardOutput.BaseStream;
        _ = Task.Run(() => ReadLoop(stream, ct), ct);
    }

    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            if (_process is { HasExited: false })
                _process.Kill();
        }
        catch { }
        _process?.Dispose();
        _process = null;
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose() => Stop();

    private async Task ReadLoop(Stream stream, CancellationToken ct)
    {
        var buffer = new byte[1600];
        while (!ct.IsCancellationRequested)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
            if (n <= 1)
                break;
            n -= n % 2;
            var samples = new short[n / 2];
            Buffer.BlockCopy(buffer, 0, samples, 0, n);
            Pcm8000Mono?.Invoke(samples);
        }
    }
}
