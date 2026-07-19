namespace Mycelium.Contracts.Plugins;

public sealed record DiscoveredUrl(
    Uri Uri,
    string Relationship = "link",
    string? Context = null);