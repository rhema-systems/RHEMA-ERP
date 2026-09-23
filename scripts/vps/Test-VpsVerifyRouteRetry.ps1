[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$sourcePath = Join-Path $PSScriptRoot 'Invoke-RhemaVpsRemote.ps1'
$source = Get-Content -Raw -LiteralPath $sourcePath

$probeStart = $source.IndexOf('function Invoke-LocalRouteWithRetry', [StringComparison]::Ordinal)
$verifyStart = $source.IndexOf('function Invoke-Verify', [StringComparison]::Ordinal)
if ($probeStart -lt 0 -or $verifyStart -le $probeStart) {
    throw 'Retrying local-route probe must be declared before VPS verification.'
}

$probe = $source.Substring($probeStart, $verifyStart - $probeStart)
foreach ($requiredToken in @(
        '[DateTime]::UtcNow.AddSeconds($DeadlineSeconds)',
        '-TimeoutSec $AttemptTimeoutSeconds',
        'Start-Sleep -Seconds 3',
        'Last failure: $lastFailure')) {
    if ($probe.IndexOf($requiredToken, [StringComparison]::Ordinal) -lt 0) {
        throw "Local-route retry probe is missing: $requiredToken"
    }
}

$verify = $source.Substring($verifyStart)
$routeOrder = @(
    "Name = 'api-live'",
    "Name = 'api-ready'",
    "Name = 'api-health'",
    "Name = 'frontend-login'"
)
$priorIndex = -1
foreach ($route in $routeOrder) {
    $index = $verify.IndexOf($route, [StringComparison]::Ordinal)
    if ($index -le $priorIndex) {
        throw 'VPS verification must check liveness, readiness, aggregate health, then frontend login.'
    }
    $priorIndex = $index
}

if ([regex]::Matches($verify, 'Invoke-LocalRouteWithRetry').Count -ne 1 -or
    $verify -match 'Invoke-WebRequest\s+\$item\.Uri') {
    throw 'VPS verification must route all local checks through the bounded retry helper.'
}

Write-Host 'PASS: VPS verification retries bounded route probes in dependency-safe order and retains endpoint-specific failures.'
