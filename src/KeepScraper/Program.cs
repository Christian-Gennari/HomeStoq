using HomeStoq.KeepScraper;

// Load .env file
DotNetEnv.Env.Load();

var builder = Host.CreateApplicationBuilder(args);

// Add config.ini as a configuration source
builder.Configuration.AddIniFile("config.ini", optional: true, reloadOnChange: true);

builder.Configuration.AddJsonFile("appsettings.json", optional: true);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddHostedService<KeepScraperWorker>();

var host = builder.Build();
await host.RunAsync();
