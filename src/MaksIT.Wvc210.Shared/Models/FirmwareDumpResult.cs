namespace MaksIT.Wvc210.Shared;

public sealed class FirmwareDumpResult
{
    public required byte[] Data { get; init; }
    public required string SourcePath { get; init; }
    public string ContentType { get; init; } = "";
    public string SuggestedFileName { get; init; } = "fw.bin";
}

public sealed class CameraIdentity
{
    public string Model { get; init; } = "";
    public string HostName { get; init; } = "";
    public string Firmware { get; init; } = "";
    public string Serial { get; init; } = "";
}
