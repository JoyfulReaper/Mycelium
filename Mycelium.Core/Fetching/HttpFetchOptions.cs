namespace Mycelium.Core.Fetching;

public sealed class HttpFetchOptions
{
    public const string SectionName = "HttpFetch";

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxResponseBytes { get; set; } =
        4 * 1024 * 1024;

    public int MaxRedirects { get; set; } = 10;

    public string UserAgent { get; set; } =
        "Mycelium/0.1";
}