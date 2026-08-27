using MaksIT.Wvc210.Shared;


namespace MaksIT.Wvc210.Tests;

public class DayNightAutoTests {
  [Fact]
  public void MeanLuma_black_is_zero() {
    var luma = DayNightAuto.MeanLuma(Fill(640, 480, 0, 0, 0), 640, 480, 640 * 4);
    Assert.Equal(0, luma);
  }

  [Fact]
  public void MeanLuma_white_is_255() {
    var luma = DayNightAuto.MeanLuma(Fill(64, 64, 255, 255, 255), 64, 64, 64 * 4);
    Assert.Equal(255, luma);
  }

  [Fact]
  public void Observe_dark_for_dwell_enters_night() {
    var auto = new DayNightAuto();
    var t = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
    Assert.Null(auto.Observe(20, t));
    Assert.Null(auto.Observe(20, t.AddSeconds(2)));
    Assert.True(auto.Observe(20, t.Add(DayNightAuto.Dwell)));
    Assert.True(auto.IsNight);
  }

  [Fact]
  public void Observe_brief_dark_does_not_switch() {
    var auto = new DayNightAuto();
    auto.Assume(false);
    var t = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
    Assert.Null(auto.Observe(10, t));
    Assert.Null(auto.Observe(90, t.AddSeconds(1)));
    Assert.False(auto.IsNight);
  }

  [Fact]
  public void Observe_hysteresis_stays_night_until_exit_luma() {
    var auto = new DayNightAuto();
    auto.Assume(true);
    var t = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
    Assert.Null(auto.Observe(60, t));
    Assert.True(auto.IsNight);
    Assert.Null(auto.Observe(90, t));
    Assert.False(auto.Observe(90, t.Add(DayNightAuto.Dwell)));
    Assert.False(auto.IsNight);
  }

  [Fact]
  public void Observe_daylight_on_start_does_not_cgi() {
    var auto = new DayNightAuto();
    var t = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
    Assert.Null(auto.Observe(120, t));
    Assert.False(auto.IsNight);
  }

  private static byte[] Fill(int width, int height, byte r, byte g, byte b) {
    var pitch = width * 4;
    var data = new byte[pitch * height];
    for (var i = 0; i < data.Length; i += 4) {
      data[i] = b;
      data[i + 1] = g;
      data[i + 2] = r;
      data[i + 3] = 255;
    }

    return data;
  }
}
