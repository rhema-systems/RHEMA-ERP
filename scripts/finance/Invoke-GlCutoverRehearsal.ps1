[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('InspectSource', 'RehearseEmpty', 'DropRehearsal')]
    [string]$Mode,

    [string]$TargetConnectionEnvironmentVariable = 'RHEMA_GL_REHEARSAL_CONNECTION',
    [string]$SourceConnectionEnvironmentVariable = 'RHEMA_GL_SOURCE_READONLY_CONNECTION',
    [string]$EvidenceDirectory,
    [switch]$ConfirmDrop
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$apiProject = Join-Path $repositoryRoot 'src\ErpSystem.Api\ErpSystem.Api.csproj'
$dataProject = Join-Path $repositoryRoot 'src\ErpSystem.Data\ErpSystem.Data.csproj'
$baselineMigration = '20260313114533_InitialBaseline'
$baselinePredecessor = '20260312013725_AddProjectResourceRoutingRequirements'
$supersededMigrations = @(
    '20260304155434_RecreateHRTables',
    '20260311184920_AddProjectMaterialCostLedger',
    '20260311213738_AddProjectDeliverableExternalReviews',
    '20260312013725_AddProjectResourceRoutingRequirements'
)
$approvedNamePattern = '^RHEMAERP_GL_REHEARSAL_[A-Z0-9_]{1,64}$'

function Get-ProcessConnectionString([string]$variableName) {
    $value = [Environment]::GetEnvironmentVariable($variableName, 'Process')
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Process environment variable '$variableName' is required. User/machine fallbacks are deliberately forbidden."
    }
    return $value
}

function ConvertTo-ConnectionTarget([string]$connectionString, [bool]$requireRehearsalName) {
    $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($connectionString)
    if ([string]::IsNullOrWhiteSpace($builder.DataSource)) { throw 'Connection string has no SQL Server target.' }
    if ([string]::IsNullOrWhiteSpace($builder.InitialCatalog)) { throw 'Connection string has no Database/Initial Catalog.' }
    if (-not [string]::IsNullOrWhiteSpace($builder.AttachDBFilename)) { throw 'AttachDbFilename is forbidden for GL rehearsals.' }
    if ($builder.UserInstance) { throw 'User Instance connections are forbidden for GL rehearsals.' }
    if ($builder.DataSource.IndexOfAny([char[]]"`r`n") -ge 0 -or $builder.InitialCatalog.IndexOfAny([char[]]"`r`n") -ge 0) {
        throw 'Connection target contains control characters.'
    }

    $database = $builder.InitialCatalog.ToUpperInvariant()
    if ($requireRehearsalName -and $database -notmatch $approvedNamePattern) {
        throw "Refusing database '$($builder.InitialCatalog)'. Rehearsal targets must match $approvedNamePattern."
    }

    [pscustomobject]@{
        Builder = $builder
        Server = $builder.DataSource
        Database = $builder.InitialCatalog
    }
}

function Write-TargetLog([string]$operation, $target) {
    Write-Host "[$operation] SQL Server: $($target.Server)"
    Write-Host "[$operation] Database:   $($target.Database)"
}

function Invoke-Native([string]$filePath, [string[]]$arguments) {
    & $filePath @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Native command failed with exit code ${LASTEXITCODE}: $filePath"
    }
}

function Invoke-Sql([System.Data.SqlClient.SqlConnectionStringBuilder]$builder, [string]$database,
    [string]$query, [string]$inputFile = '', [string]$outputFile = '') {
    # -I keeps QUOTED_IDENTIFIER enabled for the generated baseline's filtered and
    # computed-column indexes. sqlcmd otherwise defaults it off for input scripts.
    $arguments = @('-S', $builder.DataSource, '-d', $database, '-b', '-C', '-I', '-W', '-h', '-1')
    $oldPassword = [Environment]::GetEnvironmentVariable('SQLCMDPASSWORD', 'Process')
    try {
        if ($builder.IntegratedSecurity) {
            $arguments += '-E'
        }
        else {
            if ([string]::IsNullOrWhiteSpace($builder.UserID)) { throw 'SQL authentication requires User ID.' }
            [Environment]::SetEnvironmentVariable('SQLCMDPASSWORD', $builder.Password, 'Process')
            $arguments += @('-U', $builder.UserID)
        }
        if (-not [string]::IsNullOrWhiteSpace($inputFile)) { $arguments += @('-i', $inputFile) }
        else { $arguments += @('-Q', $query) }
        if (-not [string]::IsNullOrWhiteSpace($outputFile)) { $arguments += @('-o', $outputFile, '-u') }
        Invoke-Native 'sqlcmd' $arguments
    }
    finally {
        [Environment]::SetEnvironmentVariable('SQLCMDPASSWORD', $oldPassword, 'Process')
    }
}

