namespace HomeStoq.App.Configuration;

/// <summary>
/// Configuration options for AI provider selection and settings.
/// Maps to [AI] section in config.ini.
/// </summary>
public class AIProviderOptions
{
    /// <summary>
    /// Provider selection: "Gemini" or "OpenRouter"
    /// </summary>
    public string Provider { get; set; } = "Gemini";

    #region Gemini Settings
    
    /// <summary>
    /// Gemini model name (e.g., gemini-2.5-flash-lite)
    /// </summary>
    public string GeminiModel { get; set; } = "gemini-2.5-flash-lite";
    
    /// <summary>
    /// Optional: Override Gemini base URL (defaults to OpenAI-compatible endpoint)
    /// </summary>
    public string? GeminiBaseUrl { get; set; }
    
    #endregion

    #region OpenRouter Settings
    
    /// <summary>
    /// OpenRouter model name (e.g., openrouter/free or specific model path)
    /// </summary>
    public string OpenRouterModel { get; set; } = "openrouter/free";
    
    /// <summary>
    /// Optional: Override OpenRouter base URL
    /// </summary>
    public string? OpenRouterBaseUrl { get; set; }
    
    #endregion
}

/// <summary>
/// Configuration options for AI resilience and fallback behavior.
/// Maps to [AI.Resilience] section in config.ini.
/// </summary>
public class AIResilienceOptions
{
    /// <summary>
    /// Enable automatic retry on failure
    /// </summary>
    public bool EnableRetry { get; set; } = true;
    
    /// <summary>
    /// Number of retry attempts before failing
    /// </summary>
    public int RetryAttempts { get; set; } = 3;
    
    /// <summary>
    /// Base delay in milliseconds between retries (exponential backoff)
    /// </summary>
    public int RetryBaseDelayMs { get; set; } = 1000;
    
    /// <summary>
    /// Backoff multiplier for exponential delay
    /// </summary>
    public int RetryBackoffMultiplier { get; set; } = 2;
    
    /// <summary>
    /// Maximum delay in milliseconds between retries
    /// </summary>
    public int RetryMaxDelayMs { get; set; } = 8000;
    
    /// <summary>
    /// Add jitter to retry delays to prevent thundering herd
    /// </summary>
    public bool EnableJitter { get; set; } = true;
    
    /// <summary>
    /// Enable automatic fallback to other provider on failure
    /// </summary>
    public bool EnableCrossProviderFallback { get; set; } = true;
    
    /// <summary>
    /// Enable circuit breaker pattern for failing providers
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;
    
    /// <summary>
    /// Number of consecutive failures before opening circuit
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    
    /// <summary>
    /// Seconds to wait before attempting to close circuit
    /// </summary>
    public int CircuitBreakerRecoveryTimeout { get; set; } = 60;
}
