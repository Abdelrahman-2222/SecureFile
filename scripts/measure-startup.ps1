<#
.SYNOPSIS
  Measures "time to first window" of a desktop exe (plan Step 5.2, §8.2): launch, poll until the main window is visible.
.DESCRIPTION
  Starts the exe N times, one after the other, and for each run records the milliseconds from process start until the
  process has a visible main window. The first run is reported separately: right after a reboot (or after clearing the
  file cache, e.g. with Sysinternals RAMMap "Empty Standby List") it is the COLD figure; later runs are WARM.
  Each process is closed again. Example:
    pwsh scripts/measure-startup.ps1 -ExePath artifacts/beta/SecureFile-0.1.0-beta1-win-x64/SecureFile.exe
.PARAMETER ExePath
  The exe to launch. Defaults to the Release build of the WPF project.
.PARAMETER Runs
  Number of launches (default 10).
.PARAMETER TimeoutSeconds
  Give up on one launch after this many seconds (default 20).
#>
param(
    [string]$ExePath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'src/SecureFile.Wpf/bin/Release/net10.0-windows/SecureFile.exe'),
    [int]$Runs = 10,
    [int]$TimeoutSeconds = 20
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path $ExePath)) { throw "Not found: $ExePath. Build first: dotnet build SecureFile.slnx -c Release" }
$exe = (Resolve-Path $ExePath).Path
$times = New-Object System.Collections.Generic.List[double]

for ($i = 1; $i -le $Runs; $i++) {
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $exe -PassThru
    $shown = $false
    while ($watch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $process.Refresh()
        if ($process.HasExited) { break }
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) { $shown = $true; break }
        Start-Sleep -Milliseconds 5
    }
    $ms = $watch.Elapsed.TotalMilliseconds
    if (-not $process.HasExited) { $process.CloseMainWindow() | Out-Null; if (-not $process.WaitForExit(3000)) { $process.Kill() } }
    if (-not $shown) { Write-Warning "Run $i : no window appeared (exited or timed out)."; continue }
    $times.Add($ms)
    '{0,2}: {1,7:N0} ms' -f $i, $ms
    Start-Sleep -Milliseconds 500
}

if ($times.Count -eq 0) { throw 'No successful launch.' }
$sorted = $times | Sort-Object
$median = $sorted[[int][math]::Floor(($sorted.Count - 1) / 2)]
$p95 = $sorted[[int][math]::Ceiling(($sorted.Count - 1) * 0.95)]
'First run (cold if the cache was empty): {0:N0} ms' -f $times[0]
'Median: {0:N0} ms   p95: {1:N0} ms   (n={2})' -f $median, $p95, $times.Count
