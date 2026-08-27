using System.Diagnostics;
using MaksIT.Wvc210.Shared;


namespace MaksIT.Wvc210.Client;

/// <summary>
/// Pushes a generated G.711 beep pattern to the camera speaker (no local microphone).
/// </summary>
public static class SpeakerTest {
  public const int SampleRate = 8000;
  public const int ClipSamples = 1000;
  public const int ClipMilliseconds = 125;

  public static readonly (double Hertz, int Milliseconds)[] Segments = [
    (880, 250),
    (0, 125),
    (880, 250),
    (0, 125),
    (1320, 500),
    (0, 125),
    (1000, 750)
  ];

  public struct PatternState {
    public int Segment;
    public int Offset;
    public double Phase;
  }

  public static double FillClip(
      Span<short> pcm,
      Span<byte> encoded,
      TalkCodec codec,
      bool tone,
      ref PatternState state) {
    pcm.Clear();
    if (!tone || Segments.Length == 0) {
      state = default;
      // Not digital zero: the camera aborts the talk CGI on true silence.
      pcm.Fill(48);
      Encode(pcm, encoded, codec);
      return 0;
    }

    var (hertz, milliseconds) = Segments[state.Segment];
    var n = Math.Min(ClipSamples, pcm.Length);
    if (hertz > 0)
      PcmTone.FillSine(pcm[..n], SampleRate, hertz, 0.55, ref state.Phase);
    else
      state.Phase = 0;

    Encode(pcm[..n], encoded[..n], codec);
    state.Offset += n;
    if (state.Offset >= milliseconds * SampleRate / 1000) {
      state.Offset = 0;
      state.Segment = (state.Segment + 1) % Segments.Length;
    }

    return PcmResampler.Rms(pcm[..n]);
  }

  public static async Task PlayAsync(
      TalkUploadStream upload,
      TalkCodec codec,
      CancellationToken ct,
      Action<double>? level = null) {
    var pcm = new short[ClipSamples];
    var encoded = new byte[ClipSamples];
    var state = new PatternState();
    var posted = 0;
    var clock = Stopwatch.StartNew();
    var remaining = Segments.Sum(s => s.Milliseconds) / ClipMilliseconds;

    for (var i = 0; i < remaining; i++) {
      ct.ThrowIfCancellationRequested();
      var rms = FillClip(pcm, encoded, codec, tone: true, ref state);
      level?.Invoke(rms);
      await upload.WriteAsync(encoded.AsMemory(0, ClipSamples), ct).ConfigureAwait(false);
      posted++;
      var wait = TimeSpan.FromMilliseconds(posted * ClipMilliseconds) - clock.Elapsed;
      if (wait > TimeSpan.Zero)
        await Task.Delay(wait, ct).ConfigureAwait(false);
    }
  }

  private static void Encode(Span<short> pcm, Span<byte> encoded, TalkCodec codec) {
    if (codec == TalkCodec.G711A)
      G711Codec.EncodeAlaw(pcm, encoded);
    else
      G711Codec.EncodeUlaw(pcm, encoded);
  }
}
