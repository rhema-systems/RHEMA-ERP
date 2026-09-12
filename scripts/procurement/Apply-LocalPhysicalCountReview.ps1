param(
    [Parameter(Mandatory)]
    [ValidateSet('RhemaERP', 'RhemaERP_PO_Rehearsal_20260909')]
    [string]$Database,
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data
$countRepo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$countMigrationId = '20260911210000_PhysicalCountReviewDecisions'
$countMigrationFile = Join-Path $countRepo "src/ErpSystem.Data/Migrations/$countMigrationId.cs"
$countMigrationSource = Get-Content -LiteralPath $countMigrationFile -Raw
$countSqlMatch = [regex]::Match($countMigrationSource, '(?s)public const string UpgradeSql = """\r?\n(.*?)\r?\n\s*""";')
if (-not $countSqlMatch.Success) { throw 'Cannot read the exact migration SQL.' }
$countUpgradeSql = (($countSqlMatch.Groups[1].Value -split '\r?\n') | ForEach-Object {
    if ($_.StartsWith('        ')) { $_.Substring(8) } else { $_ }
}) -join "`n"

# The existing local Windows login is used; no credential or connection secret is persisted.
$countConnection = [System.Data.SqlClient.SqlConnection]::new("Server=RHEMA-MICHAEL\SQL2017;Database=$Database;Integrated Security=True;Application Name=PhysicalCountReviewMigration")
try {
    $countConnection.Open()
    $countCommand = $countConnection.CreateCommand()
    $countCommand.CommandText = "SELECT CASE WHEN DB_NAME()=@database AND CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))=N'RHEMA-MICHAEL\SQL2017' THEN 1 ELSE 0 END"
    [void]$countCommand.Parameters.AddWithValue('@database', $Database)
    if ([int]$countCommand.ExecuteScalar() -ne 1) { throw 'Unexpected database/server. No changes applied.' }
    $countCommand.Parameters.Clear()
    $countCommand.CommandText = "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId='20260911210000_PhysicalCountReviewDecisions'"
    if ([int]$countCommand.ExecuteScalar() -gt 0) { Write-Output "Review migration already applied to $Database"; return }
    $countTransaction = $countConnection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
    $countCommand.Transaction = $countTransaction
    $countCommand.CommandTimeout = 60
    try {
        $countCommand.CommandText = 'SET XACT_ABORT ON;' + $countUpgradeSql
        [void]$countCommand.ExecuteNonQuery()
        $countCommand.CommandText = @'
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WITH (UPDLOCK,HOLDLOCK) WHERE MigrationId=@migration)
    INSERT dbo.__EFMigrationsHistory(MigrationId,ProductVersion) VALUES(@migration,N'8.0.0');
SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_PhysicalCountItems_ControlledMutation'))),2);
'@
        [void]$countCommand.Parameters.AddWithValue('@migration', $countMigrationId)
        $countTriggerHash = [string]$countCommand.ExecuteScalar()
        if ($Apply) { $countTransaction.Commit() } else { $countTransaction.Rollback() }
        [pscustomobject]@{ Database=$Database; Migration=$countMigrationId; Applied=[bool]$Apply; TriggerHash=$countTriggerHash; Scope='Count lifecycle, audit and freeze guards only; no count or stock records modified' }
    } catch {
        try { $countTransaction.Rollback() } catch { }
        throw
    } finally { $countTransaction.Dispose(); $countCommand.Dispose() }
} finally { $countConnection.Dispose() }
