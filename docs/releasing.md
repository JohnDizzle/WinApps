# Releasing WinApps

The app bundles both the .NET runtime and Windows App SDK runtime. The project uses a root `Package.appxmanifest` and single-project MSIX tooling with the reserved WinApps Store identity.

1. Run the inspection and native UI checks, then review the app on Windows in both themes.
2. Publish the portable self-contained app:

   ```powershell
   dotnet publish WinApps.csproj --configuration Release --runtime win-x64 --self-contained true -p:WindowsPackageType=None --output artifacts/win-x64
   ```

3. Build a signed MSIX using an existing matching current-user certificate:

   ```powershell
   .\scripts\Build-Msix.ps1 -Version 1.0.0.0 -CertificateThumbprint YOUR_CERTIFICATE_THUMBPRINT
   ```

   This uses .NET MSBuild's single-project MSIX targets and requires the Windows SDK packaging tools. Each build uses a fresh directory under `artifacts/msix`. The script validates the certificate's publisher, private key, validity period, and code-signing usage. Omitting the thumbprint prefers an already trusted matching certificate, then the latest expiry. Signing does not install the app or trust the certificate.

4. For Microsoft Store submission, prepare an unsigned package:

   ```powershell
   .\scripts\Build-Msix.ps1 -Version 1.0.0.0 -Unsigned
   ```

   Microsoft Store signs MSIX submissions. Upload the package through the existing WinApps Partner Center product after checking its identity and completing submission requirements. The script does not access Partner Center or publish a release. [Microsoft signing guidance](https://learn.microsoft.com/en-us/windows/msix/package/sign-msix-package-guide)

5. Test installation and runtime behavior on a separate Windows machine. Use a higher three-part version for each release, with the fourth part zero. `-Runtime win-arm64` is supported for packaging; do not advertise ARM64 validation until tested.
6. Retain required dependency notices and provide the matching GPL source. The package includes `LICENSE`, `THIRD_PARTY_NOTICES.md`, and `docs/licenses`; review remaining dependency notices before distribution.

Build output and signing keys remain outside Git. Never commit a PFX, password, or private key. Store identifiers are recorded in [store-identity.md](store-identity.md).
