using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260911170000_AllowDraftPhysicalCountLineRemoval")]
public sealed class AllowDraftPhysicalCountLineRemoval : Migration
{
    // Keep every existing scope, valuation, first-count and recount guard. Only
    // the existing repository's active -> soft-deleted transition in Draft is allowed.
    public const string UpgradeSql = """
        DECLARE @triggerId int = OBJECT_ID(N'dbo.TR_PhysicalCountItems_ControlledMutation', N'TR');
        DECLARE @definition nvarchar(max) = REPLACE(OBJECT_DEFINITION(@triggerId), NCHAR(13), N'');
        DECLARE @old nvarchar(max) = N'OR i.SystemQuantity <> d.SystemQuantity OR i.UnitCost <> d.UnitCost OR i.IsDeleted <> d.IsDeleted)';
        DECLARE @new nvarchar(max) = N'OR i.SystemQuantity <> d.SystemQuantity OR i.UnitCost <> d.UnitCost
                   OR (i.IsDeleted <> d.IsDeleted AND NOT (
                       d.IsDeleted = 0 AND i.IsDeleted = 1
                       AND EXISTS (SELECT 1 FROM dbo.PhysicalCounts draft
                                   WHERE draft.Id = d.PhysicalCountId AND draft.TenantId = d.TenantId
                                     AND draft.Status = N''Draft'' AND draft.IsDeleted = 0))))';
        SET @new = REPLACE(@new, NCHAR(13), N'');
        IF @definition IS NULL OR EXISTS (SELECT 1 FROM sys.triggers WHERE object_id = @triggerId AND is_disabled = 1)
            THROW 51927, 'INV_COUNT_DRAFT_REMOVAL_MIGRATION: the enabled count-line guard must exist.', 1;
        IF CHARINDEX(@new, @definition) = 0
        BEGIN
            IF CHARINDEX(@old, @definition) = 0
                THROW 51927, 'INV_COUNT_DRAFT_REMOVAL_MIGRATION: unexpected count-line guard; review before applying.', 1;
            SET @definition = REPLACE(@definition, @old, @new);
            SET @definition = STUFF(@definition, 1, CHARINDEX(N'TRIGGER', UPPER(@definition)) - 1, N'CREATE OR ALTER ');
            EXEC sys.sp_executesql @definition;
        END;
        """;

    public const string DowngradeSql = """
        DECLARE @triggerId int = OBJECT_ID(N'dbo.TR_PhysicalCountItems_ControlledMutation', N'TR');
        DECLARE @definition nvarchar(max) = REPLACE(OBJECT_DEFINITION(@triggerId), NCHAR(13), N'');
        DECLARE @old nvarchar(max) = N'OR i.SystemQuantity <> d.SystemQuantity OR i.UnitCost <> d.UnitCost OR i.IsDeleted <> d.IsDeleted)';
        DECLARE @new nvarchar(max) = N'OR i.SystemQuantity <> d.SystemQuantity OR i.UnitCost <> d.UnitCost
                   OR (i.IsDeleted <> d.IsDeleted AND NOT (
                       d.IsDeleted = 0 AND i.IsDeleted = 1
                       AND EXISTS (SELECT 1 FROM dbo.PhysicalCounts draft
                                   WHERE draft.Id = d.PhysicalCountId AND draft.TenantId = d.TenantId
                                     AND draft.Status = N''Draft'' AND draft.IsDeleted = 0))))';
        SET @new = REPLACE(@new, NCHAR(13), N'');
        IF @definition IS NULL
            THROW 51927, 'INV_COUNT_DRAFT_REMOVAL_MIGRATION: count-line guard is missing.', 1;
        IF CHARINDEX(@old, @definition) = 0
        BEGIN
            IF CHARINDEX(@new, @definition) = 0
                THROW 51927, 'INV_COUNT_DRAFT_REMOVAL_MIGRATION: unexpected count-line guard; review before reverting.', 1;
            SET @definition = REPLACE(@definition, @new, @old);
            SET @definition = STUFF(@definition, 1, CHARINDEX(N'TRIGGER', UPPER(@definition)) - 1, N'CREATE OR ALTER ');
            EXEC sys.sp_executesql @definition;
        END;
        """;

    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(UpgradeSql);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(DowngradeSql);
}
