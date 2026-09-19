using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912160000_FinancePurchasingOptionalApproval")]
public sealed class FinancePurchasingOptionalApproval : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "ApprovalRequired", table: "FinancePurchaseOrders", type: "bit", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<bool>(name: "ApprovalRequired", table: "FinancePurchaseOrderReceipts", type: "bit", nullable: false, defaultValue: true);
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_FinancePurchaseOrders_ApprovalPolicy
            ON dbo.FinancePurchaseOrders AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                    WHERE (i.ApprovalRequired=0 AND (d.Id IS NULL OR i.Status IN (1,9,10)))
                        OR (d.Id IS NOT NULL AND d.ApprovalRequired<>i.ApprovalRequired AND NOT (
                            d.Status IN (1,10) AND i.Status=2 AND i.ApprovalRequired=0
                            AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'FinancePurchaseOrder',i.Id)=0)))
                    THROW 51988, 'FINANCE_PO_APPROVAL_STATE_INVALID: direct completion is allowed only at a validated submission without active approval.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_FinancePurchaseOrderReceipts_ApprovalPolicy
            ON dbo.FinancePurchaseOrderReceipts AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                    WHERE (i.ApprovalRequired=0 AND (
                        d.Id IS NULL OR i.Status<>3 OR i.WorkflowInstanceId IS NOT NULL
                        OR i.ApprovedAt IS NOT NULL OR i.ApprovedById IS NOT NULL
                        OR i.SubmittedAt IS NULL OR i.SubmittedById IS NULL))
                        OR (d.Id IS NOT NULL AND d.ApprovalRequired<>i.ApprovalRequired AND NOT (
                            d.Status=1 AND i.Status=3 AND i.ApprovalRequired=0
                            AND d.ApprovedAt IS NULL AND d.ApprovedById IS NULL AND d.WorkflowInstanceId IS NULL
                            AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'FinancePurchaseOrderReceipt',i.Id)=0)))
                    THROW 51989, 'FINANCE_GRV_APPROVAL_STATE_INVALID: direct completion cannot replace an existing approval or fabricate human approval metadata.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS(SELECT 1 FROM dbo.FinancePurchaseOrders WHERE ApprovalRequired=0)
                OR EXISTS(SELECT 1 FROM dbo.FinancePurchaseOrderReceipts WHERE ApprovalRequired=0)
                THROW 51990, 'Direct finance purchasing history exists. Roll forward to preserve its decision.', 1;
            DROP TRIGGER IF EXISTS dbo.TR_FinancePurchaseOrderReceipts_ApprovalPolicy;
            DROP TRIGGER IF EXISTS dbo.TR_FinancePurchaseOrders_ApprovalPolicy;
            """);
        migrationBuilder.DropColumn(name: "ApprovalRequired", table: "FinancePurchaseOrderReceipts");
        migrationBuilder.DropColumn(name: "ApprovalRequired", table: "FinancePurchaseOrders");
    }
}
