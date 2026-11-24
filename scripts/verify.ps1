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
# TreatWarningsAsErrors (Directory.Build.props) turns every warning into an error, so the exit code alone is the
# truth; no parsing of (language-dependent) console text.
$build = Invoke-Native 'Build' { dotnet build SecureFile.slnx -c Release }
Add-Result 'Build (warnings = errors)' ($build.ExitCode -eq 0) "exit code $($build.ExitCode)"
if ($build.ExitCode -ne 0) { Write-Host $build.Output }

# 3. Tests + coverage data ----------------------------------------------------
$testResultsFolder = Join-Path $artifactsFolder 'TestResults'
$test = Invoke-Native 'Tests' {
    dotnet test --solution SecureFile.slnx -c Release --no-build --coverage --coverage-output-format cobertura --results-directory $testResultsFolder
}
$total = if ($test.Output -match 'total:\s*(\d+)') { $Matches[1] } else { '?' }
$failed = if ($test.Output -match 'failed:\s*(\d+)') { $Matches[1] } else { '?' }
$skipped = if ($test.Output -match 'skipped:\s*(\d+)') { $Matches[1] } else { '?' }
Add-Result 'Tests' ($test.ExitCode -eq 0) "total $total, failed $failed, skipped $skipped (exit code $($test.ExitCode))"
if ($test.ExitCode -ne 0) { Write-Host $test.Output }

# 4. Coverage of SecureFile.Core ---------------------------------------------
# Fail-closed: a missing report or a value that cannot be read is a FAIL, never a silent pass.
$coverageFolder = Join-Path $artifactsFolder 'Coverage'
dotnet reportgenerator "-reports:$testResultsFolder/*.cobertura.xml" "-targetdir:$coverageFolder" `
    '-reporttypes:TextSummary;Html' '-assemblyfilters:+SecureFile.Core' | Out-Null
$summaryPath = Join-Path $coverageFolder 'Summary.txt'
$summary = if (Test-Path $summaryPath) { Get-Content $summaryPath -Raw } else { '' }
if ($summary -match 'Line coverage:\s*([\d.]+)%') {
    $lineCoverage = [double]$Matches[1]
    $branchCoverage = if ($summary -match 'Branch coverage:\s*([\d.]+)%') { [double]$Matches[1] }
                      elseif ($summary -match 'Branch coverage:\s*N/A') { 100 }   # code with no branches at all
                      else { -1 }
    $coveragePassed = $lineCoverage -ge $MinimumCoreLineCoverage -and $branchCoverage -ge $MinimumCoreBranchCoverage
    Add-Result 'Coverage (Core)' $coveragePassed "lines $lineCoverage % (min $MinimumCoreLineCoverage), branches $branchCoverage % (min $MinimumCoreBranchCoverage)"
}
else {
    Add-Result 'Coverage (Core)' $false 'coverage report missing or unreadable'
}

# 5. Vulnerable packages ------------------------------------------------------
# JSON output instead of console text, so the check does not depend on the SDK's display language.
$vulnerable = Invoke-Native 'Vulnerable packages' { dotnet list SecureFile.slnx package --vulnerable --include-transitive --format json }
try {
    $report = $vulnerable.Output | ConvertFrom-Json
    $vulnerablePackages = @(
        foreach ($project in @($report.projects)) {
            # A project with no vulnerable packages has no "frameworks" property at all.
            $frameworksProperty = $project.PSObject.Properties['frameworks']
            if (-not $frameworksProperty) { continue }
            foreach ($framework in @($frameworksProperty.Value)) {
                foreach ($listName in 'topLevelPackages', 'transitivePackages') {
                    $list = $framework.PSObject.Properties[$listName]
                    if ($list) { @($list.Value) | Where-Object { $_.PSObject.Properties['vulnerabilities'] } | ForEach-Object { $_.id } }
                }
            }
        })
    Add-Result 'Vulnerable packages' ($vulnerable.ExitCode -eq 0 -and $vulnerablePackages.Count -eq 0) $(if ($vulnerablePackages.Count) { $vulnerablePackages -join ', ' } else { 'none found' })
}
catch {
    Add-Result 'Vulnerable packages' $false "could not read the audit result: $($_.Exception.Message)"
}

# Helper: runs git grep and separates "found" (exit 0), "nothing found" (exit 1) and "git failed" (anything else).
function Invoke-GitGrep([string] $pattern, [string[]] $pathSpecs) {
    $output = git grep -I -n -P -e $pattern -- @pathSpecs 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -gt 1) { throw "git grep failed (exit $exitCode): $output" }
    return @($output | Where-Object { $_ })
}

# 6. Secrets in tracked files -------------------------------------------------
# Fast local check. CI additionally runs gitleaks over the full commit history.
Write-Host "`n=== Secrets ===" -ForegroundColor Cyan
$secretPatterns = @(
    '-----BEGIN ([A-Z]+ )?PRIVATE KEY-----',                            # private keys (PEM)
    'gh[pousr]_[A-Za-z0-9]{36,}|github_pat_[A-Za-z0-9_]{40,}',         # GitHub tokens
    'AKIA[0-9A-Z]{16}',                                                 # AWS access key id
    'xox[abposr]-[A-Za-z0-9-]{10,}',                                    # Slack tokens
    '\bsk-[A-Za-z0-9_-]{20,}',                                          # "sk-" style API keys (at a word start)
    '(?i)AccountKey=[A-Za-z0-9+/=]{20,}',                               # Azure storage connection strings
    '(?i)(api[_-]?key|client[_-]?secret|access[_-]?token|password)\s*[:=]\s*["''][^"''\s]{8,}["'']'
)
try {
    $secretHits = foreach ($pattern in $secretPatterns) { Invoke-GitGrep $pattern @('.', ':(exclude)scripts/verify.ps1') }
    Add-Result 'Secrets' (-not $secretHits) $(if ($secretHits) { ($secretHits | Select-Object -First 5) -join ' | ' } else { 'none found' })
}
catch {
    Add-Result 'Secrets' $false $_.Exception.Message
}

