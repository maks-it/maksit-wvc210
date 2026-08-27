namespace MaksIT.Wvc210.Shared;


/// <summary>
/// WVC210 has no photosensor / IR-cut CGI. Auto day/night is mean Rec.601 luma
/// of the live frame (center crop), with hysteresis and dwell so a passing shadow
/// does not flicker black-and-white.
/// </summary>
public sealed class DayNightAuto {
  public const int NightEnterLuma = 42;
  public const int NightExitLuma = 78;
  public static readonly TimeSpan Dwell = TimeSpan.FromSeconds(2.5);

  private bool? _night;
  private bool? _pendingNight;
  private DateTime _pendingSince;

  public bool? IsNight => _night;

  public void Reset() {
    _night = null;
    _pendingNight = null;
  }

  public void Assume(bool night) {
    _night = night;
    _pendingNight = null;
  }

  /// <returns>true = switch to night, false = switch to day, null = hold.</returns>
  public bool? Observe(int luma, DateTime utcNow) {
    var wantNight = _night == true
      ? luma < NightExitLuma
      : luma <= NightEnterLuma;

    if (_night is null && !wantNight) {
      _night = false;
      _pendingNight = null;
      return null;
    }

    if (_night == wantNight) {
      _pendingNight = null;
      return null;
    }

    if (_pendingNight != wantNight) {
      _pendingNight = wantNight;
      _pendingSince = utcNow;
      return null;
    }

    if (utcNow - _pendingSince < Dwell)
      return null;

    _night = wantNight;
    _pendingNight = null;
    return wantNight;
  }

  public static int MeanLuma(byte[] bgra, int width, int height, int pitch) {
    if (bgra.Length == 0 || width <= 0 || height <= 0 || pitch < width * 4)
      return 128;

    var x0 = Math.Max(0, width / 4);
    var x1 = Math.Min(width, width - x0);
    var y0 = Math.Max(0, height / 4);
    var y1 = Math.Min(height, height - y0);
    if (x1 <= x0 || y1 <= y0) {
      x0 = 0;
      x1 = width;
      y0 = 0;
      y1 = height;
    }

    long sum = 0;
    var n = 0;
    for (var y = y0; y < y1; y += 8) {
      var row = y * pitch;
      for (var x = x0; x < x1; x += 8) {
        var i = row + x * 4;
        if ((uint)(i + 2) >= (uint)bgra.Length)
          continue;
        var b = bgra[i];
        var g = bgra[i + 1];
        var r = bgra[i + 2];
        sum += (r * 77 + g * 150 + b * 29) >> 8;
        n++;
      }
    }

    return n == 0 ? 128 : (int)(sum / n);
  }
}
