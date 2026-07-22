using Mycelium.Contracts.Crawling;

namespace Mycelium.Core.Fetching;

public interface IResourceFetcher
{
    bool CanFetch(Uri uri);

    Task<FetchedResource> FetchAsync(
        CrawlRequest request,
        CancellationToken cancellationToken);
}