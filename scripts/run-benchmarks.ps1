<#
.SYNOPSIS
  Runs the BenchmarkDotNet suite (plan Step 5.1) and copies the markdown/CSV results to docs/quality/benchmarks.
.DESCRIPTION
  Not part of the pre-push gate or CI. Example:
    pwsh scripts/run-benchmarks.ps1 -WorkFolder "D:\Abelrahman\Abdelrahman Current\TestFolder" -Include1GB
  The work folder decides which disk the copy baseline and the encryption use; exclude it from the antivirus if the
  benchmark process is killed (an antivirus can kill programs that write and encrypt many large files).
.PARAMETER WorkFolder
  Where the temporary test files are created (deleted afterwards). Defaults to the system temp folder.
.PARAMETER Include1GB
  Also measures the 1 GB case (needs about 3 GB free on that disk and several minutes).
.PARAMETER Filter
  BenchmarkDotNet --filter expression. Default: every benchmark.
#>
param(
    [string]$WorkFolder = [IO.Path]::GetTempPath(),
    [switch]$Include1GB,
    [string]$Filter = '*'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Force $WorkFolder | Out-Null
$env:SECUREFILE_BENCH_DIR = (Resolve-Path $WorkFolder).Path
$env:SECUREFILE_BENCH_1GB = if ($Include1GB) { '1' } else { '0' }

$artifacts = Join-Path $root 'artifacts/benchmarks'
Push-Location $root
try {
    dotnet run --project benchmarks/SecureFile.Benchmarks -c Release -- --filter $Filter --artifacts $artifacts
    if ($LASTEXITCODE -ne 0) { throw "Benchmarks failed with exit code $LASTEXITCODE" }
    $target = Join-Path $root 'docs/quality/benchmarks'
    New-Item -ItemType Directory -Force $target | Out-Null
    Get-ChildItem (Join-Path $artifacts 'results') -Include '*-report-github.md', '*-report.csv' -Recurse |
        Copy-Item -Destination $target -Force
    Write-Host "Results copied to $target"
}
finally { Pop-Location }
