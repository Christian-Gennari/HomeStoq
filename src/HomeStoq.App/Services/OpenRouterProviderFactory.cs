using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace HomeStoq.App.Services;

/// <summary>
/// Factory for creating OpenRouter provider chat client.
/// Uses Microsoft's MEAI OpenAI client with OpenRouter's API.
/// </summary>
public class OpenRouterProviderFactory : IAIProviderFactory
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;
    private readonly ILogger<OpenRouterProviderFactory> _logger;

    public string ProviderName => "OpenRouter";
    public bool SupportsFunctionCalling => true; // Depends on selected model
    public bool SupportsMultimodal => true; // openrouter/free router selects vision-capable models

    public OpenRouterProviderFactory(
        string apiKey,
        string model,
        string? baseUrl = null,
        ILogger<OpenRouterProviderFactory>? logger = null)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _model = model ?? "openrouter/free";
        _baseUrl = baseUrl ?? "https://openrouter.ai/api/v1";
        _logger = logger ?? LoggerFactory.Create(b => b.AddConsole()).CreateLogger<OpenRouterProviderFactory>();
    }

    public IChatClient CreateClient()
    {
        _logger.LogInformation("Creating OpenRouter chat client for model: {Model}", _model);
        
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(_baseUrl)
        };
        
        var openAIClient = new OpenAIClient(_apiKey, options);
        
        return openAIClient.AsChatClient(_model);
    }
}
