using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HomeStoq.Shared.DTOs;
using HomeStoq.Shared.Utils;
using HomeStoq.App.Repositories;
using HomeStoq.App.Services;
using HomeStoq.App.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
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
        .Text.RegularExpressions.Regex.Match(File.ReadAllText(configIniPath), @"^\s*HostUrl\s*=\s*([^\s#]+)", System.Text.RegularExpressions.RegexOptions.Multiline)
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

// Register AI Provider and Client
// Read API keys from environment (not config.ini - security best practice)
var geminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
var openRouterApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");

// Read provider configuration from config.ini
var aiProvider = builder.Configuration["AI:Provider"] ?? "Gemini";
var geminiModel = builder.Configuration["AI:GeminiModel"] ?? "gemini-2.5-flash-lite";
var geminiBaseUrl = builder.Configuration["AI:GeminiBaseUrl"];
var openRouterModel = builder.Configuration["AI:OpenRouterModel"] ?? "openrouter/free";
var openRouterBaseUrl = builder.Configuration["AI:OpenRouterBaseUrl"];

// Create appropriate provider factory
IAIProviderFactory providerFactory;
if (aiProvider.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase))
{
    if (string.IsNullOrEmpty(openRouterApiKey))
    {
        throw new InvalidOperationException("OPENROUTER_API_KEY environment variable is required when Provider=OpenRouter");
    }
    
    providerFactory = new OpenRouterProviderFactory(
        openRouterApiKey,
        openRouterModel,
        openRouterBaseUrl,
        logger: null); // Logger will be created internally
}
else
{
    // Default to Gemini
    if (string.IsNullOrEmpty(geminiApiKey))
    {
        throw new InvalidOperationException("GEMINI_API_KEY environment variable is required when Provider=Gemini (or by default)");
    }
    
    providerFactory = new GeminiProviderFactory(
        geminiApiKey,
        geminiModel,
        geminiBaseUrl,
        logger: null); // Logger will be created internally
}

// Register the provider factory as singleton
builder.Services.AddSingleton(providerFactory);

// Register IChatClient using the factory
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var factory = sp.GetRequiredService<IAIProviderFactory>();
    return factory.CreateClient()
        .AsBuilder()
        .UseFunctionInvocation()
        .Build();
});

// Bind configuration options
builder.Services.Configure<AIProviderOptions>(options =>
{
    options.Provider = aiProvider;
    options.GeminiModel = geminiModel;
    options.GeminiBaseUrl = geminiBaseUrl;
    options.OpenRouterModel = openRouterModel;
    options.OpenRouterBaseUrl = openRouterBaseUrl;
});

builder.Services.Configure<AIResilienceOptions>(builder.Configuration.GetSection("AI:Resilience"));

builder.Services.AddScoped<HomeStoq.App.Data.DbInitializer>();
builder.Services.AddSingleton<PromptProvider>();
builder.Services.AddScoped<InventoryRepository>();
builder.Services.AddScoped<AIService>(); // Renamed from GeminiService
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
