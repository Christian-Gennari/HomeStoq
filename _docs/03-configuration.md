# Configuration Guide

📖 **Documentation Index:**
[README](../README.md) | [Getting Started](01-getting-started.md) | [Usage Guide](02-usage-guide.md) | Configuration | [Architecture](04-architecture.md)

HomeStoq is configured through two files:
- **`.env`** — Secrets (API keys, credentials)
- **`config.ini`** — Behavior settings

---

## The `.env` File

This file contains sensitive information. Never commit it to git.

```bash
# .env
GEMINI_API_KEY=your_api_key_here

# Google Keep Credentials (Optional)
GOOGLE_USERNAME=your_email@gmail.com
GOOGLE_PASSWORD=your_password
```

### Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `GEMINI_API_KEY` | **Yes** | Your Google AI Studio API key. |
| `GOOGLE_USERNAME` | No | Your Google account email for the scraper. |
| `GOOGLE_PASSWORD` | No | Your Google account password (or App Password). |

### Example

```bash
GEMINI_API_KEY=AIzaSyBxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
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
Model=gemini-3.1-flash-lite-preview

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

| Environment Variable | Purpose |
|----------------------|---------|
| `GEMINI_API_KEY` | Google AI API key (required) |
| `GOOGLE_USERNAME` | Google Keep auto-login email |
| `GOOGLE_PASSWORD` | Google Keep auto-login password |

**Note:** Application settings like `BrowserMode`, `HostUrl`, `Headless`, etc. should be configured in `config.ini`, not via environment variables. The Dockerfiles no longer include hardcoded ENV overrides for these settings.

**Example `.env` file:**
```bash
# .env
GEMINI_API_KEY=AIzaSyBxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
GOOGLE_USERNAME=homestoq.pantry@gmail.com
GOOGLE_PASSWORD=my-secure-password
```

**Docker Compose example:**
```yaml
services:
  homestoq:
    environment:
      - GEMINI_API_KEY=${GEMINI_API_KEY}
  
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
