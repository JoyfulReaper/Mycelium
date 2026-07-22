using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Mycelium.Core.Fetching;
using Mycelium.Core.Plugins;
using System.Net;

namespace Mycelium.Core;

public static class MyceliumServiceCollectionExtensions
{
    public static IServiceCollection AddMyceliumCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<HttpFetchOptions>()
            .Bind(
                configuration.GetSection(
                    HttpFetchOptions.SectionName))
            .Validate(
                options => options.TimeoutSeconds > 0,
                "HttpFetch:TimeoutSeconds must be positive.")
            .Validate(
                options => options.MaxResponseBytes > 0,
                "HttpFetch:MaxResponseBytes must be positive.")
            .Validate(
                options => options.MaxRedirects >= 0,
                "HttpFetch:MaxRedirects cannot be negative.")
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(
                        options.UserAgent),
                "HttpFetch:UserAgent must not be empty.")
            .ValidateOnStart();

        services
            .AddHttpClient<IResourceFetcher, HttpResourceFetcher>(
                (serviceProvider, client) =>
                {
                    HttpFetchOptions options =
                        serviceProvider
                            .GetRequiredService<
                                IOptions<HttpFetchOptions>>()
                            .Value;

                    client.Timeout =
                        Timeout.InfiniteTimeSpan;

                    client.DefaultRequestHeaders
                        .UserAgent
                        .ParseAdd(options.UserAgent);
                })
            .ConfigurePrimaryHttpMessageHandler(
                () => new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    AutomaticDecompression =
                        DecompressionMethods.All,
                    PooledConnectionLifetime =
                        TimeSpan.FromMinutes(10)
                });

        services.TryAddTransient<
            ICrawlPluginPipeline,
            CrawlPluginPipeline>();

        return services;
    }
}