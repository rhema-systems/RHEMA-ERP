using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912150000_UnitJournalEntryOptionalWorkflow")]
public sealed class UnitJournalEntryOptionalWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "ApprovalRequired", table: "UnitJournalEntries",
            type: "bit", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<Guid>(name: "WorkflowInstanceId", table: "UnitJournalEntries",
            type: "uniqueidentifier", nullable: true);
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_UnitJournalEntries_OptionalApproval
            ON dbo.UnitJournalEntries AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                    WHERE (i.Status=6 AND i.ApprovalRequired<>0)
                       OR (i.ApprovalRequired=0 AND (
                           d.Id IS NULL OR i.Status NOT IN (4,5,6) OR i.WorkflowInstanceId IS NOT NULL
                           OR i.ApprovedBy IS NOT NULL OR i.ApprovedAt IS NOT NULL
                           OR NULLIF(i.ApprovedByName,N'') IS NOT NULL
                           OR ((d.ApprovalRequired<>0 OR d.Status IN (0,3))
                               AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'UnitJournalEntry',i.Id)<>0)))
                       OR (d.Id IS NOT NULL AND d.ApprovalRequired<>i.ApprovalRequired
                           AND NOT (d.Status IN (0,3) AND i.Status=6 AND i.ApprovalRequired=0
                               AND d.ApprovedBy IS NULL AND d.ApprovedAt IS NULL
                               AND NULLIF(d.ApprovedByName,N'') IS NULL)))
                    THROW 51985, 'UNIT_JOURNAL_APPROVAL_STATE_INVALID: direct readiness requires confirmed absent approval and cannot fabricate human approval metadata.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    LEFT JOIN dbo.WorkflowInstances w ON w.Id=i.WorkflowInstanceId AND w.TenantId=i.TenantId
                        AND w.EntityId=i.Id AND w.IsDeleted=0
                    LEFT JOIN dbo.WorkflowEntityTypes e ON e.Id=w.EntityTypeId AND e.TenantId=i.TenantId
                    WHERE i.WorkflowInstanceId IS NOT NULL AND
                        (w.Id IS NULL OR e.Id IS NULL OR
                         (dbo.WorkflowApprovalEntityKey(e.Code)<>N'UNITJOURNALENTRY'
                          AND dbo.WorkflowApprovalEntityKey(e.Name)<>N'UNITJOURNALENTRY')))
                    THROW 51986, 'UNIT_JOURNAL_WORKFLOW_LINEAGE_INVALID: workflow must belong to this entry and tenant.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM dbo.UnitJournalEntries WHERE ApprovalRequired=0 OR WorkflowInstanceId IS NOT NULL OR Status=6)
                THROW 51987, 'Unit journal optional-approval history exists. Roll forward to preserve submission lineage.', 1;
            DROP TRIGGER IF EXISTS dbo.TR_UnitJournalEntries_OptionalApproval;
            """);
        migrationBuilder.DropColumn(name: "WorkflowInstanceId", table: "UnitJournalEntries");
        migrationBuilder.DropColumn(name: "ApprovalRequired", table: "UnitJournalEntries");
    }
}
