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
$remotePath = Join-Path $repositoryRoot 'scripts\vps\Invoke-RhemaVpsRemote.ps1'
$zipPackagePath = Join-Path $repositoryRoot 'scripts\vps\New-RhemaZipPackage.ps1'
$ciBootstrapPath = Join-Path $repositoryRoot `
    'scripts\vps\Initialize-GitHubVpsCicd.ps1'
$workflowPath = Join-Path $repositoryRoot '.github\workflows\ci-cd.yml'
foreach ($path in @($buildPath, $recoveryPath, $deployPath,
        $remotePath, $zipPackagePath, $ciBootstrapPath)) {
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

$assetTestRoot = Join-Path ([IO.Path]::GetTempPath()) `
    ('rhema-syncfusion-assets-' + [Guid]::NewGuid().ToString('N'))
try {
    $assetScriptDirectory = Join-Path $assetTestRoot 'scripts'
    $assetSource = Join-Path $assetTestRoot `
        'node_modules\@syncfusion\ej2-pdfviewer\dist\ej2-pdfviewer-lib'
    $syncfusionTarget = Join-Path $assetTestRoot `
        'public\syncfusion\ej2-pdfviewer-lib'
    foreach ($directory in @($assetScriptDirectory, $assetSource, $syncfusionTarget)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    Copy-Item (Join-Path $repositoryRoot `
        'frontend\scripts\copy-syncfusion-pdfviewer-assets.js') `
        -Destination $assetScriptDirectory -Force
    foreach ($name in @('pdfium.js', 'pdfium.wasm')) {
        [IO.File]::WriteAllText((Join-Path $assetSource $name), "test-$name")
    }
    $stale = Join-Path $syncfusionTarget 'stale-from-old-package.bin'
    [IO.File]::WriteAllText($stale, 'stale')
    & node (Join-Path $assetScriptDirectory 'copy-syncfusion-pdfviewer-assets.js')
    Assert-Test ($LASTEXITCODE -eq 0) 'Syncfusion asset preparation failed.'
    Assert-Test (-not (Test-Path -LiteralPath $stale)) `
        'Syncfusion asset preparation retained a stale vendor file.'
    foreach ($name in @('pdfium.js', 'pdfium.wasm')) {
        Assert-Test (Test-Path -LiteralPath (Join-Path $syncfusionTarget $name)) `
            "Syncfusion asset preparation omitted $name."
    }
}
finally {
    if (Test-Path -LiteralPath $assetTestRoot) {
        Remove-Item -LiteralPath $assetTestRoot -Recurse -Force
    }
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
        'PrepareOperationalUat',
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
Assert-Test $deploy.Contains("if (`$PrepareOperationalUat -and -not `$FreshDatabaseName)") `
    'Normal application deployment still runs operational UAT seeds unconditionally.'

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
        'Expand-ZipArchiveChecked', 'tar.exe', "'/MT:16'",
        'Start API, run migrations, and wait for liveness',
        'Activate frontend release and wait for readiness',
        'REMOTE_TIMING_JSON|')) {
    Assert-Test $remote.Contains($contract) "Remote release contract is missing: $contract"
}
Assert-Test (-not $remote.Contains('Expand-Archive')) `
    'Remote activation still uses slow PowerShell Expand-Archive.'

$workflow = Get-Content $workflowPath -Raw
Assert-Test $workflow.Contains('frontend/.next-production/cache') `
    'CI does not persist the real Next.js build cache.'
foreach ($contract in @('vps-release-contract', 'Test-CanonicalMigrationPreflight.ps1',
        'Test-RhemaZipPackage.ps1', 'Test-RhemaReleaseArtifactFlow.ps1',
        'Build-RhemaRelease.ps1', 'Deploy-RhemaVps.ps1',
        '-DeployOnly', '-ArtifactDirectory',
        'actions/upload-artifact@v4', 'actions/download-artifact@v4',
        'compression-level: 0', 'StrictHostKeyChecking=yes',
        'environment: test-vps', 'deploy_to_test_vps')) {
    Assert-Test $workflow.Contains($contract) `
        "CI does not enforce the VPS release contract: $contract"
}
Assert-Test (-not $workflow.Contains('docker/build-push-action')) `
    'The active Windows VPS workflow still builds unused Docker images.'
Assert-Test (-not $workflow.Contains('docker compose')) `
    'The active Windows VPS workflow still invokes the unused Docker deployment path.'
Assert-Test (-not (Test-Path (Join-Path $repositoryRoot `
            '.github\workflows\deploy-environments.yml'))) `
    'The obsolete Docker environment deployment workflow is still active.'

$ciBootstrap = Get-Content $ciBootstrapPath -Raw
foreach ($contract in @('SYNCFUSION_LICENSE', 'VPS_SSH_PRIVATE_KEY',
        'VPS_SSH_KNOWN_HOSTS', 'VPS_PUBLIC_BASE_URL',
        'C:\ProgramData\ssh\ssh_host_ed25519_key.pub',
        'windows_amd64.zip',
        'AE64E556ECC240B200F7EBA60D550E4BB60D78E860E69DD88C449405B86067F4',
        'Get-AuthenticodeSignature', 'auth login',
        'github-actions-vps-deployment', 'administrators_authorized_keys',
        'Creating a dedicated GitHub Actions deployment identity',
        'The dedicated deployment key could not authenticate',
        'workflow enable ci-cd.yml', 'build_release=false',
        'GITHUB_VPS_CICD|CONFIGURED',
        'GITHUB_VPS_CICD|CONTRACT_VALIDATION_QUEUED')) {
    Assert-Test $ciBootstrap.Contains($contract) `
        "GitHub CI/CD bootstrap is missing contract: $contract"
}
Assert-Test (-not $ciBootstrap.Contains('Write-Output $syncfusionLicense')) `
    'GitHub CI/CD bootstrap can print the Syncfusion license.'
Assert-Test (-not $ciBootstrap.Contains('Write-Output $privateKey')) `
    'GitHub CI/CD bootstrap can print the SSH private key.'
Assert-Test (-not $ciBootstrap.Contains("-P '' -f `$SshPrivateKeyPath")) `
    'GitHub CI/CD bootstrap uses an empty native argument that Windows PowerShell drops.'

Write-Output 'PASS|Phase 1 release flow: one-time Syncfusion assets, persistent build cache, prebuilt deploy-only artifacts, environment identity, versioned releases, and application-only rollback.'
