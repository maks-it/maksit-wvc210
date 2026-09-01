namespace MaksIT.Wvc210.Shared;


/// <summary>
/// One operator preset stored in app settings (not camera NVRAM).
/// Coordinates are <c>X,Y</c> as returned by the PTZ group after Save.
/// </summary>
public sealed class SavedPreset {
  public int Index { get; set; }
  public string Name { get; set; } = "";
  public string Position { get; set; } = "";
}
