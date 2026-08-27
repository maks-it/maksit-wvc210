using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace MaksIT.Wvc210.Client;

public interface IPcmPlayer : IDisposable
{
    bool Muted { get; set; }
    void Write(short[] pcm);
}

public static class PcmPlayer
{
    public static IPcmPlayer? TryCreate()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return new WindowsPcmPlayer();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return new LinuxPcmPlayer();
        }
        catch
        {
            return null;
        }

        return null;
    }
}

internal sealed class WindowsPcmPlayer : IPcmPlayer
{
    private readonly BufferedWaveProvider _buffer;
    private readonly WaveOutEvent _output;
    private readonly object _gate = new();

    public WindowsPcmPlayer()
    {
        _buffer = new BufferedWaveProvider(new WaveFormat(8000, 16, 1))
        {
            BufferDuration = TimeSpan.FromMilliseconds(800),
            DiscardOnBufferOverflow = true
        };
        _output = new WaveOutEvent { DesiredLatency = 120 };
        _output.Init(_buffer);
        _output.Play();
    }

    public bool Muted { get; set; }

    public void Write(short[] pcm)
    {
        if (Muted || pcm.Length == 0)
            return;
        var bytes = new byte[pcm.Length * 2];
        Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);
        lock (_gate)
            _buffer.AddSamples(bytes, 0, bytes.Length);
    }

    public void Dispose()
    {
        try { _output.Stop(); } catch { }
        _output.Dispose();
    }
}

internal sealed class LinuxPcmPlayer : IPcmPlayer
{
    private readonly Process _process;
    private readonly Stream _stdin;
    private readonly object _gate = new();

    public LinuxPcmPlayer()
    {
        _process = Process.Start(new ProcessStartInfo
        {
            FileName = "aplay",
            Arguments = "-q -t raw -r 8000 -f S16_LE -c 1",
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Could not start aplay.");
        _stdin = _process.StandardInput.BaseStream;
    }

    public bool Muted { get; set; }

    public void Write(short[] pcm)
    {
        if (Muted || pcm.Length == 0)
            return;
        var bytes = new byte[pcm.Length * 2];
        Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);
        lock (_gate)
        {
            try { _stdin.Write(bytes, 0, bytes.Length); }
            catch { /* aplay closed */ }
        }
    }

    public void Dispose()
    {
        try { _stdin.Dispose(); } catch { }
        try
        {
            if (!_process.HasExited)
                _process.Kill();
        }
        catch { }
        _process.Dispose();
    }
}
