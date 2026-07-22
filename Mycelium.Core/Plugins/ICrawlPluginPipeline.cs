using Mycelium.Contracts.Crawling;
using Mycelium.Contracts.Plugins;

namespace Mycelium.Core.Plugins;

public interface ICrawlPluginPipeline
{
    ValueTask<CrawlPluginResult> ExecuteAsync(
        FetchedResource resource,
        CancellationToken cancellationToken);
}