# Embedded in the versioned VPS helper. No code executes merely by loading this file.
function Get-FreshCutoverStatePath {
    Assert-DeploymentId
    return (Join-Path $BackupsRoot "deploy-$DeploymentId\fresh-cutover.json")
}

function Save-FreshCutoverState {
    param($State)
    [IO.File]::WriteAllText((Get-FreshCutoverStatePath), ($State | ConvertTo-Json -Depth 8),
        (New-Object Text.UTF8Encoding($false)))
}

function Assert-FreshChildPath {
    param([string]$Path, [string]$Parent)
    $full = [IO.Path]::GetFullPath($Path)
    $prefix = [IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    Assert-True ($full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) 'Cutover path escapes its deployment directory.'
}

function Assert-FreshDatabaseServiceIdentity {
    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder (Get-DatabaseConnectionString)
    if ($builder.IntegratedSecurity) {
        $service = Get-CimInstance Win32_Service -Filter "Name='RhemaERPAPI'"
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent().Name
        Assert-True ($service.StartName -eq $identity) `
            'Fresh database provisioning with integrated SQL authentication must run as the API service identity. Configure a reviewed SQL login or run under that identity; no permissions were changed.'
    }
}

function Get-FreshExpectedMigrationIds {
    # Generated from the exact checked-out release, included in the helper hash.
    return @([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('__RHEMA_FRESH_MIGRATION_IDS__')) | ConvertFrom-Json)
}

function Invoke-FreshDatabaseCutover {
    param([string]$StageApi, [string]$StageFrontend, [string]$Backup, [string]$Retired)
    Assert-FreshDatabaseServiceIdentity
    Assert-True (-not (Test-Path -LiteralPath (Get-FreshCutoverStatePath))) 'Fresh cutover state already exists; review it before retrying.'
    $backupManifest = Get-Content -LiteralPath (Join-Path $Backup 'backup-manifest.json') -Raw | ConvertFrom-Json
    Assert-True ($backupManifest.deploymentId -eq $DeploymentId -and $backupManifest.commit -eq $ExpectedCommit -and $backupManifest.databaseBackupVerified) `
        'Fresh cutover requires a verified backup for this exact deployment.'
    $source = New-Object System.Data.SqlClient.SqlConnectionStringBuilder (Get-DatabaseConnectionString)
    # A complete rollback plan is persisted before touching services or their configuration.
    $apiFiles = @(Get-ChildItem -LiteralPath $StageApi -File -Recurse | ForEach-Object {
        $relative = $_.FullName.Substring($StageApi.TrimEnd('\').Length + 1)
        Assert-True ($relative -notmatch '^(?i:logs|secure-file-storage|wwwroot\\uploads)(\\|$)' -and
            $relative -notmatch '(^|\\)(?i:appsettings[^\\]*\.json|\.env[^\\]*)$') 'API package contains protected runtime files.'
        [pscustomobject]@{ path = $relative; existed = Test-Path -LiteralPath (Join-Path $ApiRoot $relative) }
    })
    $frontendItems = @('.next', 'public', 'node_modules', 'package.json', 'package-lock.json', 'next.config.js')
    foreach ($name in $frontendItems) {
        Assert-True (Test-Path -LiteralPath (Join-Path $FrontendRoot $name)) "Existing frontend item is missing: $name"
    }
    $result = Invoke-RhemaFreshDatabaseProvisioning -SourceConnectionString $source.ConnectionString `
        -FreshDatabaseName $FreshDatabaseName -StagedApiDirectory $StageApi `
        -ExpectedMigrationIds (Get-FreshExpectedMigrationIds) -WorkDirectory (Join-Path $PackagesRoot "fresh-$DeploymentId")
    $provisionEvidence = [ordered]@{
        database = $FreshDatabaseName; migrations = $result.MigrationIds; seedCounts = $result.SeedCounts
        commands = $result.CliEvidence; physicalIntegrityPassed = $result.PhysicalIntegrityPassed
        foreignKeysTrusted = $result.ForeignKeysTrusted
    }
    [IO.File]::WriteAllText((Join-Path $Backup 'fresh-provisioning.json'), ($provisionEvidence | ConvertTo-Json -Depth 6),
        (New-Object Text.UTF8Encoding($false)))
    Write-Output "FRESH_DATABASE_VALIDATED|$FreshDatabaseName|Migrations=$(@($result.MigrationIds).Count)"

    $releasePath = Join-Path $LogsRoot 'current-release.json'
    if (Test-Path -LiteralPath $releasePath) {
        Copy-Item -LiteralPath $releasePath -Destination (Join-Path $Backup 'previous-release.json')
    }
    $state = [ordered]@{
        schemaVersion = 1; deploymentId = $DeploymentId; commit = $ExpectedCommit; status = 'Prepared'
        sourceDatabase = $source.InitialCatalog; targetDatabase = $FreshDatabaseName; server = $source.DataSource
        apiFiles = $apiFiles; frontendItems = $frontendItems
        hadRelease = Test-Path -LiteralPath $releasePath
        previousReleaseHash = if (Test-Path -LiteralPath $releasePath) { (Get-FileHash -LiteralPath $releasePath -Algorithm SHA256).Hash } else { $null }
    }
    Save-FreshCutoverState $state
    try {
        Stop-ManagedService RhemaERPFrontend
        Stop-ManagedService RhemaERPAPI
        $state.status = 'Switching'; Save-FreshCutoverState $state
        Set-TestServerConfiguration
        Set-ApiServiceEnvironmentValues @{ ConnectionStrings__DefaultConnection = $result.ConnectionString }
        foreach ($item in $apiFiles) {
            $destination = Join-Path $ApiRoot $item.path
            Assert-FreshChildPath $destination $ApiRoot
            New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
            Copy-Item -LiteralPath (Join-Path $StageApi $item.path) -Destination $destination -Force
        }
        foreach ($name in $frontendItems) {
            $live = Join-Path $FrontendRoot $name
            $old = Join-Path $Retired $name
            Assert-FreshChildPath $live $FrontendRoot
            Assert-FreshChildPath $old $PackagesRoot
            Move-Item -LiteralPath $live -Destination $old
            Move-Item -LiteralPath (Join-Path $StageFrontend $name) -Destination $live
        }
        Start-ApiWithControlledMigrations (Get-Date)
        Start-Service RhemaERPFrontend
        Wait-FrontendReady
        $state.status = 'Applied'; Save-FreshCutoverState $state
        Write-Output "FRESH_DATABASE_APPLIED|$FreshDatabaseName"
    }
    catch {
        $failure = $_
        Restore-FreshDatabaseCutover | Out-Host
        throw $failure
    }
}

function Restore-FreshDatabaseCutover {
    $statePath = Get-FreshCutoverStatePath
    if (-not (Test-Path -LiteralPath $statePath)) { Write-Output 'FRESH_ROLLBACK|NOT_STARTED'; return }
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    Assert-True ($state.schemaVersion -eq 1 -and $state.deploymentId -eq $DeploymentId -and
        $state.commit -eq $ExpectedCommit -and $state.targetDatabase -eq $FreshDatabaseName) 'Rollback state does not match this deployment.'
    if ($state.status -eq 'RolledBack') { Write-Output 'FRESH_ROLLBACK|ALREADY_RESTORED'; return }
    Assert-True ($state.status -ne 'Committed') 'A completed cutover cannot be automatically rolled back by a failed deployment retry.'
    $current = New-Object System.Data.SqlClient.SqlConnectionStringBuilder (Get-DatabaseConnectionString)
    Assert-True ($current.DataSource -eq $state.server -and $current.InitialCatalog -in @($state.sourceDatabase, $state.targetDatabase)) `
        'The running service now points to an unrelated database; automatic rollback refused.'
    $releasePath = Join-Path $LogsRoot 'current-release.json'
    if (Test-Path -LiteralPath $releasePath) {
        $unchanged = $state.hadRelease -and (Get-FileHash -LiteralPath $releasePath -Algorithm SHA256).Hash -eq $state.previousReleaseHash
        if (-not $unchanged) {
            $release = Get-Content -LiteralPath $releasePath -Raw | ConvertFrom-Json
            Assert-True ($release.deploymentId -eq $DeploymentId -and $release.commit -eq $ExpectedCommit) `
                'Another release changed the VPS after this cutover; automatic rollback refused.'
        }
    }
    $backup = Join-Path $BackupsRoot "deploy-$DeploymentId"
    $retired = Join-Path $PackagesRoot "retired-$DeploymentId"
    $failed = Join-Path $PackagesRoot "fresh-failed-$DeploymentId"
    Stop-ManagedService RhemaERPFrontend
    Stop-ManagedService RhemaERPAPI
    # Services stay stopped if restoration fails; never start mismatched binaries/configuration.
    foreach ($item in $state.apiFiles) {
        $live = Join-Path $ApiRoot $item.path
        $old = Join-Path (Join-Path $backup 'api') $item.path
        Assert-FreshChildPath $live $ApiRoot
        Assert-FreshChildPath $old (Join-Path $backup 'api')
        if ($item.existed) {
            Copy-Item -LiteralPath $old -Destination $live -Force
        } elseif (Test-Path -LiteralPath $live) {
            $failedPath = Join-Path (Join-Path $failed 'api') $item.path
            Assert-FreshChildPath $failedPath $PackagesRoot
            New-Item -ItemType Directory -Path (Split-Path -Parent $failedPath) -Force | Out-Null
            Move-Item -LiteralPath $live -Destination $failedPath -Force
        }
    }
    foreach ($name in $state.frontendItems) {
        $live = Join-Path $FrontendRoot $name
        $old = Join-Path $retired $name
        Assert-FreshChildPath $live $FrontendRoot
        Assert-FreshChildPath $old $PackagesRoot
        # If retirement never happened, the live item is still the original.
        if (Test-Path -LiteralPath $old) {
            $failedPath = Join-Path (Join-Path $failed 'frontend') $name
            Assert-FreshChildPath $failedPath $PackagesRoot
            New-Item -ItemType Directory -Path (Split-Path -Parent $failedPath) -Force | Out-Null
            if (Test-Path -LiteralPath $live) { Move-Item -LiteralPath $live -Destination $failedPath -Force }
            Move-Item -LiteralPath $old -Destination $live
        }
    }
    if ($UsesNssmApiConfiguration) {
        Restore-NssmEnvironmentSnapshot (Get-Content -LiteralPath (Join-Path $backup 'services\api\RhemaERPAPI.nssm-environment.json') -Raw | ConvertFrom-Json)
    } else {
        Copy-Item -LiteralPath (Join-Path $backup 'services\api\RhemaERPAPI.xml') -Destination $ApiServiceXml -Force
    }
    $releasePath = Join-Path $LogsRoot 'current-release.json'
    if ($state.hadRelease) {
        Copy-Item -LiteralPath (Join-Path $backup 'previous-release.json') -Destination $releasePath -Force
    } elseif (Test-Path -LiteralPath $releasePath) {
        New-Item -ItemType Directory -Path $failed -Force | Out-Null
        Move-Item -LiteralPath $releasePath -Destination (Join-Path $failed 'current-release.json') -Force
    }
    Start-Service RhemaERPAPI
    Wait-ApiReady (Get-Date)
    Start-Service RhemaERPFrontend
    Wait-FrontendReady
    $state.status = 'RolledBack'; Save-FreshCutoverState $state
    Write-Output 'FRESH_ROLLBACK|PASS|Original application and connection restored; both databases preserved.'
}

function Complete-FreshDatabaseCutover {
    $state = Get-Content -LiteralPath (Get-FreshCutoverStatePath) -Raw | ConvertFrom-Json
    Assert-True ($state.status -eq 'Applied' -and $state.deploymentId -eq $DeploymentId -and
        $state.commit -eq $ExpectedCommit -and $state.targetDatabase -eq $FreshDatabaseName) 'Fresh cutover is not ready to complete.'
    $current = New-Object System.Data.SqlClient.SqlConnectionStringBuilder (Get-DatabaseConnectionString)
    Assert-True ($current.InitialCatalog -eq $FreshDatabaseName -and $current.DataSource -eq $state.server) 'The configured database differs from the verified fresh target.'
    $state.status = 'Committed'; Save-FreshCutoverState $state
    Write-Output "FRESH_CUTOVER|PASS|$FreshDatabaseName"
}
