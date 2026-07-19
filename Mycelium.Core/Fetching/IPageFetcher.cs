using Mycelium.Contracts.Crawling;

namespace Mycelium.Core.Fetching;

public interface IPageFetcher
{
    Task<CrawlDocument> FetchAsync(
        CrawlRequest request,
        CancellationToken cancellationToken);
}