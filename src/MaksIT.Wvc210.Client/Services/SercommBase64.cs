using System.Text;

namespace MaksIT.Wvc210.Client;

public static class SercommBase64
{
    private const string Alphabet = "ACEGIKMOQSUWYBDFHJLNPRTVXZacegikmoqsuwybdfhjlnprtvxz0246813579=+/";

    public static string DecodeToString(byte[] encoded)
    {
        var decoded = Decode(encoded);
        return Encoding.ASCII.GetString(decoded);
    }

    public static byte[] Decode(byte[] encoded)
    {
        var map = new int[256];
        Array.Fill(map, -1);
        for (var n = 0; n < Alphabet.Length; n++)
            map[Alphabet[n]] = n;

        var output = new List<byte>(encoded.Length);
        var iBuf = encoded.Where(b => !char.IsWhiteSpace((char)b)).ToArray();
        var pos = 0;
        while (pos < iBuf.Length)
        {
            int Enc()
            {
                if (pos >= iBuf.Length) return 64;
                var idx = map[iBuf[pos++]];
                return idx < 0 ? 64 : idx;
            }

            var enc1 = Enc();
            var enc2 = Enc();
            var enc3 = Enc();
            var enc4 = Enc();
            output.Add((byte)((enc1 << 2) | (enc2 >> 4)));
            if (enc3 != 64)
                output.Add((byte)(((enc2 & 15) << 4) | (enc3 >> 2)));
            if (enc4 != 64)
                output.Add((byte)(((enc3 & 3) << 6) | enc4));
        }

        return output.ToArray();
    }
}
