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
}
