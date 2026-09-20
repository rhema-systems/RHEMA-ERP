param([Parameter(Mandatory)][System.Data.Common.DbConnection]$Connection)
$ErrorActionPreference = 'Stop'
$rehearsalDatabase = 'RhemaERP_PO_Rehearsal_20260909'
if ($Connection.State -ne 'Open' -or $Connection.Database -ne 'RhemaERP') { throw 'An open local RhemaERP source connection is required.' }
function Invoke-RehearsalSql([string]$Sql, [hashtable]$Parameters = @{}, [switch]$Scalar) {
    $rehearsalCommand = $Connection.CreateCommand()
    try {
        $rehearsalCommand.CommandText = $Sql
        $rehearsalCommand.CommandTimeout = 300
        foreach ($entry in $Parameters.GetEnumerator()) { [void]$rehearsalCommand.Parameters.AddWithValue('@' + $entry.Key, $entry.Value) }
        if ($Scalar) { return $rehearsalCommand.ExecuteScalar() }
        [void]$rehearsalCommand.ExecuteNonQuery()
    } finally { $rehearsalCommand.Dispose() }
}
try {
    if ([string](Invoke-RehearsalSql "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))" -Scalar) -ne 'RHEMA-MICHAEL\SQL2017') { throw 'Unexpected SQL Server.' }
    if ([int](Invoke-RehearsalSql "SELECT COUNT(*) FROM sys.databases WHERE name='$rehearsalDatabase'" -Scalar) -ne 0) { throw 'Rehearsal database already exists; refusing to overwrite it.' }
    if ([int](Invoke-RehearsalSql 'SELECT COUNT(*) FROM sys.database_files' -Scalar) -ne 2) { throw 'Review the source file layout before restoring.' }
    $rehearsalDataLogical = [string](Invoke-RehearsalSql 'SELECT name FROM sys.database_files WHERE type=0' -Scalar)
    $rehearsalLogLogical = [string](Invoke-RehearsalSql 'SELECT name FROM sys.database_files WHERE type=1' -Scalar)
    $rehearsalDataRoot = [string](Invoke-RehearsalSql "SELECT CONVERT(nvarchar(260),SERVERPROPERTY('InstanceDefaultDataPath'))" -Scalar)
    $rehearsalLogRoot = [string](Invoke-RehearsalSql "SELECT CONVERT(nvarchar(260),SERVERPROPERTY('InstanceDefaultLogPath'))" -Scalar)
    $rehearsalBackupRoot = [string](Invoke-RehearsalSql "SELECT CONVERT(nvarchar(260),SERVERPROPERTY('InstanceDefaultBackupPath'))" -Scalar)
    $rehearsalBackup = Join-Path $rehearsalBackupRoot ($rehearsalDatabase + '.bak')
    $rehearsalData = Join-Path $rehearsalDataRoot ($rehearsalDatabase + '.mdf')
    $rehearsalLog = Join-Path $rehearsalLogRoot ($rehearsalDatabase + '_log.ldf')
    foreach ($rehearsalFile in @($rehearsalBackup, $rehearsalData, $rehearsalLog)) {
        if (Test-Path -LiteralPath $rehearsalFile) { throw 'A rehearsal backup/data target already exists; no overwrite permitted.' }
    }
    $rehearsalSourcePoHash = Invoke-RehearsalSql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM dbo.PurchaseOrders WHERE Id='a7393eec-565c-4ff5-8346-184315d8bc95' FOR JSON PATH)),2)" -Scalar
    $rehearsalSourceMigrations = [int](Invoke-RehearsalSql 'SELECT COUNT(*) FROM dbo.__EFMigrationsHistory' -Scalar)
    Invoke-RehearsalSql 'BACKUP DATABASE [RhemaERP] TO DISK=@backup WITH COPY_ONLY, CHECKSUM, COMPRESSION;' @{backup=$rehearsalBackup}
    Invoke-RehearsalSql 'RESTORE VERIFYONLY FROM DISK=@backup WITH CHECKSUM;' @{backup=$rehearsalBackup}
    $Connection.ChangeDatabase('master')
    Invoke-RehearsalSql "RESTORE DATABASE [$rehearsalDatabase] FROM DISK=@backup WITH MOVE @dataLogical TO @dataFile, MOVE @logLogical TO @logFile, RECOVERY;" @{
        backup=$rehearsalBackup; dataLogical=$rehearsalDataLogical; dataFile=$rehearsalData; logLogical=$rehearsalLogLogical; logFile=$rehearsalLog
    }
    Invoke-RehearsalSql "DBCC CHECKDB ([$rehearsalDatabase]) WITH PHYSICAL_ONLY, NO_INFOMSGS;"
    $Connection.ChangeDatabase($rehearsalDatabase)
    if ($Connection.Database -ne $rehearsalDatabase) { throw 'Clone isolation check failed.' }
    if ([int](Invoke-RehearsalSql 'SELECT COUNT(*) FROM dbo.__EFMigrationsHistory' -Scalar) -ne $rehearsalSourceMigrations) { throw 'Migration parity check failed.' }
    # Rehearsal-only settings. No original UAT records are updated.
    Invoke-RehearsalSql @'
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF DB_NAME()<>N'RhemaERP_PO_Rehearsal_20260909' THROW 51000,'Unexpected rehearsal database.',1;
UPDATE dbo.EmailSettings SET SmtpHost='',SmtpUsername='',SmtpPassword='',FromName='REHEARSAL - EMAIL DISABLED';
UPDATE dbo.SmsSettings SET TwilioEnabled=0,GhanaGatewayEnabled=0,TwilioAuthToken='',GhanaGatewayApiKey='',GhanaGatewayUrlTemplate='',FallbackProvidersJson='[]';
UPDATE dbo.Tenants SET LdapEnabled=0,Name=LEFT('REHEARSAL - '+Name,200);
COMMIT;
'@
    $rehearsalClonePoHash = Invoke-RehearsalSql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM dbo.PurchaseOrders WHERE Id='a7393eec-565c-4ff5-8346-184315d8bc95' FOR JSON PATH)),2)" -Scalar
    if ($rehearsalClonePoHash -ne $rehearsalSourcePoHash) { throw 'Copied PO differs from the source snapshot.' }
    $Connection.ChangeDatabase('RhemaERP')
    $rehearsalSourcePoHashAfter = Invoke-RehearsalSql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM dbo.PurchaseOrders WHERE Id='a7393eec-565c-4ff5-8346-184315d8bc95' FOR JSON PATH)),2)" -Scalar
    [pscustomobject]@{Database=$rehearsalDatabase;Backup=$rehearsalBackup;BackupVerified=$true;PhysicalCheckPassed=$true;MigrationCount=$rehearsalSourceMigrations;PoSnapshotMatches=$true;OriginalPoUnchanged=($rehearsalSourcePoHashAfter -eq $rehearsalSourcePoHash);EmailAndSmsDisabled=$true} | ConvertTo-Json -Compress
} finally { if ($Connection.State -eq 'Open') { $Connection.ChangeDatabase('RhemaERP') } }
