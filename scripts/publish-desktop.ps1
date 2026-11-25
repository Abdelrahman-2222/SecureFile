<#
.SYNOPSIS
  Builds the cross-platform desktop window (Avalonia, Stage 7) for macOS and Linux: one archive per system.
.DESCRIPTION
  Not part of the pre-push gate or CI. Self-contained (the .NET runtime is inside), trimmed (unused code removed; WPF
  cannot do this, Avalonia can) and single-file; Avalonia's two native libraries (Skia, HarfBuzz) stay next to the
  program. Output in artifacts/desktop/, one .tar.gz per system plus SHA256SUMS.txt:
    Linux:  SecureFile-<v>-<label>-linux-x64.tar.gz    -> SecureFile-.../SecureFile (+ README.md)
    macOS:  SecureFile-<v>-<label>-osx-arm64.tar.gz    -> SecureFile-.../SecureFile.app (+ README.md)
  Names start with "SecureFile-" like the Windows zips (different systems, so no clash) and differ from the
  command-line archives ("securefile-cli-..."): GitHub treats asset names as case-insensitive.
  The archives are written with real Unix permissions (scripts/lib/TarGz.ps1), so the program starts without chmod.
  NOT code-signed or notarized: macOS Gatekeeper refuses the app until the user allows it (docs/DESKTOP_README.md,
  shipped inside every archive). macOS builds made here have never run on a Mac: see docs/RELEASING.md. Example:
    pwsh scripts/publish-desktop.ps1
    pwsh scripts/publish-desktop.ps1 -Label beta2 -Runtimes linux-x64
.PARAMETER Label
  The pre-release label in the file names (default: beta1). The version number comes from Directory.Build.props.
.PARAMETER Runtimes
  The .NET runtime identifiers to build (default: linux-x64, linux-arm64, osx-arm64, osx-x64).
#>
param(
    [string]$Label = 'beta1',
    [string[]]$Runtimes = @('linux-x64', 'linux-arm64', 'osx-arm64', 'osx-x64')
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
. (Join-Path $PSScriptRoot 'lib/TarGz.ps1')

$version = ([xml](Get-Content 'Directory.Build.props')).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$root = Join-Path $repo 'artifacts/desktop'
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
New-Item -ItemType Directory -Path $root | Out-Null

# Info.plist: what macOS needs to treat the folder as an app (name, program to start, version, Retina support).
function Write-InfoPlist([string]$path) {
    @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key><string>SecureFile</string>
    <key>CFBundleDisplayName</key><string>SecureFile</string>
    <key>CFBundleIdentifier</key><string>com.abdelrahmanhatta.securefile</string>
    <key>CFBundleExecutable</key><string>SecureFile</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>$version</string>
    <key>CFBundleVersion</key><string>$version</string>
    <key>LSMinimumSystemVersion</key><string>14.0</string>
    <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
"@ | Set-Content -Path $path -Encoding utf8NoBOM
}

$sums = @()
foreach ($rid in $Runtimes) {
    $name = "SecureFile-$version-$Label-$rid"
    $folder = Join-Path $root $name
    $publish = Join-Path $root "publish-$rid"

    dotnet publish src/SecureFile.Avalonia -c Release -r $rid --self-contained true `
        -p:PublishSingleFile=true -p:PublishTrimmed=true `
        -p:DebugType=none -p:DebugSymbols=false -p:PublishDocumentationFiles=false -o $publish
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $rid (exit code $LASTEXITCODE)." }
    # SkiaSharp's Windows packages copy about 100 MB of native debug symbols; they are not part of the program.
    Get-ChildItem $publish -Filter '*.pdb' | Remove-Item

    New-Item -ItemType Directory -Path $folder | Out-Null
    if ($rid.StartsWith('osx')) {
        # An app bundle, so Finder shows one "SecureFile" icon that opens a window (a bare program opens Terminal).
        $contents = Join-Path $folder 'SecureFile.app/Contents'
        New-Item -ItemType Directory -Path (Join-Path $contents 'MacOS') -Force | Out-Null
        Copy-Item (Join-Path $publish '*') (Join-Path $contents 'MacOS')
        Write-InfoPlist (Join-Path $contents 'Info.plist')
    }
    else {
        Copy-Item (Join-Path $publish '*') $folder
    }
    Copy-Item 'docs/DESKTOP_README.md' (Join-Path $folder 'README.md')
    Remove-Item $publish -Recurse -Force

    $archive = Join-Path $root "$name.tar.gz"
    New-TarGz -ParentFolder $root -FolderName $name -Archive $archive -Executables @('SecureFile')

    $hash = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    $sums += "$hash  $(Split-Path -Leaf $archive)"
    $sizeMb = [math]::Round((Get-Item $archive).Length / 1MB, 1)
    $programMb = [math]::Round(((Get-ChildItem $folder -Recurse -File | Where-Object Name -ne 'README.md' | Measure-Object Length -Sum).Sum) / 1MB, 1)
    Write-Host ("{0,-12} archive {1,6} MB, program files {2,6} MB  {3}" -f $rid, $sizeMb, $programMb, (Split-Path -Leaf $archive)) -ForegroundColor Green
}

$sums | Set-Content -Path (Join-Path $root 'SHA256SUMS.txt') -Encoding ascii
Write-Host ''
Write-Host "Done. Archives and SHA256SUMS.txt are in $root" -ForegroundColor Green
$sums | ForEach-Object { Write-Host "  $_" }
