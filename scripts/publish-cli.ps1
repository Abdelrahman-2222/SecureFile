<#
.SYNOPSIS
  Builds the command-line version for Windows, Linux and macOS: one self-contained, trimmed, single-file `securefile` per system.
.DESCRIPTION
  Not part of the pre-push gate or CI. Nothing needs to be installed on the target computer (the .NET runtime is inside the
  file). Output goes to artifacts/cli/: one archive per system (.zip for Windows, .tar.gz for Linux and macOS) and a
  SHA256SUMS.txt. The executables are NOT code-signed or notarized, so macOS Gatekeeper and Windows SmartScreen will warn;
  docs/CLI_README.md (shipped inside every archive) tells people what to do. Example:
    pwsh scripts/publish-cli.ps1
    pwsh scripts/publish-cli.ps1 -Label beta2 -Runtimes win-x64,linux-x64
.PARAMETER Label
  The pre-release label in the file names (default: beta1). The version number comes from Directory.Build.props.
.PARAMETER Runtimes
  The .NET runtime identifiers to build (default: win-x64, win-arm64, linux-x64, linux-arm64, osx-arm64, osx-x64).
#>
param(
    [string]$Label = 'beta1',
    [string[]]$Runtimes = @('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-arm64', 'osx-x64')
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo

$version = ([xml](Get-Content 'Directory.Build.props')).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$root = Join-Path $repo 'artifacts/cli'
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
New-Item -ItemType Directory -Path $root | Out-Null

$sums = @()
foreach ($rid in $Runtimes) {
    $targetIsWindows = $rid.StartsWith('win')
    $name = "securefile-$version-$Label-$rid"
    $folder = Join-Path $root $name

    # Self-contained + single file + trimmed: a small download that needs nothing installed.
    dotnet publish src/SecureFile.Cli -c Release -r $rid --self-contained true `
        -p:PublishSingleFile=true -p:PublishTrimmed=true `
        -p:DebugType=none -p:DebugSymbols=false -p:PublishDocumentationFiles=false -o $folder
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $rid (exit code $LASTEXITCODE)." }

    Copy-Item 'docs/CLI_README.md' (Join-Path $folder 'README.md')

    if ($targetIsWindows) {
        $archive = Join-Path $root "$name.zip"
        Compress-Archive -Path (Join-Path $folder '*') -DestinationPath $archive -CompressionLevel Optimal
    }
    else {
        # tar keeps the file name inside a folder and is what Linux and macOS users expect (Windows 10+ ships bsdtar).
        $archive = Join-Path $root "$name.tar.gz"
        # On Windows use the system's own tar.exe: a GNU tar earlier on the PATH (Git Bash) reads "D:" as a remote host.
        $tar = if ($IsWindows) { Join-Path $env:SystemRoot 'System32\tar.exe' } else { 'tar' }
        & $tar -czf $archive -C $root $name
        if ($LASTEXITCODE -ne 0) { throw "tar failed for $rid (exit code $LASTEXITCODE)." }
    }

    $hash = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    $sums += "$hash  $(Split-Path -Leaf $archive)"
    $sizeMb = [math]::Round((Get-Item $archive).Length / 1MB, 1)
    Write-Host ("{0,-12} {1,6} MB  {2}" -f $rid, $sizeMb, (Split-Path -Leaf $archive)) -ForegroundColor Green
}

$sums | Set-Content -Path (Join-Path $root 'SHA256SUMS.txt') -Encoding ascii
Write-Host ''
Write-Host "Done. Archives and SHA256SUMS.txt are in $root" -ForegroundColor Green
$sums | ForEach-Object { Write-Host "  $_" }
