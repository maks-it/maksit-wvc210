namespace MaksIT.Wvc210.Shared;


/// <summary>
/// WVC210 preset occupancy: a name from <c>preset=all</c> (PT1…) or PTZ
/// <c>PresetNPosition</c> coordinates. Unset slots are blank or 0,0 / -1,-1.
/// </summary>
public static class PresetOccupancy {
  public static bool HasName(string? name) =>
    !string.IsNullOrWhiteSpace(name);

  public static bool HasCoordinates(string? position) {
    if (string.IsNullOrWhiteSpace(position))
      return false;

    var parts = position.Split(',');
    if (parts.Length < 2)
      return false;
    if (!int.TryParse(parts[0].Trim(), out var x) || !int.TryParse(parts[1].Trim(), out var y))
      return false;
    if (x == 0 && y == 0)
      return false;
    if (x < 0 && y < 0)
      return false;
    return true;
  }

  public static bool IsOccupied(string? name, string? position) =>
    HasName(name) || HasCoordinates(position);
}
