namespace MaksIT.Wvc210.Client;

/// <summary>
/// ITU-T G.726 16 kbit/s (2 bits/sample) ADPCM used by the WVC210 OCX live mux
/// (<c>/img/mjpeg.cgi</c> frame type 0x02).
/// </summary>
public sealed class G726Codec {
  private static readonly int[] Power2 = [
    1, 2, 4, 8, 0x10, 0x20, 0x40, 0x80,
    0x100, 0x200, 0x400, 0x800, 0x1000, 0x2000, 0x4000
  ];

  private static readonly int[] DqlnTab = [116, 365, 365, 116];
  private static readonly int[] WiTab = [-704, 14048, 14048, -704];
  private static readonly int[] FiTab = [0, 0xE00, 0xE00, 0];
  private static readonly int[] QuantTab = [261];

  private int _yl;
  private int _yu;
  private int _dms;
  private int _dml;
  private int _ap;
  private readonly int[] _a = new int[2];
  private readonly int[] _b = new int[6];
  private readonly int[] _pk = new int[2];
  private readonly int[] _dq = new int[6];
  private readonly int[] _sr = new int[2];
  private int _td;

  public G726Codec() =>
    Reset();

  public void Reset() {
    _yl = 34816;
    _yu = 544;
    _dms = 0;
    _dml = 0;
    _ap = 0;
    _td = 0;
    Array.Clear(_a);
    Array.Clear(_b);
    Array.Clear(_pk);
    for (var i = 0; i < 2; i++)
      _sr[i] = 32;

    for (var i = 0; i < 6; i++)
      _dq[i] = 32;
  }

  public byte[] Encode(ReadOnlySpan<short> pcm) {
    var byteCount = (pcm.Length + 3) / 4;
    var dest = new byte[byteCount];
    var sample = 0;
    for (var i = 0; i < dest.Length; i++) {
      var packed = 0;
      for (var shift = 6; shift >= 0; shift -= 2) {
        var sl = sample < pcm.Length ? pcm[sample] : (short)0;
        packed |= EncodeSample(sl) << shift;
        sample++;
      }

      dest[i] = (byte)packed;
    }

    return dest;
  }

  public short[] Decode(ReadOnlySpan<byte> encoded) {
    var pcm = new short[encoded.Length * 4];
    var o = 0;
    foreach (var b in encoded) {
      pcm[o++] = DecodeSample((b >> 6) & 0x03);
      pcm[o++] = DecodeSample((b >> 4) & 0x03);
      pcm[o++] = DecodeSample((b >> 2) & 0x03);
      pcm[o++] = DecodeSample(b & 0x03);
    }

    return pcm;
  }

  internal int EncodeSample(int sl) {
    sl >>= 2;
    var sezi = PredictorZero();
    var sez = sezi >> 1;
    var se = (sezi + PredictorPole()) >> 1;
    var d = sl - se;
    var y = StepSize();
    var i = Quantize(d, y);
    var dq = Reconstruct((i & 0x02) != 0, DqlnTab[i], y);
    var sr = dq < 0 ? se - (dq & 0x3FFF) : se + dq;
    Update(y, WiTab[i], FiTab[i], dq, sr, sr + sez - se);
    return i;
  }

  internal short DecodeSample(int i) {
    i &= 0x03;
    var sezi = PredictorZero();
    var sez = sezi >> 1;
    var se = (sezi + PredictorPole()) >> 1;
    var y = StepSize();
    var dq = Reconstruct((i & 0x02) != 0, DqlnTab[i], y);
    var sr = dq < 0 ? se - (dq & 0x3FFF) : se + dq;
    Update(y, WiTab[i], FiTab[i], dq, sr, sr - se + sez);
    return (short)Math.Clamp(sr << 2, short.MinValue, short.MaxValue);
  }

  private int PredictorZero() {
    var sezi = Fmult(_b[0] >> 2, _dq[0]);
    for (var i = 1; i < 6; i++)
      sezi += Fmult(_b[i] >> 2, _dq[i]);

    return sezi;
  }

  private int PredictorPole() =>
    Fmult(_a[1] >> 2, _sr[1]) + Fmult(_a[0] >> 2, _sr[0]);

  private int StepSize() {
    if (_ap >= 256)
      return _yu;

    var y = _yl >> 6;
    var dif = _yu - y;
    var al = _ap >> 2;
    if (dif > 0)
      y += (dif * al) >> 6;
    else if (dif < 0)
      y += (dif * al + 0x3F) >> 6;

    return y;
  }

  private static int Quantize(int d, int y) {
    var dqm = Math.Abs(d);
    var exp = Quan(dqm >> 1, Power2);
    var mant = ((dqm << 7) >> exp) & 0x7F;
    var dln = (exp << 7) + mant - (y >> 2);
    var i = 0;
    if (dln >= QuantTab[0])
      i = 1;

    if (d < 0)
      return 3 - i;

    return i;
  }

