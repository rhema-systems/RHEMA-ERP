[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$deploy = Get-Content (Join-Path $PSScriptRoot '..\Deploy-RhemaVps.ps1') -Raw
$helper = Get-Content (Join-Path $PSScriptRoot 'Invoke-RhemaVpsRemote.ps1') -Raw
$browser = Get-Content (Join-Path $PSScriptRoot 'Test-RhemaVpsBrowserSmoke.mjs') -Raw

foreach ($source in @($deploy, $helper, $browser)) {
    if ($source -match '149\.102\.145\.190|RhemaERPHTTPSIPProxy') {
        throw 'The VPS deployment still references the retired 8443 proxy.'
    }
}
if ($deploy -notmatch "PublicBaseUrl = 'https://63\.141\.230\.56'") {
    throw 'The deployment does not default to the live Caddy HTTPS origin.'
}
if ($helper -notmatch 'Get-Service RhemaERPCaddy') {
    throw 'The VPS helper does not verify the live Caddy service.'
}
if ($deploy -match "'/health', '/health/ready', '/health/live'") {
    throw 'Public smoke must not send private loopback health routes through the frontend-only Caddy fallback.'
}
if ($deploy -notmatch "'/api/tenant'.*'/api/auth/security-settings'.*'/api/health/ready'") {
    throw 'Public smoke must verify API and browser-readiness routing through Caddy.'
}
if ($deploy -notmatch "Invoke-PublicSmoke '' -AllowConfigurationDrift") {
    throw 'Dry-run smoke does not tolerate public-origin drift that apply will reconcile.'
}
if ($deploy -notmatch 'CONFIG_DRIFT\|FRONTEND_API_ORIGIN') {
    throw 'Dry-run smoke does not report the previous compiled frontend origin as drift.'
}
if ($helper -notmatch 'CONFIG_DRIFT\|\$name\|EXPECTED=\$ExpectedPublicOrigin') {
    throw 'Preflight does not report repairable public-origin drift.'
}
if (-not $deploy.Contains(
        '$Parameters[''ExpectedPublicOrigin''] = $PublicBaseUrl.TrimEnd')) {
    throw 'The selected public origin is not passed to the remote helper.'
}

Write-Output 'PASS: VPS deployment targets the existing Caddy port-443 architecture.'
