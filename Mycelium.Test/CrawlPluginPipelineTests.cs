using Microsoft.Extensions.Logging.Abstractions;
using Mycelium.Contracts.Crawling;
using Mycelium.Contracts.Plugins;
using Mycelium.Core.Plugins;

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
                        ]
                    });
            });

        CrawlPluginPipeline pipeline =
            CreatePipeline(first, second);

        CrawlPluginResult result =
            await pipeline.ExecuteAsync(
                CreateResource(),
                CancellationToken.None);

        Assert.Equal(
            ["first", "second"],
            calls);

        Assert.Equal(
            2,
            result.DiscoveredUrls.Count);

        Assert.Single(result.Findings);
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

        CrawlPluginPipeline pipeline =
            CreatePipeline(
                failingPlugin,
                successfulPlugin);

        await pipeline.ExecuteAsync(
            CreateResource(),
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

        CrawlPluginPipeline pipeline =
            CreatePipeline(plugin);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await pipeline.ExecuteAsync(
                    CreateResource(),
                    cancellationSource.Token));
    }

    private static CrawlPluginPipeline CreatePipeline(
        params ICrawlPlugin[] plugins) =>
        new(
            plugins,
            NullLogger<CrawlPluginPipeline>.Instance);

    private static FetchedResource CreateResource()
    {
        var uri =
            new Uri("https://example.com/");

        return new FetchedResource
        {
            Request = new CrawlRequest
            {
                Uri = uri
            },
            FinalUri = uri,
            ProtocolStatus = new ResourceStatus(
                Code: "200",
                Description: "OK"),
            MediaType = "text/html; charset=utf-8",
            Content = ReadOnlyMemory<byte>.Empty,
            TextContent = "<html></html>",
            FetchedAt = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromMilliseconds(20)
        };
    }

    private sealed class DelegatePlugin(
        string name,
        Func<
            FetchedResource,
            CancellationToken,
            ValueTask<CrawlPluginResult>> process)
        : ICrawlPlugin
    {
        public string Name { get; } = name;

        public ValueTask<CrawlPluginResult> ProcessAsync(
            FetchedResource resource,
            CancellationToken cancellationToken) =>
            process(resource, cancellationToken);
    }
}