using HomeStoq.Contracts.SharedUtils;
using HomeStoq.Plugins.GoogleKeepScraper;

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

builder.Services.AddHostedService<GoogleKeepScraperWorker>();

var host = builder.Build();
await host.RunAsync();
