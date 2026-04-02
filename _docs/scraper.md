# Keep Scraper: Technical Guide

## Overview

The Keep Scraper is a C# Playwright-based automation service that monitors your Google Keep lists for voice commands. It acts as the bridge between your voice assistant (e.g., Google Nest) and the HomeStoq inventory system.

**Key Responsibilities:**
- Poll Google Keep at regular intervals for new unchecked items
- Extract item text from checklist entries
- Send parsed commands to the HomeStoq API for inventory updates
- Mark processed items as complete and delete them from the list

**Architecture:**
- Runs as a .NET `BackgroundService` (`GoogleKeepScraperWorker`)
- Uses Playwright for Chromium browser automation
- Persists browser session in `browser-profile/` directory
- Communicates with HomeStoq API via HTTP POST using `HomeStoq.Contracts`

---

## How It Works

### The Poll Cycle

```
┌─────────────────────────────────────────────────────────────────┐
│                        Poll Cycle                                │
│                                                                  │
│  1. Check if within ActiveHours                                  │
│     ├── No  → Sleep 30 min, repeat check                        │
│     └── Yes → Continue                                          │
│                                                                  │
│  2. (10% chance) Perform random behavioral activity            │
│     ├── Navigate tabs / Scroll / Hover                          │
│     └── Mimics human interaction                                │
│                                                                  │
│  3. Navigate to Google Keep                                      │
│     └── Detect if session is still valid                        │
│                                                                  │
│  4. For each monitored list:                                     │
│     ├── Open list note                                          │
│     ├── Find all unchecked checkboxes                           │
│     └── Process each item:                                      │
│          ├── Extract text                                       │
│          ├── POST to /api/voice/command                         │
│          ├── On success: Check + Delete item                    │
│          └── On failure: Log warning, skip                      │
│                                                                  │
│  5. Close list, sleep ~45s (+ jitter), repeat                   │
└─────────────────────────────────────────────────────────────────┘
```

### Item Processing Flow

When an unchecked item is found in the list:

1. **Extract**: Get the text content of the list item
2. **Parse**: POST `{ "text": "köpte 3 äpplen" }` to the API
3. **Check**: Click the checkbox to mark as done
4. **Delete**: Open "More" menu → "Delete ticked items" to remove completed items

### Multilingual Support

The scraper detects and handles both English and Swedish Google Keep interfaces:

| Element | English | Swedish |
| :--- | :--- | :--- |
| More menu button | `More` | `Mer` |
| Delete option | `Delete ticked items` | `Ta bort markerade objekt` |
| Done button | `Done` | `Stäng` |
| Sidebar: Notes | `Notes` | `Anteckningar` |
| Sidebar: Reminders | `Reminders` | `Påminnelser` |

---

## Configuration

All scraper settings are in `config.ini` under the `[Scraper]` section:

```ini
[Scraper]
# Active hours for the scraper (24-hour format). 
# The scraper only polls during these hours.
# Outside these hours, it sleeps for 30-minute intervals.
# Example: 07-23 (Scraper works from 7 AM to 11 PM).
ActiveHours=07-23

# The base interval (in seconds) between poll cycles.
# A random jitter is added to this value.
PollIntervalSeconds=45

# The maximum random jitter (in seconds) added to PollIntervalSeconds.
# Effective interval = PollIntervalSeconds ± PollIntervalJitterSeconds
PollIntervalJitterSeconds=15
```

### Related Settings

Settings in the `[Voice]` section also affect the scraper:

```ini
[Voice]
# Comma-separated list of Google Keep list names to monitor.
# The scraper will check each list in order.
KeepListName=inköpslistan, inköpslista
```

### Environment Variable Overrides

The scraper also reads these environment variables (useful for docker-compose):

| Variable | Config Equivalent | Default |
| :--- | :--- | :--- |
| `KEEP_LIST_NAME` | `Voice:KeepListName` | `inköpslistan` |
| `HOMESTOQ_API_URL` | `API:BaseUrl` | `http://localhost:5000/api/voice/command` |
| `BROWSER_PROFILE_DIR` | (internal) | `browser-profile` |
| `POLL_INTERVAL_SECONDS` | `Scraper:PollIntervalSeconds` | `45` |
| `POLL_INTERVAL_JITTER_SECONDS` | `Scraper:PollIntervalJitterSeconds` | `15` |

---

## Stealth Architecture

Google actively detects and blocks automated browser activity. The scraper implements several countermeasures to minimize the risk of account suspension.

### Browser Fingerprint Spoofing

The scraper injects a JavaScript initialization script that patches the browser environment before any page loads.

#### What gets spoofed:

