# WinApps — Microsoft Store listing

## Short description
Explore installed apps, inspect running windows, and follow live window events in one place.

## Description
Know what is installed. See what is running. Understand the windows behind your apps.

WinApps brings application discovery, live activity, window inspection, package information, and supported settings into one Windows utility. Browse your desktop and packaged apps in a searchable library, then move from an app's overview to its running windows, files, and configuration.

INSPECT RUNNING WINDOWS
Open the Window Inspector from a running app's card. See the app's icon, available publisher and version, memory use, thread count, and start time. Browse visible main windows, or include child and hidden windows when you need more detail. Expand window properties to inspect titles, classes, handles, bounds, DPI, state, and styles.

FOLLOW LIVE WINDOW EVENTS
Watch supported Windows events as they arrive, including focus, show/hide, movement, resizing, and title changes. Each row shows its time, event type, captured window title, and handle/thread context. Filter by event type, search the log, or show events for one selected window. Pause and resume the stream, turn off Follow latest to read earlier rows, and copy or export the filtered log.

EXPLORE YOUR APPLICATIONS
See open apps in a card carousel, switch to their windows, and follow a selected app's RAM, CPU usage, process count, and thread count. Review available startup entries and open Windows startup settings to manage what launches at sign-in.

UNDERSTAND PACKAGES AND FILES
Inspect available MSIX identities, manifests, capabilities, dependencies, and resource packages. Browse accessible app files, view file information, and preview supported JSON, XML, and text. Edit supported Windows ApplicationData scalar settings with type-aware controls, validation, and an explicit Save action.

MAKE IT YOUR WORKSPACE
Use Mini Mode for compact app search and access. Choose light or dark appearance and available Mica, Acrylic, or Mica Alt backdrops. Launch supported packaged apps and access confirmed package-removal and process-close actions.

Window inspection and event capture run locally without injecting code into the inspected application. Event history is bounded to the latest 2,000 retained entries. Capture starts when the inspector opens; event availability depends on Windows, application behavior, and access permissions. This is a Windows UI event viewer, not a debugger or a complete trace of an application's internal activity. Separate helper processes are not automatically included in the selected process's event stream.

Homepage RAM values are snapshots. Live resource matching may omit helpers or include applications that share an installation folder. Startup discovery covers Run registrations and startup folders. Registry hives and SQLite files can be identified; hive decoding and database-table browsing are not supported. Close an app before editing its settings, as it may overwrite changes. Closing processes may lose unsaved work.

WinApps source code is available under GPL-3.0-only.

## Feature bullets
- Searchable, cached library of desktop and packaged applications.
- Running-app carousel with icons, RAM snapshots, horizontal scrolling, and window switching.
- Automatic detection of opened and closed app windows with a brief new-app highlight.
- Window Inspector with app identity, visible/child/hidden windows, and detailed window properties.
- Streaming Windows events with captured titles, timestamps, and handle/thread context.
- Event-type, selected-window, and text filters with pause/resume and Follow latest.
- Copy and export of the filtered event log with bounded in-memory history.
- Live RAM, CPU, process, and thread measurements for the selected application.
- Startup entries, available status, commands, and a link to Windows startup settings.
- Application, Package, and Files cards with clear selection feedback.
- MSIX manifest, capability, dependency, and related resource-package inspection.
- Supported typed settings editing with validation and explicit Save.
- Read-only JSON, XML, and text previews with file information.
- Package launch and confirmed uninstall actions.
- Confirmed termination of matching running processes.
- Light/dark theme and Mica, Acrylic, or Mica Alt appearance.
- Mini Mode for compact app search and access.
- Source code available under GPL-3.0-only.

## Declared capability
Current Package.appxmanifest declares only the restricted runFullTrust capability. It does not declare allowElevation, broadFileSystemAccess, or a remote-management capability.

Suggested runFullTrust justification for Partner Center:

