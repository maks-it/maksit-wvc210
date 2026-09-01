namespace MaksIT.Wvc210.Shared;


/// <summary>
/// WVC210 preset occupancy: a name from <c>preset=all</c> (PT1…) or PTZ
/// <c>PresetNPosition</c> coordinates. Unset slots are blank or 0,0 / -1,-1.
/// After a camera reboot NVRAM is often empty; app settings keep last-known
/// coordinates and win when the camera has no valid X,Y.
/// </summary>
public static class PresetOccupancy {
  public static bool HasName(string? name) =>
    !string.IsNullOrWhiteSpace(name);

  public static bool HasCoordinates(string? position) =>
    TryGetCoordinates(position, out _, out _);

  public static bool TryGetCoordinates(string? position, out int x, out int y) {
    x = 0;
    y = 0;
    if (string.IsNullOrWhiteSpace(position))
      return false;

    var parts = position.Split(',');
    if (parts.Length < 2)
      return false;
    if (!int.TryParse(parts[0].Trim(), out x) || !int.TryParse(parts[1].Trim(), out y))
      return false;
    if ((x == 0 && y == 0) || (x < 0 && y < 0)) {
      x = 0;
      y = 0;
      return false;
    }

    return true;
  }

  public static bool IsOccupied(string? name, string? position) =>
    HasName(name) || HasCoordinates(position);

  /// <summary>
  /// Camera coordinates win when valid (Setup page edits). Otherwise keep the
  /// locally stored X,Y so Go/Patrol still work after a reboot wipe.
  /// Names: camera if set, else local.
  /// </summary>
  public static (string Name, string Position) MergeWithCamera(
    string localName,
    string localPosition,
    string? cameraName,
    string? cameraPosition) {
    var position = HasCoordinates(cameraPosition)
      ? cameraPosition!.Trim()
      : (HasCoordinates(localPosition) ? localPosition.Trim() : "");
    var name = HasName(cameraName)
      ? cameraName!.Trim()
      : (HasName(localName) ? localName.Trim() : "");
    return (name, position);
  }
}
