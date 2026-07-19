using Microsoft.Extensions.Logging;
using Mycelium.Contracts.Crawling;
using Mycelium.Contracts.Plugins;
using Mycelium.Core.Fetching;
using Mycelium.Core.Plugins;

namespace Mycelium.Cli;

public sealed class MyceliumApplication(
    IEnumerable<ICrawlPlugin> plugins,
    IPageFetcher pageFetcher,
    ICrawlPluginPipeline pluginPipeline,
    ILogger<MyceliumApplication> logger)
{
    private readonly IReadOnlyList<ICrawlPlugin> _plugins =
        plugins.ToArray();

    public async Task<int> RunAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogInformation(
            "Mycelium initialized with {PluginCount} plugins.",
            _plugins.Count);

        if (args.Length == 0 ||
            IsHelpCommand(args[0]))
        {
            WriteHelp();
            return 0;
        }

        if (string.Equals(
                args[0],
                "plugins",
                StringComparison.OrdinalIgnoreCase))
        {
            return RunPluginsCommand(args);
        }

        if (string.Equals(
            args[0],
            "fetch",
            StringComparison.OrdinalIgnoreCase))
        {
            return await RunFetchCommandAsync(
                args,
                cancellationToken);
        }

        logger.LogWarning(
            "Unknown command {Command}.",
            args[0]);

        Console.Error.WriteLine(
            $"Unknown command: {args[0]}");
        Console.Error.WriteLine();

        WriteHelp();

        return 2;
    }

    private async Task<int> RunFetchCommandAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        if (args.Length != 2 ||
            !Uri.TryCreate(
                args[1],
                UriKind.Absolute,
                out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            Console.Error.WriteLine(
                "Usage: mycelium fetch <http-or-https-url>");

            return 2;
        }

        try
        {
            var request = new CrawlRequest
            {
                Uri = uri,
                Mode = FetchMode.Http
            };

            CrawlDocument document =
                await pageFetcher.FetchAsync(
                    request,
                    cancellationToken);

            CrawlPluginResult pluginResult =
                await pluginPipeline.ExecuteAsync(
                    document,
                    cancellationToken);

            Console.WriteLine($"Requested:    {request.Uri}");
            Console.WriteLine($"Final URL:    {document.FinalUri}");
            Console.WriteLine(
                $"Status:       {(int)document.StatusCode} " +
                $"{document.StatusCode}");
            Console.WriteLine(
                $"Content type: {document.ContentType ?? "unknown"}");
            Console.WriteLine(
                $"Bytes:        {document.Content.Length}");
            Console.WriteLine(
                $"Text decoded: {document.TextContent is not null}");
            Console.WriteLine(
                $"Duration:     {document.Duration.TotalMilliseconds:F1} ms");
            Console.WriteLine(
                $"URLs found:   {pluginResult.DiscoveredUrls.Count}");
            Console.WriteLine(
                $"Findings:     {pluginResult.Findings.Count}");

            return 0;
        }
        catch (Exception exception)
            when (exception is
                HttpRequestException or
                TimeoutException or
                ResponseTooLargeException)
        {
            logger.LogError(
                exception,
                "Unable to fetch {Uri}.",
                uri);

            return 1;
        }
    }

    private int RunPluginsCommand(string[] args)
    {
        if (args.Length > 2 ||
            (args.Length == 2 &&
             !string.Equals(
                 args[1],
                 "list",
                 StringComparison.OrdinalIgnoreCase)))
        {
            Console.Error.WriteLine(
                "Usage: mycelium plugins list");

            return 2;
        }

        if (_plugins.Count == 0)
        {
            Console.WriteLine("No plugins registered.");
            return 0;
        }

        Console.WriteLine(
            $"Registered plugins ({_plugins.Count}):");

        foreach (ICrawlPlugin plugin in _plugins)
        {
            Console.WriteLine($"  {plugin.Name}");
        }

        return 0;
    }

    private static bool IsHelpCommand(string command) =>
        command.Equals(
            "help",
            StringComparison.OrdinalIgnoreCase) ||
        command.Equals(
            "--help",
            StringComparison.OrdinalIgnoreCase) ||
        command.Equals(
            "-h",
            StringComparison.OrdinalIgnoreCase);

    private static void WriteHelp()
    {
        Console.WriteLine(
            """
            Mycelium - extensible web crawler

            Usage:
              mycelium <command> [options]

            Commands:
              fetch <url>     Fetch one URL and run its content through the plugin pipeline
              plugins list    List registered crawl plugins
              help            Show this help

            Crawl commands will be added in the next development slices.
            """);
    }
}