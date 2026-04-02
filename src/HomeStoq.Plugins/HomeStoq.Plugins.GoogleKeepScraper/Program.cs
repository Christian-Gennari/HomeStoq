using HomeStoq.Plugins.GoogleKeepScraper;

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
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (current != null)
    {
        var candidate = Path.Combine(current.FullName, "config.ini");
        if (File.Exists(candidate))
        {
            configIniPath = candidate;
            break;
        }

        current = current.Parent;
    }
}
builder.Configuration.AddIniFile(configIniPath, optional: true, reloadOnChange: true);

builder.Configuration.AddJsonFile("appsettings.json", optional: true);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddHostedService<GoogleKeepScraperWorker>();

var host = builder.Build();
await host.RunAsync();
