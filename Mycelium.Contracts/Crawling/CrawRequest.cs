namespace Mycelium.Contracts.Crawling;

public sealed record CrawlRequest
{
    public required Uri Uri { get; init; }

    public Uri? DiscoveredFrom { get; init; }

    public int Depth { get; init; }
}