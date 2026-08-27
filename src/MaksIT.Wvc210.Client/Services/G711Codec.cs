using MaksIT.Wvc210.Shared;

namespace MaksIT.Wvc210.Client;

/// <summary>
/// ITU-T G.711 A-law / µ-law encoding from 16-bit linear PCM.
/// </summary>
public static class G711Codec
{
    public static void EncodeUlaw(ReadOnlySpan<short> pcm, Span<byte> dest)
    {
        var n = Math.Min(pcm.Length, dest.Length);
        for (var i = 0; i < n; i++)
            dest[i] = LinearToUlaw(pcm[i]);
    }

    public static void EncodeAlaw(ReadOnlySpan<short> pcm, Span<byte> dest)
    {
        var n = Math.Min(pcm.Length, dest.Length);
        for (var i = 0; i < n; i++)
            dest[i] = LinearToAlaw(pcm[i]);
    }

    public static byte LinearToUlaw(short pcm)
    {
        const int bias = 0x84;
        const int clip = 32635;
        int mask;
        int sample = pcm;
        if (sample < 0)
        {
            if (sample == short.MinValue)
                sample++;
            sample = bias - sample;
            mask = 0x7F;
        }
        else
        {
            sample += bias;
            mask = 0xFF;
        }

        if (sample > clip)
            sample = clip;

        int exponent = 7;
        for (var expMask = 0x4000; (sample & expMask) == 0 && exponent > 0; exponent--, expMask >>= 1)
        {
        }

        var mantissa = (sample >> (exponent + 3)) & 0x0F;
        var ulaw = (exponent << 4) | mantissa;
        return (byte)(ulaw ^ mask);
    }

    public static short[] Decode(TalkCodec codec, ReadOnlySpan<byte> encoded)
    {
        var pcm = new short[encoded.Length];
        if (codec == TalkCodec.G711A)
            DecodeAlaw(encoded, pcm);
        else
            DecodeUlaw(encoded, pcm);
        return pcm;
    }

    public static void DecodeUlaw(ReadOnlySpan<byte> encoded, Span<short> pcm)
    {
        var n = Math.Min(encoded.Length, pcm.Length);
        for (var i = 0; i < n; i++)
            pcm[i] = UlawToLinear(encoded[i]);
    }

    public static void DecodeAlaw(ReadOnlySpan<byte> encoded, Span<short> pcm)
    {
        var n = Math.Min(encoded.Length, pcm.Length);
        for (var i = 0; i < n; i++)
            pcm[i] = AlawToLinear(encoded[i]);
    }

    public static short UlawToLinear(byte ulaw)
    {
        ulaw = (byte)~ulaw;
        var t = ((ulaw & 0x0F) << 3) + 0x84;
        t <<= (ulaw & 0x70) >> 4;
        return (short)((ulaw & 0x80) != 0 ? (0x84 - t) : (t - 0x84));
    }

    public static short AlawToLinear(byte alaw)
    {
        alaw ^= 0x55;
        var t = (alaw & 0x0F) << 4;
        var seg = (alaw & 0x70) >> 4;
        t = seg switch
        {
            0 => t + 8,
            1 => t + 0x108,
            _ => (t + 0x108) << (seg - 1)
        };
        return (short)((alaw & 0x80) != 0 ? t : -t);
    }

    public static byte LinearToAlaw(short pcm)
    {
        const int clip = 32635;
        var sign = (pcm >> 8) & 0x80;
        if (sign == 0)
            pcm = (short)-pcm;
        if (pcm > clip)
            pcm = clip;
        int exponent = 7;
        for (var expMask = 0x4000; (pcm & expMask) == 0 && exponent > 0; exponent--, expMask >>= 1)
        {
        }

        var mantissa = (pcm >> (exponent == 0 ? 4 : (exponent + 3))) & 0x0F;
        var alaw = sign | (exponent << 4) | mantissa;
        return (byte)(alaw ^ 0x55);
    }
}
