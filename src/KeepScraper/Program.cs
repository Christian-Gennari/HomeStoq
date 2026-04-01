using HomeStoq.KeepScraper;

// Load environment variables from .env if present
DotNetEnv.Env.Load(Path.Combine(Directory.GetCurrentDirectory(), ".env"));
if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), ".env")))
{
    DotNetEnv.Env.Load(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env"));
}

var builder = Host.CreateApplicationBuilder(args);

// Add config.ini as a configuration source (searching up to project root)
var configIniPath = Path.Combine(Directory.GetCurrentDirectory(), "config.ini");
if (!File.Exists(configIniPath))
{
    configIniPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "config.ini");
}
builder.Configuration.AddIniFile(configIniPath, optional: true, reloadOnChange: true);

builder.Configuration.AddJsonFile("appsettings.json", optional: true);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddHostedService<KeepScraperWorker>();

var host = builder.Build();
await host.RunAsync();
