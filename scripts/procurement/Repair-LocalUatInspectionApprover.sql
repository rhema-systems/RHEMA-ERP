-- User-authorized local configuration repair, 9 September 2026.
-- Supply @Apply bit as a SQL parameter (0 = preflight; 1 = apply).
-- Preserve the expired historical assignment; create a new DEMO-PM assignment.
-- This does not submit, approve, sign, post, or change workflow definitions.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) <> N'RHEMA-MICHAEL\SQL2017'
    THROW 51000, 'Unexpected SQL Server; repair is local only.', 1;
IF DB_NAME() NOT IN (N'RhemaERP',N'RhemaERP_PO_Rehearsal_20260909')
    THROW 51001, 'Unexpected database; repair is local UAT only.', 1;

DECLARE @user uniqueidentifier='45eacad1-fff9-40f4-91fb-a0e72e2fa7f7';
DECLARE @role uniqueidentifier='a9a30119-1c67-4ff4-9500-5bc7d0a37846';
DECLARE @tenant uniqueidentifier='00000000-0000-0000-0000-000000000001';
DECLARE @warehouse uniqueidentifier='39e1b1fb-17c8-41cb-a703-f0a6e740acc8';
DECLARE @assignment uniqueidentifier='70b5d776-e296-458b-bd9c-ef05897f65eb';
DECLARE @warehouseLink uniqueidentifier='0f48b160-634c-471d-a4fa-d5cc70703083';
DECLARE @oldAssignment uniqueidentifier='ae389e55-49c8-4f39-b128-5c1dd793298d';
DECLARE @po uniqueidentifier='a7393eec-565c-4ff5-8346-184315d8bc95';
DECLARE @now datetime2=SYSUTCDATETIME();
DECLARE @actor nvarchar(100)=N'Codex - user-authorized local UAT repair';
DECLARE @reason nvarchar(1000)=N'User requested eligible active inspection approver in main UAT and rehearsal on 2026-09-09. procurementapprover is the independent Stores Manager for DEMO-PM only. Keep available during ongoing local UAT; review and remove this access after demonstrations. No workflow, Finance or payment permission definitions changed.';

