# Configuration Guide

📖 **Documentation Index:**
[README](../README.md) | [Getting Started](01-getting-started.md) | [Usage Guide](02-usage-guide.md) | Configuration | [Architecture](04-architecture.md)

HomeStoq is configured through two files:
- **`.env`** — Secrets (API keys, credentials)
- **`config.ini`** — Behavior settings

---

## Accessing HomeStoq

Before diving into configuration files, here's how to determine the URL you'll use to access HomeStoq from different devices.

### Default Ports

| Service | Default | Configured In | Purpose |
|---------|---------|---------------|---------|
| HomeStoq Web App | 5050 | `[App]` → `HostUrl` | Main web interface (inventory, scanning, chat) |
| noVNC (Remote Desktop) | 6080 | `docker-compose.yml` | Viewing Chrome for Google Keep login |

### Finding Your Server's IP Address

To access HomeStoq from your phone or other devices, you need your server's local IP:

**Windows:**
```powershell
ipconfig
# Look for "IPv4 Address" under your active network adapter (e.g., 192.168.1.50)
```

**Mac:**
```bash
ipconfig getifaddr en0
# Alternative: ifconfig | grep "inet " | grep -v 127.0.0.1
```

**Linux:**
```bash
hostname -I
# Alternative: ip addr show | grep "inet " | grep -v 127.0.0.1
```

### Constructing Your HomeStoq URL

Once you have your IP and port:

```
http://192.168.1.50:5050
```

**Examples:**
- Local access: `http://localhost:5050`
- From phone on same network: `http://192.168.1.50:5050` (use your actual IP)

### Troubleshooting Access

**"Can't connect from my phone":**
1. Verify both devices are on the same WiFi network
2. Check `config.ini` has `HostUrl=http://*:5050` (the `*` is essential for external access)
3. Try disabling mobile data on your phone (force WiFi only)
4. Some routers block inter-device communication — check router "AP isolation" settings

**"Connection refused":**
- HomeStoq not running: `npm run dev`
- Wrong port: Check `config.ini` `[App]` → `HostUrl`
- Port already in use: Change to another port (e.g., `http://*:8080`)

---

## The `.env` File

This file contains sensitive information. Never commit it to git.

```bash
# .env

# REQUIRED: Gemini API key (always needed for receipt scanning)
GEMINI_API_KEY=your_api_key_here

# OPTIONAL: OpenRouter API key (only needed when Provider=OpenRouter in config.ini)
# OPENROUTER_API_KEY=sk-or-v1-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx

# Google Keep Credentials (Optional)
GOOGLE_USERNAME=your_email@gmail.com
GOOGLE_PASSWORD=your_password
```

### Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `GEMINI_API_KEY` | **Always** | Google AI Studio API key. Required for receipt scanning (vision) and when `Provider=Gemini`. |
| `OPENROUTER_API_KEY` | When `Provider=OpenRouter` | OpenRouter API key. Required for chat/voice when using OpenRouter as provider. |
| `GOOGLE_USERNAME` | No | Google account email for the scraper. |
| `GOOGLE_PASSWORD` | No | Google account password (or App Password). |

### Example

```bash
# .env

# Required for ALL setups
GEMINI_API_KEY=AIzaSyBxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx

# Required only when Provider=OpenRouter in config.ini
# OPENROUTER_API_KEY=sk-or-v1-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx

# Optional: Google Keep auto-login (not recommended with 2FA)
GOOGLE_USERNAME=homestoq.pantry@gmail.com
GOOGLE_PASSWORD=my-secure-password
```

---

## The `config.ini` File

This file controls how HomeStoq behaves. It's safe to edit and doesn't contain secrets.

### Full Example

