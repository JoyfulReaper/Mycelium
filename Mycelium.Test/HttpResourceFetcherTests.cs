using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mycelium.Contracts.Crawling;
using Mycelium.Core.Fetching;
using System.Net;
using System.Text;

namespace Mycelium.Test.Fetching;

public sealed class HttpResourceFetcherTests
{
    [Fact]
    public async Task FetchAsync_ReturnsTextResource()
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

        HttpResourceFetcher fetcher =
            CreateFetcher(handler);

        FetchedResource resource =
            await fetcher.FetchAsync(
                CreateRequest(),
                CancellationToken.None);

        Assert.NotNull(resource.ProtocolStatus);

        Assert.Equal(
            "200",
            resource.ProtocolStatus.Code);

        Assert.Equal(
            "OK",
            resource.ProtocolStatus.Description);

        Assert.Equal(
            Uri.UriSchemeHttps,
            resource.Protocol);

        Assert.StartsWith(
            "text/html",
            resource.MediaType,
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            Html,
            resource.TextContent);

        Assert.NotEmpty(
            resource.Content.ToArray());
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

        HttpResourceFetcher fetcher =
            CreateFetcher(handler);

        FetchedResource resource =
            await fetcher.FetchAsync(
                CreateRequest(),
                CancellationToken.None);

        Assert.Null(resource.TextContent);

        Assert.Equal(
            "application/octet-stream",
            resource.MediaType);

        Assert.Equal(
            4,
            resource.Content.Length);
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

        HttpResourceFetcher fetcher =
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
                        new Uri(
                            "/final",
                            UriKind.Relative);

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

        HttpResourceFetcher fetcher =
            CreateFetcher(handler);

        FetchedResource resource =
            await fetcher.FetchAsync(
                CreateRequest(),
                CancellationToken.None);

        Assert.Equal(
            new Uri("https://example.com/final"),
            resource.FinalUri);

        Assert.Equal(
            "200",
            resource.ProtocolStatus?.Code);

        Assert.Equal(
            2,
            requestCount);
    }

    [Theory]
    [InlineData("http://example.com/", true)]
    [InlineData("https://example.com/", true)]
    [InlineData("gopher://example.com/", false)]
    [InlineData("ftp://example.com/", false)]
    public void CanFetch_ReturnsExpectedResult(
        string uri,
        bool expected)
    {
        HttpResourceFetcher fetcher =
            CreateFetcher(
                new DelegateHandler(
                    (_, _) =>
                        throw new InvalidOperationException(
                            "No request should be sent.")));

        bool result =
            fetcher.CanFetch(
                new Uri(uri));

        Assert.Equal(
            expected,
            result);
    }

    private static HttpResourceFetcher CreateFetcher(
        HttpMessageHandler handler,
        int maximumBytes = 4096)
    {
        var client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        return new HttpResourceFetcher(
            client,
            Options.Create(
                new HttpFetchOptions
                {
                    TimeoutSeconds = 5,
                    MaxResponseBytes = maximumBytes,
                    MaxRedirects = 5,
                    UserAgent = "Mycelium.Test"
                }),
            NullLogger<HttpResourceFetcher>.Instance);
    }

    private static CrawlRequest CreateRequest() =>
        new()
        {
            Uri = new Uri(
                "https://example.com/")
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
            handler(
                request,
                cancellationToken);
    }
}