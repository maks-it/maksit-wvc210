using MaksIT.Wvc210.Shared;


namespace MaksIT.Wvc210.Tests;

public class LiveStreamChoiceTests {
  [Fact]
  public void Find_unknown_returns_asf() {
    var choice = LiveStreamChoice.Find("not-a-stream");
    Assert.Equal(LiveStreamKind.Asf, choice.Kind);
  }

  [Fact]
  public void Find_maps_legacy_vlc_names() {
    Assert.Equal(LiveStreamKind.Asf, LiveStreamChoice.Find("AsfVlc").Kind);
    Assert.Equal(LiveStreamKind.Rtsp, LiveStreamChoice.Find("RtspVlc").Kind);
  }

  [Fact]
  public void Find_legacy_ocx_maps_to_asf() {
    Assert.Equal(LiveStreamKind.Asf, LiveStreamChoice.Find("ocx").Kind);
  }
}