```ini
[App]
Language=Swedish
HostUrl=http://*:5050    # Server binding URL (used by API and scraper)

[AI]
# AI Provider Selection
Provider=Gemini                    # Gemini | OpenRouter
GeminiModel=gemini-2.5-flash-lite  # When Provider=Gemini
# OpenRouterModel=openrouter/free  # When Provider=OpenRouter

[AI.Vision]
# Receipt scanning always uses Gemini with fallback chain
PrimaryModel=gemini-2.5-flash-lite
FallbackModels=gemini-2.5-flash-lite,gemini-2.5-flash,gemini-2.5-pro

[AI.Resilience]
# Retry and fallback behavior
EnableRetry=true
RetryAttempts=3
RetryBaseDelayMs=1000

[API]
# Optional: Override auto-derived API URL
# BaseUrl=http://localhost:5050/api/voice/command

[GoogleKeepScraper]
KeepListName=inköpslistan, inköpslista
ActiveHours=07-23
PollIntervalSeconds=45
PollIntervalJitterSeconds=15
BrowserMode=RemoteDebugging
ChromeRelaunchAttempts=5
```

---

## Section by Section

### [App] — General Application Settings

| Setting | Default | Options | Description |
|---------|---------|---------|-------------|
| `Language` | `English` | `English`, `Swedish` | Language for all AI interactions (voice parsing, receipt OCR, chat, shopping suggestions) |
| `HostUrl` | `http://*:5050` | Valid URL | Server binding URL. Controls where the web server listens. Scraper uses this to derive API callback URL. |

**Examples:**
```ini
[App]
Language=Swedish
HostUrl=http://*:5050
```

**Impact:**
- Voice commands parsed in Swedish
- Receipt items named in Swedish ("Mjölk" not "Milk")
- Chat responses in Swedish
- Shopping list suggestions in Swedish
- Server binds to port 5050 (change port to avoid permission issues)

**HostUrl Common scenarios:**
- `localhost:5050` — Testing only
- `*:5050` — Development/production (all network interfaces)
- `*:80` — Requires admin/root privileges
- Specific IP — Multi-homed servers

**Docker:**
Always use `http://*:PORT` in Docker (never `localhost` or specific IPs).

---

### [API] — API Endpoint Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `BaseUrl` | *(auto-derived from HostUrl)* | Where the scraper sends voice commands. Only set if you need a custom URL. |

**Default Behavior:**
By default, the API URL is automatically derived from `HostUrl` by replacing `*` with `localhost`.
- `HostUrl=http://*:5050` → `BaseUrl=http://localhost:5050/api/voice/command`
- `HostUrl=http://*:8080` → `BaseUrl=http://localhost:8080/api/voice/command`

**When to Override:**
- Running API on a different machine from the scraper
- Docker networking scenarios (container names as hosts)
- Advanced network configurations

**Examples:**
```ini
[API]
# Docker: API in container, scraper on host
BaseUrl=http://homestoq:5050/api/voice/command

# Different machine on network
BaseUrl=http://192.168.1.50:5050/api/voice/command
```

---

### [GoogleKeepScraper] — Scraper Behavior

These settings control how the voice integration works.

| Setting | Default | Range | Description |
|---------|---------|-------|-------------|
| `KeepListName` | `inköpslistan` | Any list name(s) | Name(s) of Google Keep list(s) to monitor. |
| `BrowserMode` | `RemoteDebugging` | `RemoteDebugging`, `Playwright` | Which browser connection to use |
| `Headless` | `false` | `true`, `false` | Run in headless mode (not recommended for anti-detection) |
| `ActiveHours` | `07-23` | `00-24` format | When the scraper actively polls |
| `PollIntervalSeconds` | `45` | 10-300 | Base time between checks |
| `PollIntervalJitterSeconds` | `15` | 0-60 | Random variance added to interval |
| `ChromeRelaunchAttempts` | `5` | 1-20 | How many times to retry if Chrome crashes |

> **Note:** `HostUrl` is now configured in the `[App]` section. The scraper inherits this setting to derive its API callback URL.

