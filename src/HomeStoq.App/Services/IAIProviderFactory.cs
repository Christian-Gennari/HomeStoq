using Microsoft.Extensions.AI;

namespace HomeStoq.App.Services;

/// <summary>
/// Factory interface for creating AI provider chat clients.
/// Enables provider-independent AI service architecture.
/// </summary>
public interface IAIProviderFactory
{
    /// <summary>
    /// Creates an IChatClient configured for the specific provider.
    /// </summary>
    IChatClient CreateClient();
    
    /// <summary>
    /// Provider name (Gemini or OpenRouter)
    /// </summary>
    string ProviderName { get; }
    
    /// <summary>
    /// Whether this provider supports function calling (tools)
    /// </summary>
    bool SupportsFunctionCalling { get; }
    
    /// <summary>
    /// Whether this provider supports multimodal inputs (images, audio)
    /// </summary>
    bool SupportsMultimodal { get; }
}
