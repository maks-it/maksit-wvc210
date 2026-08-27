namespace MaksIT.Wvc210.Shared;


/// <summary>
/// WVC210 AUDIO <c>operation_mode</c>: simplex listen, simplex talk, half duplex, full duplex.
/// </summary>
public enum AudioOperationMode {
  SimplexListen,
  SimplexTalk,
  HalfDuplex,
  FullDuplex
}

public sealed class AudioOperationChoice {
  public AudioOperationChoice(
      AudioOperationMode mode,
      string cgi,
      string title,
      string hint,
      bool allowsListen,
      bool allowsTalk) {
    Mode = mode;
    Cgi = cgi;
    Title = title;
    Hint = hint;
    AllowsListen = allowsListen;
    AllowsTalk = allowsTalk;
  }

  public AudioOperationMode Mode { get; }
  public string Cgi { get; }
  public string Title { get; }
  public string Hint { get; }
  public bool AllowsListen { get; }
  public bool AllowsTalk { get; }

  public static IReadOnlyList<AudioOperationChoice> All { get; } = [
    new(
      AudioOperationMode.SimplexListen,
      "0",
      "Simplex listen",
      "Camera microphone to the PC only. Speak and speaker test stay off.",
      allowsListen: true,
      allowsTalk: false),
    new(
      AudioOperationMode.SimplexTalk,
      "1",
      "Simplex talk",
      "PC to the camera speaker only. Live listen is muted.",
      allowsListen: false,
      allowsTalk: true),
    new(
      AudioOperationMode.HalfDuplex,
      "2",
      "Half duplex",
      "Listen or talk, not both. Live listen mutes while Speak or speaker CGI is held.",
      allowsListen: true,
      allowsTalk: true),
    new(
      AudioOperationMode.FullDuplex,
      "3",
      "Full duplex",
      "Listen and talk at the same time. Live ASF/RTSP audio stays up during speaker test.",
      allowsListen: true,
      allowsTalk: true)
  ];

  public static AudioOperationChoice Default => All[0];

  public static AudioOperationChoice Parse(string? cgi) {
    foreach (var choice in All) {
      if (choice.Cgi == cgi)
        return choice;
    }

    return Default;
  }

  public static AudioOperationChoice Find(AudioOperationMode mode) {
    foreach (var choice in All) {
      if (choice.Mode == mode)
        return choice;
    }

    return Default;
  }

  public override string ToString() =>
    Title;
}
