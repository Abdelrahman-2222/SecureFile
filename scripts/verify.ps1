<#
.SYNOPSIS
    The quality gate: runs every automated check from PROJECT_PLAN §1.7 and fails if any check fails.

.DESCRIPTION
    Run it before every commit/push (the pre-push Git hook runs it automatically):

        pwsh -File scripts/verify.ps1

    Checks, in order:
      1. Code format            dotnet format --verify-no-changes
      2. Build                  Release build, all analyzers, warnings are errors
      3. Tests                  every test project
      4. Coverage               SecureFile.Core: line >= 90 %, branch >= 85 %
      5. Vulnerable packages    NuGet advisory database, including indirect packages
      6. Secrets                no private keys, tokens or hard-coded secrets in tracked files
      7. Forbidden patterns     ADR-13 / security rules (catch Exception, empty catch, throw ex, async void, System.Random…)
      8. AI attribution         no AI attribution lines in commit messages that are not yet pushed

.NOTES
    Exit code 0 = all checks passed; 1 = at least one check failed.
#>

[CmdletBinding()]
param(
    [double] $MinimumCoreLineCoverage = 90,
    [double] $MinimumCoreBranchCoverage = 85
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repositoryRoot

$artifactsFolder = Join-Path $repositoryRoot 'artifacts/verify'
if (Test-Path $artifactsFolder) { Remove-Item $artifactsFolder -Recurse -Force }
New-Item -ItemType Directory -Path $artifactsFolder | Out-Null

$results = [System.Collections.Generic.List[pscustomobject]]::new()

function Add-Result([string] $check, [bool] $passed, [string] $details) {
    $results.Add([pscustomobject]@{ Check = $check; Result = $(if ($passed) { 'PASS' } else { 'FAIL' }); Details = $details })
    $color = if ($passed) { 'Green' } else { 'Red' }
    Write-Host ("[{0}] {1} - {2}" -f $(if ($passed) { 'PASS' } else { 'FAIL' }), $check, $details) -ForegroundColor $color
}

function Invoke-Native([string] $title, [scriptblock] $command) {
    Write-Host "`n=== $title ===" -ForegroundColor Cyan
    $output = & $command 2>&1
    return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output | Out-String) }
}

# Tools pinned in dotnet-tools.json (ReportGenerator is used for coverage).
dotnet tool restore | Out-Null

# 1. Format -----------------------------------------------------------------
$format = Invoke-Native 'Format' { dotnet format SecureFile.slnx --verify-no-changes }
Add-Result 'Format' ($format.ExitCode -eq 0) $(if ($format.ExitCode -eq 0) { 'no changes needed' } else { 'run: dotnet format SecureFile.slnx' })

# 2. Build ------------------------------------------------------------------
$build = Invoke-Native 'Build' { dotnet build SecureFile.slnx -c Release }
$warningCount = if ($build.Output -match '(\d+) Warning\(s\)') { [int]$Matches[1] } else { -1 }
Add-Result 'Build (warnings = errors)' ($build.ExitCode -eq 0 -and $warningCount -eq 0) "exit $($build.ExitCode), warnings $warningCount"
if ($build.ExitCode -ne 0) { Write-Host $build.Output }

# 3. Tests + coverage data ----------------------------------------------------
$testResultsFolder = Join-Path $artifactsFolder 'TestResults'
$test = Invoke-Native 'Tests' {
    dotnet test --solution SecureFile.slnx -c Release --no-build --coverage --coverage-output-format cobertura --results-directory $testResultsFolder
}
$total = if ($test.Output -match 'total:\s*(\d+)') { $Matches[1] } else { '?' }
$failed = if ($test.Output -match 'failed:\s*(\d+)') { $Matches[1] } else { '?' }
Add-Result 'Tests' ($test.ExitCode -eq 0) "total $total, failed $failed"
if ($test.ExitCode -ne 0) { Write-Host $test.Output }

