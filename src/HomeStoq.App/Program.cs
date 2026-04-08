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
using Microsoft.Extensions.Options;
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

// Add future config if exists (user overrides that apply on next restart)
var futureConfigPath = PathHelper.ResolveFutureConfigPath();
if (File.Exists(futureConfigPath))
{
    builder.Configuration.AddIniFile(futureConfigPath, optional: true, reloadOnChange: false);
}

// Register DbContext
builder.Services.AddDbContext<HomeStoq.App.Data.PantryDbContext>(options =>
    options.UseSqlite($"Data Source={PathHelper.ResolveDatabasePath()}"));

// =============================================================================
// AI Provider Configuration - Hybrid Architecture
// =============================================================================
// Vision (OCR/Receipt scanning): Always uses Gemini model chain
// General (Chat/Voice/Shopping): Uses configured provider (Gemini or OpenRouter)

// Read API keys from environment (not config.ini - security best practice)
var geminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
var openRouterApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");

// Validate required keys
if (string.IsNullOrEmpty(geminiApiKey))
{
    throw new InvalidOperationException(
        "GEMINI_API_KEY environment variable is required. " +
        "This is mandatory for receipt scanning (OCR) functionality. " +
        "Get your key at: https://aistudio.google.com/app/apikey");
}

// Read provider configuration from config.ini
var aiProvider = builder.Configuration["AI:Provider"] ?? "Gemini";
var geminiModel = builder.Configuration["AI:GeminiModel"] ?? "gemini-2.5-flash-lite";
var geminiBaseUrl = builder.Configuration["AI:GeminiBaseUrl"];
var openRouterModel = builder.Configuration["AI:OpenRouterModel"] ?? "openrouter/free";
var openRouterBaseUrl = builder.Configuration["AI:OpenRouterBaseUrl"];

// Read vision configuration
var visionModels = builder.Configuration.GetSection("AI:Vision:FallbackModels")
    .Get<string[]>() ?? new[] { "gemini-2.5-flash-lite", "gemini-2.5-flash", "gemini-2.5-pro" };

// Bind configuration options early (needed for VisionFallbackService)
builder.Services.Configure<AIProviderOptions>(options =>
{
    options.Provider = aiProvider;
    options.GeminiModel = geminiModel;
    options.GeminiBaseUrl = geminiBaseUrl;
    options.OpenRouterModel = openRouterModel;
    options.OpenRouterBaseUrl = openRouterBaseUrl;
});

builder.Services.Configure<AIResilienceOptions>(builder.Configuration.GetSection("AI:Resilience"));

// Create logger factory for provider initialization
var loggerFactory = LoggerFactory.Create(b => b.AddConsole());

// =============================================================================
// Create Vision Client (Always Gemini - for receipt scanning)
// =============================================================================
// Read resilience options directly from configuration
var resilienceSection = builder.Configuration.GetSection("AI:Resilience");
var resilienceOptions = new AIResilienceOptions
{
    RetryAttempts = resilienceSection.GetValue<int?>(nameof(AIResilienceOptions.RetryAttempts)) ?? 3,
    RetryBaseDelayMs = resilienceSection.GetValue<int?>(nameof(AIResilienceOptions.RetryBaseDelayMs)) ?? 1000,
    RetryMaxDelayMs = resilienceSection.GetValue<int?>(nameof(AIResilienceOptions.RetryMaxDelayMs)) ?? 10000
};

var visionClient = new VisionFallbackService(
    geminiApiKey,
    visionModels,
    Options.Create(resilienceOptions),
    loggerFactory.CreateLogger<VisionFallbackService>());

// =============================================================================
// Create General Client (Configurable - for chat/voice/shopping)
// =============================================================================
IChatClient generalClient;
if (aiProvider.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase))
{
    if (string.IsNullOrEmpty(openRouterApiKey))
    {
        throw new InvalidOperationException(
            "OPENROUTER_API_KEY environment variable is required when Provider=OpenRouter. " +
            "Both GEMINI_API_KEY and OPENROUTER_API_KEY are required when using OpenRouter. " +
            "Get your OpenRouter key at: https://openrouter.ai/keys");
    }
    
    generalClient = new OpenRouterProviderFactory(
        openRouterApiKey,
        openRouterModel,
        openRouterBaseUrl,
        loggerFactory.CreateLogger<OpenRouterProviderFactory>()).CreateClient();
    
    loggerFactory.CreateLogger<Program>().LogInformation(
        "General AI provider: OpenRouter (Model: {Model})", openRouterModel);
}
else
{
    // Default to Gemini for general operations too
    generalClient = new GeminiProviderFactory(
        geminiApiKey,
        geminiModel,
        geminiBaseUrl,
        loggerFactory.CreateLogger<GeminiProviderFactory>()).CreateClient();
    
    loggerFactory.CreateLogger<Program>().LogInformation(
        "General AI provider: Gemini (Model: {Model})", geminiModel);
}

// =============================================================================
// Register Hybrid AI Client
// =============================================================================
// Vision requests (images/PDFs) -> VisionFallbackService (Gemini model chain)
// General requests (text) -> Configured provider (Gemini or OpenRouter)
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var hybridLogger = sp.GetRequiredService<ILogger<HybridAIClient>>();
    
    return new HybridAIClient(visionClient, generalClient, hybridLogger)
        .AsBuilder()
        .UseFunctionInvocation()
        .Build();
});

// =============================================================================
// Exception Handler for Vision Service
// =============================================================================
builder.Services.AddProblemDetails();

builder.Services.AddScoped<HomeStoq.App.Data.DbInitializer>();
builder.Services.AddSingleton<PromptProvider>();
builder.Services.AddScoped<InventoryRepository>();
builder.Services.AddScoped<AIService>();
builder.Services.AddHttpClient();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

var app = builder.Build();

// Custom exception handler for vision service
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (VisionServiceUnavailableException ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Vision service unavailable - returning user-friendly error");
        
        context.Response.StatusCode = 503;
        context.Response.ContentType = "application/json";
        
        var errorResponse = new
        {
            error = "Receipt scanning temporarily unavailable",
            message = ex.Message,
            retryAfter = 60,
            timestamp = DateTime.UtcNow
        };
        
        await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
    }
});

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
app.MapSettingsEndpoints();

app.Run();
