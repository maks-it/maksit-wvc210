using System.Net.Sockets;
using System.Text;

namespace MaksIT.Wvc210.Client;

public sealed class OcxLiveConnection : IDisposable
{
    private readonly TcpClient _client;

    public OcxLiveConnection(TcpClient client, NetworkStream stream, byte[] preamble)
    {
        _client = client;
        Stream = stream;
        Preamble = preamble;
    }

    public NetworkStream Stream { get; }
    public byte[] Preamble { get; }

    public void Dispose()
    {
        try { Stream.Dispose(); } catch { }
        try { _client.Dispose(); } catch { }
    }
}

public sealed class OcxLiveStreamService
{
    public const byte FrameJpeg = 1;
    /// <summary>Cisco OCX mux: G.726 16 kbit/s live listen audio.</summary>
    public const byte FrameG726 = 2;
    /// <summary>Some firmwares mux G.711 instead of G.726.</summary>
    public const byte FrameG711 = 3;

    public async Task RunAsync(
        CameraClient client,
        Func<byte[], Task> onJpeg,
        Action<byte, byte[]> onAudio,
        CancellationToken ct)
    {
        using var connection = await client.OpenOcxLiveAsync(ct).ConfigureAwait(false);
        var reader = new FrameReader(connection.Stream, connection.Preamble);
        while (!ct.IsCancellationRequested)
        {
            var header = await reader.ReadExactAsync(48, ct).ConfigureAwait(false);
            if (!IsMagic(header))
            {
                if (!await reader.ResyncAsync(header, ct).ConfigureAwait(false))
                    throw new IOException("OCX live stream lost MJPG sync.");
                continue;
            }

            var size = BitConverter.ToInt32(header, 4);
            if (size <= 0 || size > 2_000_000)
                throw new IOException("OCX live frame size is invalid.");

            var type = header[22];
            var payload = await reader.ReadExactAsync(size, ct).ConfigureAwait(false);
            if (type == FrameJpeg)
                await onJpeg(payload).ConfigureAwait(false);
            else if (type is FrameG726 or FrameG711)
                onAudio(type, payload);
        }
    }

    private static bool IsMagic(byte[] header)
        => header.Length >= 4 &&
           header[0] == (byte)'M' && header[1] == (byte)'J' &&
           header[2] == (byte)'P' && header[3] == (byte)'G';

    private sealed class FrameReader
    {
        private readonly Stream _stream;
        private readonly byte[] _buffer = new byte[64 * 1024];
        private int _offset;
        private int _count;

        public FrameReader(Stream stream, byte[] preamble)
        {
            _stream = stream;
            if (preamble.Length == 0)
                return;
            var n = Math.Min(preamble.Length, _buffer.Length);
            Buffer.BlockCopy(preamble, 0, _buffer, 0, n);
            _count = n;
        }

        public async Task<byte[]> ReadExactAsync(int length, CancellationToken ct)
        {
            var data = new byte[length];
            var copied = 0;
            while (copied < length)
            {
                await EnsureAsync(ct).ConfigureAwait(false);
                if (_count == 0)
                    throw new IOException("OCX live stream ended.");
                var n = Math.Min(length - copied, _count);
                Buffer.BlockCopy(_buffer, _offset, data, copied, n);
                _offset += n;
                _count -= n;
                copied += n;
            }

            return data;
        }

        public async Task<bool> ResyncAsync(byte[] header, CancellationToken ct)
        {
            var window = new List<byte>(header);
            while (window.Count < 48 + 4096)
            {
                var b = await ReadByteAsync(ct).ConfigureAwait(false);
                if (b < 0)
                    return false;
                window.Add((byte)b);
                if (window.Count < 4)
                    continue;
                for (var i = 0; i <= window.Count - 4; i++)
                {
                    if (window[i] == (byte)'M' && window[i + 1] == (byte)'J' &&
                        window[i + 2] == (byte)'P' && window[i + 3] == (byte)'G')
                    {
                        var leftover = window.Skip(i).ToArray();
                        _offset = 0;
                        _count = 0;
                        if (leftover.Length > 0)
                        {
                            Buffer.BlockCopy(leftover, 0, _buffer, 0, leftover.Length);
                            _count = leftover.Length;
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        private async Task<int> ReadByteAsync(CancellationToken ct)
        {
            await EnsureAsync(ct).ConfigureAwait(false);
            if (_count == 0)
                return -1;
            var b = _buffer[_offset];
            _offset++;
            _count--;
            return b;
        }

        private async Task EnsureAsync(CancellationToken ct)
        {
            if (_count > 0)
                return;
            _offset = 0;
            _count = await _stream.ReadAsync(_buffer.AsMemory(0, _buffer.Length), ct).ConfigureAwait(false);
        }
    }
}
