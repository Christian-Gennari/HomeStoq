# AI Resilience & OpenRouter Integration - Branch Plan

**Branch:** `feature/ai-openrouter-integration`
**Created:** 2025-04-08
**Goal:** Evaluate and integrate OpenRouter free tier as cost-effective alternative to direct Gemini API, with automatic fallback chain for resilience.

---

## Phase 1: Investigation & Testing (Issue #14)

**Objective:** Evaluate `openrouter/free` against all current AI use cases before production integration.

### Test Scenarios
| # | Scenario | Input | Expected Output | Gemini Baseline | OpenRouter Target |
|---|----------|-------|-----------------|-----------------|-------------------|
| 1 | Voice Command Parsing | Swedish: "slut på mjölk" | JSON array of actions | ✅ Working | ⬜ Test |
| 2 | Receipt OCR | JPEG receipt image | JSON list of items | ✅ Working | ⬜ Test |
| 3 | Chat with Tools | "How much milk do I have?" | Function call to GetStockLevel | ✅ Working | ⬜ Test |
| 4 | Shopping List Generation | 30-day history + inventory | JSON shopping suggestions | ✅ Working | ⬜ Test |
| 5 | JSON Reliability | 100 identical prompts | 100 valid JSON responses | ⬜ Measure | ⬜ Measure |
| 6 | Swedish Comprehension | 20 Swedish voice commands | Correct intent extraction | ⬜ Measure | ⬜ Measure |

### Metrics to Capture
- Success rate (valid JSON / total requests)
- Mean latency (p50, p95, p99)
- Rate limit hits per typical usage hour
- Swedish language accuracy score (manual review of 20 samples)

### Deliverables
- [ ] `/tests/OpenRouterEvaluation/` test harness project
- [ ] `phase1-results.md` with test results and go/no-go decision

---

## Phase 2: Dual-Provider Architecture

**Objective:** Implement configurable provider switching (Gemini ↔ OpenRouter).

### Code Changes
| File | Change |
|------|--------|
| `HomeStoq.App.csproj` | Replace `Google.GenAI` with `Microsoft.Extensions.AI.OpenAI` |
| `Program.cs` | Add `AIProviderFactory` for client initialization |
| `config.ini` | Add `[AI] Provider=`, `BaseUrl=`, `ApiKey=` settings |
| `GeminiService.cs` | Rename to `AIService` or keep as generic abstraction |
| New: `AIProviderFactory.cs` | Factory pattern for provider instantiation |
| New: `OpenRouterChatClient.cs` | Wrapper if needed for special handling |

### Configuration Schema
```ini
[AI]
Provider=Gemini              ; Gemini | OpenRouter
Model=gemini-2.5-flash-lite  ; Provider-specific model name
; Optional overrides:
BaseUrl=https://openrouter.ai/api/v1  ; For OpenRouter
ApiKey=sk-or-v1-...                   ; OpenRouter key (or use GEMINI_API_KEY env)
```

### Acceptance Criteria
- [ ] Switching `Provider=Gemini` ↔ `Provider=OpenRouter` works with config.ini change + restart
- [ ] All 4 AI features work with both providers
- [ ] No breaking changes to existing Gemini-only setups (backward compatible)

---

## Phase 3: Resilience Layer (Issue #15)

**Objective:** Automatic retry logic with cross-provider fallback chain.

### Implementation
- [ ] `AIResilienceService.cs` - Orchestrates retries and fallbacks
- [ ] `RetryPolicy.cs` - Configurable exponential backoff
- [ ] `FallbackChain.cs` - Provider prioritization

### Fallback Chain
```
Primary:    config.ini [AI] Provider (Gemini or OpenRouter)
├─ Retry 2x with exponential backoff
└─ Fallback: Other provider (if Primary=Gemini → OpenRouter, vice versa)
   ├─ Retry 1x
   └─ Last resort: Gemini 1.5 Flash (most reliable)
      └─ If all fail: Return cached response or error message
```

### Configuration
```ini
[AI]
RetryAttempts=3
RetryBaseDelayMs=1000
EnableFallbackChain=true
```

### Acceptance Criteria
- [ ] 99.9% success rate for AI requests
- [ ] Fallback activates within 15 seconds of primary failure
- [ ] Logs track all fallback events with reason
- [ ] Graceful degradation (never hard fail)

---

## Phase 4: Documentation & Cleanup

- [ ] Update README.md with new AI configuration options
- [ ] Add OpenRouter setup instructions
- [ ] Document fallback behavior
- [ ] Remove test harness (or move to `/tools/`)

---

## Open Questions / Decisions Needed

1. **Provider priority**: Gemini default with OpenRouter fallback, or vice versa?
2. **Rate limit handling**: Hard fail when OpenRouter 200/day hits, or immediate fallback to Gemini?
3. **API key strategy**: Separate env var `OPENROUTER_API_KEY` or reuse `GEMINI_API_KEY` with detection?
4. **Testing budget**: Do we want to burn ~50 requests on OpenRouter for testing, or mock more?

---

## Related Issues
- #14 - Investigate OpenRouter Free Tier for AI Cost Optimization
- #15 - AI Resilience: Retry Logic and Model Fallback Chain
