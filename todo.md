# HomeStoq Development Todo

Last updated: 2026-04-07

## ✅ Recently Completed

- **#13** - Scraper Batch Processing Fails Due to Stale Element References ✅  
  Fixed scraper only processing 1 item per poll instead of all unchecked items.  
  Changes:
  - Replaced static `for` loop with re-querying `while` loop
  - Re-query checkboxes each iteration to avoid stale element references
  - Added 50-iteration safety limit to prevent infinite loops
  - Converted CSS selectors to accessibility-based selectors (`GetByRole`)
  - Fixed `KeepListProcessor.cs` checkbox processing loop (lines 101-175)
  [View Issue](https://github.com/Christian-Gennari/HomeStoq/issues/13)

- **#12** - Frontend Refactoring: Break Up Large UI Files ✅  
  Split monolithic files into feature-based modules:
  - `i18n.js` - Externalized translations (136 lines)
  - `css/core.css` - Shared styles (~400 lines)
  - `features/inventory/` - Stock view (~250 lines CSS, ~150 lines JS)
  - `features/scan/` - Receipt scanning (~200 lines CSS, ~50 lines JS)
  - `features/receipts/` - Receipt history (~180 lines CSS, ~50 lines JS)
  - `features/chat/` - Pantry chat (~300 lines CSS, ~100 lines JS)
  - `features/shopping/` - Inköpslistor (~900 lines CSS, ~600 lines JS)
  - `js/main.js` - App orchestrator (~100 lines)
  - Backup files removed after successful testing
  [View Issue](https://github.com/Christian-Gennari/HomeStoq/issues/12)

- **#10** - i18n Unification ✅  
  Moved translations from inline `app.js` to `js/i18n.js`  
  Languages: English, Swedish (67 keys each)  
  [View Issue](https://github.com/Christian-Gennari/HomeStoq/issues/10)

- **#11** - UI Revamp: Restructure and Improve "Inköpslista" Tab ✅  
  [View Issue](https://github.com/Christian-Gennari/HomeStoq/issues/11)

- **Configuration Cleanup & Docker Hardening** ✅  
  Fixed configuration drift by making `config.ini` the single source of truth:
  - Removed hardcoded ENVs from Dockerfiles (ASPNETCORE_URLS, GOOGLEKEEPSCRAPER_*)
  - Updated PathHelper to detect Docker containers via `/.dockerenv`
  - Fixed noVNC auto-redirect to `vnc_auto.html`
  - Added `--no-sandbox` flag for Chrome in Docker (root user requirement)
  - Added Chrome installation to .NET 10 SDK base image
  - Documented Alpine vs Ubuntu Noble base image choices
  - Updated all documentation to reflect config.ini as primary configuration
  - Added 2FA troubleshooting section (prevents approval spam)

---

## 🚧 Active GitHub Issues

### 🔴 High Priority

_None remaining - all complete! 🎉_

### 🟡 Medium Priority

- **#15** - AI Resilience: Retry Logic and Model Fallback Chain  
  Implement automatic retry with intelligent fallback when primary AI model fails.  
  Includes exponential backoff, failure classification, and cross-model fallback chain.  
  Labels: `enhancement`, `ai`, `reliability`, `resilience`  
  [View Issue](https://github.com/Christian-Gennari/HomeStoq/issues/15)

- **#14** - Investigate OpenRouter Free Tier for AI Cost Optimization  
  Evaluate OpenRouter's free models as cost-effective alternative to direct Gemini API.  
  Test text-only models for Swedish support, JSON reliability, and latency.  
  Labels: `enhancement`, `ai`, `cost-optimization`, `research`  
  [View Issue](https://github.com/Christian-Gennari/HomeStoq/issues/14)

- **#6** - Historical Analytics Dashboard  
  Add "Trends" tab with consumption charts, spending analysis, and item statistics.  
  Labels: `enhancement`, `ui`, `analytics`  
  [View Issue](https://github.com/Christian-Gennari/HomeStoq/issues/6)

- **#5** - Receipt Scanner Mobile Optimizations  
  Drag-and-drop, image cropping/rotation, better mobile UX for receipt scanning.  
  Labels: `enhancement`, `ui`, `mobile`  
  [View Issue](https://github.com/Christian-Gennari/HomeStoq/issues/5)

- **#4** - Push Notifications for Voice Commands  
  Integrate with Home Assistant/Pushover/Gotify for command confirmation notifications.  
  Labels: `enhancement`, `integrations`  
  [View Issue](https://github.com/Christian-Gennari/HomeStoq/issues/4)

- **#3** - Scraper Dashboard - Health & Status Visibility  
  Real-time scraper status, last poll time, items processed, health indicators.  
  Labels: `enhancement`, `scraper`, `ui`  
  [View Issue](https://github.com/Christian-Gennari/HomeStoq/issues/3)

- **#2** - Multiple Google Account Support  
  Support multiple Google accounts and lists per scraper instance.  
  Labels: `enhancement`, `scraper`  
  [View Issue](https://github.com/Christian-Gennari/HomeStoq/issues/2)

- **#1** - Scraper Configuration UI  
  Web UI for editing scraper settings (ActiveHours, PollInterval, KeepListName).  
  Labels: `enhancement`, `scraper`, `ui`  
  [View Issue](https://github.com/Christian-Gennari/HomeStoq/issues/1)

### 🟢 Low Priority (Technical Debt)

- **#9** - Docker Compose Health Checks  
  Add `HEALTHCHECK` to API and scraper containers for monitoring.  
  Labels: `technical debt`, `devops`, `docker`  
  [View Issue](https://github.com/Christian-Gennari/HomeStoq/issues/9)

- **#8** - Playwright E2E Tests  
  E2E tests for scraper UI interactions using Playwright.  
  Labels: `technical debt`, `testing`  
  [View Issue](https://github.com/Christian-Gennari/HomeStoq/issues/8)

- **#7** - Unit Tests for GeminiService  
  Test coverage for AI parsing logic in `GeminiService`.  
  Labels: `technical debt`, `testing`  
  [View Issue](https://github.com/Christian-Gennari/HomeStoq/issues/7)

---

## 📊 Quick Reference

**Total Open Issues:** 11  
**Priority Distribution:** 0 High | 8 Medium | 3 Low  
**Recently Completed:** 4 High Priority Issues ✅

### New Frontend Structure

```
wwwroot/
├── index.html              # Main shell (464 lines, unchanged)
├── css/
│   └── core.css            # Shared styles (~400 lines)
├── js/
│   ├── i18n.js             # Translations (~150 lines)
│   └── main.js             # App orchestrator (~100 lines)
└── features/
    ├── inventory/          # Stock view
    │   ├── inventory.css   # ~250 lines
    │   └── inventory.js    # ~150 lines
    ├── scan/               # Receipt scanning
    │   ├── scan.css        # ~200 lines
    │   └── scan.js         # ~50 lines
    ├── receipts/           # Receipt history
    │   ├── receipts.css    # ~180 lines
    │   └── receipts.js     # ~50 lines
    ├── chat/               # Pantry chat
    │   ├── chat.css        # ~300 lines
    │   └── chat.js         # ~100 lines
    └── shopping/           # Inköpslistor (largest)
        ├── shopping.css    # ~900 lines
        └── shopping.js     # ~600 lines
```

### Benefits Achieved
- ✅ No file exceeds 900 lines (was 2,734 for CSS, 1,000 for JS)
- ✅ Feature co-location: all related code in one folder
- ✅ Reduced merge conflicts potential
- ✅ i18n externalized and manageable
- ✅ Clear separation of concerns
- ✅ Easier testing and maintenance

### Labels
- `enhancement` - New features
- `technical debt` - Code quality improvements
- `refactoring` - Structural changes
- `ui` - User interface changes
- `scraper` - Scraper-related
- `testing` - Test coverage
- `devops` - Deployment/ops
- `good first issue` - Beginner-friendly
- `ai` - Artificial intelligence / ML
- `reliability` - System reliability and resilience
- `resilience` - Fault tolerance and recovery
- `cost-optimization` - Reducing operational costs
- `research` - Investigation and exploration tasks

---

## Local Notes

_Add any local development notes or temporary tasks here:_

```

```
