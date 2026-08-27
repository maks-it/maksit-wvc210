namespace MaksIT.Wvc210.Client;

public static class PcmResampler
{
    public static short[] To8000(short[] input, int sampleRate)
    {
        if (input.Length == 0)
            return input;
        if (sampleRate <= 0 || sampleRate == 8000)
            return input;

        var outLen = Math.Max(1, (int)(input.Length * (8000.0 / sampleRate)));
        var output = new short[outLen];
        var step = sampleRate / 8000.0;
        for (var i = 0; i < outLen; i++)
        {
            var src = i * step;
            var i0 = (int)src;
            if (i0 >= input.Length - 1)
            {
                output[i] = input[^1];
                continue;
            }

            var frac = src - i0;
            var s0 = input[i0];
            var s1 = input[i0 + 1];
            output[i] = (short)(s0 + (s1 - s0) * frac);
        }

        return output;
    }

    public static double Rms(ReadOnlySpan<short> pcm)
    {
        if (pcm.Length == 0)
            return 0;
        double sum = 0;
        foreach (var s in pcm)
            sum += s * (double)s;
        return Math.Sqrt(sum / pcm.Length) / 32768.0;
    }
}
