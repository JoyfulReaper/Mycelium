using Microsoft.Extensions.Logging;
using Mycelium.Contracts.Crawling;
using Mycelium.Contracts.Plugins;
using System.Diagnostics;

namespace Mycelium.Core.Plugins;

public sealed partial class CrawlPluginPipeline(
    IEnumerable<ICrawlPlugin> plugins,
    ILogger<CrawlPluginPipeline> logger)
    : ICrawlPluginPipeline
{
    private readonly IReadOnlyList<ICrawlPlugin> _plugins =
        plugins.ToArray();

    public async ValueTask<CrawlPluginResult> ExecuteAsync(
        CrawlDocument document,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        var discoveredUrls = new List<DiscoveredUrl>();
        var findings = new List<CrawlFinding>();
        BrowserRenderRequest? renderRequest = null;

        LogPipelineStarted(
            logger,
            _plugins.Count,
            document.FinalUri);

        foreach (ICrawlPlugin plugin in _plugins)
        {
            cancellationToken.ThrowIfCancellationRequested();

            long startedAt = Stopwatch.GetTimestamp();

            try
            {
                CrawlPluginResult result =
                    await plugin.ProcessAsync(
                        document,
                        cancellationToken);

                discoveredUrls.AddRange(result.DiscoveredUrls);
                findings.AddRange(result.Findings);

                // Rendering only needs to happen once. Retain the first
                // plugin's reason for requesting it.
                renderRequest ??= result.RenderRequest;

                long elapsedMilliseconds =
                    (long)Stopwatch
                        .GetElapsedTime(startedAt)
                        .TotalMilliseconds;

                LogPluginCompleted(
                    logger,
                    plugin.Name,
                    document.FinalUri,
                    elapsedMilliseconds,
                    result.DiscoveredUrls.Count,
                    result.Findings.Count);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // Caller cancellation must stop the pipeline immediately.
                throw;
            }
            catch (Exception exception)
            {
                // A broken plugin must not terminate the entire crawl.
                LogPluginFailed(
                    logger,
                    plugin.Name,
                    document.FinalUri,
                    exception);
            }
        }

        LogPipelineCompleted(
            logger,
            document.FinalUri,
            discoveredUrls.Count,
            findings.Count,
            renderRequest is not null);

        return new CrawlPluginResult
        {
            DiscoveredUrls = discoveredUrls,
            Findings = findings,
            RenderRequest = renderRequest
        };
    }

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Debug,
        Message =
            "Running {PluginCount} crawl plugins for {Uri}.")]
    private static partial void LogPipelineStarted(
        ILogger logger,
        int pluginCount,
        Uri uri);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Debug,
        Message =
            "Plugin {PluginName} processed {Uri} in " +
            "{ElapsedMilliseconds} ms and produced " +
            "{DiscoveredUrlCount} URLs and {FindingCount} findings.")]
    private static partial void LogPluginCompleted(
        ILogger logger,
        string pluginName,
        Uri uri,
        long elapsedMilliseconds,
        int discoveredUrlCount,
        int findingCount);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message =
            "Plugin {PluginName} failed while processing {Uri}; " +
            "continuing with the remaining plugins.")]
    private static partial void LogPluginFailed(
        ILogger logger,
        string pluginName,
        Uri uri,
        Exception exception);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Debug,
        Message =
            "Plugin pipeline completed for {Uri}: " +
            "{DiscoveredUrlCount} URLs, {FindingCount} findings, " +
            "browser rendering requested: {BrowserRenderRequested}.")]
    private static partial void LogPipelineCompleted(
        ILogger logger,
        Uri uri,
        int discoveredUrlCount,
        int findingCount,
        bool browserRenderRequested);
}