WinApps is a packaged Windows desktop utility that requires full-trust execution to discover installed desktop applications through the current user's Start Menu and registry metadata, inspect accessible installed package manifests and application data, read and update supported package settings, and invoke Windows application launch and package-management APIs. It also enumerates application windows and processes to display local RAM/CPU measurements, inspects window properties, and subscribes to selected-process Windows accessibility/system events using out-of-context event hooks without injecting code into target applications. Users can filter, copy, or export the captured event log. WinApps switches to user-selected windows, reads Windows Run registrations and startup shortcuts, and can terminate matching processes after explicit confirmation. Package removal also requires explicit confirmation. Ordinary browsing does not require administrator rights. An administrator launch request for a selected supported application is handled by Windows' normal elevation mechanism. Activity information is processed locally and is not uploaded to the maintainers.

Review the justification against the exact package submitted. Store approval is not implied by declaring this capability.

## Screenshot plan — 1920 × 1080
Current capture order, copy-ready captions, and supplied-image readiness are maintained in [Store screenshot captions](store-screenshot-captions.md). Use that guide for the Window Inspector release; the older sequence below describes the previous promotional set.

1. Homepage running-app carousel and startup section: "Explore open app windows and their RAM snapshots; review what starts at sign-in."
2. Selected application and live resource footprint: "Follow RAM, CPU usage, process count, and thread count for a selected app."
3. Application, Package, and Files overview cards: "Choose a card to inspect app identity, package information, and file locations."
4. Package manifest, capabilities, and resource-package relationships.
5. Supported settings editor showing harmless Boolean and numeric values.
6. LocalState file information and a supported JSON/XML preview.
7. Light-theme appearance with the theme/backdrop menu open.

Capture the real application at 1920 × 1080. Use an application with non-sensitive demo data; avoid tokens, account identifiers, personal paths, and private file contents. Do not use branded hero art as an application screenshot.

## Promotional video set

The simple 30-second intro tells the story of filtering the application library, selecting an app, inspecting its footprint, understanding its type and metadata, and finding installation locations. Separate videos cover MSIX package inspection (29 seconds), supported settings and configuration previews (27 seconds), and active applications with launch and terminate controls (27 seconds).

These are captioned screenshot tours using maintainer-provided development screenshots, with fades and silent stereo audio. They do not demonstrate recorded interactions. Error and loading screens are excluded. Settings screenshots are cropped to exclude account identifiers. Framework information is presented as inspection and clues, not guaranteed framework detection. No ACID transaction claim is made.

Each topic has a compact MP4, a Store master MP4, a PNG thumbnail, and a PNG contact sheet under `artifacts/store/intro`, `artifacts/store/packages`, `artifacts/store/settings`, or `artifacts/store/active`. Captions are embedded. The reproducible renderer is `scripts/render_store_intro.py --topic intro|packages|settings|active --ffmpeg <encoder path>`.

Intro sequence: title (0–3s), library filters (3–9s), selection and footprint (9–15s), type and metadata (15–21s), installation and data locations (21–27s), end card (27–30s). Use the generated actual-scene PNG thumbnail with each video. Recapture screenshots when the UI changes before Store submission.

## Asset requirements reference
https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images

Generated promotional art: artifacts/store/WinApps-hero-1920x1080.png
Generated square listing icon: artifacts/store/WinApps-icon-300x300.png

## What's new
New Window Inspector: open the magnifier on a running app card to explore its windows and live Windows events. See app identity and process activity, inspect main/child/hidden windows, and follow events in a table with captured window titles. Filter by event type, window, or text; pause/resume, control Follow latest, and copy or export the filtered log. Includes clearer listening states and collapsible technical details.

## Additional listing fields
- Language: English (United States).
- Suggested category: Utilities & tools (confirm the available Partner Center category).
- Search terms: window inspector; Windows events; app explorer; MSIX; startup apps; app settings; process monitor.
- Privacy: use the published Azure URL for `privacy.html` after uploading the updated page.
- Website: use the published Azure URL for `features.html`.
- Support contact: dbadmin@fizzledbydizzlelive.onmicrosoft.com.
- Source: https://github.com/JohnDizzle/WinApps.
- Product identity and Store links: see `docs/store-identity.md`.
- Supported architectures: submit only the package architectures actually built and verified; ARM64 runtime behavior remains unverified.
- Complete age rating and pricing/availability questions in Partner Center based on the exact release. This document does not assign a rating or publish a submission.
