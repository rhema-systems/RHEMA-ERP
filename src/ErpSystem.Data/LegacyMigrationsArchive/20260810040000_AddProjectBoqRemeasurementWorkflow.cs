using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260810040000_AddProjectBoqRemeasurementWorkflow")]
public sealed class AddProjectBoqRemeasurementWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE [ProjectBoqRemeasurementRevisions] (
                [Id] uniqueidentifier NOT NULL, [TenantId] uniqueidentifier NOT NULL, [ProjectId] uniqueidentifier NOT NULL,
                [ProjectBoqVersionId] uniqueidentifier NOT NULL, [SourceApprovedBoqVersionId] uniqueidentifier NOT NULL,
                [ClientRequestId] uniqueidentifier NOT NULL, [RequestHash] varchar(64) NOT NULL, [MeasurementSetHash] varchar(64) NOT NULL,
                [SelectedMeasurementCount] int NOT NULL, [ChangedLineCount] int NOT NULL,
                [TotalAbsoluteQuantityDelta] decimal(18,4) NOT NULL, [IsFinalized] bit NOT NULL,
                [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NULL, [CreatedBy] nvarchar(max) NULL, [UpdatedBy] nvarchar(max) NULL,
                [CreatedById] uniqueidentifier NULL, [LastModifiedById] uniqueidentifier NULL, [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL, [DeletedBy] nvarchar(max) NULL,
                CONSTRAINT [PK_ProjectBoqRemeasurementRevisions] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_ProjectBoqRemeasurementRevisions_Counts] CHECK ([SelectedMeasurementCount]>0 AND [ChangedLineCount]>0),
                CONSTRAINT [CK_ProjectBoqRemeasurementRevisions_Delta] CHECK ([TotalAbsoluteQuantityDelta]>0),
                CONSTRAINT [CK_ProjectBoqRemeasurementRevisions_Hashes] CHECK (LEN([RequestHash])=64 AND LEN([MeasurementSetHash])=64),
                CONSTRAINT [FK_ProjectBoqRemeasurementRevisions_Tenants] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id]),
                CONSTRAINT [FK_ProjectBoqRemeasurementRevisions_Projects] FOREIGN KEY ([ProjectId]) REFERENCES [Projects]([Id]),
                CONSTRAINT [FK_ProjectBoqRemeasurementRevisions_Version] FOREIGN KEY ([ProjectBoqVersionId]) REFERENCES [ProjectBoqVersions]([Id]),
                CONSTRAINT [FK_ProjectBoqRemeasurementRevisions_SourceVersion] FOREIGN KEY ([SourceApprovedBoqVersionId]) REFERENCES [ProjectBoqVersions]([Id])
            );

            CREATE TABLE [ProjectBoqRemeasurementLines] (
                [Id] uniqueidentifier NOT NULL, [TenantId] uniqueidentifier NOT NULL, [ProjectId] uniqueidentifier NOT NULL,
                [RemeasurementRevisionId] uniqueidentifier NOT NULL, [ProjectBoqVersionId] uniqueidentifier NOT NULL,
                [ProjectBoqVersionLineId] uniqueidentifier NOT NULL, [SourceApprovedBoqVersionLineId] uniqueidentifier NOT NULL,
                [BoqLineKey] uniqueidentifier NOT NULL, [PreviousQuantity] decimal(18,4) NOT NULL,
                [RevisedQuantity] decimal(18,4) NOT NULL, [QuantityDelta] decimal(18,4) NOT NULL,
                [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NULL, [CreatedBy] nvarchar(max) NULL, [UpdatedBy] nvarchar(max) NULL,
                [CreatedById] uniqueidentifier NULL, [LastModifiedById] uniqueidentifier NULL, [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL, [DeletedBy] nvarchar(max) NULL,
                CONSTRAINT [PK_ProjectBoqRemeasurementLines] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_ProjectBoqRemeasurementLines_Quantities] CHECK ([PreviousQuantity]>=0 AND [RevisedQuantity]>=0 AND [QuantityDelta]=[RevisedQuantity]-[PreviousQuantity] AND [QuantityDelta]<>0),
                CONSTRAINT [FK_ProjectBoqRemeasurementLines_Tenants] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id]),
                CONSTRAINT [FK_ProjectBoqRemeasurementLines_Projects] FOREIGN KEY ([ProjectId]) REFERENCES [Projects]([Id]),
                CONSTRAINT [FK_ProjectBoqRemeasurementLines_Revision] FOREIGN KEY ([RemeasurementRevisionId]) REFERENCES [ProjectBoqRemeasurementRevisions]([Id]),
                CONSTRAINT [FK_ProjectBoqRemeasurementLines_Version] FOREIGN KEY ([ProjectBoqVersionId]) REFERENCES [ProjectBoqVersions]([Id]),
                CONSTRAINT [FK_ProjectBoqRemeasurementLines_VersionLine] FOREIGN KEY ([ProjectBoqVersionLineId]) REFERENCES [ProjectBoqVersionLines]([Id]),
                CONSTRAINT [FK_ProjectBoqRemeasurementLines_SourceLine] FOREIGN KEY ([SourceApprovedBoqVersionLineId]) REFERENCES [ProjectBoqVersionLines]([Id])
            );

            CREATE TABLE [ProjectBoqRemeasurementSources] (
                [Id] uniqueidentifier NOT NULL, [TenantId] uniqueidentifier NOT NULL, [ProjectId] uniqueidentifier NOT NULL,
                [RemeasurementRevisionId] uniqueidentifier NOT NULL, [RemeasurementLineId] uniqueidentifier NOT NULL,
                [MeasurementSheetId] uniqueidentifier NOT NULL, [MeasurementReferenceSnapshot] nvarchar(50) NOT NULL,
                [MeasuredQuantitySnapshot] decimal(18,4) NOT NULL, [RecordedAtSnapshot] datetime2 NOT NULL,
                [MeasurementRequestHashSnapshot] varchar(64) NOT NULL,
                [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NULL, [CreatedBy] nvarchar(max) NULL, [UpdatedBy] nvarchar(max) NULL,
                [CreatedById] uniqueidentifier NULL, [LastModifiedById] uniqueidentifier NULL, [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL, [DeletedBy] nvarchar(max) NULL,
                CONSTRAINT [PK_ProjectBoqRemeasurementSources] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_ProjectBoqRemeasurementSources_Quantity] CHECK ([MeasuredQuantitySnapshot]>0),
                CONSTRAINT [CK_ProjectBoqRemeasurementSources_Hash] CHECK (LEN([MeasurementRequestHashSnapshot])=64),
                CONSTRAINT [FK_ProjectBoqRemeasurementSources_Tenants] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id]),
                CONSTRAINT [FK_ProjectBoqRemeasurementSources_Projects] FOREIGN KEY ([ProjectId]) REFERENCES [Projects]([Id]),
                CONSTRAINT [FK_ProjectBoqRemeasurementSources_Revision] FOREIGN KEY ([RemeasurementRevisionId]) REFERENCES [ProjectBoqRemeasurementRevisions]([Id]),
                CONSTRAINT [FK_ProjectBoqRemeasurementSources_Line] FOREIGN KEY ([RemeasurementLineId]) REFERENCES [ProjectBoqRemeasurementLines]([Id]),
                CONSTRAINT [FK_ProjectBoqRemeasurementSources_Measurement] FOREIGN KEY ([MeasurementSheetId]) REFERENCES [QuantitySurveyMeasurementSheets]([Id])
            );

            CREATE UNIQUE INDEX [UX_ProjectBoqRemeasurementRevisions_Tenant_Version] ON [ProjectBoqRemeasurementRevisions]([TenantId],[ProjectBoqVersionId]);
            CREATE UNIQUE INDEX [UX_ProjectBoqRemeasurementRevisions_Tenant_Request] ON [ProjectBoqRemeasurementRevisions]([TenantId],[ClientRequestId]);
            CREATE INDEX [IX_ProjectBoqRemeasurementRevisions_Tenant_Project_Source] ON [ProjectBoqRemeasurementRevisions]([TenantId],[ProjectId],[SourceApprovedBoqVersionId]);
            CREATE INDEX [IX_ProjectBoqRemeasurementRevisions_Project] ON [ProjectBoqRemeasurementRevisions]([ProjectId]);
            CREATE INDEX [IX_ProjectBoqRemeasurementRevisions_SourceVersion] ON [ProjectBoqRemeasurementRevisions]([SourceApprovedBoqVersionId]);
            CREATE UNIQUE INDEX [UX_ProjectBoqRemeasurementLines_Tenant_Revision_LineKey] ON [ProjectBoqRemeasurementLines]([TenantId],[RemeasurementRevisionId],[BoqLineKey]);
            CREATE UNIQUE INDEX [UX_ProjectBoqRemeasurementLines_Tenant_VersionLine] ON [ProjectBoqRemeasurementLines]([TenantId],[ProjectBoqVersionLineId]);
            CREATE INDEX [IX_ProjectBoqRemeasurementLines_Project] ON [ProjectBoqRemeasurementLines]([ProjectId]);
            CREATE INDEX [IX_ProjectBoqRemeasurementLines_Version] ON [ProjectBoqRemeasurementLines]([ProjectBoqVersionId]);
            CREATE INDEX [IX_ProjectBoqRemeasurementLines_SourceLine] ON [ProjectBoqRemeasurementLines]([SourceApprovedBoqVersionLineId]);
            CREATE UNIQUE INDEX [UX_ProjectBoqRemeasurementSources_Tenant_Measurement] ON [ProjectBoqRemeasurementSources]([TenantId],[MeasurementSheetId]);
            CREATE UNIQUE INDEX [UX_ProjectBoqRemeasurementSources_Tenant_Line_Measurement] ON [ProjectBoqRemeasurementSources]([TenantId],[RemeasurementLineId],[MeasurementSheetId]);
            CREATE INDEX [IX_ProjectBoqRemeasurementSources_Project] ON [ProjectBoqRemeasurementSources]([ProjectId]);
            CREATE INDEX [IX_ProjectBoqRemeasurementSources_Revision] ON [ProjectBoqRemeasurementSources]([RemeasurementRevisionId]);
            CREATE INDEX [IX_ProjectBoqRemeasurementSources_Line] ON [ProjectBoqRemeasurementSources]([RemeasurementLineId]);
            """);

        CreateGuards(migrationBuilder);
    }

    private static void CreateGuards(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_ProjectBoqRemeasurementRevisions_Guard] ON [ProjectBoqRemeasurementRevisions]
            AFTER INSERT, UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted d WHERE NOT EXISTS (SELECT 1 FROM inserted i WHERE i.[Id]=d.[Id]))
                THROW 51090, 'Remeasurement revision lineage is append-only.', 1;
              IF EXISTS (
                SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
                WHERE NOT (d.[IsFinalized]=0 AND i.[IsFinalized]=1)
                   OR EXISTS (SELECT i.[TenantId],i.[ProjectId],i.[ProjectBoqVersionId],i.[SourceApprovedBoqVersionId],i.[ClientRequestId],i.[RequestHash],i.[MeasurementSetHash],i.[SelectedMeasurementCount],i.[ChangedLineCount],i.[TotalAbsoluteQuantityDelta],i.[CreatedAt],i.[CreatedById],i.[IsDeleted]
                              EXCEPT SELECT d.[TenantId],d.[ProjectId],d.[ProjectBoqVersionId],d.[SourceApprovedBoqVersionId],d.[ClientRequestId],d.[RequestHash],d.[MeasurementSetHash],d.[SelectedMeasurementCount],d.[ChangedLineCount],d.[TotalAbsoluteQuantityDelta],d.[CreatedAt],d.[CreatedById],d.[IsDeleted]))
                THROW 51090, 'Only atomic remeasurement-lineage finalization is permitted.', 1;
              IF EXISTS (
                SELECT 1 FROM inserted i
                WHERE NOT EXISTS (SELECT 1 FROM [ProjectBoqVersions] v WHERE v.[Id]=i.[ProjectBoqVersionId] AND v.[TenantId]=i.[TenantId] AND v.[ProjectId]=i.[ProjectId] AND v.[VersionType]=4 AND v.[SourceVersionId]=i.[SourceApprovedBoqVersionId] AND v.[Status]='Draft' AND v.[IsDeleted]=0)
                   OR NOT EXISTS (SELECT 1 FROM [ProjectBoqVersions] s WHERE s.[Id]=i.[SourceApprovedBoqVersionId] AND s.[TenantId]=i.[TenantId] AND s.[ProjectId]=i.[ProjectId] AND s.[VersionType]=2 AND s.[Status]='Approved' AND s.[PublishedAt] IS NOT NULL AND s.[IsDeleted]=0))
                THROW 51090, 'Remeasurement must derive from the current approved BoQ through a Draft Remeasurement candidate.', 1;
              IF EXISTS (
                SELECT 1 FROM inserted i WHERE i.[IsFinalized]=1 AND (
                  i.[ChangedLineCount]<>(SELECT COUNT(*) FROM [ProjectBoqRemeasurementLines] l WHERE l.[RemeasurementRevisionId]=i.[Id] AND l.[TenantId]=i.[TenantId] AND l.[IsDeleted]=0)
                  OR i.[SelectedMeasurementCount]<>(SELECT COUNT(*) FROM [ProjectBoqRemeasurementSources] s WHERE s.[RemeasurementRevisionId]=i.[Id] AND s.[TenantId]=i.[TenantId] AND s.[IsDeleted]=0)
                  OR i.[TotalAbsoluteQuantityDelta]<>(SELECT SUM(ABS(l.[QuantityDelta])) FROM [ProjectBoqRemeasurementLines] l WHERE l.[RemeasurementRevisionId]=i.[Id] AND l.[TenantId]=i.[TenantId] AND l.[IsDeleted]=0)
                  OR EXISTS (SELECT 1 FROM [ProjectBoqRemeasurementLines] l WHERE l.[RemeasurementRevisionId]=i.[Id] AND l.[TenantId]=i.[TenantId] AND l.[IsDeleted]=0 AND l.[RevisedQuantity]<>(SELECT SUM(s.[MeasuredQuantitySnapshot]) FROM [ProjectBoqRemeasurementSources] s WHERE s.[RemeasurementLineId]=l.[Id] AND s.[TenantId]=i.[TenantId] AND s.[IsDeleted]=0))))
                THROW 51090, 'Finalized remeasurement counts, deltas and measured quantities must reconcile exactly.', 1;
            END
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_ProjectBoqRemeasurementLines_Guard] ON [ProjectBoqRemeasurementLines]
            AFTER INSERT, UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted) THROW 51091, 'Remeasurement quantity lineage is append-only.', 1;
              IF EXISTS (
                SELECT 1 FROM inserted i
                WHERE NOT EXISTS (SELECT 1 FROM [ProjectBoqRemeasurementRevisions] r WHERE r.[Id]=i.[RemeasurementRevisionId] AND r.[TenantId]=i.[TenantId] AND r.[ProjectId]=i.[ProjectId] AND r.[ProjectBoqVersionId]=i.[ProjectBoqVersionId] AND r.[IsFinalized]=0 AND r.[IsDeleted]=0)
                   OR NOT EXISTS (SELECT 1 FROM [ProjectBoqVersionLines] c JOIN [ProjectBoqVersions] v ON v.[Id]=c.[ProjectBoqVersionId] WHERE c.[Id]=i.[ProjectBoqVersionLineId] AND c.[TenantId]=i.[TenantId] AND c.[ProjectId]=i.[ProjectId] AND c.[ProjectBoqVersionId]=i.[ProjectBoqVersionId] AND c.[LineKey]=i.[BoqLineKey] AND c.[Quantity]=i.[RevisedQuantity] AND c.[IsDeleted]=0 AND v.[VersionType]=4 AND v.[Status]='Draft' AND v.[IsDeleted]=0)
                   OR NOT EXISTS (SELECT 1 FROM [ProjectBoqVersionLines] b JOIN [ProjectBoqRemeasurementRevisions] r ON r.[Id]=i.[RemeasurementRevisionId] WHERE b.[Id]=i.[SourceApprovedBoqVersionLineId] AND b.[TenantId]=i.[TenantId] AND b.[ProjectId]=i.[ProjectId] AND b.[ProjectBoqVersionId]=r.[SourceApprovedBoqVersionId] AND b.[LineKey]=i.[BoqLineKey] AND b.[Quantity]=i.[PreviousQuantity] AND b.[IsDeleted]=0))
                THROW 51091, 'Remeasurement previous/current quantities must match the approved and candidate BoQ line lineage.', 1;
            END
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_ProjectBoqRemeasurementSources_Guard] ON [ProjectBoqRemeasurementSources]
            AFTER INSERT, UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted) THROW 51092, 'Remeasurement measurement-source lineage is append-only.', 1;
              IF EXISTS (
                SELECT 1 FROM inserted i
                WHERE NOT EXISTS (SELECT 1 FROM [ProjectBoqRemeasurementRevisions] r WHERE r.[Id]=i.[RemeasurementRevisionId] AND r.[TenantId]=i.[TenantId] AND r.[ProjectId]=i.[ProjectId] AND r.[IsFinalized]=0 AND r.[IsDeleted]=0)
                   OR NOT EXISTS (SELECT 1 FROM [ProjectBoqRemeasurementLines] l WHERE l.[Id]=i.[RemeasurementLineId] AND l.[TenantId]=i.[TenantId] AND l.[ProjectId]=i.[ProjectId] AND l.[RemeasurementRevisionId]=i.[RemeasurementRevisionId] AND l.[IsDeleted]=0)
                   OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyMeasurementSheets] m JOIN [ProjectBoqRemeasurementLines] l ON l.[Id]=i.[RemeasurementLineId] WHERE m.[Id]=i.[MeasurementSheetId] AND m.[TenantId]=i.[TenantId] AND m.[ProjectId]=i.[ProjectId] AND m.[ProjectBoqVersionLineId]=l.[SourceApprovedBoqVersionLineId] AND m.[Status]='Recorded' AND m.[SheetReference]=i.[MeasurementReferenceSnapshot] AND m.[TotalMeasuredQuantity]=i.[MeasuredQuantitySnapshot] AND m.[RecordedAt]=i.[RecordedAtSnapshot] AND m.[RequestHash]=i.[MeasurementRequestHashSnapshot] AND m.[IsDeleted]=0))
                THROW 51092, 'Only immutable Recorded measurement sheets may source a remeasurement revision.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_ProjectBoqRemeasurementSources_Guard];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_ProjectBoqRemeasurementLines_Guard];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_ProjectBoqRemeasurementRevisions_Guard];");
        migrationBuilder.Sql("DROP TABLE [ProjectBoqRemeasurementSources]; DROP TABLE [ProjectBoqRemeasurementLines]; DROP TABLE [ProjectBoqRemeasurementRevisions];");
    }
}
