# WinApps

[![CI](https://github.com/JohnDizzle/WinApps/actions/workflows/ci.yml/badge.svg)](https://github.com/JohnDizzle/WinApps/actions/workflows/ci.yml)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)

WinApps lets you browse installed Windows applications, inspect their package configuration and local files, edit supported settings, and launch packaged apps.

WinApps is in active development. Release builds are self-contained, including both the .NET and Windows App SDK runtimes. See [the release procedure](docs/releasing.md) for portable publishing and Store-identity MSIX packaging.

## Features

- Cached application discovery, search, alphabet navigation, and application icons.
- App files embedded beneath the selected application, with file information and default-app opening.
- Manifest configuration, declared runtime/trust, dependencies, capabilities, and related resource packages.
- Typed scalar settings editors with explicit Save, automatic storage-type validation, and stale-value detection.
- Read-only JSON/XML inspection and bounded text previews.
- Package launch actions, with UAC elevation when declared and supported, and confirmed current-user package uninstall.
- Windows app-management settings access for repair, reset, and uninstall where supported.
- Registry hive discovery and SQLite signature identification, with inspection limits shown.
- Mica window material, sun/moon theme toggle and separately mounted navigation, details, and setting editors.
- Display-error recovery with retry, restart, and collapsed technical details.

## Build and run

Building from source requires Windows, a stable .NET 10 SDK, and NuGet access. Visual Studio uses the MSIX launch profiles and root `Package.appxmanifest`; the command below runs the portable development variant.

```powershell
git clone https://github.com/JohnDizzle/WinApps.git
cd WinApps
dotnet restore MyApps.csproj
dotnet run --project MyApps.csproj -p:WindowsPackageType=None
```

Run the portable checks with:

```powershell
dotnet run --project tests/DataInspection/DataInspection.csproj
```

The first launch scans the current user's installed applications. Later launches use `%LOCALAPPDATA%\MyApps\library.json`; Refresh applications updates the cache. File inventory, settings, and package configuration are read live when an app is selected.

## Documentation and participation

- [Inspection behavior and limits](docs/inspection.md)
- [Architecture and directory structure](docs/architecture.md)
- [Application selection and navigation diagram](docs/navigation-flow.md)
- [Contributing and validation](CONTRIBUTING.md)
- [Release procedure](docs/releasing.md)
- [Reserved Store identity](docs/store-identity.md)
- [Security reporting](SECURITY.md)
- [Code of conduct](CODE_OF_CONDUCT.md)
- [Report a bug or request a feature](https://github.com/JohnDizzle/WinApps/issues/new/choose)

SQLite adapters and selected-file backup remain planned work. WinApps does not decrypt secrets or retrieve PasswordVault entries. Close a target app before editing its settings; it may overwrite changes when it exits.

## License

Copyright (c) 2026 John M. Doyle (JohnDizzle) and WinApps contributors.

WinApps project code is licensed under **GPL-3.0-only**. See [LICENSE](LICENSE). Third-party dependencies retain their own terms and notices; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Application names, logos, and settings inspected on your computer belong to their respective owners and are not distributed by this repository.
