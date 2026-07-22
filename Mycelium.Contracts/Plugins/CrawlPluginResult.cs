namespace Mycelium.Contracts.Plugins;

public sealed record CrawlPluginResult
{
    public static CrawlPluginResult Empty { get; } = new();

    public IReadOnlyList<DiscoveredUrl> DiscoveredUrls
    {
        get;
        init;
    } = [];

    public IReadOnlyList<CrawlFinding> Findings
    {
        get;
        init;
    } = [];
}