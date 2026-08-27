using MaksIT.Wvc210.Client;


namespace MaksIT.Wvc210.Tests;

public class G711CodecTests {
  [Fact]
  public void EncodeUlaw_writes_one_byte_per_sample() {
    Span<short> pcm = stackalloc short[] { 0, 32767, -32768 };
    Span<byte> dest = stackalloc byte[3];
    G711Codec.EncodeUlaw(pcm, dest);
    Assert.Equal(3, dest.Length);
    Assert.NotEqual(dest[0], dest[1]);
  }
}
