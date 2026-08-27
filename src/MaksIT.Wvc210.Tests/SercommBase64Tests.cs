using MaksIT.Wvc210.Client;


namespace MaksIT.Wvc210.Tests;

public class SercommBase64Tests {
  [Fact]
  public void Decode_empty_returns_empty() =>
    Assert.Empty(SercommBase64.Decode([]));

  [Fact]
  public void Decode_whitespace_only_returns_empty() =>
    Assert.Empty(SercommBase64.Decode("  \n\t"u8.ToArray()));
}
