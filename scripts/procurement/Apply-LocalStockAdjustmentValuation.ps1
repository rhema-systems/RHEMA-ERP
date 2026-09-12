param([switch]$Apply)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data
$costRepo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$costMigration = '20260912013000_AlignStockAdjustmentLocationValuation'
$costSource = Get-Content -LiteralPath (Join-Path $costRepo "src/ErpSystem.Data/Migrations/$costMigration.cs") -Raw
$costMatch = [regex]::Match($costSource, '(?s)public const string UpgradeSql = """\r?\n(.*?)\r?\n\s*""";')
if (-not $costMatch.Success) { throw 'Exact valuation migration SQL was not found.' }
$costConnection = [System.Data.SqlClient.SqlConnection]::new('Server=RHEMA-MICHAEL\SQL2017;Database=RhemaERP_PO_Rehearsal_20260909;Integrated Security=True;Application Name=StockAdjustmentValuationMigration')
try {
    $costConnection.Open()
    $costCommand = $costConnection.CreateCommand()
    $costCommand.CommandTimeout = 120
    $costCommand.CommandText = "SELECT CASE WHEN DB_NAME()=N'RhemaERP_PO_Rehearsal_20260909' AND CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))=N'RHEMA-MICHAEL\SQL2017' THEN 1 ELSE 0 END"
    if ([int]$costCommand.ExecuteScalar() -ne 1) { throw 'Unexpected target. No migration applied.' }
    $costCommand.CommandText = "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'$costMigration'"
    if ([int]$costCommand.ExecuteScalar() -gt 0) { Write-Output 'Rehearsal valuation migration already applied.'; return }
    if ($Apply) {
        $costCommand.CommandText = "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath'))"
        $costBackupDirectory = [string]$costCommand.ExecuteScalar()
        if ([string]::IsNullOrWhiteSpace($costBackupDirectory)) { throw 'SQL backup directory is unavailable.' }
        $costBackupPath = Join-Path $costBackupDirectory ('RhemaERP_Rehearsal_before_adjustment_valuation_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '.bak')
        $costCommand.CommandText = 'BACKUP DATABASE [RhemaERP_PO_Rehearsal_20260909] TO DISK=@path WITH COPY_ONLY, CHECKSUM; RESTORE VERIFYONLY FROM DISK=@path WITH CHECKSUM;'
        [void]$costCommand.Parameters.AddWithValue('@path', $costBackupPath)
        [void]$costCommand.ExecuteNonQuery()
        $costCommand.Parameters.Clear()
        Write-Output "Verified rehearsal backup: $costBackupPath"
    }
    $costTransaction = $costConnection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
    $costCommand.Transaction = $costTransaction
    try {
        $costCommand.CommandText = 'SET XACT_ABORT ON;' + $costMatch.Groups[1].Value
        [void]$costCommand.ExecuteNonQuery()
        $costCommand.CommandText = "INSERT dbo.__EFMigrationsHistory(MigrationId,ProductVersion) VALUES(N'$costMigration',N'8.0.0'); SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_StockAdjustmentItems_ControlledMutation'))),2);"
        $costHash = [string]$costCommand.ExecuteScalar()
        if ($Apply) { $costTransaction.Commit() } else { $costTransaction.Rollback() }
        [pscustomobject]@{Database='RhemaERP_PO_Rehearsal_20260909';Migration=$costMigration;Applied=[bool]$Apply;TriggerHash=$costHash;Scope='Valuation validation and unit-cost precision only; no count quantities, stock movements or approvals changed'}
    } catch { try {$costTransaction.Rollback()} catch {}; throw }
    finally {$costTransaction.Dispose();$costCommand.Dispose()}
} finally {$costConnection.Dispose()}
