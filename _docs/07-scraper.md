# Scraper Deep-Dive

📖 **Documentation Index:**
[README](../README.md) | [Getting Started](01-getting-started.md) | [Usage Guide](02-usage-guide.md) | [Configuration](03-configuration.md) | [Architecture](04-architecture.md) | [Scraper](07-scraper.md) | [Development](08-development.md)

This guide covers the Google Keep scraper in detail: how it works, the two browser modes, anti-detection strategies, and troubleshooting.

---

## What the Scraper Does

The scraper is the bridge between your voice assistant and HomeStoq:

```
You say: "slut på mjölk"
        ↓
Google Assistant adds to Keep list
        ↓
[SCRAPER] Watches list every ~45 seconds
        ↓
Detects new item, sends to HomeStoq API
        ↓
Inventory updated, item cleaned up
```

Without the scraper, you'd have to manually check Google Keep and type updates into HomeStoq.

---

## Two Browser Modes

The scraper can connect to browsers in two ways:
## Two Browser Modes

### Mode 1: Remote Debugging (Recommended)

Connects to your **real Chrome browser** via Chrome DevTools Protocol (CDP).

**How it works:**
1. Scraper launches Chrome with `--remote-debugging-port=9222`.
2. Connects to Chrome's debug interface.
3. In Docker, this runs inside a **Virtual Desktop (Xvfb)** to allow "Headed" mode.

**Why it's better:**
- Uses real browser fingerprints.
- Avoids Google's "automated software" detection by running in full **Headed mode** (even in Docker).

**Configuration:**
```ini
[GoogleKeepScraper]
BrowserMode=RemoteDebugging
Headless=false
```

> Set these in `config.ini` — they apply to both Docker and local runs.

---

## Dockerized Scraping with Xvfb

When running in Docker, HomeStoq uses a specialized setup to avoid bot detection while remaining headless on your server:

1.  **Xvfb (X Virtual Framebuffer):** Creates a "virtual monitor" in the container's memory.
2.  **Headed Chrome:** Chrome runs in its normal, visible mode inside this virtual monitor.
3.  **Automatic Login:** If `GOOGLE_USERNAME` and `GOOGLE_PASSWORD` are in your `.env`, the scraper will automatically type them into the login form.
4.  **noVNC (Remote View):** A web-based VNC client is provided on port **6080**.

### How to use noVNC for 2FA

If your Google account requires 2FA or shows a CAPTCHA:

1. Start HomeStoq: `npm run dev`
2. Open `http://localhost:6080` in your browser.
3. You will see the Chrome window running inside the container.
4. Use your mouse and keyboard to complete the 2FA process.
5. Once logged in, the session is saved to the `chrome-profile` volume.

**Configuration (in config.ini):**
```ini
[GoogleKeepScraper]
BrowserMode=RemoteDebugging
Headless=false
```

> **Note:** The scraper reads `Headless` and `BrowserMode` from `config.ini`. Do not set these via Docker environment variables.

---

### Mode 2: Playwright (Fallback)

Launches an **isolated browser** controlled by Playwright.

**How it works:**
1. Scraper launches its own Chromium instance
2. Injects anti-detection JavaScript
3. Navigates to Google Keep
4. Same polling behavior

**When to use:**
- Chrome not installed
- CDP mode having issues
- Testing environment
- Running on low-power devices

**Trade-offs:**
- More detectable (uses anti-detection scripts)
- Isolated profile (no access to your existing cookies)
- Works out of the box

**Profile location:** `browser-profile/` in the project directory

**Configuration:**
```ini
[GoogleKeepScraper]
BrowserMode=Playwright
Headless=true  # Optional: set to false for visible browser
```

> Set these in `config.ini` — they apply to both Docker and local runs.

---

## The Polling Cycle

Every ~45 seconds (with random variation), the scraper:

```
1. Check: Are we within ActiveHours?
   ├── No → Sleep 30 minutes, check again
   └── Yes → Continue

2. (CDP mode only) Check: Is Chrome still running?
   ├── No → Relaunch Chrome
   └── Yes → Continue

3. Navigate to Google Keep

4. For each configured list:
   ├── Open the list
   ├── Find all unchecked items
   └── For each item:
       ├── Extract text
       ├── POST to /api/voice/command
       ├── On success: Check the box
       └── On failure: Log error

5. Delete all checked items (cleanup)

6. Close list

7. Sleep ~45 seconds (randomized)
```

