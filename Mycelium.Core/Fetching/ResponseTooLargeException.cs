namespace Mycelium.Core.Fetching;

public sealed class ResponseTooLargeException(
    Uri uri,
    long responseBytes,
    int maximumBytes)
    : IOException(
        $"The response from {uri} contained at least " +
        $"{responseBytes} bytes, exceeding the configured " +
        $"limit of {maximumBytes} bytes.")
{
    public Uri Uri { get; } = uri;

    public long ResponseBytes { get; } = responseBytes;

    public int MaximumBytes { get; } = maximumBytes;
}