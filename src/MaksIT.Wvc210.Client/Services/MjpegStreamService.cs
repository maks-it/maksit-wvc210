using System.Globalization;
using System.Text;

namespace MaksIT.Wvc210.Client;

public sealed class MjpegStreamService
{
    public async Task RunAsync(
        CameraClient client,
        Func<byte[], Task> onFrame,
        CancellationToken ct)
    {
        try
        {
            await RunMjpegAsync(client, onFrame, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            await RunSnapshotLoopAsync(client, onFrame, ct).ConfigureAwait(false);
        }
    }

    private static async Task RunMjpegAsync(CameraClient client, Func<byte[], Task> onFrame, CancellationToken ct)
    {
        using var response = await client.OpenMjpegAsync(ct).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var reader = new BufferedByteReader(stream);

        while (!ct.IsCancellationRequested)
        {
            string? line;
            var contentLength = -1;
            do
            {
                line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line is null)
                    throw new IOException("MJPEG stream ended.");
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    var raw = line["Content-Length:".Length..].Trim();
                    if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                        contentLength = n;
                }
            } while (line.Length > 0);

            if (contentLength <= 0 || contentLength > 4_000_000)
            {
                var jpeg = await reader.ReadJpegMarkerAsync(ct).ConfigureAwait(false);
                if (jpeg is null)
                    throw new IOException("Could not parse JPEG from MJPEG stream.");
                await onFrame(jpeg).ConfigureAwait(false);
                continue;
            }

            var frame = await reader.ReadExactAsync(contentLength, ct).ConfigureAwait(false);
            await onFrame(frame).ConfigureAwait(false);
        }
    }

    private static async Task RunSnapshotLoopAsync(CameraClient client, Func<byte[], Task> onFrame, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var jpeg = await client.SnapshotAsync(ct).ConfigureAwait(false);
            await onFrame(jpeg).ConfigureAwait(false);
            await Task.Delay(180, ct).ConfigureAwait(false);
        }
    }

    private sealed class BufferedByteReader
    {
        private readonly Stream _stream;
        private readonly byte[] _buffer = new byte[64 * 1024];
        private int _offset;
        private int _count;

        public BufferedByteReader(Stream stream) => _stream = stream;

        public async Task<string?> ReadLineAsync(CancellationToken ct)
        {
            var acc = new MemoryStream();
            while (true)
            {
                var b = await ReadByteAsync(ct).ConfigureAwait(false);
                if (b < 0)
                    return acc.Length == 0 ? null : Encoding.ASCII.GetString(acc.ToArray());
                if (b == '\n')
                {
                    var bytes = acc.ToArray();
                    if (bytes.Length > 0 && bytes[^1] == '\r')
                        return Encoding.ASCII.GetString(bytes, 0, bytes.Length - 1);
                    return Encoding.ASCII.GetString(bytes);
                }

                acc.WriteByte((byte)b);
            }
        }

        public async Task<byte[]> ReadExactAsync(int length, CancellationToken ct)
        {
            var data = new byte[length];
            var copied = 0;
            while (copied < length)
            {
                await EnsureAsync(ct).ConfigureAwait(false);
                if (_count == 0)
                    throw new IOException("Unexpected end of stream.");
                var n = Math.Min(length - copied, _count);
                Buffer.BlockCopy(_buffer, _offset, data, copied, n);
                _offset += n;
                _count -= n;
                copied += n;
            }

            return data;
        }

        public async Task<byte[]?> ReadJpegMarkerAsync(CancellationToken ct)
        {
            int prev = -1;
            while (true)
            {
                var b = await ReadByteAsync(ct).ConfigureAwait(false);
                if (b < 0) return null;
                if (prev == 0xFF && b == 0xD8)
                {
                    var ms = new MemoryStream();
                    ms.WriteByte(0xFF);
                    ms.WriteByte(0xD8);
                    prev = 0xD8;
                    while (true)
                    {
                        var n = await ReadByteAsync(ct).ConfigureAwait(false);
                        if (n < 0) return null;
                        ms.WriteByte((byte)n);
                        if (prev == 0xFF && n == 0xD9)
                            return ms.ToArray();
                        prev = n;
                    }
                }

                prev = b;
            }
        }

        private async Task<int> ReadByteAsync(CancellationToken ct)
        {
            await EnsureAsync(ct).ConfigureAwait(false);
            if (_count == 0) return -1;
            var b = _buffer[_offset];
            _offset++;
            _count--;
            return b;
        }

        private async Task EnsureAsync(CancellationToken ct)
        {
            if (_count > 0) return;
            _offset = 0;
            _count = await _stream.ReadAsync(_buffer.AsMemory(0, _buffer.Length), ct).ConfigureAwait(false);
        }
    }
}
