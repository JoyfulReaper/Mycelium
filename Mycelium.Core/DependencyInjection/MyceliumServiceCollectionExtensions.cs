using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mycelium.Core.Plugins;

namespace Mycelium.Core;

public static class MyceliumServiceCollectionExtensions
{
    public static IServiceCollection AddMyceliumCore(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddTransient<
            ICrawlPluginPipeline,
            CrawlPluginPipeline>();

        return services;
    }
}