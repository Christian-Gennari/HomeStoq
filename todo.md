# HomeStoq Development Todo

Last updated: 2026-04-06

## ✅ Recently Completed

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

---

## 🚧 Active GitHub Issues

### 🔴 High Priority

- **#13** - Scraper Batch Processing Fails Due to Stale Element References  
  Scraper only processes 1 item per poll instead of all unchecked items.  
  Stale DOM references after Google Keep moves checked items to bottom.  
  Fix: Re-query checkboxes each iteration + accessibility-based selectors.  
  Labels: `bug`, `scraper`, `performance`  
  [View Issue](https://github.com/Christian-Gennari/HomeStoq/issues/13)

### 🟡 Medium Priority

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

**Total Open Issues:** 10  
**Priority Distribution:** 1 High | 6 Medium | 3 Low  
**Recently Completed:** 3 High Priority Issues ✅

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

---

## Local Notes

_Add any local development notes or temporary tasks here:_

```

```
