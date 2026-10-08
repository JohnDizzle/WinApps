# WinApps — Microsoft Store listing

## Short description
Browse Windows apps, inspect MSIX packages and local data, and edit supported app settings.

## Description
Get a clearer view of the applications installed on your Windows PC.

WinApps brings your desktop and packaged applications into one searchable library. Find an app by name or letter, then explore its package information, settings, and local files from a focused interface.

Inspect MSIX manifests, identities, versions, dependencies, capabilities, and related resource packages. Review available runtime and AppContainer information, with explanations where an application’s framework cannot be determined conclusively.

Read supported JSON, XML, and text files, see file sizes and modification dates, and open files with their default application. Edit supported Windows ApplicationData scalar settings using controls matched to their stored types, automatic validation, and an explicit Save action.

Launch packaged applications, request administrator launch when supported by their manifests, and uninstall a selected package with confirmation. Customize the interface with light or dark theme and Mica, Acrylic, or Mica Alt backdrops.

WinApps is free and open source under GPL-3.0-only.

Registry hive discovery and SQLite file identification are available; decoding registry hives and browsing database tables are not currently supported. Close an application before changing its settings because the application may overwrite them when it exits.

## Feature bullets
- Searchable, cached library of desktop and packaged applications.
- MSIX manifest, capability, dependency, and related resource-package inspection.
- Supported typed settings editing with validation and explicit Save.
- Read-only JSON, XML, and text previews with file information.
- Package launch and confirmed uninstall actions.
- Light/dark theme and Mica, Acrylic, or Mica Alt appearance.
- Free and open source.

## Declared capability
Current Package.appxmanifest declares only the restricted runFullTrust capability. It does not declare allowElevation, broadFileSystemAccess, or a remote-management capability.

Suggested runFullTrust justification for Partner Center:

WinApps is a packaged Windows desktop utility that requires full-trust execution to discover installed desktop applications through the current user’s Start Menu and registry metadata, inspect accessible installed package manifests and application data, read and update supported package settings, and invoke Windows application launch and package-management APIs. Package removal requires explicit confirmation in the interface. The app does not require administrator rights for ordinary browsing. An administrator launch request for a selected supported application is handled by Windows’ normal elevation mechanism.

Review the justification against the exact package submitted. Store approval is not implied by declaring this capability.

## Screenshot plan — 1920 × 1080
1. Searchable library and selected application overview.
2. Package manifest, capabilities, and resource-package relationships.
3. Supported settings editor showing harmless Boolean and numeric values.
4. LocalState file information and a supported JSON/XML preview.
5. Light-theme appearance with the theme/backdrop menu open.

Capture the real application at 1920 × 1080. Use an application with non-sensitive demo data; avoid tokens, account identifiers, personal paths, and private file contents. Do not use branded hero art as an application screenshot.

## Suggested video — 45 to 60 seconds
- 0–8 seconds: WinApps title and searchable library.
- 8–18 seconds: Filter, select an app, and inspect its overview.
- 18–30 seconds: Show manifest information and related packages.
- 30–42 seconds: Demonstrate a harmless typed setting and explicit Save.
- 42–52 seconds: Preview a supported text file and switch appearance.
- 52–60 seconds: End card: WinApps — Explore your Windows applications.

Use an actual still from the finished trailer as its 1920 × 1080 thumbnail.

## Asset requirements reference
https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images

Generated promotional art: artifacts/store/WinApps-hero-1920x1080.png
Generated square listing icon: artifacts/store/WinApps-icon-300x300.png
