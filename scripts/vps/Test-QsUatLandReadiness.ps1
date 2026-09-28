[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$SqlServer)
$ErrorActionPreference='Stop'
# Evaluate the SHIPPING selector query against SQL-local fixture variables only.
# No persisted table is read or modified and no business database is required.
$inventory=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'QsUatPrerequisiteInventory.sql'))
$start=$inventory.IndexOf('DECLARE @ReadyLand TABLE')
$end=$inventory.IndexOf('DECLARE @DecisionKeys TABLE')
if($start -lt 0 -or $end -le $start){throw 'Ready-land query boundaries were not found.'}
$selector=$inventory.Substring($start,$end-$start).
    Replace('dbo.EstateLandDemarcations','@Demarcations').
    Replace('dbo.EstateManagedAssets','@Assets').
    Replace('dbo.ProjectDevelopmentProfiles','@Profiles')
$fixture=@'
DECLARE @Tenant uniqueidentifier='11111111-1111-1111-1111-111111111111';
DECLARE @OtherTenant uniqueidentifier='22222222-2222-2222-2222-222222222222';
DECLARE @Assets TABLE(Id uniqueidentifier,TenantId uniqueidentifier,AssetCode nvarchar(100),ProjectCode nvarchar(100),Name nvarchar(200),AssetType nvarchar(40),Status nvarchar(40),IsDeleted bit,IsReadyForProjectManagement bit,IsPublishedToExternalPortal bit);
DECLARE @Demarcations TABLE(Id uniqueidentifier,TenantId uniqueidentifier,EstateManagedAssetId uniqueidentifier,ParentDemarcationId uniqueidentifier,DemarcationNumber int,BoundaryVerified bit,IsDeleted bit,IsReadyForProjectManagement bit,IsPublishedToExternalPortal bit);
DECLARE @Profiles TABLE(TenantId uniqueidentifier,LandReference nvarchar(200),IsDeleted bit);
DECLARE @n int=1,@asset uniqueidentifier,@portion uniqueidentifier;
WHILE @n<=19 BEGIN
 SET @asset=NEWID();SET @portion=NEWID();
 INSERT @Assets VALUES(@asset,CASE WHEN @n=9 THEN @OtherTenant ELSE @Tenant END,N'UAT-'+CONVERT(nvarchar(2),@n),NULL,N'Land '+CONVERT(nvarchar(2),@n),
 CASE WHEN @n=10 THEN N'Building' ELSE N'Land' END,CASE WHEN @n=11 THEN N'Assigned' ELSE N'LandBank' END,
 CASE WHEN @n=12 THEN 1 ELSE 0 END,CASE WHEN @n=18 THEN 1 ELSE 0 END,CASE WHEN @n=4 THEN 1 ELSE 0 END);
 INSERT @Demarcations VALUES(@portion,CASE WHEN @n=15 THEN @OtherTenant ELSE @Tenant END,@asset,NULL,CASE WHEN @n=19 THEN 1001 ELSE 1 END,
 CASE WHEN @n=2 THEN 0 ELSE 1 END,CASE WHEN @n=13 THEN 1 ELSE 0 END,CASE WHEN @n IN(3,18) THEN 0 ELSE 1 END,CASE WHEN @n=5 THEN 1 ELSE 0 END);
 IF @n=8 INSERT @Demarcations VALUES(NEWID(),@Tenant,@asset,@portion,2,1,0,1,0);
 IF @n=14 INSERT @Demarcations VALUES(NEWID(),@Tenant,@asset,NULL,2,0,0,1,0);
 SET @n+=1;
END;
INSERT @Profiles VALUES(@Tenant,N' uat-6-portion-001 ',0),(@Tenant,N'Land 7',0),(@Tenant,N'Land 14',0),
(@OtherTenant,N'UAT-16-PORTION-001',0),(@Tenant,N'UAT-17-PORTION-001',1);
'@
$builder=New-Object Data.SqlClient.SqlConnectionStringBuilder
$builder['Data Source']=$SqlServer;$builder['Initial Catalog']='master';$builder['Integrated Security']=$true;$builder['TrustServerCertificate']=$true
$connection=New-Object Data.SqlClient.SqlConnection $builder.ConnectionString
$command=$null
try {
 $connection.Open();$command=$connection.CreateCommand();$command.CommandTimeout=30
 $command.CommandText=$fixture+"`n"+$selector+"`nSELECT LandReference FROM @ReadyLand ORDER BY LandReference;"
 $actual=New-Object 'System.Collections.Generic.List[string]'
 $reader=$command.ExecuteReader()
 try{while($reader.Read()){[void]$actual.Add($reader.GetString(0))}}finally{$reader.Dispose()}
 $expected=@('UAT-1-PORTION-001','UAT-8-PORTION-002','UAT-14-PORTION-001','UAT-16-PORTION-001','UAT-17-PORTION-001','UAT-18-PORTION-001','UAT-19-PORTION-1001') | Sort-Object
 if(@(Compare-Object @($actual) $expected).Count){throw 'Ready-land selector differs from Estate eligibility and assignment filters.'}
 Write-Output 'PASS|19 SQL-local ready-land fixtures: tenant, leaf, verification, readiness, publication, status, deletion, canonical/legacy assignment and portion formatting.'
} finally {
 if($command){$command.Dispose()};$connection.Dispose();$builder=$null
}
