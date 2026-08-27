using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MaksIT.Wvc210.Client;

public static class ExternalApps
{
    public static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    public static void OpenVlc(string? configuredPath, string mediaUrl, bool rtspTcp)
    {
        var path = FindVlc(configuredPath);
        if (path is null)
        {
            throw new FileNotFoundException(
                "VLC was not found. Install VLC or set the path in connection settings.");
        }

        var args = rtspTcp
            ? $"--rtsp-tcp \"{mediaUrl}\""
            : $"\"{mediaUrl}\"";

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            Arguments = args,
            UseShellExecute = false
        });
    }

    public static string? FindVlc(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
            return configuredPath;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var candidate in new[]
                     {
                         Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VideoLAN", "VLC", "vlc.exe"),
                         Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "VideoLAN", "VLC", "vlc.exe")
                     })
            {
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return FindOnPath(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "vlc.exe" : "vlc");
    }

    private static string? FindOnPath(string fileName)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
        foreach (var dir in paths)
        {
            try
            {
                var candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate))
                    return candidate;
            }
            catch
            {
                // ignore invalid PATH entries
            }
        }

        return null;
    }
}
