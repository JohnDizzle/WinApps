# Releasing WinApps

There are no published binary releases or signing credentials configured by this repository setup.

1. Confirm CI succeeds on the intended `main` commit.
2. Run the local installed-package checks and review the UI on Windows in light and dark themes.
3. Review dependency licenses and retain their required notices in any binary distribution.
4. Publish from a clean checkout:

   ```powershell
   dotnet publish MyApps.csproj --configuration Release --runtime win-x64 --self-contained false --output artifacts/win-x64
   ```

   This configuration bundles the Windows App SDK runtime according to the project settings, but requires the .NET 10 runtime. Validate the result on a separate Windows machine before distributing it. Do not advertise ARM64/x86 validation unless those builds were tested.

5. Include `LICENSE`, `THIRD_PARTY_NOTICES.md`, and the corresponding source for the published commit. Include the applicable dependency license/notice files from restored packages.
6. Create a version tag only when ready to release; record the commit, requirements, changes, and known limits in the release notes.

Build output, signing keys, and personal inventories must remain outside Git. Code signing and automatic release publishing are separate maintainer decisions.