function Assert-TargetAbsent($target) {
    $master = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($target.Builder.ConnectionString)
    $master.set_InitialCatalog('master')
    $connection = [System.Data.SqlClient.SqlConnection]::new($master.ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = 'SELECT COUNT_BIG(*) FROM sys.databases WHERE name=@database;'
        [void]$command.Parameters.AddWithValue('@database', $target.Database)
        $count = [long]$command.ExecuteScalar()
    }
    finally { $connection.Dispose() }
    if ($count -ne 0) {
        throw "Rehearsal target '$($target.Database)' already exists. This harness never overwrites or auto-drops a database."
    }
}

function Set-ApplicationConnection([string]$connectionString, [scriptblock]$action) {
    $oldConnection = [Environment]::GetEnvironmentVariable('ConnectionStrings__DefaultConnection', 'Process')
    $oldEnvironment = [Environment]::GetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Process')
    try {
        [Environment]::SetEnvironmentVariable('ConnectionStrings__DefaultConnection', $connectionString, 'Process')
        [Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Development', 'Process')
        & $action
    }
    finally {
        [Environment]::SetEnvironmentVariable('ConnectionStrings__DefaultConnection', $oldConnection, 'Process')
        [Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', $oldEnvironment, 'Process')
    }
}

function Get-DefaultEvidenceDirectory([string]$database) {
    Join-Path $repositoryRoot ".artifacts\finance-gl-rehearsal\$database"
}

if ($Mode -eq 'InspectSource') {
    $sourceConnection = Get-ProcessConnectionString $SourceConnectionEnvironmentVariable
    $source = ConvertTo-ConnectionTarget $sourceConnection $false
    Write-TargetLog 'READ-ONLY SOURCE INSPECTION' $source
    $evidence = if ($EvidenceDirectory) { $EvidenceDirectory } else { Get-DefaultEvidenceDirectory 'source-readiness' }
    New-Item -ItemType Directory -Force -Path $evidence | Out-Null
    Invoke-Sql $source.Builder $source.Database '' (Join-Path $PSScriptRoot 'sql\gl-source-readiness.sql') `
        (Join-Path $evidence 'source-readiness.txt')
    Write-Host "Source readiness evidence: $evidence"
    return
}

$targetConnection = Get-ProcessConnectionString $TargetConnectionEnvironmentVariable
$target = ConvertTo-ConnectionTarget $targetConnection $true
Write-TargetLog $Mode $target

if ($Mode -eq 'DropRehearsal') {
    if (-not $ConfirmDrop) { throw 'DropRehearsal requires -ConfirmDrop.' }
    $escapedName = $target.Database.Replace(']', ']]')
    Write-Host '[DROP] Prefix assertion passed; dropping the exact resolved rehearsal database.'
    Invoke-Sql $target.Builder 'master' "IF DB_ID(N'$($target.Database.Replace("'", "''"))') IS NOT NULL BEGIN ALTER DATABASE [$escapedName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$escapedName]; END;"
    return
}

Assert-TargetAbsent $target
$evidenceDirectoryResolved = if ($EvidenceDirectory) { $EvidenceDirectory } else { Get-DefaultEvidenceDirectory $target.Database }
New-Item -ItemType Directory -Force -Path $evidenceDirectoryResolved | Out-Null
$baselineScript = Join-Path $evidenceDirectoryResolved 'initial-baseline.sql'
$escapedDatabase = $target.Database.Replace(']', ']]')

Write-Host '[CREATE] Prefix assertion passed; creating the exact resolved rehearsal database.'
Invoke-Sql $target.Builder 'master' "CREATE DATABASE [$escapedDatabase];"

try {
    Push-Location $repositoryRoot
    $oldFocusedTooling = [Environment]::GetEnvironmentVariable('TdcFocusedEfToolingBuild', 'Process')
    try {
        # The full historical designers exceed hundreds of MB of generated C#. The repository's
        # focused tooling mode compiles the authoritative current snapshot plus lightweight
        # discovery metadata, whose parity is separately tested below.
        [Environment]::SetEnvironmentVariable('TdcFocusedEfToolingBuild', 'true', 'Process')
        Invoke-Native 'git' @('diff', '--check')
        Invoke-Native 'dotnet' @('build', $apiProject, '--configuration', 'Debug', '--nologo',
            "-p:TdcEfToolingMigrationDesigner=Migrations\20260313114533_InitialBaseline.Designer.cs")
        Invoke-Native 'dotnet' @('ef', 'migrations', 'script', $baselinePredecessor, $baselineMigration,
            '--project', $dataProject, '--startup-project', $apiProject, '--configuration', 'Debug',
            '--context', 'ApplicationDbContext', '--no-build', '--output', $baselineScript)
        Invoke-Sql $target.Builder $target.Database @"
IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[__EFMigrationsHistory]
    (
        [MigrationId] nvarchar(150) NOT NULL CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY,
        [ProductVersion] nvarchar(32) NOT NULL
    );
END;
"@
        Invoke-Sql $target.Builder $target.Database '' $baselineScript

        $stampedMigrations = @($supersededMigrations) + $baselineMigration
        $stampValues = ($stampedMigrations | ForEach-Object {
            "IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId]=N'$_') INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N'$_', N'8.0.0');"
        }) -join "`n"
        Invoke-Sql $target.Builder $target.Database $stampValues

        Set-ApplicationConnection $targetConnection {
            # Exercise the same controlled deployment entry point operators use. It runs
            # Database.MigrateAsync without starting the HTTP host or invoking seeders.
            Invoke-Native 'dotnet' @('run', '--no-build', '--configuration', 'Debug',
                '--project', $apiProject, '--', 'apply-migrations')
            Invoke-Native 'dotnet' @('run', '--no-build', '--configuration', 'Debug', '--project', $apiProject, '--', 'seed-db')
        }

        $passOne = Join-Path $evidenceDirectoryResolved 'invariants-pass-1.txt'
        Invoke-Sql $target.Builder $target.Database '' (Join-Path $PSScriptRoot 'sql\gl-rehearsal-invariants.sql') $passOne

        Set-ApplicationConnection $targetConnection {
            Invoke-Native 'dotnet' @('run', '--no-build', '--configuration', 'Debug', '--project', $apiProject, '--', 'seed-db')
        }

        $passTwo = Join-Path $evidenceDirectoryResolved 'invariants-pass-2.txt'
        Invoke-Sql $target.Builder $target.Database '' (Join-Path $PSScriptRoot 'sql\gl-rehearsal-invariants.sql') $passTwo
        $first = (Get-FileHash -Algorithm SHA256 $passOne).Hash
        $second = (Get-FileHash -Algorithm SHA256 $passTwo).Hash
        if ($first -ne $second) { throw 'Second seed changed the canonical Finance invariant snapshot.' }

        Set-ApplicationConnection $targetConnection {
            Invoke-Native 'dotnet' @('ef', 'migrations', 'has-pending-model-changes', '--project', $dataProject,
                '--startup-project', $apiProject, '--configuration', 'Debug', '--context', 'ApplicationDbContext', '--no-build')
        }

        $summary = [ordered]@{
            status = 'PASS'
            gitHead = (git rev-parse HEAD).Trim()
            server = $target.Server
            database = $target.Database
            latestMigration = '20260904003118_AddGovernedAccountSegmentIdentity'
            semanticSnapshotSha256 = $first
            seedPasses = 2
            completedAtUtc = [DateTime]::UtcNow.ToString('O')
        }
        $summary | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $evidenceDirectoryResolved 'summary.json')
        Write-Host "GL cutover empty-database rehearsal passed. Evidence: $evidenceDirectoryResolved"
    }
    finally {
        [Environment]::SetEnvironmentVariable('TdcFocusedEfToolingBuild', $oldFocusedTooling, 'Process')
        Pop-Location
    }
}
catch {
    Write-Error "Rehearsal failed. The safe-named database was preserved for inspection: $($target.Database). $($_.Exception.Message)"
    throw
}
