[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

function Assert-Test {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

$buildPath = Join-Path $repositoryRoot 'scripts\Build-RhemaRelease.ps1'
$recoveryPath = Join-Path $repositoryRoot 'scripts\Complete-RhemaReleasePackaging.ps1'
$deployPath = Join-Path $repositoryRoot 'scripts\Deploy-RhemaVps.ps1'
$qsDeployPath = Join-Path $repositoryRoot 'scripts\Deploy-QsUatVps.ps1'
$remotePath = Join-Path $repositoryRoot 'scripts\vps\Invoke-RhemaVpsRemote.ps1'
$zipPackagePath = Join-Path $repositoryRoot 'scripts\vps\New-RhemaZipPackage.ps1'
$workflowPath = Join-Path $repositoryRoot '.github\workflows\ci-cd.yml'
foreach ($path in @($buildPath, $recoveryPath, $deployPath, $qsDeployPath, $remotePath, $zipPackagePath)) {
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
        'git diff --quiet', 'frontendBuildCommit', 'Assert-RhemaMigrationGuardCoverage',
        'BUILD TIMING SUMMARY (slowest first)',
        'resourcesBefore', 'resourcesAfter', 'completedUtc')) {
    Assert-Test $build.Contains($contract) "Release builder is missing contract: $contract"
}
Assert-Test $build.Contains('RELEASE_ARTIFACT_DIRECTORY|') `
    'Release builder does not expose its immutable artifact directory to the optimized wrapper.'
Assert-Test $build.Contains(
    "Select-String -Pattern 'localhost:5000|localhost:53484|localhost:7095'") `
    'Compiled URL validation leaves -Pattern without its argument in Windows PowerShell.'
Assert-Test $build.Contains('[string]::Equals(') `
    'Frontend ZIP validation still suffix-matches dependency package manifests.'
Assert-Test (-not $build.Contains('Compress-Archive')) `
    'Release builder still uses the slow PowerShell Compress-Archive implementation.'

$recovery = Get-Content $recoveryPath -Raw
foreach ($contract in @('ArtifactSourceCommit', 'FrontendBuildCommit',
        'Application build inputs changed', 'packagingRecovered',
        'RELEASE_BUILD_PASSED|')) {
    Assert-Test $recovery.Contains($contract) `
        "Compressed release recovery is missing contract: $contract"
}
foreach ($contract in @('$priorManifest.packagingRecovered -eq $true',
        '$priorManifest.commit -eq $artifactCommit',
        '$priorManifest.frontendBuildCommit -eq $frontendCommit',
        "archivePackagingEngine = 'ReusedVerifiedZip'")) {
    Assert-Test $recovery.Contains($contract) `
        "Packaging recovery is missing verified artifact-promotion contract: $contract"
}

$deploy = Get-Content $deployPath -Raw
foreach ($contract in @('DeployOnly', 'ArtifactDirectory',
        'PreflightOnly', 'PREFLIGHT ONLY PASSED',
        'Consume prebuilt immutable release artifacts',
        'Rollback application release after failed verification',
        'Deploy-only artifact validation failed',
        'DEPLOYMENT TIMING SUMMARY (slowest first)', 'REMOTE_TIMING_JSON|',
        'slowestSteps', 'completedUtc', 'Publish self-contained API',
        'Restore locked frontend dependencies', 'Build Next.js production application',
        'Stage frontend runtime and production dependencies',
        'Package API artifact', 'Package frontend artifact')) {
    Assert-Test $deploy.Contains($contract) "Deploy-only contract is missing: $contract"
}
Assert-Test (-not $deploy.Contains("NODE_OPTIONS = '--max-old-space-size=8192'")) `
    'The legacy deployer still overrides the build wrapper heap with 8 GB.'
Assert-Test (-not $deploy.Contains('Compress-Archive')) `
    'The legacy deployer still uses the slow PowerShell Compress-Archive implementation.'

$zipPackage = Get-Content $zipPackagePath -Raw
foreach ($contract in @('tar.exe', 'ZipFile]::CreateFromDirectory', 'NoCompression', 'Fastest')) {
    Assert-Test $zipPackage.Contains($contract) "Fast ZIP packager is missing contract: $contract"
}

$remote = Get-Content $remotePath -Raw
foreach ($contract in @("Join-Path `$RhemaRoot 'releases'", 'VERSIONED_RELEASE|',
        'RollbackRelease', 'APPLICATION_ROLLBACK|PASS|DATABASE_UNCHANGED',
        "['status'] = 'Successful'", 'Get-RemoteResourceSnapshot',
        'Create compressed SQL COPY_ONLY backup',
        'Verify SQL backup checksum and restore metadata',
        'Recheck migration guards after API stop',
        'Start API, run migrations, and wait for liveness',
        'Activate frontend release and wait for readiness',
        'REMOTE_TIMING_JSON|')) {
    Assert-Test $remote.Contains($contract) "Remote release contract is missing: $contract"
}

$qsDeploy = Get-Content $qsDeployPath -Raw
foreach ($contract in @('Build immutable release once', 'Activate verified ERP release',
        'Validate VPS before release build', 'PreflightOnly',
        'DeployOnly', 'ArtifactDirectory', 'LegacyFullBuild',
        "ArchiveCompressionLevel='Fastest'",
        'Validate and optionally update source checkout', 'UpdateSource', '--ff-only',
        'Prepare QS UAT data and decisions', 'Generate QS UAT readiness evidence',
        'QS DEPLOYMENT TIMING SUMMARY (slowest first)', 'slowestSteps')) {
    Assert-Test $qsDeploy.Contains($contract) "QS deployment timing contract is missing: $contract"
}

$workflow = Get-Content $workflowPath -Raw
Assert-Test $workflow.Contains('frontend/.next-production/cache') `
    'CI does not persist the real Next.js build cache.'
Assert-Test ($workflow.Contains('frontend/.next-production/') -and
    $workflow.Contains('!frontend/.next-production/cache/**')) `
    'CI does not publish the real frontend build output.'
Assert-Test (-not $workflow.Contains('path: frontend/.next/')) `
    'CI still uploads the unused frontend/.next directory.'
foreach ($contract in @('vps-release-contract', 'Test-CanonicalMigrationPreflight.ps1',
        'Test-RhemaZipPackage.ps1', 'Test-RhemaReleaseArtifactFlow.ps1',
        'Test-QsUatVpsDeployment.ps1')) {
    Assert-Test $workflow.Contains($contract) `
        "CI does not enforce the VPS release contract: $contract"
}

Write-Output 'PASS|Phase 1 release flow: one-time Syncfusion assets, persistent build cache, prebuilt deploy-only artifacts, environment identity, versioned releases, and application-only rollback.'
