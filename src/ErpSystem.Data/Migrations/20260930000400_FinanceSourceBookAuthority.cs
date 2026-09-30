using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext)), Migration("20260930000400_FinanceSourceBookAuthority")]
public partial class FinanceSourceBookAuthority : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[dbo].[FinanceSourceBookAuthorityOrigins]', N'U') IS NOT NULL
               OR OBJECT_ID(N'[dbo].[FinanceSourceBookAuthorities]', N'U') IS NOT NULL
                THROW 51000, 'SOURCE_BOOK_AUTHORITY_SCHEMA_EXISTS: reconcile the existing schema before applying this migration.', 1;

            CREATE TABLE [dbo].[FinanceSourceBookAuthorities]
            (
                [Id] uniqueidentifier NOT NULL,
                [TenantId] uniqueidentifier NOT NULL,
                [OriginModuleCode] nvarchar(10) NOT NULL,
                [SourceDocumentType] nvarchar(100) NOT NULL,
                [SourceDocumentId] uniqueidentifier NOT NULL,
                [PostingAction] nvarchar(50) NOT NULL,
                [AuthorityVersion] int NOT NULL,
                [SupersedesAuthorityId] uniqueidentifier NULL,
                [SourceWorkflowInstanceId] uniqueidentifier NULL,
                [FreezeStage] nvarchar(30) NOT NULL,
                [EffectiveDate] date NOT NULL,
                [AccountingBookId] uniqueidentifier NOT NULL,
                [AccountingBookCode] nvarchar(20) NOT NULL,
                [FunctionalCurrencyCode] nvarchar(3) NOT NULL,
                [TransactionCurrencyCode] nvarchar(3) NOT NULL,
                [SelectionBasis] nvarchar(30) NOT NULL,
                [AuthorityFingerprint] nvarchar(64) NOT NULL,
                [FrozenByUserId] uniqueidentifier NULL,
                [FrozenAtUtc] datetime2 NOT NULL,
                [OriginalFinancePostingEventId] uniqueidentifier NULL,
                [OriginalJournalEntryId] uniqueidentifier NULL,
                [BoundByUserId] uniqueidentifier NULL,
                [BoundAtUtc] datetime2 NULL,
                [RowVersion] rowversion NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                [UpdatedAt] datetime2 NULL,
                [CreatedBy] nvarchar(max) NULL,
                [UpdatedBy] nvarchar(max) NULL,
                [CreatedById] uniqueidentifier NULL,
                [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL,
                [DeletedBy] nvarchar(max) NULL,
                CONSTRAINT [PK_FinanceSourceBookAuthorities] PRIMARY KEY ([Id]),
                CONSTRAINT [AK_FinanceSourceBookAuthorities_TenantId_Id] UNIQUE ([TenantId], [Id]),
                CONSTRAINT [CK_FinanceSourceBookAuthorities_NoDelete] CHECK ([IsDeleted] = 0),
                CONSTRAINT [CK_FinanceSourceBookAuthorities_Version] CHECK ([AuthorityVersion] > 0),
                CONSTRAINT [CK_FinanceSourceBookAuthorities_Lineage] CHECK (([AuthorityVersion]=1 AND [SupersedesAuthorityId] IS NULL) OR ([AuthorityVersion]>1 AND [SupersedesAuthorityId] IS NOT NULL)),
                CONSTRAINT [CK_FinanceSourceBookAuthorities_FreezeStage] CHECK ([FreezeStage] IN (N'SUBMITTED',N'AUTHORIZED',N'PRE_POST',N'LEGACY_POSTED')),
                CONSTRAINT [CK_FinanceSourceBookAuthorities_SelectionBasis] CHECK ([SelectionBasis] IN (N'DEFAULT_PRIMARY',N'INHERITED_ORIGINAL',N'RETAINED_POSTED_ORIGINAL')),
                CONSTRAINT [CK_FinanceSourceBookAuthorities_BindingShape] CHECK (([OriginalFinancePostingEventId] IS NULL AND [OriginalJournalEntryId] IS NULL AND [BoundByUserId] IS NULL AND [BoundAtUtc] IS NULL) OR ([OriginalFinancePostingEventId] IS NOT NULL AND [OriginalJournalEntryId] IS NOT NULL AND [BoundAtUtc] IS NOT NULL AND (([SelectionBasis]=N'RETAINED_POSTED_ORIGINAL' AND [BoundByUserId] IS NULL) OR ([SelectionBasis]<>N'RETAINED_POSTED_ORIGINAL' AND [BoundByUserId] IS NOT NULL)))),
                CONSTRAINT [CK_FinanceSourceBookAuthorities_LegacyShape] CHECK (([SelectionBasis]=N'RETAINED_POSTED_ORIGINAL' AND [FreezeStage]=N'LEGACY_POSTED' AND [SourceWorkflowInstanceId] IS NULL AND [FrozenByUserId] IS NULL AND [OriginalFinancePostingEventId] IS NOT NULL) OR ([SelectionBasis]<>N'RETAINED_POSTED_ORIGINAL' AND [FreezeStage]<>N'LEGACY_POSTED' AND [FrozenByUserId] IS NOT NULL)),
                CONSTRAINT [CK_FinanceSourceBookAuthorities_IdentityCanonical] CHECK (DATALENGTH([OriginModuleCode])=LEN([OriginModuleCode])*2 AND LEFT([OriginModuleCode],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [OriginModuleCode] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%' AND DATALENGTH([SourceDocumentType])=LEN([SourceDocumentType])*2 AND LEFT([SourceDocumentType],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [SourceDocumentType] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%' AND DATALENGTH([PostingAction])=LEN([PostingAction])*2 AND LEFT([PostingAction],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [PostingAction] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%'),
                CONSTRAINT [CK_FinanceSourceBookAuthorities_CurrencyCanonical] CHECK ([FunctionalCurrencyCode] COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z][A-Z][A-Z]' AND [TransactionCurrencyCode] COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z][A-Z][A-Z]'),
                CONSTRAINT [CK_FinanceSourceBookAuthorities_Fingerprint] CHECK (LEN([AuthorityFingerprint])=64 AND [AuthorityFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'),
                CONSTRAINT [FK_FinanceSourceBookAuthorities_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
                CONSTRAINT [FK_FinanceSourceBookAuthorities_AccountingBooks_TenantId_AccountingBookId] FOREIGN KEY ([TenantId],[AccountingBookId]) REFERENCES [dbo].[AccountingBooks]([TenantId],[Id]),
                CONSTRAINT [FK_FinanceSourceBookAuthorities_FinanceSourceBookAuthorities_TenantId_SupersedesAuthorityId] FOREIGN KEY ([TenantId],[SupersedesAuthorityId]) REFERENCES [dbo].[FinanceSourceBookAuthorities]([TenantId],[Id]),
                CONSTRAINT [FK_FinanceSourceBookAuthorities_WorkflowInstances_SourceWorkflowInstanceId] FOREIGN KEY ([SourceWorkflowInstanceId]) REFERENCES [dbo].[WorkflowInstances]([Id]),
                CONSTRAINT [FK_FinanceSourceBookAuthorities_FinancePostingEvents_TenantId_OriginalFinancePostingEventId] FOREIGN KEY ([TenantId],[OriginalFinancePostingEventId]) REFERENCES [dbo].[FinancePostingEvents]([TenantId],[Id]),
                CONSTRAINT [FK_FinanceSourceBookAuthorities_JournalEntries_TenantId_OriginalJournalEntryId] FOREIGN KEY ([TenantId],[OriginalJournalEntryId]) REFERENCES [dbo].[JournalEntries]([TenantId],[Id])
            );

            CREATE UNIQUE INDEX [IX_FinanceSourceBookAuthorities_SourceVersion] ON [dbo].[FinanceSourceBookAuthorities] ([TenantId],[OriginModuleCode],[SourceDocumentType],[SourceDocumentId],[PostingAction],[AuthorityVersion]);
            CREATE UNIQUE INDEX [IX_FinanceSourceBookAuthorities_SourceWorkflow] ON [dbo].[FinanceSourceBookAuthorities] ([TenantId],[OriginModuleCode],[SourceDocumentType],[SourceDocumentId],[PostingAction],[SourceWorkflowInstanceId]) WHERE [SourceWorkflowInstanceId] IS NOT NULL;
            CREATE UNIQUE INDEX [IX_FinanceSourceBookAuthorities_Supersedes] ON [dbo].[FinanceSourceBookAuthorities] ([TenantId],[SupersedesAuthorityId]) WHERE [SupersedesAuthorityId] IS NOT NULL;
            CREATE UNIQUE INDEX [IX_FinanceSourceBookAuthorities_PostingEvent] ON [dbo].[FinanceSourceBookAuthorities] ([TenantId],[OriginalFinancePostingEventId]) WHERE [OriginalFinancePostingEventId] IS NOT NULL;
            CREATE UNIQUE INDEX [IX_FinanceSourceBookAuthorities_Journal] ON [dbo].[FinanceSourceBookAuthorities] ([TenantId],[OriginalJournalEntryId]) WHERE [OriginalJournalEntryId] IS NOT NULL;
            CREATE INDEX [IX_FinanceSourceBookAuthorities_AccountingBookId] ON [dbo].[FinanceSourceBookAuthorities] ([TenantId],[AccountingBookId]);
            CREATE INDEX [IX_FinanceSourceBookAuthorities_Workflow] ON [dbo].[FinanceSourceBookAuthorities] ([SourceWorkflowInstanceId]);

            CREATE TABLE [dbo].[FinanceSourceBookAuthorityOrigins]
            (
                [Id] uniqueidentifier NOT NULL,
                [TenantId] uniqueidentifier NOT NULL,
                [FinanceSourceBookAuthorityId] uniqueidentifier NOT NULL,
                [OriginAuthorityId] uniqueidentifier NOT NULL,
                [Role] nvarchar(30) NOT NULL,
                [OriginalFinancePostingEventId] uniqueidentifier NOT NULL,
                [OriginalJournalEntryId] uniqueidentifier NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                [UpdatedAt] datetime2 NULL,
                [CreatedBy] nvarchar(max) NULL,
                [UpdatedBy] nvarchar(max) NULL,
                [CreatedById] uniqueidentifier NULL,
                [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL,
                [DeletedBy] nvarchar(max) NULL,
                CONSTRAINT [PK_FinanceSourceBookAuthorityOrigins] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_FinanceSourceBookAuthorityOrigins_NoDelete] CHECK ([IsDeleted] = 0),
                CONSTRAINT [CK_FinanceSourceBookAuthorityOrigins_NoSelf] CHECK ([FinanceSourceBookAuthorityId] <> [OriginAuthorityId]),
                CONSTRAINT [FK_FinanceSourceBookAuthorityOrigins_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
                CONSTRAINT [FK_FinanceSourceBookAuthorityOrigins_Authorities_TenantId_AuthorityId] FOREIGN KEY ([TenantId],[FinanceSourceBookAuthorityId]) REFERENCES [dbo].[FinanceSourceBookAuthorities]([TenantId],[Id]),
                CONSTRAINT [FK_FinanceSourceBookAuthorityOrigins_OriginAuthorities_TenantId_OriginAuthorityId] FOREIGN KEY ([TenantId],[OriginAuthorityId]) REFERENCES [dbo].[FinanceSourceBookAuthorities]([TenantId],[Id]),
                CONSTRAINT [FK_FinanceSourceBookAuthorityOrigins_Events_TenantId_EventId] FOREIGN KEY ([TenantId],[OriginalFinancePostingEventId]) REFERENCES [dbo].[FinancePostingEvents]([TenantId],[Id]),
                CONSTRAINT [FK_FinanceSourceBookAuthorityOrigins_Journals_TenantId_JournalId] FOREIGN KEY ([TenantId],[OriginalJournalEntryId]) REFERENCES [dbo].[JournalEntries]([TenantId],[Id])
            );
            CREATE UNIQUE INDEX [IX_FinanceSourceBookAuthorityOrigins_AuthorityOriginRole] ON [dbo].[FinanceSourceBookAuthorityOrigins] ([TenantId],[FinanceSourceBookAuthorityId],[OriginAuthorityId],[Role]);
            CREATE INDEX [IX_FinanceSourceBookAuthorityOrigins_Origin] ON [dbo].[FinanceSourceBookAuthorityOrigins] ([TenantId],[OriginAuthorityId]);
            CREATE INDEX [IX_FinanceSourceBookAuthorityOrigins_Event] ON [dbo].[FinanceSourceBookAuthorityOrigins] ([TenantId],[OriginalFinancePostingEventId]);
            CREATE INDEX [IX_FinanceSourceBookAuthorityOrigins_Journal] ON [dbo].[FinanceSourceBookAuthorityOrigins] ([TenantId],[OriginalJournalEntryId]);
            """);

        migrationBuilder.Sql(
            """
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
                           SELECT i.Id,i.TenantId,i.OriginModuleCode,i.SourceDocumentType,i.SourceDocumentId,i.PostingAction,i.AuthorityVersion,i.SupersedesAuthorityId,i.SourceWorkflowInstanceId,i.FreezeStage,i.EffectiveDate,i.AccountingBookId,i.AccountingBookCode,i.FunctionalCurrencyCode,i.TransactionCurrencyCode,i.SelectionBasis,i.AuthorityFingerprint,i.FrozenByUserId,i.FrozenAtUtc,i.CreatedAt,i.CreatedBy,i.CreatedById,i.IsDeleted,i.DeletedAt,i.DeletedBy
                           EXCEPT
                           SELECT d.Id,d.TenantId,d.OriginModuleCode,d.SourceDocumentType,d.SourceDocumentId,d.PostingAction,d.AuthorityVersion,d.SupersedesAuthorityId,d.SourceWorkflowInstanceId,d.FreezeStage,d.EffectiveDate,d.AccountingBookId,d.AccountingBookCode,d.FunctionalCurrencyCode,d.TransactionCurrencyCode,d.SelectionBasis,d.AuthorityFingerprint,d.FrozenByUserId,d.FrozenAtUtc,d.CreatedAt,d.CreatedBy,d.CreatedById,d.IsDeleted,d.DeletedAt,d.DeletedBy
                       )
                ) THROW 51001, 'SOURCE_BOOK_AUTHORITY_IMMUTABLE: only the first complete original-posting binding is allowed.', 1;
            END
            """);
        migrationBuilder.Sql(
            """
            CREATE TRIGGER [dbo].[TR_FinanceSourceBookAuthorities_NoDelete]
            ON [dbo].[FinanceSourceBookAuthorities] INSTEAD OF DELETE AS
            BEGIN
                THROW 51002, 'SOURCE_BOOK_AUTHORITY_NO_DELETE: authority evidence is immutable.', 1;
            END
            """);
        migrationBuilder.Sql(
            """
            CREATE TRIGGER [dbo].[TR_FinanceSourceBookAuthorities_Evidence]
            ON [dbo].[FinanceSourceBookAuthorities] AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS
                (
                    SELECT 1 FROM inserted i
                    LEFT JOIN [dbo].[WorkflowInstances] w ON w.Id=i.SourceWorkflowInstanceId
                    LEFT JOIN [dbo].[WorkflowEntityTypes] wt ON wt.Id=w.EntityTypeId AND wt.TenantId=w.TenantId
                    WHERE i.SourceWorkflowInstanceId IS NOT NULL
                      AND (w.Id IS NULL OR w.TenantId<>i.TenantId OR w.EntityId<>i.SourceDocumentId OR w.IsDeleted=1
                        OR wt.Id IS NULL OR wt.IsDeleted=1 OR wt.IsActive=0
                        OR UPPER(LTRIM(RTRIM(wt.Code))) COLLATE Latin1_General_100_BIN2<>i.SourceDocumentType COLLATE Latin1_General_100_BIN2
                        OR (i.OriginalFinancePostingEventId IS NULL AND i.FreezeStage=N'SUBMITTED' AND w.Status NOT IN (0,1,6))
                        OR (i.OriginalFinancePostingEventId IS NULL AND i.FreezeStage IN (N'AUTHORIZED',N'PRE_POST') AND (w.Status<>2 OR w.CompletedDate IS NULL))
                        OR (i.OriginalFinancePostingEventId IS NOT NULL AND (w.Status<>2 OR w.CompletedDate IS NULL)))
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
                            CASE UPPER(LTRIM(RTRIM(f.SourceModule))) WHEN N'PAYROLL' THEN N'HR' WHEN N'INV' THEN N'INV'
                                WHEN N'INVENTORY' THEN N'INV' WHEN N'PROC' THEN N'PROC' WHEN N'PROCUREMENT' THEN N'PROC'
                                WHEN N'SALES' THEN N'SALES' ELSE N'FIN' END) COLLATE Latin1_General_100_BIN2<>i.OriginModuleCode COLLATE Latin1_General_100_BIN2
                        OR (CASE UPPER(LTRIM(RTRIM(j.SourceModule))) WHEN N'PAYROLL' THEN N'HR' WHEN N'INV' THEN N'INV'
                                WHEN N'INVENTORY' THEN N'INV' WHEN N'PROC' THEN N'PROC' WHEN N'PROCUREMENT' THEN N'PROC'
                                WHEN N'SALES' THEN N'SALES' ELSE N'FIN' END) COLLATE Latin1_General_100_BIN2<>i.OriginModuleCode COLLATE Latin1_General_100_BIN2
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
            """);
        migrationBuilder.Sql(
            """
            CREATE TRIGGER [dbo].[TR_FinanceSourceBookAuthorityOrigins_AppendOnly]
            ON [dbo].[FinanceSourceBookAuthorityOrigins] INSTEAD OF UPDATE, DELETE AS
            BEGIN
                THROW 51003, 'SOURCE_BOOK_AUTHORITY_ORIGIN_APPEND_ONLY: inherited origin evidence is immutable.', 1;
            END
            """);
        migrationBuilder.Sql(
            """
            CREATE TRIGGER [dbo].[TR_FinanceSourceBookAuthorityOrigins_Evidence]
            ON [dbo].[FinanceSourceBookAuthorityOrigins] AFTER INSERT AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS
                (
                    SELECT 1 FROM inserted i
                    JOIN [dbo].[FinanceSourceBookAuthorities] target ON target.Id=i.FinanceSourceBookAuthorityId AND target.TenantId=i.TenantId
                    JOIN [dbo].[FinanceSourceBookAuthorities] origin ON origin.Id=i.OriginAuthorityId AND origin.TenantId=i.TenantId
                    WHERE target.SelectionBasis<>N'INHERITED_ORIGINAL'
                       OR origin.OriginalFinancePostingEventId IS NULL OR origin.OriginalJournalEntryId IS NULL
                       OR i.OriginalFinancePostingEventId<>origin.OriginalFinancePostingEventId
                       OR i.OriginalJournalEntryId<>origin.OriginalJournalEntryId
                       OR target.AccountingBookId<>origin.AccountingBookId
                       OR target.AccountingBookCode COLLATE Latin1_General_100_BIN2<>origin.AccountingBookCode COLLATE Latin1_General_100_BIN2
                       OR target.FunctionalCurrencyCode COLLATE Latin1_General_100_BIN2<>origin.FunctionalCurrencyCode COLLATE Latin1_General_100_BIN2
                ) THROW 51007, 'SOURCE_BOOK_AUTHORITY_ORIGIN_EVIDENCE_MISMATCH', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (SELECT 1 FROM [dbo].[FinanceSourceBookAuthorityOrigins])
               OR EXISTS (SELECT 1 FROM [dbo].[FinanceSourceBookAuthorities])
                THROW 51004, 'SOURCE_BOOK_AUTHORITY_DOWN_BLOCKED: immutable authority evidence exists.', 1;
            DROP TRIGGER IF EXISTS [dbo].[TR_FinanceSourceBookAuthorityOrigins_AppendOnly];
            DROP TRIGGER IF EXISTS [dbo].[TR_FinanceSourceBookAuthorityOrigins_Evidence];
            DROP TRIGGER IF EXISTS [dbo].[TR_FinanceSourceBookAuthorities_NoDelete];
            DROP TRIGGER IF EXISTS [dbo].[TR_FinanceSourceBookAuthorities_ImmutableBinding];
            DROP TRIGGER IF EXISTS [dbo].[TR_FinanceSourceBookAuthorities_Evidence];
            DROP TABLE [dbo].[FinanceSourceBookAuthorityOrigins];
            DROP TABLE [dbo].[FinanceSourceBookAuthorities];
            """);
    }
}
