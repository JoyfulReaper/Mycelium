namespace Mycelium.Contracts.Plugins;

public sealed record CrawlFinding(
    string Kind,
    string Value,
    string? Context = null,
    IReadOnlyDictionary<string, string?>? Metadata = null);