#### KeepListName

The name(s) of the Google Keep list(s) the scraper will monitor for voice commands.
When you say *"Hey Google, add 'slut på mjölk' to my [list name]"*, it must match exactly.

**Examples:**
```ini
[GoogleKeepScraper]
KeepListName=inköpslistan                    # Single list
KeepListName=inköpslistan, Shopping          # Multiple lists
KeepListName=HomeStoq, Groceries, Pantry     # Family setup
```

**Tips:**
- Use simple, memorable names
- Swedish default works well with Google Nest in Sweden
- Multiple lists are checked in order

---

#### BrowserMode
- Uses your real Chrome browser
- Harder for Google to detect
- Requires Chrome installed
- Profile saved to `%LocalAppData%/HomeStoq/chrome-profile`

**`Playwright`** (Fallback)
- Uses bundled Chromium browser
- More isolated but more detectable
- Works without Chrome installed
- Profile saved to `browser-profile/`

**Example:**
```ini
[GoogleKeepScraper]
BrowserMode=RemoteDebugging
```

#### ActiveHours

Format: `HH-HH` in 24-hour format.

**Examples:**
```ini
ActiveHours=07-23      # 7 AM to 11 PM (default)
ActiveHours=08-20      # 8 AM to 8 PM
ActiveHours=22-06      # 10 PM to 6 AM (overnight)
ActiveHours=00-24      # Always active (not recommended)
```

**Why it matters:**
- Prevents 24/7 bot-like patterns
- Saves resources when you're asleep
- Avoids late-night Google security checks

**Behavior:**
- Inside hours: Polls every ~45 seconds
- Outside hours: Sleeps for 30 minutes, checks if inside hours yet

#### PollIntervalSeconds & PollIntervalJitterSeconds

Together these create unpredictable, human-like timing.

**Example:**
```ini
PollIntervalSeconds=45
PollIntervalJitterSeconds=15
```

**Result:**
Actual intervals range from 30 to 60 seconds (45 ± 15).

**Why randomize:**
- Perfectly regular intervals look like bots
- Human behavior is unpredictable
- Helps avoid detection

#### ChromeRelaunchAttempts

Only applies to `RemoteDebugging` mode.

**Example:**
```ini
ChromeRelaunchAttempts=5
```

**Behavior:**
- If Chrome crashes, scraper tries to relaunch it
- Waits progressively longer between attempts (10s, 20s, 40s...)
- Gives up after this many attempts

#### HostUrl (configured in [App] section)

Controls where the web server listens. This setting has been moved to the `[App]` section to reflect that it's a global application setting, not specific to the scraper.

**Examples:**
```ini
[App]
HostUrl=http://localhost:5050     # Local only
HostUrl=http://*:5050             # All interfaces, port 5050 (default)
HostUrl=http://*:80               # All interfaces, port 80 (requires admin)
HostUrl=http://192.168.1.50:5050  # Specific IP
```

**Common scenarios:**
- `localhost:5050` — Testing only
- `*:5050` — Development/production (default, no admin required)
- `*:80` — Requires admin/root privileges (not recommended)
- Specific IP — Multi-homed servers

**Docker:**
Always use `http://*:PORT` in Docker (never `localhost` or specific IPs).

**Why port 5050?**
Port 80 requires administrator/root privileges on most systems. Port 5050 is used by default to avoid permission issues while being easy to remember.

---

### [AI] — AI Provider Selection

HomeStoq uses a **hybrid AI architecture**:
- **Vision (receipt scanning):** Always uses Gemini (for reliable OCR)
- **General (chat/voice/shopping):** Configurable (Gemini or OpenRouter)

