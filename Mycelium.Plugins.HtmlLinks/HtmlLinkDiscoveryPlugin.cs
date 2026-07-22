using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Mycelium.Contracts.Crawling;
using Mycelium.Contracts.Plugins;
using System.Diagnostics.CodeAnalysis;

namespace Mycelium.Plugins.HtmlLinks;

public sealed class HtmlLinkDiscoveryPlugin : ICrawlPlugin
{
    private const int MaximumContextLength = 200;

    public string Name => "html-links";

    public ValueTask<CrawlPluginResult> ProcessAsync(
        FetchedResource resource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resource);

        cancellationToken.ThrowIfCancellationRequested();

        if (resource.TextContent is not { } textContent ||
        !CanProcessMediaType(resource.MediaType))
        {
            return ValueTask.FromResult(
                CrawlPluginResult.Empty);
        }

        var parser = new HtmlParser();

        IDocument htmlDocument =
            parser.ParseDocument(textContent);

        cancellationToken.ThrowIfCancellationRequested();

        Uri baseUri = ResolveBaseUri(
            htmlDocument,
            resource.FinalUri);

        var discoveredUrls =
            new List<DiscoveredUrl>();

        AddCandidates(
            htmlDocument.QuerySelectorAll("a[href]"),
            attributeName: "href",
            relationship: "anchor",
            baseUri,
            discoveredUrls);

        AddCandidates(
            htmlDocument.QuerySelectorAll("area[href]"),
            attributeName: "href",
            relationship: "area",
            baseUri,
            discoveredUrls);

        AddCandidates(
            htmlDocument.QuerySelectorAll("iframe[src]"),
            attributeName: "src",
            relationship: "iframe",
            baseUri,
            discoveredUrls);

        AddLinkElements(
            htmlDocument,
            baseUri,
            discoveredUrls);

        return ValueTask.FromResult(
            new CrawlPluginResult
            {
                DiscoveredUrls = discoveredUrls
            });
    }

    private static bool CanProcessMediaType(
        string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return false;
        }

        return mediaType.StartsWith(
                   "text/html",
                   StringComparison.OrdinalIgnoreCase) ||
               mediaType.StartsWith(
                   "application/xhtml+xml",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static Uri ResolveBaseUri(
        IDocument document,
        Uri fallbackUri)
    {
        string? baseHref =
            document
                .QuerySelector("base[href]")
                ?.GetAttribute("href");

        if (TryResolveHttpUri(
                fallbackUri,
                baseHref,
                out Uri? resolvedBaseUri))
        {
            return resolvedBaseUri;
        }

        return fallbackUri;
    }

    private static void AddCandidates(
        IEnumerable<IElement> elements,
        string attributeName,
        string relationship,
        Uri baseUri,
        ICollection<DiscoveredUrl> discoveredUrls)
    {
        foreach (IElement element in elements)
        {
            string? rawUrl =
                element.GetAttribute(attributeName);

            if (!TryResolveHttpUri(
                    baseUri,
                    rawUrl,
                    out Uri? resolvedUri))
            {
                continue;
            }

            discoveredUrls.Add(
                new DiscoveredUrl(
                    resolvedUri,
                    Relationship: relationship,
                    Context: GetContext(element)));
        }
    }

    private static void AddLinkElements(
        IDocument document,
        Uri baseUri,
        ICollection<DiscoveredUrl> discoveredUrls)
    {
        foreach (IElement element in
                 document.QuerySelectorAll("link[href]"))
        {
            string? rawUrl =
                element.GetAttribute("href");

            if (!TryResolveHttpUri(
                    baseUri,
                    rawUrl,
                    out Uri? resolvedUri))
            {
                continue;
            }

            string? rel =
                element.GetAttribute("rel")?.Trim();

            string relationship =
                string.IsNullOrWhiteSpace(rel)
                    ? "link"
                    : rel;

            discoveredUrls.Add(
                new DiscoveredUrl(
                    resolvedUri,
                    Relationship: relationship));
        }
    }

    private static bool TryResolveHttpUri(
        Uri baseUri,
        string? rawUrl,
        [NotNullWhen(true)] out Uri? resolvedUri)
    {
        resolvedUri = null;

        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return false;
        }

        string trimmedUrl = rawUrl.Trim();

        if (trimmedUrl.StartsWith('#'))
        {
            return false;
        }

        if (!Uri.TryCreate(
                baseUri,
                trimmedUrl,
                out Uri? candidate))
        {
            return false;
        }

        if (candidate.Scheme != Uri.UriSchemeHttp &&
            candidate.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        resolvedUri = candidate;
        return true;
    }

    private static string? GetContext(
        IElement element)
    {
        string context =
            element.TextContent.Trim();

        if (context.Length == 0)
        {
            return null;
        }

        if (context.Length <= MaximumContextLength)
        {
            return context;
        }

        return string.Concat(
            context.AsSpan(
                0,
                MaximumContextLength - 3),
            "...");
    }
}