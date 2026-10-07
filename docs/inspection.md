# Inspection behavior

## Recovery scope

The app reads package metadata, opens accessible data folders, and reads packaged app settings through Windows ApplicationData. Supported existing scalar settings can be edited and saved explicitly with their original Windows types. It does not decrypt secrets or retrieve PasswordVault entries.

Planned modules: known-format SQLite adapters; selected-file backup with a recovery manifest. Encrypted formats require app-specific, documented recovery support.

JSON and XML/configuration text previews validate and format complete files within the 64 KiB preview limit. Invalid documents show a diagnostic followed by the original text. Truncated files remain raw text and are not parsed. JSON nesting is limited to 64 levels; XML DTDs and external entity resolution are disabled. Inspection does not modify source files, and XML whitespace is preserved.

Package details include InstalledLocation from PackageManager. Measure footprint scans installed package files and Settings, LocalState, RoamingState, LocalCache, AC, and TempState. Sizes are logical file bytes, not allocated disk space; shared dependencies are excluded. Reparse points are not followed. Inaccessible or disappearing entries are reported as partial results. Other AppData/ProgramData folders, registry settings, and unpackaged applications are not yet discovered.

Desktop app discovery reads HKCU/HKLM uninstall registrations in both registry views. It displays source, InstallLocation, and installer EstimatedSize where supplied. Missing locations remain unknown; portable/unregistered apps and desktop app data outside a known package folder need additional discovery adapters. Registry metadata may be stale. Desktop install folder measurements may include shared files and are not exclusive per-app disk usage.

## Application library and disk inspector

Applications appear in an alphabetical tree under Packaged apps and Desktop apps. Packaged framework/resource packages and packages without application-list entries are excluded. Desktop uninstall entries marked SystemComponent, child updates, and known runtime/SDK components are filtered. This is best-effort classification; some vendor supporting components may still appear.

The detail pane provides Overview, Storage, Settings, and LocalState sections. Selecting an application inventories accessible files under its package Settings and LocalState directories, including nested paths, logical file sizes, modification timestamps, and format hints. Selecting a supported text file loads a read-only preview capped at 64 KiB. settings.dat is identified as a binary Windows settings store; decoded settings are read through Windows ApplicationData; database queries are not implemented. Inventory is capped at 2,000 files and reports unavailable paths. Filesystem links are skipped. Desktop apps still require adapters to identify persistent data locations.

## Decoded Windows settings

Selecting an application loads LocalSettings and RoamingSettings using Windows.Management.Core.ApplicationDataManager.CreateForPackageFamily for the current Windows user. Nested containers and composite values are shown with their stored types and values. Access failures appear in the Settings view.

Existing scalar settings show their stored type and an explicit Save button, enabled when a valid value changes. Booleans use a True/False toggle; numbers use native NumberBox controls. Int64/UInt64 use an exact numeric text field with increment/decrement buttons to preserve integers beyond Double precision. Supported types are String, Boolean, Byte, Int16/UInt16, Int32/UInt32, Int64/UInt64, Single, Double, Char, Guid, DateTimeOffset, and TimeSpan. Numeric parsing uses invariant culture and enforces the stored type's range; non-finite floating-point values are rejected. DateTimeOffset uses round-trip (O) format and TimeSpan uses constant (c) format. Arrays, composite values/members, and unsupported types remain labeled read only. Saving updates only an existing key in its exact LocalSettings/RoamingSettings container, without creating containers or changing its type. Before assignment, the writer checks that the current type and formatted value match the original edit snapshot, and then reads back the saved value. The Windows settings API does not provide an atomic compare-and-swap, so close the target app before editing to reduce concurrent writes. It may overwrite settings at exit, and changes may take effect on its next launch. Reload settings discards all drafts. Editing and saving a setting updates its own component state; it does not update the application rail or replace the detail pane. No changes are written until Save is clicked. Application-specific value constraints beyond the storage type are not validated.

The selected application's list row contains an App files expander with Settings and LocalState file nodes. Selecting an app collapses Open Apps List (the alphabet filter) and expands its file browser. Opening either expander collapses the other to keep navigation compact. Refreshing the library restores the alphabet filter. File rows provide both read-only inspection and Open with default app, which launches the Windows file association; the external application may allow editing. Missing files or association failures are reported in the status message.

One theme button lives in the title bar: the sun switches to light and the moon switches to dark. Startup follows the system theme until toggled. Controls use rounded surfaces, Windows theme colors, and native interaction states.

## MSIX package configuration

Selecting a packaged app reads its installed AppxManifest.xml and current-user Windows package registration. Overview summarizes application runtime and trust; the Package tab and embedded manifest node expose identity, architecture, signature kind, package flags, resolved dependencies, target device families, capabilities, resources, all application declarations, and app/package extensions. Every top-level manifest section and the complete original manifest are available as read-only text, including fields not specially interpreted by MyApps. Manifest reads are bounded to 2 MiB characters / 4 MiB bytes, reject filesystem links, and prohibit DTDs/external entities.

RuntimeBehavior and TrustLevel are shown separately. Legacy EntryPoint combinations are labeled as inferred; missing declarations or an unmatched application Id remain unknown. WinUI 3 is not established by MSIX packaging or a Windows App SDK dependency alone. Capabilities are declarations, not granted permissions, and extension activation may override application defaults. Runtime process tokens and binary framework usage are not inspected. Configuration is read live on selection; it is not stored in the library cache.

The Package view also correlates installed resource packages through the current-user Windows package-family query with the Resource type filter. Resource packs remain beneath their parent application's configuration rather than becoming independent application rows. Each pack shows its resource Id, full identity, version, architecture, publisher, signature kind, package health, installation path, and whether its version matches the selected library entry. Its own installed AppxManifest.xml is inspected with the same bounds and read-only protections as the main manifest; language/scale declarations, other sections, and the complete source are available. Missing manifests and discovery failures are reported independently so that accessible main/resource results remain visible. Resource-only manifests do not require an Application element. Inspection is capped at 128 resource packages, with a notice when limited. These are installed same-family relationships, not a determination of active resource selection or packs available for download.

Selecting Settings/settings.dat refreshes the live decoded package settings. This does not parse arbitrary offline .dat files: LocalState .dat files can use application-specific formats. Their file information remains available, and supported text files have read-only previews.

WinApps discovers desktop applications from registered installations and Start Menu shortcuts. Packaged applications appear with their own names and icons.

Use Launch beside the selected application's heading to open a registered packaged app. Launch elevated appears when its package declares supported elevation; Windows then requests administrator approval. Launch is unavailable for desktop entries without a known package activation identity.

## Virtual registry and databases

The Registry view inventories per-user stores under `SystemAppData/Helium` and known registry baseline files in the installed package. Baselines and per-user changes are shown separately. Hive files are identified by their signature where readable; keys and values are not decoded or editable. These stores are independent of the ApplicationData settings editor.

The package view reports declared registry/filesystem virtualization options and exclusions. An absent override means Windows runtime defaults apply; it does not establish that no private registry exists. A manifest does not enumerate every settings file or database.

LocalState files with a SQLite signature are identified even without a database extension. Other database extensions are marked as unverified. Database tables are not inspected or editable; database adapters remain planned work. Discovery reads metadata and file headers without modifying files.
