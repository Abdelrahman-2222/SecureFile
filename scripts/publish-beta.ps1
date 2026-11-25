<#
.SYNOPSIS
  Builds the "friends build": a self-contained, single-file SecureFile.exe per Windows architecture, each in a zip with a README and a SHA-256 file.
.DESCRIPTION
  Not part of the pre-push gate or CI. Nothing needs to be installed on the friend's PC (the .NET runtime is inside the exe).
  The exe is NOT code-signed, so Windows SmartScreen and some antivirus programs may warn; the README tells friends so and
  gives them the SHA-256 to compare. The win-arm64 build is made by cross-compiling on an x64 PC; it has never been run
  (no ARM machine here), see docs/RELEASING.md. Example:
    pwsh scripts/publish-beta.ps1
    pwsh scripts/publish-beta.ps1 -Label beta2 -Runtimes win-x64
.PARAMETER Label
  The pre-release label in the file names (default: beta1). The version number comes from Directory.Build.props.
.PARAMETER Runtimes
  The .NET runtime identifiers to build (default: win-x64, win-arm64).
#>
param(
    [string]$Label = 'beta1',
    [string[]]$Runtimes = @('win-x64', 'win-arm64')
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo

# One source of truth for the version: Directory.Build.props.
$version = ([xml](Get-Content 'Directory.Build.props')).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$root = Join-Path $repo 'artifacts/beta'
$sums = @()

foreach ($rid in $Runtimes) {
    $name = "SecureFile-$version-$Label-$rid"
    $out = Join-Path $root $name
    $zip = Join-Path $root "$name.zip"

    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    if (Test-Path $zip) { Remove-Item $zip -Force }

    # Self-contained + single file: friends need nothing installed. No trimming (WPF does not support it safely).
    dotnet publish src/SecureFile.Wpf -c Release -r $rid --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=none -p:DebugSymbols=false -p:PublishDocumentationFiles=false -o $out
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $rid (exit code $LASTEXITCODE)." }

    Copy-Item 'docs/BETA_README.md' (Join-Path $out 'README.md')
    Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -CompressionLevel Optimal

    $hash = (Get-FileHash $zip -Algorithm SHA256).Hash
    "$($hash.ToLowerInvariant())  $name.zip" | Set-Content -Path "$zip.sha256" -Encoding ascii
    $sums += "$($hash.ToLowerInvariant())  $name.zip"
    $sizeMb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
    $exeMb = [math]::Round((Get-Item (Join-Path $out 'SecureFile.exe')).Length / 1MB, 1)
    Write-Host ("{0,-12} zip {1,6} MB, SecureFile.exe {2,6} MB  {3}" -f $rid, $sizeMb, $exeMb, "$name.zip") -ForegroundColor Green
}

$sums | Set-Content -Path (Join-Path $root 'SHA256SUMS.txt') -Encoding ascii
Write-Host ''
Write-Host "Friends build ready in $root. Send each zip together with its SHA-256 (also in <zip>.sha256 and SHA256SUMS.txt):" -ForegroundColor Green
$sums | ForEach-Object { Write-Host "  $_" }
