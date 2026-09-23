[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$deploy = Get-Content (Join-Path $PSScriptRoot '..\Deploy-RhemaVps.ps1') -Raw
$remote = Get-Content (Join-Path $PSScriptRoot 'Invoke-RhemaVpsRemote.ps1') -Raw
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
        "'ci', '--include=dev', '--no-audit', '--no-fund'",
        'Frontend locked-dependency restore failed',
        'ReuseApiOutputFromCommit',
        'API source or build inputs changed; refusing to reuse',
        'ReuseFrontendBuildFromCommit',
        'Frontend source or build inputs changed; refusing to reuse',
        "'ci', '--omit=dev', '--ignore-scripts', '--no-audit', '--no-fund'",
        'Regular Next.js server files are missing')) {
    if (-not $deploy.Contains($token)) {
        throw "Release dependency/retry protection is missing: $token"
    }
}
foreach ($token in @(
        "'package.json', 'package-lock.json', 'next.config.js'",
        'node_modules\next\package.json',
        'Staged frontend dependency lock is missing',
        'Staged frontend Next.js configuration is missing',
        'RhemaERPFrontend must use: npm run start -- -p 3001.')) {
    if (-not $remote.Contains($token)) {
        throw "Next.js production runtime apply protection is missing: $token"
    }
}
foreach ($token in @(
        "'ALLOWED_ORIGINS' = `$ExpectedPublicOrigin",
        "'CorsSettings__AllowedOrigins__0' = `$ExpectedPublicOrigin")) {
    if (-not $remote.Contains($token)) {
        throw "Effective VPS CORS reconciliation is missing: $token"
    }
}
if (-not $deploy.Contains('but received $actualOrigin')) {
    throw 'Public CORS smoke does not report the actual returned origin.'
}
foreach ($legacy in @(
        "Join-Path `$stageFrontend 'server.js'",
        "Join-Path `$nextOutput 'standalone\server.js'")) {
    if ($remote.Contains($legacy) -or $deploy.Contains($legacy)) {
        throw "VPS release still assumes a standalone frontend runtime: $legacy"
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
