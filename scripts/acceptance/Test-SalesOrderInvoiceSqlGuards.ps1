<# Tests exact Sales invoice SQL guards against an isolated schema copy; never mutates the app database. #>
$ErrorActionPreference='Stop'
$run=[DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss')+'_'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$database='RhemaERP_SalesInvoiceGuard_'+$run
$sourceDatabase='RhemaERP_ReceiptUpgrade_20260927_023608_918225d0'
$results=[Collections.Generic.List[object]]::new()
$evidence=Join-Path (Get-Location) "tmp/sales-invoice-guards-$run.json"
$connection=[Data.SqlClient.SqlConnection]::new('Server=RHEMA-MICHAEL\SQL2017;Database=master;Integrated Security=True;TrustServerCertificate=True;')
function Run([string]$sql){$cmd=$connection.CreateCommand();$cmd.CommandText=$sql;try{[void]$cmd.ExecuteNonQuery()}finally{$cmd.Dispose()}}
function Scalar([string]$sql){$cmd=$connection.CreateCommand();$cmd.CommandText=$sql;try{return $cmd.ExecuteScalar()}finally{$cmd.Dispose()}}
function Check([string]$name,[string]$sql,[int]$expected=0){
 $actual=0;Run 'BEGIN TRANSACTION'
 try{Run $sql;Run 'COMMIT TRANSACTION'}catch{$errorItem=$_.Exception;while($errorItem -and $errorItem -isnot [Data.SqlClient.SqlException]){$errorItem=$errorItem.InnerException};Run 'IF @@TRANCOUNT>0 ROLLBACK TRANSACTION';if(!$errorItem){throw};$actual=$errorItem.Number;if($actual -ne $expected){Write-Output $errorItem.Message}}
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
 foreach($table in @('SalesOrders','SalesOrderLines','Invoices','InvoiceLineItem')){Run "SELECT TOP(0) * INTO dbo.[$table] FROM [$sourceDatabase].dbo.[$table]"}
 Run 'ALTER TABLE dbo.SalesOrders ADD InvoiceGenerationKey nvarchar(100) NULL,InvoiceGenerationHash nvarchar(64) NULL,InvoiceGeneratedById uniqueidentifier NULL,InvoiceEconomicsJson nvarchar(max) NULL,InvoiceSourceJson nvarchar(max) NULL; CREATE UNIQUE INDEX UX_SalesInvoice ON dbo.SalesOrders(TenantId,InvoiceId) WHERE InvoiceId IS NOT NULL;'
 $tenant=[Guid]::NewGuid();$actor=[Guid]::NewGuid();$order=[Guid]::NewGuid();$line=[Guid]::NewGuid();$invoice=[Guid]::NewGuid();$customer=[Guid]::NewGuid();$account=[Guid]::NewGuid();$warehouse=[Guid]::NewGuid();$bin=[Guid]::NewGuid();$item=[Guid]::NewGuid()
 InsertRow 'SalesOrders' @{Id=$order;TenantId=$tenant;BusinessPartnerId=$customer;DocumentNumber='SO-SQL';OrderType=1;OrderStatus=3;Currency='GHS';ExchangeRate=[decimal]1;SubTotal=[decimal]20;TotalAmount=[decimal]20;TaxAmount=[decimal]0;DiscountAmount=[decimal]0;DiscountPercentage=[decimal]0;ShippingAmount=[decimal]0;WarehouseId=$warehouse;IsDeleted=$false}
 InsertRow 'SalesOrderLines' @{Id=$line;TenantId=$tenant;SalesOrderId=$order;Description='Stock line';Quantity=[decimal]2;UnitPrice=[decimal]10;DiscountAmount=[decimal]0;DiscountPercentage=[decimal]0;TaxAmount=[decimal]0;TaxRate=[decimal]0;Unit='EA';GLAccountId=$account;InventoryItemId=$item;WarehouseId=$warehouse;LocationId=$bin;IsDeleted=$false}
 InsertRow 'Invoices' @{Id=$invoice;TenantId=$tenant;BusinessPartnerId=$customer;InvoiceNumber='SI-SQL';Reference='SO-SQL';CurrencyCode='GHS';ExchangeRate=[decimal]1;SubTotal=[decimal]20;TotalAmount=[decimal]20;TaxAmount=[decimal]0;DiscountAmount=[decimal]0;Status=0;IsDeleted=$false;IsOpeningBalance=$false}
 InsertRow 'InvoiceLineItem' @{Id=$line;InvoiceId=$invoice;TenantId=$tenant;Description='Stock line';Quantity=[decimal]2;UnitPrice=[decimal]10;DiscountAmount=[decimal]0;DiscountPercentage=[decimal]0;TaxAmount=[decimal]0;TaxRate=[decimal]0;Unit='EA';GLAccountId=$account;InventoryItemId=$item;WarehouseId=$warehouse;LocationId=$bin;TaxTreatment=2;LineItemType=3;IsDeleted=$false}
 $guard=[IO.File]::ReadAllText((Join-Path (Get-Location) 'src/ErpSystem.Data/Migrations/SalesOrderInvoiceGuards.cs'))
 foreach($match in [regex]::Matches($guard,'private const string (\w+) = """\s*([\s\S]*?)\s*""";')){Check ('Compile '+$match.Groups[1].Value) $match.Groups[2].Value}
 $snapshot=@"
 DECLARE @economics nvarchar(max)=(SELECT v.*,JSON_QUERY((SELECT x.* FROM dbo.InvoiceLineItem x WHERE x.InvoiceId=v.Id AND x.IsDeleted=0 FOR JSON PATH)) AS Lines FROM dbo.Invoices v WHERE v.Id='$invoice' FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 DECLARE @stock nvarchar(max)=(SELECT x.* FROM dbo.InvoiceLineItem x WHERE x.InvoiceId='$invoice' AND x.IsDeleted=0 FOR JSON PATH);
 DECLARE @invoiceSnapshot nvarchar(max)=JSON_MODIFY(JSON_MODIFY(N'{}','$.Economics',@economics),'$.Stock',JSON_QUERY(@stock));
 DECLARE @source nvarchar(max)=(SELECT o.*,o.DiscountAmount AS Discount,o.ShippingAmount AS Shipping,o.TaxAmount AS Tax,o.TotalAmount AS Total,
 JSON_QUERY((SELECT s.*,s.UnitPrice AS Price,s.DiscountAmount AS Discount,s.TaxAmount AS Tax FROM dbo.SalesOrderLines s WHERE s.SalesOrderId=o.Id AND s.IsDeleted=0 FOR JSON PATH)) AS Lines
 FROM dbo.SalesOrders o WHERE o.Id='$order' FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
"@
 $link="UPDATE dbo.SalesOrders SET InvoiceId='$invoice',InvoiceGenerationKey=N'generate-once',InvoiceGenerationHash=REPLICATE('a',64),InvoiceGeneratedById='$actor',InvoiceEconomicsJson=@invoiceSnapshot,InvoiceSourceJson=@source WHERE Id='$order';"
 Check 'Null snapshot rejected' ($snapshot+"SET @invoiceSnapshot=NULL;"+$link) 51996
 Check 'Empty snapshot rejected' ($snapshot+"SET @invoiceSnapshot=N'{}';"+$link) 51999
 Check 'Cross tenant invoice rejected' ("UPDATE dbo.Invoices SET TenantId=NEWID() WHERE Id='$invoice';"+$snapshot+$link) 51996
 Check 'Changed source quantity rejected' ("UPDATE dbo.InvoiceLineItem SET Quantity=3 WHERE Id='$line';"+$snapshot+$link) 51996
 Check 'Changed source bin rejected' ("UPDATE dbo.InvoiceLineItem SET LocationId=NEWID() WHERE Id='$line';"+$snapshot+$link) 51996
 Check 'Changed source description rejected' ("UPDATE dbo.InvoiceLineItem SET Description=N'Other' WHERE Id='$line';"+$snapshot+$link) 51996
 Check 'Changed source revenue account rejected' ("UPDATE dbo.InvoiceLineItem SET GLAccountId=NEWID() WHERE Id='$line';"+$snapshot+$link) 51996
 Check 'Forged invoice economics snapshot rejected' ($snapshot+"SET @economics=JSON_MODIFY(@economics,'$.TotalAmount',999);SET @invoiceSnapshot=JSON_MODIFY(@invoiceSnapshot,'$.Economics',@economics);"+$link) 51999
 Check 'Forged source snapshot rejected' ($snapshot+"SET @source=JSON_MODIFY(@source,'$.ExchangeRate',2);"+$link) 51999
 Check 'Missing stock snapshot rejected' ($snapshot+"SET @invoiceSnapshot=JSON_MODIFY(@invoiceSnapshot,'$.Stock',JSON_QUERY(N'[]'));"+$link) 51999
 Check 'Missing source reference rejected' ("UPDATE dbo.Invoices SET Reference=NULL WHERE Id='$invoice';"+$snapshot+$link) 51996
 Check 'Missing stock bin rejected' ("UPDATE dbo.InvoiceLineItem SET LocationId=NULL WHERE Id='$line';UPDATE dbo.SalesOrderLines SET LocationId=NULL WHERE Id='$line';"+$snapshot+$link) 51996
 Check 'Approved invoice FX may differ from retained order FX' ("SAVE TRANSACTION FxProbe;UPDATE dbo.Invoices SET ExchangeRate=2 WHERE Id='$invoice';"+$snapshot+$link+"ROLLBACK TRANSACTION FxProbe;")
 Check 'Link exact approved source' ($snapshot+$link)
 Check 'Order customer immutable' "UPDATE dbo.SalesOrders SET BusinessPartnerId=NEWID() WHERE Id='$order'" 51996
 Check 'Order source FX immutable' "UPDATE dbo.SalesOrders SET ExchangeRate=2 WHERE Id='$order'" 51996
 Check 'Order cannot cancel linked invoice' "UPDATE dbo.SalesOrders SET OrderStatus=8 WHERE Id='$order'" 51996
 Check 'Order cannot be removed' "DELETE dbo.SalesOrders WHERE Id='$order'" 51996
 Check 'Order source snapshot immutable' "UPDATE dbo.SalesOrders SET InvoiceSourceJson=N'{}' WHERE Id='$order'" 51996
 Check 'Source line quantity immutable' "UPDATE dbo.SalesOrderLines SET Quantity=3 WHERE Id='$line'" 51997
 Check 'Source line bin immutable' "UPDATE dbo.SalesOrderLines SET LocationId=NEWID() WHERE Id='$line'" 51997
 Check 'Source line cannot be removed' "DELETE dbo.SalesOrderLines WHERE Id='$line'" 51997
 Check 'Invoice header amount immutable' "UPDATE dbo.Invoices SET TotalAmount=99 WHERE Id='$invoice'" 51998
 Check 'Invoice cannot use status-only void' "UPDATE dbo.Invoices SET Status=6 WHERE Id='$invoice'" 51998
 Check 'Invoice cannot be removed' "DELETE dbo.Invoices WHERE Id='$invoice'" 51998
 Check 'Invoice line price immutable' "UPDATE dbo.InvoiceLineItem SET UnitPrice=11 WHERE Id='$line'" 51998
 Check 'Invoice line bin immutable' "UPDATE dbo.InvoiceLineItem SET LocationId=NEWID() WHERE Id='$line'" 51998
 Check 'Invoice line cannot be removed' "DELETE dbo.InvoiceLineItem WHERE Id='$line'" 51998
 Check 'Canonical costing remains allowed' "UPDATE dbo.InvoiceLineItem SET UnitCost=4,CostTotal=8 WHERE Id='$line'"
 Check 'Canonical invoice posting and settlement remain allowed' "UPDATE dbo.Invoices SET Status=2,JournalEntryId=NEWID(),PaidAmount=10 WHERE Id='$invoice'"
 Check 'Delivery progress remains allowed' "UPDATE dbo.SalesOrders SET OrderStatus=5 WHERE Id='$order'; UPDATE dbo.SalesOrderLines SET DeliveredQuantity=2,InvoicedQuantity=2 WHERE Id='$line'"
}catch{$results.Add([pscustomobject]@{name='Fixture';passed=$false;error=$_.Exception.Message});Write-Output $_.Exception.Message}finally{if($connection){$connection.Dispose()};[pscustomobject]@{database=$database;scope='Exact SQL guard tests on an isolated schema copy; not full migration or application lifecycle acceptance';results=$results}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $evidence;Write-Output "EVIDENCE|$evidence"}
if(@($results|Where-Object {!$_.passed}).Count){exit 1}
