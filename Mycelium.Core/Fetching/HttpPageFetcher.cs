using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mycelium.Contracts.Crawling;
using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Mycelium.Core.Fetching;

public sealed partial class HttpPageFetcher(
    HttpClient httpClient,
    IOptions<HttpFetchOptions> options,
    ILogger<HttpPageFetcher> logger)
    : IPageFetcher
{
    private const int BufferSize = 81920;

    private readonly HttpFetchOptions _options =
        options.Value;

    public async Task<CrawlDocument> FetchAsync(
        CrawlRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateUri(request.Uri);

        using var timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeoutSource.CancelAfter(
            TimeSpan.FromSeconds(
                _options.TimeoutSeconds));

        try
        {
            return await FetchCoreAsync(
                request,
                timeoutSource.Token);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Fetching {request.Uri} exceeded the configured " +
                $"timeout of {_options.TimeoutSeconds} seconds.",
                exception);
        }
    }

    private async Task<CrawlDocument> FetchCoreAsync(
        CrawlRequest request,
        CancellationToken cancellationToken)
    {
        Uri currentUri = request.Uri;
        long startedAt = Stopwatch.GetTimestamp();

        for (int redirectCount = 0; ; redirectCount++)
        {
            using var message = new HttpRequestMessage(
                HttpMethod.Get,
                currentUri);

            using HttpResponseMessage response =
                await httpClient.SendAsync(
                    message,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            if (IsRedirect(response.StatusCode) &&
                response.Headers.Location is not null)
            {
                if (redirectCount >= _options.MaxRedirects)
                {
                    throw new HttpRequestException(
                        $"Fetching {request.Uri} exceeded the configured " +
                        $"redirect limit of {_options.MaxRedirects}.");
                }

                Uri nextUri = ResolveRedirectUri(
                    currentUri,
                    response.Headers.Location);

                ValidateUri(nextUri);

                LogRedirect(
                    logger,
                    currentUri,
                    nextUri,
                    (int)response.StatusCode);

                currentUri = nextUri;
                continue;
            }

            Uri finalUri =
                response.RequestMessage?.RequestUri ??
                currentUri;

            byte[] content = await ReadContentAsync(
                response,
                finalUri,
                cancellationToken);

            string? textContent =
                DecodeTextContent(
                    response.Content.Headers.ContentType,
                    content);

            TimeSpan duration =
                Stopwatch.GetElapsedTime(startedAt);

            IReadOnlyDictionary<
                string,
                IReadOnlyList<string>> headers =
                    CopyHeaders(response);

            LogFetchCompleted(
                logger,
                (int)response.StatusCode,
                finalUri,
                duration.TotalMilliseconds,
                content.Length,
                response.Content.Headers.ContentType?.ToString());

            return new CrawlDocument
            {
                Request = request,
                FinalUri = finalUri,
                StatusCode = response.StatusCode,
                Headers = headers,
                ContentType =
                    response.Content.Headers.ContentType?.ToString(),
                Content = content,
                TextContent = textContent,
                FetchedAt = DateTimeOffset.UtcNow,
                Duration = duration,
                FetchMode = FetchMode.Http
            };
        }
    }

    private async Task<byte[]> ReadContentAsync(
        HttpResponseMessage response,
        Uri uri,
        CancellationToken cancellationToken)
    {
        long? declaredLength =
            response.Content.Headers.ContentLength;

        if (declaredLength > _options.MaxResponseBytes)
        {
            LogResponseTooLarge(
                logger,
                uri,
                declaredLength.Value,
                _options.MaxResponseBytes);

            throw new ResponseTooLargeException(
                uri,
                declaredLength.Value,
                _options.MaxResponseBytes);
        }

        await using Stream input =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        using var output = declaredLength is > 0 and <= int.MaxValue
            ? new MemoryStream((int)declaredLength.Value)
            : new MemoryStream();

        byte[] buffer =
            ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            while (true)
            {
                int bytesRead = await input.ReadAsync(
                    buffer.AsMemory(0, BufferSize),
                    cancellationToken);

                if (bytesRead == 0)
                {
                    break;
                }

                if (output.Length + bytesRead >
                    _options.MaxResponseBytes)
                {
                    LogResponseTooLarge(
                        logger,
                        uri,
                        output.Length + bytesRead,
                        _options.MaxResponseBytes);

                    throw new ResponseTooLargeException(
                        uri,
                        output.Length + bytesRead,
                        _options.MaxResponseBytes);
                }

                await output.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    cancellationToken);
            }

            return output.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static string? DecodeTextContent(
        MediaTypeHeaderValue? contentType,
        byte[] content)
    {
        if (!IsTextContentType(contentType?.MediaType))
        {
            return null;
        }

        Encoding encoding =
            ResolveEncoding(contentType?.CharSet);

        using var stream = new MemoryStream(
            content,
            writable: false);

        using var reader = new StreamReader(
            stream,
            encoding,
            detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd();
    }

    private static Encoding ResolveEncoding(
        string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8;
        }

        string normalized =
            charset.Trim().Trim('"');

        try
        {
            return Encoding.GetEncoding(normalized);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    private static bool IsTextContentType(
        string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return false;
        }

        return mediaType.StartsWith(
                   "text/",
                   StringComparison.OrdinalIgnoreCase) ||
               mediaType.Equals(
                   "application/json",
                   StringComparison.OrdinalIgnoreCase) ||
               mediaType.EndsWith(
                   "+json",
                   StringComparison.OrdinalIgnoreCase) ||
               mediaType.Equals(
                   "application/xml",
                   StringComparison.OrdinalIgnoreCase) ||
               mediaType.EndsWith(
                   "+xml",
                   StringComparison.OrdinalIgnoreCase) ||
               mediaType.Contains(
                   "javascript",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<
        string,
        IReadOnlyList<string>> CopyHeaders(
            HttpResponseMessage response)
    {
        var headers =
            new Dictionary<
                string,
                IReadOnlyList<string>>(
                    StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<
                     string,
                     IEnumerable<string>> header
                 in response.Headers)
        {
            headers[header.Key] =
                header.Value.ToArray();
        }

        foreach (KeyValuePair<
                     string,
                     IEnumerable<string>> header
                 in response.Content.Headers)
        {
            headers[header.Key] =
                header.Value.ToArray();
        }

        return headers;
    }

    private static Uri ResolveRedirectUri(
        Uri currentUri,
        Uri location) =>
        location.IsAbsoluteUri
            ? location
            : new Uri(currentUri, location);

    private static bool IsRedirect(
        HttpStatusCode statusCode) =>
        statusCode is
            HttpStatusCode.MovedPermanently or
            HttpStatusCode.Redirect or
            HttpStatusCode.RedirectMethod or
            HttpStatusCode.TemporaryRedirect or
            HttpStatusCode.PermanentRedirect;

    private static void ValidateUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "Only absolute HTTP and HTTPS URLs are supported.",
                nameof(uri));
        }
    }

    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Debug,
        Message =
            "Following HTTP redirect {StatusCode} from {FromUri} to {ToUri}.")]
    private static partial void LogRedirect(
        ILogger logger,
        Uri fromUri,
        Uri toUri,
        int statusCode);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message =
            "Fetched {StatusCode} {Uri} in {ElapsedMilliseconds:F1} ms; " +
            "{ByteCount} bytes, content type {ContentType}.")]
    private static partial void LogFetchCompleted(
        ILogger logger,
        int statusCode,
        Uri uri,
        double elapsedMilliseconds,
        int byteCount,
        string? contentType);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Warning,
        Message =
            "Response from {Uri} contained {ResponseBytes} bytes, " +
            "exceeding the configured limit of {MaximumBytes} bytes.")]
    private static partial void LogResponseTooLarge(
        ILogger logger,
        Uri uri,
        long responseBytes,
        int maximumBytes);
}