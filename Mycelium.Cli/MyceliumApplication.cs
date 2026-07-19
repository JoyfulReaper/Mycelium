using Microsoft.Extensions.Logging;
using Mycelium.Contracts.Plugins;

namespace Mycelium.Cli;

public sealed class MyceliumApplication(
    IEnumerable<ICrawlPlugin> plugins,
    ILogger<MyceliumApplication> logger)
{
    private readonly IReadOnlyList<ICrawlPlugin> _plugins =
        plugins.ToArray();

    public Task<int> RunAsync(
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
            return Task.FromResult(0);
        }

        if (string.Equals(
                args[0],
                "plugins",
                StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(RunPluginsCommand(args));
        }

        logger.LogWarning(
            "Unknown command {Command}.",
            args[0]);

        Console.Error.WriteLine(
            $"Unknown command: {args[0]}");
        Console.Error.WriteLine();

        WriteHelp();

        return Task.FromResult(2);
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
              plugins list    List registered crawl plugins
              help            Show this help

            Crawl commands will be added in the next development slices.
            """);
    }
}