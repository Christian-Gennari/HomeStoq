# AI Resilience & OpenRouter Integration - Branch Plan

**Branch:** `feature/ai-openrouter-integration`
**Created:** 2025-04-08
**Goal:** Implement full interchangeability between Gemini and OpenRouter providers with automatic cross-provider fallback for maximum resilience.

---

## Current State: Gemini Free Tier Analysis

### ✅ Gemini Free Tier is Already Optimal

Per [Gemini API pricing docs](https://ai.google.dev/gemini-api/docs/pricing) (April 2025):

| Model | Input | Output | Multimodal | Status |
|-------|-------|--------|------------|--------|
| **gemini-2.5-flash-lite** | ✅ Free | ✅ Free | ✅ Yes | **CURRENT - Optimal** |
| gemini-2.5-pro | ✅ Free | ✅ Free | ✅ Yes | Available |
| gemini-2.5-flash | ✅ Free | ✅ Free | ✅ Yes | Available |
| gemini-3.x series | ✅ Free | ✅ Free | ✅ Yes | Preview |

**Key Findings:**
- We're already on the best free tier option
- Gemini 2.5-flash-lite: $0 cost, full multimodal, 1M token context, 500-1500 RPD limits
- No cost savings from switching - OpenRouter evaluation is for **resilience/redundancy only**

### ⚠️ Deprecated Models (Migrate by June 1, 2026)
- `gemini-2.0-flash` → Use `gemini-2.5-flash`
- `gemini-2.0-flash-lite` → Use `gemini-2.5-flash-lite`

---

## Phase 1: Test Harness for Interchangeability Validation (Issue #14)

**Objective:** Verify both providers work identically for all AI use cases.

### Test Matrix
| # | Scenario | Input | Gemini 2.5-flash-lite | OpenRouter (openrouter/free) |
|---|----------|-------|------------------------|------------------------------|
| 1 | Voice Command Parsing | Swedish: "slut på mjölk" | ✅ Baseline | ⬜ Must match |
| 2 | Receipt OCR (Vision) | JPEG receipt image | ✅ Native multimodal | ⬜ Router must select vision model |
| 3 | Chat with Tools | "How much milk do I have?" | ✅ Function calling | ⬜ Must support tool calls |
| 4 | Shopping List Generation | History + inventory JSON | ✅ 99%+ JSON reliability | ⬜ Must match |
| 5 | JSON Reliability | 100 prompts, measure valid % | ⬜ Measure | ⬜ Measure |
| 6 | Swedish Quality | 20 voice commands | ⬜ Score | ⬜ Score |
| 7 | Latency | Average response time | ⬜ p50/p95/p99 | ⬜ p50/p95/p99 |
| 8 | Rate Limit Handling | Burst requests | ⬜ 500-1500 RPD | ⬜ 20 RPM / 200/day |

### Interchangeability Criteria
For OpenRouter to be viable as swap-in replacement:
- [ ] Function calling works identically (tools: GetStockLevel, GetFullInventory, etc.)
- [ ] Vision/multimodal works (receipt scanning)
- [ ] JSON reliability ≥95% (compare to Gemini baseline)
- [ ] Swedish comprehension ≥90% of Gemini quality
- [ ] Latency within 2x of Gemini

### Deliverables
- [ ] `/tests/AIEvaluation/` test harness project (tests both providers)
- [ ] `phase1-results.md` with comparison matrix
- [ ] Go/No-Go decision for OpenRouter as fallback option

---

## Phase 2: Dual-Provider Architecture

**Objective:** Full interchangeability - change `Provider=` in config.ini, everything else works identically.

### Code Changes

| File | Change |
|------|--------|
| `HomeStoq.App.csproj` | Add `Microsoft.Extensions.AI.OpenAI` (keep `Google.GenAI` for now) |
| `Program.cs` | Replace direct Gemini initialization with `AIProviderFactory` |
| `config.ini` | New `[AI]` and `[AI.Resilience]` sections |
| `GeminiService.cs` | Rename to `AIService.cs` (keeps `IChatClient` abstraction) |
| New: `AIProviderFactory.cs` | Factory creates correct `IChatClient` based on config |
| New: `OpenRouterOptions.cs` | Configuration class for OpenRouter settings |
| New: `GeminiOptions.cs` | Configuration class for Gemini settings |

### Configuration Schema (config.ini)

```ini
[AI]
; Primary provider selection
Provider=Gemini                          ; Gemini | OpenRouter

; Provider-specific settings
; For Gemini:
GeminiModel=gemini-2.5-flash-lite        ; Gemini model name
GeminiApiKey=${GEMINI_API_KEY}           ; From env var or literal

; For OpenRouter:
OpenRouterModel=openrouter/free            ; Router picks free model with needed features
; OR specific model: google/gemini-2.5-flash-lite:free
OpenRouterApiKey=${OPENROUTER_API_KEY}   ; Required for OpenRouter
OpenRouterBaseUrl=https://openrouter.ai/api/v1  ; Default, rarely needs override

[AI.Resilience]
; Retry and fallback configuration
EnableRetry=true
RetryAttempts=3
RetryBaseDelayMs=1000
RetryBackoffMultiplier=2
EnableCrossProviderFallback=true          ; Fallback to other provider on failure

; Feature-specific settings
EnableFunctionCalling=true                 ; Required for chat tools
RequireMultimodal=true                     ; Required for receipt scanning
```

### Implementation Notes

**Provider Factory Pattern:**
```csharp
public interface IAIProviderFactory
{
    IChatClient CreateClient();
    bool SupportsFunctionCalling { get; }
    bool SupportsMultimodal { get; }
}

public class GeminiProviderFactory : IAIProviderFactory { ... }
public class OpenRouterProviderFactory : IAIProviderFactory { ... }
```

**API Key Strategy:**
- Two separate env vars: `GEMINI_API_KEY` and `OPENROUTER_API_KEY`
- Both can exist in `.env` file
- Factory uses the key matching selected provider
- For cross-provider fallback, both keys must be present

**Model Naming:**
- Use provider-specific model names (no normalization)
- Gemini: `gemini-2.5-flash-lite`
- OpenRouter: `openrouter/free` or `google/gemini-2.5-flash-lite:free`
- Document examples in comments

### Acceptance Criteria
- [ ] Change `Provider=Gemini` → `Provider=OpenRouter` in config.ini → restart → works identically
- [ ] All 4 AI features work with both providers (voice, receipts, chat, shopping lists)
- [ ] Function calling works with both providers
- [ ] Multimodal (vision) works with both providers
- [ ] No breaking changes to existing Gemini-only setups
- [ ] Both API keys can coexist, only selected provider is used

---

## Phase 3: Resilience Layer with Cross-Provider Fallback (Issue #15)

**Objective:** Automatic retry and cross-provider fallback for 99.9% uptime.

### Architecture

```
User Request
    ↓
AIResilienceService
    ↓
┌─────────────────────────────────────────┐
│  Phase 1: Primary Provider Retries     │
│  - Retry 2x with exponential backoff   │
│  - If all fail → proceed to fallback   │
└─────────────────────────────────────────┘
    ↓ (if EnableCrossProviderFallback=true)
┌─────────────────────────────────────────┐
│  Phase 2: Cross-Provider Fallback      │
│  - Switch to other provider            │
│  - Retry 1x                            │
│  - If fail → proceed to last resort    │
└─────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────┐
│  Phase 3: Last Resort                  │
│  - Static/cached response              │
│  - OR error message to user            │
└─────────────────────────────────────────┘
```

### Components

| Component | Responsibility |
|-----------|--------------|
| `AIResilienceService` | Orchestrates retry and fallback logic |
| `RetryPolicy` | Configurable exponential backoff with jitter |
| `ProviderHealthMonitor` | Tracks success/failure rates per provider |
| `CircuitBreaker` | Temporarily disable failing provider |

### Configuration
```ini
[AI.Resilience]
EnableRetry=true
RetryAttempts=3
RetryBaseDelayMs=1000
RetryMaxDelayMs=8000
RetryBackoffMultiplier=2
EnableJitter=true                    ; Add randomness to prevent thundering herd

EnableCrossProviderFallback=true
FallbackProviderAutoSwitch=true      ; Automatically swap primary if one fails repeatedly

; Circuit breaker settings
CircuitBreakerFailureThreshold=5     ; Failures before opening circuit
CircuitBreakerRecoveryTimeout=60     ; Seconds before trying again

; Health monitoring
HealthCheckIntervalMinutes=5
```

### Fallback Scenarios

| Scenario | Behavior |
|----------|----------|
| Gemini rate limited (429) | Retry 2x → fallback to OpenRouter |
| Gemini server error (5xx) | Immediate fallback to OpenRouter |
| OpenRouter 200/day limit hit | Fallback to Gemini |
| Both providers failing | Return cached/static response |
| Function calling fails on OpenRouter | Retry with Gemini only |
| Vision fails on OpenRouter | Retry with Gemini only |

### Observability
All fallback events logged with:
- Original provider
- Fallback provider used
- Failure reason
- Latency of each attempt
- Final outcome

### Acceptance Criteria
- [ ] 99.9% success rate for AI requests (up from current ~95% with single provider)
- [ ] Fallback activates within 15 seconds of primary failure
- [ ] All retry/fallback events logged
- [ ] Alert if fallback rate >20% (indicates primary provider issues)
- [ ] Graceful degradation (user sees "Using backup AI..." message or seamless switch)

---

## Phase 4: Documentation & Cleanup

- [ ] Update `README.md` with new `[AI]` configuration options
- [ ] Document both provider setup (Gemini API key, OpenRouter API key)
- [ ] Add `docs/AI_CONFIGURATION.md` with examples
- [ ] Document interchangeability: "Change one line to switch providers"
- [ ] Document resilience behavior and fallback logic
- [ ] Move test harness to `/tools/` or delete after validation
- [ ] Update `.env.example` with both API key placeholders

---

## Open Questions / Decisions Made

| # | Question | Decision |
|---|----------|----------|
| 1 | **Provider priority for fallback?** | Configurable via `EnableCrossProviderFallback` and `FallbackProviderAutoSwitch` |
| 2 | **Rate limit handling?** | Automatic fallback to other provider when rate limits hit |
| 3 | **API key strategy?** | Two separate env vars: `GEMINI_API_KEY` and `OPENROUTER_API_KEY` |
| 4 | **Model naming normalization?** | No - use provider-specific names, document examples |
| 5 | **Keep Gemini SDK or switch to MEAI entirely?** | **TBD during implementation** - MEAI OpenAI package may work for both via Gemini's OpenAI-compatible endpoint |
| 6 | **Testing budget?** | ~100 requests (50 per provider) for thorough validation |

---

## Related Issues
- #14 - Investigate OpenRouter Free Tier for AI Cost Optimization
- #15 - AI Resilience: Retry Logic and Model Fallback Chain

---

## Implementation Order Recommendation

1. **Week 1**: Phase 2 (Dual-Provider Architecture) - Get both working
2. **Week 2**: Phase 1 (Test Harness) - Validate interchangeability
3. **Week 3**: Phase 3 (Resilience) - Add fallback logic
4. **Week 4**: Phase 4 (Documentation) - Write it all up
