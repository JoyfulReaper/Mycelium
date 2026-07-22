using Mycelium.Contracts.Crawling;

namespace Mycelium.Contracts.Plugins;

public interface ICrawlPlugin
{
    string Name { get; }

    ValueTask<CrawlPluginResult> ProcessAsync(
        FetchedResource resource,
        CancellationToken cancellationToken);
}