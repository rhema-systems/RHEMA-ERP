using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912141000_JournalOptionalApprovalSubmission")]
public sealed class JournalOptionalApprovalSubmission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE OR ALTER TRIGGER dbo.TR_JournalEntries_OptionalApprovalSubmission
        ON dbo.JournalEntries AFTER UPDATE AS
        BEGIN
            SET NOCOUNT ON;
            -- Existing journals defaulted RequiresApproval to false, including
            -- human-approved journals. Guard only the new direct manual transition;
            -- do not reinterpret that historical flag or generated subledger entries.
            IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id
                WHERE d.PostingStatus=N'Draft' AND i.PostingStatus=N'Approved'
                  AND i.ApprovalStatus=N'Not Required'
                  AND COALESCE(i.SourceModule,N'GL')=N'GL'
                  AND COALESCE(i.SourceDocumentType,N'ManualJournalEntry')=N'ManualJournalEntry'
                  AND NOT EXISTS (SELECT 1 FROM dbo.JournalBatchItems b
                      WHERE b.JournalEntryId=i.Id AND b.TenantId=i.TenantId AND b.IsDeleted=0)
                  AND (i.TenantId<>d.TenantId OR i.RequiresApproval<>0
                       OR i.ApprovedByUserId IS NOT NULL OR i.ApprovedDate IS NOT NULL
                       OR d.ApprovedByUserId IS NOT NULL OR d.ApprovedDate IS NOT NULL
                       OR NULLIF(LTRIM(RTRIM(i.ApprovalWorkflowId)),N'') IS NOT NULL
                       OR NULLIF(LTRIM(RTRIM(d.ApprovalWorkflowId)),N'') IS NOT NULL
                       OR dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'JournalEntry',i.Id)=1))
                THROW 51843, 'The manual journal cannot complete directly while workflow approval is required or human approval is recorded.', 1;
        END
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.TR_JournalEntries_OptionalApprovalSubmission;");
}
