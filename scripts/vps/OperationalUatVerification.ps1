# Read-only, explicit-target operational UAT verification. No service-global
# connection is consulted. Fingerprints exclude credentials, password hashes,
# security stamps, contact details and audit timestamps.
function Get-RhemaOperationalSeedVerificationSql {
    return @'
SET NOCOUNT ON;
IF DB_NAME() <> @ExpectedDatabase THROW 51998, 'Operational verification target mismatch.', 1;
DECLARE @Tenant uniqueidentifier;
DECLARE @Failures TABLE(Name nvarchar(200));
DECLARE @State TABLE(Name nvarchar(100), RecordCount bigint, Payload nvarchar(max));
DECLARE @Now datetime2=SYSUTCDATETIME();
IF (SELECT COUNT(*) FROM dbo.Tenants WHERE Code=N'DEFAULT' AND IsDeleted=0 AND Status=1)<>1
    INSERT @Failures VALUES(N'DEFAULT.ActiveTenant');
SELECT @Tenant=Id FROM dbo.Tenants WHERE Code=N'DEFAULT' AND IsDeleted=0 AND Status=1;

DECLARE @Roles TABLE(UserName nvarchar(256),RoleName nvarchar(256));
INSERT @Roles VALUES
(N'procurementofficer',N'TDC_PROCUREMENT_OFFICER'),
(N'procurementapprover',N'Procurement User'),(N'procurementapprover',N'TDC_HEAD_OF_PROCUREMENT'),(N'procurementapprover',N'TDC_STORES_MANAGER'),
(N'procurementevaluator',N'TDC_EVALUATOR'),(N'tdc0102-checker-201531',N'TDC_EVALUATOR'),
(N'financereviewer',N'TDC_FINANCE_REVIEWER'),(N'financeapprover',N'Finance User'),
(N'storesofficer',N'Inventory User'),(N'storesofficer',N'TDC_STORES_OFFICER'),
(N'storesmanager',N'Inventory User'),(N'storesmanager',N'TDC_STORES_MANAGER'),
(N'uat.qs.preparer',N'Employee'),(N'uat.qs.preparer',N'TDC_QUANTITY_SURVEYOR'),
(N'uat.qs.reviewer',N'Employee'),(N'uat.qs.reviewer',N'TDC_QUANTITY_SURVEYOR'),(N'uat.qs.reviewer',N'TDC_SUPERVISING_QUANTITY_SURVEYOR'),
(N'uat.qs.approver',N'Manager'),(N'uat.qs.approver',N'TDC_QUANTITY_SURVEYOR'),(N'uat.qs.approver',N'TDC_SUPERVISING_QUANTITY_SURVEYOR'),
(N'admin',N'TDC_SUPERVISING_QUANTITY_SURVEYOR'),
(N'manager',N'TDC_USER_DEPARTMENT_HEAD'),(N'manager',N'TDC_STORES_OFFICER'),(N'manager',N'TDC_STORES_MANAGER'),
(N'employee',N'TDC_MANAGING_DIRECTOR'),(N'employee',N'TDC_INTERNAL_AUDIT'),
(N'ap.officer',N'TDC_HEAD_OF_PROCUREMENT'),(N'ap.officer',N'TDC_STORES_MANAGER'),(N'finance.manager',N'TDC_EVALUATOR');
DECLARE @Actors TABLE(UserName nvarchar(256) PRIMARY KEY);
INSERT @Actors SELECT DISTINCT UserName FROM @Roles;
INSERT @Failures SELECT N'Actor.Active:'+e.UserName FROM @Actors e WHERE
 (SELECT COUNT(*) FROM dbo.Users u WHERE u.UserName=e.UserName AND u.IsActive=1 AND u.TenantId=@Tenant)<>1;
INSERT @Failures SELECT N'Actor.TenantAccess:'+e.UserName FROM @Actors e WHERE
 (SELECT COUNT(*) FROM dbo.UserTenants t JOIN dbo.Users u ON u.Id=t.UserId WHERE u.UserName=e.UserName
  AND t.TenantId=@Tenant AND t.IsDeleted=0 AND t.Status=0 AND (t.ExpiresAt IS NULL OR t.ExpiresAt>@Now))<>1;
INSERT @Failures SELECT N'Actor.Role:'+e.UserName+N'/'+e.RoleName FROM @Roles e WHERE
 (SELECT COUNT(*) FROM dbo.Users u JOIN dbo.UserRoles ur ON ur.UserId=u.Id JOIN dbo.AspNetRoles r ON r.Id=ur.RoleId
  WHERE u.UserName=e.UserName AND r.Name=e.RoleName)<>1;

DECLARE @Uoms TABLE(Code nvarchar(50) PRIMARY KEY);
INSERT @Uoms VALUES(N'EA'),(N'EACH'),(N'KG'),(N'L'),(N'PACK');
DECLARE @Categories TABLE(Code nvarchar(50) PRIMARY KEY, Uom nvarchar(50));
INSERT @Categories VALUES(N'FILT',N'EA'),(N'FLD',N'L'),(N'IGN',N'EA'),(N'LUB',N'L'),(N'PROJECT-DEMO',N'EA'),(N'TOOLS',N'EA');
DECLARE @Warehouses TABLE(Code nvarchar(50) PRIMARY KEY);
INSERT @Warehouses VALUES(N'DEMO-PM'),(N'WH-02');
DECLARE @Bins TABLE(WarehouseCode nvarchar(50),Code nvarchar(50),IsDefault bit);
INSERT @Bins VALUES(N'DEMO-PM',N'DEFAULT',1),(N'DEMO-PM',N'LOC-001',0),(N'WH-02',N'DEFAULT',1);
DECLARE @Items TABLE(Code nvarchar(100) PRIMARY KEY,CategoryCode nvarchar(50),Uom nvarchar(50));
INSERT @Items VALUES
(N'FILTER-AIR-001',N'FILT',N'EA'),(N'FILTER-OIL-001',N'FILT',N'EA'),(N'FLUID-BRAKE-DOT4',N'FLD',N'L'),
(N'OIL-5W30-5L',N'LUB',N'L'),(N'PM-BARCODE-DEVICE',N'PROJECT-DEMO',N'EA'),(N'SKU-001',N'PROJECT-DEMO',N'EACH'),
(N'SPARK-PLUG-001',N'IGN',N'EA'),(N'TOOL-SCAN-001',N'TOOLS',N'EA'),(N'UAT-PO-2026-0001-WIRELESS-KEYBOARD',N'FILT',N'EA');
INSERT @Failures SELECT N'Uom.Active:'+e.Code FROM @Uoms e WHERE
 (SELECT COUNT(*) FROM dbo.UnitsOfMeasure u WHERE u.TenantId=@Tenant AND u.Code=e.Code AND u.IsDeleted=0 AND u.IsActive=1)<>1;
INSERT @Failures SELECT N'Category.Active:'+e.Code FROM @Categories e WHERE
 (SELECT COUNT(*) FROM dbo.InventoryCategories c WHERE c.TenantId=@Tenant AND c.Code=e.Code AND c.IsDeleted=0 AND c.IsActive=1 AND c.DefaultUnitOfMeasure=e.Uom)<>1;
INSERT @Failures SELECT N'Warehouse.Active:'+e.Code FROM @Warehouses e WHERE
 (SELECT COUNT(*) FROM dbo.Warehouses w WHERE w.TenantId=@Tenant AND w.Code=e.Code AND w.IsDeleted=0 AND w.IsActive=1)<>1;
INSERT @Failures SELECT N'Bin.Active:'+e.WarehouseCode+N'/'+e.Code FROM @Bins e WHERE
 (SELECT COUNT(*) FROM dbo.WarehouseLocations l JOIN dbo.Warehouses w ON w.Id=l.WarehouseId AND w.TenantId=l.TenantId
  WHERE w.TenantId=@Tenant AND w.Code=e.WarehouseCode AND l.LocationCode=e.Code AND l.IsDeleted=0 AND l.IsActive=1
    AND l.IsPickingLocation=1 AND l.IsReceivingLocation=1 AND l.IsDefault=e.IsDefault)<>1;
INSERT @Failures SELECT N'Item.ActiveLineage:'+e.Code FROM @Items e WHERE
 (SELECT COUNT(*) FROM dbo.InventoryItems i JOIN dbo.InventoryCategories c ON c.Id=i.CategoryId AND c.TenantId=i.TenantId
  WHERE i.TenantId=@Tenant AND i.ItemCode=e.Code AND i.IsDeleted=0 AND i.Status=1 AND i.UnitOfMeasure=e.Uom
    AND c.Code=e.CategoryCode AND c.IsDeleted=0 AND c.IsActive=1)<>1;
INSERT @Failures SELECT N'Item.CanonicalUom:'+e.Code FROM @Items e WHERE
 (SELECT COUNT(*) FROM dbo.InventoryItems i JOIN dbo.ItemUnitsOfMeasure x ON x.InventoryItemId=i.Id AND x.TenantId=i.TenantId
  JOIN dbo.UnitsOfMeasure u ON u.Id=x.UnitOfMeasureId AND u.TenantId=x.TenantId
  WHERE i.TenantId=@Tenant AND i.ItemCode=e.Code AND i.IsDeleted=0 AND x.IsDeleted=0 AND x.IsActive=1
    AND u.Code=e.Uom AND u.IsDeleted=0 AND u.IsActive=1 AND x.ConversionToBase=1
    AND x.IsBaseUnit=1 AND x.IsStockingUnit=1 AND x.IsPurchaseUnit=1)<>1;
INSERT @Failures SELECT N'Item.AmbiguousBaseUom:'+e.Code FROM @Items e WHERE
 (SELECT COUNT(*) FROM dbo.InventoryItems i JOIN dbo.ItemUnitsOfMeasure x ON x.InventoryItemId=i.Id
  WHERE i.TenantId=@Tenant AND i.ItemCode=e.Code AND i.IsDeleted=0 AND x.IsDeleted=0 AND x.IsActive=1 AND x.IsBaseUnit=1)<>1;
INSERT @Failures SELECT DISTINCT N'Item.CrossTenantUom:'+e.Code FROM @Items e
 JOIN dbo.InventoryItems i ON i.ItemCode=e.Code AND i.TenantId=@Tenant
 JOIN dbo.ItemUnitsOfMeasure x ON x.InventoryItemId=i.Id
 LEFT JOIN dbo.UnitsOfMeasure u ON u.Id=x.UnitOfMeasureId
 WHERE x.IsDeleted=0 AND (x.TenantId<>i.TenantId OR u.Id IS NULL OR u.TenantId<>i.TenantId);

DECLARE @Suppliers TABLE(Code nvarchar(50) PRIMARY KEY);
INSERT @Suppliers VALUES(N'CONT-GH-ADOM-BUILD'),(N'SUP260001');
INSERT @Failures SELECT N'Supplier.CanonicalIdentity:'+e.Code FROM @Suppliers e WHERE
 (SELECT COUNT(*) FROM dbo.BusinessPartners b WHERE b.TenantId=@Tenant AND b.PartnerCode=e.Code AND b.IsDeleted=0 AND b.IsActive=1)<>1;
INSERT @Failures SELECT N'Supplier.Role:'+e.Code FROM @Suppliers e WHERE
 (SELECT COUNT(*) FROM dbo.BusinessPartners b JOIN dbo.BusinessPartnerRoles r ON r.BusinessPartnerId=b.Id AND r.TenantId=b.TenantId
  WHERE b.TenantId=@Tenant AND b.PartnerCode=e.Code AND b.IsDeleted=0 AND r.IsDeleted=0 AND r.RoleType=1 AND r.Status=1
    AND r.ActiveFromUtc<=@Now AND (r.InactiveFromUtc IS NULL OR r.InactiveFromUtc>@Now))<>1;
INSERT @Failures SELECT N'Supplier.ApprovedApProfile:'+e.Code FROM @Suppliers e WHERE
 (SELECT COUNT(*) FROM dbo.BusinessPartners b JOIN dbo.BusinessPartnerRoles r ON r.BusinessPartnerId=b.Id AND r.TenantId=b.TenantId
  JOIN dbo.BusinessPartnerApProfileVersions p ON p.BusinessPartnerRoleId=r.Id AND p.TenantId=r.TenantId
  WHERE b.TenantId=@Tenant AND b.PartnerCode=e.Code AND b.IsDeleted=0 AND r.IsDeleted=0 AND r.RoleType=1 AND r.Status=1
    AND p.IsDeleted=0 AND p.Status=3 AND CONVERT(date,p.EffectiveFrom)<=CONVERT(date,@Now)
    AND (p.EffectiveTo IS NULL OR CONVERT(date,p.EffectiveTo)>=CONVERT(date,@Now)))<>1;
INSERT @Failures SELECT N'Supplier.LegacyIdentity:'+e.Code FROM @Suppliers e WHERE
 EXISTS(SELECT 1 FROM dbo.Suppliers s WHERE s.TenantId=@Tenant AND s.SupplierCode=e.Code);
INSERT @Failures SELECT DISTINCT N'Supplier.CrossTenantProfile:'+e.Code FROM @Suppliers e
 JOIN dbo.BusinessPartners b ON b.PartnerCode=e.Code AND b.TenantId=@Tenant
 JOIN dbo.BusinessPartnerRoles r ON r.BusinessPartnerId=b.Id
 LEFT JOIN dbo.BusinessPartnerApProfileVersions p ON p.BusinessPartnerRoleId=r.Id AND p.IsDeleted=0
 WHERE r.IsDeleted=0 AND (r.TenantId<>b.TenantId OR p.TenantId<>b.TenantId);

DECLARE @Responsibilities TABLE(UserName nvarchar(256),RoleName nvarchar(256),ScopeMode int);
INSERT @Responsibilities VALUES
(N'manager',N'TDC_STORES_OFFICER',2),(N'manager',N'TDC_STORES_MANAGER',1),
(N'procurementapprover',N'TDC_STORES_MANAGER',2),(N'ap.officer',N'TDC_STORES_MANAGER',2),
(N'financereviewer',N'TDC_FINANCE_REVIEWER',2),(N'employee',N'TDC_INTERNAL_AUDIT',2),
(N'storesofficer',N'TDC_STORES_OFFICER',2),(N'storesmanager',N'TDC_STORES_MANAGER',2);
DECLARE @Scopes TABLE(UserName nvarchar(256),RoleName nvarchar(256),WarehouseCode nvarchar(50));
INSERT @Scopes SELECT UserName,RoleName,N'DEMO-PM' FROM @Responsibilities WHERE ScopeMode=2;
INSERT @Scopes VALUES(N'storesofficer',N'TDC_STORES_OFFICER',N'WH-02'),(N'storesmanager',N'TDC_STORES_MANAGER',N'WH-02');
INSERT @Failures SELECT N'Responsibility.Active:'+e.UserName+N'/'+e.RoleName FROM @Responsibilities e WHERE
 (SELECT COUNT(*) FROM dbo.ProcurementResponsibilityAssignments a JOIN dbo.Users u ON u.Id=a.UserId
  JOIN dbo.AspNetRoles r ON r.Id=a.RoleId AND r.Name=a.RoleName
  WHERE a.TenantId=@Tenant AND u.UserName=e.UserName AND a.RoleName=e.RoleName AND a.IsDeleted=0 AND a.IsActive=1
    AND a.WarehouseScopeMode=e.ScopeMode AND a.LocationScopeMode=1 AND a.EffectiveFrom<=@Now
    AND (a.EffectiveTo IS NULL OR a.EffectiveTo>@Now))<>1;
INSERT @Failures SELECT N'Responsibility.Warehouse:'+e.UserName+N'/'+e.WarehouseCode FROM @Scopes e WHERE
 (SELECT COUNT(*) FROM dbo.ProcurementResponsibilityAssignments a JOIN dbo.Users u ON u.Id=a.UserId
  JOIN dbo.ProcurementResponsibilityWarehouses x ON x.AssignmentId=a.Id AND x.TenantId=a.TenantId
  JOIN dbo.Warehouses w ON w.Id=x.WarehouseId AND w.TenantId=x.TenantId
  WHERE a.TenantId=@Tenant AND u.UserName=e.UserName AND a.RoleName=e.RoleName AND a.IsDeleted=0 AND a.IsActive=1
    AND x.IsDeleted=0 AND w.IsDeleted=0 AND w.IsActive=1 AND w.Code=e.WarehouseCode
    AND a.EffectiveFrom<=@Now AND (a.EffectiveTo IS NULL OR a.EffectiveTo>@Now))<>1;
INSERT @Failures SELECT DISTINCT N'Responsibility.CrossTenantWarehouse:'+e.UserName FROM @Responsibilities e
 JOIN dbo.Users u ON u.UserName=e.UserName
 JOIN dbo.ProcurementResponsibilityAssignments a ON a.UserId=u.Id AND a.RoleName=e.RoleName AND a.TenantId=@Tenant
 JOIN dbo.ProcurementResponsibilityWarehouses x ON x.AssignmentId=a.Id
 LEFT JOIN dbo.Warehouses w ON w.Id=x.WarehouseId
 WHERE a.IsDeleted=0 AND x.IsDeleted=0 AND (x.TenantId<>a.TenantId OR w.Id IS NULL OR w.TenantId<>a.TenantId);

-- Deterministic semantic snapshots. Only explicitly named, nonsecret columns
-- enter these JSON payloads. The client receives hashes/counts, never payloads.
INSERT @State SELECT N'Actors',COUNT_BIG(*),(SELECT u.Id,u.UserName,u.TenantId,u.IsActive,u.AuthenticationProvider FROM dbo.Users u JOIN @Actors e ON e.UserName=u.UserName ORDER BY u.Id FOR JSON PATH,INCLUDE_NULL_VALUES) FROM dbo.Users u JOIN @Actors e ON e.UserName=u.UserName;
INSERT @State SELECT N'RoleLinks',COUNT_BIG(*),(SELECT ur.UserId,ur.RoleId,r.Name FROM dbo.UserRoles ur JOIN dbo.Users u ON u.Id=ur.UserId JOIN @Actors e ON e.UserName=u.UserName JOIN dbo.AspNetRoles r ON r.Id=ur.RoleId ORDER BY ur.UserId,ur.RoleId FOR JSON PATH,INCLUDE_NULL_VALUES) FROM dbo.UserRoles ur JOIN dbo.Users u ON u.Id=ur.UserId JOIN @Actors e ON e.UserName=u.UserName;
INSERT @State SELECT N'TenantMemberships',COUNT_BIG(*),(SELECT t.Id,t.UserId,t.TenantId,t.Status,t.AccessLevel,t.ExpiresAt,t.IsDeleted FROM dbo.UserTenants t JOIN dbo.Users u ON u.Id=t.UserId JOIN @Actors e ON e.UserName=u.UserName WHERE t.TenantId=@Tenant ORDER BY t.Id FOR JSON PATH,INCLUDE_NULL_VALUES) FROM dbo.UserTenants t JOIN dbo.Users u ON u.Id=t.UserId JOIN @Actors e ON e.UserName=u.UserName WHERE t.TenantId=@Tenant;
INSERT @State SELECT N'UnitsOfMeasure',COUNT_BIG(*),(SELECT u.Id,u.Code,u.TenantId,u.IsDeleted,u.IsActive,u.IsBaseUnit FROM dbo.UnitsOfMeasure u JOIN @Uoms e ON e.Code=u.Code WHERE u.TenantId=@Tenant ORDER BY u.Id FOR JSON PATH,INCLUDE_NULL_VALUES) FROM dbo.UnitsOfMeasure u JOIN @Uoms e ON e.Code=u.Code WHERE u.TenantId=@Tenant;
INSERT @State SELECT N'Categories',COUNT_BIG(*),(SELECT c.Id,c.Code,c.TenantId,c.DefaultUnitOfMeasure,c.IsActive,c.IsDeleted FROM dbo.InventoryCategories c JOIN @Categories e ON e.Code=c.Code WHERE c.TenantId=@Tenant ORDER BY c.Id FOR JSON PATH,INCLUDE_NULL_VALUES) FROM dbo.InventoryCategories c JOIN @Categories e ON e.Code=c.Code WHERE c.TenantId=@Tenant;
INSERT @State SELECT N'Warehouses',COUNT_BIG(*),(SELECT w.Id,w.Code,w.TenantId,w.IsActive,w.IsDefault,w.IsDeleted FROM dbo.Warehouses w JOIN @Warehouses e ON e.Code=w.Code WHERE w.TenantId=@Tenant ORDER BY w.Id FOR JSON PATH,INCLUDE_NULL_VALUES) FROM dbo.Warehouses w JOIN @Warehouses e ON e.Code=w.Code WHERE w.TenantId=@Tenant;
INSERT @State SELECT N'Bins',COUNT_BIG(*),(SELECT l.Id,l.WarehouseId,l.TenantId,l.LocationCode,l.IsActive,l.IsDefault,l.IsPickingLocation,l.IsReceivingLocation,l.IsDeleted FROM dbo.WarehouseLocations l JOIN dbo.Warehouses w ON w.Id=l.WarehouseId JOIN @Bins e ON e.WarehouseCode=w.Code AND e.Code=l.LocationCode WHERE w.TenantId=@Tenant ORDER BY l.Id FOR JSON PATH,INCLUDE_NULL_VALUES) FROM dbo.WarehouseLocations l JOIN dbo.Warehouses w ON w.Id=l.WarehouseId JOIN @Bins e ON e.WarehouseCode=w.Code AND e.Code=l.LocationCode WHERE w.TenantId=@Tenant;
INSERT @State SELECT N'Items',COUNT_BIG(*),(SELECT i.Id,i.ItemCode,i.TenantId,i.CategoryId,i.UnitOfMeasure,i.Status,i.StandardCost,i.AverageCost,i.LastPurchaseCost,i.IsDeleted FROM dbo.InventoryItems i JOIN @Items e ON e.Code=i.ItemCode WHERE i.TenantId=@Tenant ORDER BY i.Id FOR JSON PATH,INCLUDE_NULL_VALUES) FROM dbo.InventoryItems i JOIN @Items e ON e.Code=i.ItemCode WHERE i.TenantId=@Tenant;
INSERT @State SELECT N'ItemUomLinks',COUNT_BIG(*),(SELECT x.Id,x.TenantId,x.InventoryItemId,x.UnitOfMeasureId,x.ConversionToBase,x.IsBaseUnit,x.IsStockingUnit,x.IsPurchaseUnit,x.IsSalesUnit,x.IsActive,x.IsDeleted FROM dbo.ItemUnitsOfMeasure x JOIN dbo.InventoryItems i ON i.Id=x.InventoryItemId JOIN @Items e ON e.Code=i.ItemCode WHERE i.TenantId=@Tenant ORDER BY x.Id FOR JSON PATH,INCLUDE_NULL_VALUES) FROM dbo.ItemUnitsOfMeasure x JOIN dbo.InventoryItems i ON i.Id=x.InventoryItemId JOIN @Items e ON e.Code=i.ItemCode WHERE i.TenantId=@Tenant;
INSERT @State SELECT N'CanonicalSuppliers',COUNT_BIG(*),(SELECT b.Id,b.PartnerCode,b.TenantId,b.IsActive,b.IsDeleted,b.Currency FROM dbo.BusinessPartners b JOIN @Suppliers e ON e.Code=b.PartnerCode WHERE b.TenantId=@Tenant ORDER BY b.Id FOR JSON PATH,INCLUDE_NULL_VALUES) FROM dbo.BusinessPartners b JOIN @Suppliers e ON e.Code=b.PartnerCode WHERE b.TenantId=@Tenant;
INSERT @State SELECT N'SupplierRoles',COUNT_BIG(*),(SELECT r.Id,r.TenantId,r.BusinessPartnerId,r.RoleType,r.Status,r.ActiveFromUtc,r.InactiveFromUtc,r.IsDeleted FROM dbo.BusinessPartnerRoles r JOIN dbo.BusinessPartners b ON b.Id=r.BusinessPartnerId JOIN @Suppliers e ON e.Code=b.PartnerCode WHERE b.TenantId=@Tenant ORDER BY r.Id FOR JSON PATH,INCLUDE_NULL_VALUES) FROM dbo.BusinessPartnerRoles r JOIN dbo.BusinessPartners b ON b.Id=r.BusinessPartnerId JOIN @Suppliers e ON e.Code=b.PartnerCode WHERE b.TenantId=@Tenant;
INSERT @State SELECT N'SupplierApProfiles',COUNT_BIG(*),(SELECT p.Id,p.TenantId,p.BusinessPartnerRoleId,p.VersionNumber,p.Status,p.EffectiveFrom,p.EffectiveTo,p.PaymentTermId,p.DefaultTaxGroupId,p.DefaultExpenseAccountId,p.SubjectToWithholding,p.DefaultWithholdingLineId,p.IsDeleted FROM dbo.BusinessPartnerApProfileVersions p JOIN dbo.BusinessPartnerRoles r ON r.Id=p.BusinessPartnerRoleId JOIN dbo.BusinessPartners b ON b.Id=r.BusinessPartnerId JOIN @Suppliers e ON e.Code=b.PartnerCode WHERE b.TenantId=@Tenant ORDER BY p.Id FOR JSON PATH,INCLUDE_NULL_VALUES) FROM dbo.BusinessPartnerApProfileVersions p JOIN dbo.BusinessPartnerRoles r ON r.Id=p.BusinessPartnerRoleId JOIN dbo.BusinessPartners b ON b.Id=r.BusinessPartnerId JOIN @Suppliers e ON e.Code=b.PartnerCode WHERE b.TenantId=@Tenant;
INSERT @State SELECT N'LegacySuppliers',COUNT_BIG(*),(SELECT s.Id,s.TenantId,s.SupplierCode,s.IsDeleted FROM dbo.Suppliers s JOIN @Suppliers e ON e.Code=s.SupplierCode WHERE s.TenantId=@Tenant ORDER BY s.Id FOR JSON PATH,INCLUDE_NULL_VALUES) FROM dbo.Suppliers s JOIN @Suppliers e ON e.Code=s.SupplierCode WHERE s.TenantId=@Tenant;
INSERT @State SELECT N'Responsibilities',COUNT_BIG(*),(SELECT a.Id,a.TenantId,a.UserId,a.RoleId,a.RoleName,a.WarehouseScopeMode,a.LocationScopeMode,a.EffectiveFrom,a.EffectiveTo,a.IsActive,a.IsDeleted FROM dbo.ProcurementResponsibilityAssignments a JOIN dbo.Users u ON u.Id=a.UserId JOIN @Responsibilities e ON e.UserName=u.UserName AND e.RoleName=a.RoleName WHERE a.TenantId=@Tenant ORDER BY a.Id FOR JSON PATH,INCLUDE_NULL_VALUES) FROM dbo.ProcurementResponsibilityAssignments a JOIN dbo.Users u ON u.Id=a.UserId JOIN @Responsibilities e ON e.UserName=u.UserName AND e.RoleName=a.RoleName WHERE a.TenantId=@Tenant;
INSERT @State SELECT N'ResponsibilityWarehouses',COUNT_BIG(*),(SELECT x.Id,x.TenantId,x.AssignmentId,x.WarehouseId,x.IsDeleted FROM dbo.ProcurementResponsibilityWarehouses x JOIN dbo.ProcurementResponsibilityAssignments a ON a.Id=x.AssignmentId JOIN dbo.Users u ON u.Id=a.UserId JOIN @Responsibilities e ON e.UserName=u.UserName AND e.RoleName=a.RoleName WHERE a.TenantId=@Tenant ORDER BY x.Id FOR JSON PATH,INCLUDE_NULL_VALUES) FROM dbo.ProcurementResponsibilityWarehouses x JOIN dbo.ProcurementResponsibilityAssignments a ON a.Id=x.AssignmentId JOIN dbo.Users u ON u.Id=a.UserId JOIN @Responsibilities e ON e.UserName=u.UserName AND e.RoleName=a.RoleName WHERE a.TenantId=@Tenant;
DECLARE @Payload nvarchar(max)=(SELECT Name,RecordCount,Payload FROM @State ORDER BY Name FOR JSON PATH,INCLUDE_NULL_VALUES);
SELECT N'Count' RowType,Name,RecordCount,CAST(NULL AS varchar(64)) Fingerprint FROM @State
UNION ALL SELECT N'Fingerprint',N'OperationalUat',NULL,CONVERT(varchar(64),HASHBYTES('SHA2_256',@Payload),2)
UNION ALL SELECT N'Failure',Name,1,NULL FROM @Failures
ORDER BY RowType,Name;
'@
}

