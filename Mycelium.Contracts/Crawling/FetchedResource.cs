namespace Mycelium.Contracts.Crawling;

public sealed record FetchedResource
{
    public required CrawlRequest Request { get; init; }

    public required Uri FinalUri { get; init; }

    public string Protocol => FinalUri.Scheme;

    public ResourceStatus? ProtocolStatus { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Metadata
    {
        get;
        init;
    } = new Dictionary<string, IReadOnlyList<string>>();

    public string? MediaType { get; init; }

    public ReadOnlyMemory<byte> Content { get; init; }

    public string? TextContent { get; init; }

    public required DateTimeOffset FetchedAt { get; init; }

    public required TimeSpan Duration { get; init; }
}