| Property | Real Playwright Value | Spoofed Value | Detection Vector |
| :--- | :--- | :--- | :--- |
| `navigator.webdriver` | `true` | `undefined` | Google checks this first |
| WebGL Vendor | varies | `Google Inc. (Intel)` | Bot detectors look for "SwiftShader" |
| WebGL Renderer | varies | `ANGLE (Intel UHD Graphics 620...)` | Common real GPU string |
| `window.chrome.loadTimes()` | `undefined` | Full mock object | Chrome-specific API |
| `window.chrome.csi()` | `undefined` | Full mock object | Used by Google scripts |
| `navigator.plugins.length` | `3` (Playwright default) | `3` (with proper methods) | Uncommon plugin count |
| `navigator.hardwareConcurrency` | `2` (often) | `8` | Bot detectors flag low values |
| `navigator.deviceMemory` | `undefined` | `8` | Bot detectors flag missing value |

#### Why this matters:

Google's bot detection runs invisible JavaScript probes in the background. These probes check:
- Canvas and WebGL fingerprinting
- DOM property access patterns
- Timing anomalies between frames
- Browser automation flags and mocks

The spoofing layer makes the scraper indistinguishable from a normal Chrome user.

### Behavioral Noise

Even with perfect fingerprints, 24/7 polling with perfect timing is a bot tell. The scraper adds human-like unpredictability:

#### Active Hours
```
Example: ActiveHours=07-23

00:00 - 06:59  → Sleeping (30-min sleep cycles)
07:00 - 22:59  → Active (polling every ~45s)
23:00 - 23:59  → Sleeping
```

#### Random Delay Jitter
```
Base interval: 45s
Jitter: ±15s

Actual intervals range from 30s to 60s
```

#### Random Actions (10% chance per cycle)

1. **Tab Switching**: Navigate from Notes to Reminders and back
2. **Scrolling**: Wheel up and down by 100-500px
3. **Hovering**: Move mouse to a random note card

### Session Persistence

- Browser cookies and localStorage are saved to `browser-profile/`
- On restart, the scraper loads the existing session automatically
- If Google requires re-verification, the scraper opens a visible window for manual login
- After successful login, the session is saved for future runs

---

## Troubleshooting

### Session expired / Google requires login

**Symptoms:**
- Scraper logs: `"Session expired. Please log in again in the browser window."`
- A new browser window opens to the Google login page

**Fix:**
1. Log in to Google in the opened browser window
2. Navigate to Google Keep manually
3. The scraper will detect the login and resume polling

### Items not being deleted after processing

**Symptoms:**
- Items are checked off but remain in the list under "Completed"

**Possible causes:**
1. The "Delete ticked items" option was not found in the menu
2. The More menu button could not be located

**Fix:**
1. Check the scraper logs for warnings like `"'Delete ticked items' option not found in menu"`
2. Verify your Google Keep UI language matches the scraper's supported languages (EN/SV)
3. Try running the scraper with `Headless = false` to visually inspect the menu

### Active hours not working as expected

**Symptoms:**
- Scraper continues polling outside configured hours

**Possible causes:**
1. `ActiveHours` setting is in wrong format
2. Server time differs from local time

**Fix:**
- Ensure format is `HH-HH` (e.g., `07-23`, `22-06` for overnight)
- Check server/system timezone matches your expectation

### Scraper gets detected / blocked

**Symptoms:**
- Google shows "Verify it's you" or CAPTCHA
- Account locked temporarily

**Mitigation:**
1. Use a dedicated Google account for HomeStoq (recommended)
2. Increase `ActiveHours` to shorter window
3. Increase `PollIntervalSeconds` and `PollIntervalJitterSeconds`
4. Ensure you're running from a residential IP (not a VPN or data center)

---

## Advanced: Remote Debugging (Future)

For maximum stealth, the scraper could attach to an existing Chrome browser instead of launching its own. This would use your real browser profile, cookies, and IP address.

**Concept:**
1. Launch Chrome with: `chrome --remote-debugging-port=9222`
2. The scraper connects via WebSocket to control the existing browser
3. No new browser process is created, making automation virtually undetectable

This approach is not currently implemented but is architecturally possible with Playwright's `chromium.connectOverCDP()`.

---

## File Structure

```
src/HomeStoq.Plugins/HomeStoq.Plugins.GoogleKeepScraper/
├── GoogleKeepScraperWorker.cs    # Main BackgroundService implementation
├── Program.cs                    # Worker entry point, DI setup
└── HomeStoq.Plugins.GoogleKeepScraper.csproj

browser-profile/                  # Persisted Chromium session (created at runtime)
```
