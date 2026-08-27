using MaksIT.Wvc210.Client;


namespace MaksIT.Wvc210.Tests;

public class PtzClickMapTests {
  [Fact]
  public void Center_is_home() {
    var (x, y) = PtzClickMap.ToCameraOffset(0.5, 0.5, 640, 480);
    Assert.Equal(0, x);
    Assert.Equal(0, y);
  }

  [Fact]
  public void Click_right_is_positive_pan() {
    var (x, _) = PtzClickMap.ToCameraOffset(1, 0.5, 640, 480);
    Assert.Equal(320, x);
  }

  [Fact]
  public void Click_below_center_is_negative_tilt() {
    var (_, y) = PtzClickMap.ToCameraOffset(0.5, 1, 640, 480);
    Assert.Equal(-240, y);
  }

  [Fact]
  public void Click_above_center_is_positive_tilt() {
    var (_, y) = PtzClickMap.ToCameraOffset(0.5, 0, 640, 480);
    Assert.Equal(240, y);
  }
}
