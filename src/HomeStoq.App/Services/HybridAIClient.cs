using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MeaiChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace HomeStoq.App.Services;

/// <summary>
/// Hybrid AI client that routes requests based on content type:
/// - Vision requests (images/PDFs): Always use Gemini model chain
/// - General requests (text): Use configured provider (Gemini or OpenRouter)
/// </summary>
public class HybridAIClient : IChatClient
{
    private readonly IChatClient _visionClient;      // Always Gemini model chain
    private readonly IChatClient _generalClient;     // Configurable provider
    private readonly ILogger<HybridAIClient> _logger;

    public HybridAIClient(
        IChatClient visionClient,
        IChatClient generalClient,
        ILogger<HybridAIClient> logger)
    {
        _visionClient = visionClient ?? throw new ArgumentNullException(nameof(visionClient));
        _generalClient = generalClient ?? throw new ArgumentNullException(nameof(generalClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<MeaiChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        bool isVisionRequest = AIContentInspector.ContainsVisionContent(messages);

        if (isVisionRequest)
        {
            _logger.LogInformation(
                "Routing request to VISION path (receipt scanning) - {MessageCount} messages",
                messages.Count());
            
            try
            {
                return await _visionClient.GetResponseAsync(messages, options, cancellationToken);
            }
            catch (VisionServiceUnavailableException)
            {
                // Re-throw with the user-friendly message
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vision path failed unexpectedly");
                throw;
            }
        }
        else
        {
            _logger.LogDebug(
                "Routing request to GENERAL path (configured provider) - {MessageCount} messages",
                messages.Count());
            
            return await _generalClient.GetResponseAsync(messages, options, cancellationToken);
        }
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<MeaiChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        bool isVisionRequest = AIContentInspector.ContainsVisionContent(messages);
        var client = isVisionRequest ? _visionClient : _generalClient;
        
        if (isVisionRequest)
        {
            _logger.LogInformation("Streaming request routed to VISION path");
        }
        else
        {
            _logger.LogDebug("Streaming request routed to GENERAL path");
        }

        return client.GetStreamingResponseAsync(messages, options, cancellationToken);
    }

    public object? GetService(Type serviceType, object? key = null)
    {
        // Delegate to general client for service resolution
        return _generalClient.GetService(serviceType, key);
    }

    public void Dispose()
    {
        // Dispose both clients
        _visionClient.Dispose();
        _generalClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
