# Security policy

WinApps is under active development. Security fixes are made on `main`; there are no separately maintained release branches yet.

## Reporting a vulnerability

Use GitHub's **Security → Report a vulnerability** option if it is available. Otherwise, contact the maintainer through an available private method on [JohnDizzle's profile](https://github.com/JohnDizzle) to arrange confidential reporting. Do not post exploit details, credentials, settings contents, or personal application data in a public issue.

Include the commit/version, Windows version, affected operation, impact, and minimal reproduction using synthetic data. Allow time for investigation and a fix before public disclosure. No guaranteed response time is promised.

## Relevant boundaries

WinApps reads current-user package metadata and accessible files. Settings writes are explicit, limited to supported existing scalar keys, and retain their stored types. Filesystem links, oversized previews/manifests, XML DTDs, and external entities are excluded from inspection. Save operations check for stale values, but Windows ApplicationData does not provide atomic compare-and-swap.

Opening a file invokes its Windows association. Elevated launch requires a declared package capability and a validated installed executable, and uses Windows UAC. No secrets or account credentials are needed by WinApps. See the README for the scope and limits of each inspector.
