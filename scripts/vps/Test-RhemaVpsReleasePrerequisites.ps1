[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$deploy = Get-Content (Join-Path $PSScriptRoot '..\Deploy-RhemaVps.ps1') -Raw
$browser = Get-Content (Join-Path $PSScriptRoot 'Test-RhemaVpsBrowserSmoke.mjs') -Raw

if ($deploy -match "Migrations\\FastBuildMigrationMetadata\.cs") {
    throw 'Release packaging still requires metadata removed from the active migration baseline.'
}
foreach ($token in @(
        'function Assert-MigrationDiscovery',
        '$id.Designer.cs',
        'Active EF migrations are missing compiled discovery metadata')) {
    if (-not $deploy.Contains($token)) {
        throw "Release migration discovery check is missing: $token"
    }
}
foreach ($token in @(
        'RHEMA_BROWSER_PATH',
        'Google\\Chrome\\Application\\chrome.exe',
        'Microsoft\\Edge\\Application\\msedge.exe',
        'for (const candidate of browserCandidates)')) {
    if (-not $browser.Contains($token)) {
        throw "Browser smoke discovery is missing: $token"
    }
}

Write-Output 'PASS: VPS release prerequisites match the merged migration baseline and supported browsers.'
