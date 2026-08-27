using System.Runtime.InteropServices;
using MaksIT.Wvc210.Shared;

namespace MaksIT.Wvc210.Client;

public interface IMicrophoneCapture : IDisposable
{
    event Action<short[]> Pcm8000Mono;
    void Start();
    void Stop();
}

public static class MicrophoneEnumerator
{
    public static IReadOnlyList<MicrophoneDevice> List()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return WindowsMicrophoneCapture.ListDevices();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return LinuxMicrophoneCapture.ListDevices();
        return [];
    }

    public static IMicrophoneCapture Open(MicrophoneDevice device)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsMicrophoneCapture(device.Id);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxMicrophoneCapture(device.Id);
        throw new NotSupportedException("Microphone capture is not supported on this OS.");
    }
}
