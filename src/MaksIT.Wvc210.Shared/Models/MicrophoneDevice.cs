namespace MaksIT.Wvc210.Shared;

public sealed class MicrophoneDevice
{
    public MicrophoneDevice(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; }
    public string Name { get; }

    public override string ToString() => Name;
}

public enum TalkCodec
{
    G711U,
    G711A
}

public sealed class TalkModeOption
{
    public TalkModeOption(string value, string label)
    {
        Value = value;
        Label = label;
    }

    public string Value { get; }
    public string Label { get; }
}
