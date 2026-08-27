using MaksIT.Wvc210.Shared;

namespace MaksIT.Wvc210.Client;

public sealed class TalkbackSession : IDisposable
{
    private const int ClipBytes = 1000; // 125 ms — matches the camera's own G.711 frames
    private readonly CameraClient _client;
    private readonly TalkCodec _codec;
    private readonly ClipPump _pump;
    private IMicrophoneCapture? _capture;
    private int _gainPercent = 200;

    public TalkbackSession(CameraClient client, TalkCodec codec)
    {
        _client = client;
        _codec = codec;
        _pump = new ClipPump(client, codec);
    }

    public event Action<double>? LevelChanged;
    public event Action<string>? Failed;
    public string Path => _codec == TalkCodec.G711A ? "/img/g711a.cgi" : "/img/g711u.cgi";

    public void SetGain(int percent) => _gainPercent = Math.Clamp(percent, 10, 400);

    public void Attach(IMicrophoneCapture capture)
    {
        _capture = capture;
        capture.Pcm8000Mono += OnPcm;
        capture.Start();
        _pump.Failed += msg => Failed?.Invoke(msg);
        _pump.Start();
    }

    public void Dispose()
    {
        if (_capture is not null)
            _capture.Pcm8000Mono -= OnPcm;
        _capture?.Dispose();
        _pump.Dispose();
    }

    private void OnPcm(short[] pcm)
    {
        if (_gainPercent != 100)
        {
            var g = _gainPercent / 100.0;
            for (var i = 0; i < pcm.Length; i++)
            {
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
        _pump.Enqueue(encoded);
    }

    private sealed class ClipPump : IDisposable
    {
        private readonly CameraClient _client;
        private readonly TalkCodec _codec;
        private readonly System.Threading.Channels.Channel<byte[]> _channel =
            System.Threading.Channels.Channel.CreateBounded<byte[]>(new System.Threading.Channels.BoundedChannelOptions(32)
            {
                FullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
        private readonly CancellationTokenSource _cts = new();
        private Task? _loop;

        public ClipPump(CameraClient client, TalkCodec codec)
        {
            _client = client;
            _codec = codec;
        }

        public event Action<string>? Failed;

        public void Start() => _loop = Task.Run(RunAsync);

        public void Enqueue(byte[] data) => _channel.Writer.TryWrite(data);

        private async Task RunAsync()
        {
            var pending = new byte[ClipBytes];
            var pendingCount = 0;
            try
            {
                await foreach (var frame in _channel.Reader.ReadAllAsync(_cts.Token))
                {
                    var offset = 0;
                    while (offset < frame.Length)
                    {
                        var n = Math.Min(ClipBytes - pendingCount, frame.Length - offset);
                        Buffer.BlockCopy(frame, offset, pending, pendingCount, n);
                        pendingCount += n;
                        offset += n;
                        if (pendingCount < ClipBytes)
                            continue;
                        await _client.PostTalkClipAsync(pending, ClipBytes, _codec, _cts.Token).ConfigureAwait(false);
                        pendingCount = 0;
                    }
                }

                if (pendingCount > 0)
                    await _client.PostTalkClipAsync(pending, pendingCount, _codec, CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Failed?.Invoke(ex.Message);
            }
        }

        public void Dispose()
        {
            _channel.Writer.TryComplete();
            _cts.Cancel();
            try { _loop?.Wait(800); } catch { }
            _cts.Dispose();
        }
    }
}
