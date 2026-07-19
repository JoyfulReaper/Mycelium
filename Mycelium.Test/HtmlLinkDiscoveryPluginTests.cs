using Mycelium.Contracts.Crawling;
using Mycelium.Contracts.Plugins;
using Mycelium.Plugins.HtmlLinks;
using System.Net;
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
                CreateDocument(Html),
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
                CreateDocument(Html),
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

        CrawlDocument document =
            CreateDocument(
                """{"url":"https://example.com"}""",
                "application/json");

        CrawlPluginResult result =
            await plugin.ProcessAsync(
                document,
                CancellationToken.None);

        Assert.Empty(result.DiscoveredUrls);
        Assert.Empty(result.Findings);
    }

    private static CrawlDocument CreateDocument(
        string content,
        string contentType = "text/html; charset=utf-8")
    {
        var uri =
            new Uri("https://example.com/index.html");

        return new CrawlDocument
        {
            Request = new CrawlRequest
            {
                Uri = uri,
                Mode = FetchMode.Http
            },
            FinalUri = uri,
            StatusCode = HttpStatusCode.OK,
            ContentType = contentType,
            Content = Encoding.UTF8.GetBytes(content),
            TextContent = content,
            FetchedAt = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromMilliseconds(10),
            FetchMode = FetchMode.Http
        };
    }
}