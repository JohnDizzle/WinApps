# SPDX-License-Identifier: GPL-3.0-only
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')][string]$Runtime = 'win-x64',
    [string]$Version = '1.0.0.0',
    [string]$CertificateThumbprint,
    [switch]$Unsigned
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = Split-Path $PSScriptRoot -Parent
$packageVersion = [Version]$Version
if ($packageVersion.Build -lt 0 -or $packageVersion.Revision -ne 0 -or
    $packageVersion.Major -gt 65535 -or $packageVersion.Minor -gt 65535 -or $packageVersion.Build -gt 65535) {
    throw 'Use a four-part Store version with revision zero, such as 1.0.0.0.'
}
[xml]$manifest = Get-Content (Join-Path $repo 'Package.appxmanifest') -Raw
$manifest.Package.Identity.Version = $Version
$manifest.Package.Identity.ProcessorArchitecture = $Runtime.Substring(4)
$publisher = $manifest.Package.Identity.Publisher
$certificate = $null
if (!$Unsigned) {
    $candidates = @(Get-ChildItem Cert:\CurrentUser\My | Where-Object {
        $_.Subject -eq $publisher -and $_.HasPrivateKey -and $_.NotBefore -le (Get-Date) -and
        $_.NotAfter -gt (Get-Date) -and ($_.EnhancedKeyUsageList.ObjectId -contains '1.3.6.1.5.5.7.3.3')
    })
    if ($CertificateThumbprint) {
        $certificate = $candidates | Where-Object Thumbprint -EQ ($CertificateThumbprint -replace '\s', '') | Select-Object -First 1
    } else {
        $trusted = @(Get-ChildItem Cert:\CurrentUser\TrustedPeople, Cert:\LocalMachine\TrustedPeople | Where-Object Subject -EQ $publisher | Select-Object -ExpandProperty Thumbprint)
        $certificate = $candidates | Sort-Object @{ Expression = { $trusted -contains $_.Thumbprint }; Descending = $true },
            @{ Expression = { $_.NotAfter }; Descending = $true } | Select-Object -First 1
    }
    if (!$certificate) { throw 'No valid current-user code-signing certificate with a private key matches the Store publisher. Specify -CertificateThumbprint, or use -Unsigned for Store submission.' }
}
$output = Join-Path $repo ("artifacts/msix/{0}/{1}-{2}" -f $Version, $Runtime, [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$buildManifest = Join-Path $output 'Package.appxmanifest'
$manifest.Save($buildManifest)
$arguments = @((Join-Path $repo 'MyApps.csproj'), '/restore', '/t:Build', '/p:Configuration=Release',
    "/p:Platform=$($Runtime.Substring(4))", "/p:RuntimeIdentifier=$Runtime", '/p:GenerateAppxPackageOnBuild=true',
    "/p:WinAppsPackageManifest=$buildManifest", "/p:AppxPackageDir=$output\", '/p:AppxBundle=Never',
    '/p:UapAppxPackageBuildMode=SideloadOnly', '/p:GenerateTestArtifacts=false', '/p:AppxSymbolPackageEnabled=false',
    '/p:SelfContained=true', '/p:WindowsAppSDKSelfContained=true', '/p:PublishTrimmed=false',
    "/p:AppxPackageSigningEnabled=$(!$Unsigned)")
# Supply the optional native PDB helper when Visual Studio's C++ tools are installed.
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if (Test-Path $vswhere) {
    $installation = & $vswhere -latest -products '*' -property installationPath | Select-Object -First 1
    if ($installation) {
        $tools = Join-Path $installation 'VC/Tools/MSVC'
        if (Test-Path $tools) {
            $pdbTool = Get-ChildItem $tools -Directory | Sort-Object Name -Descending | ForEach-Object {
                $candidate = Join-Path $_.FullName 'bin/Hostx64/x64/mspdbcmf.exe'
                if (Test-Path $candidate) { $candidate }
            } | Select-Object -First 1
            if ($pdbTool) { $arguments += "/p:PdbCmfx64ExeFullPath=$pdbTool"; $arguments += "/p:PdbCmfx86ExeFullPath=$pdbTool" }
        }
    }
}
if ($certificate) { $arguments += "/p:PackageCertificateThumbprint=$($certificate.Thumbprint)" }
& dotnet msbuild @arguments
if ($LASTEXITCODE -ne 0) { throw "MSIX build failed with exit code $LASTEXITCODE." }
$packages = @(Get-ChildItem $output -Recurse -File | Where-Object Extension -In @('.msix', '.appx'))
if (!$packages) { throw 'MSBuild completed without producing a package.' }
$packages | ForEach-Object { Write-Output "Package: $($_.FullName)" }
if ($Unsigned) { Write-Output 'Unsigned output: intended for Store submission or external signing.' }

if (!$Unsigned) {
    $sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
    $sdk = Get-ChildItem $sdkRoot -Directory | Where-Object {
        $_.Name -match '^10\.0\.\d+\.0$' -and (Test-Path (Join-Path $_.FullName 'x64/signtool.exe'))
    } | Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
    if (!$sdk) { throw 'SignTool was not found for package verification.' }
    foreach ($package in $packages) {
        & (Join-Path $sdk.FullName 'x64/signtool.exe') verify /pa /v $package.FullName
        if ($LASTEXITCODE -ne 0) { throw "Signature verification failed for $($package.FullName)." }
    }
}
