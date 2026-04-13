# E2E Test Fixtures

## Google Keep DOM Snapshot

The file `keep-list-snapshot.html` should be placed here.

### Required DOM Elements

The snapshot must mirror the Google Keep DOM structure that the scraper relies on
(see `KeepListProcessor.cs` for the selectors used):

| Element | Selector | Notes |
|---------|----------|-------|
| List title | `GetByText(listName, { Exact: true })` | Click to open/expand the list |
| Sidebar list labels | `GetByText(listName)` | Fallback when list not in main view |
| Checkboxes (unchecked) | `AriaRole.Checkbox` | `aria-checked="false"` — the main interaction target |
| Checkboxes (checked) | `AriaRole.Checkbox` | `aria-checked="true"` — already-processed items |
| List item text | Parent→parent of checkbox | The grocery item name (e.g. "Milk", "Bread") |
| "More" / "Mer" button | `AriaRole.Button` | Opens the note menu |
| "Delete ticked items" menu | `AriaRole.Menuitem` | Text: "Delete ticked items" or "Ta bort markerade objekt" |
| "Done" / "Stäng" button | `AriaRole.Button` | Closes the expanded note |

### Inline JS Handlers

Add minimal event handlers so the harness page is interactive:

- **Checkboxes:** Toggle `aria-checked` on click
- **"More" button:** Show/hide the context menu
- **"Delete ticked items":** Remove checked checkboxes from the DOM
- **"Done" button:** Close/hide the expanded note

### Sanitization

Remove all personal data from the snapshot before committing:
- Replace real email addresses with `test@example.com`
- Replace real item names with generics like "Milk", "Bread", "Eggs"
- Remove any Google session tokens or auth cookies