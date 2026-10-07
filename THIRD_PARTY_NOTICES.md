# Third-party notices

The GPL-3.0-only license applies to WinApps project code. Dependencies, templates, Windows platform components, and inspected application assets retain their own licenses and ownership. This repository does not relicense those components.

## Direct dependencies

| Dependency | Version | License/terms | Upstream |
| --- | --- | --- | --- |
| Microsoft.UI.Reactor | 0.1.0-preview.12 | MIT; copyright Microsoft Corporation | [Repository](https://github.com/microsoft/microsoft-ui-reactor), [retained notice](docs/licenses/Microsoft.UI.Reactor.txt) |
| Microsoft.UI.Reactor.Devtools | 0.1.0-preview.12 | MIT; copyright Microsoft Corporation; used in Debug builds | [Repository](https://github.com/microsoft/microsoft-ui-reactor), [retained notice](docs/licenses/Microsoft.UI.Reactor.txt) |
| Microsoft.WindowsAppSDK | 2.1.3 | Microsoft Windows App SDK software license terms, including notices for its components | [Repository](https://github.com/microsoft/WindowsAppSDK), [retained terms](docs/licenses/Microsoft.WindowsAppSDK.txt) |
| System.Drawing.Common | 10.0.9 | MIT; copyright Microsoft Corporation | [Repository](https://github.com/dotnet/winforms) |

Reactor template conventions remain in the application entry/project setup. Its Microsoft copyright and MIT permission notice are retained above. Windows App SDK package distribution terms are separate from the licenses of individual open-source components.

NuGet also restores transitive dependencies. This table is an inventory of direct references, not a complete binary-distribution notice bundle. Before publishing binaries, inspect the resolved dependency graph in `obj/project.assets.json` and retain every required license, copyright, and notice from the restored packages. Dependencies may change when versions are upgraded.

## Application assets

WinApps displays application icons and metadata from the user's Windows installation. Those assets are not bundled in this source repository. Do not add proprietary application files, real settings snapshots, or personal inventories to test fixtures; use synthetic data.
