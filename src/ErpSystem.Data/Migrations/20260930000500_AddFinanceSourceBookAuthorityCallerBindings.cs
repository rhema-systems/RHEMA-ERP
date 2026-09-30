using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext)), Migration("20260930000500_AddFinanceSourceBookAuthorityCallerBindings")]
public partial class AddFinanceSourceBookAuthorityCallerBindings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_FinanceSourceBookAuthorities_ImmutableBinding]; DROP TRIGGER IF EXISTS [dbo].[TR_FinanceSourceBookAuthorities_Evidence];");
        migrationBuilder.AddColumn<string>(name: "SourceWorkflowEntityType", table: "FinanceSourceBookAuthorities", type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.Sql(
            """
            UPDATE [dbo].[FinanceSourceBookAuthorities]
            SET [SourceWorkflowEntityType]=[SourceDocumentType]
            WHERE [SourceWorkflowEntityType] IS NULL;

            ;WITH OriginEvidence AS
            (
                SELECT o.[TenantId],o.[FinanceSourceBookAuthorityId],
                    STRING_AGG(CONCAT(LOWER(CONVERT(varchar(36),o.[OriginAuthorityId])),N':',o.[Role],N':',
                        LOWER(CONVERT(varchar(36),o.[OriginalFinancePostingEventId])),N':',
                        LOWER(CONVERT(varchar(36),o.[OriginalJournalEntryId]))),N'|')
                    WITHIN GROUP (ORDER BY CONVERT(char(36),o.[OriginAuthorityId]),o.[Role]) AS Evidence
                FROM [dbo].[FinanceSourceBookAuthorityOrigins] o
                WHERE o.[IsDeleted]=0
                GROUP BY o.[TenantId],o.[FinanceSourceBookAuthorityId]
            )
            UPDATE a SET [AuthorityFingerprint]=UPPER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varchar(max),CONCAT(
                N'SOURCE-BOOK-AUTHORITY-V1|',LOWER(CONVERT(varchar(36),a.[TenantId])),N'|',a.[OriginModuleCode],N'|',a.[SourceDocumentType],N'|',
                LOWER(CONVERT(varchar(36),a.[SourceDocumentId])),N'|',a.[PostingAction],N'|',a.[AuthorityVersion],N'|',
                COALESCE(LOWER(CONVERT(varchar(36),a.[SupersedesAuthorityId])),N''),N'|',
                COALESCE(LOWER(CONVERT(varchar(36),a.[SourceWorkflowInstanceId])),N''),N'|',a.[FreezeStage],N'|',CONVERT(char(10),a.[EffectiveDate],23),N'|',
                a.[SourceWorkflowEntityType],N'|',LOWER(CONVERT(varchar(36),a.[AccountingBookId])),N'|',a.[AccountingBookCode],N'|',
                a.[FunctionalCurrencyCode],N'|',a.[TransactionCurrencyCode],N'|',a.[SelectionBasis],N'|',COALESCE(o.Evidence,N''),N'|',
                CASE WHEN a.[SelectionBasis]=N'RETAINED_POSTED_ORIGINAL' THEN CONCAT(N'LEGACY:',LOWER(CONVERT(varchar(36),a.[OriginalFinancePostingEventId])),N':',LOWER(CONVERT(varchar(36),a.[OriginalJournalEntryId]))) ELSE N'' END
            ))),2))
            FROM [dbo].[FinanceSourceBookAuthorities] a
            LEFT JOIN OriginEvidence o ON o.[TenantId]=a.[TenantId] AND o.[FinanceSourceBookAuthorityId]=a.[Id];
            """);
        migrationBuilder.AlterColumn<string>(name: "SourceWorkflowEntityType", table: "FinanceSourceBookAuthorities", type: "nvarchar(100)", maxLength: 100, nullable: false, oldClrType: typeof(string), oldType: "nvarchar(100)", oldMaxLength: 100, oldNullable: true);

        AddCallerBinding(migrationBuilder, "Invoices");
        AddCallerBinding(migrationBuilder, "CustomerPayment");
        AddCallerBinding(migrationBuilder, "CashTransaction");
        AddCallerBinding(migrationBuilder, "VendorInvoice");
        migrationBuilder.Sql(ImmutableTriggerSql(includeWorkflowType: true));
        migrationBuilder.Sql(EvidenceTriggerSql("i.[SourceWorkflowEntityType]"));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (SELECT 1 FROM [dbo].[Invoices] WHERE [SourceBookAuthorityId] IS NOT NULL)
               OR EXISTS (SELECT 1 FROM [dbo].[CustomerPayment] WHERE [SourceBookAuthorityId] IS NOT NULL)
               OR EXISTS (SELECT 1 FROM [dbo].[CashTransaction] WHERE [SourceBookAuthorityId] IS NOT NULL)
               OR EXISTS (SELECT 1 FROM [dbo].[VendorInvoice] WHERE [SourceBookAuthorityId] IS NOT NULL)
                THROW 51009, 'SOURCE_BOOK_AUTHORITY_CALLER_DOWN_BLOCKED: retained caller authority links exist.', 1;
            IF EXISTS (SELECT 1 FROM [dbo].[FinanceSourceBookAuthorities])
                THROW 51010, 'SOURCE_BOOK_AUTHORITY_WORKFLOW_TYPE_DOWN_BLOCKED: retained authority evidence uses the workflow-type contract.', 1;
            DROP TRIGGER IF EXISTS [dbo].[TR_FinanceSourceBookAuthorities_ImmutableBinding];
            DROP TRIGGER IF EXISTS [dbo].[TR_FinanceSourceBookAuthorities_Evidence];
            """);
        DropCallerBinding(migrationBuilder, "VendorInvoice");
        DropCallerBinding(migrationBuilder, "CashTransaction");
        DropCallerBinding(migrationBuilder, "CustomerPayment");
        DropCallerBinding(migrationBuilder, "Invoices");
        migrationBuilder.DropColumn(name: "SourceWorkflowEntityType", table: "FinanceSourceBookAuthorities");
        migrationBuilder.Sql(ImmutableTriggerSql(includeWorkflowType: false));
        migrationBuilder.Sql(EvidenceTriggerSql("i.[SourceDocumentType]"));
    }

    private static void AddCallerBinding(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.AddColumn<Guid>(name: "SourceBookAuthorityId", table: table, type: "uniqueidentifier", nullable: true);
        migrationBuilder.CreateIndex(name: $"IX_{table}_TenantId_SourceBookAuthorityId", table: table, columns: new[] { "TenantId", "SourceBookAuthorityId" });
        migrationBuilder.AddForeignKey(name: $"FK_{table}_FinanceSourceBookAuthorities_TenantId_SourceBookAuthorityId", table: table,
            columns: new[] { "TenantId", "SourceBookAuthorityId" }, principalTable: "FinanceSourceBookAuthorities",
            principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
    }

    private static void DropCallerBinding(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.DropForeignKey(name: $"FK_{table}_FinanceSourceBookAuthorities_TenantId_SourceBookAuthorityId", table: table);
        migrationBuilder.DropIndex(name: $"IX_{table}_TenantId_SourceBookAuthorityId", table: table);
        migrationBuilder.DropColumn(name: "SourceBookAuthorityId", table: table);
    }

    private static string ImmutableTriggerSql(bool includeWorkflowType)
    {
        var workflowColumn = includeWorkflowType ? ",i.SourceWorkflowEntityType" : string.Empty;
        var deletedWorkflowColumn = includeWorkflowType ? ",d.SourceWorkflowEntityType" : string.Empty;
        return $$"""
            CREATE TRIGGER [dbo].[TR_FinanceSourceBookAuthorities_ImmutableBinding]
            ON [dbo].[FinanceSourceBookAuthorities] AFTER UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS
                (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id
                    WHERE d.OriginalFinancePostingEventId IS NOT NULL OR d.OriginalJournalEntryId IS NOT NULL OR d.BoundByUserId IS NOT NULL OR d.BoundAtUtc IS NOT NULL
                       OR i.OriginalFinancePostingEventId IS NULL OR i.OriginalJournalEntryId IS NULL OR i.BoundByUserId IS NULL OR i.BoundAtUtc IS NULL
                       OR EXISTS
                       (
                           SELECT i.Id,i.TenantId,i.OriginModuleCode,i.SourceDocumentType,i.SourceDocumentId,i.PostingAction,i.AuthorityVersion,i.SupersedesAuthorityId,i.SourceWorkflowInstanceId{{workflowColumn}},i.FreezeStage,i.EffectiveDate,i.AccountingBookId,i.AccountingBookCode,i.FunctionalCurrencyCode,i.TransactionCurrencyCode,i.SelectionBasis,i.AuthorityFingerprint,i.FrozenByUserId,i.FrozenAtUtc,i.CreatedAt,i.CreatedBy,i.CreatedById,i.IsDeleted,i.DeletedAt,i.DeletedBy
                           EXCEPT
                           SELECT d.Id,d.TenantId,d.OriginModuleCode,d.SourceDocumentType,d.SourceDocumentId,d.PostingAction,d.AuthorityVersion,d.SupersedesAuthorityId,d.SourceWorkflowInstanceId{{deletedWorkflowColumn}},d.FreezeStage,d.EffectiveDate,d.AccountingBookId,d.AccountingBookCode,d.FunctionalCurrencyCode,d.TransactionCurrencyCode,d.SelectionBasis,d.AuthorityFingerprint,d.FrozenByUserId,d.FrozenAtUtc,d.CreatedAt,d.CreatedBy,d.CreatedById,d.IsDeleted,d.DeletedAt,d.DeletedBy
                       )
                ) THROW 51001, 'SOURCE_BOOK_AUTHORITY_IMMUTABLE: only the first complete original-posting binding is allowed.', 1;
            END
            """;
    }

    private static string EvidenceTriggerSql(string workflowTypeExpression) => $$"""
        CREATE TRIGGER [dbo].[TR_FinanceSourceBookAuthorities_Evidence]
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
                    SELECT COUNT_BIG(*) AS FinalApprovalCount,
                        SUM(CASE WHEN a.ProcessedById=w.InitiatedById THEN 1 ELSE 0 END) AS InitiatorApprovalCount
                    FROM [dbo].[WorkflowStepInstances] s
                    JOIN [dbo].[WorkflowApprovals] a ON a.StepInstanceId=s.Id AND a.TenantId=s.TenantId
                    WHERE s.TenantId=i.TenantId AND s.WorkflowInstanceId=w.Id AND s.IsDeleted=0 AND a.IsDeleted=0
                      AND s.Status=2 AND s.CompletedDate IS NOT NULL AND a.Status=1 AND a.ProcessedById IS NOT NULL
                      AND a.ProcessedDate=latestApproval.FinalProcessedDate AND a.ProcessedDate<=s.CompletedDate AND a.ProcessedDate<=w.CompletedDate
                ) finalApproval
                WHERE i.SourceWorkflowInstanceId IS NOT NULL
                  AND (w.Id IS NULL OR w.EntityId<>i.SourceDocumentId OR w.IsDeleted=1 OR wt.Id IS NULL OR wt.IsDeleted=1 OR wt.IsActive=0
                    OR UPPER(LTRIM(RTRIM(wt.Code))) COLLATE Latin1_General_100_BIN2<>{{workflowTypeExpression}} COLLATE Latin1_General_100_BIN2
                    OR (i.OriginalFinancePostingEventId IS NULL AND i.FreezeStage=N'SUBMITTED' AND w.Status NOT IN (0,1,6))
                    OR ((i.OriginalFinancePostingEventId IS NOT NULL OR i.FreezeStage IN (N'AUTHORIZED',N'PRE_POST'))
                        AND (w.Status<>2 OR w.CompletedDate IS NULL OR finalApproval.FinalApprovalCount<>1 OR COALESCE(finalApproval.InitiatorApprovalCount,0)<>0)))
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
                    OR (CASE UPPER(LTRIM(RTRIM(j.SourceModule))) WHEN N'PAYROLL' THEN N'HR' WHEN N'INV' THEN N'INV' WHEN N'INVENTORY' THEN N'INV' WHEN N'PROC' THEN N'PROC' WHEN N'PROCUREMENT' THEN N'PROC' WHEN N'SALES' THEN N'SALES' ELSE N'FIN' END) COLLATE Latin1_General_100_BIN2<>i.OriginModuleCode COLLATE Latin1_General_100_BIN2
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
