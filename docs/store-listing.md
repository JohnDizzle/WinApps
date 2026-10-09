# WinApps — Microsoft Store listing

## Short description
Explore Windows apps, live resource usage, startup entries, packages, files, and settings.

## Description
Get a clearer view of the applications installed on your Windows PC.

WinApps brings your desktop and packaged applications into one searchable library. Find an app by name or letter, then explore its package information, settings, and local files from a focused interface.

See what is open on your PC in a horizontal carousel with app icons, window titles, and RAM snapshots. Use the mouse wheel, swipe, or arrow buttons to explore the cards. Newly detected apps appear first with a brief rainbow highlight. Switch to an open window, or select an app linked to your installed library to inspect it. The library rail reveals your selected app and available files; Back and Clear selection return you to the homepage.

Follow a selected app's live resource footprint: RAM, CPU usage, process count, and thread count, refreshed every five seconds. Initial homepage measurements carry into the overview while live sampling starts. Choose uniform Application, Package, and Files cards to show their details below.

Review Windows startup entries with available enabled/disabled status, inspect startup commands and shortcuts, and open Windows startup settings to manage what launches at sign-in.

Inspect MSIX manifests, identities, versions, dependencies, capabilities, and related resource packages. Review available runtime and AppContainer information, with explanations where an application’s framework cannot be determined conclusively.

Read supported JSON, XML, and text files, see file sizes and modification dates, and open files with their default application. Edit supported Windows ApplicationData scalar settings using controls matched to their stored types, automatic validation, and an explicit Save action.

Launch packaged applications, request administrator launch when supported by their manifests, and uninstall a selected package with confirmation. Customize the interface with light or dark theme and Mica, Acrylic, or Mica Alt backdrops.

Terminate matching processes for a selected running app after confirmation. When multiple matching processes are listed, the confirmation identifies that they will all stop. Unsaved work may be lost.

WinApps is free and open source under GPL-3.0-only.

Registry hive discovery and SQLite file identification are available; decoding registry hives and browsing database tables are not currently supported. Close an application before changing its settings because the application may overwrite them when it exits.

Homepage RAM values are snapshots; Refresh activity refreshes them. The carousel detects window changes about every two seconds and filters embedded WebView2 helpers, tool windows, overlays, and tray-only apps without a qualifying main window. Live resource matching uses executable installation paths; shared folders may include other apps, and helpers outside the folder may be missed. Startup discovery covers Windows Run registrations and startup folders, not packaged startup tasks, services, or scheduled tasks.

## Feature bullets
- Searchable, cached library of desktop and packaged applications.
- Running-app carousel with icons, RAM snapshots, horizontal scrolling, and window switching.
- Automatic detection of opened and closed app windows with a brief new-app highlight.
- Live RAM, CPU, process, and thread measurements for the selected application.
- Startup entries, available status, commands, and a link to Windows startup settings.
- Application, Package, and Files cards with clear selection feedback.
- MSIX manifest, capability, dependency, and related resource-package inspection.
- Supported typed settings editing with validation and explicit Save.
- Read-only JSON, XML, and text previews with file information.
- Package launch and confirmed uninstall actions.
- Confirmed termination of matching running processes.
- Light/dark theme and Mica, Acrylic, or Mica Alt appearance.
- Free and open source.

## Declared capability
Current Package.appxmanifest declares only the restricted runFullTrust capability. It does not declare allowElevation, broadFileSystemAccess, or a remote-management capability.

Suggested runFullTrust justification for Partner Center:

WinApps is a packaged Windows desktop utility that requires full-trust execution to discover installed desktop applications through the current user's Start Menu and registry metadata, inspect accessible installed package manifests and application data, read and update supported package settings, and invoke Windows application launch and package-management APIs. It also enumerates application windows and processes to display local RAM/CPU measurements, switches to user-selected windows, reads Windows Run registrations and startup shortcuts, and can terminate matching processes after explicit confirmation. Package removal also requires explicit confirmation. Ordinary browsing does not require administrator rights. An administrator launch request for a selected supported application is handled by Windows' normal elevation mechanism. Activity information is processed locally and is not uploaded to the maintainers.

Review the justification against the exact package submitted. Store approval is not implied by declaring this capability.

## Screenshot plan — 1920 × 1080
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
Explore running apps in a scrollable card carousel, switch to open windows, and discover newly opened apps automatically. Inspect a selected app's live RAM and CPU usage, process count, and thread count. Review startup entries and open Windows startup settings. Navigate Application, Package, and Files details with clearer selection feedback and synchronized library selection.

## Additional listing fields
- Language: English (United States).
- Suggested category: Utilities & tools (confirm the available Partner Center category).
- Search terms: Windows apps; app explorer; MSIX; startup apps; resource usage; app settings; package inspector.
- Privacy: use the published Azure URL for `privacy.html` after uploading the updated page.
- Website: use the published Azure URL for `features.html`.
- Support contact: dbadmin@fizzledbydizzlelive.onmicrosoft.com.
- Source: https://github.com/JohnDizzle/WinApps.
- Product identity and Store links: see `docs/store-identity.md`.
- Supported architectures: submit only the package architectures actually built and verified; ARM64 runtime behavior remains unverified.
- Complete age rating and pricing/availability questions in Partner Center based on the exact release. This document does not assign a rating or publish a submission.
