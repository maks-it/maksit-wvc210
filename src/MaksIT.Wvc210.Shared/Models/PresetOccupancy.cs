using System.Diagnostics.CodeAnalysis;


namespace MaksIT.Wvc210.Shared;


/// <summary>
/// WVC210 preset occupancy: a name from <c>preset=all</c> (PT1…) or PTZ
/// <c>PresetNPosition</c> coordinates. Unset slots are blank or 0,0 / -1,-1.
/// App settings keep last-known coordinates so empty camera NVRAM can be
/// written back after a reboot. Go/Patrol use camera slots, not click offsets.
/// </summary>
public static class PresetOccupancy {
  public static bool HasName([NotNullWhen(true)] string? name) =>
    !string.IsNullOrWhiteSpace(name);

  public static bool HasCoordinates([NotNullWhen(true)] string? position) =>
    TryGetCoordinates(position, out _, out _);

  public static bool TryGetCoordinates([NotNullWhen(true)] string? position, out int x, out int y) {
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
  /// locally stored X,Y so the slot can be written back to NVRAM after a wipe.
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

  /// <summary>
  /// Restores locally backed-up coordinates into camera slots that are empty
  /// (reboot wipe). Does not overwrite slots the camera already holds.
  /// When a restore write is required, <c>Patrol1Position</c> is updated in
  /// the same payload so NVRAM is touched once. Returns whether
  /// <paramref name="ptz"/> changed.
  /// </summary>
  public static bool ApplyLocalBackup(
    Dictionary<string, string> ptz,
    IReadOnlyList<SavedPreset> local,
    string? userHome) {
    var changed = false;

    foreach (var slot in local) {
      if (slot.Index is < 1 or > 9 || !HasCoordinates(slot.Position))
        continue;

      var nameKey = "Preset" + slot.Index + "Name";
      var posKey = "Preset" + slot.Index + "Position";
      ptz.TryGetValue(posKey, out var cameraPosition);
      if (HasCoordinates(cameraPosition))
        continue;

      ptz[posKey] = slot.Position.Trim();
      ptz.TryGetValue(nameKey, out var cameraName);
      if (!HasName(cameraName))
        ptz[nameKey] = HasName(slot.Name) ? slot.Name.Trim() : "Preset " + slot.Index;
      changed = true;
    }

    if (HasCoordinates(userHome)) {
      ptz.TryGetValue("PredefineHome", out var cameraHome);
      if (!HasCoordinates(cameraHome)) {
        ptz["PredefineHome"] = userHome.Trim();
        changed = true;
      }
    }

    if (changed)
      SyncPatrolSequence(ptz);

    return changed;
  }

  /// <summary>
  /// Sets <c>Patrol1Position</c> from occupied slots when it differs.
  /// Call only as part of a write that is already required (restore or delete).
  /// </summary>
  public static bool SyncPatrolSequence(Dictionary<string, string> ptz) {
    var sequence = BuildPatrolSequence(ptz);
    ptz.TryGetValue("Patrol1Position", out var existingPatrol);
    if (NormalizePatrolSequence(sequence) == NormalizePatrolSequence(existingPatrol))
      return false;

    ptz["Patrol1Position"] = sequence;
    return true;
  }

  /// <summary>
  /// Camera patrol list: <c>preset,seconds;preset,seconds</c> for slots that
  /// have coordinates, using <c>PatrolInterval</c> (5–60 s, default 8).
  /// </summary>
  public static string BuildPatrolSequence(IReadOnlyDictionary<string, string> ptz) {
    var interval = 8;
    if (ptz.TryGetValue("PatrolInterval", out var raw) && int.TryParse(raw, out var parsed))
      interval = Math.Clamp(parsed, 5, 60);

    var parts = new List<string>();
    for (var i = 1; i <= 9; i++) {
      ptz.TryGetValue("Preset" + i + "Position", out var position);
      if (!HasCoordinates(position))
        continue;
      parts.Add(i + "," + interval);
    }

    return string.Join(";", parts);
  }

  public static string NormalizePatrolSequence(string? value) =>
    string.IsNullOrWhiteSpace(value)
      ? ""
      : value.Trim().TrimEnd(';').Replace(" ", "", StringComparison.Ordinal);
}
