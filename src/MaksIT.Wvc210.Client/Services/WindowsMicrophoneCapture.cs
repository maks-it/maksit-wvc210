using NAudio.CoreAudioApi;
using NAudio.Wave;
using MaksIT.Wvc210.Shared;

namespace MaksIT.Wvc210.Client;

public sealed class WindowsMicrophoneCapture : IMicrophoneCapture
{
    private readonly WasapiCapture _capture;
    private readonly WaveFormat _sourceFormat;

    public WindowsMicrophoneCapture(string deviceId)
    {
        using var enumerator = new MMDeviceEnumerator();
        var device = enumerator.GetDevice(deviceId);
        _capture = new WasapiCapture(device, true, 20);
        _sourceFormat = _capture.WaveFormat;
        _capture.DataAvailable += OnData;
    }

    public event Action<short[]>? Pcm8000Mono;
    public WaveFormat SourceFormat => _sourceFormat;

    public static IReadOnlyList<MicrophoneDevice> ListDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .Select(d => new MicrophoneDevice(d.ID, d.FriendlyName))
            .ToList();
    }

    public void Start() => _capture.StartRecording();

    public void Stop()
    {
        try { _capture.StopRecording(); } catch { /* already stopped */ }
    }

    public void Dispose()
    {
        _capture.DataAvailable -= OnData;
        try { _capture.StopRecording(); } catch { }
        _capture.Dispose();
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0)
            return;
        var mono = ToMono16(_sourceFormat, e.Buffer.AsSpan(0, e.BytesRecorded));
        if (mono.Length == 0)
            return;
        var resampled = PcmResampler.To8000(mono, _sourceFormat.SampleRate);
        if (resampled.Length > 0)
            Pcm8000Mono?.Invoke(resampled);
    }

    private static short[] ToMono16(WaveFormat format, ReadOnlySpan<byte> buffer)
    {
        var channels = Math.Max(1, format.Channels);
        if (IsIeeeFloat(format))
            return MixFloat32(buffer, channels);

        if (format.BitsPerSample == 32)
            return MixPcm32(buffer, channels);

        if (format.BitsPerSample == 24)
            return MixPcm24(buffer, channels);

        if (format.BitsPerSample == 16)
            return MixPcm16(buffer, channels);

        return [];
    }

    private static bool IsIeeeFloat(WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat)
            return true;
            if (format is WaveFormatExtensible ext)
                return ext.SubFormat == Guid.Parse("00000003-0000-0010-8000-00aa00389b71");
        return format.Encoding == WaveFormatEncoding.Extensible && format.BitsPerSample == 32;
    }

    private static short[] MixFloat32(ReadOnlySpan<byte> buffer, int channels)
    {
        var frames = buffer.Length / (4 * channels);
        var output = new short[frames];
        for (var i = 0; i < frames; i++)
        {
            float mix = 0;
            for (var c = 0; c < channels; c++)
                mix += BitConverter.ToSingle(buffer.Slice((i * channels + c) * 4, 4));
            mix = Math.Clamp(mix / channels, -1f, 1f);
            output[i] = (short)(mix * 32767f);
        }

        return output;
    }

    private static short[] MixPcm32(ReadOnlySpan<byte> buffer, int channels)
    {
        var frames = buffer.Length / (4 * channels);
        var output = new short[frames];
        for (var i = 0; i < frames; i++)
        {
            long mix = 0;
            for (var c = 0; c < channels; c++)
                mix += BitConverter.ToInt32(buffer.Slice((i * channels + c) * 4, 4));
            output[i] = (short)Math.Clamp(mix / channels / 65536, short.MinValue, short.MaxValue);
        }

        return output;
    }

    private static short[] MixPcm24(ReadOnlySpan<byte> buffer, int channels)
    {
        var stride = 3 * channels;
        var frames = buffer.Length / stride;
        var output = new short[frames];
        for (var i = 0; i < frames; i++)
        {
            int mix = 0;
            for (var c = 0; c < channels; c++)
            {
                var o = i * stride + c * 3;
                var sample = buffer[o] | (buffer[o + 1] << 8) | (buffer[o + 2] << 16);
                if ((sample & 0x800000) != 0)
                    sample |= unchecked((int)0xFF000000);
                mix += sample;
            }

            output[i] = (short)Math.Clamp(mix / channels / 256, short.MinValue, short.MaxValue);
        }

        return output;
    }

    private static short[] MixPcm16(ReadOnlySpan<byte> buffer, int channels)
    {
        var frames = buffer.Length / (2 * channels);
        var output = new short[frames];
        for (var i = 0; i < frames; i++)
        {
            int mix = 0;
            for (var c = 0; c < channels; c++)
                mix += BitConverter.ToInt16(buffer.Slice((i * channels + c) * 2, 2));
            output[i] = (short)(mix / channels);
        }

        return output;
    }
}
