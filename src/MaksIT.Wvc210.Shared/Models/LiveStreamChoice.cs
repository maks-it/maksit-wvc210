namespace MaksIT.Wvc210.Shared;


public enum LiveStreamKind {
  Asf,
  Rtsp,
  Mjpeg,
  Snapshot,
  Ocx
}

public sealed class LiveStreamChoice {
  public LiveStreamChoice(LiveStreamKind kind, string title, string hint) {
    Kind = kind;
    Title = title;
    Hint = hint;
  }

  public LiveStreamKind Kind { get; }
  public string Title { get; }
  public string Hint { get; }

  public static IReadOnlyList<LiveStreamChoice> All { get; } = [
    new(
      LiveStreamKind.Asf,
      "ASF (MPEG-4 + audio)",
      "Same HTTP stream VLC uses. Bundled libVLC — VLC install is not required."),
    new(
      LiveStreamKind.Rtsp,
      "RTSP-TCP (MPEG-4 + audio)",
      "Same RTSP stream VLC uses. Bundled libVLC — VLC install is not required."),
    new(
      LiveStreamKind.Mjpeg,
      "MJPEG (JPEG, no audio)",
      "PushServer JPEG preview. No listen audio."),
    new(
      LiveStreamKind.Snapshot,
      "Snapshots (stills)",
      "Repeated JPEG stills. Lightest on the camera.")
  ];

  public static LiveStreamChoice Default => All[0];

  public static LiveStreamChoice Find(string? name) {
    if (string.Equals(name, "AsfVlc", StringComparison.OrdinalIgnoreCase))
      name = nameof(LiveStreamKind.Asf);
    if (string.Equals(name, "RtspVlc", StringComparison.OrdinalIgnoreCase))
      name = nameof(LiveStreamKind.Rtsp);
    if (string.Equals(name, "Ocx", StringComparison.OrdinalIgnoreCase))
      name = nameof(LiveStreamKind.Asf);

    if (Enum.TryParse<LiveStreamKind>(name, ignoreCase: true, out var kind)) {
      foreach (var choice in All) {
        if (choice.Kind == kind)
          return choice;
      }
    }

    return Default;
  }

  public override string ToString() =>
    Title;
}
