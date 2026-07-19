using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mycelium.Contracts.Crawling;
using Mycelium.Core.Fetching;
using System.Net;
using System.Text;

namespace Mycelium.Test.Fetching;

public sealed class HttpPageFetcherTests
{
    [Fact]
    public async Task FetchAsync_ReturnsTextDocument()
    {
        const string Html =
            "<html><body>Mycelium</body></html>";

        var handler = new DelegateHandler(
            (_, _) =>
            {
                var response =
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            Html,
                            Encoding.UTF8,
                            "text/html")
                    };

                return Task.FromResult(response);
            });

        HttpPageFetcher fetcher =
            CreateFetcher(handler);

        CrawlDocument document =
            await fetcher.FetchAsync(
                CreateRequest(),
                CancellationToken.None);

        Assert.Equal(
            HttpStatusCode.OK,
            document.StatusCode);

        Assert.Equal(Html, document.TextContent);
        Assert.NotEmpty(document.Content.ToArray());
        Assert.Equal(FetchMode.Http, document.FetchMode);
    }

    [Fact]
    public async Task FetchAsync_DoesNotDecodeBinaryContent()
    {
        var handler = new DelegateHandler(
            (_, _) =>
            {
                var content =
                    new ByteArrayContent([1, 2, 3, 4]);

                content.Headers.ContentType =
                    new("application/octet-stream");

                return Task.FromResult(
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content = content
                    });
            });

        HttpPageFetcher fetcher =
            CreateFetcher(handler);

        CrawlDocument document =
            await fetcher.FetchAsync(
                CreateRequest(),
                CancellationToken.None);

        Assert.Null(document.TextContent);
        Assert.Equal(4, document.Content.Length);
    }

    [Fact]
    public async Task FetchAsync_RejectsOversizedResponse()
    {
        var handler = new DelegateHandler(
            (_, _) =>
            {
                var content =
                    new ByteArrayContent(
                        new byte[32]);

                return Task.FromResult(
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content = content
                    });
            });

        HttpPageFetcher fetcher =
            CreateFetcher(
                handler,
                maximumBytes: 16);

        await Assert.ThrowsAsync<
            ResponseTooLargeException>(
                () => fetcher.FetchAsync(
                    CreateRequest(),
                    CancellationToken.None));
    }

    [Fact]
    public async Task FetchAsync_FollowsRelativeRedirect()
    {
        int requestCount = 0;

        var handler = new DelegateHandler(
            (request, _) =>
            {
                requestCount++;

                if (requestCount == 1)
                {
                    var redirect =
                        new HttpResponseMessage(
                            HttpStatusCode.Redirect);

                    redirect.Headers.Location =
                        new Uri("/final", UriKind.Relative);

                    return Task.FromResult(redirect);
                }

                Assert.Equal(
                    "https://example.com/final",
                    request.RequestUri?.ToString());

                return Task.FromResult(
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "done")
                    });
            });

        HttpPageFetcher fetcher =
            CreateFetcher(handler);

        CrawlDocument document =
            await fetcher.FetchAsync(
                CreateRequest(),
                CancellationToken.None);

        Assert.Equal(
            new Uri("https://example.com/final"),
            document.FinalUri);

        Assert.Equal(2, requestCount);
    }

    private static HttpPageFetcher CreateFetcher(
        HttpMessageHandler handler,
        int maximumBytes = 4096)
    {
        var client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        return new HttpPageFetcher(
            client,
            Options.Create(
                new HttpFetchOptions
                {
                    TimeoutSeconds = 5,
                    MaxResponseBytes = maximumBytes,
                    MaxRedirects = 5,
                    UserAgent = "Mycelium.Test"
                }),
            NullLogger<HttpPageFetcher>.Instance);
    }

    private static CrawlRequest CreateRequest() =>
        new()
        {
            Uri = new Uri("https://example.com/"),
            Mode = FetchMode.Http
        };

    private sealed class DelegateHandler(
        Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}