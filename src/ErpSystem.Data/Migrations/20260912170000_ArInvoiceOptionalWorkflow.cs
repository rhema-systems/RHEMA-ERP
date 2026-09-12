using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912170000_ArInvoiceOptionalWorkflow")]
public sealed class ArInvoiceOptionalWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "ApprovalRequired", table: "Invoices", type: "bit", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<Guid>(name: "WorkflowInstanceId", table: "Invoices", type: "uniqueidentifier", nullable: true);
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_Invoices_OptionalApproval ON dbo.Invoices AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                    WHERE (i.Status=10 AND (i.ApprovalRequired<>0 OR i.JournalEntryId IS NOT NULL))
                       OR (i.ApprovalRequired=0 AND (d.Id IS NULL OR i.Status NOT IN (2,3,4,5,6,10) OR i.WorkflowInstanceId IS NOT NULL))
                       OR (d.Id IS NOT NULL AND d.ApprovalRequired<>i.ApprovalRequired AND NOT
                           (d.ApprovalRequired=1 AND d.Status IN (1,9) AND i.Status=10 AND i.ApprovalRequired=0
                            AND d.JournalEntryId IS NULL AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'Invoice',i.Id)=0)))
                    THROW 51991, 'AR_INVOICE_APPROVAL_STATE_INVALID: direct readiness requires an absent approval process and cannot replace pending or posted history.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    LEFT JOIN dbo.WorkflowInstances w ON w.Id=i.WorkflowInstanceId AND w.TenantId=i.TenantId AND w.EntityId=i.Id AND w.IsDeleted=0
                    LEFT JOIN dbo.WorkflowEntityTypes e ON e.Id=w.EntityTypeId AND e.TenantId=i.TenantId
                    WHERE i.WorkflowInstanceId IS NOT NULL AND (w.Id IS NULL OR e.Id IS NULL OR
                        (dbo.WorkflowApprovalEntityKey(e.Code) NOT IN (N'INVOICE',N'CUSTOMERINVOICE') AND
                         dbo.WorkflowApprovalEntityKey(e.Name) NOT IN (N'INVOICE',N'CUSTOMERINVOICE'))))
                    THROW 51992, 'AR_INVOICE_WORKFLOW_LINEAGE_INVALID: retained workflow must belong to this invoice and tenant.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id
                    LEFT JOIN dbo.WorkflowInstances w ON w.Id=i.WorkflowInstanceId AND w.TenantId=i.TenantId AND w.EntityId=i.Id AND w.IsDeleted=0
                    WHERE d.Status IN (7,8) AND i.Status=2 AND
                        (i.ApprovalRequired<>1 OR w.Id IS NULL OR w.Status<>2 OR w.CompletedDate IS NULL))
                    THROW 51993, 'AR_INVOICE_APPROVAL_INCOMPLETE: pending invoices require a completed retained workflow before release.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM dbo.Invoices WHERE ApprovalRequired=0 OR WorkflowInstanceId IS NOT NULL OR Status=10)
                THROW 51994, 'AR invoice optional approval history exists; roll forward to retain submission lineage.', 1;
            DROP TRIGGER IF EXISTS dbo.TR_Invoices_OptionalApproval;
            """);
        migrationBuilder.DropColumn(name: "WorkflowInstanceId", table: "Invoices");
        migrationBuilder.DropColumn(name: "ApprovalRequired", table: "Invoices");
    }
}
