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

# Execute the real discovery guard against every active migration, rather than
# merely checking that the guard's source exists. Load only its pure functions;
# never invoke the deployment script's build, database or service actions.
$RepositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$parseTokens = $null
$parseErrors = $null
$deployAst = [Management.Automation.Language.Parser]::ParseInput($deploy, [ref]$parseTokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw 'Deployment script does not parse.' }
foreach ($functionName in @('Assert-True', 'Get-LocalMigrationIds', 'Assert-MigrationDiscovery')) {
    $functionNode = $deployAst.Find({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $functionName
    }, $true)
    if ($null -eq $functionNode) { throw "Deployment function is missing: $functionName" }
    Invoke-Expression $functionNode.Extent.Text
}
Assert-MigrationDiscovery
$migrationIds = @(Get-LocalMigrationIds)
if ($migrationIds.Count -eq 0) { throw 'Active migration inventory is empty.' }
foreach ($id in $migrationIds) {
    $migrationPath = Join-Path $RepositoryRoot "src\ErpSystem.Data\Migrations\$id.cs"
    $metadata = Get-Content -LiteralPath $migrationPath -Raw
    $designerPath = Join-Path $RepositoryRoot "src\ErpSystem.Data\Migrations\$id.Designer.cs"
    if (Test-Path -LiteralPath $designerPath) { $metadata += Get-Content -LiteralPath $designerPath -Raw }
    if (-not $metadata.Contains('DbContext(typeof(ApplicationDbContext))')) {
        throw "Active EF migration lacks ApplicationDbContext discovery metadata: $id"
    }
}
Write-Output "PASS: all $($migrationIds.Count) active migrations have migration ID and database-context discovery metadata."

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