# 7. Forbidden code patterns (ADR-13 and security rules) -------------------------
# Each C# file is read whole (so patterns can span lines, e.g. an empty catch written over three lines) and
# comments are removed first (so documentation that mentions a pattern is not reported).
# Note: catch (Exception) and "throw ex;" are also enforced by the analyzers CA1031 and CA2200 in the build.
Write-Host "`n=== Forbidden patterns ===" -ForegroundColor Cyan
function Get-CodeWithoutComments([string] $path) {
    $text = Get-Content $path -Raw
    $text = [regex]::Replace($text, '/\*.*?\*/', '', 'Singleline')          # block comments
    return [regex]::Replace($text, '(?m)^\s*//.*$|\s//[^"\r\n]*$', '')     # line and trailing comments
}
$rules = @(
    @{ Name = 'catch (Exception) in Core/Presentation'; Pattern = 'catch\s*\(\s*(System\.)?Exception\b'; Folders = @('src/SecureFile.Core', 'src/SecureFile.Presentation') },
    @{ Name = 'empty catch block'; Pattern = 'catch\s*(\([^)]*\))?\s*(when\s*\([^)]*\)\s*)?\{\s*\}'; Folders = @('src') },
    @{ Name = 'async void outside the UI apps'; Pattern = '\basync\s+void\b'; Folders = @('src/SecureFile.Core', 'src/SecureFile.Presentation') },
    @{ Name = 'System.Random (not secure)'; Pattern = '\bnew\s+(System\.)?Random\s*\(|\bRandom\.Shared\b|\bRandom\s+\w+\s*=\s*new\s*\('; Folders = @('src') },
    @{ Name = 'whole-file reads in Core (must stream)'; Pattern = '\bFile\.(ReadAllBytes|ReadAllText|ReadAllLines|ReadAllBytesAsync|ReadAllTextAsync|ReadAllLinesAsync)\b'; Folders = @('src/SecureFile.Core') },
    @{ Name = 'console output in app code'; Pattern = '\bConsole\.(Write|WriteLine)\b'; Folders = @('src') }
)
$trackedCodeFiles = @(git ls-files -- 'src/*.cs')
foreach ($rule in $rules) {
    $hits = foreach ($file in $trackedCodeFiles) {
        $inScope = $rule.Folders | Where-Object { $file.StartsWith("$_/") }
        if ($inScope -and (Get-CodeWithoutComments $file) -match $rule.Pattern) { $file }
    }
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
