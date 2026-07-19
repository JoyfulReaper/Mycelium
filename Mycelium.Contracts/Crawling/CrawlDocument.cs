using System.Net;

namespace Mycelium.Contracts.Crawling;

public sealed record CrawlDocument
{
    public required CrawlRequest Request { get; init; }

    public required Uri FinalUri { get; init; }

    public required HttpStatusCode StatusCode { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers
    {
        get;
        init;
    } = new Dictionary<string, IReadOnlyList<string>>(
        StringComparer.OrdinalIgnoreCase);

    public string? ContentType { get; init; }

    public ReadOnlyMemory<byte> Content { get; init; }

    public string? TextContent { get; init; }

    public required DateTimeOffset FetchedAt { get; init; }

    public required TimeSpan Duration { get; init; }

    public required FetchMode FetchMode { get; init; }
}