using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

internal static class PhysicalCountRecountGuards
{
    public static void Install(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE p SET ObservationSubmittedAtUtc=a.SubmittedAt FROM dbo.PhysicalCounts p
            CROSS APPLY(SELECT MIN(OccurredAtUtc) SubmittedAt FROM dbo.PhysicalCountActions WHERE PhysicalCountId=p.Id AND TenantId=p.TenantId AND ActionType=7 AND IsDeleted=0) a
            WHERE p.ObservationSubmittedAtUtc IS NULL AND a.SubmittedAt IS NOT NULL;
            """);
        migrationBuilder.Sql(FreezePatch);
        migrationBuilder.Sql(LineGuard);
        migrationBuilder.Sql(ClaimGuard);
        migrationBuilder.Sql(CountGuard);
        migrationBuilder.Sql(AdjustmentGuard);
    }

    public static void Remove(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        IF EXISTS(SELECT 1 FROM dbo.PhysicalCounts WHERE RootPhysicalCountId IS NOT NULL) OR EXISTS(SELECT 1 FROM dbo.PhysicalCountAdjustmentClaims)
          THROW 51993,'INV_RECOUNT_DOWN_BLOCKED: retained recount or resolution history cannot be removed.',1;
        DECLARE @name sysname,@sql nvarchar(max);
        DECLARE @old nvarchar(max)=N'p.Status IN (N''InProgress'',N''UnderReview'',N''UnderInvestigation'',N''RecountRequired'',N''PendingStoresApproval'',N''PendingFinanceApproval'',N''PendingAuditAttestation'',N''ReadyToPost'')';
        DECLARE @new nvarchar(max)=N'('+@old+N' OR (p.Status=N''Draft'' AND p.RootPhysicalCountId IS NOT NULL))';
        DECLARE guardCursor CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM sys.triggers WHERE name IN
          (N'TR_WarehouseQuantities_PhysicalCountFreeze',N'TR_InventoryItems_PhysicalCountFreeze',N'TR_StockMovements_PhysicalCountFreeze');
        OPEN guardCursor; FETCH NEXT FROM guardCursor INTO @name;
        WHILE @@FETCH_STATUS=0 BEGIN
          SET @sql=OBJECT_DEFINITION(OBJECT_ID(N'dbo.'+@name));
          IF CHARINDEX(@new,@sql)=0 THROW 51993,'INV_RECOUNT_DOWN_DRIFT: recount freeze guard is missing.',1;
          SET @sql=REPLACE(@sql,@new,@old);
          SET @sql=STUFF(@sql,1,CHARINDEX(N'TRIGGER',UPPER(@sql))-1,N'CREATE OR ALTER ');
          EXEC sys.sp_executesql @sql;
          FETCH NEXT FROM guardCursor INTO @name;
        END;
        CLOSE guardCursor; DEALLOCATE guardCursor;
        DROP TRIGGER IF EXISTS dbo.TR_StockAdjustments_CountResolutionClaims;
        DROP TRIGGER IF EXISTS dbo.TR_PhysicalCounts_RecountLineage;
        DROP TRIGGER IF EXISTS dbo.TR_PhysicalCountItems_RecountLineage;
        DROP TRIGGER IF EXISTS dbo.TR_PhysicalCountAdjustmentClaims_Immutable;
        """);

    public const string FreezePatch = """
        DECLARE @name sysname,@sql nvarchar(max);
        DECLARE @old nvarchar(max)=N'p.Status IN (N''InProgress'',N''UnderReview'',N''UnderInvestigation'',N''RecountRequired'',N''PendingStoresApproval'',N''PendingFinanceApproval'',N''PendingAuditAttestation'',N''ReadyToPost'')';
        DECLARE @new nvarchar(max)=N'('+@old+N' OR (p.Status=N''Draft'' AND p.RootPhysicalCountId IS NOT NULL))';
        DECLARE guardCursor CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM sys.triggers WHERE name IN
          (N'TR_WarehouseQuantities_PhysicalCountFreeze',N'TR_InventoryItems_PhysicalCountFreeze',N'TR_StockMovements_PhysicalCountFreeze');
        IF (SELECT COUNT(*) FROM sys.triggers WHERE name IN(N'TR_WarehouseQuantities_PhysicalCountFreeze',N'TR_InventoryItems_PhysicalCountFreeze',N'TR_StockMovements_PhysicalCountFreeze'))<>3
          THROW 51988,'INV_RECOUNT_FREEZE_MISSING: existing freeze guards are required.',1;
        OPEN guardCursor; FETCH NEXT FROM guardCursor INTO @name;
        WHILE @@FETCH_STATUS=0 BEGIN
          SET @sql=OBJECT_DEFINITION(OBJECT_ID(N'dbo.'+@name));
          IF CHARINDEX(@old,@sql)=0 THROW 51988,'INV_RECOUNT_FREEZE_DRIFT: expected count scope is missing.',1;
          SET @sql=REPLACE(@sql,@old,@new);
          SET @sql=STUFF(@sql,1,CHARINDEX(N'TRIGGER',UPPER(@sql))-1,N'CREATE OR ALTER ');
          EXEC sys.sp_executesql @sql;
          FETCH NEXT FROM guardCursor INTO @name;
        END;
        CLOSE guardCursor; DEALLOCATE guardCursor;
        """;