  private static int Reconstruct(bool negative, int dqln, int y) {
    var dql = dqln + (y >> 2);
    if (dql < 0)
      return negative ? -0x8000 : 0;

    var dex = (dql >> 7) & 15;
    var dqt = 128 + (dql & 127);
    var dq = (dqt << 7) >> (14 - dex);
    return negative ? dq - 0x8000 : dq;
  }

  private void Update(int y, int wi, int fi, int dq, int sr, int dqsez) {
    var pk0 = dqsez < 0 ? 1 : 0;
    var mag = dq & 0x7FFF;
    var ylint = _yl >> 15;
    var ylfrac = (_yl >> 10) & 0x1F;
    var thr1 = (32 + ylfrac) << ylint;
    var thr2 = ylint > 9 ? 31 << 10 : thr1;
    var dqthr = (thr2 + (thr2 >> 1)) >> 1;
    var tr = _td != 0 && mag > dqthr ? 1 : 0;

    _yu = y + ((wi - y) >> 5);
    _yu = Math.Clamp(_yu, 544, 5120);
    _yl += _yu + ((-_yl) >> 6);

    var a2p = 0;
    if (tr != 0) {
      Array.Clear(_a);
      Array.Clear(_b);
    }
    else {
      var pks1 = pk0 ^ _pk[0];
      a2p = _a[1] - (_a[1] >> 7);
      if (dqsez != 0) {
        var fa1 = pks1 != 0 ? _a[0] : -_a[0];
        if (fa1 < -8191)
          a2p -= 0x100;
        else if (fa1 > 8191)
          a2p += 0xFF;
        else
          a2p += fa1 >> 5;

        if ((pk0 ^ _pk[1]) != 0) {
          if (a2p <= -12160)
            a2p = -12288;
          else if (a2p >= 12416)
            a2p = 12288;
          else
            a2p -= 0x80;
        }
        else if (a2p <= -12416)
          a2p = -12288;
        else if (a2p >= 12160)
          a2p = 12288;
        else
          a2p += 0x80;
      }

      _a[1] = a2p;
      _a[0] -= _a[0] >> 8;
      if (dqsez != 0)
        _a[0] += pks1 == 0 ? 192 : -192;

      var a1ul = 15360 - a2p;
      if (_a[0] < -a1ul)
        _a[0] = -a1ul;
      else if (_a[0] > a1ul)
        _a[0] = a1ul;

      for (var cnt = 0; cnt < 6; cnt++) {
        _b[cnt] -= _b[cnt] >> 8;
        if ((dq & 0x7FFF) == 0)
          continue;

        _b[cnt] += (dq ^ _dq[cnt]) >= 0 ? 128 : -128;
      }
    }

    for (var cnt = 5; cnt > 0; cnt--)
      _dq[cnt] = _dq[cnt - 1];

    _dq[0] = PackFloat(dq >= 0, mag);
    _sr[1] = _sr[0];
    _sr[0] = PackSignedFloat(sr);
    _pk[1] = _pk[0];
    _pk[0] = pk0;
    _td = tr == 0 && a2p < -11776 ? 1 : 0;
    _dms += (fi - _dms) >> 5;
    _dml += ((fi << 2) - _dml) >> 7;

    if (tr != 0 || y < 1536 || _td != 0 || Math.Abs((_dms << 2) - _dml) >= (_dml >> 3))
      _ap += (0x200 - _ap) >> 4;
    else
      _ap += (-_ap) >> 4;

    if (tr != 0)
      _ap = 256;
  }

  private static int PackFloat(bool positive, int mag) {
    if (mag == 0)
      return positive ? 0x20 : 0xFC20;

    var exp = Quan(mag, Power2);
    var packed = (exp << 6) + ((mag << 6) >> exp);
    return positive ? packed : packed - 0x400;
  }

  private static int PackSignedFloat(int sr) {
    if (sr == 0)
      return 0x20;
    if (sr > 0)
      return PackFloat(true, sr);
    if (sr > -32768)
      return PackFloat(false, -sr);

    return 0xFC20;
  }

  private static int Fmult(int an, int srn) {
    var anmag = an > 0 ? an : (-an) & 0x1FFF;
    var anexp = Quan(anmag, Power2) - 6;
    var anmant = anmag == 0
      ? 32
      : anexp >= 0 ? anmag >> anexp : anmag << -anexp;
    var wanexp = anexp + ((srn >> 10) & 0xF) - 13;
    var wanmant = (anmant * (srn & 0x3FF) + 0x30) >> 4;
    var retval = wanexp >= 0
      ? (wanmant << wanexp) & 0x7FFF
      : wanmant >> -wanexp;
    return (an ^ srn) < 0 ? -retval : retval;
  }

  private static int Quan(int val, int[] table) {
    for (var i = 0; i < table.Length; i++) {
      if (val < table[i])
        return i;
    }

    return table.Length;
  }
}
