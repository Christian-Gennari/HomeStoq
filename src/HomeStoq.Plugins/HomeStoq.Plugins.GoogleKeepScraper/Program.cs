using System;
using System.Net.Http;
using HomeStoq.Contracts.SharedUtils;
using HomeStoq.Plugins.GoogleKeepScraper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using HomeStoq.Plugins.GoogleKeepScraper.Configuration;
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

builder.Services.Configure<VoiceOptions>(builder.Configuration.GetSection("Voice"));
builder.Services.Configure<ScraperOptions>(builder.Configuration.GetSection("Scraper"));

builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(30) });
builder.Services.AddSingleton<IBrowserService, PlaywrightBrowserService>();
builder.Services.AddTransient<IKeepListProcessor, KeepListProcessor>();
builder.Services.AddHostedService<GoogleKeepScraperWorker>();

var host = builder.Build();
await host.RunAsync();
