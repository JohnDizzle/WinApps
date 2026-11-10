# SPDX-License-Identifier: GPL-3.0-only
[CmdletBinding()]
param([ValidateSet('win-x64', 'win-arm64')][string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repo "artifacts/native-aot/$Runtime"
$arguments = @('publish', (Join-Path $repo 'WinApps.csproj'), '-c', 'Release', '-r', $Runtime,
    '-p:PublishAot=true', '-p:WindowsPackageType=None', '-p:EnableMsixTooling=false',
    '-p:GenerateAppxPackageOnBuild=false', '-p:PublishAppxPackage=false', '-p:AppxPackage=false',
    '-p:UapAppxPackageBuildMode=SideloadOnly', '-o', $output)
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw "Native AOT publish failed with exit code $LASTEXITCODE." }
Copy-Item -LiteralPath (Join-Path $repo 'Assets/WinApps.ico') -Destination (Join-Path $output 'Assets/WinApps.ico') -Force
if ($Runtime -eq 'win-x64') {
    $standardOutput = Join-Path $repo "artifacts/native-aot/$Runtime-self-check.stdout.log"
    $standardError = Join-Path $repo "artifacts/native-aot/$Runtime-self-check.stderr.log"
    # WinExe applications otherwise return immediately from an interactive
    # PowerShell session, leaving LASTEXITCODE from a previous command.
    $check = Start-Process -FilePath (Join-Path $output 'WinApps.exe') -ArgumentList '--aot-self-check' -Wait -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput $standardOutput -RedirectStandardError $standardError
    $report = Get-Content $standardOutput -Raw
    Write-Output $report
    if ($check.ExitCode -ne 0 -or $report -notmatch 'Native AOT verified;') {
        throw "The published executable failed its Native AOT runtime self-check. See $standardError"
    }
}
else {
    Write-Warning 'ARM64 output was published but not runtime-verified. Run --aot-self-check on the target Windows machine.'
}
Write-Output "Native AOT output: $output"
Write-Output 'No MSIX was built or uploaded. ARM64 runtime checks require an ARM64 Windows machine.'
