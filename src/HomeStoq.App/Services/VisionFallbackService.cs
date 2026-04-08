using System.ClientModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace HomeStoq.App.Services;

/// <summary>
/// Custom exception for vision service unavailability.
/// Provides user-friendly error message when all vision models fail.
/// </summary>
public class VisionServiceUnavailableException : Exception
{
    public VisionServiceUnavailableException(string message, Exception inner)
        : base(message, inner) { }
}

/// <summary>
/// Vision-specific AI client with Gemini model fallback chain.
/// Always uses Gemini regardless of general provider configuration.
/// Implements smart retry with exponential backoff across model chain.
/// </summary>
public class VisionFallbackService : IChatClient
{
    private readonly string _apiKey;
    private readonly List<string> _modelChain;
    private readonly ILogger<VisionFallbackService> _logger;

    public VisionFallbackService(
        string apiKey,
        IEnumerable<string> models,
        ILogger<VisionFallbackService> logger)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _modelChain = models?.ToList() ?? new List<string> { "gemini-2.5-flash-lite" };
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_modelChain.Count == 0)
            throw new ArgumentException("At least one vision model must be specified", nameof(models));
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var failures = new List<Exception>();

        foreach (var (model, modelIndex) in _modelChain.Select((m, i) => (m, i)))
        {
            // Try each model up to 2 times
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    _logger.LogInformation(
                        "Vision request: Trying model {Model} (attempt {Attempt}/2, model {ModelNum}/{TotalModels})",
                        model, attempt, modelIndex + 1, _modelChain.Count);

                    var client = CreateClient(model);
                    var response = await client.GetResponseAsync(messages, options, cancellationToken);

                    _logger.LogInformation(
                        "Vision request succeeded with model {Model} (attempt {Attempt})",
                        model, attempt);
                    
                    return response;
                }
                catch (Exception ex) when (IsProviderError(ex))
                {
                    var isLastAttempt = (attempt == 2) && (modelIndex == _modelChain.Count - 1);
                    
                    if (isLastAttempt)
                    {
                        _logger.LogError(ex,
                            "Vision model {Model} failed on final attempt. All models exhausted.",
                            model);
                    }
                    else
                    {
                        _logger.LogWarning(ex,
                            "Vision model {Model} failed (attempt {Attempt}): {Error}. Will try fallback...",
                            model, attempt, ex.Message);
                    }

                    // Add delay before retry (except for last attempt)
                    if (!isLastAttempt)
                    {
                        var delay = CalculateDelay(modelIndex, attempt);
                        _logger.LogDebug("Waiting {Delay}ms before next attempt...", delay);
                        await Task.Delay(delay, cancellationToken);
                    }

                    failures.Add(ex);
                }
            }
        }

        // All models exhausted - throw user-friendly exception
        _logger.LogError("All vision models failed. Attempted: {Models}", 
            string.Join(", ", _modelChain));

        throw new VisionServiceUnavailableException(
            "Receipt scanning temporarily unavailable. Please try again in 1 minute.",
            new AggregateException(failures));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Vision OCR typically doesn't need streaming, but implement for completeness
        return GetResponseAsync(messages, options, cancellationToken)
            .ToAsyncEnumerable()
            .SelectMany(r => r.ToChatResponseUpdates());
    }

    private bool IsProviderError(Exception ex) => ex switch
    {
        ClientResultException => true,           // OpenAI/MEAI API errors (400, 429, 500, etc.)
        HttpRequestException => true,            // Network errors
        TimeoutException => true,                // Timeouts
        TaskCanceledException => true,          // Cancellation (often timeout-related)
        IOException => true,                     // I/O errors
        _ => false
    };

    private int CalculateDelay(int modelIndex, int attempt)
    {
        // Exponential backoff: base 1s, doubles per attempt, with jitter
        // Model 0, Attempt 1: ~1.1s
        // Model 0, Attempt 2: ~2.3s
        // Model 1, Attempt 1: ~4.1s
        // Model 1, Attempt 2: ~8.2s
        // etc.
        var baseDelay = 1000 * Math.Pow(2, modelIndex * 2 + attempt - 1);
        var cappedDelay = Math.Min(baseDelay, 10000);  // Cap at 10s
        var jitter = Random.Shared.Next(100, 500);
        return (int)cappedDelay + jitter;
    }

    private IChatClient CreateClient(string model)
    {
        var credential = new ApiKeyCredential(_apiKey);
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/")
        };
        
        return new ChatClient(model, credential, options).AsIChatClient();
    }
}

// Extension method for IAsyncEnumerable
file static class AsyncEnumerableExtensions
{
    public static IAsyncEnumerable<T> SelectMany<T>(this IAsyncEnumerable<IEnumerable<T>> source)
    {
        return source.SelectMany(x => x.ToAsyncEnumerable());
    }

    private static IAsyncEnumerable<T> SelectMany<T>(this IAsyncEnumerable<IEnumerable<T>> source, Func<IEnumerable<T>, IAsyncEnumerable<T>> selector)
    {
        return source.SelectMany(selector);
    }
}
