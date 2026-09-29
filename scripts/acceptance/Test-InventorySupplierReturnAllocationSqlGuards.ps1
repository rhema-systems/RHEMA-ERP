<# Exact return-allocation SQL guards on an isolated schema copy; no application data is changed. #>
$ErrorActionPreference='Stop'
$run=[DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss')+'_'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$database='RhemaERP_ReturnGuard_'+$run
if($database -notmatch '^RhemaERP_ReturnGuard_\d{8}_\d{6}_[a-f0-9]{8}$'){throw 'Invalid isolated database name'}
$sourceDatabase='RhemaERP_ReceiptUpgrade_20260927_023608_918225d0'
$results=[Collections.Generic.List[object]]::new()
$evidence=Join-Path (Get-Location) "tmp/return-guards-$run.json"
$connection=[Data.SqlClient.SqlConnection]::new('Server=RHEMA-MICHAEL\SQL2017;Database=master;Integrated Security=True;TrustServerCertificate=True;')
function Run([string]$sql){$cmd=$connection.CreateCommand();$cmd.CommandText=$sql;try{[void]$cmd.ExecuteNonQuery()}finally{$cmd.Dispose()}}
function Scalar([string]$sql){$cmd=$connection.CreateCommand();$cmd.CommandText=$sql;try{return $cmd.ExecuteScalar()}finally{$cmd.Dispose()}}
function Check([string]$name,[string]$sql,[int]$expected=0){
 $actual=0;try{Run $sql}catch{$errorItem=$_.Exception;while($errorItem -and $errorItem -isnot [Data.SqlClient.SqlException]){$errorItem=$errorItem.InnerException};if(!$errorItem){throw};$actual=$errorItem.Number;if($actual -ne $expected){Write-Output $errorItem.Message}}
 $results.Add([pscustomobject]@{name=$name;passed=($actual -eq $expected);expected=$expected;actual=$actual});Write-Output "$name SQL=$actual expected=$expected"
}
function InsertRow([string]$table,[hashtable]$values){
 $command=$connection.CreateCommand();$command.CommandText="SELECT c.name,t.name AS TypeName,c.is_nullable,c.is_identity,c.is_computed FROM sys.columns c JOIN sys.types t ON t.user_type_id=c.user_type_id WHERE c.object_id=OBJECT_ID(N'dbo.$table') ORDER BY c.column_id"
 $schema=[Data.DataTable]::new();$reader=$command.ExecuteReader();$schema.Load($reader);$reader.Dispose();$command.Dispose()
 $columns=[Collections.Generic.List[string]]::new();$parameters=[Collections.Generic.List[string]]::new();$command=$connection.CreateCommand()
 foreach($col in $schema.Rows){
  if($col.is_identity -or $col.is_computed -or $col.TypeName -in @('timestamp','rowversion')){continue}
  $name=[string]$col.name
  if($values.ContainsKey($name)){$value=$values[$name]}elseif($col.is_nullable){continue}else{
   $value=switch([string]$col.TypeName){'uniqueidentifier'{[Guid]::NewGuid()};'bit'{$false};{$_ -in @('datetime','datetime2','date','smalldatetime')}{[DateTime]::UtcNow};{$_ -in @('int','smallint','tinyint','bigint','decimal','numeric','float','real','money')}{0};{$_ -in @('binary','varbinary')}{[byte[]]@(0)};default{'x'}}
  }
  $parameter='@p'+$parameters.Count;$columns.Add("[$name]");$parameters.Add($parameter);[void]$command.Parameters.AddWithValue($parameter,$value)
 }
 $command.CommandText="INSERT dbo.[$table] ($($columns -join ',')) VALUES ($($parameters -join ','))"
 try{[void]$command.ExecuteNonQuery()}finally{$command.Dispose()}
}

try {
 $connection.Open();if((Scalar "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))") -ine 'RHEMA-MICHAEL\SQL2017'){throw 'Wrong SQL instance'}
 Run "CREATE DATABASE [$database]";$connection.ChangeDatabase($database)
 foreach($table in @('PurchaseReturns','PurchaseReturnItems','GoodsReceiptNotes','GoodsReceiptNoteItems','PurchaseOrderReceiptItems','VendorInvoice','VendorInvoiceReceiptAllocations','FinancePostingEvents','JournalEntries','AccountTransactions','InventoryMovements','SupplierDebitNotes','SupplierDebitNoteLineItems','InventorySupplierReturnPostings')){Run "SELECT TOP(0) * INTO dbo.[$table] FROM [$sourceDatabase].dbo.[$table]"}
 Run @'
 ALTER TABLE dbo.PurchaseReturns ADD AccountingAllocationVersion int NOT NULL DEFAULT 0;
 ALTER TABLE dbo.SupplierDebitNotes ADD InventorySupplierReturnAccountingGroupId uniqueidentifier NULL;
 CREATE TABLE dbo.InventorySupplierReturnAccountingGroups(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier NOT NULL,InventoryPurchaseReturnId uniqueidentifier NOT NULL,OriginalVendorInvoiceId uniqueidentifier NULL,DispatchPostingEventId uniqueidentifier NULL,DispatchJournalEntryId uniqueidentifier NULL,ClearingAccountId uniqueidentifier NULL,CarryingAmount decimal(18,2) NOT NULL,OriginalAccrualAmount decimal(18,2) NOT NULL,CapturedAtUtc datetime2 NOT NULL,IsDeleted bit NOT NULL DEFAULT 0);
 CREATE TABLE dbo.InventorySupplierReturnAllocations(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier NOT NULL,InventoryPurchaseReturnId uniqueidentifier NOT NULL,InventoryPurchaseReturnItemId uniqueidentifier NOT NULL,GoodsReceiptNoteItemId uniqueidentifier NOT NULL,PurchaseOrderReceiptItemId uniqueidentifier NOT NULL,AccountingGroupId uniqueidentifier NOT NULL,VendorInvoiceReceiptAllocationId uniqueidentifier NULL,OriginalVendorInvoiceId uniqueidentifier NULL,OriginalVendorInvoiceLineItemId uniqueidentifier NULL,OriginalReceiptJournalEntryId uniqueidentifier NOT NULL,BaseQuantity decimal(18,4) NOT NULL,PurchaseQuantity decimal(18,4) NOT NULL,ConversionToBase decimal(18,8) NOT NULL,OriginalAccrualAmount decimal(18,2) NOT NULL,OriginalAccrualForeignAmount decimal(18,2) NOT NULL,CarryingAmount decimal(18,2) NOT NULL,IsDeleted bit NOT NULL DEFAULT 0);
 ALTER TABLE dbo.InventorySupplierReturnAllocations ADD ProcurementReceiptCostBasisId uniqueidentifier NULL,PurchaseCurrency nvarchar(3) NOT NULL DEFAULT 'GHS',FunctionalCurrency nvarchar(3) NOT NULL DEFAULT 'GHS';
 CREATE TABLE dbo.ProcurementReceiptCostBases(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier NOT NULL,IsDeleted bit NOT NULL DEFAULT 0,PurchaseOrderReceiptItemId uniqueidentifier NOT NULL,PurchaseOrderReceiptId uniqueidentifier NOT NULL,InventoryItemId uniqueidentifier NOT NULL,PurchaseCurrency nvarchar(3) NOT NULL,FunctionalCurrency nvarchar(3) NOT NULL,ConversionToBase decimal(18,6) NOT NULL,PurchaseAmount decimal(18,2) NOT NULL);
 CREATE TABLE dbo.InventorySupplierReturnAccrualShares(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier NOT NULL,InventorySupplierReturnAllocationId uniqueidentifier NOT NULL,OriginalReceiptAccountTransactionId uniqueidentifier NOT NULL,AccountId uniqueidentifier NOT NULL,Amount decimal(18,2) NOT NULL,IsDeleted bit NOT NULL DEFAULT 0);
 CREATE TABLE dbo.VendorInvoiceReceiptCostAllocations(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier NOT NULL,ReversalJournalEntryId uniqueidentifier NULL,IsDeleted bit NOT NULL DEFAULT 0,GoodsReceiptNoteItemId uniqueidentifier NULL,ReceiptForeignAmount decimal(18,2) NOT NULL DEFAULT 0);
 CREATE TABLE dbo.VendorInvoiceReceiptCostPostingLines(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier NOT NULL,CostAllocationId uniqueidentifier NOT NULL,OriginalReceiptAccountTransactionId uniqueidentifier NULL,Purpose nvarchar(20) NOT NULL,FunctionalAmount decimal(18,2) NOT NULL,IsDeleted bit NOT NULL DEFAULT 0);
'@
 $accountingBook=[Guid]::NewGuid();$tenant=[Guid]::NewGuid();$ret=[Guid]::NewGuid();$line=[Guid]::NewGuid();$grn=[Guid]::NewGuid();$grnLine=[Guid]::NewGuid();$receipt=[Guid]::NewGuid();$receiptLine=[Guid]::NewGuid();$po=[Guid]::NewGuid();$poLine=[Guid]::NewGuid();$supplier=[Guid]::NewGuid();$item=[Guid]::NewGuid();$warehouse=[Guid]::NewGuid();$invoice=[Guid]::NewGuid();$invoiceLine=[Guid]::NewGuid();$invoiceAllocation=[Guid]::NewGuid();$receiptJournal=[Guid]::NewGuid();$returnJournal=[Guid]::NewGuid();$invoiceJournal=[Guid]::NewGuid();$returnEvent=[Guid]::NewGuid();$accrual=[Guid]::NewGuid();$inventory=[Guid]::NewGuid();$clearing=[Guid]::NewGuid();$originalAccrual=[Guid]::NewGuid();$uninvGroup=[Guid]::NewGuid();$invGroup=[Guid]::NewGuid();$uninvAllocation=[Guid]::NewGuid();$invAllocation=[Guid]::NewGuid()
 InsertRow 'PurchaseReturns' @{Id=$ret;TenantId=$tenant;SupplierId=$supplier;WarehouseId=$warehouse;GoodsReceiptNoteId=$grn;PurchaseOrderId=$po;Status='Shipped';ShippedDate=[DateTime]::UtcNow;AccountingAllocationVersion=0}
 InsertRow 'GoodsReceiptNotes' @{Id=$grn;TenantId=$tenant;SupplierId=$supplier;WarehouseId=$warehouse;PurchaseOrderId=$po;PurchaseOrderReceiptId=$receipt;StockUpdated=$true}
 InsertRow 'GoodsReceiptNoteItems' @{Id=$grnLine;TenantId=$tenant;GoodsReceiptNoteId=$grn;PurchaseOrderItemId=$poLine;InventoryItemId=$item;AcceptedQuantity=[decimal]100}
 InsertRow 'PurchaseOrderReceiptItems' @{Id=$receiptLine;TenantId=$tenant;ReceiptId=$receipt;PurchaseOrderItemId=$poLine;AcceptedQuantity=[decimal]100}
 InsertRow 'PurchaseReturnItems' @{Id=$line;TenantId=$tenant;PurchaseReturnId=$ret;GoodsReceiptNoteItemId=$grnLine;InventoryItemId=$item;ReturnQuantity=[decimal]50;StockReversed=$true}
 InsertRow 'VendorInvoice' @{Id=$invoice;TenantId=$tenant;BusinessPartnerId=$supplier;PurchaseOrderId=$po;JournalEntryId=$invoiceJournal;Status=3}
 InsertRow 'VendorInvoiceReceiptAllocations' @{Id=$invoiceAllocation;TenantId=$tenant;VendorInvoiceId=$invoice;VendorInvoiceLineItemId=$invoiceLine;GoodsReceiptNoteItemId=$grnLine;PurchaseOrderReceiptItemId=$receiptLine;Quantity=[decimal]60}
 InsertRow 'JournalEntries' @{Id=$receiptJournal;TenantId=$tenant;AccountingBookId=$accountingBook;PostingStatus='Posted';SourceDocumentType='ProcurementPurchaseOrderReceipt';SourceDocumentId=$receipt}
 InsertRow 'JournalEntries' @{Id=$invoiceJournal;TenantId=$tenant;AccountingBookId=$accountingBook;PostingStatus='Posted';SourceDocumentType='VendorInvoice';SourceDocumentId=$invoice}
 InsertRow 'JournalEntries' @{Id=$returnJournal;TenantId=$tenant;AccountingBookId=$accountingBook;PostingStatus='Posted';SourceDocumentType='SupplierReturnDispatch';SourceDocumentId=$ret}
 InsertRow 'FinancePostingEvents' @{Id=$returnEvent;TenantId=$tenant;PostingStatus='Posted';SourceDocumentType='SupplierReturnDispatch';SourceDocumentId=$ret;JournalEntryId=$returnJournal}
 InsertRow 'AccountTransactions' @{Id=$originalAccrual;TenantId=$tenant;JournalEntryId=$receiptJournal;AccountId=$accrual;SourceDocumentLineId=$item;PostingStatus='Posted';CreditAmount=[decimal]1000;TransactionTag='INV-RECEIPT-GRV-ACCRUAL'}
 foreach($posting in @(@{AccountId=$inventory;CreditAmount=[decimal]500;TransactionTag='RTV-Dispatch-Inventory'},@{AccountId=$accrual;DebitAmount=[decimal]400;TransactionTag='RTV-Uninvoiced-Accrual'},@{AccountId=$clearing;DebitAmount=[decimal]100;TransactionTag='RTV-Dispatch-Clearing'})){$posting.Id=[Guid]::NewGuid();$posting.TenantId=$tenant;$posting.JournalEntryId=$returnJournal;$posting.PostingStatus='Posted';InsertRow 'AccountTransactions' $posting}
 InsertRow 'InventoryMovements' @{Id=[Guid]::NewGuid();TenantId=$tenant;InventoryItemId=$item;WarehouseId=$warehouse;ReferenceType=7;ReferenceId=$ret;MovementType=10;Direction=2;Quantity=[decimal]50;TotalValue=[decimal]500;IsPosted=$true}
 $cost=[Guid]::NewGuid();InsertRow 'VendorInvoiceReceiptCostAllocations' @{Id=$cost;TenantId=$tenant};InsertRow 'VendorInvoiceReceiptCostPostingLines' @{Id=[Guid]::NewGuid();TenantId=$tenant;CostAllocationId=$cost;OriginalReceiptAccountTransactionId=$originalAccrual;Purpose='Accrual';FunctionalAmount=[decimal]600}
 $source=Get-Content src/ErpSystem.Data/Migrations/InventorySupplierReturnAllocationGuards.cs -Raw
 $legacy=Scalar "SELECT definition FROM [$sourceDatabase].sys.sql_modules WHERE object_id=(SELECT object_id FROM [$sourceDatabase].sys.triggers WHERE name='TR_SupplierDebitNotes_InventoryReturnCreditGuard')"
 if(!$legacy){throw 'Retained legacy credit guard missing'};Run $legacy
 foreach($name in @('GroupGuard','AllocationGuard','AccrualGuard','SealGuard','CreditGuard','CreditLineGuard')){$m=[regex]::Match($source,'(?s)internal const string '+$name+' = """\r?\n(.*?)\r?\n\s*""";');if(!$m.Success){throw "Missing guard $name"};Run $m.Groups[1].Value;$results.Add([pscustomobject]@{name="Compile/$name";passed=$true})}
 $patch=[regex]::Match($source,'(?s)internal const string LegacyGuardPatch = """\r?\n(.*?)\r?\n\s*""";');Run $patch.Groups[1].Value;$results.Add([pscustomobject]@{name='Compile/LegacyGuardPatch';passed=$true})
 $groupSql="INSERT dbo.InventorySupplierReturnAccountingGroups VALUES('{0}','$tenant','$ret',{1},'$returnEvent','$returnJournal',{2},{3},{4},SYSUTCDATETIME(),0)"
 Check 'Capture uninvoiced group' ($groupSql -f $uninvGroup,'NULL','NULL',400,400)
 Run "UPDATE dbo.JournalEntries SET IsReversed=1 WHERE Id='$invoiceJournal'"
 Check 'Reject reversed original invoice' ($groupSql -f $invGroup,"'$invoice'","'$clearing'",100,0) 52841
 Run "UPDATE dbo.JournalEntries SET IsReversed=0,AccountingBookId=NEWID() WHERE Id='$invoiceJournal'"
 Check 'Reject different original invoice book' ($groupSql -f $invGroup,"'$invoice'","'$clearing'",100,0) 52841
 Run "UPDATE dbo.JournalEntries SET AccountingBookId='$accountingBook' WHERE Id='$invoiceJournal'"
 Check 'Capture original invoice group' ($groupSql -f $invGroup,"'$invoice'","'$clearing'",100,0)
 Check 'Group cannot be changed' "UPDATE dbo.InventorySupplierReturnAccountingGroups SET CarryingAmount=999 WHERE Id='$invGroup'" 52840
 Check 'Cannot seal incomplete claims' "UPDATE dbo.PurchaseReturns SET AccountingAllocationVersion=1 WHERE Id='$ret'" 52853
 $allocationSql="INSERT dbo.InventorySupplierReturnAllocations VALUES('{0}','$tenant','$ret','$line','$grnLine','$receiptLine','{1}',{2},{3},{4},'$receiptJournal',{5},{5},1,{6},{6},{7},0,NULL,'GHS','GHS')"
 $basisId=[Guid]::NewGuid()
 InsertRow 'ProcurementReceiptCostBases' @{Id=$basisId;TenantId=$tenant;PurchaseOrderReceiptItemId=$receiptLine;PurchaseOrderReceiptId=$receipt;InventoryItemId=$item;PurchaseCurrency='GHS';FunctionalCurrency='GHS';ConversionToBase=[decimal]1;PurchaseAmount=[decimal]1000}
 Run "UPDATE dbo.VendorInvoiceReceiptCostAllocations SET GoodsReceiptNoteItemId='$grnLine',ReceiptForeignAmount=600 WHERE Id='$cost'"
 $basisAllocationSql=$allocationSql.Replace("0,NULL,'GHS','GHS')","0,'$basisId','GHS','GHS')")
 Check 'Original foreign amount cannot be overcleared after invoice' ($basisAllocationSql -f $uninvAllocation,$uninvGroup,'NULL','NULL','NULL',40,401,400) 52864
 Check 'Cannot omit retained receipt basis' ($allocationSql -f $uninvAllocation,$uninvGroup,'NULL','NULL','NULL',40,400,400) 52843
 $allocationSql=$basisAllocationSql
 Check 'Capture40 uninvoiced units' ($allocationSql -f $uninvAllocation,$uninvGroup,'NULL','NULL','NULL',40,400,400)
 Check 'Capture10 invoiced units' ($allocationSql -f $invAllocation,$invGroup,"'$invoiceAllocation'","'$invoice'","'$invoiceLine'",10,0,100)
 Check 'Additional cumulative overreturn blocked' ($allocationSql -f ([Guid]::NewGuid()),$invGroup,"'$invoiceAllocation'","'$invoice'","'$invoiceLine'",1,0,10) 52844
 Check 'Allocation immutable' "UPDATE dbo.InventorySupplierReturnAllocations SET BaseQuantity=9 WHERE Id='$invAllocation'" 52842
 Check 'Wrong original account blocked' "INSERT dbo.InventorySupplierReturnAccrualShares VALUES(NEWID(),'$tenant','$uninvAllocation','$originalAccrual','$inventory',400,0)" 52848
 Run "UPDATE dbo.AccountTransactions SET SourceDocumentLineId=NEWID() WHERE Id='$originalAccrual'"
 Check 'Wrong original receipt item blocked' "INSERT dbo.InventorySupplierReturnAccrualShares VALUES(NEWID(),'$tenant','$uninvAllocation','$originalAccrual','$accrual',400,0)" 52848
 Run "UPDATE dbo.AccountTransactions SET SourceDocumentLineId='$item' WHERE Id='$originalAccrual'"
 Check 'Accrual excessive amount blocked' "INSERT dbo.InventorySupplierReturnAccrualShares VALUES(NEWID(),'$tenant','$uninvAllocation','$originalAccrual','$accrual',401,0)" 52849
 Check 'Exact remaining400 GRNI' "INSERT dbo.InventorySupplierReturnAccrualShares VALUES(NEWID(),'$tenant','$uninvAllocation','$originalAccrual','$accrual',400,0)"
 Check 'Complete allocation seal' "UPDATE dbo.PurchaseReturns SET AccountingAllocationVersion=1 WHERE Id='$ret'"
 Check 'Seal immutable' "UPDATE dbo.PurchaseReturns SET AccountingAllocationVersion=0 WHERE Id='$ret'" 52851
 Check 'No claims after seal' ($allocationSql -f ([Guid]::NewGuid()),$invGroup,"'$invoiceAllocation'","'$invoice'","'$invoiceLine'",1,0,10) 52843
 Check 'Retained share cannot be removed' "DELETE dbo.InventorySupplierReturnAccrualShares" 52847
 $note=[Guid]::NewGuid();InsertRow 'SupplierDebitNotes' @{Id=$note;TenantId=$tenant;InventoryPurchaseReturnId=$ret;InventorySupplierReturnAccountingGroupId=$invGroup;OriginalVendorInvoiceId=$invoice;VendorId=$supplier;Status=0}
 Check 'Credit group cannot be changed' "UPDATE dbo.SupplierDebitNotes SET InventorySupplierReturnAccountingGroupId='$uninvGroup' WHERE Id='$note'" 52854
 $noteLine=[Guid]::NewGuid();InsertRow 'SupplierDebitNoteLineItems' @{Id=$noteLine;TenantId=$tenant;SupplierDebitNoteId=$note;OriginalVendorInvoiceLineItemId=$invoiceLine;Quantity=[decimal]10;UnitPrice=[decimal]10;LineTotal=[decimal]100}
 Check 'Credit qty immutable' "UPDATE dbo.SupplierDebitNoteLineItems SET Quantity=11 WHERE Id='$noteLine'" 52857
 Check 'Credit source line cannot be deleted' "DELETE dbo.SupplierDebitNoteLineItems WHERE Id='$noteLine'" 52857
 Check 'Cannot bypass group using whole-return credit' "UPDATE dbo.SupplierDebitNotes SET InventorySupplierReturnAccountingGroupId=NULL WHERE Id='$note'" 52863
 $legacyRet=[Guid]::NewGuid();InsertRow 'PurchaseReturns' @{Id=$legacyRet;TenantId=$tenant;SupplierId=$supplier;WarehouseId=$warehouse;GoodsReceiptNoteId=$grn;PurchaseOrderId=$po;Status='Shipped';ShippedDate=[DateTime]::UtcNow;AccountingAllocationVersion=0}
 $legacyNote=[Guid]::NewGuid();InsertRow 'SupplierDebitNotes' @{Id=$legacyNote;TenantId=$tenant;InventoryPurchaseReturnId=$legacyRet;OriginalVendorInvoiceId=$invoice;VendorId=$supplier;Status=0}
 Check 'Legacy whole-return guard remains active' "UPDATE dbo.SupplierDebitNotes SET VendorId=NEWID() WHERE Id='$legacyNote'" 51982
 # Independent sessions race for the same accepted receipt, using distinct return
 # documents so document-local locks cannot masquerade as receipt serialization.
 $raceStatements=@()
 foreach($race in 1..2){
  $raceReturn=[Guid]::NewGuid();$raceLine=[Guid]::NewGuid();$raceJournal=[Guid]::NewGuid();$raceEvent=[Guid]::NewGuid();$raceGroup=[Guid]::NewGuid()
  InsertRow 'PurchaseReturns' @{Id=$raceReturn;TenantId=$tenant;SupplierId=$supplier;WarehouseId=$warehouse;GoodsReceiptNoteId=$grn;PurchaseOrderId=$po;Status='Shipped';ShippedDate=[DateTime]::UtcNow;AccountingAllocationVersion=0}
  InsertRow 'PurchaseReturnItems' @{Id=$raceLine;TenantId=$tenant;PurchaseReturnId=$raceReturn;GoodsReceiptNoteItemId=$grnLine;InventoryItemId=$item;ReturnQuantity=[decimal]40;StockReversed=$true}
  InsertRow 'JournalEntries' @{Id=$raceJournal;TenantId=$tenant;AccountingBookId=$accountingBook;PostingStatus='Posted';SourceDocumentType='SupplierReturnDispatch';SourceDocumentId=$raceReturn}
  InsertRow 'FinancePostingEvents' @{Id=$raceEvent;TenantId=$tenant;PostingStatus='Posted';SourceDocumentType='SupplierReturnDispatch';SourceDocumentId=$raceReturn;JournalEntryId=$raceJournal}
  Run "INSERT dbo.InventorySupplierReturnAccountingGroups VALUES('$raceGroup','$tenant','$raceReturn','$invoice','$raceEvent','$raceJournal','$clearing',400,0,SYSUTCDATETIME(),0)"
  $raceStatements += "INSERT dbo.InventorySupplierReturnAllocations VALUES(NEWID(),'$tenant','$raceReturn','$raceLine','$grnLine','$receiptLine','$raceGroup','$invoiceAllocation','$invoice','$invoiceLine','$receiptJournal',40,40,1,0,0,400,0,'$basisId','GHS','GHS')"
 }
 $sessionA=[Data.SqlClient.SqlConnection]::new($connection.ConnectionString);$sessionB=[Data.SqlClient.SqlConnection]::new($connection.ConnectionString)
 try {
  $sessionA.Open();$sessionB.Open();$sessionA.ChangeDatabase($database);$sessionB.ChangeDatabase($database)
  if($sessionA.Database -ne $database -or $sessionB.Database -ne $database){throw 'Concurrent sessions must use isolated fixture database'}
  $sessionProbe=$sessionB.CreateCommand();$sessionProbe.CommandText='SELECT @@SPID';$sessionBId=[int]$sessionProbe.ExecuteScalar();$sessionProbe.Dispose()
  $txA=$sessionA.BeginTransaction();$txB=$sessionB.BeginTransaction()
  $commandA=$sessionA.CreateCommand();$commandA.Transaction=$txA;$commandA.CommandText=$raceStatements[0];[void]$commandA.ExecuteNonQuery()
  $commandB=$sessionB.CreateCommand();$commandB.Transaction=$txB;$commandB.CommandText=$raceStatements[1];$commandB.CommandTimeout=30
  $pending=$commandB.ExecuteNonQueryAsync();$waitType='';$deadline=[DateTime]::UtcNow.AddSeconds(5)
  while(!$pending.IsCompleted -and [DateTime]::UtcNow -lt $deadline){
   $waitType=[string](Scalar "SELECT wait_type FROM sys.dm_exec_requests WHERE session_id=$sessionBId")
   if($waitType -like 'LCK_M*'){break};Start-Sleep -Milliseconds 50
  }
  $results.Add([pscustomobject]@{name='Competing return waits on original receipt lock';passed=($waitType -like 'LCK_M*');waitType=$waitType})
  $txA.Commit();$raceError=0
  try{[void]$pending.GetAwaiter().GetResult();$txB.Commit()}catch{$failure=$_.Exception;while($failure -and $failure -isnot [Data.SqlClient.SqlException]){$failure=$failure.InnerException};if(!$failure){throw};$raceError=$failure.Number;try{$txB.Rollback()}catch{}}
  $claimed=[decimal](Scalar "SELECT SUM(BaseQuantity) FROM dbo.InventorySupplierReturnAllocations WHERE TenantId='$tenant' AND GoodsReceiptNoteItemId='$grnLine'")
  $results.Add([pscustomobject]@{name='Competing cumulative overreturn rejected after first commit';passed=($raceError -eq 52845 -and $claimed -eq 90);actual=$raceError;committedQuantity=$claimed})
 } finally {if($pending -and !$pending.IsCompleted){$commandB.Cancel();try{[void]$pending.GetAwaiter().GetResult()}catch{}};if($commandA){$commandA.Dispose()};if($commandB){$commandB.Dispose()};if($txA){$txA.Dispose()};if($txB){$txB.Dispose()};$sessionA.Dispose();$sessionB.Dispose()}
} catch {$results.Add([pscustomobject]@{name='Fixture';passed=$false;error=$_.Exception.Message});Write-Output $_.Exception.Message}
finally {if($connection){$connection.Dispose()};[pscustomobject]@{database=$database;scope='Exact SQL guards on isolated copied schema; not full EF migration or service lifecycle acceptance';results=$results}|ConvertTo-Json -Depth 5|Set-Content $evidence;Write-Output "EVIDENCE|$evidence"}
if(@($results|Where-Object {!$_.passed}).Count){exit 1}