function Get-RhemaMissingUserNames {
    param(
        [Parameter(Mandatory=$true)][string]$ConnectionString,
        [Parameter(Mandatory=$true)][string]$DatabaseName,
        [Parameter(Mandatory=$true)][string[]]$UserNames
    )
    $required=@($UserNames | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    if($required.Count -eq 0){return @()}
    foreach($name in $required){
        if($name -cnotmatch '^[A-Za-z0-9._-]{1,256}$'){throw 'Operational UAT actor name is invalid.'}
    }
    $connection=$null;$command=$null
    try {
        $builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder $ConnectionString
        if([string]::IsNullOrWhiteSpace($DatabaseName) -or $builder.InitialCatalog -cne $DatabaseName){
            throw 'Explicit database identity does not match the connection.'
        }
        $connection=New-Object System.Data.SqlClient.SqlConnection $ConnectionString
        $connection.Open()
        $command=$connection.CreateCommand();$command.CommandTimeout=120
        $values=New-Object 'System.Collections.Generic.List[string]'
        for($index=0;$index -lt $required.Count;$index++){
            $parameterName='@User'+$index
            [void]$values.Add('('+ $parameterName +')')
            [void]$command.Parameters.Add($parameterName,[System.Data.SqlDbType]::NVarChar,256)
            $command.Parameters[$parameterName].Value=$required[$index]
        }
        [void]$command.Parameters.Add('@ExpectedDatabase',[System.Data.SqlDbType]::NVarChar,128)
        $command.Parameters['@ExpectedDatabase'].Value=$DatabaseName
        $command.CommandText=@"
SET NOCOUNT ON;
IF DB_NAME()<>@ExpectedDatabase THROW 51998,'Operational actor target mismatch.',1;
DECLARE @Tenant uniqueidentifier=(SELECT Id FROM dbo.Tenants WHERE Code=N'DEFAULT' AND IsDeleted=0 AND Status=1);
IF @Tenant IS NULL THROW 51997,'Active DEFAULT tenant is missing.',1;
DECLARE @Required TABLE(UserName nvarchar(256) PRIMARY KEY);
INSERT @Required(UserName) VALUES $($values -join ',');
SELECT r.UserName FROM @Required r
WHERE NOT EXISTS(SELECT 1 FROM dbo.Users u WHERE u.TenantId=@Tenant AND u.UserName=r.UserName)
ORDER BY r.UserName;
"@
        $missing=New-Object 'System.Collections.Generic.List[string]'
        $reader=$command.ExecuteReader()
        try{while($reader.Read()){[void]$missing.Add([string]$reader.GetString(0))}}finally{$reader.Dispose()}
        return @($missing)
    }catch{
        $failure=$_.Exception
        while($failure.InnerException -and $failure -isnot [System.Data.SqlClient.SqlException]){$failure=$failure.InnerException}
        $code=if($failure -is [System.Data.SqlClient.SqlException]){[string][int]$failure.Number}else{'unavailable'}
        throw "Operational UAT actor check failed (SQL number $code). Connection details were not logged."
    }finally{if($command){$command.Dispose()};if($connection){$connection.Dispose()}}
}

function Get-RhemaMissingOperationalActorNames {
    param([Parameter(Mandatory=$true)][string]$ConnectionString,
          [Parameter(Mandatory=$true)][string]$DatabaseName)
    return @(Get-RhemaMissingUserNames -ConnectionString $ConnectionString -DatabaseName $DatabaseName -UserNames @(
        'procurementofficer','procurementapprover','procurementevaluator','tdc0102-checker-201531',
        'financereviewer','financeapprover','storesofficer','storesmanager',
        'uat.qs.preparer','uat.qs.reviewer','uat.qs.approver'
    ))
}

function Get-RhemaOperationalSeedSnapshot {
    param([Parameter(Mandatory=$true)][string]$ConnectionString,
          [Parameter(Mandatory=$true)][string]$DatabaseName)
    $connection=$null; $command=$null
    try {
        $builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder $ConnectionString
        if ([string]::IsNullOrWhiteSpace($DatabaseName) -or $builder.InitialCatalog -cne $DatabaseName) {
            throw 'Explicit database identity does not match the connection.'
        }
        $connection=New-Object System.Data.SqlClient.SqlConnection $ConnectionString
        $connection.Open()
        $command=$connection.CreateCommand(); $command.CommandTimeout=120
        $command.CommandText=Get-RhemaOperationalSeedVerificationSql
        [void]$command.Parameters.Add('@ExpectedDatabase',[System.Data.SqlDbType]::NVarChar,128)
        $command.Parameters['@ExpectedDatabase'].Value=$DatabaseName
        $table=New-Object System.Data.DataTable
        $reader=$command.ExecuteReader()
        try { $table.Load($reader) } finally { $reader.Dispose() }
        $counts=[ordered]@{}; $failures=@(); $fingerprint=$null
        foreach($row in $table.Rows) {
            switch([string]$row.RowType) {
                'Count' { $counts[[string]$row.Name]=[long]$row.RecordCount }
                'Failure' { $failures += [string]$row.Name }
                'Fingerprint' { $fingerprint=[string]$row.Fingerprint }
            }
        }
        if ($fingerprint -cnotmatch '^[A-F0-9]{64}$') { throw 'Verification fingerprint missing.' }
        return [pscustomobject]@{ Counts=$counts; Fingerprint=$fingerprint; Failures=$failures; Ready=($failures.Count -eq 0) }
    } catch {
        $failure=$_.Exception
        while($failure.InnerException -and $failure -isnot [System.Data.SqlClient.SqlException]){$failure=$failure.InnerException}
        $code=if($failure -is [System.Data.SqlClient.SqlException]){[string][int]$failure.Number}else{'unavailable'}
        throw "Operational UAT read-only verification failed (SQL number $code). Connection details were not logged."
    } finally { if($command){$command.Dispose()}; if($connection){$connection.Dispose()} }
}

function Assert-RhemaOperationalSeedReadiness {
    param([Parameter(Mandatory=$true)]$Snapshot)
    if (-not $Snapshot.Ready -or @($Snapshot.Failures).Count -gt 0) {
        throw ('Operational UAT readiness failed: '+(@($Snapshot.Failures) -join '; ')+'. Existing users and tenant-owned records were not overwritten; review the reported master data.')
    }
}
