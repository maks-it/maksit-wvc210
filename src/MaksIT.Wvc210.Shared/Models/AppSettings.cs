namespace MaksIT.Wvc210.Shared;

public sealed class AppSettings
{
    public string Host { get; set; } = "camera30cf5b.corp.maks-it.com";
    public int HttpPort { get; set; } = 80;
    public int RtspPort { get; set; } = 554;
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "admin";
    public bool AutoConnect { get; set; } = true;
    public string VlcPath { get; set; } = "";
    public int PanStep { get; set; } = 8;
    public string MicrophoneId { get; set; } = "";
}
