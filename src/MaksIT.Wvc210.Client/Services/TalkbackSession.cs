using MaksIT.Wvc210.Shared;


namespace MaksIT.Wvc210.Client;

/// <summary>
/// Encodes local microphone PCM to G.711 clips. The Live hold pump writes them
/// on the recycled talk CGI — this type does not open or close that socket.
/// </summary>
public sealed class TalkbackSession : IDisposable {
  public const int ClipBytes = 1000;

  private readonly TalkCodec _codec;
  private readonly byte[] _pending = new byte[ClipBytes];
  private IMicrophoneCapture? _capture;
  private int _gainPercent = 200;
  private int _pendingCount;

  public TalkbackSession(TalkCodec codec) {
    _codec = codec;
  }

  public event Action<byte[]>? Encoded;
  public event Action<double>? LevelChanged;
  public string Path => _codec == TalkCodec.G711A ? "/img/g711a.cgi" : "/img/g711u.cgi";

  public void SetGain(int percent) =>
    _gainPercent = Math.Clamp(percent, 10, 400);

  public void Attach(IMicrophoneCapture capture) {
    _capture = capture;
    capture.Pcm8000Mono += OnPcm;
    capture.Start();
  }

  public void Dispose() {
    if (_capture is not null)
      _capture.Pcm8000Mono -= OnPcm;
    _capture?.Dispose();
    _capture = null;
  }

  private void OnPcm(short[] pcm) {
    if (_gainPercent != 100) {
      var g = _gainPercent / 100.0;
      for (var i = 0; i < pcm.Length; i++) {
        var v = pcm[i] * g;
        pcm[i] = (short)Math.Clamp(v, short.MinValue, short.MaxValue);
      }
    }

    LevelChanged?.Invoke(PcmResampler.Rms(pcm));
    var encoded = new byte[pcm.Length];
    if (_codec == TalkCodec.G711A)
      G711Codec.EncodeAlaw(pcm, encoded);
    else
      G711Codec.EncodeUlaw(pcm, encoded);

    var offset = 0;
    while (offset < encoded.Length) {
      var n = Math.Min(ClipBytes - _pendingCount, encoded.Length - offset);
      Buffer.BlockCopy(encoded, offset, _pending, _pendingCount, n);
      _pendingCount += n;
      offset += n;
      if (_pendingCount < ClipBytes)
        continue;
      var clip = new byte[ClipBytes];
      Buffer.BlockCopy(_pending, 0, clip, 0, ClipBytes);
      _pendingCount = 0;
      Encoded?.Invoke(clip);
    }
  }
}