| Setting | Default | Options | Description |
|---------|---------|---------|-------------|
| `Provider` | `Gemini` | `Gemini`, `OpenRouter` | Which AI provider for general operations |
| `GeminiModel` | `gemini-2.5-flash-lite` | See below | Model when Provider=Gemini |
| `OpenRouterModel` | `openrouter/free` | See OpenRouter docs | Model when Provider=OpenRouter |
| `GeminiApiKey` | `${GEMINI_API_KEY}` | Any valid key | API key for Gemini (env var reference) |
| `OpenRouterApiKey` | `${OPENROUTER_API_KEY}` | Any valid key | API key for OpenRouter (required when Provider=OpenRouter) |

**⚠️ Important:** Receipt scanning (vision/OCR) **always** uses Gemini regardless of `Provider` setting. This is because OpenRouter's free tier has inconsistent vision support. Therefore, `GEMINI_API_KEY` is **always required** in `.env`, even when using OpenRouter.

**Available Gemini Models:**

| Model | Speed | Quality | Best For |
|-------|-------|---------|----------|
| `gemini-2.5-flash-lite` | Fastest | Good | **Default** — Quick tasks, generous limits |
| `gemini-2.5-flash` | Fast | Better | Fallback if rate limited |
| `gemini-2.5-pro` | Slower | Best | Complex reasoning, meal planning |
| `gemini-3.1-flash-lite-preview` | Fast | Good | Preview features |

