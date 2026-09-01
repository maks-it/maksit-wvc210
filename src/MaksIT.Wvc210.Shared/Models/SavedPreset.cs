namespace MaksIT.Wvc210.Shared;


/// <summary>
/// Occupancy backup for one camera preset slot (AppData settings).
/// Coordinates are <c>X,Y</c> from the PTZ group after Save, used to write
/// the slot back to NVRAM when the camera comes up empty.
/// </summary>
public sealed class SavedPreset {
  public int Index { get; set; }
  public string Name { get; set; } = "";
  public string Position { get; set; } = "";
}
