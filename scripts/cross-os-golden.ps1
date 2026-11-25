<#
.SYNOPSIS
  Cross-OS "golden file" check (plan Step 7.3): a file encrypted on one operating system must decrypt on every other.
.DESCRIPTION
  Used by the CI job "cross-OS golden files" in two phases:
    Encrypt: builds a fixed 3 MB sample (several 1 MB chunks, so chunk boundaries are crossed), encrypts it with the
             command-line app and writes <OutDir>/golden-<Label>.bin.enc.
    Decrypt: decrypts every *.enc in <InDir> (one per OS, downloaded from the first phase) and compares the result
             byte for byte with the same sample.
  The sample is computed, not stored, so nothing large is committed. The password is passed on standard input
  (--password-stdin), never on the command line. Needs PowerShell 7 (pwsh). Local run, both phases on this machine:
    pwsh scripts/cross-os-golden.ps1 -Mode Encrypt -Directory artifacts/golden -Label local
    pwsh scripts/cross-os-golden.ps1 -Mode Decrypt -Directory artifacts/golden
.PARAMETER Mode
  Encrypt or Decrypt.
.PARAMETER Directory
  Encrypt: where the .enc file is written. Decrypt: where the .enc files are read (searched recursively).
.PARAMETER Label
  Encrypt only: a name for the producing system (windows, linux, macos), used in the file name.
#>
param(
    [Parameter(Mandatory)][ValidateSet('Encrypt', 'Decrypt')][string]$Mode,
    [Parameter(Mandatory)][string]$Directory,
    [string]$Label = 'local'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
# Made-up sample password for this public test only: non-ASCII on purpose, so UTF-8 handling is compared across systems.
$samplePhrase = "golden-sample-p$([char]0x00E4)ss-$([char]0x00FC)nicode"
$cli = Join-Path $repo 'src/SecureFile.Cli/bin/Release/net10.0/securefile.dll'
if (-not (Test-Path $cli)) { throw "Build first: dotnet build src/SecureFile.Cli -c Release ($cli is missing)." }
New-Item -ItemType Directory -Force -Path $Directory | Out-Null
$work = Join-Path ([System.IO.Path]::GetTempPath()) ("securefile-golden-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null

# The same 3 MB on every system: byte i = (i * 31 + i / 997) mod 256.
function New-Sample([string]$path) {
    $bytes = [byte[]]::new(3 * 1024 * 1024 + 4099)
    for ($i = 0; $i -lt $bytes.Length; $i++) { $bytes[$i] = [byte](($i * 31 + [math]::Floor($i / 997)) % 256) }
    [System.IO.File]::WriteAllBytes($path, $bytes)
}

# Runs the CLI with the password on standard input; throws on a non-zero exit code.
function Invoke-Cli([string[]]$arguments) {
    $samplePhrase | dotnet $cli @arguments --password-stdin --force
    if ($LASTEXITCODE -ne 0) { throw "securefile $($arguments -join ' ') failed (exit code $LASTEXITCODE)." }
}

try {
    $sample = Join-Path $work 'sample.bin'
    New-Sample $sample
    $expected = (Get-FileHash $sample -Algorithm SHA256).Hash

    if ($Mode -eq 'Encrypt') {
        $target = Join-Path $Directory "golden-$Label.bin.enc"
        Invoke-Cli @('encrypt', $sample, '--output', $target)
        Write-Host "Encrypted on $Label -> $target"
        return
    }

    $files = @(Get-ChildItem -Path $Directory -Filter '*.enc' -Recurse)
    if ($files.Count -eq 0) { throw "No .enc files found in $Directory." }
    foreach ($file in $files) {
        $restored = Join-Path $work ($file.BaseName)
        Invoke-Cli @('decrypt', $file.FullName, '--output', $restored)
        $actual = (Get-FileHash $restored -Algorithm SHA256).Hash
        if ($actual -ne $expected) { throw "$($file.Name): decrypted bytes differ from the sample." }
        Write-Host "OK  $($file.Name) decrypts to the original sample" -ForegroundColor Green
    }
    Write-Host "All $($files.Count) golden files decrypt on $([System.Runtime.InteropServices.RuntimeInformation]::OSDescription)."
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
