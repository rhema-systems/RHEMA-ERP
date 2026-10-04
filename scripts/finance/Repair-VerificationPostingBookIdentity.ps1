param([switch]$Apply)
$ErrorActionPreference='Stop'
# Restore the C1 shape already present in the current-model baseline. This tool is
# intentionally restricted to the named verification clone; it cannot target production.
$taskRepo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $taskRepo 'tmp/procurement-verification-db.ps1')
$taskDb=Open-ProcurementVerificationConnection
if($taskDb.Database -cne 'RhemaERP_Procurement_Verification_20260925'){throw 'Verification clone required.'}
$taskTx=$taskDb.BeginTransaction([System.Data.IsolationLevel]::Serializable)
function Invoke-RepairSql([string]$Sql){
 $taskCmd=$taskDb.CreateCommand();$taskCmd.Transaction=$taskTx;$taskCmd.CommandTimeout=120
 try{$taskCmd.CommandText=$Sql;return $taskCmd.ExecuteScalar()}finally{$taskCmd.Dispose()}
}
function Get-RepairHash([string]$Table,[string]$Columns){
 return [string](Invoke-RepairSql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT $Columns FROM dbo.[$Table] ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)),2)")
}
try {
 $taskTables=@('JournalEntries','AccountTransactions','FinancePostingEvents')
 $taskOldColumns=@{};$taskOldHashes=@{};$taskCounts=@{}
 foreach($taskTable in @($taskTables)+@('AccountingBooks','BusinessPartners','FinanceSettings')){
  $taskOldColumns[$taskTable]=[string](Invoke-RepairSql "SELECT STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(name)),N',') WITHIN GROUP (ORDER BY column_id) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.$taskTable')")
  $taskOldHashes[$taskTable]=Get-RepairHash $taskTable $taskOldColumns[$taskTable]
  $taskCounts[$taskTable]=[int](Invoke-RepairSql "SELECT COUNT(*) FROM dbo.[$taskTable]")
 }
 $taskExisting=[int](Invoke-RepairSql "SELECT COUNT(*) FROM sys.columns WHERE (object_id IN (OBJECT_ID(N'dbo.JournalEntries'),OBJECT_ID(N'dbo.AccountTransactions'),OBJECT_ID(N'dbo.FinancePostingEvents')) AND name=N'AccountingBookId') OR (object_id=OBJECT_ID(N'dbo.FinancePostingEvents') AND name IN (N'RequestFingerprint',N'RequestFingerprintVersion'))")
 if($taskExisting -notin @(0,5)){throw 'Partial book identity schema requires review; no changes applied.'}
 if($taskExisting -eq 0){
  # The code snapshot is authoritative. Do not default, normalize or infer a book.
  foreach($taskTable in $taskTables){
   [void](Invoke-RepairSql @"
IF EXISTS (SELECT 1 FROM dbo.[$taskTable] r WHERE
 NULLIF(LTRIM(RTRIM(r.BookClassification)),N'') IS NULL OR
 UPPER(LTRIM(RTRIM(r.BookClassification))) IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS') OR
 (SELECT COUNT_BIG(*) FROM dbo.AccountingBooks b WHERE b.TenantId=r.TenantId AND b.IsDeleted=0
  AND b.Code COLLATE Latin1_General_100_BIN2=r.BookClassification COLLATE Latin1_General_100_BIN2
  AND DATALENGTH(b.Code)=DATALENGTH(r.BookClassification)
  AND b.Code COLLATE Latin1_General_100_BIN2=UPPER(LTRIM(RTRIM(b.Code))) COLLATE Latin1_General_100_BIN2
  AND DATALENGTH(b.Code)=DATALENGTH(UPPER(LTRIM(RTRIM(b.Code)))))<>1)
 THROW 51740,'Retained book code must resolve exactly to one same-tenant accounting book.',1;
"@)
  }
  foreach($taskTable in $taskTables){[void](Invoke-RepairSql "ALTER TABLE dbo.[$taskTable] ADD AccountingBookId uniqueidentifier NULL;")}
  [void](Invoke-RepairSql 'ALTER TABLE dbo.FinancePostingEvents ADD RequestFingerprint nvarchar(64) NULL, RequestFingerprintVersion nvarchar(40) NULL;')
  foreach($taskTable in $taskTables){
   [void](Invoke-RepairSql "UPDATE r SET AccountingBookId=b.Id FROM dbo.[$taskTable] r JOIN dbo.AccountingBooks b ON b.TenantId=r.TenantId AND b.IsDeleted=0 AND b.Code COLLATE Latin1_General_100_BIN2=r.BookClassification COLLATE Latin1_General_100_BIN2 AND DATALENGTH(b.Code)=DATALENGTH(r.BookClassification); ALTER TABLE dbo.[$taskTable] ALTER COLUMN AccountingBookId uniqueidentifier NOT NULL;")
  }
 }
 # Validate identities and historical lineage even on a repeat execution.
 foreach($taskTable in $taskTables){
  [void](Invoke-RepairSql "IF EXISTS(SELECT 1 FROM dbo.[$taskTable] r LEFT JOIN dbo.AccountingBooks b ON b.Id=r.AccountingBookId AND b.TenantId=r.TenantId WHERE b.Id IS NULL OR b.Code COLLATE Latin1_General_100_BIN2<>r.BookClassification COLLATE Latin1_General_100_BIN2 OR DATALENGTH(b.Code)<>DATALENGTH(r.BookClassification)) THROW 51741,'Posting book identity does not match the retained snapshot.',1;")
 }
 foreach($taskTable in @('AccountTransactions','FinancePostingEvents')){
  [void](Invoke-RepairSql "IF EXISTS(SELECT 1 FROM dbo.[$taskTable] r LEFT JOIN dbo.JournalEntries j ON j.Id=r.JournalEntryId AND j.TenantId=r.TenantId AND j.AccountingBookId=r.AccountingBookId WHERE r.JournalEntryId IS NOT NULL AND j.Id IS NULL) THROW 51742,'Posting evidence has inconsistent journal lineage.',1;")
 }
 foreach($taskKey in @(
  @('AccountingBooks','AK_AccountingBooks_TenantId_Id','TenantId,Id'),
  @('JournalEntries','AK_JournalEntries_TenantId_Id_AccountingBookId','TenantId,Id,AccountingBookId'),
  @('FinancePostingEvents','AK_FinancePostingEvents_TenantId_Id','TenantId,Id'))){
  [void](Invoke-RepairSql "IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.$($taskKey[0])') AND name=N'$($taskKey[1])') ALTER TABLE dbo.[$($taskKey[0])] ADD CONSTRAINT [$($taskKey[1])] UNIQUE($($taskKey[2]));")
 }
 foreach($taskTable in $taskTables){
  $taskFk="FK_${taskTable}_AccountingBooks_TenantId_AccountingBookId"
  [void](Invoke-RepairSql "IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'$taskFk') ALTER TABLE dbo.[$taskTable] WITH CHECK ADD CONSTRAINT [$taskFk] FOREIGN KEY(TenantId,AccountingBookId) REFERENCES dbo.AccountingBooks(TenantId,Id); ALTER TABLE dbo.[$taskTable] WITH CHECK CHECK CONSTRAINT [$taskFk];")
 }
 foreach($taskTable in @('AccountTransactions','FinancePostingEvents')){
  $taskFk="FK_${taskTable}_JournalEntries_TenantId_JournalEntryId_AccountingBookId"
  [void](Invoke-RepairSql "IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'$taskFk') ALTER TABLE dbo.[$taskTable] WITH CHECK ADD CONSTRAINT [$taskFk] FOREIGN KEY(TenantId,JournalEntryId,AccountingBookId) REFERENCES dbo.JournalEntries(TenantId,Id,AccountingBookId); ALTER TABLE dbo.[$taskTable] WITH CHECK CHECK CONSTRAINT [$taskFk];")
 }
 foreach($taskIndex in @(
  @('JournalEntries','TenantId_AccountingBookId','TenantId,AccountingBookId','',''),
  @('AccountTransactions','TenantId_JournalEntryId_AccountingBookId','TenantId,JournalEntryId,AccountingBookId','',''),
  @('AccountTransactions','TenantId_AccountingBookId_TransactionDate_AccountId','TenantId,AccountingBookId,TransactionDate,AccountId','',''),
  @('FinancePostingEvents','TenantId_JournalEntryId_AccountingBookId','TenantId,JournalEntryId,AccountingBookId','',''),
  @('FinancePostingEvents','TenantId_AccountingBookId_IdempotencyKey','TenantId,AccountingBookId,IdempotencyKey','UNIQUE','WHERE IsDeleted=0 AND IdempotencyKey IS NOT NULL'),
  @('FinancePostingEvents','TenantId_AccountingBookId_SourceDocumentType_SourceDocumentId_PostingAction','TenantId,AccountingBookId,SourceDocumentType,SourceDocumentId,PostingAction','UNIQUE','WHERE IsDeleted=0'),
  @('FinancePostingEvents','TenantId_AccountingBookId_SourceModule_SourceDocumentType_SourceDocumentId_PostingAction','TenantId,AccountingBookId,SourceModule,SourceDocumentType,SourceDocumentId,PostingAction','UNIQUE','WHERE IsDeleted=0'))){
  $taskName="IX_$($taskIndex[0])_$($taskIndex[1])"
  [void](Invoke-RepairSql "IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.$($taskIndex[0])') AND name=N'$taskName') CREATE $($taskIndex[3]) INDEX [$taskName] ON dbo.[$($taskIndex[0])]($($taskIndex[2])) $($taskIndex[4]);")
 }
 # Remove predecessor constraints only after their tenant/book-qualified replacements exist.
 foreach($taskTable in @('AccountTransactions','FinancePostingEvents')){
  $taskFk="FK_${taskTable}_JournalEntries_JournalEntryId"
  [void](Invoke-RepairSql "IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'$taskFk') ALTER TABLE dbo.[$taskTable] DROP CONSTRAINT [$taskFk];")
 }
 foreach($taskIndex in @('TenantId_IdempotencyKey','TenantId_SourceDocumentType_SourceDocumentId_PostingAction','TenantId_SourceModule_SourceDocumentType_SourceDocumentId_PostingAction')){
  [void](Invoke-RepairSql "IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.FinancePostingEvents') AND name=N'IX_FinancePostingEvents_$taskIndex') DROP INDEX [IX_FinancePostingEvents_$taskIndex] ON dbo.FinancePostingEvents;")
 }
 foreach($taskTable in $taskOldHashes.Keys){
  if((Get-RepairHash $taskTable $taskOldColumns[$taskTable]) -cne $taskOldHashes[$taskTable]){throw "Existing data changed in $taskTable; rolling back."}
 }
 if($Apply){$taskTx.Commit()}else{$taskTx.Rollback()}
 [pscustomobject]@{Database=$taskDb.Database;Applied=[bool]$Apply;AlreadyHadColumns=($taskExisting -eq 5);ExistingDataUnchanged=$true;Rows=$taskCounts;HistoricalFingerprintsFabricated=$false;VerifiedAtUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json -Depth 4
}catch{try{$taskTx.Rollback()}catch{};throw}finally{$taskTx.Dispose();$taskDb.Dispose()}
