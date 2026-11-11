# Native AOT publishing

Release builds enable `PublishAot`; Debug and WinApps/devtools configurations keep the normal managed development loop. Native AOT requires trimming, so Release enables it and disables reflection-based JSON serialization. AOT/trim warnings are not suppressed; IL2026 and IL3050 are errors and native/linker analysis warnings fail publishing.

Use a Windows Developer PowerShell with the Visual Studio C++ native tools and Windows SDK installed:

```powershell
.\scripts\Publish-NativeAot.ps1
```

This publishes the x64 native application and supporting WinUI assets to `artifacts/native-aot/win-x64`. It explicitly disables MSIX creation and upload, including local Visual Studio package preferences. Keep the complete output folder together; native compilation does not remove the need for the Windows App SDK's native DLLs and PRI resources.

The script runs the published executable with `--aot-self-check` on an x64 host. That command refuses a managed/JIT executable, checks source-generated JSON, creates/reads/deletes a temporary Shell shortcut without launching it, and exercises native window enumeration. It does not open the WinApps UI or inspect/save the user's library/settings. Run it again with:

```powershell
$check = Start-Process .\artifacts\native-aot\win-x64\WinApps.exe --aot-self-check -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput .\artifacts\aot-check.log
Get-Content .\artifacts\aot-check.log
$check.ExitCode
```

Expect a `Native AOT verified` report and exit code zero. Use an explicit wait because PowerShell can otherwise return immediately when starting a Windows GUI executable. The publishing script handles this automatically.

For ARM64, pass `-Runtime win-arm64` and validate the published executable on ARM64 Windows. ARM64 behavior has not been verified here. Native AOT x86 publishing is not supported by this workflow.

## What changed

- JSON caches, window preferences, history, UI resources, and known setting arrays use source-generated metadata or `Utf8JsonWriter`.
- Start Menu reading and shortcut creation use CsWin32-generated `IShellLinkW` / `IPersistFile` interfaces instead of `WScript.Shell`, `dynamic`, or runtime COM wrappers.
- CsWin32 is configured without runtime marshalling. Native callbacks use `UnmanagedCallersOnly` entry points with explicit lifetime management.
- Inspector rows use `GeneratedBindableCustomProperty` metadata for runtime XAML bindings.
- Reactor core, Advanced (Markdown), and development tools use matching preview.17 packages. This replaces preview.12's Native AOT-incompatible unpackaged Jump List COM path and includes the publish-resource copying fix.
- Reactor UI lifecycle, pooling, visibility, and modifier diagnostics are enforced as errors for Browser sources. UI subscriptions use declarative event modifiers or effect cleanup; reconciled properties use the DSL. See [UI lifecycle conventions](architecture.md#reactor-lifecycle-and-ui-properties).

## Build, publish, and package are different

A successful Visual Studio Rebuild or creation of an MSIX is not proof that native compilation ran. Look for `Generating native code` in the publish log and run the native self-check against the exact output.

`scripts/Build-Msix.ps1` now requests the Publish pipeline with AOT enabled and packaging after Publish, rather than packaging a managed Build output. This packaging-path change has not been exercised here; no MSIX was built as part of this migration. Validate the exact installed package, including startup, Mini Mode, Jump Lists, window events, settings, and shortcuts, before shipping. The previously reported Microsoft Store slowdown still needs its separate runtime retest.
