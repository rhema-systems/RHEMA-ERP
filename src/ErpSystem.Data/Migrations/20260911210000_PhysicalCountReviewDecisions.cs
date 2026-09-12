using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260911210000_PhysicalCountReviewDecisions")]
public sealed class PhysicalCountReviewDecisions : Migration
{
    public const string UpgradeSql = """
        -- No count, approval, quantity or posting records are rewritten by this migration.
        ALTER TABLE dbo.PhysicalCountActions DROP CONSTRAINT CK_PhysicalCountActions_ActionType;
        ALTER TABLE dbo.PhysicalCountActions ADD CONSTRAINT CK_PhysicalCountActions_ActionType CHECK (ActionType BETWEEN 1 AND 14);

        DECLARE @line nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_PhysicalCountItems_ControlledMutation'));
        DECLARE @life nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_PhysicalCounts_ControlledLifecycle'));
        IF @line IS NULL OR @life IS NULL
            THROW 51928, 'INV_COUNT_REVIEW_MIGRATION: count guards are missing.', 1;
        IF CHARINDEX(N'p.Status <> N''InProgress'' OR i.CountedById <> p.CountedById', @line) = 0
            THROW 51928, 'INV_COUNT_REVIEW_MIGRATION: unexpected count-line guard.', 1;
        SET @line = REPLACE(@line, N'p.Status <> N''InProgress'' OR i.CountedById <> p.CountedById',
            N'p.Status NOT IN (N''InProgress'',N''UnderReview'') OR i.CountedById <> p.CountedById');
        -- Keep the first observed count forever; corrections change CountedQuantity only.
        SET @line = REPLACE(@line, N'OR i.SystemQuantity <> d.SystemQuantity OR i.UnitCost <> d.UnitCost',
            N'OR i.SystemQuantity <> d.SystemQuantity OR i.UnitCost <> d.UnitCost
               OR (d.FirstCountQuantity IS NOT NULL AND (i.FirstCountQuantity IS NULL OR i.FirstCountQuantity <> d.FirstCountQuantity))');
        SET @line = STUFF(@line, 1, CHARINDEX(N'TRIGGER', UPPER(@line)) - 1, N'CREATE OR ALTER ');
        EXEC sys.sp_executesql @line;

        IF CHARINDEX(N'N''Draft'',N''InProgress'',N''RecountRequired'',N''PendingStoresApproval''', @life) = 0
            THROW 51928, 'INV_COUNT_REVIEW_MIGRATION: unexpected lifecycle guard.', 1;
        SET @life = REPLACE(@life, N'N''Draft'',N''InProgress'',N''RecountRequired'',N''PendingStoresApproval''',
            N'N''Draft'',N''InProgress'',N''UnderReview'',N''UnderInvestigation'',N''RecountRequired'',N''PendingStoresApproval''');
        SET @life = REPLACE(@life, N'(d.Status = N''InProgress'' AND i.Status IN (N''RecountRequired'',N''PendingStoresApproval'')',
            N'(d.Status = N''UnderReview'' AND i.Status = N''PendingStoresApproval''');
        SET @life = REPLACE(@life, N'AND i.Status=N''RecountRequired'' AND EXISTS',
            N'AND i.Status=N''UnderInvestigation'' AND EXISTS');
        SET @life = REPLACE(@life, N'(d.Status IN (N''Draft'',N''InProgress'',N''RecountRequired'')',
            N'(d.Status IN (N''Draft'',N''InProgress'',N''UnderReview'',N''UnderInvestigation'',N''RecountRequired'')');
        SET @life = REPLACE(@life, N'WHERE i.Status <> d.Status AND NOT (',
            N'WHERE i.Status <> d.Status AND NOT (
                (d.Status IN (N''InProgress'',N''RecountRequired'',N''UnderInvestigation'') AND i.Status=N''UnderReview''
                 AND i.StockAdjustmentId IS NULL AND EXISTS (SELECT 1 FROM PhysicalCountActions a
                    WHERE a.PhysicalCountId=i.Id AND a.TenantId=i.TenantId AND a.ActionType=14 AND a.ActorUserId=i.CountedById
                    AND a.Sequence=(SELECT MAX(lastAction.Sequence) FROM PhysicalCountActions lastAction
                        WHERE lastAction.PhysicalCountId=i.Id AND lastAction.TenantId=i.TenantId))) OR ');
        SET @life = STUFF(@life, 1, CHARINDEX(N'TRIGGER', UPPER(@life)) - 1, N'CREATE OR ALTER ');
        EXEC sys.sp_executesql @life;

        DECLARE @actions nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_PhysicalCountActions_AppendOnly'));
        IF @actions IS NULL OR CHARINDEX(N'i.ActionType NOT BETWEEN 1 AND 13', @actions) = 0
            THROW 51928, 'INV_COUNT_REVIEW_MIGRATION: unexpected audit guard.', 1;
        SET @actions = REPLACE(@actions, N'i.ActionType NOT BETWEEN 1 AND 13', N'i.ActionType NOT BETWEEN 1 AND 14');
        SET @actions = STUFF(@actions, 1, CHARINDEX(N'TRIGGER', UPPER(@actions)) - 1, N'CREATE OR ALTER ');
        EXEC sys.sp_executesql @actions;

        -- The same freeze remains active through review and investigation.
        DECLARE @freezeName nvarchar(128), @freeze nvarchar(max);
        IF (SELECT COUNT(*) FROM sys.triggers WHERE parent_class=1 AND name IN
            (N'TR_WarehouseQuantities_PhysicalCountFreeze',N'TR_InventoryItems_PhysicalCountFreeze',N'TR_StockMovements_PhysicalCountFreeze')) <> 3
            THROW 51928, 'INV_COUNT_REVIEW_MIGRATION: freeze guards are missing.', 1;
        DECLARE freezeGuards CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM sys.triggers
            WHERE name IN (N'TR_WarehouseQuantities_PhysicalCountFreeze',N'TR_InventoryItems_PhysicalCountFreeze',N'TR_StockMovements_PhysicalCountFreeze');
        OPEN freezeGuards;
        FETCH NEXT FROM freezeGuards INTO @freezeName;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @freeze = OBJECT_DEFINITION(OBJECT_ID(N'dbo.'+@freezeName));
            IF CHARINDEX(N'N''InProgress'',N''RecountRequired''', @freeze) = 0
                THROW 51928, 'INV_COUNT_REVIEW_MIGRATION: unexpected freeze guard.', 1;
            SET @freeze = REPLACE(@freeze, N'N''InProgress'',N''RecountRequired''',
                N'N''InProgress'',N''UnderReview'',N''UnderInvestigation'',N''RecountRequired''');
            SET @freeze = STUFF(@freeze, 1, CHARINDEX(N'TRIGGER', UPPER(@freeze)) - 1, N'CREATE OR ALTER ');
            EXEC sys.sp_executesql @freeze;
            FETCH NEXT FROM freezeGuards INTO @freezeName;
        END;
        CLOSE freezeGuards;
        DEALLOCATE freezeGuards;
        """;

    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(UpgradeSql);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        THROW 51928, 'Review decisions retain new audit events. Restore the pre-change database backup and matching application to roll back safely.', 1;
        """);
}