BEGIN TRY
    BEGIN TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id=@user AND UserName=N'procurementapprover' AND IsActive=1 AND TenantId=@tenant)
        THROW 51002, 'Expected active independent approver is missing.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.AspNetRoles WHERE Id=@role AND Name=N'TDC_STORES_MANAGER')
        THROW 51003, 'Expected existing Stores Manager role is missing.', 1;
    IF (SELECT COUNT(*) FROM dbo.RolePermissions rp JOIN dbo.Permissions p ON p.Id=rp.PermissionId
        WHERE rp.RoleId=@role AND p.IsDeleted=0 AND p.Name IN (N'procurement.inventory.read',N'procurement.inventory.receive'))<>2
        THROW 51004, 'Existing Stores Manager permissions are incomplete.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Warehouses WHERE Id=@warehouse AND Code=N'DEMO-PM' AND TenantId=@tenant AND IsActive=1 AND IsDeleted=0)
        THROW 51005, 'Expected local UAT warehouse is missing.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.ProcurementResponsibilityAssignments WHERE Id=@oldAssignment AND UserId=@user
        AND RoleId=@role AND TenantId=@tenant AND IsActive=0 AND IsDeleted=0)
        THROW 51006, 'Historical inactive assignment changed; inspect before proceeding.', 1;
    IF EXISTS (SELECT 1 FROM dbo.ProcurementResponsibilityAssignments WITH (UPDLOCK,HOLDLOCK)
        WHERE UserId=@user AND TenantId=@tenant AND RoleName=N'TDC_STORES_MANAGER'
        AND IsDeleted=0 AND IsActive=1 AND Id<>@assignment)
        THROW 51007, 'Another active Stores Manager assignment already exists; inspect it.', 1;
    IF EXISTS (SELECT 1 FROM dbo.PurchaseOrders WHERE Id=@po AND CreatedById=@user)
        OR EXISTS (SELECT 1 FROM dbo.PurchaseOrderReceipts WHERE PurchaseOrderId=@po AND ReceivedById=@user)
        OR EXISTS (SELECT 1 FROM dbo.ProcurementReceiptInspectionCases c JOIN dbo.PurchaseOrderReceipts r ON r.Id=c.PurchaseOrderReceiptId
            WHERE r.PurchaseOrderId=@po AND (c.CreatedByUserId=@user OR c.SubmittedByUserId=@user))
        THROW 51008, 'Approver is a maker or receiver on the UAT source; preserve independence.', 1;

    -- Snapshot protected business/control tables and the original assignment.
    DECLARE @protected TABLE (Name sysname PRIMARY KEY, BeforeHash varbinary(32));
    INSERT @protected(Name) VALUES
        ('PurchaseOrders'),('PurchaseOrderItems'),('PurchaseOrderReceipts'),('GoodsReceiptNotes'),('VendorInvoice'),
        ('ProcurementReceiptInspectionCases'),('ProcurementReceiptInspectionLines'),('ProcurementReceiptInspectionEvidence'),
        ('ProcurementReceiptInspectionActions'),('ProcurementReceiptDocuments'),('ProcurementReceiptDocumentSignatures'),
        ('ProcurementReceiptDocumentActions'),('WorkflowDefinitions'),('WorkflowSteps'),('WorkflowInstances'),('RolePermissions');
    DECLARE @table sysname,@sql nvarchar(max),@hash varbinary(32),@old nvarchar(max),@previousRoles nvarchar(max);
    SET @old=(SELECT * FROM dbo.ProcurementResponsibilityAssignments WHERE Id=@oldAssignment FOR JSON PATH);
    SET @previousRoles=(SELECT RoleId FROM dbo.UserRoles WHERE UserId=@user ORDER BY RoleId FOR JSON PATH);
    DECLARE protected_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT Name FROM @protected ORDER BY Name;
    OPEN protected_cursor;
    FETCH NEXT FROM protected_cursor INTO @table;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @sql=N'SELECT @result=HASHBYTES(''SHA2_256'',COALESCE((SELECT * FROM dbo.'+QUOTENAME(@table)+N' ORDER BY '+CASE WHEN @table=N'RolePermissions' THEN N'RoleId,PermissionId' ELSE N'Id' END+N' FOR JSON PATH),N''[]''));';
        EXEC sys.sp_executesql @sql,N'@result varbinary(32) OUTPUT',@result=@hash OUTPUT;
        UPDATE @protected SET BeforeHash=@hash WHERE Name=@table;
        FETCH NEXT FROM protected_cursor INTO @table;
    END;
    CLOSE protected_cursor;
    DEALLOCATE protected_cursor;

    DECLARE @rolesAdded int=0,@assignmentsAdded int=0,@linksAdded int=0;
    IF @Apply=1
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.UserRoles WITH (UPDLOCK,HOLDLOCK) WHERE UserId=@user AND RoleId=@role)
        BEGIN
            INSERT dbo.UserRoles(UserId,RoleId) VALUES(@user,@role);
            SET @rolesAdded=1;
        END;
        IF NOT EXISTS (SELECT 1 FROM dbo.ProcurementResponsibilityAssignments WHERE Id=@assignment)
        BEGIN
            INSERT dbo.ProcurementResponsibilityAssignments
                (Id,UserId,RoleId,RoleName,WarehouseScopeMode,LocationScopeMode,EffectiveFrom,EffectiveTo,IsActive,Reason,CreatedAt,CreatedBy,IsDeleted,TenantId)
            VALUES(@assignment,@user,@role,N'TDC_STORES_MANAGER',2,1,@now,NULL,1,@reason,@now,@actor,0,@tenant);
            SET @assignmentsAdded=1;
        END;
        IF NOT EXISTS (SELECT 1 FROM dbo.ProcurementResponsibilityWarehouses WHERE Id=@warehouseLink)
        BEGIN
            INSERT dbo.ProcurementResponsibilityWarehouses(Id,AssignmentId,WarehouseId,CreatedAt,CreatedBy,IsDeleted,TenantId)
            VALUES(@warehouseLink,@assignment,@warehouse,@now,@actor,0,@tenant);
            SET @linksAdded=1;
        END;
        IF NOT EXISTS (SELECT 1 FROM dbo.ProcurementResponsibilityAssignments a
            JOIN dbo.ProcurementResponsibilityWarehouses w ON w.AssignmentId=a.Id AND w.IsDeleted=0
            WHERE a.Id=@assignment AND a.UserId=@user AND a.RoleId=@role AND a.TenantId=@tenant
            AND a.IsDeleted=0 AND a.IsActive=1 AND a.EffectiveFrom<=@now AND a.EffectiveTo IS NULL
            AND a.WarehouseScopeMode=2 AND a.LocationScopeMode=1 AND w.WarehouseId=@warehouse)
            THROW 51009, 'Expected effective assignment not present after repair.', 1;
        IF (SELECT COUNT(*) FROM dbo.ProcurementResponsibilityWarehouses WHERE AssignmentId=@assignment AND IsDeleted=0)<>1
            OR EXISTS (SELECT 1 FROM dbo.ProcurementResponsibilityLocations WHERE AssignmentId=@assignment AND IsDeleted=0)
            THROW 51010, 'Assignment has unexpected additional or conflicting scope.', 1;
        IF @rolesAdded+@assignmentsAdded+@linksAdded>0
            INSERT dbo.SecurityLogs(Id,UserId,Username,Action,IpAddress,UserAgent,Success,Details,Timestamp,TenantId,CreatedAt,CreatedBy,IsDeleted)
            VALUES(NEWID(),NULL,@actor,N'LOCAL_UAT_STORES_APPROVER_CONFIGURED',N'Local SQL',N'Repair-LocalUatInspectionApprover.sql',1,
                CONCAT(N'Target=procurementapprover; Role=TDC_STORES_MANAGER; new assignment=',CONVERT(nvarchar(36),@assignment),
                    N'; warehouse=DEMO-PM; EffectiveTo=NULL for ongoing UAT; role added=',@rolesAdded,
                    N'; assignment added=',@assignmentsAdded,N'; previous roles=',@previousRoles,
                    N'; old inactive assignment preserved. User explicitly authorized both local databases; no business approval performed.'),
                @now,@tenant,@now,@actor,0);
    END;

    IF @old<>(SELECT * FROM dbo.ProcurementResponsibilityAssignments WHERE Id=@oldAssignment FOR JSON PATH)
        THROW 51011, 'Historical assignment changed; rollback.', 1;
    DECLARE verify_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT Name FROM @protected ORDER BY Name;
    OPEN verify_cursor;
    FETCH NEXT FROM verify_cursor INTO @table;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @sql=N'SELECT @result=HASHBYTES(''SHA2_256'',COALESCE((SELECT * FROM dbo.'+QUOTENAME(@table)+N' ORDER BY '+CASE WHEN @table=N'RolePermissions' THEN N'RoleId,PermissionId' ELSE N'Id' END+N' FOR JSON PATH),N''[]''));';
        EXEC sys.sp_executesql @sql,N'@result varbinary(32) OUTPUT',@result=@hash OUTPUT;
        IF @hash<>(SELECT BeforeHash FROM @protected WHERE Name=@table)
            THROW 51012, 'Protected business/control data changed; repair rolled back.', 1;
        FETCH NEXT FROM verify_cursor INTO @table;
    END;
    CLOSE verify_cursor;
    DEALLOCATE verify_cursor;
    COMMIT;
    SELECT (SELECT DB_NAME() DatabaseName,@Apply Applied,@rolesAdded RolesAdded,@assignmentsAdded AssignmentsAdded,
        @linksAdded WarehouseLinksAdded,@assignment AssignmentId,@now CheckedAtUtc,
        (SELECT u.UserName FROM dbo.Users u JOIN dbo.UserRoles ur ON ur.UserId=u.Id
            WHERE ur.RoleId=@role AND u.IsActive=1 AND u.TenantId=@tenant
            AND u.Id<>'9e475ce9-34ad-4a6d-0dbc-08de862e82ee' FOR JSON PATH) EligibleIndependentApprovers,
        (SELECT Name,CONVERT(varchar(64),BeforeHash,2) PreservedHash FROM @protected ORDER BY Name FOR JSON PATH) PreservedTables
        FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
