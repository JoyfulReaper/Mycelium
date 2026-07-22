namespace Mycelium.Contracts.Crawling;

public sealed record ResourceStatus(
    string Code,
    string? Description = null);