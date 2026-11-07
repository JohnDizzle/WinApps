# Store screenshots and captions — November 2026

## Capture requirements

Capture the actual shipping UI as PNG, ideally at 1920 × 1080. Microsoft's desktop minimum is 1366 × 768, with each screenshot below 50 MB. Provide at least four screenshots for a useful product tour; up to ten desktop screenshots are supported. Captions must be 200 characters or fewer. Keep marketing text separate from the screenshot: Microsoft's guidance says not to add logos, icons, or marketing overlays.

Source: [Microsoft Store screenshot guidance](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images).

Maximize the relevant WinApps window on a suitable display, then use Alt+Print Screen or Snipping Tool's Window capture. Check the saved PNG dimensions. Use harmless demo data and avoid private project names, usernames, account details, credentials, or unrelated desktop windows. Do not stretch a smaller image to meet the minimum.

## Recommended order and copy-ready captions

| Order | Filename | Scene to capture | Store caption |
|---|---|---|---|
| 1 | 01-app-library.png | Main WinApps library with running-app cards; choose a clean selection and show the magnifier action. | Find installed apps, see what is running, and open an app's overview or Window Inspector from its activity card. |
| 2 | 02-live-window-events.png | Inspector with a visible main window selected, All app windows, All event types, and several readable events. Keep details collapsed. | Follow live Windows events with captured window titles. Filter by event type, window, or text, then copy or export the log. |
| 3 | 03-window-properties.png | Same app, Window & process details expanded; show a representative main window rather than an obscure hidden helper. | Inspect window titles, handles, classes, bounds, DPI, and styles alongside app identity and live process information. |
| 4 | 04-event-filtering.png | Inspector showing Focus or Location changed selected in Event type, with matching events and a readable title. | Focus on the activity that matters: show one event type or selected window while retaining the captured history. |
| 5 | 05-package-details.png | Selected packaged app with identity, capabilities, or dependencies visible. | Explore available package identities, manifests, capabilities, dependencies, and related resource packages. |
| 6 | 06-settings-and-files.png | Harmless supported setting or JSON/XML preview; no tokens or personal configuration. | Preview supported configuration files and edit supported scalar settings with validation and an explicit Save action. |
| 7 | 07-mini-mode.png | Mini Mode with non-sensitive suggested apps or a useful search result; capture at a compliant size without adding mock UI. | Keep app search and access close at hand with WinApps Mini Mode. |

Screenshots 1–4 tell the core discovery and inspection story. Screenshots 5–7 are optional additions. Use separate light/dark captures if they demonstrate the product better than a duplicate feature scene. A compact Mini Mode window may be too small for the desktop screenshot minimum; prefer another full-size view rather than enlarging pixels or fabricating a background.

## Supplied references

Two unchanged user-supplied captures are retained in `artifacts/store/listing-2026-11/references`:

- `01-window-inspector-visual-studio.png`: 1086 × 843. Good reference for app branding, window picker, and event table. The selected project/file name is visible; use a deliberate demo title for the final capture.
- `02-window-inspector-codex.png`: 1086 × 843. Demonstrates child-window events. Internal Chromium titles may be less clear to a first-time customer; use as an optional advanced scene.

Both are reference images, **not upload-ready desktop screenshots**, because their width is below Microsoft's minimum. No current full-resolution screenshots were captured in this task. Older screenshots under `artifacts/store/*/screenshots` may show an earlier UI and should be recaptured before submission.

## Submission notes

- Description and feature copy: `docs/store-listing.md`.
- Do not describe this paid listing as free. GPL source availability and the Store purchase price are separate facts.
- Avoid claims of full debugging, all-event capture, guaranteed framework detection, system-wide process capture, or database-table browsing.
- Upload captions separately in Partner Center. This preparation does not publish a listing or build a package.
