<# Exact production allocation triggers, isolated minimal SQL fixture. Not a full migration or UI test. #>
param([string]$Workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path)
$ErrorActionPreference='Stop'
$Workspace=(Resolve-Path -LiteralPath $Workspace).Path
$run=[DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss')+'_'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$database='RhemaERP_TransferGuard_'+$run
$server='RHEMA-MICHAEL\SQL2017'
if($database -notmatch '^RhemaERP_TransferGuard_[0-9]{8}_[0-9]{6}_[a-f0-9]{8}$'){throw 'Invalid isolated database name.'}
$evidence=Join-Path $Workspace "tmp\transfer-guards-$run.json"
$results=[Collections.Generic.List[object]]::new()
$connection=$null
$stage='Read configuration'
$created=$false
$ids=@'
DECLARE @tenant uniqueidentifier='10000000-0000-0000-0000-000000000001',@actor uniqueidentifier='10000000-0000-0000-0000-000000000002',
@source uniqueidentifier='20000000-0000-0000-0000-000000000001',@destination uniqueidentifier='20000000-0000-0000-0000-000000000002',@transit uniqueidentifier='20000000-0000-0000-0000-000000000003',
@bin1 uniqueidentifier='30000000-0000-0000-0000-000000000001',@bin2 uniqueidentifier='30000000-0000-0000-0000-000000000002',@bin3 uniqueidentifier='30000000-0000-0000-0000-000000000003',@transitBin uniqueidentifier='30000000-0000-0000-0000-000000000004',
@transfer uniqueidentifier='40000000-0000-0000-0000-000000000001',@line uniqueidentifier='40000000-0000-0000-0000-000000000002',@item uniqueidentifier='40000000-0000-0000-0000-000000000003',
@dispatch uniqueidentifier='50000000-0000-0000-0000-000000000001',@receive uniqueidentifier='50000000-0000-0000-0000-000000000002',@resolve uniqueidentifier='50000000-0000-0000-0000-000000000003',
@pick1 uniqueidentifier='60000000-0000-0000-0000-000000000001',@pick2 uniqueidentifier='60000000-0000-0000-0000-000000000002',
@receipt uniqueidentifier='70000000-0000-0000-0000-000000000001',@returned uniqueidentifier='70000000-0000-0000-0000-000000000002';
'@
function Run-Sql([string]$sql) {
 $command=$connection.CreateCommand();$command.CommandTimeout=60;$command.CommandText=$sql
 try {[void]$command.ExecuteNonQuery()}finally{$command.Dispose()}
}
function Check-Sql([string]$name,[string]$sql,[int]$expected=0) {
 $command=$connection.CreateCommand();$command.CommandTimeout=60
 $command.CommandText="SET XACT_ABORT ON; BEGIN TRY BEGIN TRAN;`n$ids`n$sql`nCOMMIT; SELECT 0; END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; SELECT ERROR_NUMBER(); END CATCH;"
 try{$actual=[int]$command.ExecuteScalar()}finally{$command.Dispose()}
 $pass=$actual -eq $expected
 $results.Add([pscustomobject]@{name=$name;passed=$pass;expectedSqlError=$expected;actualSqlError=$actual})
 Write-Output ("{0}|{1}|SQL={2}" -f $(if($pass){'PASS'}else{'FAIL'}),$name,$actual)
}
function Leg([string]$pick,[string]$receipt,[string]$leg,[string]$warehouse,[string]$bin,[decimal]$quantity,[decimal]$value) {
 $direction=if($leg -in @('SourceOut','TransitOut')){2}else{1}
 $type=if($direction -eq 2){3}else{4}
 return "INSERT dbo.InventoryMovements(Id,TenantId,InventoryItemId,WarehouseId,LocationId,Quantity,TotalValue,MovementType,Direction,ReferenceType,ReferenceId,ReferenceNumber,CreatedById,IsPosted,IsDeleted,TransferDispatchAllocationId,TransferReceiptAllocationId,TransferLeg) VALUES(NEWID(),@tenant,@item,$warehouse,$bin,$quantity,$value,$type,$direction,4,@line,N'TR-PROBE',@actor,1,0,$pick,$receipt,N'$leg');"
}
try {
 $project=[IO.File]::ReadAllText((Join-Path $Workspace 'src\ErpSystem.Api\ErpSystem.Api.csproj'))
 $secretId=[regex]::Match($project,'<UserSecretsId>([^<]+)</UserSecretsId>').Groups[1].Value
 $secret=[IO.File]::ReadAllText((Join-Path $env:APPDATA "Microsoft\UserSecrets\$secretId\secrets.json"))|ConvertFrom-Json
 $candidate=$secret.'ConnectionStrings:DefaultConnection'
 if(!$candidate -and $secret.ConnectionStrings){$candidate=$secret.ConnectionStrings.DefaultConnection}
 if(!$candidate){throw 'Default SQL connection is missing.'}
 $builder=[Data.SqlClient.SqlConnectionStringBuilder]::new($candidate);$secret=$null;$candidate=$null
 if($builder.DataSource -notin @($server,'.\SQL2017','localhost\SQL2017')){throw 'Connection instance differs from approved isolated SQL target.'}
 $builder['Initial Catalog']='master';$builder['Connect Timeout']=20;$builder['TrustServerCertificate']=$true
 $connection=[Data.SqlClient.SqlConnection]::new($builder.ConnectionString);$connection.Open()
 $command=$connection.CreateCommand();$command.CommandText="SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))"
 try{$actual=[string]$command.ExecuteScalar()}finally{$command.Dispose()}
 if($actual -ine $server){throw 'SQL instance identity mismatch.'}
 $stage='Create isolated fixture'
 Run-Sql "IF DB_ID(N'$database') IS NOT NULL THROW 51000,'Isolated database already exists.',1; CREATE DATABASE [$database];"
 $created=$true;$connection.ChangeDatabase($database)
 Run-Sql @'
CREATE TABLE dbo.Warehouses(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,Code nvarchar(50),WarehouseType nvarchar(50),IsConsignmentWarehouse bit,IsDeleted bit,IsActive bit);
CREATE TABLE dbo.WarehouseLocations(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,WarehouseId uniqueidentifier,LocationCode nvarchar(50),IsPickingLocation bit,IsReceivingLocation bit,IsInTransitLocation bit,IsQuarantineLocation bit,IsInspectionLocation bit,IsDamageLocation bit,IsConsignmentBin bit,ConsignmentWarehouseId uniqueidentifier NULL,IsDeleted bit,IsActive bit);
CREATE TABLE dbo.BusinessPartners(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,PartnerType nvarchar(50),PartnerName nvarchar(200),PartnerCode nvarchar(50),IsActive bit,IsDeleted bit);
CREATE TABLE dbo.InventoryTransfers(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,TransferNumber nvarchar(50),SourceWarehouseId uniqueidentifier,DestinationWarehouseId uniqueidentifier,Status int,IsDeleted bit);
CREATE TABLE dbo.InventoryTransferItems(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,InventoryTransferId uniqueidentifier,InventoryItemId uniqueidentifier,SourceLocationId uniqueidentifier NULL,DestinationLocationId uniqueidentifier NULL,IsDeleted bit);
CREATE TABLE dbo.InventoryTransferActions(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,InventoryTransferId uniqueidentifier,ActionType int,ActorUserId uniqueidentifier,Sequence int,SnapshotJson nvarchar(max),IsDeleted bit);
CREATE TABLE dbo.InventoryTransferActionLines(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,InventoryTransferActionId uniqueidentifier,InventoryTransferItemId uniqueidentifier,DispatchedQuantity decimal(18,4),ReceivedQuantity decimal(18,4),DamagedQuantity decimal(18,4),ShortageQuantity decimal(18,4),IsDeleted bit);
CREATE TABLE dbo.InventoryTransferDispatchAllocations(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,InventoryTransferActionId uniqueidentifier,InventoryTransferItemId uniqueidentifier,SourceLocationId uniqueidentifier,SourceInventoryWarehouseId uniqueidentifier,InTransitLocationId uniqueidentifier,Quantity decimal(18,4),CarrierBusinessPartnerId uniqueidentifier NULL,CarrierName nvarchar(200) NULL,CarrierAccountNumber nvarchar(50) NULL,IsDeleted bit);
CREATE TABLE dbo.InventoryTransferReceiptAllocations(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,InventoryTransferActionId uniqueidentifier,DispatchAllocationId uniqueidentifier,DestinationLocationId uniqueidentifier,DestinationInventoryWarehouseId uniqueidentifier,Quantity decimal(18,4),ReturnedToSource bit,IsDeleted bit);
CREATE TABLE dbo.InventoryMovements(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,InventoryItemId uniqueidentifier,WarehouseId uniqueidentifier,LocationId uniqueidentifier NULL,Quantity decimal(18,4),TotalValue decimal(18,2),MovementType int,Direction int,ReferenceType int,ReferenceId uniqueidentifier NULL,ReferenceNumber nvarchar(50),CreatedById uniqueidentifier NULL,IsPosted bit,IsDeleted bit,TransferDispatchAllocationId uniqueidentifier NULL,TransferReceiptAllocationId uniqueidentifier NULL,TransferLeg nvarchar(20) NULL);
CREATE UNIQUE INDEX UX_MovementDispatchLeg ON dbo.InventoryMovements(TenantId,TransferDispatchAllocationId,TransferLeg) WHERE TransferDispatchAllocationId IS NOT NULL AND TransferReceiptAllocationId IS NULL;
CREATE UNIQUE INDEX UX_MovementReceiptLeg ON dbo.InventoryMovements(TenantId,TransferReceiptAllocationId,TransferLeg) WHERE TransferReceiptAllocationId IS NOT NULL;
CREATE TABLE dbo.StockMovements(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,InventoryItemId uniqueidentifier,WarehouseId uniqueidentifier,LocationId uniqueidentifier NULL,Quantity decimal(18,4),TotalValue decimal(18,2),UnitCost decimal(18,2),MovementType nvarchar(50),ReferenceType int,ReferenceId uniqueidentifier NULL,ReferenceNumber nvarchar(50),ProcessedById uniqueidentifier NULL,IsDeleted bit,TransferDispatchAllocationId uniqueidentifier NULL,TransferReceiptAllocationId uniqueidentifier NULL,TransferLeg nvarchar(20) NULL);
'@
 $source=[IO.File]::ReadAllText((Join-Path $Workspace 'src\ErpSystem.Data\Migrations\InventoryTransferAllocationGuards.cs'))
 $archive=[IO.File]::ReadAllText((Join-Path $Workspace 'src\ErpSystem.Data\Migrations\ArchivedGovernanceBaselineSql.cs'))
 $legacy=[regex]::Match($archive,'(?s)CREATE OR ALTER TRIGGER \[dbo\]\.\[TR_StockMovements_GovernedInventoryTransfer\].*?(?=\r?\n\s*"""\);)')
 if(!$legacy.Success){throw 'Original projection trigger source is unavailable.'}
 Run-Sql $legacy.Value
 $stage='Install exact production guards'
 foreach($name in @('DispatchGuard','ReceiptGuard','MovementGuard','ProjectionGuard','TransitLocationGuard','TransitWarehouseGuard','LegacyProjectionPatch')) {
  $pattern='(?s)public (?:const string '+$name+' =|static string '+$name+' =>) (?:\$\$)?"""\r?\n(.*?)\r?\n\s*""";'
  $match=[regex]::Match($source,$pattern);if(!$match.Success){throw "SQL source unavailable: $name"}
  $sql=$match.Groups[1].Value.Replace('{{(int)ReferenceType.Transfer}}','4').Replace('{{(int)InventoryMovementType.TransferOut}}','3').Replace('{{(int)InventoryMovementType.TransferIn}}','4').Replace('{{(int)MovementDirection.Out}}','2').Replace('{{(int)MovementDirection.In}}','1')
  if($sql.Contains('{{')){throw 'Unresolved SQL interpolation.'}
  Run-Sql $sql
  $results.Add([pscustomobject]@{name="Compile/$name";passed=$true})
 }
 Run-Sql ($ids+@'
INSERT dbo.Warehouses VALUES(@source,@tenant,N'SOURCE',N'Standard',0,0,1),(@destination,@tenant,N'DEST',N'Standard',0,0,1),(@transit,@tenant,N'TRANSIT',N'Transit',0,0,1);
INSERT dbo.WarehouseLocations VALUES(@bin1,@tenant,@source,N'S1',1,1,0,0,0,0,0,NULL,0,1),(@bin2,@tenant,@source,N'S2',1,1,0,0,0,0,0,NULL,0,1),(@bin3,@tenant,@destination,N'D1',1,1,0,0,0,0,0,NULL,0,1),(@transitBin,@tenant,@transit,N'TRANSIT',0,0,1,0,0,0,0,NULL,0,1);
INSERT dbo.InventoryTransfers VALUES(@transfer,@tenant,N'TR-PROBE',@source,@destination,3,0);
INSERT dbo.InventoryTransferItems VALUES(@line,@tenant,@transfer,@item,NULL,NULL,0);
INSERT dbo.InventoryTransferActions VALUES(@dispatch,@tenant,@transfer,5,@actor,1,N'{}',0);
INSERT dbo.InventoryTransferActionLines VALUES(NEWID(),@tenant,@dispatch,@line,100,0,0,0,0);
'@)
 $stage='Exercise allocation and physical ledger controls'
 Check-Sql 'Split source picks 60+40' @'
INSERT dbo.InventoryTransferDispatchAllocations VALUES(@pick1,@tenant,@dispatch,@line,@bin1,@source,@transitBin,60,NULL,NULL,NULL,0),(@pick2,@tenant,@dispatch,@line,@bin2,@source,@transitBin,40,NULL,NULL,NULL,0);
'@
 Check-Sql 'Cumulative picks exceed action' 'INSERT dbo.InventoryTransferDispatchAllocations VALUES(NEWID(),@tenant,@dispatch,@line,@bin1,@source,@transitBin,1,NULL,NULL,NULL,0);' 51902
 Check-Sql 'Foreign tenant pick' "INSERT dbo.InventoryTransferDispatchAllocations VALUES(NEWID(),NEWID(),@dispatch,@line,@bin1,@source,@transitBin,1,NULL,NULL,NULL,0);" 51902
 Check-Sql 'Immutable source pick' 'UPDATE dbo.InventoryTransferDispatchAllocations SET Quantity=59 WHERE Id=@pick1;' 51901
 Check-Sql 'Transit requires preceding source value' (Leg '@pick1' 'NULL' 'TransitIn' '@transit' '@transitBin' 60 600) 51907
 Check-Sql 'Source/transit carrying pair 60' ((Leg '@pick1' 'NULL' 'SourceOut' '@source' '@bin1' 60 600)+(Leg '@pick1' 'NULL' 'TransitIn' '@transit' '@transitBin' 60 600))
 Check-Sql 'Source/transit carrying pair 40' ((Leg '@pick2' 'NULL' 'SourceOut' '@source' '@bin2' 40 400)+(Leg '@pick2' 'NULL' 'TransitIn' '@transit' '@transitBin' 40 400))
 Check-Sql 'Ordinary transit ledger mutation blocked' (Leg 'NULL' 'NULL' 'TransitIn' '@transit' '@transitBin' 1 10) 51906
 Check-Sql 'Retained ledger immutable' "UPDATE dbo.InventoryMovements SET TotalValue=599 WHERE TransferDispatchAllocationId=@pick1 AND TransferLeg=N'SourceOut';" 51905
 Check-Sql 'Transit identity immutable' "UPDATE dbo.WarehouseLocations SET IsInTransitLocation=0 WHERE Id=@transitBin;" 51913
 Check-Sql 'Operational bin cannot become transit' 'UPDATE dbo.WarehouseLocations SET IsInTransitLocation=1 WHERE Id=@bin1;' 51914
 Check-Sql 'Transit ownership immutable' 'UPDATE dbo.Warehouses SET IsConsignmentWarehouse=1 WHERE Id=@transit;' 51915
 Run-Sql ($ids+@'
UPDATE dbo.InventoryTransfers SET Status=5 WHERE Id=@transfer;
INSERT dbo.InventoryTransferActions VALUES(@receive,@tenant,@transfer,6,@actor,2,N'{}',0);
INSERT dbo.InventoryTransferActionLines VALUES(NEWID(),@tenant,@receive,@line,0,100,0,0,0);
'@)
 Check-Sql 'Superseded dispatch action rejected within its quantity allowance' 'UPDATE dbo.InventoryTransferActionLines SET DispatchedQuantity=101 WHERE InventoryTransferActionId=@dispatch; INSERT dbo.InventoryTransferDispatchAllocations VALUES(NEWID(),@tenant,@dispatch,@line,@bin1,@source,@transitBin,1,NULL,NULL,NULL,0);' 51902
 Check-Sql 'Receipt action cannot use Received lifecycle' 'UPDATE dbo.InventoryTransfers SET Status=6 WHERE Id=@transfer; INSERT dbo.InventoryTransferReceiptAllocations VALUES(@receipt,@tenant,@receive,@pick1,@bin3,@destination,60,0,0);' 51904
 Check-Sql 'Retained receipt 60' 'INSERT dbo.InventoryTransferReceiptAllocations VALUES(@receipt,@tenant,@receive,@pick1,@bin3,@destination,60,0,0);'
 Check-Sql 'Cumulative overreceipt rejected' 'INSERT dbo.InventoryTransferReceiptAllocations VALUES(NEWID(),@tenant,@receive,@pick1,@bin3,@destination,1,0,0);' 51904
 Check-Sql 'Immutable receipt allocation' 'UPDATE dbo.InventoryTransferReceiptAllocations SET Quantity=59 WHERE Id=@receipt;' 51903
 Check-Sql 'Final transit release must preserve entire value' (Leg '@pick1' '@receipt' 'TransitOut' '@transit' '@transitBin' 60 599) 51908
 Check-Sql 'Transit release 60 exact value' (Leg '@pick1' '@receipt' 'TransitOut' '@transit' '@transitBin' 60 600)
 Check-Sql 'Destination cannot invent carrying value' (Leg '@pick1' '@receipt' 'DestinationIn' '@destination' '@bin3' 60 601) 51907
 Check-Sql 'Destination receipt retains value' (Leg '@pick1' '@receipt' 'DestinationIn' '@destination' '@bin3' 60 600)
 Check-Sql 'Projection matches retained destination leg' @'
INSERT dbo.StockMovements VALUES(NEWID(),@tenant,@item,@destination,@bin3,60,600,10,N'TransferIn',4,@transfer,N'TR-PROBE',@actor,0,@pick1,@receipt,N'DestinationIn');
'@
 Check-Sql 'Projection quantity mismatch rejected' @'
INSERT dbo.StockMovements VALUES(NEWID(),@tenant,@item,@destination,@bin3,61,600,10,N'TransferIn',4,@transfer,N'TR-PROBE',@actor,0,@pick1,@receipt,N'DestinationIn');
'@ 51911
 Run-Sql ($ids+@'
UPDATE dbo.InventoryTransfers SET Status=6 WHERE Id=@transfer;
INSERT dbo.InventoryTransferActions VALUES(@resolve,@tenant,@transfer,7,@actor,3,N'{"Metadata":{"ResolutionCode":"RETURNED_TO_SOURCE"}}',0);
INSERT dbo.InventoryTransferActionLines VALUES(NEWID(),@tenant,@resolve,@line,0,0,0,40,0);
'@)
 Check-Sql 'Discrepancy source return must use original picked bin' 'INSERT dbo.InventoryTransferReceiptAllocations VALUES(@returned,@tenant,@resolve,@pick2,@bin1,@source,40,1,0);' 51904
 Check-Sql 'Discrepancy direction must match immutable resolution action' 'INSERT dbo.InventoryTransferReceiptAllocations VALUES(@returned,@tenant,@resolve,@pick2,@bin3,@destination,40,0,0);' 51904
 Check-Sql 'Discrepancy return retains original pick' ('INSERT dbo.InventoryTransferReceiptAllocations VALUES(@returned,@tenant,@resolve,@pick2,@bin2,@source,40,1,0);'+(Leg '@pick2' '@returned' 'TransitOut' '@transit' '@transitBin' 40 400)+(Leg '@pick2' '@returned' 'SourceReturn' '@source' '@bin2' 40 400))
 Check-Sql 'Final physical transit quantity/value zero' @'
IF (SELECT SUM(CASE WHEN TransferLeg=N'TransitIn' THEN Quantity ELSE -Quantity END) FROM dbo.InventoryMovements WHERE TransferLeg IN(N'TransitIn',N'TransitOut'))<>0
 OR (SELECT SUM(CASE WHEN TransferLeg=N'TransitIn' THEN TotalValue ELSE -TotalValue END) FROM dbo.InventoryMovements WHERE TransferLeg IN(N'TransitIn',N'TransitOut'))<>0
 THROW 51001,'Transit quantity/value conservation failed.',1;
'@
 $zeroSetup=@'
SET @transfer=NEWID();SET @line=NEWID();SET @dispatch=NEWID();SET @receive=NEWID();SET @pick1=NEWID();SET @receipt=NEWID();
INSERT dbo.InventoryTransfers VALUES(@transfer,@tenant,N'TR-ZERO',@source,@destination,3,0);
INSERT dbo.InventoryTransferItems VALUES(@line,@tenant,@transfer,@item,NULL,NULL,0);
INSERT dbo.InventoryTransferActions VALUES(@dispatch,@tenant,@transfer,5,@actor,1,N'{}',0);
INSERT dbo.InventoryTransferActionLines VALUES(NEWID(),@tenant,@dispatch,@line,2,0,0,0,0);
INSERT dbo.InventoryTransferDispatchAllocations VALUES(@pick1,@tenant,@dispatch,@line,@bin1,@source,@transitBin,2,NULL,NULL,NULL,0);
'@
 $zeroReceive=@'
UPDATE dbo.InventoryTransfers SET Status=5 WHERE Id=@transfer;
INSERT dbo.InventoryTransferActions VALUES(@receive,@tenant,@transfer,6,@actor,2,N'{}',0);
INSERT dbo.InventoryTransferActionLines VALUES(NEWID(),@tenant,@receive,@line,0,2,0,0,0);
INSERT dbo.InventoryTransferReceiptAllocations VALUES(@receipt,@tenant,@receive,@pick1,@bin3,@destination,2,0,0);
'@
 $zeroLegs=(Leg '@pick1' 'NULL' 'SourceOut' '@source' '@bin1' 2 0)+(Leg '@pick1' 'NULL' 'TransitIn' '@transit' '@transitBin' 2 0)+$zeroReceive+(Leg '@pick1' '@receipt' 'TransitOut' '@transit' '@transitBin' 2 0)+(Leg '@pick1' '@receipt' 'DestinationIn' '@destination' '@bin3' 2 0)
 Check-Sql 'Zero-cost stock completes all four physical legs without invented value' ($zeroSetup+$zeroLegs.Replace('TR-PROBE','TR-ZERO'))
 # Prove the cumulative guard with overlapping real SQL sessions, below the service lock.
 foreach($scenario in @(@{Quantity=60;Error=51904;Total=60;Rows=1},@{Quantity=40;Error=0;Total=100;Rows=2})) {
  $stage='Concurrent allocation receipt'
  $transferId=[Guid]::NewGuid().ToString();$lineId=[Guid]::NewGuid().ToString();$pickId=[Guid]::NewGuid().ToString()
  $raceIds=$ids.Replace('40000000-0000-0000-0000-000000000001',$transferId).Replace('40000000-0000-0000-0000-000000000002',$lineId).Replace('60000000-0000-0000-0000-000000000001',$pickId).Replace('50000000-0000-0000-0000-000000000001',[Guid]::NewGuid().ToString()).Replace('50000000-0000-0000-0000-000000000002',[Guid]::NewGuid().ToString())
  $raceNumber='TR-RACE-'+$transferId.Substring(0,8)
  $raceIds+="DECLARE @otherDestinationBin uniqueidentifier='$([Guid]::NewGuid().ToString())';"
  $setup=@'
INSERT dbo.WarehouseLocations VALUES(@otherDestinationBin,@tenant,@destination,CONVERT(nvarchar(50),@otherDestinationBin),1,1,0,0,0,0,0,NULL,0,1);
INSERT dbo.InventoryTransfers VALUES(@transfer,@tenant,N'TR-PROBE',@source,@destination,3,0);
INSERT dbo.InventoryTransferItems VALUES(@line,@tenant,@transfer,@item,NULL,NULL,0);
INSERT dbo.InventoryTransferActions VALUES(@dispatch,@tenant,@transfer,5,@actor,1,N'{}',0);
INSERT dbo.InventoryTransferActionLines VALUES(NEWID(),@tenant,@dispatch,@line,100,0,0,0,0);
INSERT dbo.InventoryTransferDispatchAllocations VALUES(@pick1,@tenant,@dispatch,@line,@bin1,@source,@transitBin,100,NULL,NULL,NULL,0);
'@
  $setup+=(Leg '@pick1' 'NULL' 'SourceOut' '@source' '@bin1' 100 1000)+(Leg '@pick1' 'NULL' 'TransitIn' '@transit' '@transitBin' 100 1000)
  $setup+=@'
UPDATE dbo.InventoryTransfers SET Status=5 WHERE Id=@transfer;
INSERT dbo.InventoryTransferActions VALUES(@receive,@tenant,@transfer,6,@actor,2,N'{}',0);
INSERT dbo.InventoryTransferActionLines VALUES(NEWID(),@tenant,@receive,@line,0,100,0,0,0);
'@
  Run-Sql ($raceIds+$setup.Replace('TR-PROBE',$raceNumber))
  $sessionA=$null;$sessionB=$null;$transactionA=$null;$commandB=$null;$pending=$null
  try {
   $builder['Initial Catalog']=$database
   $sessionA=[Data.SqlClient.SqlConnection]::new($builder.ConnectionString);$sessionA.Open()
   $sessionB=[Data.SqlClient.SqlConnection]::new($builder.ConnectionString);$sessionB.Open()
   $cmd=$sessionA.CreateCommand();$cmd.CommandText='SELECT @@SPID';$spidA=[int]$cmd.ExecuteScalar();$cmd.Dispose()
   $cmd=$sessionB.CreateCommand();$cmd.CommandText='SELECT @@SPID';$spidB=[int]$cmd.ExecuteScalar();$cmd.Dispose()
   if($spidA -eq $spidB){throw 'Expected distinct SQL sessions.'}
   $transactionA=$sessionA.BeginTransaction([Data.IsolationLevel]::Serializable)
   $cmd=$sessionA.CreateCommand();$cmd.Transaction=$transactionA;$cmd.CommandTimeout=30
   $cmd.CommandText=$raceIds+'INSERT dbo.InventoryTransferReceiptAllocations VALUES(NEWID(),@tenant,@receive,@pick1,@bin3,@destination,60,0,0);'
   try{[void]$cmd.ExecuteNonQuery()}finally{$cmd.Dispose()}
   $commandB=$sessionB.CreateCommand();$commandB.CommandTimeout=30
   $commandB.CommandText="SET XACT_ABORT ON; SET TRANSACTION ISOLATION LEVEL SERIALIZABLE; BEGIN TRY BEGIN TRAN;`n$raceIds`nINSERT dbo.InventoryTransferReceiptAllocations VALUES(NEWID(),@tenant,@receive,@pick1,@otherDestinationBin,@destination,$($scenario.Quantity),0,0); COMMIT; END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;"
   $pending=$commandB.BeginExecuteNonQuery();$blocked=$false;$waitType=$null;$deadline=[DateTime]::UtcNow.AddSeconds(10)
   while([DateTime]::UtcNow -lt $deadline -and !$pending.IsCompleted) {
    $cmd=$connection.CreateCommand();$cmd.CommandText="SELECT wait_type FROM sys.dm_exec_requests WHERE session_id=$spidB AND blocking_session_id=$spidA AND wait_type LIKE N'LCK_M_%'"
    try{$waitType=$cmd.ExecuteScalar()}finally{$cmd.Dispose()}
    if($waitType -is [string] -and $waitType.Length){$blocked=$true;break}
    Start-Sleep -Milliseconds 100
   }
   if(!$blocked){throw 'Real overlapping SQL allocation blocking was not observed.'}
   $transactionA.Commit();$transactionA.Dispose();$transactionA=$null;$sqlError=0
   try{[void]$commandB.EndExecuteNonQuery($pending)}catch{
    $inner=$_.Exception;while($inner -and $inner -isnot [Data.SqlClient.SqlException]){$inner=$inner.InnerException}
    if(!$inner){throw};$sqlError=$inner.Number
   }
   $pending=$null
   $pass=$sqlError -eq $scenario.Error
   $results.Add([pscustomobject]@{name="Concurrent60+$($scenario.Quantity)";passed=$pass;sessionA=$spidA;sessionB=$spidB;blockingObserved=$blocked;waitType=$waitType;expectedSqlError=$scenario.Error;actualSqlError=$sqlError})
   Write-Output ("{0}|Concurrent60+{1}|SQL={2}|Blocking={3}" -f $(if($pass){'PASS'}else{'FAIL'}),$scenario.Quantity,$sqlError,$waitType)
   Check-Sql "Concurrent60+$($scenario.Quantity) committed rows" "IF (SELECT SUM(Quantity) FROM dbo.InventoryTransferReceiptAllocations WHERE DispatchAllocationId='$pickId')<>$($scenario.Total) OR (SELECT COUNT(*) FROM dbo.InventoryTransferReceiptAllocations WHERE DispatchAllocationId='$pickId')<>$($scenario.Rows) THROW 51001,'Concurrent allocation invariant failed.',1;"
  } finally {
   if($transactionA){try{$transactionA.Rollback()}catch{};$transactionA.Dispose()}
   if($pending -and $commandB){$commandB.Cancel();try{[void]$commandB.EndExecuteNonQuery($pending)}catch{}}
   if($commandB){$commandB.Dispose()};if($sessionA){$sessionA.Dispose()};if($sessionB){$sessionB.Dispose()}
  }
 }
} catch {
 $results.Add([pscustomobject]@{name=$stage;passed=$false;errorType=$_.Exception.GetType().FullName;sqlError=if($_.Exception.Number){$_.Exception.Number}else{$null}})
 Write-Output "FAIL|$stage|$($_.Exception.GetType().Name)"
} finally {
 if($connection){$connection.Dispose()}
 [pscustomobject]@{database=$database;databaseCreated=$created;scope='Exact allocation guards and DMV-observed two-session cumulative receipt concurrency against isolated minimal tables. Does not claim full EF schema/index/migration or HTTP lifecycle acceptance.';results=$results} |
  ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $evidence -Encoding UTF8
 Write-Output "EVIDENCE|$evidence"
 if($created){Write-Output "RETAINED_DATABASE|$database"}
}
if(@($results | Where-Object {!$_.passed}).Count){exit 1}
