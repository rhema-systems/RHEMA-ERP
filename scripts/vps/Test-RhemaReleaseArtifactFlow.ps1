[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

function Assert-Test {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

$buildPath = Join-Path $repositoryRoot 'scripts\Build-RhemaRelease.ps1'
$deployPath = Join-Path $repositoryRoot 'scripts\Deploy-RhemaVps.ps1'
$remotePath = Join-Path $repositoryRoot 'scripts\vps\Invoke-RhemaVpsRemote.ps1'
$workflowPath = Join-Path $repositoryRoot '.github\workflows\ci-cd.yml'
foreach ($path in @($buildPath, $deployPath, $remotePath)) {
    $tokens = $null; $errors = $null
    [void][Management.Automation.Language.Parser]::ParseFile(
        $path, [ref]$tokens, [ref]$errors)
    Assert-Test ($errors.Count -eq 0) "PowerShell parser rejected $path."
}

$packagePath = Join-Path $repositoryRoot 'frontend\package.json'
$package = Get-Content $packagePath -Raw | ConvertFrom-Json
Assert-Test ($package.scripts.postinstall -eq 'node scripts/copy-syncfusion-pdfviewer-assets.js') `
    'Local installs no longer prepare Syncfusion assets.'
Assert-Test ($package.scripts.'prepare:syncfusion' -eq $package.scripts.postinstall) `
    'The explicit release Syncfusion command is missing.'
Assert-Test ($null -eq $package.scripts.PSObject.Properties['prebuild'] -and
    $null -eq $package.scripts.PSObject.Properties['prestart']) `
    'Syncfusion asset copying still runs redundantly during build or start.'
$tsconfig = Get-Content (Join-Path $repositoryRoot 'frontend\tsconfig.json') -Raw
Assert-Test $tsconfig.Contains('.next-production/types/**/*.ts') `
    'The production output type path is missing; Next would mutate tsconfig during a release build.'

$syncfusionTarget = Join-Path $repositoryRoot `
    'frontend\public\syncfusion\ej2-pdfviewer-lib'
New-Item -ItemType Directory -Path $syncfusionTarget -Force | Out-Null
$stale = Join-Path $syncfusionTarget 'stale-from-old-package.bin'
[IO.File]::WriteAllText($stale, 'stale')
& node (Join-Path $repositoryRoot 'frontend\scripts\copy-syncfusion-pdfviewer-assets.js')
Assert-Test ($LASTEXITCODE -eq 0) 'Syncfusion asset preparation failed.'
Assert-Test (-not (Test-Path -LiteralPath $stale)) `
    'Syncfusion asset preparation retained a stale vendor file.'
foreach ($name in @('pdfium.js', 'pdfium.wasm')) {
    Assert-Test (Test-Path -LiteralPath (Join-Path $syncfusionTarget $name)) `
        "Syncfusion asset preparation omitted $name."
}

$build = Get-Content $buildPath -Raw
Assert-Test (($build | Select-String -Pattern "'ci', '--include=dev'" -AllMatches).Matches.Count -eq 1) `
    'Release construction must perform exactly one npm ci.'
foreach ($contract in @('CleanBuild', '.next-production', "'cache'", 'npm.cmd', "'prune'",
        'release-manifest.json', 'nextPublicApiUrl', 'availablePhysicalMemoryBytes',
        'totalBuildSeconds', 'api.zip', 'frontend.zip', 'ReuseFrontendBuildFromCommit',
        'git diff --quiet', 'frontendBuildCommit')) {
    Assert-Test $build.Contains($contract) "Release builder is missing contract: $contract"
}
Assert-Test $build.Contains(
    "Select-String -Pattern 'localhost:5000|localhost:53484|localhost:7095'") `
    'Compiled URL validation leaves -Pattern without its argument in Windows PowerShell.'

$deploy = Get-Content $deployPath -Raw
foreach ($contract in @('DeployOnly', 'ArtifactDirectory',
        'Consume prebuilt immutable release artifacts',
        'Rollback application release after failed verification',
        'Deploy-only artifact validation failed')) {
    Assert-Test $deploy.Contains($contract) "Deploy-only contract is missing: $contract"
}
Assert-Test (-not $deploy.Contains("NODE_OPTIONS = '--max-old-space-size=8192'")) `
    'The legacy deployer still overrides the build wrapper heap with 8 GB.'

$remote = Get-Content $remotePath -Raw
foreach ($contract in @("Join-Path `$RhemaRoot 'releases'", 'VERSIONED_RELEASE|',
        'RollbackRelease', 'APPLICATION_ROLLBACK|PASS|DATABASE_UNCHANGED',
        "['status'] = 'Successful'")) {
    Assert-Test $remote.Contains($contract) "Remote release contract is missing: $contract"
}

$workflow = Get-Content $workflowPath -Raw
Assert-Test $workflow.Contains('frontend/.next-production/cache') `
    'CI does not persist the real Next.js build cache.'
Assert-Test ($workflow.Contains('frontend/.next-production/') -and
    $workflow.Contains('!frontend/.next-production/cache/**')) `
    'CI does not publish the real frontend build output.'
Assert-Test (-not $workflow.Contains('path: frontend/.next/')) `
    'CI still uploads the unused frontend/.next directory.'

Write-Output 'PASS|Phase 1 release flow: one-time Syncfusion assets, persistent build cache, prebuilt deploy-only artifacts, environment identity, versioned releases, and application-only rollback.'
