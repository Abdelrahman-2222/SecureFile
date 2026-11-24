<#
.SYNOPSIS
  Runs the large-file round-trip tests (T-27, Step 4.2): a 1 GiB and a 4.5 GiB file are encrypted, decrypted and compared.
.DESCRIPTION
  Not part of the pre-push gate or CI: it needs minutes and about 14 GB of free disk space. The tests print the time,
  speed and peak memory of each run. Example:
    pwsh scripts/large-file-check.ps1 -WorkFolder "D:\Abelrahman\Abdelrahman Current\TestFolder"
.PARAMETER WorkFolder
  Where the temporary files are created. They are deleted afterwards. Defaults to the system temp folder.
#>
param(
    [string]$WorkFolder = [IO.Path]::GetTempPath()
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $WorkFolder | Out-Null
$drive = Get-PSDrive -Name ([IO.Path]::GetPathRoot((Resolve-Path $WorkFolder)).Substring(0, 1))
$freeGb = [math]::Round($drive.Free / 1GB, 1)
if ($freeGb -lt 15) { throw "Only $freeGb GB free on the drive of '$WorkFolder'; at least 15 GB are needed." }

$env:SECUREFILE_LARGE_TESTS = '1'
$env:SECUREFILE_LARGE_DIR = (Resolve-Path $WorkFolder).Path
dotnet test --project tests/SecureFile.Integration.Tests -c Release --filter-class '*LargeFileTests' --output Detailed
$code = $LASTEXITCODE

$left = Get-ChildItem $env:SECUREFILE_LARGE_DIR -Directory -Filter 'SecureFile-large-*' -ErrorAction SilentlyContinue
if ($left) { Write-Warning "Leftover work folders (delete them): $($left.FullName -join ', ')" }
exit $code
