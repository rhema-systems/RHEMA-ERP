param([Parameter(Mandatory)][System.Data.Common.DbConnection]$SourceConnection)
$ErrorActionPreference = 'Stop'
$taskOriginalDatabase = $SourceConnection.Database
$taskDatabase = 'RhemaERP_ValidityTest_' + [guid]::NewGuid().ToString('N')
if ($taskDatabase -notmatch '^RhemaERP_ValidityTest_[a-f0-9]{32}$') { throw 'Invalid owned test database name.' }
function Invoke-TestSql([string]$sql) {
    $taskCommand = $SourceConnection.CreateCommand()
    try { $taskCommand.CommandText = $sql; $taskCommand.CommandTimeout = 60; [void]$taskCommand.ExecuteNonQuery() }
    finally { $taskCommand.Dispose() }
}
$taskCreated = $false
try {
    $SourceConnection.ChangeDatabase('master')
    Invoke-TestSql "CREATE DATABASE [$taskDatabase]"
    $taskCreated = $true
    $SourceConnection.ChangeDatabase($taskDatabase)
    Invoke-TestSql @'
CREATE TABLE dbo.Tenders (Id uniqueidentifier NOT NULL PRIMARY KEY, Status nvarchar(30) NOT NULL, BidValidityPeriodDays int NULL,
 CONSTRAINT CK_Tenders_BidValidityPeriodDays CHECK (BidValidityPeriodDays IS NULL OR BidValidityPeriodDays > 0));
CREATE TABLE dbo.ProcurementTenderDocumentRegisters (TenderId uniqueidentifier NOT NULL);
INSERT INTO dbo.Tenders VALUES ('255010bc-26f1-4dcf-9b4a-378932323cb0','Draft',30);
'@
    $taskMigration = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../src/ErpSystem.Data/Migrations/20260907180000_RecordTenderBidValidityTerms.cs') -Raw
    $taskTrigger = [regex]::Match($taskMigration, '(?s)CREATE OR ALTER TRIGGER dbo.TR_Tenders_BidValidityTerms_Immutable.*?END;').Value
    if (-not $taskTrigger) { throw 'Migration trigger not found.' }
    Invoke-TestSql $taskTrigger
    $taskCases = @(
        @{Name='Draft can set terms'; Sql='UPDATE dbo.Tenders SET BidValidityPeriodDays=60'; Pass=$true},
        @{Name='Draft can clear terms without inventing a default'; Sql='UPDATE dbo.Tenders SET BidValidityPeriodDays=NULL'; Pass=$true},
        @{Name='Terms survive submission unchanged'; Sql="UPDATE dbo.Tenders SET Status='Submitted'"; Pass=$true},
        @{Name='Changing terms while submitting is rejected'; Sql="UPDATE dbo.Tenders SET Status='Submitted',BidValidityPeriodDays=60"; Pass=$false},
        @{Name='Approved terms are immutable'; Sql="UPDATE dbo.Tenders SET Status='Approved'; UPDATE dbo.Tenders SET BidValidityPeriodDays=60"; Pass=$false},
        @{Name='Resetting approved status cannot change terms in the same write'; Sql="UPDATE dbo.Tenders SET Status='Approved'; UPDATE dbo.Tenders SET Status='Draft',BidValidityPeriodDays=60"; Pass=$false},
        @{Name='Bound draft terms are immutable'; Sql="INSERT dbo.ProcurementTenderDocumentRegisters SELECT Id FROM dbo.Tenders; UPDATE dbo.Tenders SET BidValidityPeriodDays=60"; Pass=$false},
        @{Name='Zero period rejected'; Sql='UPDATE dbo.Tenders SET BidValidityPeriodDays=0'; Pass=$false},
        @{Name='Negative period rejected'; Sql='UPDATE dbo.Tenders SET BidValidityPeriodDays=-1'; Pass=$false}
    )
    foreach ($taskCase in $taskCases) {
        Invoke-TestSql 'BEGIN TRANSACTION'
        $taskSucceeded = $true
        try { Invoke-TestSql $taskCase.Sql } catch { $taskSucceeded = $false }
        finally { Invoke-TestSql 'IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION' }
        if ($taskSucceeded -ne $taskCase.Pass) { throw "Guard scenario failed: $($taskCase.Name)" }
        "PASS: $($taskCase.Name)"
    }
    "PASSED: $($taskCases.Count) isolated SQL guard scenarios"
} finally {
    $SourceConnection.ChangeDatabase('master')
    if ($taskCreated) { Invoke-TestSql "ALTER DATABASE [$taskDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$taskDatabase]" }
    $SourceConnection.ChangeDatabase($taskOriginalDatabase)
}
