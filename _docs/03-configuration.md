# Configuration Guide

📖 **Documentation Index:**
[README](../README.md) | [Getting Started](01-getting-started.md) | [Usage Guide](02-usage-guide.md) | Configuration | [Architecture](04-architecture.md)

HomeStoq is configured through two files:
- **`.env`** — Secrets (API keys)
- **`config.ini`** — Behavior settings

---

## The `.env` File

This file contains sensitive information. Never commit it to git.

```bash
# .env
GEMINI_API_KEY=your_api_key_here
```

### Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `GEMINI_API_KEY` | **Yes** | Your Google AI Studio API key. Get one at [aistudio.google.com](https://aistudio.google.com/app/apikey) |

### Example

```bash
GEMINI_API_KEY=AIzaSyBxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

---

## The `config.ini` File

This file controls how HomeStoq behaves. It's safe to edit and doesn't contain secrets.

### Full Example

```ini
[App]
Language=Swedish

[AI]
Model=gemini-3.1-flash-lite-preview

[API]
# Optional: Override auto-derived API URL
# BaseUrl=http://localhost/api/voice/command

[GoogleKeepScraper]
KeepListName=inköpslistan, inköpslista
ActiveHours=07-23
PollIntervalSeconds=45
PollIntervalJitterSeconds=15
HostUrl=http://*:80
BrowserMode=RemoteDebugging
ChromeRelaunchAttempts=5
```

---

## Section by Section

### [App] — General Application Settings

| Setting | Default | Options | Description |
|---------|---------|---------|-------------|
| `Language` | `English` | `English`, `Swedish` | Language for all AI interactions (voice parsing, receipt OCR, chat, shopping suggestions) |

**Example:**
```ini
[App]
Language=Swedish
```

**Impact:**
- Voice commands parsed in Swedish
- Receipt items named in Swedish ("Mjölk" not "Milk")
- Chat responses in Swedish
- Shopping list suggestions in Swedish

---

### [API] — API Endpoint Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `BaseUrl` | *(auto-derived from HostUrl)* | Where the scraper sends voice commands. Only set if you need a custom URL. |

**Default Behavior:**
By default, the API URL is automatically derived from `HostUrl` by replacing `*` with `localhost`.
- `HostUrl=http://*:80` → `BaseUrl=http://localhost/api/voice/command`
- `HostUrl=http://*:5000` → `BaseUrl=http://localhost:5000/api/voice/command`

**When to Override:**
- Running API on a different machine from the scraper
- Docker networking scenarios (container names as hosts)
- Advanced network configurations

**Examples:**
```ini
[API]
# Docker: API in container, scraper on host
BaseUrl=http://homestoq:5000/api/voice/command

# Different machine on network
BaseUrl=http://192.168.1.50:8080/api/voice/command
```

---

### [GoogleKeepScraper] — Scraper Behavior

These settings control how the voice integration works.

| Setting | Default | Range | Description |
|---------|---------|-------|-------------|
| `KeepListName` | `inköpslistan` | Any list name(s) | Name(s) of Google Keep list(s) to monitor. Comma-separated for multiple. |
| `BrowserMode` | `RemoteDebugging` | `RemoteDebugging`, `Playwright` | Which browser connection to use |
| `ActiveHours` | `07-23` | `00-24` format | When the scraper actively polls |
| `PollIntervalSeconds` | `45` | 10-300 | Base time between checks |
| `PollIntervalJitterSeconds` | `15` | 0-60 | Random variance added to interval |
| `ChromeRelaunchAttempts` | `5` | 1-20 | How many times to retry if Chrome crashes |
| `HostUrl` | `http://*:80` | Valid URL | Where the web server binds |

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

#### HostUrl

Controls where the web server listens.

**Examples:**
```ini
HostUrl=http://localhost:5000     # Local only
HostUrl=http://*:5000             # All interfaces, port 5000
HostUrl=http://*:80               # All interfaces, port 80 (default)
HostUrl=http://192.168.1.50:8080  # Specific IP
```

**Common scenarios:**
- `localhost` — Testing only
- `*:5000` — Development with specific port
- `*:80` — Production (easy to remember, no port needed)
- Specific IP — Multi-homed servers

**Docker:**
Always use `http://*:PORT` in Docker (never `localhost` or specific IPs).

---

### [AI] — AI Model Settings

| Setting | Default | Options | Description |
|---------|---------|---------|-------------|
| `Model` | `gemini-3.1-flash-lite-preview` | See below | Which Gemini model to use |

**Available Models:**

| Model | Speed | Quality | Best For |
|-------|-------|---------|----------|
| `gemini-3.1-flash-lite-preview` | Fast | Good | **Default** — General use, balanced |
| `gemini-2.5-flash` | Fast | Better | Fallback if rate limited |
| `gemini-2.5-pro` | Slower | Best | Complex reasoning, meal planning |
| `gemini-2.5-flash-lite` | Fastest | Good | Quick tasks, generous limits |
| `gemini-embedding-001` | Varies | N/A | Semantic search (advanced) |

**Example:**
```ini
[AI]
Model=gemini-2.5-pro
```

**When to Change:**
- Getting rate limited? Try `gemini-2.5-flash`
- Need better reasoning? Try `gemini-2.5-pro`
- Normal use? Keep the default

---

## Environment Variable Overrides

You can override any config setting with environment variables (useful for Docker):

| Environment Variable | Overrides |
|----------------------|-----------|
| `KEEP_LIST_NAME` | `GoogleKeepScraper:KeepListName` |
| `POLL_INTERVAL_SECONDS` | `GoogleKeepScraper:PollIntervalSeconds` |
| `POLL_INTERVAL_JITTER_SECONDS` | `GoogleKeepScraper:PollIntervalJitterSeconds` |
| `DATABASE_PATH` | Database file location |

**Example in Docker Compose:**
```yaml
services:
  scraper:
    environment:
      - POLL_INTERVAL_SECONDS=60
      - KEEP_LIST_NAME=Shopping,Groceries
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