    public const string LineGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_PhysicalCountItems_RecountLineage ON dbo.PhysicalCountItems AFTER INSERT,UPDATE,DELETE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id JOIN dbo.PhysicalCounts p ON p.Id=d.PhysicalCountId
            WHERE i.Id IS NULL AND p.RootPhysicalCountId IS NOT NULL)
            THROW 51989,'INV_RECOUNT_LINE_RETAINED: selected recount lines cannot be removed.',1;
          IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.PhysicalCounts p ON p.Id=i.PhysicalCountId AND p.TenantId=i.TenantId
            LEFT JOIN dbo.PhysicalCountItems previous ON previous.Id=i.PredecessorPhysicalCountItemId AND previous.TenantId=i.TenantId
            LEFT JOIN dbo.PhysicalCountItems root ON root.Id=i.RootPhysicalCountItemId AND root.TenantId=i.TenantId
            WHERE p.RootPhysicalCountId IS NOT NULL AND (previous.Id IS NULL OR root.Id IS NULL OR previous.PhysicalCountId<>p.ParentPhysicalCountId
              OR root.PhysicalCountId<>p.RootPhysicalCountId OR i.InventoryItemId<>root.InventoryItemId OR i.SystemQuantity<>root.SystemQuantity OR i.UnitCost<>root.UnitCost
              OR (root.LocationId IS NOT NULL AND ISNULL(i.LocationId,'00000000-0000-0000-0000-000000000000')<>root.LocationId)
              OR ISNULL(i.LotNumber,N'')<>ISNULL(root.LotNumber,N'') OR ISNULL(i.SerialNumber,N'')<>ISNULL(root.SerialNumber,N'')))
            THROW 51989,'INV_RECOUNT_LINEAGE_INVALID: recount lines must preserve original observations and tracking.',1;
          IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.Id=d.Id JOIN dbo.PhysicalCounts p ON p.Id=i.PhysicalCountId AND p.TenantId=i.TenantId
            WHERE (i.CountedQuantity<>d.CountedQuantity OR i.DefectiveQuantity<>d.DefectiveQuantity OR ISNULL(i.DefectiveNotes,N'')<>ISNULL(d.DefectiveNotes,N'')
              OR ISNULL(i.LotNumber,N'')<>ISNULL(d.LotNumber,N'') OR ISNULL(i.SerialNumber,N'')<>ISNULL(d.SerialNumber,N'') OR ISNULL(i.Notes,N'')<>ISNULL(d.Notes,N''))
              AND (p.ObservationSubmittedAtUtc IS NOT NULL OR d.SupersededByPhysicalCountId IS NOT NULL))
            THROW 51989,'INV_COUNT_OBSERVATION_IMMUTABLE: submitted or selected original observations cannot be overwritten.',1;
          IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id
            LEFT JOIN dbo.PhysicalCounts child ON child.Id=i.SupersededByPhysicalCountId AND child.ParentPhysicalCountId=i.PhysicalCountId AND child.TenantId=i.TenantId
            WHERE i.SupersededByPhysicalCountId IS NOT NULL AND d.SupersededByPhysicalCountId IS NULL AND (child.Id IS NULL OR i.RequiresRecount=0 OR LEN(LTRIM(RTRIM(ISNULL(i.RecountReason,N''))))=0))
            THROW 51989,'INV_RECOUNT_SELECTION_INVALID: selected observations require a same-tenant child and reason.',1;
          IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.Id=d.Id WHERE
            ISNULL(i.RootPhysicalCountItemId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.RootPhysicalCountItemId,'00000000-0000-0000-0000-000000000000')
            OR ISNULL(i.PredecessorPhysicalCountItemId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.PredecessorPhysicalCountItemId,'00000000-0000-0000-0000-000000000000')
            OR (d.SupersededByPhysicalCountId IS NOT NULL AND (ISNULL(i.SupersededByPhysicalCountId,'00000000-0000-0000-0000-000000000000')<>d.SupersededByPhysicalCountId OR i.RequiresRecount=0 OR ISNULL(i.RecountReason,N'')<>ISNULL(d.RecountReason,N''))))
            THROW 51989,'INV_RECOUNT_LINEAGE_IMMUTABLE: investigation lineage must remain retained.',1;
        END
        """;

    public const string ClaimGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_PhysicalCountAdjustmentClaims_Immutable ON dbo.PhysicalCountAdjustmentClaims AFTER INSERT,UPDATE,DELETE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS(SELECT 1 FROM deleted) THROW 51990,'INV_COUNT_CLAIM_IMMUTABLE: root-line resolution history cannot be changed.',1;
          IF EXISTS(SELECT 1 FROM inserted i
            LEFT JOIN dbo.PhysicalCounts p ON p.Id=i.PhysicalCountId AND p.TenantId=i.TenantId AND p.IsDeleted=0
            LEFT JOIN dbo.PhysicalCountItems line ON line.Id=i.PhysicalCountItemId AND line.PhysicalCountId=p.Id AND line.TenantId=i.TenantId AND line.IsDeleted=0
            LEFT JOIN dbo.PhysicalCountItems root ON root.Id=i.RootPhysicalCountItemId AND root.TenantId=i.TenantId AND root.IsDeleted=0
            LEFT JOIN dbo.StockAdjustmentItems a ON a.Id=i.StockAdjustmentItemId AND a.AdjustmentId=i.StockAdjustmentId AND a.TenantId=i.TenantId AND a.IsDeleted=0
            WHERE p.Id IS NULL OR line.Id IS NULL OR root.Id IS NULL OR p.Status<>N'ReadyToPost' OR i.IsDeleted=1
              OR line.RequiresRecount=1 OR line.SupersededByPhysicalCountId IS NOT NULL OR line.IsCounted=0
              OR i.RootPhysicalCountItemId<>ISNULL(line.RootPhysicalCountItemId,line.Id)
              OR i.SystemQuantity<>root.SystemQuantity OR i.SystemQuantity<>line.SystemQuantity OR i.CountedQuantity<>line.CountedQuantity
              OR i.VarianceQuantity<>line.VarianceQuantity OR i.VarianceQuantity<>i.CountedQuantity-i.SystemQuantity
              OR (i.VarianceQuantity=0 AND (i.StockAdjustmentId IS NOT NULL OR i.StockAdjustmentItemId IS NOT NULL))
              OR (i.VarianceQuantity<>0 AND (a.Id IS NULL OR i.StockAdjustmentId<>p.StockAdjustmentId OR a.InventoryItemId<>line.InventoryItemId OR a.AdjustmentQuantity<>line.VarianceQuantity OR a.UnitCost<>line.UnitCost)))
            THROW 51990,'INV_COUNT_CLAIM_INVALID: each resolved root line must match its final observation and governed adjustment.',1;
        END
        """;

    public const string CountGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_PhysicalCounts_RecountLineage ON dbo.PhysicalCounts AFTER INSERT,UPDATE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN dbo.PhysicalCounts parent ON parent.Id=i.ParentPhysicalCountId AND parent.TenantId=i.TenantId
            LEFT JOIN dbo.PhysicalCounts root ON root.Id=i.RootPhysicalCountId AND root.TenantId=i.TenantId
            WHERE i.RootPhysicalCountId IS NOT NULL AND (parent.Id IS NULL OR root.Id IS NULL OR root.RootPhysicalCountId IS NOT NULL
              OR i.RootPhysicalCountId<>ISNULL(parent.RootPhysicalCountId,parent.Id) OR i.WarehouseId<>parent.WarehouseId OR i.RecountAttempt<1
              OR i.FreezeInventory=0 OR i.FreezeStartedAtUtc IS NULL OR i.Status=N'Cancelled'))
            THROW 51991,'INV_RECOUNT_COUNT_LINEAGE: recounts retain the original scope and unresolved stock freeze.',1;
          IF EXISTS(SELECT 1 FROM inserted i WHERE (i.RootPhysicalCountId IS NULL AND (i.ParentPhysicalCountId IS NOT NULL OR i.RecountAttempt<>0))
            OR (i.RootPhysicalCountId IS NOT NULL AND (i.ParentPhysicalCountId IS NULL OR i.RecountRequestKey IS NULL OR i.RecountRequestHash IS NULL)))
            THROW 51991,'INV_RECOUNT_ROOT_INVALID: sheet root, parent and replay identity must agree.',1;
          IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id
            JOIN dbo.PhysicalCounts family ON family.TenantId=i.TenantId AND ISNULL(family.RootPhysicalCountId,family.Id)=ISNULL(i.RootPhysicalCountId,i.Id)
            WHERE ((i.StoresApprovedById IS NOT NULL AND ISNULL(d.StoresApprovedById,'00000000-0000-0000-0000-000000000000')<>i.StoresApprovedById
                      AND (family.InitiatedById=i.StoresApprovedById OR family.CountedById=i.StoresApprovedById OR EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=family.Id AND c.TenantId=i.TenantId AND c.UserId=i.StoresApprovedById)))
               OR (i.FinanceApprovedById IS NOT NULL AND ISNULL(d.FinanceApprovedById,'00000000-0000-0000-0000-000000000000')<>i.FinanceApprovedById
                      AND (family.InitiatedById=i.FinanceApprovedById OR family.CountedById=i.FinanceApprovedById OR EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=family.Id AND c.TenantId=i.TenantId AND c.UserId=i.FinanceApprovedById)))
               OR (i.AuditAttestedById IS NOT NULL AND ISNULL(d.AuditAttestedById,'00000000-0000-0000-0000-000000000000')<>i.AuditAttestedById
                      AND (family.InitiatedById=i.AuditAttestedById OR family.CountedById=i.AuditAttestedById OR EXISTS(SELECT 1 FROM dbo.PhysicalCountCounters c WHERE c.PhysicalCountId=family.Id AND c.TenantId=i.TenantId AND c.UserId=i.AuditAttestedById)))))
            THROW 51991,'INV_RECOUNT_REVIEWER_INDEPENDENCE: all original and recount participants remain excluded from approval.',1;
          IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id WHERE
            ISNULL(i.RootPhysicalCountId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.RootPhysicalCountId,'00000000-0000-0000-0000-000000000000')
            OR ISNULL(i.ParentPhysicalCountId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.ParentPhysicalCountId,'00000000-0000-0000-0000-000000000000')
            OR i.RecountAttempt<>d.RecountAttempt OR (d.ObservationSubmittedAtUtc IS NOT NULL AND (i.ObservationSubmittedAtUtc IS NULL OR i.ObservationSubmittedAtUtc<>d.ObservationSubmittedAtUtc)))
            THROW 51991,'INV_RECOUNT_COUNT_IMMUTABLE: original observation and sheet lineage cannot change.',1;
          IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id JOIN dbo.PhysicalCountItems line ON line.PhysicalCountId=i.Id AND line.TenantId=i.TenantId AND line.IsDeleted=0
            WHERE i.Status=N'Posted' AND d.Status<>N'Posted' AND line.RequiresRecount=0 AND line.SupersededByPhysicalCountId IS NULL
              AND NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountAdjustmentClaims c WHERE c.TenantId=i.TenantId AND c.PhysicalCountItemId=line.Id AND c.RootPhysicalCountItemId=ISNULL(line.RootPhysicalCountItemId,line.Id)))
            THROW 51991,'INV_COUNT_POST_CLAIM_REQUIRED: every resolved line requires a unique retained resolution claim.',1;
        END
        """;

    public const string AdjustmentGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_StockAdjustments_CountResolutionClaims ON dbo.StockAdjustments AFTER UPDATE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id JOIN dbo.PhysicalCounts p ON p.StockAdjustmentId=i.Id AND p.TenantId=i.TenantId
            JOIN dbo.StockAdjustmentItems line ON line.AdjustmentId=i.Id AND line.TenantId=i.TenantId AND line.IsDeleted=0
            WHERE i.Status=N'Posted' AND d.Status<>N'Posted' AND NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountAdjustmentClaims c WHERE c.TenantId=i.TenantId AND c.StockAdjustmentItemId=line.Id AND c.PhysicalCountId=p.Id))
            THROW 51992,'INV_COUNT_ADJUSTMENT_CLAIM_REQUIRED: post the linked count through its governed resolution owner.',1;
        END
        """;
}
