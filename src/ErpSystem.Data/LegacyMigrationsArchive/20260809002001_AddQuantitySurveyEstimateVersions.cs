using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260809002001_AddQuantitySurveyEstimateVersions")]
public partial class AddQuantitySurveyEstimateVersions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE [QuantitySurveyEstimateVersions] (
                [Id] uniqueidentifier NOT NULL,
                [ProjectId] uniqueidentifier NOT NULL,
                [ProjectBoqVersionId] uniqueidentifier NOT NULL,
                [SourceEstimateVersionId] uniqueidentifier NULL,
                [ClientRequestId] uniqueidentifier NOT NULL,
                [VersionNumber] int NOT NULL,
                [EstimateType] int NOT NULL,
                [Name] nvarchar(160) NOT NULL,
                [EstimateDate] datetime2 NOT NULL,
                [CurrencyId] uniqueidentifier NOT NULL,
                [CurrencyCodeSnapshot] varchar(3) NOT NULL,
                [DirectCost] decimal(18,2) NOT NULL,
                [MarkupTotal] decimal(18,2) NOT NULL,
                [TotalAmount] decimal(18,2) NOT NULL,
                [Status] nvarchar(30) NOT NULL,
                [ApprovalStatus] nvarchar(30) NOT NULL,
                [WorkflowInstanceId] uniqueidentifier NULL,
                [WorkflowDefinitionId] uniqueidentifier NULL,
                [SubmittedById] uniqueidentifier NULL,
                [SubmittedAt] datetime2 NULL,
                [ApprovedById] uniqueidentifier NULL,
                [ApprovedAt] datetime2 NULL,
                [RejectionReason] nvarchar(2000) NULL,
                [ChangeReason] nvarchar(1000) NOT NULL,
                [SnapshotHash] varchar(64) NOT NULL,
                [LineCount] int NOT NULL,
                [AssumptionCount] int NOT NULL,
                [MarkupCount] int NOT NULL,
                [ConfigurationProfileId] uniqueidentifier NOT NULL,
                [ConfigurationDecisionId] uniqueidentifier NOT NULL,
                [ConfigurationProfileVersion] int NOT NULL,
                [CentralDocumentRecordId] uniqueidentifier NULL,
                [CentralDocumentVersionId] uniqueidentifier NULL,
                [AuditAction] nvarchar(100) NOT NULL,
                [CorrelationId] nvarchar(100) NOT NULL,
                [ActorRoles] nvarchar(500) NULL,
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
                [TenantId] uniqueidentifier NOT NULL,
                CONSTRAINT [PK_QuantitySurveyEstimateVersions] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_QsEstimateVersions_Version] CHECK ([VersionNumber] > 0),
                CONSTRAINT [CK_QsEstimateVersions_Type] CHECK ([EstimateType] BETWEEN 0 AND 2),
                CONSTRAINT [CK_QsEstimateVersions_Status] CHECK ([Status] IN ('Draft','PendingApproval','Approved','Rejected','Retired')),
                CONSTRAINT [CK_QsEstimateVersions_Approval] CHECK ([ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')),
                CONSTRAINT [CK_QsEstimateVersions_Totals] CHECK ([DirectCost] >= 0 AND [MarkupTotal] >= 0 AND [TotalAmount] = [DirectCost] + [MarkupTotal]),
                CONSTRAINT [CK_QsEstimateVersions_Counts] CHECK ([LineCount] > 0 AND [AssumptionCount] >= 0 AND [MarkupCount] >= 0),
                CONSTRAINT [CK_QsEstimateVersions_EvidencePair] CHECK (([CentralDocumentRecordId] IS NULL AND [CentralDocumentVersionId] IS NULL) OR ([CentralDocumentRecordId] IS NOT NULL AND [CentralDocumentVersionId] IS NOT NULL)),
                CONSTRAINT [FK_QuantitySurveyEstimateVersions_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects]([Id]),
                CONSTRAINT [FK_QuantitySurveyEstimateVersions_ProjectBoqVersions_ProjectBoqVersionId] FOREIGN KEY ([ProjectBoqVersionId]) REFERENCES [ProjectBoqVersions]([Id]),
                CONSTRAINT [FK_QuantitySurveyEstimateVersions_QuantitySurveyEstimateVersions_SourceEstimateVersionId] FOREIGN KEY ([SourceEstimateVersionId]) REFERENCES [QuantitySurveyEstimateVersions]([Id]),
                CONSTRAINT [FK_QuantitySurveyEstimateVersions_Currencies_CurrencyId] FOREIGN KEY ([CurrencyId]) REFERENCES [Currencies]([Id]),
                CONSTRAINT [FK_QuantitySurveyEstimateVersions_QuantitySurveyConfigurationProfiles_ConfigurationProfileId] FOREIGN KEY ([ConfigurationProfileId]) REFERENCES [QuantitySurveyConfigurationProfiles]([Id]),
                CONSTRAINT [FK_QuantitySurveyEstimateVersions_QuantitySurveyConfigurationDecisions_ConfigurationDecisionId] FOREIGN KEY ([ConfigurationDecisionId]) REFERENCES [QuantitySurveyConfigurationDecisions]([Id]),
                CONSTRAINT [FK_QuantitySurveyEstimateVersions_CentralDocumentRecords_CentralDocumentRecordId] FOREIGN KEY ([CentralDocumentRecordId]) REFERENCES [CentralDocumentRecords]([Id]),
                CONSTRAINT [FK_QuantitySurveyEstimateVersions_CentralDocumentVersions_CentralDocumentVersionId] FOREIGN KEY ([CentralDocumentVersionId]) REFERENCES [CentralDocumentVersions]([Id]),
                CONSTRAINT [FK_QuantitySurveyEstimateVersions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id])
            );

            CREATE TABLE [QuantitySurveyEstimateLines] (
                [Id] uniqueidentifier NOT NULL,
                [EstimateVersionId] uniqueidentifier NOT NULL,
                [Sequence] int NOT NULL,
                [ProjectBoqVersionLineId] uniqueidentifier NOT NULL,
                [SourceRateId] uniqueidentifier NULL,
                [LineNumberSnapshot] nvarchar(50) NOT NULL,
                [ItemCodeSnapshot] nvarchar(50) NULL,
                [DescriptionSnapshot] nvarchar(1000) NOT NULL,
                [UnitOfMeasureSnapshot] nvarchar(20) NULL,
                [Quantity] decimal(18,4) NOT NULL,
                [UnitRate] decimal(18,6) NOT NULL,
                [LineAmount] decimal(18,2) NOT NULL,
                [SourceRateItemCodeSnapshot] nvarchar(50) NULL,
                [SourceRateVersionSnapshot] int NULL,
                [RateSourceSnapshot] nvarchar(50) NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                [UpdatedAt] datetime2 NULL,
                [CreatedBy] nvarchar(max) NULL,
                [UpdatedBy] nvarchar(max) NULL,
                [CreatedById] uniqueidentifier NULL,
                [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL,
                [DeletedBy] nvarchar(max) NULL,
                [TenantId] uniqueidentifier NOT NULL,
                CONSTRAINT [PK_QuantitySurveyEstimateLines] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_QsEstimateLines_Sequence] CHECK ([Sequence] > 0),
                CONSTRAINT [CK_QsEstimateLines_Amounts] CHECK ([Quantity] >= 0 AND [UnitRate] >= 0 AND [LineAmount] >= 0),
                CONSTRAINT [FK_QuantitySurveyEstimateLines_QuantitySurveyEstimateVersions_EstimateVersionId] FOREIGN KEY ([EstimateVersionId]) REFERENCES [QuantitySurveyEstimateVersions]([Id]) ON DELETE CASCADE,
                CONSTRAINT [FK_QuantitySurveyEstimateLines_ProjectBoqVersionLines_ProjectBoqVersionLineId] FOREIGN KEY ([ProjectBoqVersionLineId]) REFERENCES [ProjectBoqVersionLines]([Id]),
                CONSTRAINT [FK_QuantitySurveyEstimateLines_QuantitySurveyRateLibraryRates_SourceRateId] FOREIGN KEY ([SourceRateId]) REFERENCES [QuantitySurveyRateLibraryRates]([Id]),
                CONSTRAINT [FK_QuantitySurveyEstimateLines_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id])
            );

            CREATE TABLE [QuantitySurveyEstimateAssumptions] (
                [Id] uniqueidentifier NOT NULL,
                [EstimateVersionId] uniqueidentifier NOT NULL,
                [Sequence] int NOT NULL,
                [Code] varchar(50) NOT NULL,
                [Description] nvarchar(250) NOT NULL,
                [Value] nvarchar(500) NOT NULL,
                [Unit] nvarchar(50) NULL,
                [CreatedAt] datetime2 NOT NULL,
                [UpdatedAt] datetime2 NULL,
                [CreatedBy] nvarchar(max) NULL,
                [UpdatedBy] nvarchar(max) NULL,
                [CreatedById] uniqueidentifier NULL,
                [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL,
                [DeletedBy] nvarchar(max) NULL,
                [TenantId] uniqueidentifier NOT NULL,
                CONSTRAINT [PK_QuantitySurveyEstimateAssumptions] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_QsEstimateAssumptions_Sequence] CHECK ([Sequence] > 0),
                CONSTRAINT [FK_QuantitySurveyEstimateAssumptions_QuantitySurveyEstimateVersions_EstimateVersionId] FOREIGN KEY ([EstimateVersionId]) REFERENCES [QuantitySurveyEstimateVersions]([Id]) ON DELETE CASCADE,
                CONSTRAINT [FK_QuantitySurveyEstimateAssumptions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id])
            );

            CREATE TABLE [QuantitySurveyEstimateMarkups] (
                [Id] uniqueidentifier NOT NULL,
                [EstimateVersionId] uniqueidentifier NOT NULL,
                [Sequence] int NOT NULL,
                [Component] int NOT NULL,
                [Percentage] decimal(9,4) NOT NULL,
                [BasisAmount] decimal(18,2) NOT NULL,
                [Amount] decimal(18,2) NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                [UpdatedAt] datetime2 NULL,
                [CreatedBy] nvarchar(max) NULL,
                [UpdatedBy] nvarchar(max) NULL,
                [CreatedById] uniqueidentifier NULL,
                [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL,
                [DeletedBy] nvarchar(max) NULL,
                [TenantId] uniqueidentifier NOT NULL,
                CONSTRAINT [PK_QuantitySurveyEstimateMarkups] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_QsEstimateMarkups_Sequence] CHECK ([Sequence] > 0),
                CONSTRAINT [CK_QsEstimateMarkups_Component] CHECK ([Component] IN (5,6,8,9)),
                CONSTRAINT [CK_QsEstimateMarkups_Amounts] CHECK ([Percentage] >= 0 AND [BasisAmount] >= 0 AND [Amount] >= 0),
                CONSTRAINT [FK_QuantitySurveyEstimateMarkups_QuantitySurveyEstimateVersions_EstimateVersionId] FOREIGN KEY ([EstimateVersionId]) REFERENCES [QuantitySurveyEstimateVersions]([Id]) ON DELETE CASCADE,
                CONSTRAINT [FK_QuantitySurveyEstimateMarkups_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id])
            );

            CREATE TABLE [QuantitySurveyEstimateRevisions] (
                [Id] uniqueidentifier NOT NULL,
                [EstimateVersionId] uniqueidentifier NOT NULL,
                [Action] nvarchar(100) NOT NULL,
                [ActorUserId] uniqueidentifier NOT NULL,
                [ActorName] nvarchar(300) NOT NULL,
                [ActorRoles] nvarchar(500) NULL,
                [CorrelationId] nvarchar(100) NOT NULL,
                [Reason] nvarchar(2000) NULL,
                [BeforeJson] nvarchar(max) NULL,
                [AfterJson] nvarchar(max) NULL,
                [CreatedAt] datetime2 NOT NULL,
                [UpdatedAt] datetime2 NULL,
                [CreatedBy] nvarchar(max) NULL,
                [UpdatedBy] nvarchar(max) NULL,
                [CreatedById] uniqueidentifier NULL,
                [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL,
                [DeletedBy] nvarchar(max) NULL,
                [TenantId] uniqueidentifier NOT NULL,
                CONSTRAINT [PK_QuantitySurveyEstimateRevisions] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_QuantitySurveyEstimateRevisions_QuantitySurveyEstimateVersions_EstimateVersionId] FOREIGN KEY ([EstimateVersionId]) REFERENCES [QuantitySurveyEstimateVersions]([Id]),
                CONSTRAINT [FK_QuantitySurveyEstimateRevisions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id])
            );

            CREATE UNIQUE INDEX [IX_QuantitySurveyEstimateVersions_TenantId_ClientRequestId] ON [QuantitySurveyEstimateVersions]([TenantId], [ClientRequestId]);
            CREATE UNIQUE INDEX [IX_QuantitySurveyEstimateVersions_TenantId_ProjectId_EstimateType_VersionNumber] ON [QuantitySurveyEstimateVersions]([TenantId], [ProjectId], [EstimateType], [VersionNumber]);
            CREATE INDEX [IX_QuantitySurveyEstimateVersions_TenantId_ProjectId_EstimateType_Status] ON [QuantitySurveyEstimateVersions]([TenantId], [ProjectId], [EstimateType], [Status]);
            CREATE UNIQUE INDEX [IX_QuantitySurveyEstimateVersions_TenantId_ProjectId_EstimateType] ON [QuantitySurveyEstimateVersions]([TenantId], [ProjectId], [EstimateType]) WHERE [Status] = 'Approved' AND [IsDeleted] = 0;
            CREATE INDEX [IX_QuantitySurveyEstimateVersions_ProjectId] ON [QuantitySurveyEstimateVersions]([ProjectId]);
            CREATE INDEX [IX_QuantitySurveyEstimateVersions_ProjectBoqVersionId] ON [QuantitySurveyEstimateVersions]([ProjectBoqVersionId]);
            CREATE INDEX [IX_QuantitySurveyEstimateVersions_SourceEstimateVersionId] ON [QuantitySurveyEstimateVersions]([SourceEstimateVersionId]);
            CREATE INDEX [IX_QuantitySurveyEstimateVersions_CurrencyId] ON [QuantitySurveyEstimateVersions]([CurrencyId]);
            CREATE INDEX [IX_QuantitySurveyEstimateVersions_ConfigurationProfileId] ON [QuantitySurveyEstimateVersions]([ConfigurationProfileId]);
            CREATE INDEX [IX_QuantitySurveyEstimateVersions_ConfigurationDecisionId] ON [QuantitySurveyEstimateVersions]([ConfigurationDecisionId]);
            CREATE INDEX [IX_QuantitySurveyEstimateVersions_CentralDocumentRecordId] ON [QuantitySurveyEstimateVersions]([CentralDocumentRecordId]);
            CREATE INDEX [IX_QuantitySurveyEstimateVersions_CentralDocumentVersionId] ON [QuantitySurveyEstimateVersions]([CentralDocumentVersionId]);

            CREATE UNIQUE INDEX [IX_QuantitySurveyEstimateLines_TenantId_EstimateVersionId_Sequence] ON [QuantitySurveyEstimateLines]([TenantId], [EstimateVersionId], [Sequence]);
            CREATE UNIQUE INDEX [IX_QuantitySurveyEstimateLines_TenantId_EstimateVersionId_ProjectBoqVersionLineId] ON [QuantitySurveyEstimateLines]([TenantId], [EstimateVersionId], [ProjectBoqVersionLineId]);
            CREATE INDEX [IX_QuantitySurveyEstimateLines_EstimateVersionId] ON [QuantitySurveyEstimateLines]([EstimateVersionId]);
            CREATE INDEX [IX_QuantitySurveyEstimateLines_ProjectBoqVersionLineId] ON [QuantitySurveyEstimateLines]([ProjectBoqVersionLineId]);
            CREATE INDEX [IX_QuantitySurveyEstimateLines_SourceRateId] ON [QuantitySurveyEstimateLines]([SourceRateId]);

            CREATE UNIQUE INDEX [IX_QuantitySurveyEstimateAssumptions_TenantId_EstimateVersionId_Sequence] ON [QuantitySurveyEstimateAssumptions]([TenantId], [EstimateVersionId], [Sequence]);
            CREATE UNIQUE INDEX [IX_QuantitySurveyEstimateAssumptions_TenantId_EstimateVersionId_Code] ON [QuantitySurveyEstimateAssumptions]([TenantId], [EstimateVersionId], [Code]);
            CREATE INDEX [IX_QuantitySurveyEstimateAssumptions_EstimateVersionId] ON [QuantitySurveyEstimateAssumptions]([EstimateVersionId]);

            CREATE UNIQUE INDEX [IX_QuantitySurveyEstimateMarkups_TenantId_EstimateVersionId_Sequence] ON [QuantitySurveyEstimateMarkups]([TenantId], [EstimateVersionId], [Sequence]);
            CREATE UNIQUE INDEX [IX_QuantitySurveyEstimateMarkups_TenantId_EstimateVersionId_Component] ON [QuantitySurveyEstimateMarkups]([TenantId], [EstimateVersionId], [Component]);
            CREATE INDEX [IX_QuantitySurveyEstimateMarkups_EstimateVersionId] ON [QuantitySurveyEstimateMarkups]([EstimateVersionId]);

            CREATE INDEX [IX_QuantitySurveyEstimateRevisions_TenantId_EstimateVersionId_CreatedAt] ON [QuantitySurveyEstimateRevisions]([TenantId], [EstimateVersionId], [CreatedAt]);
            CREATE INDEX [IX_QuantitySurveyEstimateRevisions_TenantId_CorrelationId] ON [QuantitySurveyEstimateRevisions]([TenantId], [CorrelationId]);
            CREATE INDEX [IX_QuantitySurveyEstimateRevisions_EstimateVersionId] ON [QuantitySurveyEstimateRevisions]([EstimateVersionId]);
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsEstimateVersions_TenantAndLineageGuard]
            ON [QuantitySurveyEstimateVersions]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    WHERE NOT EXISTS (SELECT 1 FROM [Projects] p WHERE p.[Id] = i.[ProjectId] AND p.[TenantId] = i.[TenantId] AND p.[IsDeleted] = 0)
                       OR NOT EXISTS (SELECT 1 FROM [ProjectBoqVersions] b WHERE b.[Id] = i.[ProjectBoqVersionId] AND b.[ProjectId] = i.[ProjectId] AND b.[TenantId] = i.[TenantId] AND b.[Status] = 'Approved' AND b.[PublishedAt] IS NOT NULL AND b.[IsDeleted] = 0)
                       OR NOT EXISTS (SELECT 1 FROM [Currencies] c WHERE c.[Id] = i.[CurrencyId] AND c.[TenantId] = i.[TenantId] AND c.[IsActive] = 1 AND c.[IsDeleted] = 0)
                       OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyConfigurationProfiles] p WHERE p.[Id] = i.[ConfigurationProfileId] AND p.[TenantId] = i.[TenantId] AND p.[IsDeleted] = 0)
                       OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyConfigurationDecisions] d WHERE d.[Id] = i.[ConfigurationDecisionId] AND d.[ProfileId] = i.[ConfigurationProfileId] AND d.[TenantId] = i.[TenantId] AND d.[IsDeleted] = 0)
                       OR (i.[CentralDocumentVersionId] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [CentralDocumentVersions] v WHERE v.[Id] = i.[CentralDocumentVersionId] AND v.[DocumentRecordId] = i.[CentralDocumentRecordId] AND v.[TenantId] = i.[TenantId] AND v.[IsDeleted] = 0))
                       OR (i.[SourceEstimateVersionId] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [QuantitySurveyEstimateVersions] s WHERE s.[Id] = i.[SourceEstimateVersionId] AND s.[TenantId] = i.[TenantId] AND s.[ProjectId] = i.[ProjectId] AND s.[EstimateType] = i.[EstimateType] AND s.[VersionNumber] < i.[VersionNumber] AND s.[Status] IN ('Approved','Retired') AND s.[IsDeleted] = 0))
                ) THROW 51000, 'Invalid tenant, approved BoQ, controlled master, DMS, or estimate lineage.', 1;
            END
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsEstimateVersions_ApprovedImmutable]
            ON [QuantitySurveyEstimateVersions]
            AFTER UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id] = d.[Id] WHERE d.[Status] = 'Retired' OR (d.[Status] = 'Approved' AND i.[Id] IS NULL))
                    THROW 51001, 'Approved or retired estimate versions cannot be deleted and retired versions cannot be changed.', 1;

                IF EXISTS (
                    SELECT d.[Id],d.[ProjectId],d.[ProjectBoqVersionId],d.[SourceEstimateVersionId],d.[ClientRequestId],d.[VersionNumber],d.[EstimateType],d.[Name],d.[EstimateDate],d.[CurrencyId],d.[CurrencyCodeSnapshot],d.[DirectCost],d.[MarkupTotal],d.[TotalAmount],d.[ApprovalStatus],d.[WorkflowInstanceId],d.[WorkflowDefinitionId],d.[SubmittedById],d.[SubmittedAt],d.[ApprovedById],d.[ApprovedAt],d.[RejectionReason],d.[ChangeReason],d.[SnapshotHash],d.[LineCount],d.[AssumptionCount],d.[MarkupCount],d.[ConfigurationProfileId],d.[ConfigurationDecisionId],d.[ConfigurationProfileVersion],d.[CentralDocumentRecordId],d.[CentralDocumentVersionId],d.[CreatedAt],d.[CreatedBy],d.[CreatedById],d.[IsDeleted],d.[DeletedAt],d.[DeletedBy],d.[TenantId]
                    FROM deleted d WHERE d.[Status] = 'Approved'
                    EXCEPT
                    SELECT i.[Id],i.[ProjectId],i.[ProjectBoqVersionId],i.[SourceEstimateVersionId],i.[ClientRequestId],i.[VersionNumber],i.[EstimateType],i.[Name],i.[EstimateDate],i.[CurrencyId],i.[CurrencyCodeSnapshot],i.[DirectCost],i.[MarkupTotal],i.[TotalAmount],i.[ApprovalStatus],i.[WorkflowInstanceId],i.[WorkflowDefinitionId],i.[SubmittedById],i.[SubmittedAt],i.[ApprovedById],i.[ApprovedAt],i.[RejectionReason],i.[ChangeReason],i.[SnapshotHash],i.[LineCount],i.[AssumptionCount],i.[MarkupCount],i.[ConfigurationProfileId],i.[ConfigurationDecisionId],i.[ConfigurationProfileVersion],i.[CentralDocumentRecordId],i.[CentralDocumentVersionId],i.[CreatedAt],i.[CreatedBy],i.[CreatedById],i.[IsDeleted],i.[DeletedAt],i.[DeletedBy],i.[TenantId]
                    FROM inserted i WHERE i.[Status] = 'Retired'
                ) THROW 51002, 'An approved estimate is immutable; only the controlled Approved to Retired transition is allowed.', 1;
            END
            """);

        foreach (var (table, trigger) in new[]
        {
            ("QuantitySurveyEstimateLines", "TR_QsEstimateLines_ParentAndImmutableGuard"),
            ("QuantitySurveyEstimateAssumptions", "TR_QsEstimateAssumptions_ParentAndImmutableGuard"),
            ("QuantitySurveyEstimateMarkups", "TR_QsEstimateMarkups_ParentAndImmutableGuard")
        })
        {
            migrationBuilder.Sql($$"""
                CREATE TRIGGER [{{trigger}}]
                ON [{{table}}]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE NOT EXISTS (SELECT 1 FROM [QuantitySurveyEstimateVersions] e WHERE e.[Id] = i.[EstimateVersionId] AND e.[TenantId] = i.[TenantId] AND e.[IsDeleted] = 0)
                    ) THROW 51003, 'Estimate snapshot child rows must belong to the same tenant as their estimate.', 1;
                    IF EXISTS (
                        SELECT 1 FROM (SELECT [EstimateVersionId] FROM inserted UNION SELECT [EstimateVersionId] FROM deleted) x
                        JOIN [QuantitySurveyEstimateVersions] e ON e.[Id] = x.[EstimateVersionId]
                        WHERE e.[Status] IN ('Approved','Retired')
                    ) THROW 51004, 'Approved or retired estimate snapshot rows are immutable.', 1;
                END
                """);
        }

        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsEstimateLines_LineageGuard]
            ON [QuantitySurveyEstimateLines]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    JOIN [QuantitySurveyEstimateVersions] e ON e.[Id] = i.[EstimateVersionId]
                    WHERE NOT EXISTS (SELECT 1 FROM [ProjectBoqVersionLines] l WHERE l.[Id] = i.[ProjectBoqVersionLineId] AND l.[ProjectBoqVersionId] = e.[ProjectBoqVersionId] AND l.[TenantId] = i.[TenantId] AND l.[IsDeleted] = 0)
                       OR (i.[SourceRateId] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [QuantitySurveyRateLibraryRates] r WHERE r.[Id] = i.[SourceRateId] AND r.[TenantId] = i.[TenantId] AND r.[IsDeleted] = 0))
                ) THROW 51005, 'Estimate line BoQ or rate lineage is invalid for the tenant.', 1;
            END
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsEstimateRevisions_Immutable]
            ON [QuantitySurveyEstimateRevisions]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM inserted i WHERE NOT EXISTS (SELECT 1 FROM [QuantitySurveyEstimateVersions] e WHERE e.[Id] = i.[EstimateVersionId] AND e.[TenantId] = i.[TenantId]))
                    THROW 51006, 'Estimate revision history must belong to the same tenant as its estimate.', 1;
                IF EXISTS (SELECT 1 FROM deleted)
                    THROW 51007, 'Estimate revision history is append-only.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS [TR_QsEstimateRevisions_Immutable];
            DROP TRIGGER IF EXISTS [TR_QsEstimateLines_LineageGuard];
            DROP TRIGGER IF EXISTS [TR_QsEstimateMarkups_ParentAndImmutableGuard];
            DROP TRIGGER IF EXISTS [TR_QsEstimateAssumptions_ParentAndImmutableGuard];
            DROP TRIGGER IF EXISTS [TR_QsEstimateLines_ParentAndImmutableGuard];
            DROP TRIGGER IF EXISTS [TR_QsEstimateVersions_ApprovedImmutable];
            DROP TRIGGER IF EXISTS [TR_QsEstimateVersions_TenantAndLineageGuard];
            DROP TABLE [QuantitySurveyEstimateRevisions];
            DROP TABLE [QuantitySurveyEstimateMarkups];
            DROP TABLE [QuantitySurveyEstimateAssumptions];
            DROP TABLE [QuantitySurveyEstimateLines];
            DROP TABLE [QuantitySurveyEstimateVersions];
            """);
    }
}
