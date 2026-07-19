namespace Mycelium.Contracts.Crawling;

public sealed record CrawlRequest
{
    public required Uri Uri { get; init; }

    public Uri? Referrer { get; init; }

    public int Depth { get; init; }

    public FetchMode Mode { get; init; } =
        FetchMode.BrowserFallback;
}