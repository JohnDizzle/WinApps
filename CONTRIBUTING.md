# Contributing to WinApps

Thanks for helping improve WinApps. Contributions are licensed under GPL-3.0-only, the same license as the project. Preserve third-party notices when reusing code.

## Development setup

Use Windows with the .NET 10 SDK and NuGet access. Clone the repository and run:

```powershell
git clone https://github.com/JohnDizzle/WinApps.git
cd WinApps
dotnet restore MyApps.csproj
dotnet run --project MyApps.csproj
```

The project uses WinUI 3 and a preview version of Microsoft.UI.Reactor. `global.json` selects an installed stable .NET 10 SDK. Keep dependency upgrades in focused changes.

## Working on a change

Discuss substantial changes in an issue first. Fork the repository, create a branch from `main`, and open a pull request to `main`. The maintainer also works directly on `main`; contributors do not need write access.

Follow [.editorconfig](.editorconfig) and the existing C# conventions. Keep components in `Browser`, immutable contracts in `Models`, and Windows/platform operations in `Services`. See [architecture](docs/architecture.md).

Use state handles (`setting.Value`, `setting.Set(...)`) in Reactor components. Keep edit drafts and save status inside each setting editor. Preserve stable component keys and native controls so unrelated navigation, focus, and scroll state survive edits.

## Validation

```powershell
dotnet build MyApps.csproj --configuration Release
dotnet run --project tests/DataInspection/DataInspection.csproj --configuration Release
git diff --check
```

If the app is running and locks the build output, build into a separate directory:

```powershell
dotnet build MyApps.csproj -o bin/Verification
```

Installed-package checks are optional local integration checks:

```powershell
dotnet run --project tests/PackageIntegration/PackageIntegration.csproj
```

Pass the exact identity names of locally installed packages after `--` to choose integration fixtures. These checks read package configuration without launching applications or changing settings. CI runs the portable inspection checks and builds WinApps; hosted runners cannot assume fixture packages are installed.

For UI changes, verify light/dark themes, alphabet button sizing, file expander widths, keyboard navigation, and setting edit focus. Include screenshots with personal information removed. For save-path changes, test exact stored types, overflow, stale snapshots, sibling preservation, and unsupported read-only values.

## Pull requests and reports

Explain the problem, the resulting behavior, and validation performed. Keep unrelated changes separate. Do not include package settings snapshots, personal files, credentials, or local app inventories. Use the issue templates for ordinary bugs and ideas; follow [SECURITY.md](SECURITY.md) for vulnerabilities.
