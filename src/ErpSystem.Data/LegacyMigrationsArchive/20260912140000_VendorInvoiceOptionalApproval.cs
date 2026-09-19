using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912140000_VendorInvoiceOptionalApproval")]
public sealed class VendorInvoiceOptionalApproval : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("ApprovalRequired", "VendorInvoice", "bit", nullable: false, defaultValue: true);
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_VendorInvoices_OptionalApproval
            ON dbo.VendorInvoice AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                    WHERE (d.Id IS NULL AND i.ApprovalRequired=0)
                       OR (d.Id IS NOT NULL AND i.ApprovalRequired<>d.ApprovalRequired AND NOT (
                            d.ApprovalRequired=1 AND i.ApprovalRequired=0
                            AND d.ApprovalStatus IN (N'Draft',N'PendingApproval')
                            AND d.ApprovedById IS NULL AND d.ApprovedDate IS NULL
                            AND i.ApprovalStatus=N'NotRequired'
                            AND i.SubmittedById IS NOT NULL AND i.SubmittedDate IS NOT NULL
                            AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'VendorInvoice',i.Id)=0)))
                    THROW 51840, 'The invoice approval mode must be determined by its initial submission.', 1;
                IF EXISTS (SELECT 1 FROM inserted WHERE ApprovalRequired=0 AND
                    (ApprovalStatus NOT IN (N'NotRequired',N'Voided') OR ApprovedById IS NOT NULL OR ApprovedDate IS NOT NULL))
                    THROW 51841, 'A directly completed invoice cannot claim a human approval.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM dbo.VendorInvoice WHERE ApprovalRequired=0)
                THROW 51842, 'Directly completed invoices must retain their approval mode; rollback is not safe.', 1;
            DROP TRIGGER IF EXISTS dbo.TR_VendorInvoices_OptionalApproval;
            """);
        migrationBuilder.DropColumn("ApprovalRequired", "VendorInvoice");
    }
}
