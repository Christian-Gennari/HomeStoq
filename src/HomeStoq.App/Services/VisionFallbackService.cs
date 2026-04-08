using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MeaiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using Google.GenAI;
using HomeStoq.App.Configuration;

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
/// Uses native Google.GenAI SDK for proper multimodal/vision support.
/// Always uses Gemini regardless of general provider configuration.
/// Implements smart retry with exponential backoff across model chain.
/// </summary>
public class VisionFallbackService : IChatClient
{
    private readonly string _apiKey;
    private readonly List<string> _modelChain;
    private readonly int _maxAttemptsPerModel;
    private readonly int _baseDelayMs;
    private readonly int _maxDelayMs;
    private readonly ILogger<VisionFallbackService> _logger;

    public VisionFallbackService(
        string apiKey,
        IEnumerable<string> models,
        IOptions<AIResilienceOptions> resilienceOptions,
        ILogger<VisionFallbackService> logger)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _modelChain = models?.ToList() ?? new List<string> { "gemini-2.5-flash-lite" };
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_modelChain.Count == 0)
            throw new ArgumentException("At least one vision model must be specified", nameof(models));

        // Use configuration values with sensible defaults
        var options = resilienceOptions?.Value ?? new AIResilienceOptions();
        _maxAttemptsPerModel = Math.Max(1, options.RetryAttempts);
        _baseDelayMs = Math.Max(100, options.RetryBaseDelayMs);
        _maxDelayMs = Math.Max(_baseDelayMs, options.RetryMaxDelayMs);
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<MeaiChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var failures = new List<Exception>();

        foreach (var (model, modelIndex) in _modelChain.Select((m, i) => (m, i)))
        {
            // Try each model up to configured attempts
            for (int attempt = 1; attempt <= _maxAttemptsPerModel; attempt++)
            {
                try
                {
                    _logger.LogInformation(
                        "Vision request: Trying model {Model} (attempt {Attempt}/{MaxAttempts}, model {ModelNum}/{TotalModels})",
                        model, attempt, _maxAttemptsPerModel, modelIndex + 1, _modelChain.Count);

                    var client = CreateClient(model);
                    var response = await client.GetResponseAsync(messages, options, cancellationToken);

                    _logger.LogInformation(
                        "Vision request succeeded with model {Model} (attempt {Attempt})",
                        model, attempt);
                    
                    return response;
                }
                catch (Exception ex) when (IsNonRecoverableError(ex))
                {
                    // Non-recoverable errors should not be retried
                    _logger.LogError(ex, 
                        "Vision model {Model} encountered non-recoverable error (attempt {Attempt}). Aborting retries.",
                        model, attempt);
                    throw;
                }
                catch (Exception ex) when (IsProviderError(ex))
                {
                    var isLastAttempt = (attempt == _maxAttemptsPerModel) && (modelIndex == _modelChain.Count - 1);
                    
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
        IEnumerable<MeaiChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Vision OCR doesn't support streaming - return non-streaming response wrapped
        _logger.LogDebug("Streaming not supported for vision requests, using standard response");
        
        async IAsyncEnumerable<ChatResponseUpdate> StreamResponse()
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            if (!string.IsNullOrEmpty(response.Text))
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
            }
        }
        
        return StreamResponse();
    }

    public object? GetService(Type serviceType, object? key = null)
    {
        // No additional services provided by this client
        return null;
    }

    public void Dispose()
    {
        // No disposable resources held by this service
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Determines if an error is non-recoverable and should not be retried.
    /// These are programming errors, validation failures, or malformed responses.
    /// </summary>
    private bool IsNonRecoverableError(Exception ex)
    {
        // Programming errors - retrying won't help
        if (ex is NullReferenceException ||
            ex is ArgumentException ||
            ex is ArgumentNullException ||
            ex is InvalidOperationException ||
            ex is NotSupportedException)
        {
            return true;
        }

        // JSON parsing errors indicate malformed response - retrying same request won't help
        if (ex is System.Text.Json.JsonException)
        {
            return true;
        }

        // Check for specific error messages that indicate non-recoverable errors
        var message = ex.Message?.ToLowerInvariant() ?? "";
        if (message.Contains("invalid") ||
            message.Contains("malformed") ||
            message.Contains("parse error") ||
            message.Contains("serialization"))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if an error is a provider/transient error that should be retried.
    /// </summary>
    private bool IsProviderError(Exception ex)
    {
        // Network and timeout errors are recoverable
        if (ex is HttpRequestException ||
            ex is TimeoutException ||
            ex is TaskCanceledException ||
            ex is IOException)
        {
            return true;
        }

        // Check for API-specific error indicators
        var message = ex.Message ?? "";
        if (message.Contains("API") ||
            message.Contains("request") ||
            message.Contains("500") ||
            message.Contains("429") ||
            message.Contains("503") ||
            message.Contains("502") ||
            message.Contains("504") ||
            message.Contains("Bad Request") ||
            message.Contains("rate limit") ||
            message.Contains("quota"))
        {
            return true;
        }

        return false;
    }

    private int CalculateDelay(int modelIndex, int attempt)
    {
        // Exponential backoff using configured base delay
        var baseDelay = _baseDelayMs * Math.Pow(2, modelIndex * 2 + attempt - 1);
        var cappedDelay = Math.Min(baseDelay, _maxDelayMs);
        var jitter = Random.Shared.Next(100, 500);
        return (int)cappedDelay + jitter;
    }

    /// <summary>
    /// Creates a native Google.GenAI client for proper multimodal/vision support.
    /// This uses the native SDK instead of OpenAI-compatible endpoint which doesn't
    /// support vision properly.
    /// </summary>
    private IChatClient CreateClient(string model)
    {
        // Use native Google.GenAI SDK for proper vision/multimodal support
        var googleClient = new Google.GenAI.Client(apiKey: _apiKey);
        return googleClient.AsIChatClient(model);
    }
}
