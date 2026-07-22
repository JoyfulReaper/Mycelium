using Mycelium.Contracts.Crawling;
using Mycelium.Contracts.Plugins;
using Mycelium.Plugins.HtmlLinks;
using System.Text;

namespace Mycelium.Test.HtmlLinks;

public sealed class HtmlLinkDiscoveryPluginTests
{
    [Fact]
    public async Task ProcessAsync_DiscoversHttpLinks()
    {
        const string Html =
            """
            <!doctype html>
            <html>
              <body>
                <a href="/about">About</a>
                <a href="https://other.example/news">News</a>
                <a href="mailto:test@example.com">Email</a>
                <a href="#section">Section</a>
              </body>
            </html>
            """;

        var plugin =
            new HtmlLinkDiscoveryPlugin();

        CrawlPluginResult result =
            await plugin.ProcessAsync(
                CreateResource(Html),
                CancellationToken.None);

        Assert.Collection(
            result.DiscoveredUrls,
            discovered =>
            {
                Assert.Equal(
                    new Uri(
                        "https://example.com/about"),
                    discovered.Uri);

                Assert.Equal(
                    "anchor",
                    discovered.Relationship);

                Assert.Equal(
                    "About",
                    discovered.Context);
            },
            discovered =>
            {
                Assert.Equal(
                    new Uri(
                        "https://other.example/news"),
                    discovered.Uri);

                Assert.Equal(
                    "anchor",
                    discovered.Relationship);

                Assert.Equal(
                    "News",
                    discovered.Context);
            });
    }

    [Fact]
    public async Task ProcessAsync_UsesHtmlBaseElement()
    {
        const string Html =
            """
            <!doctype html>
            <html>
              <head>
                <base href="/documentation/">
              </head>
              <body>
                <a href="getting-started">Start</a>
              </body>
            </html>
            """;

        var plugin =
            new HtmlLinkDiscoveryPlugin();

        CrawlPluginResult result =
            await plugin.ProcessAsync(
                CreateResource(Html),
                CancellationToken.None);

        DiscoveredUrl discovered =
            Assert.Single(
                result.DiscoveredUrls);

        Assert.Equal(
            new Uri(
                "https://example.com/documentation/getting-started"),
            discovered.Uri);
    }

    [Fact]
    public async Task ProcessAsync_IgnoresNonHtmlContent()
    {
        var plugin =
            new HtmlLinkDiscoveryPlugin();

        FetchedResource resource =
            CreateResource(
                """{"url":"https://example.com"}""",
                "application/json");

        CrawlPluginResult result =
            await plugin.ProcessAsync(
                resource,
                CancellationToken.None);

        Assert.Empty(result.DiscoveredUrls);
        Assert.Empty(result.Findings);
    }

    private static FetchedResource CreateResource(
        string content,
        string mediaType = "text/html; charset=utf-8")
    {
        var uri =
            new Uri("https://example.com/index.html");

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
            MediaType = mediaType,
            Content = Encoding.UTF8.GetBytes(content),
            TextContent = content,
            FetchedAt = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromMilliseconds(10)
        };
    }
}