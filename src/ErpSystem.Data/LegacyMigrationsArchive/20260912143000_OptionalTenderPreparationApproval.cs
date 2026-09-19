using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912143000_OptionalTenderPreparationApproval")]
public sealed class OptionalTenderPreparationApproval : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("ApprovalRequired", "Tenders", type: "bit", nullable: false, defaultValue: true);
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_Tenders_ApprovalPolicy ON dbo.Tenders AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                    WHERE (d.Id IS NULL AND i.ApprovalRequired=0)
                       OR (d.Id IS NOT NULL AND i.ApprovalRequired<>d.ApprovalRequired
                           AND NOT (d.ApprovalRequired=1 AND i.ApprovalRequired=0
                               AND d.Status=N'Draft' AND i.Status=N'Approved')))
                    THROW 52535, 'TENDER_APPROVAL_POLICY_IMMUTABLE: the submission decision cannot be rewritten.', 1;
                IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id
                    WHERE d.ApprovalRequired=1 AND i.ApprovalRequired=0
                      AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'Tender',i.Id)=1)
                    THROW 52536, 'TENDER_APPROVAL_STILL_REQUIRED: the active or in-flight approval process must be completed.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM dbo.Tenders WHERE ApprovalRequired=0) THROW 52537, 'Direct-finalized tenders prevent this approval-policy downgrade.', 1;");
        migrationBuilder.Sql("DROP TRIGGER dbo.TR_Tenders_ApprovalPolicy;");
        migrationBuilder.DropColumn("ApprovalRequired", "Tenders");
    }
}
