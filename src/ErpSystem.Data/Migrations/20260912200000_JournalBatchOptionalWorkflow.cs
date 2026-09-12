using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912200000_JournalBatchOptionalWorkflow")]
public sealed class JournalBatchOptionalWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "ApprovalRequired", table: "JournalBatches", type: "bit", nullable: false, defaultValue: true);
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_JournalBatches_OptionalApproval ON dbo.JournalBatches AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                    WHERE (i.ApprovalStatus=N'ReadyToPost' AND i.ApprovalRequired<>0)
                       OR (i.ApprovalRequired=0 AND (d.Id IS NULL OR i.ApprovalStatus<>N'ReadyToPost'
                           OR i.WorkflowInstanceId IS NOT NULL OR i.ApprovedByUserId IS NOT NULL
                           OR i.ApprovedAt IS NOT NULL OR i.ReviewCompletedAt IS NOT NULL
                           OR i.SubmittedAt IS NULL OR i.SubmittedByUserId IS NULL OR i.ContentFingerprint IS NULL))
                       OR (d.Id IS NOT NULL AND d.ApprovalRequired<>i.ApprovalRequired AND NOT
                           (d.ApprovalRequired=1 AND d.ApprovalStatus=N'Draft' AND i.ApprovalRequired=0
                            AND i.ApprovalStatus=N'ReadyToPost' AND i.PostingStatus=N'Ready' AND d.PostingStatus=N'NotReady'
                            AND i.TenantId=d.TenantId AND d.WorkflowInstanceId IS NULL
                            AND d.ApprovedByUserId IS NULL AND d.ApprovedAt IS NULL AND d.ReviewCompletedAt IS NULL
                            AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'JournalBatch',i.Id)=0)))
                    THROW 51995, 'JOURNAL_BATCH_APPROVAL_STATE_INVALID: only a draft without an active approval process can become directly posting-ready.', 1;
                IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                    LEFT JOIN dbo.WorkflowInstances w ON w.Id=i.WorkflowInstanceId AND w.TenantId=i.TenantId AND w.EntityId=i.Id AND w.IsDeleted=0
                    LEFT JOIN dbo.WorkflowEntityTypes e ON e.Id=w.EntityTypeId AND e.TenantId=i.TenantId
                    WHERE i.WorkflowInstanceId IS NOT NULL AND (d.Id IS NULL OR d.WorkflowInstanceId IS NULL OR d.WorkflowInstanceId<>i.WorkflowInstanceId)
                      AND (w.Id IS NULL OR e.Id IS NULL OR
                           (dbo.WorkflowApprovalEntityKey(e.Code)<>N'JOURNALBATCH' AND dbo.WorkflowApprovalEntityKey(e.Name)<>N'JOURNALBATCH')))
                    THROW 51996, 'JOURNAL_BATCH_WORKFLOW_LINEAGE_INVALID: workflow must belong to this batch and tenant.', 1;
                IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id
                    LEFT JOIN dbo.WorkflowInstances w ON w.Id=i.WorkflowInstanceId AND w.TenantId=i.TenantId AND w.EntityId=i.Id AND w.IsDeleted=0
                    WHERE d.ApprovalStatus=N'PendingApproval' AND i.ApprovalStatus IN (N'Approved',N'PartiallyApproved')
                      AND (i.ApprovalRequired<>1 OR w.Id IS NULL OR w.Status<>2 OR w.CompletedDate IS NULL))
                    THROW 51997, 'JOURNAL_BATCH_APPROVAL_INCOMPLETE: retained workflow approval must complete before posting readiness.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_JournalBatchItems_OptionalApproval ON dbo.JournalBatchItems AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id
                    WHERE d.ReviewStatus=N'NotRequired' AND i.ReviewStatus<>N'NotRequired')
                    THROW 51998, 'JOURNAL_BATCH_ITEM_APPROVAL_INVALID: the saved direct submission mode is immutable.', 1;
                IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                    LEFT JOIN dbo.JournalBatches b ON b.Id=i.JournalBatchId AND b.TenantId=i.TenantId AND b.IsDeleted=0
                    WHERE i.ReviewStatus=N'NotRequired' AND (d.Id IS NULL OR b.Id IS NULL OR b.ApprovalRequired<>0
                      OR b.ApprovalStatus<>N'ReadyToPost' OR i.FinalReviewedByUserId IS NOT NULL OR i.FinalReviewedAt IS NOT NULL
                      OR i.FinalRejectionReason IS NOT NULL OR d.ReviewStatus NOT IN (N'Pending',N'NotRequired')
                      OR d.JournalBatchId<>i.JournalBatchId OR d.TenantId<>i.TenantId OR d.JournalEntryId<>i.JournalEntryId
                      OR EXISTS (SELECT 1 FROM dbo.JournalBatchItemReviews r WHERE r.JournalBatchItemId=i.Id)))
                    THROW 51998, 'JOURNAL_BATCH_ITEM_APPROVAL_INVALID: direct-ready entries cannot replace retained human review.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_JournalEntries_BatchOptionalApproval ON dbo.JournalEntries AFTER UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id
                    JOIN dbo.JournalBatchItems m ON m.JournalEntryId=i.Id AND m.TenantId=i.TenantId AND m.IsDeleted=0
                    JOIN dbo.JournalBatches b ON b.Id=m.JournalBatchId AND b.TenantId=m.TenantId AND b.IsDeleted=0
                    WHERE i.ApprovalStatus=N'Not Required' AND
                        (b.ApprovalRequired<>0 OR b.ApprovalStatus<>N'ReadyToPost' OR i.RequiresApproval<>0
                         OR i.TenantId<>d.TenantId OR i.ApprovedByUserId IS NOT NULL OR i.ApprovedDate IS NOT NULL
                         OR d.ApprovedByUserId IS NOT NULL OR d.ApprovedDate IS NOT NULL
                         OR NULLIF(LTRIM(RTRIM(i.ApprovalWorkflowId)),N'') IS NOT NULL
                         OR NULLIF(LTRIM(RTRIM(d.ApprovalWorkflowId)),N'') IS NOT NULL
                         OR d.PostingStatus NOT IN (N'Draft',N'Approved',N'Posted')))
                    THROW 51999, 'JOURNAL_BATCH_ENTRY_APPROVAL_INVALID: a direct journal requires its posting-ready batch without human approval metadata.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM dbo.JournalBatches WHERE ApprovalRequired=0 OR ApprovalStatus=N'ReadyToPost')
                THROW 51990, 'Journal batch optional approval history exists; roll forward to preserve its lineage.', 1;
            DROP TRIGGER IF EXISTS dbo.TR_JournalEntries_BatchOptionalApproval;
            DROP TRIGGER IF EXISTS dbo.TR_JournalBatchItems_OptionalApproval;
            DROP TRIGGER IF EXISTS dbo.TR_JournalBatches_OptionalApproval;
            """);
        migrationBuilder.DropColumn(name: "ApprovalRequired", table: "JournalBatches");
    }
}
