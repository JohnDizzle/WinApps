# WinApps Store identity

These values were supplied by the maintainer's Partner Center product identity screenshot. They are identifiers, not signing credentials. The project uses single-project MSIX (`WindowsPackageType=MSIX`, `EnableMsixTooling=true`). `Package.appxmanifest` now declares this Store identity, and `scripts/Build-Msix.ps1` builds the self-contained Release package.

| Field | Value |
| --- | --- |
| Product display name | WinApps |
| Package/Identity/Name | `48996JohnM.Doyle.WinApps` |
| Package/Identity/Publisher | `CN=B61100C9-E803-4D04-BE74-91A6FB862E5C` |
| Package/Properties/PublisherDisplayName | `John M. Doyle` |
| Package family name | `48996JohnM.Doyle.WinApps_jw9ac53qrz6qt` |
| Store product ID | `9NWZ6MZV4BVN` |
| Store URL | <https://apps.microsoft.com/detail/9NWZ6MZV4BVN> |
| Store protocol link | `ms-windows-store://pdp/?productid=9NWZ6MZV4BVN` |

The recorded Store link does not establish that a release is published or available. Confirm the identity in Partner Center when preparing a submission. The manifest defaults to version `1.0.0.0`, x64, application Id `App`, executable `WinApps.exe`, and the desktop `runFullTrust` capability. The packaging script supports x64 and ARM64 and overrides version/architecture for each build. ARM64 runtime behavior remains unverified.

Package family name and SID are derived identities; they are not additional `Identity` manifest attributes. No MSA client ID is configured because the app currently has no Microsoft-account authentication feature.

`Package.StoreAssociation.xml` provides the local Visual Studio Store association for WinApps. It was adapted from the maintainer's LinkScape association file with the WinApps reserved name, package identity, and product ID shown in Partner Center. The project registers it as a `StoreAssociationFile`. Reload the project before reopening Create App Packages. This supplies local association metadata; it does not repair Store API authentication or connectivity, submit a package, or confirm Store acceptance. If the wizard still requires a failed online lookup, use `scripts/Build-Msix.ps1 -Unsigned` to build locally and upload the resulting package through Partner Center.

Signing uses an existing code-signing certificate in `Cert:\CurrentUser\My` whose subject exactly matches the Store publisher, has a private key, and is currently valid. Pass `-CertificateThumbprint` to select it explicitly; otherwise the script prefers an already trusted matching certificate, then the latest expiry. It does not export the private key or change certificate trust. Use `-Unsigned` to prepare a package for Store signing; this does not submit or publish it.

For Visual Studio signing, copy `WinApps.Signing.props.example` to `WinApps.Signing.props` and set the existing certificate thumbprint. The local file is ignored by Git; the private key stays in the certificate store.
