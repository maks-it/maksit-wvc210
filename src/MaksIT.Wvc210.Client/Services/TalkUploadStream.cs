using System.Net.Sockets;
using System.Text;
using MaksIT.Wvc210.Shared;


namespace MaksIT.Wvc210.Client;

/// <summary>
/// Cisco talk CGI: POST headers, then stream G.711 on the same TCP socket.
/// The camera often does not send HTTP 200 until audio arrives, so we do not wait.
/// Close with a send-side FIN (end of HTTP/1.0 POST). Do not RST — Windows reports that
/// as "connection was aborted by the software in your host machine" and the camera
/// keeps talk locked, so the next test cannot write.
/// </summary>
public sealed class TalkUploadStream : IDisposable {
  private readonly string _host;
  private readonly int _port;
  private readonly string _authorization;
  private readonly string _path;
  private TcpClient? _tcp;
  private NetworkStream? _stream;

  internal TalkUploadStream(string host, int port, string username, string password, TalkCodec codec) {
    _host = host;
    _port = port;
    _authorization = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
    _path = codec == TalkCodec.G711A ? "/img/g711a.cgi" : "/img/g711u.cgi";
  }

  public string Path => _path;

  internal async Task OpenAsync(CancellationToken ct) {
    Dispose();
    var tcp = new TcpClient { NoDelay = true };
    NetworkStream? stream = null;
    try {
      await tcp.ConnectAsync(_host, _port, ct).ConfigureAwait(false);
      stream = tcp.GetStream();
      var header =
        $"POST {_path} HTTP/1.0\r\n" +
        $"Host: {_host}\r\n" +
        $"Authorization: Basic {_authorization}\r\n" +
        "User-Agent: CameraActiveX\r\n" +
        "\r\n";
      await stream.WriteAsync(Encoding.ASCII.GetBytes(header), ct).ConfigureAwait(false);
      await stream.FlushAsync(ct).ConfigureAwait(false);
      _tcp = tcp;
      _stream = stream;
    }
    catch {
      try { stream?.Dispose(); } catch { }
      try { tcp.Dispose(); } catch { }
      throw;
    }
  }

  public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct) {
    if (_stream is null)
      throw new CameraException("Talk CGI is not open.");
    if (data.Length == 0)
      return;
    await _stream.WriteAsync(data, ct).ConfigureAwait(false);
  }

  public void Dispose() {
    var stream = _stream;
    var tcp = _tcp;
    _stream = null;
    _tcp = null;
    if (tcp is null) {
      try { stream?.Dispose(); } catch { }
      return;
    }

    try {
      var socket = tcp.Client;
      if (socket is { Connected: true }) {
        try { socket.Shutdown(SocketShutdown.Send); } catch { }
        try {
          if (socket.Poll(400_000, SelectMode.SelectRead)) {
            var buf = new byte[256];
            try { socket.Receive(buf); } catch { }
          }
        }
        catch { }
      }
    }
    catch { }

    try { stream?.Close(); } catch { }
    try { tcp.Close(); } catch { }
  }
}