**Timing details:**
- Base interval: 45 seconds
- Random jitter: ±15 seconds
- Result: Actual intervals between 30-60 seconds
- Why random? Prevents predictable bot-like patterns

---

## Anti-Detection (Playwright Mode Only)

When using Playwright mode, the scraper employs several techniques to avoid looking like a bot:

### Browser Fingerprint Spoofing

JavaScript injected before page load modifies browser properties:

| Property | What We Change | Why |
|----------|----------------|-----|
| `navigator.webdriver` | Set to `undefined` | Automation tools set this to `true` |
| WebGL vendor/renderer | Mock Intel GPU strings | Avoid "SwiftShader" detection |
| `window.chrome` object | Add full API mock | Chrome-specific APIs |
| Navigator plugins | Add realistic plugin list | Real browsers have plugins |
| Hardware specs | Report 8 cores, 8GB RAM | Avoid low-spec bot detection |

### Behavioral Noise

Even with perfect fingerprints, perfect timing is suspicious:

- **Active Hours**: Only runs 07:00-23:00 (configurable)
- **Random Delays**: Each poll varies by ±15 seconds
- **Random Actions**: 10% chance to:
  - Switch between Notes and Reminders tabs
  - Scroll up and down
  - Hover over random notes

### Why CDP Mode Doesn't Need This

CDP mode uses your **actual Chrome**:
- Real browser fingerprint
- Real user cookies and history
- Real IP address reputation
- Google's own browser

It's essentially indistinguishable from you using Chrome yourself.

---

## Configuration Reference

All scraper settings are in `config.ini`. This file is mounted as a read-only volume in Docker, ensuring the same configuration across all environments.

```ini
[GoogleKeepScraper]
BrowserMode=RemoteDebugging
Headless=false
ActiveHours=07-23
PollIntervalSeconds=45
PollIntervalJitterSeconds=15
ChromeRelaunchAttempts=5
```

### BrowserMode

- `RemoteDebugging` — Your real Chrome (recommended)
- `Playwright` — Isolated browser (fallback)

> **Docker note:** The Dockerfiles no longer hardcode these values. Always set them in `config.ini`.

### ActiveHours

Format: `HH-HH` in 24-hour time.

Examples:
- `07-23` — 7 AM to 11 PM (default)
- `08-20` — 8 AM to 8 PM
- `22-06` — 10 PM to 6 AM (overnight)

Outside these hours, the scraper sleeps.

### PollIntervalSeconds & PollIntervalJitterSeconds

Together create unpredictable timing:

```
Actual interval = 45 ± 15 seconds
= anywhere from 30 to 60 seconds
```

This mimics human inconsistency.

### ChromeRelaunchAttempts

Only for CDP mode. If Chrome crashes:
1. Try to reconnect
2. Wait progressively longer (10s, 20s, 40s...)
3. Give up after this many attempts
4. Log error and exit

---

## Troubleshooting

### Chrome Doesn't Launch (CDP Mode)

**Symptoms:** Scraper starts, but Chrome window doesn't appear.

