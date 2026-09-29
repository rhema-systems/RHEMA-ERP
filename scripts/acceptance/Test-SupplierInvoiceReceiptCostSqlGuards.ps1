<# Exact supplier invoice cost SQL guards on an isolated schema copy; no application data is changed. #>
$ErrorActionPreference='Stop'
$run=[DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss')+'_'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$database='RhemaERP_InvoiceCostGuard_'+$run
if($database -notmatch '^RhemaERP_InvoiceCostGuard_\d{8}_\d{6}_[a-f0-9]{8}$'){throw 'Invalid isolated database name'}
$sourceDatabase='RhemaERP_ReceiptUpgrade_20260927_023608_918225d0'
$results=[Collections.Generic.List[object]]::new()
$evidence=Join-Path (Get-Location) "tmp/invoice-cost-guards-$run.json"
$connection=[Data.SqlClient.SqlConnection]::new('Server=RHEMA-MICHAEL\SQL2017;Database=master;Integrated Security=True;TrustServerCertificate=True;')
function Run([string]$sql){$cmd=$connection.CreateCommand();$cmd.CommandText=$sql;try{[void]$cmd.ExecuteNonQuery()}finally{$cmd.Dispose()}}
function Scalar([string]$sql){$cmd=$connection.CreateCommand();$cmd.CommandText=$sql;try{return $cmd.ExecuteScalar()}finally{$cmd.Dispose()}}
function Check([string]$name,[string]$sql,[int]$expected=0){
 $actual=0;try{Run $sql}catch{$errorItem=$_.Exception;while($errorItem -and $errorItem -isnot [Data.SqlClient.SqlException]){$errorItem=$errorItem.InnerException};if(!$errorItem){throw};$actual=$errorItem.Number;}
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


function EntityTable([string]$path,[string]$entity,[string]$table){
 $source=Get-Content $path -Raw
 $body=[regex]::Match($source,'(?s)public sealed class '+$entity+' : TenantEntity\s*\{(.*?)(?=\r?\n\})').Groups[1].Value
 if(!$body){throw 'Cannot identify fixture entity'}
 $columns=[Collections.Generic.List[string]]::new()
 $columns.Add('Id uniqueidentifier NOT NULL PRIMARY KEY');$columns.Add('TenantId uniqueidentifier NOT NULL');$columns.Add('IsDeleted bit NOT NULL DEFAULT 0')
 foreach($property in [regex]::Matches($body,'(?m)^\s*(?:\[(?<attributes>[^\r\n]+)\]\s*)?public (?<type>Guid\??|decimal|DateTime|bool|int|string\??) (?<name>\w+) \{ get; set; \}')){
  $type=$property.Groups['type'].Value;$name=$property.Groups['name'].Value;$attributes=$property.Groups['attributes'].Value
  $sqlType=switch($type.TrimEnd('?')){'Guid'{'uniqueidentifier'};'decimal'{$precision=[regex]::Match($attributes,'decimal\(\d+,\d+\)').Value;if($precision){$precision}else{'decimal(18,4)'}};'DateTime'{'datetime2'};'bool'{'bit'};'int'{'int'};'string'{'nvarchar(200)'}}
  $nullable=if($type.EndsWith('?')){'NULL'}else{'NOT NULL'};$columns.Add("[$name] $sqlType $nullable")
 }
 Run "CREATE TABLE dbo.[$table] ($($columns -join ','))"
}
function CheckRow([string]$name,[string]$table,[hashtable]$values,[int]$expected){
 $actual=0;try{InsertRow $table $values}catch{$failure=$_.Exception;while($failure.InnerException){$failure=$failure.InnerException};if($failure -isnot [Data.SqlClient.SqlException]){throw};$actual=$failure.Number}
 $results.Add([pscustomobject]@{name=$name;passed=($actual -eq $expected);expected=$expected;actual=$actual});Write-Output "$name SQL=$actual expected=$expected"
}

try {
 $connection.Open();if((Scalar "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))") -ine 'RHEMA-MICHAEL\SQL2017'){throw 'Wrong SQL instance'}
 Run "CREATE DATABASE [$database]";$connection.ChangeDatabase($database)
 foreach($table in @('PurchaseOrders','PurchaseOrderItems','PurchaseOrderReceipts','PurchaseOrderReceiptItems','GoodsReceiptNoteItems','GoodsReceiptNotes','ProcurementReceiptInspectionCases','WarehouseLocations','ExchangeRates','InventoryMovements','VendorInvoice','VendorInvoiceLineItem','VendorInvoiceReceiptAllocations','JournalEntries','FinancePostingEvents','AccountTransactions','Accounts','PurchaseReturns')){
  Run "SELECT TOP(0) * INTO dbo.[$table] FROM [$sourceDatabase].dbo.[$table]"
 }
 Run 'ALTER TABLE dbo.PurchaseReturns ADD AccountingAllocationVersion int NOT NULL DEFAULT 0'
 EntityTable 'src/ErpSystem.Core/Entities/Procurement/ProcurementReceiptCostBasis.cs' 'ProcurementReceiptCostBasis' 'ProcurementReceiptCostBases'
 foreach($entity in @('VendorInvoiceReceiptCostAllocation','VendorInvoiceReceiptCostPostingLine','VendorInvoiceReceiptCostValuation')){
  EntityTable 'src/ErpSystem.Core/Entities/Finance/VendorInvoiceReceiptCostAllocation.cs' $entity ($entity+'s')
 }
 foreach($entity in @('InventorySupplierReturnAllocation','InventorySupplierReturnAccrualShare')){
  EntityTable 'src/ErpSystem.Core/Entities/Finance/InventorySupplierReturnAllocation.cs' $entity ($entity+'s')
 }
 $source=Get-Content 'src/ErpSystem.Data/Migrations/InventorySupplierInvoiceCostGuards.cs' -Raw
 foreach($name in @('ReceiptBasis','CostAllocation','CostLines','CostValuation','InvoiceSeal')){
  $sql=[regex]::Match($source,'(?s)internal const string '+$name+' = """\r?\n(.*?)\r?\n\s*""";').Groups[1].Value
  if(!$sql){throw 'Guard source was not found'}
  Run $sql;$results.Add([pscustomobject]@{name="Compile/$name";passed=$true})
 }
 $tenant=[Guid]::NewGuid();$item=[Guid]::NewGuid();$warehouse=[Guid]::NewGuid();$bin=[Guid]::NewGuid();$po=[Guid]::NewGuid();$poLine=[Guid]::NewGuid()
 $receipt=[Guid]::NewGuid();$receiptLine=[Guid]::NewGuid();$grnLine=[Guid]::NewGuid();$movement=[Guid]::NewGuid();$basis=[Guid]::NewGuid()
 $book=[Guid]::NewGuid();$receiptJournal=[Guid]::NewGuid();$invoice=[Guid]::NewGuid();$invoiceLine=[Guid]::NewGuid();$invoiceJournal=[Guid]::NewGuid();$event=[Guid]::NewGuid();$allocation=[Guid]::NewGuid();$cost=[Guid]::NewGuid()
 $grni=[Guid]::NewGuid();$inventory=[Guid]::NewGuid();$ppv=[Guid]::NewGuid();$originalAccount=[Guid]::NewGuid()
 InsertRow 'PurchaseOrders' @{Id=$po;TenantId=$tenant;Currency='GHS'}
 InsertRow 'PurchaseOrderItems' @{Id=$poLine;TenantId=$tenant;PurchaseOrderId=$po;InventoryItemId=$item;UnitPrice=10}
 InsertRow 'PurchaseOrderReceipts' @{Id=$receipt;TenantId=$tenant;PurchaseOrderId=$po}
 InsertRow 'PurchaseOrderReceiptItems' @{Id=$receiptLine;TenantId=$tenant;ReceiptId=$receipt;PurchaseOrderItemId=$poLine;AcceptedQuantity=100}
 InsertRow 'GoodsReceiptNoteItems' @{Id=$grnLine;TenantId=$tenant;InventoryItemId=$item;AcceptedQuantity=100}
 InsertRow 'WarehouseLocations' @{Id=$bin;TenantId=$tenant;WarehouseId=$warehouse}
 InsertRow 'InventoryMovements' @{Id=$movement;TenantId=$tenant;InventoryItemId=$item;WarehouseId=$warehouse;LocationId=$bin;MovementType=1;Direction=1;IsPosted=$true;ReferenceId=$receipt;Quantity=100;TotalValue=1000}
 InsertRow 'ProcurementReceiptCostBases' @{Id=$basis;TenantId=$tenant;Version=1;PurchaseOrderReceiptId=$receipt;PurchaseOrderReceiptItemId=$receiptLine;PurchaseOrderItemId=$poLine;InventoryItemId=$item;InventoryMovementId=$movement;WarehouseId=$warehouse;LocationId=$bin;PurchaseQuantity=100;BaseQuantity=100;ConversionToBase=1;PurchaseCurrency='GHS';FunctionalCurrency='GHS';ExchangeRateToFunctional=1;PurchaseUnitCost=10;PurchaseAmount=1000;FunctionalAccrualAmount=1000;FunctionalInventoryAmount=1000}
 $results.Add([pscustomobject]@{name='Capture original receipt basis';passed=$true})
 Check 'Receipt basis immutable' "UPDATE dbo.ProcurementReceiptCostBases SET PurchaseAmount=999 WHERE Id='$basis'" 51930
 InsertRow 'JournalEntries' @{Id=$receiptJournal;TenantId=$tenant;AccountingBookId=$book;PostingStatus='Posted'}
 InsertRow 'JournalEntries' @{Id=$invoiceJournal;TenantId=$tenant;AccountingBookId=$book;PostingStatus='Posted'}
 InsertRow 'VendorInvoice' @{Id=$invoice;TenantId=$tenant;CurrencyCode='GHS';Status=3}
 InsertRow 'VendorInvoiceLineItem' @{Id=$invoiceLine;TenantId=$tenant;VendorInvoiceId=$invoice;Quantity=100;UnitPrice=12;DiscountAmount=0}
 InsertRow 'VendorInvoiceReceiptAllocations' @{Id=$allocation;TenantId=$tenant;VendorInvoiceId=$invoice;VendorInvoiceLineItemId=$invoiceLine;GoodsReceiptNoteItemId=$grnLine;PurchaseOrderReceiptId=$receipt;PurchaseOrderReceiptItemId=$receiptLine;PurchaseOrderItemId=$poLine;Quantity=100}
 $receiptEvent=[Guid]::NewGuid()
 InsertRow 'FinancePostingEvents' @{Id=$receiptEvent;TenantId=$tenant;SourceDocumentId=$receipt;SourceDocumentType='ProcurementPurchaseOrderReceipt';PostingAction='PostAcceptedInventoryReceipt';PostingStatus='Posted';JournalEntryId=$receiptJournal;AccountingBookId=$book;FunctionalCurrencyCode='GHS'}
 InsertRow 'FinancePostingEvents' @{Id=$event;TenantId=$tenant;SourceDocumentId=$invoice;SourceDocumentType='VendorInvoice';PostingAction='Post';PostingStatus='Posted';JournalEntryId=$invoiceJournal;AccountingBookId=$book;FunctionalCurrencyCode='GHS'}
 foreach($account in @($grni,$inventory,$ppv)){InsertRow 'Accounts' @{Id=$account;TenantId=$tenant}}
 InsertRow 'AccountTransactions' @{Id=$originalAccount;TenantId=$tenant;JournalEntryId=$receiptJournal;AccountId=$grni;SourceDocumentLineId=$item;TransactionTag='INV-RECEIPT-GRV-ACCRUAL';CreditAmount=1000;DebitAmount=0}
 $costValues=@{Id=$cost;TenantId=$tenant;VendorInvoiceId=$invoice;VendorInvoiceLineItemId=$invoiceLine;VendorInvoiceReceiptAllocationId=$allocation;ProcurementReceiptCostBasisId=$basis;GoodsReceiptNoteItemId=$grnLine;InventoryItemId=$item;ReceiptJournalEntryId=$receiptJournal;AccountingBookId=$book;PurchaseQuantity=100;BaseQuantity=100;ReceiptForeignAmount=1000;ReceiptFunctionalAmount=1000;InvoiceNetForeignAmount=1200;InvoiceFunctionalAmount=1200;PriceDifferenceFunctionalAmount=200;InventoryAdjustmentAmount=200;RevaluedReceiptBaseQuantity=100;Policy='RevalueInventory';PurchaseCurrency='GHS';FunctionalCurrency='GHS';InvoiceExchangeRateToFunctional=1;PurchasePriceVarianceAccountId=$ppv;PostingEventId=$event;JournalEntryId=$invoiceJournal;SourceFingerprint=('a'*64)}
 InsertRow 'VendorInvoiceReceiptCostAllocations' $costValues
 $results.Add([pscustomobject]@{name='Capture invoice original receipt allocation';passed=$true})
 $excess=$costValues.Clone();$excess.Id=[Guid]::NewGuid();$excess.BaseQuantity=1;$excess.PurchaseQuantity=1
 CheckRow 'Combined original receipt quantity limit' 'VendorInvoiceReceiptCostAllocations' $excess 51944
 $excess=$costValues.Clone();$excess.Id=[Guid]::NewGuid();$excess.BaseQuantity=0;$excess.PurchaseQuantity=0;$excess.ReceiptForeignAmount=1
 CheckRow 'Combined original receipt foreign value limit' 'VendorInvoiceReceiptCostAllocations' $excess 51945
 Check 'Cost allocation immutable' "UPDATE dbo.VendorInvoiceReceiptCostAllocations SET ReceiptFunctionalAmount=999 WHERE Id='$cost'" 51933
 Run "UPDATE dbo.FinancePostingEvents SET SourceDocumentId=NEWID() WHERE Id='$receiptEvent'"
 Check 'Original journal must belong to exact receipt' "UPDATE dbo.VendorInvoiceReceiptCostAllocations SET SourceFingerprint=SourceFingerprint WHERE Id='$cost'" 51934
 Run "UPDATE dbo.FinancePostingEvents SET SourceDocumentId='$receipt' WHERE Id='$receiptEvent'"
 $wrongItemAccount=[Guid]::NewGuid()
 InsertRow 'AccountTransactions' @{Id=$wrongItemAccount;TenantId=$tenant;JournalEntryId=$receiptJournal;AccountId=$grni;SourceDocumentLineId=[Guid]::NewGuid();TransactionTag='INV-RECEIPT-GRV-ACCRUAL';CreditAmount=1000;DebitAmount=0}
 CheckRow 'Original accrual item identity is required' 'VendorInvoiceReceiptCostPostingLines' @{Id=[Guid]::NewGuid();TenantId=$tenant;CostAllocationId=$cost;AccountId=$grni;OriginalReceiptAccountTransactionId=$wrongItemAccount;Purpose='Accrual';ForeignAmount=1;FunctionalAmount=1} 51937
 Check 'Excess GRNI account claim rejected' "INSERT dbo.VendorInvoiceReceiptCostPostingLines(Id,TenantId,CostAllocationId,AccountId,OriginalReceiptAccountTransactionId,Purpose,ForeignAmount,FunctionalAmount,IsDeleted) VALUES(NEWID(),'$tenant','$cost','$grni','$originalAccount','Accrual',1001,1001,0)" 51947
 InsertRow 'VendorInvoiceReceiptCostPostingLines' @{Id=[Guid]::NewGuid();TenantId=$tenant;CostAllocationId=$cost;AccountId=$grni;OriginalReceiptAccountTransactionId=$originalAccount;Purpose='Accrual';ForeignAmount=1000;FunctionalAmount=1000}
 InsertRow 'VendorInvoiceReceiptCostPostingLines' @{Id=[Guid]::NewGuid();TenantId=$tenant;CostAllocationId=$cost;AccountId=$inventory;Purpose='Inventory';ForeignAmount=0;FunctionalAmount=200}
 Check 'Cannot seal missing inventory value movement' "UPDATE dbo.VendorInvoice SET JournalEntryId='$invoiceJournal' WHERE Id='$invoice'" 51940
 $valueMovement=[Guid]::NewGuid()
 InsertRow 'InventoryMovements' @{Id=$valueMovement;TenantId=$tenant;InventoryItemId=$item;WarehouseId=$warehouse;LocationId=$bin;MovementType=17;Direction=1;IsPosted=$true;ReferenceId=$invoice;Quantity=0;TotalValue=200}
 InsertRow 'VendorInvoiceReceiptCostValuations' @{Id=[Guid]::NewGuid();TenantId=$tenant;CostAllocationId=$cost;WarehouseId=$warehouse;LocationId=$bin;InventoryMovementId=$valueMovement;AttributedReceiptBaseQuantity=100;ValueChange=200}
 Check 'Cannot seal without actual GL evidence' "UPDATE dbo.VendorInvoice SET JournalEntryId='$invoiceJournal' WHERE Id='$invoice'" 51943
 InsertRow 'AccountTransactions' @{Id=[Guid]::NewGuid();TenantId=$tenant;JournalEntryId=$invoiceJournal;AccountId=$grni;SourceDocumentLineId=$invoiceLine;TransactionTag='AP-GRV';DebitAmount=1000;CreditAmount=0}
 InsertRow 'AccountTransactions' @{Id=[Guid]::NewGuid();TenantId=$tenant;JournalEntryId=$invoiceJournal;AccountId=$inventory;SourceDocumentLineId=$invoiceLine;TransactionTag='AP-INVENTORY-COST';DebitAmount=200;CreditAmount=0}
 Check 'Seal exact invoice cost and GL value' "UPDATE dbo.VendorInvoice SET JournalEntryId='$invoiceJournal' WHERE Id='$invoice'"
 CheckRow 'No original value append after posting seal' 'VendorInvoiceReceiptCostValuations' @{Id=[Guid]::NewGuid();TenantId=$tenant;CostAllocationId=$cost;WarehouseId=$warehouse;LocationId=$bin;InventoryMovementId=$valueMovement;AttributedReceiptBaseQuantity=100;ValueChange=200} 51939
 Check 'Cannot void without inventory and journal reversal' "UPDATE dbo.VendorInvoice SET Status=7 WHERE Id='$invoice'" 51940
 Check 'Posted original account evidence cannot change' "UPDATE dbo.VendorInvoiceReceiptCostPostingLines SET FunctionalAmount=999 WHERE CostAllocationId='$cost'" 51936
 $extra=[Guid]::NewGuid()
 InsertRow 'AccountTransactions' @{Id=$extra;TenantId=$tenant;JournalEntryId=$invoiceJournal;AccountId=$ppv;SourceDocumentLineId=$invoiceLine;TransactionTag='AP-PRICE-VARIANCE';DebitAmount=10;CreditAmount=0}
 Check 'Extra unmatched journal purpose rejected' "UPDATE dbo.VendorInvoice SET Status=4 WHERE Id='$invoice'" 51948
 Run "DELETE dbo.AccountTransactions WHERE Id='$extra'"
 $reverseJournal=[Guid]::NewGuid();$reverseEvent=[Guid]::NewGuid();$reverseMovement=[Guid]::NewGuid()
 InsertRow 'JournalEntries' @{Id=$reverseJournal;TenantId=$tenant;AccountingBookId=$book;PostingStatus='Posted'}
 InsertRow 'FinancePostingEvents' @{Id=$reverseEvent;TenantId=$tenant;SourceDocumentId=$invoice;SourceDocumentType='VendorInvoice';PostingAction='Reverse';PostingStatus='Posted';JournalEntryId=$reverseJournal;AccountingBookId=$book;FunctionalCurrencyCode='GHS'}
 Run "UPDATE dbo.JournalEntries SET IsReversed=1,ReversalJournalEntryId='$reverseJournal' WHERE Id='$invoiceJournal'"
 InsertRow 'InventoryMovements' @{Id=$reverseMovement;TenantId=$tenant;InventoryItemId=$item;WarehouseId=$warehouse;LocationId=$bin;MovementType=17;Direction=2;IsPosted=$true;IsReversal=$true;ReferenceId=$invoice;Quantity=0;TotalValue=-200}
 InsertRow 'VendorInvoiceReceiptCostValuations' @{Id=[Guid]::NewGuid();TenantId=$tenant;CostAllocationId=$cost;WarehouseId=$warehouse;LocationId=$bin;InventoryMovementId=$reverseMovement;AttributedReceiptBaseQuantity=100;ValueChange=-200;IsReversal=$true}
 Check 'Retain exact reversal authority' "UPDATE dbo.VendorInvoiceReceiptCostAllocations SET ReversalPostingEventId='$reverseEvent',ReversalJournalEntryId='$reverseJournal' WHERE Id='$cost'"
 Check 'Seal full retained inventory reversal' "UPDATE dbo.VendorInvoice SET Status=7 WHERE Id='$invoice'"
 CheckRow 'No reversal value append after void seal' 'VendorInvoiceReceiptCostValuations' @{Id=[Guid]::NewGuid();TenantId=$tenant;CostAllocationId=$cost;WarehouseId=$warehouse;LocationId=$bin;InventoryMovementId=$reverseMovement;AttributedReceiptBaseQuantity=100;ValueChange=-200;IsReversal=$true} 51939
 $downGuard=[regex]::Match($source,'(?s)public static void Remove\(MigrationBuilder migration\).*?migration.Sql\("""\r?\n(.*?)\r?\n\s*"""\);').Groups[1].Value
 if(!$downGuard){throw 'Cannot resolve exact Down evidence guard'}
 Check 'Down refuses immutable receipt and invoice history' $downGuard 51950
 $baseline=Get-Content 'src/ErpSystem.Data/Migrations/20260924230000_ProcurementAutoInvoiceReceipts.cs' -Raw
 foreach($pair in @(@('ReceiptAllocation','AllocationTrigger'),@('ReceiptLine','LineTrigger'))){
  $sql=[regex]::Match($baseline,'(?s)internal const string '+$pair[1]+' = """\r?\n(.*?)\r?\n\s*""";').Groups[1].Value
  $expression=[regex]::Match($source,'(?s)internal static readonly string '+$pair[0]+' =(.*?);').Groups[1].Value
  $replacements=[regex]::Matches($expression,'\.Replace\("([^"]*)",\s*"([^"]*)"\)')
  if(!$sql -or !$replacements.Count){throw 'Cannot resolve exact amended receipt guard'}
  foreach($replacement in $replacements){
   if(!$sql.Contains($replacement.Groups[1].Value)){throw 'Baseline receipt guard changed; replacement no longer applies'}
   $sql=$sql.Replace($replacement.Groups[1].Value,$replacement.Groups[2].Value)
  }
  Run $sql;$results.Add([pscustomobject]@{name="Compile/$($pair[0])";passed=$true})
 }
 $manual=[Guid]::NewGuid();$manualLine=[Guid]::NewGuid();$manualAllocation=[Guid]::NewGuid();$grn=[Guid]::NewGuid();$inspection=[Guid]::NewGuid()
 InsertRow 'GoodsReceiptNotes' @{Id=$grn;TenantId=$tenant;PurchaseOrderId=$po;PurchaseOrderReceiptId=$receipt}
 Run "UPDATE dbo.GoodsReceiptNoteItems SET GoodsReceiptNoteId='$grn',PurchaseOrderItemId='$poLine' WHERE Id='$grnLine'"
 InsertRow 'ProcurementReceiptInspectionCases' @{Id=$inspection;TenantId=$tenant;PurchaseOrderReceiptId=$receipt}
 InsertRow 'VendorInvoice' @{Id=$manual;TenantId=$tenant;CurrencyCode='GHS';Status=1;PurchaseOrderId=$po;MatchingStatus=2;MatchingControlEventId=[Guid]::NewGuid()}
 InsertRow 'VendorInvoiceLineItem' @{Id=$manualLine;TenantId=$tenant;VendorInvoiceId=$manual;Quantity=25;UnitPrice=12;PurchaseOrderItemId=$poLine}
 $manualValues=@{Id=$manualAllocation;TenantId=$tenant;VendorInvoiceId=$manual;VendorInvoiceLineItemId=$manualLine;PurchaseOrderId=$po;PurchaseOrderItemId=$poLine;PurchaseOrderReceiptId=$receipt;PurchaseOrderReceiptItemId=$receiptLine;InspectionCaseId=$inspection;GoodsReceiptNoteId=$grn;GoodsReceiptNoteItemId=$grnLine;Quantity=25}
 CheckRow 'Draft manual invoice cannot claim receipt' 'VendorInvoiceReceiptAllocations' $manualValues 51722
 Run "UPDATE dbo.VendorInvoice SET Status=3 WHERE Id='$manual'"
 CheckRow 'Approved manual invoice needs successful Finance event' 'VendorInvoiceReceiptAllocations' $manualValues 51722
 InsertRow 'FinancePostingEvents' @{Id=[Guid]::NewGuid();TenantId=$tenant;SourceDocumentId=$manual;SourceDocumentType='VendorInvoice';PostingAction='Post';PostingStatus='Posted';JournalEntryId=[Guid]::NewGuid();AccountingBookId=$book;FunctionalCurrencyCode='GHS'}
 CheckRow 'Approved matched posting captures manual allocation' 'VendorInvoiceReceiptAllocations' $manualValues 0
 Check 'Receipt-backed manual quantity immutable' "UPDATE dbo.VendorInvoiceLineItem SET Quantity=26 WHERE Id='$manualLine'" 51724
} catch {
 $failure=$_.Exception;while($failure.InnerException){$failure=$failure.InnerException}
 $number=if($failure -is [Data.SqlClient.SqlException]){$failure.Number}else{$null}
 $results.Add([pscustomobject]@{name='Fixture';passed=$false;exceptionType=$failure.GetType().FullName;sqlNumber=$number})
 Write-Output "FIXTURE_FAILURE|$($failure.GetType().Name)|SQL=$number"
} finally {
 if($connection){$connection.Dispose()}
 [pscustomobject]@{database=$database;scope='Seven exact new/amended SQL guards and positive/negative capture, capacity, posting and reversal cases on isolated copied baseline schema plus scalar fixture tables; not full EF migration or service acceptance';results=$results}|ConvertTo-Json -Depth 5|Set-Content $evidence
 Write-Output "EVIDENCE|$evidence"
}
if(@($results|Where-Object {!$_.passed}).Count){exit 1}
