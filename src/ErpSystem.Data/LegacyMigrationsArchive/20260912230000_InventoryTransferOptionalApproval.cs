using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912230000_InventoryTransferOptionalApproval")]
public sealed class InventoryTransferOptionalApproval : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("ApprovalRequired", "InventoryTransfers", "bit", nullable: false, defaultValue: true);
        Patch(migrationBuilder, "SET NOCOUNT ON;", """
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                WHERE (d.Id IS NULL AND i.ApprovalRequired=0)
                   OR (d.Id IS NOT NULL AND d.ApprovalRequired<>i.ApprovalRequired AND NOT (
                       d.ApprovalRequired=1 AND i.ApprovalRequired=0 AND d.Status=1 AND i.Status=3
                       AND d.ApprovedById IS NULL AND d.ApprovalDate IS NULL
                       AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'InventoryTransfer',i.Id)=0))
                   OR (i.ApprovalRequired=0 AND (i.Status NOT IN (3,5,6,7,8)
                       OR i.ApprovedById IS NOT NULL OR i.ApprovalDate IS NOT NULL)))
                THROW 51995, 'INV_TRANSFER_APPROVAL_MODE_INVALID: retain the original approval decision and never fabricate human approval.', 1;
            """);
        Patch(migrationBuilder, "WHERE i.Status <> d.Status AND NOT (", """
            WHERE i.Status <> d.Status AND NOT (
                (d.Status=1 AND i.Status=3 AND i.ApprovalRequired=0 AND EXISTS (
                    SELECT 1 FROM InventoryTransferActions a WHERE a.InventoryTransferId=i.Id
                    AND a.TenantId=i.TenantId AND a.IsDeleted=0 AND a.ActionType=1
                    AND a.ActorUserId=i.RequestedById
                    AND JSON_VALUE(a.SnapshotJson,'$.Metadata.ApprovalRequired')=N'false')) OR
            """);
        Patch(migrationBuilder,
            "WHERE (i.Status >= 3 AND i.Status NOT IN (8,9) AND (i.ApprovedById IS NULL OR i.ApprovedById = i.RequestedById))",
            "WHERE (i.ApprovalRequired=1 AND i.Status >= 3 AND i.Status NOT IN (8,9) AND (i.ApprovedById IS NULL OR i.ApprovedById = i.RequestedById))");
        // Preserve active approval separation; direct execution still needs its scoped actor and action.
        Patch(migrationBuilder,
            "i.ShippedById NOT IN (i.RequestedById, i.ApprovedById)",
            "(i.ApprovalRequired=0 OR (i.ShippedById <> i.RequestedById AND i.ShippedById <> i.ApprovedById))");
        Patch(migrationBuilder,
            "i.ReceivedById NOT IN (i.RequestedById, i.ApprovedById, i.ShippedById)",
            "(i.ApprovalRequired=0 OR (i.ReceivedById <> i.RequestedById AND i.ReceivedById <> i.ShippedById AND i.ReceivedById <> i.ApprovedById))");
        Patch(migrationBuilder,
            "i.ClosedById NOT IN (i.RequestedById, i.ApprovedById, i.ShippedById, i.ReceivedById)",
            "(i.ApprovalRequired=0 OR (i.ClosedById <> i.RequestedById AND i.ClosedById <> i.ShippedById AND i.ClosedById <> i.ReceivedById AND i.ClosedById <> i.ApprovedById))");
        Patch(migrationBuilder, "i.ShippedById IN (i.RequestedById, i.ApprovedById)",
            "(i.ApprovalRequired=1 AND i.ShippedById IN (i.RequestedById, i.ApprovedById))");
        Patch(migrationBuilder, "i.ReceivedById IN (i.RequestedById, i.ApprovedById, i.ShippedById)",
            "(i.ApprovalRequired=1 AND i.ReceivedById IN (i.RequestedById, i.ApprovedById, i.ShippedById))");
        Patch(migrationBuilder, "i.ClosedById IN (i.RequestedById, i.ApprovedById, i.ShippedById, i.ReceivedById)",
            "(i.ApprovalRequired=1 AND i.ClosedById IN (i.RequestedById, i.ApprovedById, i.ShippedById, i.ReceivedById))");
        Patch(migrationBuilder, "i.ResolvedById IN (t.RequestedById, t.ApprovedById, t.ShippedById, t.ReceivedById)",
            "(t.ApprovalRequired=1 AND i.ResolvedById IN (t.RequestedById, t.ApprovedById, t.ShippedById, t.ReceivedById))",
            "TR_InventoryTransferDiscrepancies_ControlledLifecycle");
    }

    private static void Patch(MigrationBuilder builder, string before, string after, string trigger = "TR_InventoryTransfers_ControlledLifecycle")
    {
        static string Literal(string value) => value.Replace("'", "''", StringComparison.Ordinal);
        builder.Sql($$"""
            DECLARE @definition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.{{trigger}}',N'TR'));
            DECLARE @before nvarchar(max)=N'{{Literal(before)}}';
            IF @definition IS NULL OR (LEN(@definition)-LEN(REPLACE(@definition,@before,N'')))/LEN(@before)<>1
                THROW 51996, 'INV_TRANSFER_OPTIONAL_GUARD_DRIFT: expected exact protected lifecycle definition.', 1;
            SET @definition=REPLACE(@definition,@before,N'{{Literal(after)}}');
            SET @definition=STUFF(@definition,1,CHARINDEX(N'TRIGGER',UPPER(@definition))-1,N'CREATE OR ALTER ');
            EXEC sys.sp_executesql @definition;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        THROW 51997, 'Transfer approval-mode history is retained. Restore a verified pre-change backup and matching application for rollback.', 1;
        """);
}