**Check:**
1. Is Google Chrome installed?
   - Windows: Check `C:\Program Files\Google\Chrome\Application\`
   - Mac: Check `/Applications/Google Chrome.app`
   - Linux: Run `which google-chrome`

2. Check scraper logs for "Chrome not found"

3. Try Playwright mode as fallback (edit `config.ini`):
   ```ini
   [GoogleKeepScraper]
   BrowserMode=Playwright
   ```

> Remember to restart the Docker container after changing `config.ini`.

### Chrome Opens But Scraper Can't Connect

**Symptoms:** Chrome window appears, but logs show connection errors.

**Solutions:**
1. **Wait 10-15 seconds** — Chrome needs time to start the debug server
2. **Check port 9222** — Might be in use by another program
   ```bash
   # Linux/Mac
   lsof -i :9222
   
   # Windows
   netstat -an | findstr 9222
   ```
3. **The scraper will auto-detect** an available port if 9222 is taken, but it takes longer

### Login Required Every Time

**Symptoms:** You have to log into Google Keep every time you restart.

**CDP Mode:**
- Profile should persist in `%LocalAppData%/HomeStoq/chrome-profile`
- Check the directory exists and is writable
- Make sure you're not in incognito/private mode
- Try logging in again and let it save

**Playwright Mode:**
- Profile in `browser-profile/` directory
- Check directory exists and has files after first login
- Don't delete this directory between runs

### Voice Commands Not Processing

**Symptoms:** Items appear in Google Keep but HomeStoq doesn't update.

**Diagnostic steps:**

1. **Check scraper is running:**
   ```bash
   npm run scraper
   ```
   Look for: `[INFO] Connected to Chrome via CDP`

2. **Check ActiveHours:**
   - Is it currently within the configured hours?
   - Check logs: "Outside active hours" or "Entering sleep period"

3. **Check list name:**
   ```ini
   [GoogleKeepScraper]
   KeepListName=inköpslistan
   ```
   Must match exactly (case-sensitive)

4. **Check the Keep list:**
   - Open Google Keep in browser
   - Is your voice command there?
   - Is it unchecked?

5. **Watch the logs:**
   - Do you see "Processing: [your text]"?
   - Do you see "API returned 200" or an error?

### Scraper Detected by Google

**Symptoms:** "Verify it's you" prompts, CAPTCHA, account warnings.

**Immediate actions:**
1. **Switch to CDP mode** if using Playwright (edit `config.ini`):
   ```ini
   [GoogleKeepScraper]
   BrowserMode=RemoteDebugging
   Headless=false
   ```
2. **Use a dedicated account** — Never your main Google account
3. **Reduce activity window:**
   ```ini
   [GoogleKeepScraper]
   ActiveHours=08-20
   PollIntervalSeconds=60
   PollIntervalJitterSeconds=30
   ```
4. **Ensure residential IP** — VPNs and data centers are more likely flagged

> After editing `config.ini`, restart the container: `npm run stop && npm run dev`

### Chrome Crashes / Relaunches Constantly

**Symptoms:** Logs show "Chrome process not responding" repeatedly.

**Solutions:**
1. **Increase relaunch attempts** (edit `config.ini`):
   ```ini
   [GoogleKeepScraper]
   ChromeRelaunchAttempts=10
   ```

2. **Switch to Playwright mode:**
   ```ini
   [GoogleKeepScraper]
   BrowserMode=Playwright
   ```

3. **Check system resources:**
   - Chrome needs RAM (especially on low-power devices)
   - Try closing other Chrome windows

4. **Check Chrome version:**
   - Very old Chrome may have compatibility issues
   - Update to latest version

> Remember: Always edit `config.ini` and restart the container to apply changes.

### Items Not Being Deleted After Processing

**Symptoms:** Items checked off but remain in "Completed" section.

**Check:**
1. Is the "More" menu visible? (Three dots or "Mer")
2. Is "Delete ticked items" option available? (Or "Ta bort markerade objekt")
3. Check logs for warnings about menu not found

**Language mismatch:**
- Your Google Keep UI language must match config
- Swedish Keep + Swedish config works
- English Keep + English config works
- Mixed may cause issues

> **After fixing config.ini:** Restart with `npm run stop && npm run dev`

---

## Advanced: Manual Chrome Launch

Normally the scraper launches Chrome automatically. For debugging, you can launch it manually:

### Windows

```powershell
& "C:\Program Files\Google\Chrome\Application\chrome.exe" --remote-debugging-port=9222
```

### Mac

```bash
/Applications/Google\ Chrome.app/Contents/MacOS/Google\ Chrome --remote-debugging-port=9222
```

### Linux

```bash
google-chrome --remote-debugging-port=9222
```

Then the scraper will connect to your manually launched Chrome.

---

## Log Messages Reference

| Message | Meaning |
|---------|---------|
| `Connected to Chrome via CDP` | Successfully connected in CDP mode |
| `Launched Chrome with CDP on port X` | Chrome auto-launched successfully |
| `Outside active hours (X-Y)` | Scraper sleeping until active hours |
| `Processing: [text]` | Found and processing a voice command |
| `Processed and checked: [text]` | Successfully processed and marked done |
| `Chrome connection lost, triggering recovery` | CDP disconnected, attempting reconnect |
| `Chrome process not responding. Relaunching...` | Chrome crashed, restarting |
| `Login detected! Session saved.` | Successfully logged in, session persisted |

---

## See Also

- **[Getting Started](01-getting-started.md)** — Initial setup
- **[Configuration Guide](03-configuration.md)** — All config options
- **[Development Guide](08-development.md)** — Modifying the scraper
