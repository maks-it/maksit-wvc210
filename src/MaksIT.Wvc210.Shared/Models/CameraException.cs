namespace MaksIT.Wvc210.Shared;

public sealed class CameraException : Exception
{
    public CameraException(string message) : base(message) { }
    public CameraException(string message, Exception inner) : base(message, inner) { }
}
