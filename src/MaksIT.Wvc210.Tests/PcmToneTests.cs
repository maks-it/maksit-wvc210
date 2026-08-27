using MaksIT.Wvc210.Client;
using MaksIT.Wvc210.Shared;


namespace MaksIT.Wvc210.Tests;

public class PcmToneTests {
  [Fact]
  public void FillSine_has_energy() {
    var pcm = new short[8000];
    var phase = 0d;
    PcmTone.FillSine(pcm, 8000, 1000, 0.4, ref phase);
    Assert.True(PcmResampler.Rms(pcm) > 0.2);
  }

  [Fact]
  public void FillSine_silence_amplitude_is_zero() {
    var pcm = new short[400];
    var phase = 0d;
    PcmTone.FillSine(pcm, 8000, 1000, 0, ref phase);
    Assert.Equal(0, PcmResampler.Rms(pcm));
  }

  [Fact]
  public void SpeakerTest_pattern_is_over_a_second() {
    var ms = SpeakerTest.Segments.Sum(s => s.Milliseconds);
    Assert.True(ms >= 1000);
  }

  [Fact]
  public void FillClip_silence_is_quiet() {
    var pcm = new short[SpeakerTest.ClipSamples];
    var encoded = new byte[SpeakerTest.ClipSamples];
    var state = new SpeakerTest.PatternState();
    var rms = SpeakerTest.FillClip(pcm, encoded, TalkCodec.G711U, tone: false, ref state);
    Assert.Equal(0, rms);
  }
}
