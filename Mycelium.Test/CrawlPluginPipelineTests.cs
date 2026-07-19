using Microsoft.Extensions.Logging.Abstractions;
using Mycelium.Contracts.Crawling;
using Mycelium.Contracts.Plugins;
using Mycelium.Core.Plugins;
using System.Net;

namespace Mycelium.Test.Plugins;

public sealed class CrawlPluginPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_RunsPluginsInOrderAndAggregatesResults()
    {
        var calls = new List<string>();

        var first = new DelegatePlugin(
            "first",
            (_, _) =>
            {
                calls.Add("first");

                return ValueTask.FromResult(
                    new CrawlPluginResult
                    {
                        DiscoveredUrls =
                        [
                            new DiscoveredUrl(
                                new Uri("https://example.com/one"))
                        ]
                    });
            });

        var second = new DelegatePlugin(
            "second",
            (_, _) =>
            {
                calls.Add("second");

                return ValueTask.FromResult(
                    new CrawlPluginResult
                    {
                        DiscoveredUrls =
                        [
                            new DiscoveredUrl(
                                new Uri("https://example.com/two"))
                        ],
                        Findings =
                        [
                            new CrawlFinding(
                                Kind: "keyword",
                                Value: "mycelium")
                        ],
                        RenderRequest =
                            new BrowserRenderRequest(
                                "JavaScript shell detected.")
                    });
            });

        var pipeline = CreatePipeline(first, second);

        CrawlPluginResult result =
            await pipeline.ExecuteAsync(
                CreateDocument(),
                CancellationToken.None);

        Assert.Equal(
            new[] { "first", "second" },
            calls);

        Assert.Equal(2, result.DiscoveredUrls.Count);
        Assert.Single(result.Findings);
        Assert.NotNull(result.RenderRequest);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesAfterPluginFailure()
    {
        bool secondPluginRan = false;

        var failingPlugin = new DelegatePlugin(
            "failing",
            (_, _) =>
                throw new InvalidOperationException(
                    "Plugin failure."));

        var successfulPlugin = new DelegatePlugin(
            "successful",
            (_, _) =>
            {
                secondPluginRan = true;

                return ValueTask.FromResult(
                    CrawlPluginResult.Empty);
            });

        var pipeline = CreatePipeline(
            failingPlugin,
            successfulPlugin);

        await pipeline.ExecuteAsync(
            CreateDocument(),
            CancellationToken.None);

        Assert.True(secondPluginRan);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesCallerCancellation()
    {
        using var cancellationSource =
            new CancellationTokenSource();

        var plugin = new DelegatePlugin(
            "cancelling",
            (_, cancellationToken) =>
            {
                cancellationSource.Cancel();
                cancellationToken.ThrowIfCancellationRequested();

                return ValueTask.FromResult(
                    CrawlPluginResult.Empty);
            });

        var pipeline = CreatePipeline(plugin);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await pipeline.ExecuteAsync(
                    CreateDocument(),
                    cancellationSource.Token));
    }

    private static CrawlPluginPipeline CreatePipeline(
        params ICrawlPlugin[] plugins) =>
        new(
            plugins,
            NullLogger<CrawlPluginPipeline>.Instance);

    private static CrawlDocument CreateDocument()
    {
        var uri = new Uri("https://example.com/");

        return new CrawlDocument
        {
            Request = new CrawlRequest
            {
                Uri = uri,
                Mode = FetchMode.Http
            },
            FinalUri = uri,
            StatusCode = HttpStatusCode.OK,
            Content = ReadOnlyMemory<byte>.Empty,
            TextContent = "<html></html>",
            FetchedAt = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromMilliseconds(20),
            FetchMode = FetchMode.Http
        };
    }

    private sealed class DelegatePlugin(
        string name,
        Func<
            CrawlDocument,
            CancellationToken,
            ValueTask<CrawlPluginResult>> process)
        : ICrawlPlugin
    {
        public string Name { get; } = name;

        public ValueTask<CrawlPluginResult> ProcessAsync(
            CrawlDocument document,
            CancellationToken cancellationToken) =>
            process(document, cancellationToken);
    }
}