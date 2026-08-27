using MaksIT.Wvc210.Shared;


namespace MaksIT.Wvc210.Tests;

public class AudioOperationChoiceTests {
  [Theory]
  [InlineData("0", AudioOperationMode.SimplexListen, true, false)]
  [InlineData("1", AudioOperationMode.SimplexTalk, false, true)]
  [InlineData("2", AudioOperationMode.HalfDuplex, true, true)]
  [InlineData("3", AudioOperationMode.FullDuplex, true, true)]
  public void Parse_cgi_matches_camera_operation(
      string cgi,
      AudioOperationMode mode,
      bool listen,
      bool talk) {
    var choice = AudioOperationChoice.Parse(cgi);
    Assert.Equal(mode, choice.Mode);
    Assert.Equal(listen, choice.AllowsListen);
    Assert.Equal(talk, choice.AllowsTalk);
  }

  [Fact]
  public void Parse_unknown_is_simplex_listen() {
    Assert.Equal(AudioOperationMode.SimplexListen, AudioOperationChoice.Parse(null).Mode);
    Assert.Equal(AudioOperationMode.SimplexListen, AudioOperationChoice.Parse("9").Mode);
  }
}
