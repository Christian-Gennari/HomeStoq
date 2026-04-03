using System;
using System.Net.Http;
using HomeStoq.Shared.Utils;
using HomeStoq.Plugins.GoogleKeepScraper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using HomeStoq.Plugins.GoogleKeepScraper.Services;

// Load environment variables from .env if present
DotNetEnv.Env.Load(PathHelper.ResolveEnvFile());

var builder = Host.CreateApplicationBuilder(args);

// Add config.ini as a configuration source
builder.Configuration.AddIniFile(
    PathHelper.ResolveConfigIni(),
    optional: true,
    reloadOnChange: true
);

builder.Configuration.AddJsonFile("appsettings.json", optional: true);
builder.Configuration.AddEnvironmentVariables();

// Determine browser mode: "RemoteDebugging" (default, secure) or "Playwright" (fallback)
var browserMode = builder.Configuration["GoogleKeepScraper:BrowserMode"]?.ToLowerInvariant() ?? "remotedebugging";

builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(30) });

if (browserMode == "playwright")
{
    builder.Services.AddSingleton<IBrowserService, PlaywrightBrowserService>();
    Console.WriteLine("[INFO] Using Playwright browser mode (fallback)");
}
else
{
    builder.Services.AddSingleton<IBrowserService, CdpBrowserService>();
    Console.WriteLine("[INFO] Using Remote Debugging browser mode (default, secure)");
    Console.WriteLine("[INFO] Chrome will be launched automatically with remote debugging enabled");
}

builder.Services.AddTransient<IKeepListProcessor, KeepListProcessor>();
builder.Services.AddHostedService<GoogleKeepScraperWorker>();

var host = builder.Build();
await host.RunAsync();
