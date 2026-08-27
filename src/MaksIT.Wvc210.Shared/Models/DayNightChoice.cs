namespace MaksIT.Wvc210.Shared;


public enum DayNightMode {
  Auto,
  Day,
  Night
}

public sealed class DayNightChoice {
  public const string ColorAuto = "0";
  public const string ColorBlackWhite = "5";

  public DayNightChoice(DayNightMode mode, string title, string hint) {
    Mode = mode;
    Title = title;
    Hint = hint;
  }

  public DayNightMode Mode { get; }
  public string Title { get; }
  public string Hint { get; }

  public static IReadOnlyList<DayNightChoice> All { get; } = [
    new(
      DayNightMode.Auto,
      "Auto",
      "Watches live brightness (center of the picture). Stays dark ~2.5s → black & white; stays bright → color. This camera has no IR lamp or light sensor CGI."),
    new(
      DayNightMode.Day,
      "Day",
      "Force color with auto white balance."),
    new(
      DayNightMode.Night,
      "Night",
      "Force black and white (VIDEO color). That is the visible night setting on WVC210.")
  ];

  public static DayNightChoice Find(DayNightMode mode) {
    foreach (var choice in All) {
      if (choice.Mode == mode)
        return choice;
    }

    return All[0];
  }

  public static DayNightChoice Find(string? name) {
    if (Enum.TryParse<DayNightMode>(name, ignoreCase: true, out var mode))
      return Find(mode);
    return All[0];
  }

  public static DayNightMode FromVideo(IReadOnlyDictionary<string, string> video) {
    if (video.GetValueOrDefault("color") is ColorBlackWhite)
      return DayNightMode.Night;
    return DayNightMode.Auto;
  }

  public static void ApplyToVideo(IDictionary<string, string> video, DayNightMode mode, ref string colorBeforeNight) {
    var current = video.TryGetValue("color", out var color) ? color : ColorAuto;
    if (mode == DayNightMode.Night) {
      if (current is not ColorBlackWhite)
        colorBeforeNight = current;
      video["color"] = ColorBlackWhite;
      return;
    }

    if (mode == DayNightMode.Day) {
      video["color"] = ColorAuto;
      colorBeforeNight = ColorAuto;
      return;
    }

    video["color"] = string.IsNullOrEmpty(colorBeforeNight) || colorBeforeNight == ColorBlackWhite
      ? ColorAuto
      : colorBeforeNight;
  }

  public override string ToString() =>
    Title;
}
