using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912231000_PurchaseReturnOptionalApproval")]
public sealed class PurchaseReturnOptionalApproval : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("ApprovalRequired", "PurchaseReturns", "bit", nullable: false, defaultValue: true);
        migrationBuilder.Sql(GuardSql);
    }

    public const string GuardSql = """
        CREATE OR ALTER TRIGGER dbo.TR_PurchaseReturns_OptionalApproval
        ON dbo.PurchaseReturns AFTER INSERT, UPDATE, DELETE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL AND d.ApprovalRequired=0)
                THROW 51991, 'INV_SUPPLIER_RETURN_HISTORY: retain the direct-completion decision and dispatch history.', 1;
            IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                WHERE (d.Id IS NULL AND (i.ApprovalRequired=0 OR i.Status=N'ReadyToDispatch'))
                   OR (d.Id IS NOT NULL AND i.TenantId<>d.TenantId)
                   OR (d.Id IS NOT NULL AND i.ApprovalRequired<>d.ApprovalRequired AND NOT (
                        d.ApprovalRequired=1 AND i.ApprovalRequired=0 AND d.Status=N'Draft' AND i.Status=N'ReadyToDispatch'
                        AND d.ApprovedById IS NULL AND d.ApprovedDate IS NULL
                        AND i.RequestedById IS NOT NULL
                        AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'PurchaseReturn',i.Id)=0))
                   OR (i.ApprovalRequired=0 AND (i.ApprovedById IS NOT NULL OR i.ApprovedDate IS NOT NULL
                        OR i.Status NOT IN(N'ReadyToDispatch',N'Shipped',N'Acknowledged',N'Completed',N'Cancelled')))
                   OR (i.ApprovalRequired=1 AND i.Status=N'ReadyToDispatch')
                   OR (d.ApprovalRequired=0 AND (ISNULL(i.GoodsReceiptNoteId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.GoodsReceiptNoteId,'00000000-0000-0000-0000-000000000000') OR i.SupplierId<>d.SupplierId
                        OR i.WarehouseId<>d.WarehouseId OR ISNULL(i.RequestedById,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.RequestedById,'00000000-0000-0000-0000-000000000000')
                        OR NOT (i.Status=d.Status
                            OR d.Status=N'ReadyToDispatch' AND i.Status IN(N'Shipped',N'Cancelled')
                            OR d.Status=N'Shipped' AND i.Status=N'Acknowledged'))))
                THROW 51992, 'INV_SUPPLIER_RETURN_APPROVAL: capture the central no-workflow decision only on draft submission; preserve real approval history.', 1;
        END;
        """;

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM dbo.PurchaseReturns WHERE ApprovalRequired=0)
                THROW 51993, 'Cannot remove supplier-return policy while direct-completion history exists.', 1;
            DROP TRIGGER IF EXISTS dbo.TR_PurchaseReturns_OptionalApproval;
            """);
        migrationBuilder.DropColumn("ApprovalRequired", "PurchaseReturns");
    }
}
