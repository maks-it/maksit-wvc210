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

  [Fact]
  public void ApplyLocalBackup_writes_empty_camera_slots_and_patrol() {
    var ptz = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
      ["Preset1Position"] = "0,0",
      ["PatrolInterval"] = "8"
    };
    var local = new List<SavedPreset> {
      new() { Index = 1, Name = "Desk", Position = "100,200" },
      new() { Index = 2, Name = "Door", Position = "40,12" }
    };

    Assert.True(PresetOccupancy.ApplyLocalBackup(ptz, local, "80,20"));
    Assert.Equal("100,200", ptz["Preset1Position"]);
    Assert.Equal("Desk", ptz["Preset1Name"]);
    Assert.Equal("40,12", ptz["Preset2Position"]);
    Assert.Equal("Door", ptz["Preset2Name"]);
    Assert.Equal("80,20", ptz["PredefineHome"]);
    Assert.Equal("1,8;2,8", ptz["Patrol1Position"]);
  }

  [Fact]
  public void ApplyLocalBackup_leaves_camera_coords_and_custom_patrol() {
    var ptz = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
      ["Preset1Name"] = "Window",
      ["Preset1Position"] = "40,12",
      ["Preset2Position"] = "80,20",
      ["Patrol1Position"] = "2,10;1,10",
      ["PatrolInterval"] = "8",
      ["PredefineHome"] = "5,5"
    };
    var local = new List<SavedPreset> {
      new() { Index = 1, Name = "Desk", Position = "100,200" }
    };

    Assert.False(PresetOccupancy.ApplyLocalBackup(ptz, local, "1,1"));
    Assert.Equal("40,12", ptz["Preset1Position"]);
    Assert.Equal("Window", ptz["Preset1Name"]);
    Assert.Equal("2,10;1,10", ptz["Patrol1Position"]);
    Assert.Equal("5,5", ptz["PredefineHome"]);
  }

  [Fact]
  public void ApplyLocalBackup_does_not_write_when_camera_already_has_slots() {
    var ptz = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
      ["Preset1Position"] = "40,12",
      ["Preset3Position"] = "80,20",
      ["PatrolInterval"] = "10"
    };
    var local = new List<SavedPreset> {
      new() { Index = 1, Name = "Desk", Position = "40,12" },
      new() { Index = 3, Name = "Door", Position = "80,20" }
    };

    Assert.False(PresetOccupancy.ApplyLocalBackup(ptz, local, null));
    Assert.False(ptz.ContainsKey("Patrol1Position"));
  }

  [Fact]
  public void SyncPatrolSequence_updates_only_when_different() {
    var ptz = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
      ["Preset1Position"] = "40,12",
      ["Preset2Position"] = "80,20",
      ["Patrol1Position"] = "1,8;2,8",
      ["PatrolInterval"] = "8"
    };

    Assert.False(PresetOccupancy.SyncPatrolSequence(ptz));
    ptz["Patrol1Position"] = "9,8";
    Assert.True(PresetOccupancy.SyncPatrolSequence(ptz));
    Assert.Equal("1,8;2,8", ptz["Patrol1Position"]);
  }

  [Fact]
  public void BuildPatrolSequence_skips_empty_slots() {
    var ptz = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
      ["Preset1Position"] = "0,0",
      ["Preset2Position"] = "40,12",
      ["PatrolInterval"] = "5"
    };

    Assert.Equal("2,5", PresetOccupancy.BuildPatrolSequence(ptz));
  }
}