**OpenRouter Models:**
- `openrouter/free` — Router picks free models automatically (may vary)
- `google/gemini-2.5-flash-lite:free` — Specific Gemini model via OpenRouter
- See [OpenRouter models](https://openrouter.ai/models) for full list

**Examples:**

```ini
# Default: Use Gemini for everything
[AI]
Provider=Gemini
GeminiModel=gemini-2.5-flash-lite

# Use OpenRouter for chat/voice (vision still uses Gemini)
[AI]
Provider=OpenRouter
OpenRouterModel=openrouter/free
# GEMINI_API_KEY still required in .env for receipt scanning!
```

**When to Use OpenRouter:**
- Want to experiment with different models
- Cost optimization (free tier has different limits)
- Redundancy/fallback options

**When to Stick with Gemini:**
- Simplicity (single key)
- Guaranteed vision support
- Consistent behavior

---

### [AI.Vision] — Vision/OCR Settings

Receipt scanning uses Gemini with automatic model fallback for reliability.

| Setting | Default | Description |
|---------|---------|-------------|
| `PrimaryModel` | `gemini-2.5-flash-lite` | First model to try for OCR |
| `FallbackModels` | `gemini-2.5-flash-lite,gemini-2.5-flash,gemini-2.5-pro` | Comma-separated list of models to try if primary fails |
| `MaxAttemptsPerModel` | `2` | How many times to retry each model before moving to next |

**How Fallback Works:**
1. Try `PrimaryModel` (up to `MaxAttemptsPerModel` times)
2. If fails, try next model in `FallbackModels` list
3. Continue until one succeeds or all fail
4. If all fail, return "Receipt scanning temporarily unavailable"

**Example:**
```ini
[AI.Vision]
PrimaryModel=gemini-2.5-flash-lite
FallbackModels=gemini-2.5-flash-lite,gemini-2.5-flash,gemini-2.5-pro
MaxAttemptsPerModel=2
```

**When to Adjust:**
- Getting frequent "temporarily unavailable" errors? Add more models to fallback list
- Want faster retries? Reduce `MaxAttemptsPerModel` to 1
- Want more retry attempts? Increase `MaxAttemptsPerModel` to 3

---

### [AI.Resilience] — Retry and Fallback Settings

Controls retry behavior for AI requests.

| Setting | Default | Description |
|---------|---------|-------------|
| `EnableRetry` | `true` | Enable automatic retry on transient failures |
| `RetryAttempts` | `3` | Maximum retry attempts per operation |
| `RetryBaseDelayMs` | `1000` | Initial delay between retries (milliseconds) |
| `RetryMaxDelayMs` | `10000` | Maximum delay cap (milliseconds) |
| `RetryBackoffMultiplier` | `2` | Exponential backoff multiplier |
| `EnableCrossProviderFallback` | `true` | Fallback to alternate provider on failure |

**Retry Logic:**
- Programming errors (JSON parsing, validation) → **No retry** (fail immediately)
- Network/transient errors (timeouts, 5xx) → **Retry with exponential backoff**
- Rate limits (429) → **Retry after delay**

**Exponential Backoff Example:**
- Retry 1: Wait 1s + jitter
- Retry 2: Wait 2s + jitter  
- Retry 3: Wait 4s + jitter (capped at 10s)

**Example:**
```ini
[AI.Resilience]
EnableRetry=true
RetryAttempts=3
RetryBaseDelayMs=1000
RetryMaxDelayMs=10000
RetryBackoffMultiplier=2
EnableCrossProviderFallback=true
```

---

## Docker Configuration

HomeStoq Docker containers read all configuration from `config.ini`. This ensures a single source of truth across all deployments.

**What's configured in config.ini:**
- Server port and binding (`App:HostUrl`)
- Browser mode and settings (`GoogleKeepScraper:BrowserMode`, `Headless`)
- All scraper timing and behavior settings
- AI model selection

**Docker volumes required:**
```yaml
services:
  homestoq:
    volumes:
      - ./config.ini:/app/config.ini:ro  # Read-only config
      - ./data:/app/data                  # Database
  
  scraper:
    volumes:
      - ./config.ini:/app/config.ini:ro   # Read-only config
      - ./chrome-profile:/app/browser-profile  # Browser session
```

**Why this approach:**
- No configuration drift between local and Docker
- Single file to edit for all settings
- Dockerfiles don't hardcode ports or modes
- Easy to version control and backup

---

## Environment Variable Overrides

**For secrets only** — Use `.env` file for sensitive data:

| Environment Variable | Required | Purpose |
|----------------------|----------|---------|
| `GEMINI_API_KEY` | **Always** | Google AI API key for vision/receipt scanning and Gemini provider |
| `OPENROUTER_API_KEY` | When `Provider=OpenRouter` | OpenRouter API key for chat/voice when using OpenRouter provider |
| `GOOGLE_USERNAME` | No | Google Keep auto-login email |
| `GOOGLE_PASSWORD` | No | Google Keep auto-login password |

**Important API Key Rules:**
- **GEMINI_API_KEY is ALWAYS required** — Even when using `Provider=OpenRouter`, receipt scanning uses Gemini
- **OPENROUTER_API_KEY only when needed** — Required only when `Provider=OpenRouter` in config.ini
- Both keys can coexist in `.env` — The active provider determines which is used for general operations

**Note:** Application settings like `BrowserMode`, `HostUrl`, `Headless`, etc. should be configured in `config.ini`, not via environment variables. The Dockerfiles no longer include hardcoded ENV overrides for these settings.

**Example `.env` file:**
```bash
# .env

# Required for ALL setups (receipt scanning always uses Gemini)
GEMINI_API_KEY=AIzaSyBxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx

# Required ONLY when Provider=OpenRouter in config.ini
# Optional when Provider=Gemini (can be omitted or commented)
OPENROUTER_API_KEY=sk-or-v1-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx

# Optional: Google Keep auto-login (NOT recommended with 2FA)
# If you have 2FA enabled, skip these and use manual login via noVNC
GOOGLE_USERNAME=homestoq.pantry@gmail.com
GOOGLE_PASSWORD=my-secure-password
```

**Docker Compose example:**
```yaml
services:
  homestoq:
    environment:
      - GEMINI_API_KEY=${GEMINI_API_KEY}
      - OPENROUTER_API_KEY=${OPENROUTER_API_KEY}  # Only needed when Provider=OpenRouter
  
  scraper:
    environment:
      - GOOGLE_USERNAME=${GOOGLE_USERNAME}
      - GOOGLE_PASSWORD=${GOOGLE_PASSWORD}
```

---

## Configuration Tips

### For Families

If multiple people use HomeStoq:

```ini
[GoogleKeepScraper]
KeepListName=inköpslistan, HomeStoq, Family Shopping
ActiveHours=06-23    # Earlier start for morning people
PollIntervalSeconds=30   # Faster response
```

### For Low-Power Devices (Raspberry Pi)

```ini
[GoogleKeepScraper]
BrowserMode=Playwright   # Uses less resources than Chrome
PollIntervalSeconds=60   # Less frequent polling
ActiveHours=07-22        # Shorter window
```

### For Privacy-Conscious Users

```ini
[GoogleKeepScraper]
BrowserMode=RemoteDebugging   # Your real browser, harder to fingerprint
ActiveHours=08-20             # Limited window
```

---

## Troubleshooting Config Issues

### "Changes not taking effect"

- Restart the service after changing `config.ini`
- Check for typos in section names (`[GoogleKeepScraper]` not `[Scraper]`)

### "Invalid value errors"

- `ActiveHours` must be `HH-HH` format (e.g., `07-23`)
- `BrowserMode` must be exactly `RemoteDebugging` or `Playwright`
- Numbers must not have quotes

### "Config file not found"

- Ensure `config.ini` is in the project root
- Check file encoding (UTF-8)
- Verify file is readable

### "AI provider errors" or "Receipt scanning fails"

**Problem:** Receipt scanning or chat returns API errors.

**Common Causes & Solutions:**

**1. Missing GEMINI_API_KEY**
- **Error:** "Vision service unavailable" or 400 Bad Request
- **Solution:** `GEMINI_API_KEY` is **always required** in `.env`, even when `Provider=OpenRouter`. Add your Gemini key.

**2. Missing OPENROUTER_API_KEY when Provider=OpenRouter**
- **Error:** "OPENROUTER_API_KEY environment variable is required"
- **Solution:** Add `OPENROUTER_API_KEY=sk-or-v1-...` to `.env`

**3. Rate limits (Gemini)**
- **Error:** 429 errors, "temporarily unavailable" messages
- **Solution:** Try a different model in `config.ini`:
  ```ini
  [AI]
  GeminiModel=gemini-2.5-flash  # Faster, higher limits
  ```
  Or adjust resilience settings:
  ```ini
  [AI.Resilience]
  RetryAttempts=5
  RetryBaseDelayMs=2000
  ```

**4. Rate limits (OpenRouter)**
- **Error:** 429 errors with OpenRouter free tier (20 RPM / 200 requests/day limit)
- **Solution:** Switch back to Gemini or upgrade to paid tier
  ```ini
  [AI]
  Provider=Gemini
  ```

**5. Vision model chain exhaustion**
- **Error:** "All vision models failed"
- **Solution:** Add more fallback models in `config.ini`:
  ```ini
  [AI.Vision]
  FallbackModels=gemini-2.5-flash-lite,gemini-2.5-flash,gemini-2.5-pro,gemini-2.0-flash
  ```

### "Provider switching not working"

**Problem:** Changed `Provider=OpenRouter` but getting errors.

**Checklist:**
1. ✅ Added `OPENROUTER_API_KEY` to `.env`
2. ✅ Kept `GEMINI_API_KEY` in `.env` (required for vision)
3. ✅ Restarted containers (`npm run stop && npm run dev`)
4. ✅ Check logs: Look for "General AI provider: OpenRouter" at startup

### "Non-recoverable error not failing fast"

**Problem:** Programming errors (JSON parsing) trigger retries instead of failing immediately.

**Solution:** This is by design. The system distinguishes between:
- **Provider errors** (network, rate limits) → Retry with backoff
- **Non-recoverable errors** (parsing, validation) → Fail immediately

If you see retries for parsing errors, check the logs — the error should be classified correctly.
