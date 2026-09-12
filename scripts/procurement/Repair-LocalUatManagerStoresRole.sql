-- User-authorized local UAT configuration repair, 9 September 2026.
-- Run separately against the two allow-listed databases. No business records
-- or role-permission definitions are changed. Existing assignments are retained.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) <> N'RHEMA-MICHAEL\SQL2017'
    THROW 51007, 'This repair is restricted to the verified local SQL Server.', 1;
IF DB_NAME() NOT IN (N'RhemaERP', N'RhemaERP_PO_Rehearsal_20260909')
    THROW 51000, 'This repair is restricted to the named local UAT databases.', 1;

DECLARE @user uniqueidentifier = '9e475ce9-34ad-4a6d-0dbc-08de862e82ee';
DECLARE @role uniqueidentifier = '10958480-d251-4c77-8dc0-5823889c2270';
DECLARE @tenant uniqueidentifier = '00000000-0000-0000-0000-000000000001';
DECLARE @warehouse uniqueidentifier = '39e1b1fb-17c8-41cb-a703-f0a6e740acc8';
DECLARE @po uniqueidentifier = 'a7393eec-565c-4ff5-8346-184315d8bc95';

BEGIN TRY
    BEGIN TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id=@user AND UserName=N'manager' AND TenantId=@tenant AND IsActive=1)
        THROW 51001, 'The expected active manager account was not found.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.AspNetRoles WHERE Id=@role AND Name=N'TDC_STORES_OFFICER')
        THROW 51002, 'The expected Stores Officer role was not found.', 1;
    IF (SELECT COUNT(*) FROM dbo.RolePermissions rp JOIN dbo.Permissions p ON p.Id=rp.PermissionId
        WHERE rp.RoleId=@role AND p.IsDeleted=0 AND p.Name IN (N'procurement.inventory.read',N'procurement.inventory.receive')) <> 2
        THROW 51003, 'The existing Stores Officer permissions are incomplete.', 1;
    IF NOT EXISTS (
        SELECT 1 FROM dbo.ProcurementResponsibilityAssignments a
        JOIN dbo.ProcurementResponsibilityWarehouses aw ON aw.AssignmentId=a.Id AND aw.IsDeleted=0
        JOIN dbo.Warehouses w ON w.Id=aw.WarehouseId AND w.IsDeleted=0 AND w.IsActive=1
        WHERE a.UserId=@user AND a.RoleId=@role AND a.RoleName=N'TDC_STORES_OFFICER' AND a.TenantId=@tenant
          AND a.IsDeleted=0 AND a.IsActive=1 AND a.EffectiveFrom<=SYSUTCDATETIME()
          AND (a.EffectiveTo IS NULL OR a.EffectiveTo>=SYSUTCDATETIME())
          AND a.WarehouseScopeMode=2 AND a.LocationScopeMode=1 AND w.Id=@warehouse AND w.Code=N'DEMO-PM')
        THROW 51004, 'The expected effective DEMO-PM Stores scope was not found.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.WarehouseLocations WHERE WarehouseId=@warehouse AND LocationCode=N'LOC-001' AND IsActive=1 AND IsDeleted=0)
        THROW 51005, 'The expected receiving location was not found.', 1;

    DECLARE @before nvarchar(max) = CONCAT(
        (SELECT * FROM dbo.PurchaseOrders WHERE Id=@po FOR JSON PATH),
        (SELECT * FROM dbo.PurchaseOrderItems WHERE PurchaseOrderId=@po ORDER BY Id FOR JSON PATH),
        (SELECT * FROM dbo.PurchaseOrderReceipts WHERE PurchaseOrderId=@po ORDER BY Id FOR JSON PATH),
        (SELECT * FROM dbo.GoodsReceiptNotes WHERE PurchaseOrderId=@po ORDER BY Id FOR JSON PATH),
        (SELECT * FROM dbo.VendorInvoice WHERE PurchaseOrderId=@po ORDER BY Id FOR JSON PATH));
    DECLARE @beforeRoles nvarchar(max) = (SELECT r.Name FROM dbo.UserRoles ur JOIN dbo.AspNetRoles r ON r.Id=ur.RoleId WHERE ur.UserId=@user ORDER BY r.Name FOR JSON PATH);
    DECLARE @added int = 0;
    IF NOT EXISTS (SELECT 1 FROM dbo.UserRoles WITH (UPDLOCK,HOLDLOCK) WHERE UserId=@user AND RoleId=@role)
    BEGIN
        INSERT dbo.UserRoles (UserId,RoleId) VALUES (@user,@role);
        SET @added=1;
    END;
    DECLARE @after nvarchar(max) = CONCAT(
        (SELECT * FROM dbo.PurchaseOrders WHERE Id=@po FOR JSON PATH),
        (SELECT * FROM dbo.PurchaseOrderItems WHERE PurchaseOrderId=@po ORDER BY Id FOR JSON PATH),
        (SELECT * FROM dbo.PurchaseOrderReceipts WHERE PurchaseOrderId=@po ORDER BY Id FOR JSON PATH),
        (SELECT * FROM dbo.GoodsReceiptNotes WHERE PurchaseOrderId=@po ORDER BY Id FOR JSON PATH),
        (SELECT * FROM dbo.VendorInvoice WHERE PurchaseOrderId=@po ORDER BY Id FOR JSON PATH));
    IF HASHBYTES('SHA2_256',@before)<>HASHBYTES('SHA2_256',@after)
        THROW 51006, 'Business-record preservation check failed; repair rolled back.', 1;
    COMMIT;
    SELECT DB_NAME() DatabaseName,N'manager' UserName,N'TDC_STORES_OFFICER' AddedRole,@added RowsAdded,
        N'DEMO-PM (existing scope preserved)' WarehouseScope,
        @beforeRoles PreviousRoles,
        (SELECT r.Name FROM dbo.UserRoles ur JOIN dbo.AspNetRoles r ON r.Id=ur.RoleId WHERE ur.UserId=@user ORDER BY r.Name FOR JSON PATH) CurrentRoles,
        CONVERT(varchar(64),HASHBYTES('SHA2_256',@after),2) PreservedBusinessFingerprint,
        SYSUTCDATETIME() AppliedAtUtc;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
