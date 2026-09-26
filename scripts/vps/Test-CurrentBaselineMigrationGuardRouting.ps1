[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$sourcePath = Join-Path $PSScriptRoot 'Invoke-RhemaVpsRemote.ps1'
$source = Get-Content -Raw -LiteralPath $sourcePath
$preflightStart = $source.IndexOf('function Invoke-Preflight', [StringComparison]::Ordinal)
if ($preflightStart -lt 0) {
    throw 'VPS preflight function is missing.'
}

$preflight = $source.Substring($preflightStart)
$historyIndex = $preflight.IndexOf('$history = @(Get-MigrationHistory)', [StringComparison]::Ordinal)
$decisionIndex = $preflight.IndexOf(
    '$currentBaselineId = ''20260916132000_DisposableDevelopmentCurrentModelBaseline''',
    [StringComparison]::Ordinal)
$summaryIndex = $preflight.IndexOf('$summary = @(Get-DatabaseControlSummary)', [StringComparison]::Ordinal)

if ($historyIndex -lt 0 -or $decisionIndex -le $historyIndex -or
    $summaryIndex -le $decisionIndex) {
    throw 'Current-baseline guard routing is not ordered after migration history and before control summary.'
}

$decisionBlock = $preflight.Substring($decisionIndex, $summaryIndex - $decisionIndex)
$expectedRouting = '(?s)' +
    '\$baselineApplied\s*=\s*@\(\$history\s*\|\s*Where-Object.*?' +
    'if\s*\(\$baselineApplied\)\s*\{.*?' +
    'MIGRATION_GUARDS\|SKIPPED\|\$currentBaselineId.*?' +
    '\$guards\s*=\s*@\(\).*?' +
    '\}\s*else\s*\{\s*\$guards\s*=\s*@\(Get-MigrationGuardResults\)\s*\}'
if ($decisionBlock -notmatch $expectedRouting) {
    throw 'Current baseline does not exclusively bypass archived migration data guards.'
}

if ([regex]::Matches($decisionBlock, 'Get-MigrationGuardResults').Count -ne 1) {
    throw 'Archived migration data guards must have one legacy-history execution path.'
}

Write-Host 'PASS: current disposable baseline bypasses archived migration data guards; legacy histories retain fail-fast probes.'
