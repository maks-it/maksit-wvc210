using MaksIT.Wvc210.Shared;


namespace MaksIT.Wvc210.Tests;

public class DayNightChoiceTests {
  [Fact]
  public void FromVideo_black_white_is_night() {
    var mode = DayNightChoice.FromVideo(new Dictionary<string, string> {
      ["color"] = "5",
      ["night_mode"] = "0"
    });
    Assert.Equal(DayNightMode.Night, mode);
  }

  [Fact]
  public void FromVideo_auto_color_is_auto() {
    var mode = DayNightChoice.FromVideo(new Dictionary<string, string> {
      ["color"] = "0",
      ["night_mode"] = "1"
    });
    Assert.Equal(DayNightMode.Auto, mode);
  }

  [Fact]
  public void ApplyToVideo_night_sets_black_white_and_remembers_wb() {
    var video = new Dictionary<string, string> { ["color"] = "1" };
    var before = "0";
    DayNightChoice.ApplyToVideo(video, DayNightMode.Night, ref before);
    Assert.Equal("5", video["color"]);
    Assert.Equal("1", before);
  }

  [Fact]
  public void ApplyToVideo_auto_restores_previous_wb() {
    var video = new Dictionary<string, string> { ["color"] = "5" };
    var before = "4";
    DayNightChoice.ApplyToVideo(video, DayNightMode.Auto, ref before);
    Assert.Equal("4", video["color"]);
  }

  [Fact]
  public void ApplyToVideo_day_forces_auto_wb() {
    var video = new Dictionary<string, string> { ["color"] = "5" };
    var before = "2";
    DayNightChoice.ApplyToVideo(video, DayNightMode.Day, ref before);
    Assert.Equal("0", video["color"]);
  }
}
