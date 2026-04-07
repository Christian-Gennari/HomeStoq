using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HomeStoq.Shared.DTOs;
using HomeStoq.Shared.Utils;
using HomeStoq.App.Repositories;
using HomeStoq.App.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Google.GenAI;
using HomeStoq.App.Endpoints;
using Microsoft.EntityFrameworkCore;

// Load environment variables from .env if present
DotNetEnv.Env.Load(PathHelper.ResolveEnvFile());

// Apply HostUrl from config.ini before the web host builder reads ASPNETCORE_URLS
// This ensures config.ini takes precedence over any existing environment variable
var configIniPath = PathHelper.ResolveConfigIni();
if (File.Exists(configIniPath))
{
    var hostUrl = System
        .Text.RegularExpressions.Regex.Match(File.ReadAllText(configIniPath), @"^\s*HostUrl\s*=\s*(.+)", System.Text.RegularExpressions.RegexOptions.Multiline)
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

// Register DbContext
builder.Services.AddDbContext<HomeStoq.App.Data.PantryDbContext>(options =>
    options.UseSqlite($"Data Source={PathHelper.ResolveDatabasePath()}"));

// Register AI Client
var apiKey = builder.Configuration["GEMINI_API_KEY"] ?? throw new InvalidOperationException("GEMINI_API_KEY not configured");
var modelId = builder.Configuration["AI:Model"] ?? "gemini-3.1-flash-lite-preview";
var googleClient = new Client(apiKey: apiKey);
builder.Services.AddSingleton<IChatClient>(sp =>
    googleClient.AsIChatClient(modelId)
        .AsBuilder()
        .UseFunctionInvocation()
        .Build());

builder.Services.AddScoped<HomeStoq.App.Data.DbInitializer>();
builder.Services.AddSingleton<PromptProvider>();
builder.Services.AddScoped<InventoryRepository>();
builder.Services.AddScoped<GeminiService>();
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
app.MapShoppingListEndpoints();

app.Run();