# 4. Coverage of SecureFile.Core ---------------------------------------------
$coverageFolder = Join-Path $artifactsFolder 'Coverage'
dotnet reportgenerator "-reports:$testResultsFolder/*.cobertura.xml" "-targetdir:$coverageFolder" `
    '-reporttypes:TextSummary;Html' '-assemblyfilters:+SecureFile.Core' | Out-Null
$summaryPath = Join-Path $coverageFolder 'Summary.txt'
if (Test-Path $summaryPath) {
    $summary = Get-Content $summaryPath -Raw
    $lineCoverage = if ($summary -match 'Line coverage:\s*([\d.]+)%') { [double]$Matches[1] } else { 0 }
    $branchCoverage = if ($summary -match 'Branch coverage:\s*([\d.]+)%') { [double]$Matches[1] } else { 100 }
    $coveragePassed = $lineCoverage -ge $MinimumCoreLineCoverage -and $branchCoverage -ge $MinimumCoreBranchCoverage
    Add-Result 'Coverage (Core)' $coveragePassed "lines $lineCoverage % (min $MinimumCoreLineCoverage), branches $branchCoverage % (min $MinimumCoreBranchCoverage)"
}
else {
    Add-Result 'Coverage (Core)' $false 'no coverage report produced'
}

# 5. Vulnerable packages ------------------------------------------------------
$vulnerable = Invoke-Native 'Vulnerable packages' { dotnet list SecureFile.slnx package --vulnerable --include-transitive }
$hasVulnerable = $vulnerable.Output -match 'has the following vulnerable packages'
Add-Result 'Vulnerable packages' (-not $hasVulnerable -and $vulnerable.ExitCode -eq 0) $(if ($hasVulnerable) { 'see output above' } else { 'none found' })
if ($hasVulnerable) { Write-Host $vulnerable.Output }

# 6. Secrets in tracked files -------------------------------------------------
Write-Host "`n=== Secrets ===" -ForegroundColor Cyan
$secretPatterns = @(
    '-----BEGIN ([A-Z]+ )?PRIVATE KEY-----',
    'gh[pousr]_[A-Za-z0-9]{36,}',
    'AKIA[0-9A-Z]{16}',
    '(?i)(api[_-]?key|client[_-]?secret|access[_-]?token)\s*[:=]\s*["''][A-Za-z0-9_\-]{16,}["'']'
)
$secretHits = foreach ($pattern in $secretPatterns) {
    git grep -I -n -P -e $pattern -- . ':(exclude)scripts/verify.ps1' 2>$null
}
Add-Result 'Secrets' (-not $secretHits) $(if ($secretHits) { ($secretHits | Select-Object -First 5) -join ' | ' } else { 'none found' })

# 7. Forbidden code patterns (ADR-13 and security rules) -------------------------
Write-Host "`n=== Forbidden patterns ===" -ForegroundColor Cyan
$rules = @(
    @{ Name = 'catch (Exception) in Core/Presentation'; Pattern = 'catch\s*\(\s*(System\.)?Exception\b'; Paths = @('src/SecureFile.Core', 'src/SecureFile.Presentation') },
    @{ Name = 'empty catch block'; Pattern = 'catch\s*(\([^)]*\))?\s*\{\s*\}'; Paths = @('src') },
    @{ Name = 'throw ex; (loses stack trace)'; Pattern = 'throw\s+[a-zA-Z_][a-zA-Z0-9_]*\s*;'; Paths = @('src') },
    @{ Name = 'async void outside the WPF app'; Pattern = 'async\s+void'; Paths = @('src/SecureFile.Core', 'src/SecureFile.Presentation') },
    @{ Name = 'System.Random (not secure)'; Pattern = '\bnew\s+Random\s*\(|Random\.Shared'; Paths = @('src') },
    @{ Name = 'whole-file reads in Core (must stream)'; Pattern = 'File\.ReadAllBytes|File\.ReadAllText'; Paths = @('src/SecureFile.Core') },
    @{ Name = 'console output in app code'; Pattern = 'Console\.Write'; Paths = @('src') }
)
foreach ($rule in $rules) {
    $hits = git grep -n -P -e $rule.Pattern -- ($rule.Paths | ForEach-Object { "$_/*.cs" }) 2>$null
    Add-Result "No $($rule.Name)" (-not $hits) $(if ($hits) { ($hits | Select-Object -First 3) -join ' | ' } else { 'clean' })
}

# 8. AI attribution in commits not yet pushed ---------------------------------------
Write-Host "`n=== Commit messages ===" -ForegroundColor Cyan
$upstream = git rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>$null
$range = if ($LASTEXITCODE -eq 0 -and $upstream) { "$upstream..HEAD" } else { 'HEAD' }
$attributionPattern = '(?i)co-authored-by:.*(claude|anthropic)|generated with.*claude|noreply@anthropic\.com'
$attributionHits = git log $range --format='%h %B' | Select-String -Pattern $attributionPattern
Add-Result 'No AI attribution in commits' (-not $attributionHits) $(if ($attributionHits) { ($attributionHits | Select-Object -First 3) -join ' | ' } else { "checked $range" })

# Summary ---------------------------------------------------------------------
Write-Host "`n=== Quality gate summary ===" -ForegroundColor Cyan
$results | Format-Table -AutoSize | Out-String -Width 200 | Write-Host
$failedChecks = @($results | Where-Object Result -eq 'FAIL')
if ($failedChecks.Count -gt 0) {
    Write-Host "QUALITY GATE FAILED: $($failedChecks.Count) check(s)." -ForegroundColor Red
    exit 1
}
Write-Host 'QUALITY GATE PASSED.' -ForegroundColor Green
Write-Host "Coverage report: $coverageFolder/index.html"
exit 0
