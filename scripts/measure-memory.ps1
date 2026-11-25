<#
.SYNOPSIS
  Peak memory versus file size (plan Step 5.2, §8.2): encrypts and decrypts files of growing size with the command-line
  program and records the process's peak working set and the time.
.DESCRIPTION
  The target is a flat curve (under 150 MB) whatever the size, because the engine streams 1 MiB chunks. The password
  goes in through --password-stdin. Not part of the gate. Example:
    pwsh scripts/measure-memory.ps1 -WorkFolder "D:\Abelrahman\Abdelrahman Current\TestFolder" -SizesMb 1,100,1024
.PARAMETER WorkFolder
  Where the temporary files are created (deleted afterwards). Defaults to the system temp folder.
.PARAMETER SizesMb
  File sizes in MB (default 1, 100, 1024).
.PARAMETER ExePath
  The command-line exe (default: the Release build).
#>
param(
    [string]$WorkFolder = [IO.Path]::GetTempPath(),
    [int[]]$SizesMb = @(1, 100, 1024),
    [string]$ExePath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'src/SecureFile.Cli/bin/Release/net10.0/securefile.exe')
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path $ExePath)) { throw "Not found: $ExePath. Build first: dotnet build SecureFile.slnx -c Release" }
$exe = (Resolve-Path $ExePath).Path
$work = Join-Path $WorkFolder ('SecureFile-mem-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $work | Out-Null
$password = 'correct horse battery staple'

function Invoke-Measured([string]$verb, [string]$file, [string]$output) {
    $info = New-Object System.Diagnostics.ProcessStartInfo $exe
    # A single argument string works in Windows PowerShell 5.1 as well as PowerShell 7 (no ArgumentList there).
    $info.Arguments = "$verb `"$file`" --output `"$output`" --force --password-stdin"
    $info.RedirectStandardInput = $true; $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    $info.UseShellExecute = $false
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $process = [System.Diagnostics.Process]::Start($info)
    $process.StandardInput.WriteLine($password); $process.StandardInput.Close()
    $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
    $peak = 0
    while (-not $process.HasExited) {
        try { $process.Refresh(); $peak = [math]::Max($peak, $process.PeakWorkingSet64) }
        catch [System.InvalidOperationException] { break }   # the process ended between the check and the read
        Start-Sleep -Milliseconds 20
    }
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "$verb failed ($($process.ExitCode)): $($stderr.Result)" }
    [pscustomobject]@{ Seconds = $watch.Elapsed.TotalSeconds; PeakMb = [math]::Round($peak / 1MB, 1) }
}

try {
    foreach ($mb in $SizesMb) {
        $plain = Join-Path $work "p$mb.bin"
        $stream = [IO.File]::Create($plain)
        $chunk = New-Object byte[] (1MB); (New-Object Random 7).NextBytes($chunk)
        for ($i = 0; $i -lt $mb; $i++) { $stream.Write($chunk, 0, $chunk.Length) }
        $stream.Dispose()
        $enc = Invoke-Measured 'encrypt' $plain "$plain.enc"
        $dec = Invoke-Measured 'decrypt' "$plain.enc" "$plain.out"
        $same = (Get-FileHash $plain -Algorithm SHA256).Hash -eq (Get-FileHash "$plain.out" -Algorithm SHA256).Hash
        '{0,5} MB | encrypt {1,6:N1} s, peak {2,6} MB | decrypt {3,6:N1} s, peak {4,6} MB | round trip {5}' -f $mb, $enc.Seconds, $enc.PeakMb, $dec.Seconds, $dec.PeakMb, $(if ($same) { 'OK' } else { 'MISMATCH' })
        Remove-Item "$plain*" -Force
    }
}
finally { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }
