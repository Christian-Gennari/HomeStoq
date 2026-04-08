using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace HomeStoq.App.Services;

/// <summary>
/// Factory for creating Gemini provider chat client using OpenAI-compatible endpoint.
/// Uses Microsoft's MEAI OpenAI client with Gemini's OpenAI-compatible API.
/// </summary>
public class GeminiProviderFactory : IAIProviderFactory
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;
    private readonly ILogger<GeminiProviderFactory> _logger;

    public string ProviderName => "Gemini";
    public bool SupportsFunctionCalling => true;
    public bool SupportsMultimodal => true;

    public GeminiProviderFactory(
        string apiKey,
        string model,
        string? baseUrl = null,
        ILogger<GeminiProviderFactory>? logger = null)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _model = model ?? "gemini-2.5-flash-lite";
        // Gemini OpenAI-compatible endpoint
        _baseUrl = baseUrl ?? "https://generativelanguage.googleapis.com/v1beta/openai/";
        _logger = logger ?? NullLogger<GeminiProviderFactory>.Instance;
    }

    public IChatClient CreateClient()
    {
        _logger.LogInformation("Creating Gemini chat client for model: {Model}", _model);
        
        // Microsoft.Extensions.AI.OpenAI 10.3.0 API
        // Use ChatClient directly with ApiKeyCredential
        var credential = new ApiKeyCredential(_apiKey);
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(_baseUrl)
        };
        
        var chatClient = new ChatClient(_model, credential, options);
        return chatClient.AsIChatClient();
    }
}
