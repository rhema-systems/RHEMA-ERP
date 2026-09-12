param([Parameter(Mandatory)][System.Data.Common.DbConnection]$SourceConnection)
$ErrorActionPreference = 'Stop'
if ($SourceConnection.State -ne [System.Data.ConnectionState]::Open) { throw 'An open connection is required.' }
if ($SourceConnection.Database -ne 'RhemaERP') {
    throw 'This repair is restricted to the verified local RhemaERP UAT database.'
}
$taskTransaction = $SourceConnection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
$taskCommand = $SourceConnection.CreateCommand()
$taskCommand.Transaction = $taskTransaction
$taskCommand.CommandTimeout = 60
try {
    # Apply only the matching EF migration, not unrelated pending module migrations.
    $taskCommand.CommandText = @'
SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'RHEMA-MICHAEL\SQL2017'
    THROW 51000, 'Unexpected SQL Server; no changes applied.', 1;
IF OBJECT_ID(N'dbo.ContractDocuments', N'U') IS NULL
    THROW 51000, 'ContractDocuments is missing.', 1;
IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ContractDocuments')
    AND name=N'ContentType' AND system_type_id=231 AND is_nullable=1 AND max_length IN (100,510,-1))
    THROW 51000, 'Unexpected ContractDocuments.ContentType schema; review before applying.', 1;
IF EXISTS (SELECT 1 FROM dbo.ContractDocuments WHERE DATALENGTH(ContentType)>510)
    THROW 51000, 'Contract document ContentType exceeds 255 characters; review before migrating.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WITH (UPDLOCK,HOLDLOCK)
    WHERE MigrationId=N'20260908200000_WidenContractDocumentContentType')
BEGIN
    ALTER TABLE dbo.ContractDocuments ALTER COLUMN ContentType nvarchar(255) NULL;
    INSERT dbo.__EFMigrationsHistory(MigrationId,ProductVersion)
    VALUES(N'20260908200000_WidenContractDocumentContentType',N'8.0.0');
END;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ContractDocuments')
    AND name=N'ContentType' AND max_length=510 AND is_nullable=1)
    THROW 51000, 'Migration history and ContentType definition disagree.', 1;
'@
    [void]$taskCommand.ExecuteNonQuery()
    $taskTransaction.Commit()
    'Verified: ContractDocuments.ContentType is nvarchar(255) NULL; migration history is recorded.'
} catch {
    try { $taskTransaction.Rollback() } catch { }
    throw
} finally {
    $taskCommand.Dispose()
    $taskTransaction.Dispose()
}
