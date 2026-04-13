# E2E Test Fixtures

## Google Keep DOM Harness

`keep-list-snapshot.html` is a self-contained, deterministic harness page that mirrors the Google Keep DOM structure the scraper relies on.

### Two-List Structure

The harness includes both lists matching the real Google Keep setup:

| List Name | Unchecked Items | Checked Items |
|-----------|----------------|---------------|
| `inköpslista` | Milk, Bread, Eggs, Coffee | Rice |
| `inköpslistan` | Potatoes, Feta cheese | Butter |

### DOM Selectors (Matching KeepListProcessor.cs)

| Element | Selector | Harness Implementation |
|---------|----------|----------------------|
| List title (card) | `GetByText(listName, { Exact: true })` | `div.IZ65Hb-YPqjbf` with exact text |
| List title (sidebar) | `GetByText(listName)` fallback | `div[role="tab"][data-list-name]` |
| Checkboxes | `AriaRole.Checkbox` | `div.Q0hgme-MPu53c[role="checkbox"][aria-checked]` |
| Item text | Parent→parent→of checkbox → `span` | `span.vIzZGf-fmcmS` (same class as real Keep) |
| "More" button | `AriaRole.Button`, `aria-label="More"` | `button[aria-label="More"]` |
| "Delete ticked items" | `AriaRole.Menuitem` | `div[role="menuitem"]` — EN + SV variants |
| "Close" button | `AriaRole.Button`, text "Close" | `button.close-btn[aria-label="Close"]` |

### Interactive JS Handlers

- **Checkbox click**: Toggles `aria-checked` between `"true"`/`"false"`, applies strikethrough + gray styling on checked items
- **List title click** (card or sidebar): Opens expanded dialog view with checklist
- **"More" button click**: Toggles context menu visibility
- **"Delete ticked items" click**: Removes all `aria-checked="true"` items from DOM, hides menu, syncs back to card view
- **"Close" button click**: Closes expanded dialog, syncs changes back to card view
- **Backdrop click**: Closes expanded dialog

### Session Expiration Simulation

Call `window.simulateSessionExpiry()` from a test to redirect to `accounts.google.com` — this simulates the expired session scenario described in issue #8.

### Serving the Harness

```bash
# Python
python3 -m http.server 8080 --directory e2e/fixtures

# Node
npx serve e2e/fixtures -l 8080
```

Then navigate to `http://localhost:8080/keep-list-snapshot.html`.

### Sanitization Note

All personal data has been replaced:
- Email: `test@example.com`
- Real item names replaced with generic English items
- No Google session tokens, auth cookies, or profile image URLs
- All Google JS framework code replaced with minimal inline handlers