using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Mycelium.Cli;
using Mycelium.Contracts.Plugins;
using Mycelium.Core;
using Mycelium.Plugins.HtmlLinks;

HostApplicationBuilder builder =
    Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();

builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat =
        "yyyy-MM-dd HH:mm:ss.fff 'UTC' ";

    options.UseUtcTimestamp = true;
    options.ColorBehavior =
        LoggerColorBehavior.Disabled;
});

builder.Services.AddMyceliumCore(builder.Configuration);
builder.Services.AddSingleton<ICrawlPlugin, HtmlLinkDiscoveryPlugin>();
builder.Services.AddSingleton<MyceliumApplication>();

using IHost host = builder.Build();

await host.StartAsync();

try
{
    MyceliumApplication application =
        host.Services.GetRequiredService<
            MyceliumApplication>();

    IHostApplicationLifetime lifetime =
        host.Services.GetRequiredService<
            IHostApplicationLifetime>();

    return await application.RunAsync(
        args,
        lifetime.ApplicationStopping);
}
finally
{
    await host.StopAsync();
}