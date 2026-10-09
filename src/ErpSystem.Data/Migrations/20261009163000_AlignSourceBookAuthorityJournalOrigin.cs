using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261009163000_AlignSourceBookAuthorityJournalOrigin")]
public sealed class AlignSourceBookAuthorityJournalOrigin : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(EvidenceTriggerSql(useExplicitJournalOrigin: true));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS
            (
                SELECT 1
                FROM [dbo].[FinanceSourceBookAuthorities] a
                JOIN [dbo].[JournalEntries] j ON j.Id=a.OriginalJournalEntryId AND j.TenantId=a.TenantId
                WHERE a.OriginalJournalEntryId IS NOT NULL
                  AND (CASE UPPER(LTRIM(RTRIM(j.SourceModule)))
                          WHEN N'PAYROLL' THEN N'HR' WHEN N'INV' THEN N'INV' WHEN N'INVENTORY' THEN N'INV'
                          WHEN N'PROC' THEN N'PROC' WHEN N'PROCUREMENT' THEN N'PROC' WHEN N'SALES' THEN N'SALES' ELSE N'FIN' END)
                      COLLATE Latin1_General_100_BIN2<>a.OriginModuleCode COLLATE Latin1_General_100_BIN2
            )
                THROW 51011, 'SOURCE_BOOK_AUTHORITY_JOURNAL_ORIGIN_DOWN_BLOCKED: retained posting evidence depends on the explicit journal origin.', 1;
            """);
        migrationBuilder.Sql(EvidenceTriggerSql(useExplicitJournalOrigin: false));
    }

    private static string EvidenceTriggerSql(bool useExplicitJournalOrigin)
    {
        var journalOriginExpression = useExplicitJournalOrigin
            ? """
              COALESCE(NULLIF(UPPER(LTRIM(RTRIM(j.OriginModuleCode))),N''),
                  CASE UPPER(LTRIM(RTRIM(j.SourceModule))) WHEN N'PAYROLL' THEN N'HR' WHEN N'INV' THEN N'INV' WHEN N'INVENTORY' THEN N'INV' WHEN N'PROC' THEN N'PROC' WHEN N'PROCUREMENT' THEN N'PROC' WHEN N'SALES' THEN N'SALES' ELSE N'FIN' END)
              """
            : """
              (CASE UPPER(LTRIM(RTRIM(j.SourceModule))) WHEN N'PAYROLL' THEN N'HR' WHEN N'INV' THEN N'INV' WHEN N'INVENTORY' THEN N'INV' WHEN N'PROC' THEN N'PROC' WHEN N'PROCUREMENT' THEN N'PROC' WHEN N'SALES' THEN N'SALES' ELSE N'FIN' END)
              """;

        return $$"""
            CREATE OR ALTER TRIGGER [dbo].[TR_FinanceSourceBookAuthorities_Evidence]
            ON [dbo].[FinanceSourceBookAuthorities] AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS
                (
                    SELECT 1 FROM inserted i
                    LEFT JOIN [dbo].[WorkflowInstances] w ON w.Id=i.SourceWorkflowInstanceId AND w.TenantId=i.TenantId
                    LEFT JOIN [dbo].[WorkflowEntityTypes] wt ON wt.Id=w.EntityTypeId AND wt.TenantId=w.TenantId
                    OUTER APPLY
                    (
                        SELECT MAX(a.ProcessedDate) AS FinalProcessedDate
                        FROM [dbo].[WorkflowStepInstances] s
                        JOIN [dbo].[WorkflowApprovals] a ON a.StepInstanceId=s.Id AND a.TenantId=s.TenantId
                        WHERE s.TenantId=i.TenantId AND s.WorkflowInstanceId=w.Id AND s.IsDeleted=0 AND a.IsDeleted=0
                          AND s.Status=2 AND s.CompletedDate IS NOT NULL AND a.Status=1 AND a.ProcessedById IS NOT NULL
                          AND a.ProcessedDate IS NOT NULL AND a.ProcessedDate<=s.CompletedDate AND a.ProcessedDate<=w.CompletedDate
                    ) latestApproval
                    OUTER APPLY
                    (
                        SELECT COUNT_BIG(*) AS FinalApprovalCount
                        FROM [dbo].[WorkflowStepInstances] s
                        JOIN [dbo].[WorkflowApprovals] a ON a.StepInstanceId=s.Id AND a.TenantId=s.TenantId
                        WHERE s.TenantId=i.TenantId AND s.WorkflowInstanceId=w.Id AND s.IsDeleted=0 AND a.IsDeleted=0
                          AND s.Status=2 AND s.CompletedDate IS NOT NULL AND a.Status=1 AND a.ProcessedById IS NOT NULL
                          AND a.ProcessedDate=latestApproval.FinalProcessedDate AND a.ProcessedDate<=s.CompletedDate AND a.ProcessedDate<=w.CompletedDate
                    ) finalApproval
                    OUTER APPLY
                    (
                        SELECT TOP (1) 1 AS InitiatorApproved
                        FROM [dbo].[WorkflowStepInstances] s
                        JOIN [dbo].[WorkflowApprovals] a ON a.StepInstanceId=s.Id AND a.TenantId=s.TenantId
                        WHERE s.TenantId=i.TenantId AND s.WorkflowInstanceId=w.Id AND s.IsDeleted=0 AND a.IsDeleted=0
                          AND s.Status=2 AND s.CompletedDate IS NOT NULL AND a.Status=1 AND a.ProcessedById=w.InitiatedById
                          AND a.ProcessedDate=latestApproval.FinalProcessedDate
                          AND a.ProcessedDate<=s.CompletedDate AND a.ProcessedDate<=w.CompletedDate
                    ) initiatorApproval
                    WHERE i.SourceWorkflowInstanceId IS NOT NULL
                      AND (w.Id IS NULL OR w.EntityId<>i.SourceDocumentId OR w.IsDeleted=1 OR wt.Id IS NULL OR wt.IsDeleted=1 OR wt.IsActive=0
                        OR UPPER(LTRIM(RTRIM(wt.Code))) COLLATE Latin1_General_100_BIN2<>i.[SourceWorkflowEntityType] COLLATE Latin1_General_100_BIN2
                        OR (i.OriginalFinancePostingEventId IS NULL AND i.FreezeStage=N'SUBMITTED' AND w.Status NOT IN (0,1,6))
                        OR ((i.OriginalFinancePostingEventId IS NOT NULL OR i.FreezeStage IN (N'AUTHORIZED',N'PRE_POST'))
                            AND (w.Status<>2 OR w.CompletedDate IS NULL OR finalApproval.FinalApprovalCount<>1 OR COALESCE(initiatorApproval.InitiatorApproved,0)<>0)))
                ) THROW 51005, 'SOURCE_BOOK_AUTHORITY_WORKFLOW_EVIDENCE_MISMATCH', 1;

                IF EXISTS
                (
                    SELECT 1 FROM inserted i
                    JOIN [dbo].[FinanceSourceBookAuthorities] p ON p.Id=i.SupersedesAuthorityId AND p.TenantId=i.TenantId
                    WHERE i.SupersedesAuthorityId IS NOT NULL
                      AND (p.OriginalFinancePostingEventId IS NOT NULL OR p.OriginalJournalEntryId IS NOT NULL
                        OR p.OriginModuleCode<>i.OriginModuleCode OR p.SourceDocumentType<>i.SourceDocumentType
                        OR p.SourceDocumentId<>i.SourceDocumentId OR p.PostingAction<>i.PostingAction
                        OR p.AuthorityVersion+1<>i.AuthorityVersion OR p.SelectionBasis<>i.SelectionBasis)
                ) THROW 51008, 'SOURCE_BOOK_AUTHORITY_SUPERSESSION_INVALID', 1;

                IF EXISTS
                (
                    SELECT 1 FROM inserted i
                    LEFT JOIN [dbo].[FinancePostingEvents] f ON f.Id=i.OriginalFinancePostingEventId AND f.TenantId=i.TenantId
                    LEFT JOIN [dbo].[JournalEntries] j ON j.Id=i.OriginalJournalEntryId AND j.TenantId=i.TenantId
                    WHERE i.OriginalFinancePostingEventId IS NOT NULL
                      AND (f.Id IS NULL OR j.Id IS NULL OR f.JournalEntryId<>j.Id OR f.PostingStatus<>N'Posted' OR j.PostingStatus<>N'Posted'
                        OR f.IsDeleted=1 OR j.IsDeleted=1 OR j.IsReversed=1 OR j.ReversalJournalEntryId IS NOT NULL OR j.ReplicatedFromJournalEntryId IS NOT NULL
                        OR f.SourceDocumentId<>i.SourceDocumentId OR j.SourceDocumentId<>i.SourceDocumentId
                        OR COALESCE(NULLIF(UPPER(LTRIM(RTRIM(f.OriginModuleCode))),N''),
                            CASE UPPER(LTRIM(RTRIM(f.SourceModule))) WHEN N'PAYROLL' THEN N'HR' WHEN N'INV' THEN N'INV' WHEN N'INVENTORY' THEN N'INV' WHEN N'PROC' THEN N'PROC' WHEN N'PROCUREMENT' THEN N'PROC' WHEN N'SALES' THEN N'SALES' ELSE N'FIN' END) COLLATE Latin1_General_100_BIN2<>i.OriginModuleCode COLLATE Latin1_General_100_BIN2
                        OR {{journalOriginExpression}} COLLATE Latin1_General_100_BIN2<>i.OriginModuleCode COLLATE Latin1_General_100_BIN2
                        OR UPPER(LTRIM(RTRIM(f.SourceDocumentType))) COLLATE Latin1_General_100_BIN2<>i.SourceDocumentType COLLATE Latin1_General_100_BIN2
                        OR UPPER(LTRIM(RTRIM(f.PostingAction))) COLLATE Latin1_General_100_BIN2<>i.PostingAction COLLATE Latin1_General_100_BIN2
                        OR UPPER(LTRIM(RTRIM(j.SourceDocumentType))) COLLATE Latin1_General_100_BIN2<>i.SourceDocumentType COLLATE Latin1_General_100_BIN2
                        OR f.AccountingBookId<>i.AccountingBookId OR j.AccountingBookId<>i.AccountingBookId
                        OR f.BookClassification COLLATE Latin1_General_100_BIN2<>i.AccountingBookCode COLLATE Latin1_General_100_BIN2
                        OR j.BookClassification COLLATE Latin1_General_100_BIN2<>i.AccountingBookCode COLLATE Latin1_General_100_BIN2
                        OR f.FunctionalCurrencyCode COLLATE Latin1_General_100_BIN2<>i.FunctionalCurrencyCode COLLATE Latin1_General_100_BIN2
                        OR COALESCE(f.PrimaryTransactionCurrencyCode,f.FunctionalCurrencyCode) COLLATE Latin1_General_100_BIN2<>i.TransactionCurrencyCode COLLATE Latin1_General_100_BIN2
                        OR CAST(f.PostingDate AS date)<>i.EffectiveDate OR CAST(j.EntryDate AS date)<>i.EffectiveDate)
                ) THROW 51006, 'SOURCE_BOOK_AUTHORITY_POSTING_EVIDENCE_MISMATCH', 1;
            END
            """;
    }
}
