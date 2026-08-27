namespace MaksIT.Wvc210.Client;


/// <summary>
/// Maps a click on the live image (0–1 in the drawn bitmap) to
/// <c>ptctrl.cgi?position=x,y</c> offsets from the frame center.
/// Pan matches the Cisco CGI (x increases left→right). Tilt is negated:
/// the firmware treats +y as up, so a click below center must send a negative y.
/// </summary>
public static class PtzClickMap {
  public static (int X, int Y) ToCameraOffset(double nx, double ny, int pixelWidth, int pixelHeight) {
    var w = Math.Max(1, pixelWidth - 1);
    var h = Math.Max(1, pixelHeight - 1);
    var x = (int)Math.Round((nx - 0.5) * w);
    var y = (int)Math.Round((0.5 - ny) * h);
    return (x, y);
  }
}
