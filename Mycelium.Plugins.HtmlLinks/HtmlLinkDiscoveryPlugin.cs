using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Mycelium.Contracts.Crawling;
using Mycelium.Contracts.Plugins;

namespace Mycelium.Plugins.HtmlLinks;

public sealed class HtmlLinkDiscoveryPlugin : ICrawlPlugin
{
    private const int MaximumContextLength = 200;

    public string Name => "html-links";

    public ValueTask<CrawlPluginResult> ProcessAsync(
        CrawlDocument document,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        cancellationToken.ThrowIfCancellationRequested();

        if (!CanProcess(document))
        {
            return ValueTask.FromResult(
                CrawlPluginResult.Empty);
        }

        var parser = new HtmlParser();

        IDocument htmlDocument =
            parser.ParseDocument(
                document.TextContent!);

        cancellationToken.ThrowIfCancellationRequested();

        Uri baseUri = ResolveBaseUri(
            htmlDocument,
            document.FinalUri);

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

    private static bool CanProcess(
        CrawlDocument document)
    {
        if (document.TextContent is null ||
            string.IsNullOrWhiteSpace(
                document.ContentType))
        {
            return false;
        }

        return document.ContentType.StartsWith(
                   "text/html",
                   StringComparison.OrdinalIgnoreCase) ||
               document.ContentType.StartsWith(
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

        return TryResolveHttpUri(
            fallbackUri,
            baseHref,
            out Uri? resolvedBaseUri)
                ? resolvedBaseUri
                : fallbackUri;
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

            string relationship =
                element.GetAttribute("rel")?.Trim();

            if (string.IsNullOrWhiteSpace(
                    relationship))
            {
                relationship = "link";
            }

            discoveredUrls.Add(
                new DiscoveredUrl(
                    resolvedUri,
                    Relationship: relationship,
                    Context: null));
        }
    }

    private static bool TryResolveHttpUri(
        Uri baseUri,
        string? rawUrl,
        out Uri? resolvedUri)
    {
        resolvedUri = null;

        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return false;
        }

        string trimmedUrl = rawUrl.Trim();

        // A fragment is navigation within the current document,
        // not another resource to crawl.
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