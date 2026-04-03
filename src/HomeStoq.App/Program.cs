using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HomeStoq.Contracts;
using HomeStoq.Contracts.SharedUtils;
using HomeStoq.App.Repositories;
using HomeStoq.App.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Google.GenAI;
using Microsoft.Extensions.Options;
using HomeStoq.App.Configuration;
using HomeStoq.App.Endpoints;

// Load environment variables from .env if present
DotNetEnv.Env.Load(PathHelper.ResolveEnvFile());

// Apply HostUrl from config.ini before the web host builder reads ASPNETCORE_URLS
// This ensures config.ini takes precedence over any existing environment variable
var configIniPath = PathHelper.ResolveConfigIni();
if (File.Exists(configIniPath))
{
    var hostUrl = System
        .Text.RegularExpressions.Regex.Match(File.ReadAllText(configIniPath), @"HostUrl\s*=\s*(.+)")
        .Groups[1]
        .Value.Trim();
    if (!string.IsNullOrEmpty(hostUrl))
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", hostUrl);
    }
}

var builder = WebApplication.CreateBuilder(args);

// Add config.ini as a configuration source
builder.Configuration.AddIniFile(configIniPath, optional: true, reloadOnChange: true);

builder.Services.Configure<AppOptions>(builder.Configuration.GetSection("App"));
builder.Services.Configure<GeminiOptions>(options => {
    options.ApiKey = builder.Configuration["GEMINI_API_KEY"] ?? string.Empty;
    options.Model = builder.Configuration["AI:Model"] ?? "gemini-3.1-flash-lite-preview";
});

// Register AI Client
var apiKey = builder.Configuration["GEMINI_API_KEY"] ?? throw new InvalidOperationException("GEMINI_API_KEY not configured");
var modelId = builder.Configuration["AI:Model"] ?? "gemini-3.1-flash-lite-preview";
var googleClient = new Client(apiKey: apiKey);
builder.Services.AddSingleton<IChatClient>(sp =>
    googleClient.AsIChatClient(modelId)
        .AsBuilder()
        .UseFunctionInvocation()
        .Build());

builder.Services.AddSingleton<HomeStoq.App.Data.DbInitializer>();
builder.Services.AddSingleton<PromptProvider>();
builder.Services.AddSingleton<InventoryRepository>();
builder.Services.AddSingleton<GeminiService>();
builder.Services.AddHttpClient();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

var app = builder.Build();

// Eagerly initialize the database on startup
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<HomeStoq.App.Data.DbInitializer>().InitializeDatabase();
}

// Static files
app.UseDefaultFiles();
app.UseStaticFiles();

// Endpoints
app.MapInventoryEndpoints();
app.MapReceiptEndpoints();
app.MapAiEndpoints();

app.Run();