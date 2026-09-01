using MaksIT.Wvc210.Shared;


namespace MaksIT.Wvc210.Tests;

public class PresetOccupancyTests {
  [Theory]
  [InlineData(null, false)]
  [InlineData("", false)]
  [InlineData("  ", false)]
  [InlineData("0,0", false)]
  [InlineData("-1,-1", false)]
  [InlineData("100,200", true)]
  [InlineData(" 80, 12 ", true)]
  public void HasCoordinates_matches_wvc210_empty_slots(string? position, bool expected) {
    Assert.Equal(expected, PresetOccupancy.HasCoordinates(position));
  }

  [Fact]
  public void IsOccupied_if_named_even_without_coordinates() {
    Assert.True(PresetOccupancy.IsOccupied("Desk", ""));
  }

  [Theory]
  [InlineData("100,200", 100, 200)]
  [InlineData(" 80, 12 ", 80, 12)]
  public void TryGetCoordinates_parses_valid_xy(string position, int x, int y) {
    Assert.True(PresetOccupancy.TryGetCoordinates(position, out var parsedX, out var parsedY));
    Assert.Equal(x, parsedX);
    Assert.Equal(y, parsedY);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("0,0")]
  [InlineData("-1,-1")]
  public void TryGetCoordinates_rejects_empty_slots(string? position) {
    Assert.False(PresetOccupancy.TryGetCoordinates(position, out var x, out var y));
    Assert.Equal(0, x);
    Assert.Equal(0, y);
  }

  [Fact]
  public void MergeWithCamera_keeps_local_xy_when_camera_wiped() {
    var merged = PresetOccupancy.MergeWithCamera("Desk", "100,200", "", "0,0");
    Assert.Equal("Desk", merged.Name);
    Assert.Equal("100,200", merged.Position);
  }

  [Fact]
  public void MergeWithCamera_imports_camera_when_local_empty() {
    var merged = PresetOccupancy.MergeWithCamera("", "", "Window", "40,12");
    Assert.Equal("Window", merged.Name);
    Assert.Equal("40,12", merged.Position);
  }

  [Fact]
  public void MergeWithCamera_prefers_camera_xy_when_both_set() {
    var merged = PresetOccupancy.MergeWithCamera("Desk", "100,200", "Window", "40,12");
    Assert.Equal("Window", merged.Name);
    Assert.Equal("40,12", merged.Position);
  }

  [Fact]
  public void MergeWithCamera_keeps_local_xy_if_camera_has_name_only() {
    var merged = PresetOccupancy.MergeWithCamera("Desk", "100,200", "Window", "-1,-1");
    Assert.Equal("Window", merged.Name);
    Assert.Equal("100,200", merged.Position);
  }
}
