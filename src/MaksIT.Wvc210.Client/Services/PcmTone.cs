namespace MaksIT.Wvc210.Client;


public static class PcmTone {
  public static void FillSine(Span<short> pcm, int sampleRate, double hertz, double amplitude, ref double phase) {
    if (pcm.Length == 0 || sampleRate <= 0)
      return;

    var step = 2 * Math.PI * hertz / sampleRate;
    var peak = amplitude * short.MaxValue;
    var twoPi = 2 * Math.PI;
    for (var i = 0; i < pcm.Length; i++) {
      pcm[i] = (short)Math.Clamp(Math.Sin(phase) * peak, short.MinValue, short.MaxValue);
      phase += step;
      if (phase > twoPi)
        phase -= twoPi;
    }
  }
}
