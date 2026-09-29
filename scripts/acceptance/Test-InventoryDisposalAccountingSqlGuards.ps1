<# Tests exact disposal SQL guards against an isolated schema copy; never mutates the app database. #>
$ErrorActionPreference='Stop'
$run=[DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss')+'_'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$database='RhemaERP_DisposalGuard_'+$run
$sourceDatabase='RhemaERP_ReceiptUpgrade_20260927_023608_918225d0'
$results=[Collections.Generic.List[object]]::new()
$evidence=Join-Path (Get-Location) "tmp/disposal-guards-$run.json"
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
try{
 $connection.Open();if((Scalar "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))") -ine 'RHEMA-MICHAEL\SQL2017'){throw 'Wrong SQL instance'}
 Run "CREATE DATABASE [$database]";$connection.ChangeDatabase($database)
 foreach($table in @('InventoryDisposalCases','InventoryDisposalLines','InventoryItems','Invoices','InvoiceLineItem','Accounts','Warehouses','Users','UserTenants','StockAdjustments')){Run "SELECT TOP(0) * INTO dbo.[$table] FROM [$sourceDatabase].dbo.[$table]"}
 Run 'ALTER TABLE dbo.InventoryDisposalCases ADD AccountingVersion int NOT NULL DEFAULT 0, PreparedStockAdjustmentId uniqueidentifier NULL; ALTER TABLE dbo.InventoryItems ADD InventoryDisposalAccountId uniqueidentifier NULL;'
 Run 'CREATE TABLE dbo.InventoryDisposalAuctionInvoices(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier NOT NULL,InventoryDisposalCaseId uniqueidentifier NOT NULL UNIQUE,InvoiceId uniqueidentifier NOT NULL UNIQUE,CreatedByUserId uniqueidentifier NOT NULL,IdempotencyKey nvarchar(100) NOT NULL,PayloadHash nvarchar(64) NOT NULL,InvoiceEconomicsJson nvarchar(max) NOT NULL,IsDeleted bit NOT NULL DEFAULT 0);'
 Run "CREATE FUNCTION dbo.WorkflowApprovalRequiredAtSubmission(@tenant uniqueidentifier,@entity nvarchar(100),@id uniqueidentifier) RETURNS bit AS BEGIN RETURN 0; END;"
 $tenant=[Guid]::NewGuid();$actor=[Guid]::NewGuid();$warehouse=[Guid]::NewGuid();$disposal=[Guid]::NewGuid();$line=[Guid]::NewGuid();$item=[Guid]::NewGuid();$account=[Guid]::NewGuid();$invoice=[Guid]::NewGuid();$prepared=[Guid]::NewGuid()
 InsertRow 'Warehouses' @{Id=$warehouse;TenantId=$tenant}
 InsertRow 'Users' @{Id=$actor;TenantId=$tenant}
 InsertRow 'UserTenants' @{Id=[Guid]::NewGuid();TenantId=$tenant;UserId=$actor;Status=0;IsDeleted=$false}
 InsertRow 'Accounts' @{Id=$account;TenantId=$tenant;IsDeleted=$false;AllowDirectPosting=$true;IsControlAccount=$false;Status=1;AccountType=5}
 InsertRow 'InventoryItems' @{Id=$item;TenantId=$tenant;InventoryDisposalAccountId=$account;IsDeleted=$false}
 $legacy=[Guid]::NewGuid();InsertRow 'InventoryDisposalCases' @{Id=$legacy;TenantId=$tenant;RequestedById=$actor;WarehouseId=$warehouse;Method=2;AccountingVersion=0;Status=1;ApprovalRequired=$true;IsDeleted=$false}
 $source=[IO.File]::ReadAllText((Join-Path (Get-Location) 'src/ErpSystem.Data/Migrations/InventoryDisposalAccountingGuards.cs'))
 foreach($name in @('CaseGuard','LinkGuard','InvoiceGuard','InvoiceLineGuard')){$m=[regex]::Match($source,'(?s)public const string '+$name+' = """\r?\n(.*?)\r?\n\s*""";');if(!$m.Success){throw "Missing guard $name"};Run $m.Groups[1].Value;$results.Add([pscustomobject]@{name="Compile/$name";passed=$true})}
 InsertRow 'InventoryDisposalCases' @{Id=$disposal;TenantId=$tenant;RequestedById=$actor;WarehouseId=$warehouse;Method=1;AccountingVersion=1;Status=1;ApprovalRequired=$true;IsDeleted=$false;DisposalNumber='DSP-SQL-001'}
 Check 'New auction initial state' "IF NOT EXISTS(SELECT 1 FROM dbo.InventoryDisposalCases WHERE Id='$disposal' AND AccountingVersion=1) THROW 51000,'Missing case',1"
 Check 'Legacy case retains identity' "UPDATE dbo.InventoryDisposalCases SET Reason=N'Legacy draft reason' WHERE Id='$legacy'"
 Check 'No new Sale conversion' "UPDATE dbo.InventoryDisposalCases SET Method=2 WHERE Id='$disposal'" 51112
 Check 'Accounting version immutable' "UPDATE dbo.InventoryDisposalCases SET AccountingVersion=0 WHERE Id='$disposal'" 51112
 Check 'No approval policy keeps governed ready state' "UPDATE dbo.InventoryDisposalCases SET Status=11,ApprovalRequired=0 WHERE Id='$disposal'"
 InsertRow 'InventoryDisposalLines' @{Id=$line;TenantId=$tenant;InventoryDisposalCaseId=$disposal;InventoryItemId=$item;Quantity=[decimal]4;IsDeleted=$false}
 InsertRow 'Invoices' @{Id=$invoice;TenantId=$tenant;Status=1;IsDeleted=$false;Reference='DSP-SQL-001';IsOpeningBalance=$false;ExchangeRate=[decimal]1;SubTotal=[decimal]61;TotalAmount=[decimal]61;DiscountAmount=[decimal]0}
 $canonical="INVENTORY:DISPOSAL:AUCTION:LINE:V1:$($tenant.ToString('N')):$($disposal.ToString('N')):$($line.ToString('N'))"
 $sha=[Security.Cryptography.SHA256]::Create();try{$bytes=$sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical))}finally{$sha.Dispose()};$invoiceLine=[Guid]::new([byte[]]$bytes[0..15])
 InsertRow 'InvoiceLineItem' @{Id=$invoiceLine;InvoiceId=$invoice;TenantId=$tenant;LineItemType=2;GLAccountId=$account;Quantity=[decimal]4;UnitPrice=[decimal]15.25;TaxTreatment=2;IsDeleted=$false;DiscountAmount=[decimal]0;DiscountPercentage=[decimal]0}
 Check 'Reject mismatched auction quantity' "UPDATE dbo.InvoiceLineItem SET Quantity=5 WHERE Id='$invoiceLine'; BEGIN TRY INSERT dbo.InventoryDisposalAuctionInvoices VALUES(NEWID(),'$tenant','$disposal','$invoice','$actor',N'bad',REPLICATE('a',64),N'{}',0); END TRY BEGIN CATCH UPDATE dbo.InvoiceLineItem SET Quantity=4 WHERE Id='$invoiceLine'; THROW; END CATCH;" 51964
 Check 'Empty economic snapshot blocked' "INSERT dbo.InventoryDisposalAuctionInvoices VALUES(NEWID(),'$tenant','$disposal','$invoice','$actor',N'bad-json',REPLICATE('a',64),N'{}',0)" 51970
 $snapshotSql="DECLARE @snapshot nvarchar(max)=(SELECT v.*,JSON_QUERY((SELECT x.* FROM dbo.InvoiceLineItem x WHERE x.InvoiceId=v.Id FOR JSON PATH)) AS Lines FROM dbo.Invoices v WHERE v.Id='$invoice' FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);"
 Check 'Snapshot must match amount' ($snapshotSql+"SET @snapshot=JSON_MODIFY(@snapshot,'$.TotalAmount',999); INSERT dbo.InventoryDisposalAuctionInvoices VALUES(NEWID(),'$tenant','$disposal','$invoice','$actor',N'bad-amount',REPLICATE('a',64),@snapshot,0)") 51970
 Check 'Link exact .NET deterministic invoice line' ($snapshotSql+"INSERT dbo.InventoryDisposalAuctionInvoices VALUES(NEWID(),'$tenant','$disposal','$invoice','$actor',N'good',REPLICATE('a',64),@snapshot,0)")
 Check 'Immutable link' "UPDATE dbo.InventoryDisposalAuctionInvoices SET PayloadHash=REPLICATE('b',64)" 51961
 Check 'Price edit blocked' "UPDATE dbo.InvoiceLineItem SET UnitPrice=20 WHERE Id='$invoiceLine'" 51967
 Check 'Quantity edit blocked' "UPDATE dbo.InvoiceLineItem SET Quantity=5 WHERE Id='$invoiceLine'" 51967
 Check 'Account edit blocked' "UPDATE dbo.InvoiceLineItem SET GLAccountId=NEWID() WHERE Id='$invoiceLine'" 51967
 Check 'Line deletion blocked' "DELETE dbo.InvoiceLineItem WHERE Id='$invoiceLine'" 51967
 Check 'Header amount edit blocked' "UPDATE dbo.Invoices SET TotalAmount=75 WHERE Id='$invoice'" 51965
 Check 'Invoice date edit blocked' "UPDATE dbo.Invoices SET InvoiceDate=DATEADD(day,1,InvoiceDate) WHERE Id='$invoice'" 51965
 Check 'Cannot cancel disposal with an active AR invoice' "UPDATE dbo.InventoryDisposalCases SET Status=10 WHERE Id='$disposal'" 51969
 Check 'Normal AR status and settlement remains allowed' "UPDATE dbo.Invoices SET Status=2,PaidAmount=10 WHERE Id='$invoice'"
 Check 'Posted auction cannot use legacy status-only void' "UPDATE dbo.Invoices SET Status=6 WHERE Id='$invoice'" 51968
 Check 'Stage without non-existent StockAdjustment FK' "UPDATE dbo.InventoryDisposalCases SET Status=7,PreparedStockAdjustmentId='$prepared',ExecutionReference=N'AUCTION-SQL' WHERE Id='$disposal'"
 Check 'Prepared identity immutable' "UPDATE dbo.InventoryDisposalCases SET PreparedStockAdjustmentId=NEWID() WHERE Id='$disposal'" 51112
 Check 'Completion without stock row blocked' "UPDATE dbo.InventoryDisposalCases SET Status=8,CompletedById='$actor',CompletedAtUtc=SYSUTCDATETIME() WHERE Id='$disposal'" 51116
 InsertRow 'StockAdjustments' @{Id=$prepared;TenantId=$tenant;Status='Posted'}
 Check 'Complete using exact posted adjustment and AR link' "UPDATE dbo.InventoryDisposalCases SET Status=8,StockAdjustmentId='$prepared',CompletedById='$actor',CompletedAtUtc=SYSUTCDATETIME() WHERE Id='$disposal'"
 Check 'Completed stock identity immutable' "UPDATE dbo.InventoryDisposalCases SET StockAdjustmentId=NEWID() WHERE Id='$disposal'" 51110
}catch{$results.Add([pscustomobject]@{name='Fixture';passed=$false;error=$_.Exception.Message});Write-Output $_.Exception.Message}finally{if($connection){$connection.Dispose()};[pscustomobject]@{database=$database;scope='Exact SQL guard tests on an isolated schema copy; not full migration or application lifecycle acceptance';results=$results}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $evidence;Write-Output "EVIDENCE|$evidence"}
if(@($results|Where-Object {!$_.passed}).Count){exit 1}
