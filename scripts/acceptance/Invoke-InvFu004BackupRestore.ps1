[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$apiProject = Join-Path $repoRoot 'src\ErpSystem.Api\ErpSystem.Api.csproj'

$raw = @(dotnet user-secrets list --project $apiProject --json)
$jsonStart = [Array]::FindIndex($raw, [Predicate[string]]{ param($line) $line.Trim() -eq '{' })
$jsonEnd = [Array]::FindLastIndex($raw, [Predicate[string]]{ param($line) $line.Trim() -eq '}' })
if ($jsonStart -lt 0 -or $jsonEnd -le $jsonStart) {
    throw 'The configured API secrets did not return a JSON object.'
}
$secrets = (($raw[$jsonStart..$jsonEnd]) -join "`n") | ConvertFrom-Json
$secret = $secrets | Where-Object {
    $_ -and $_.PSObject.Properties.Name -contains 'ConnectionStrings:DefaultConnection'
} | Select-Object -First 1
if (-not $secret) { throw 'The configured API database connection was not found.' }

Add-Type -AssemblyName System.Data
$connection = [System.Data.SqlClient.SqlConnection]::new(
    [string]$secret.'ConnectionStrings:DefaultConnection')
$connection.Open()
$sourceDatabase = $connection.Database
if ($sourceDatabase -ne 'RhemaERP') {
    throw "Expected the configured RhemaERP database, received '$sourceDatabase'."
}
$connection.ChangeDatabase('master')

$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$restoreDatabase = "RhemaERP_INVFU004_$suffix"
$restoreCreated = $false
$backupFile = $null
$dataFile = $null
$logFile = $null
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

function Invoke-Scalar([string]$Sql) {
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 0
    $command.CommandText = $Sql
    return $command.ExecuteScalar()
}

function Invoke-NonQuery([string]$Sql, [hashtable]$Parameters = @{}) {
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 0
    $command.CommandText = $Sql
    foreach ($entry in $Parameters.GetEnumerator()) {
        $null = $command.Parameters.AddWithValue("@$($entry.Key)", $entry.Value)
    }
    return $command.ExecuteNonQuery()
}

try {
    $dataPath = [string](Invoke-Scalar "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000));")
    $logPath = [string](Invoke-Scalar "SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000));")
    $backupPath = [string](Invoke-Scalar "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000));")
    $dataLogical = [string](Invoke-Scalar "SELECT name FROM [$sourceDatabase].sys.database_files WHERE type=0;")
    $logLogical = [string](Invoke-Scalar "SELECT name FROM [$sourceDatabase].sys.database_files WHERE type=1;")
    $backupFile = [IO.Path]::Combine($backupPath, "$restoreDatabase.bak")
    $dataFile = [IO.Path]::Combine($dataPath, "$restoreDatabase.mdf")
    $logFile = [IO.Path]::Combine($logPath, "${restoreDatabase}_log.ldf")

    $null = Invoke-NonQuery @"
BACKUP DATABASE [$sourceDatabase]
TO DISK=@backup
WITH COPY_ONLY, INIT, CHECKSUM, COMPRESSION;
"@ @{ backup = $backupFile }
    $null = Invoke-NonQuery 'RESTORE VERIFYONLY FROM DISK=@backup WITH CHECKSUM;' @{ backup = $backupFile }
    $null = Invoke-NonQuery @"
RESTORE DATABASE [$restoreDatabase]
FROM DISK=@backup
WITH MOVE @dataLogical TO @dataFile,
     MOVE @logLogical TO @logFile,
     RECOVERY;
"@ @{
        backup = $backupFile
        dataLogical = $dataLogical
        logLogical = $logLogical
        dataFile = $dataFile
        logFile = $logFile
    }
    $restoreCreated = $true
    $null = Invoke-NonQuery "DBCC CHECKDB ([$restoreDatabase]) WITH PHYSICAL_ONLY, NO_INFOMSGS;"

    $migrationCount = [int](Invoke-Scalar "SELECT COUNT(*) FROM [$restoreDatabase].dbo.__EFMigrationsHistory;")
    $transferCount = [int](Invoke-Scalar @"
SELECT COUNT(*) FROM [$restoreDatabase].dbo.InventoryTransfers
WHERE TenantId='00000000-0000-0000-0000-000000000001'
  AND TransferNumber='TRF26087751';
"@)
    $archiveCount = [int](Invoke-Scalar @"
SELECT COUNT(*) FROM [$restoreDatabase].dbo.AuditRecordLifecycleEvents
WHERE TenantId='00000000-0000-0000-0000-000000000001'
  AND StoreKey='procurement-inventory-control-event';
"@)
    $stopwatch.Stop()

    [pscustomobject]@{
        BackupVerify = 'Passed'
        FullRestore = 'Passed'
        CheckDbPhysicalOnly = 'Passed'
        DurationSeconds = [math]::Round($stopwatch.Elapsed.TotalSeconds, 1)
        MigrationCount = $migrationCount
        TransferCount = $transferCount
        ArchiveCount = $archiveCount
        BackupBytes = (Get-Item -LiteralPath $backupFile).Length
    } | ConvertTo-Json -Compress
}
finally {
    if ($restoreCreated) {
        $null = Invoke-NonQuery @"
ALTER DATABASE [$restoreDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [$restoreDatabase];
"@
    }
    $connection.Close()
    $connection.Dispose()

    if ($backupFile -and (Test-Path -LiteralPath $backupFile)) {
        $resolvedBackup = (Resolve-Path -LiteralPath $backupFile).Path
        $expectedDirectory = [IO.Path]::GetFullPath([IO.Path]::GetDirectoryName($backupFile))
        if (-not $resolvedBackup.StartsWith($expectedDirectory, [StringComparison]::OrdinalIgnoreCase) -or
            [IO.Path]::GetFileName($resolvedBackup) -ne "$restoreDatabase.bak") {
            throw 'Refusing to remove a backup outside the exact acceptance target.'
        }
        Remove-Item -LiteralPath $resolvedBackup -Force
    }
}
