<#
.SYNOPSIS
  Builds the "friends build": a self-contained, single-file Windows x64 SecureFile.exe in a zip with a README and a SHA-256 file.
.DESCRIPTION
  Not part of the pre-push gate or CI. Nothing needs to be installed on the friend's PC (the .NET runtime is inside the exe).
  The exe is NOT code-signed, so Windows SmartScreen and some antivirus programs may warn; the README tells friends so and
  gives them the SHA-256 to compare. Example:
    pwsh scripts/publish-beta.ps1
    pwsh scripts/publish-beta.ps1 -Label beta2
.PARAMETER Label
  The pre-release label in the file names (default: beta1). The version number comes from Directory.Build.props.
#>
param(
    [string]$Label = 'beta1'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo

# One source of truth for the version: Directory.Build.props.
$version = ([xml](Get-Content 'Directory.Build.props')).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$name = "SecureFile-$version-$Label-win-x64"
$out = Join-Path $repo "artifacts/beta/$name"
$zip = Join-Path $repo "artifacts/beta/$name.zip"

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }

# Self-contained + single file: friends need nothing installed. No trimming (WPF does not support it safely).
dotnet publish src/SecureFile.Wpf -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none -p:DebugSymbols=false -p:PublishDocumentationFiles=false -o $out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit code $LASTEXITCODE)." }

Copy-Item 'docs/BETA_README.md' (Join-Path $out 'README.md')
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -CompressionLevel Optimal

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
"$hash  $name.zip" | Set-Content -Path "$zip.sha256" -Encoding ascii
$sizeMb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
$exeMb = [math]::Round((Get-Item (Join-Path $out 'SecureFile.exe')).Length / 1MB, 1)

Write-Host ''
Write-Host "Friends build ready:" -ForegroundColor Green
Write-Host "  Zip:     $zip ($sizeMb MB; SecureFile.exe is $exeMb MB)"
Write-Host "  SHA-256: $hash"
Write-Host "  Send the zip together with the SHA-256 above (also saved in $zip.sha256)."
