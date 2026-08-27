using MaksIT.Wvc210.Client;


namespace MaksIT.Wvc210.Tests;

public class G726CodecTests {
  [Fact]
  public void Decode_expands_four_samples_per_byte() {
    var pcm = new G726Codec().Decode([0x00, 0xFF]);
    Assert.Equal(8, pcm.Length);
  }

  [Fact]
  public void Roundtrip_keeps_tone_energy() {
    var pcm = new short[800];
    for (var i = 0; i < pcm.Length; i++)
      pcm[i] = (short)(8000 * Math.Sin(2 * Math.PI * 400 * i / 8000.0));

    var encoded = new G726Codec().Encode(pcm);
    var decoded = new G726Codec().Decode(encoded);
    Assert.Equal(encoded.Length * 4, decoded.Length);

    var original = PcmResampler.Rms(pcm);
    var recovered = PcmResampler.Rms(decoded);
    Assert.True(recovered > original * 0.25, $"recovered RMS {recovered} vs original {original}");
  }
}
