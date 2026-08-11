using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260810023000_AddQuantitySurveyMeasurementSheets")]
public sealed class AddQuantitySurveyMeasurementSheets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE [QuantitySurveyMeasurementSheets] (
                [Id] uniqueidentifier NOT NULL,
                [TenantId] uniqueidentifier NOT NULL,
                [ProjectId] uniqueidentifier NOT NULL,
                [ProjectBoqVersionId] uniqueidentifier NOT NULL,
                [ProjectBoqVersionLineId] uniqueidentifier NOT NULL,
                [BoqLineKey] uniqueidentifier NOT NULL,
                [ProjectDrawingId] uniqueidentifier NULL,
                [SourceType] int NOT NULL,
                [SheetReference] nvarchar(40) NOT NULL,
                [ClientRequestId] uniqueidentifier NOT NULL,
                [RequestHash] varchar(64) NOT NULL,
                [LastMutationClientRequestId] uniqueidentifier NULL,
                [LastMutationRequestHash] varchar(64) NULL,
                [Title] nvarchar(200) NOT NULL,
                [MeasurementDate] datetime2 NOT NULL,
                [SiteLocation] nvarchar(300) NULL,
                [DrawingNumberSnapshot] nvarchar(100) NULL,
                [DrawingRevisionSnapshot] nvarchar(30) NULL,
                [DrawingStatusSnapshot] nvarchar(40) NULL,
                [BoqLineNumberSnapshot] nvarchar(50) NULL,
                [BoqItemCodeSnapshot] nvarchar(50) NULL,
                [BoqDescriptionSnapshot] nvarchar(1000) NOT NULL,
                [UnitOfMeasureSnapshot] nvarchar(20) NULL,
                [MeasurementStandardSnapshot] nvarchar(30) NULL,
                [MeasurementCodeSnapshot] nvarchar(50) NULL,
                [MeasurementRuleSnapshot] nvarchar(2000) NULL,
                [BoqQuantitySnapshot] decimal(18,4) NOT NULL,
                [TotalMeasuredQuantity] decimal(18,4) NOT NULL,
                [Status] nvarchar(30) NOT NULL,
                [ConfigurationProfileId] uniqueidentifier NOT NULL,
                [ConfigurationDecisionId] uniqueidentifier NOT NULL,
                [EvidenceMetadataTemplateId] uniqueidentifier NOT NULL,
                [EvidenceMetadataTemplateCodeSnapshot] nvarchar(80) NOT NULL,
                [PolicyHash] varchar(64) NOT NULL,
                [PreparedById] uniqueidentifier NOT NULL,
                [PreparedByName] nvarchar(300) NOT NULL,
                [PreparedAt] datetime2 NOT NULL,
                [RecordedById] uniqueidentifier NULL,
                [RecordedByName] nvarchar(300) NULL,
                [RecordedAt] datetime2 NULL,
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
                CONSTRAINT [PK_QuantitySurveyMeasurementSheets] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_QsMeasurementSheets_Status] CHECK ([Status] IN ('Draft','Recorded')),
                CONSTRAINT [CK_QsMeasurementSheets_Source] CHECK ([SourceType] IN (0,1) AND (([SourceType]=0 AND [ProjectDrawingId] IS NOT NULL) OR ([SourceType]=1 AND [SiteLocation] IS NOT NULL))),
                CONSTRAINT [CK_QsMeasurementSheets_Quantity] CHECK ([BoqQuantitySnapshot] >= 0 AND [TotalMeasuredQuantity] > 0),
                CONSTRAINT [CK_QsMeasurementSheets_Hashes] CHECK (LEN([RequestHash])=64 AND LEN([PolicyHash])=64 AND ([LastMutationRequestHash] IS NULL OR LEN([LastMutationRequestHash])=64)),
                CONSTRAINT [CK_QsMeasurementSheets_Lifecycle] CHECK (([Status]='Draft' AND [RecordedById] IS NULL AND [RecordedAt] IS NULL) OR ([Status]='Recorded' AND [RecordedById] IS NOT NULL AND [RecordedAt] IS NOT NULL)),
                CONSTRAINT [FK_QsMeasurementSheets_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id]),
                CONSTRAINT [FK_QsMeasurementSheets_Project] FOREIGN KEY ([ProjectId]) REFERENCES [Projects]([Id]),
                CONSTRAINT [FK_QsMeasurementSheets_BoqVersion] FOREIGN KEY ([ProjectBoqVersionId]) REFERENCES [ProjectBoqVersions]([Id]),
                CONSTRAINT [FK_QsMeasurementSheets_BoqLine] FOREIGN KEY ([ProjectBoqVersionLineId]) REFERENCES [ProjectBoqVersionLines]([Id]),
                CONSTRAINT [FK_QsMeasurementSheets_Drawing] FOREIGN KEY ([ProjectDrawingId]) REFERENCES [ProjectDrawings]([Id]),
                CONSTRAINT [FK_QsMeasurementSheets_Profile] FOREIGN KEY ([ConfigurationProfileId]) REFERENCES [QuantitySurveyConfigurationProfiles]([Id]),
                CONSTRAINT [FK_QsMeasurementSheets_Decision] FOREIGN KEY ([ConfigurationDecisionId]) REFERENCES [QuantitySurveyConfigurationDecisions]([Id]),
                CONSTRAINT [FK_QsMeasurementSheets_Template] FOREIGN KEY ([EvidenceMetadataTemplateId]) REFERENCES [CentralDocumentMetadataTemplates]([Id]),
                CONSTRAINT [FK_QsMeasurementSheets_PreparedBy] FOREIGN KEY ([PreparedById]) REFERENCES [Users]([Id]),
                CONSTRAINT [FK_QsMeasurementSheets_RecordedBy] FOREIGN KEY ([RecordedById]) REFERENCES [Users]([Id])
            );

            CREATE TABLE [QuantitySurveyMeasurementLines] (
                [Id] uniqueidentifier NOT NULL,
                [TenantId] uniqueidentifier NOT NULL,
                [MeasurementSheetId] uniqueidentifier NOT NULL,
                [ClientLineKey] uniqueidentifier NOT NULL,
                [Sequence] int NOT NULL,
                [Description] nvarchar(500) NOT NULL,
                [FormulaType] int NOT NULL,
                [Timesing] decimal(18,4) NOT NULL,
                [Length] decimal(18,4) NULL,
                [Width] decimal(18,4) NULL,
                [Height] decimal(18,4) NULL,
                [IsDeduction] bit NOT NULL,
                [FormulaSnapshot] nvarchar(200) NOT NULL,
                [CalculatedQuantity] decimal(18,4) NOT NULL,
                [Notes] nvarchar(1000) NULL,
                [CreatedAt] datetime2 NOT NULL,
                [UpdatedAt] datetime2 NULL,
                [CreatedBy] nvarchar(max) NULL,
                [UpdatedBy] nvarchar(max) NULL,
                [CreatedById] uniqueidentifier NULL,
                [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL,
                [DeletedBy] nvarchar(max) NULL,
                CONSTRAINT [PK_QuantitySurveyMeasurementLines] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_QsMeasurementLines_Formula] CHECK ([FormulaType] IN (0,1,2,3) AND [Timesing] > 0 AND (([FormulaType]=0 AND [Length] IS NULL AND [Width] IS NULL AND [Height] IS NULL) OR ([FormulaType]=1 AND [Length]>0 AND [Width] IS NULL AND [Height] IS NULL) OR ([FormulaType]=2 AND [Length]>0 AND [Width]>0 AND [Height] IS NULL) OR ([FormulaType]=3 AND [Length]>0 AND [Width]>0 AND [Height]>0))),
                CONSTRAINT [CK_QsMeasurementLines_Calculation] CHECK ([CalculatedQuantity]<>0 AND (([IsDeduction]=1 AND [CalculatedQuantity]<0) OR ([IsDeduction]=0 AND [CalculatedQuantity]>0))),
                CONSTRAINT [FK_QsMeasurementLines_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id]),
                CONSTRAINT [FK_QsMeasurementLines_Sheet] FOREIGN KEY ([MeasurementSheetId]) REFERENCES [QuantitySurveyMeasurementSheets]([Id])
            );

            CREATE TABLE [QuantitySurveyMeasurementAttachments] (
                [Id] uniqueidentifier NOT NULL,
                [TenantId] uniqueidentifier NOT NULL,
                [MeasurementSheetId] uniqueidentifier NOT NULL,
                [ClientRequestId] uniqueidentifier NOT NULL,
                [RequestHash] varchar(64) NOT NULL,
                [EvidenceType] int NOT NULL,
                [Title] nvarchar(200) NOT NULL,
                [OriginalFileName] nvarchar(260) NOT NULL,
                [ContentType] nvarchar(120) NOT NULL,
                [FileSize] bigint NOT NULL,
                [ChecksumSha256] varchar(64) NOT NULL,
                [FileUploadRecordId] uniqueidentifier NOT NULL,
                [CentralDocumentRecordId] uniqueidentifier NOT NULL,
                [CentralDocumentVersionId] uniqueidentifier NOT NULL,
                [UploadedById] uniqueidentifier NOT NULL,
                [UploadedByName] nvarchar(300) NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                [UpdatedAt] datetime2 NULL,
                [CreatedBy] nvarchar(max) NULL,
                [UpdatedBy] nvarchar(max) NULL,
                [CreatedById] uniqueidentifier NULL,
                [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL,
                [DeletedBy] nvarchar(max) NULL,
                CONSTRAINT [PK_QuantitySurveyMeasurementAttachments] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_QsMeasurementAttachments_Type] CHECK ([EvidenceType] IN (0,1,2,3)),
                CONSTRAINT [CK_QsMeasurementAttachments_File] CHECK ([FileSize]>0 AND LEN([ChecksumSha256])=64 AND LEN([RequestHash])=64),
                CONSTRAINT [FK_QsMeasurementAttachments_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id]),
                CONSTRAINT [FK_QsMeasurementAttachments_Sheet] FOREIGN KEY ([MeasurementSheetId]) REFERENCES [QuantitySurveyMeasurementSheets]([Id]),
                CONSTRAINT [FK_QsMeasurementAttachments_Upload] FOREIGN KEY ([FileUploadRecordId]) REFERENCES [FileUploadRecords]([Id]),
                CONSTRAINT [FK_QsMeasurementAttachments_Document] FOREIGN KEY ([CentralDocumentRecordId]) REFERENCES [CentralDocumentRecords]([Id]),
                CONSTRAINT [FK_QsMeasurementAttachments_Version] FOREIGN KEY ([CentralDocumentVersionId]) REFERENCES [CentralDocumentVersions]([Id]),
                CONSTRAINT [FK_QsMeasurementAttachments_UploadedBy] FOREIGN KEY ([UploadedById]) REFERENCES [Users]([Id])
            );

            CREATE TABLE [QuantitySurveyMeasurementRevisions] (
                [Id] uniqueidentifier NOT NULL,
                [TenantId] uniqueidentifier NOT NULL,
                [MeasurementSheetId] uniqueidentifier NOT NULL,
                [Action] nvarchar(100) NOT NULL,
                [ActorUserId] uniqueidentifier NOT NULL,
                [ActorName] nvarchar(300) NOT NULL,
                [ActorRoles] nvarchar(500) NULL,
                [CorrelationId] nvarchar(100) NOT NULL,
                [Reason] nvarchar(1000) NULL,
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
                CONSTRAINT [PK_QuantitySurveyMeasurementRevisions] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_QsMeasurementRevisions_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id]),
                CONSTRAINT [FK_QsMeasurementRevisions_Sheet] FOREIGN KEY ([MeasurementSheetId]) REFERENCES [QuantitySurveyMeasurementSheets]([Id]),
                CONSTRAINT [FK_QsMeasurementRevisions_Actor] FOREIGN KEY ([ActorUserId]) REFERENCES [Users]([Id])
            );

            CREATE UNIQUE INDEX [UX_QsMeasurementSheets_Tenant_Reference] ON [QuantitySurveyMeasurementSheets]([TenantId],[SheetReference]);
            CREATE UNIQUE INDEX [UX_QsMeasurementSheets_Tenant_CreateRequest] ON [QuantitySurveyMeasurementSheets]([TenantId],[ClientRequestId]);
            CREATE UNIQUE INDEX [UX_QsMeasurementSheets_Tenant_MutationRequest] ON [QuantitySurveyMeasurementSheets]([TenantId],[LastMutationClientRequestId]) WHERE [LastMutationClientRequestId] IS NOT NULL;
            CREATE INDEX [IX_QsMeasurementSheets_Tenant_Project_Status_Date] ON [QuantitySurveyMeasurementSheets]([TenantId],[ProjectId],[Status],[MeasurementDate]);
            CREATE INDEX [IX_QsMeasurementSheets_Tenant_BoqLine_Status] ON [QuantitySurveyMeasurementSheets]([TenantId],[ProjectBoqVersionLineId],[Status]);
            CREATE INDEX [IX_QsMeasurementSheets_BoqVersion] ON [QuantitySurveyMeasurementSheets]([ProjectBoqVersionId]);
            CREATE INDEX [IX_QsMeasurementSheets_Drawing] ON [QuantitySurveyMeasurementSheets]([ProjectDrawingId]);
            CREATE INDEX [IX_QsMeasurementSheets_Profile] ON [QuantitySurveyMeasurementSheets]([ConfigurationProfileId]);
            CREATE INDEX [IX_QsMeasurementSheets_Decision] ON [QuantitySurveyMeasurementSheets]([ConfigurationDecisionId]);
            CREATE INDEX [IX_QsMeasurementSheets_Template] ON [QuantitySurveyMeasurementSheets]([EvidenceMetadataTemplateId]);
            CREATE INDEX [IX_QsMeasurementSheets_PreparedBy] ON [QuantitySurveyMeasurementSheets]([PreparedById]);
            CREATE INDEX [IX_QsMeasurementSheets_RecordedBy] ON [QuantitySurveyMeasurementSheets]([RecordedById]);

            CREATE UNIQUE INDEX [UX_QsMeasurementLines_Tenant_Sheet_Key] ON [QuantitySurveyMeasurementLines]([TenantId],[MeasurementSheetId],[ClientLineKey]);
            CREATE UNIQUE INDEX [UX_QsMeasurementLines_Tenant_Sheet_Sequence] ON [QuantitySurveyMeasurementLines]([TenantId],[MeasurementSheetId],[Sequence]);
            CREATE INDEX [IX_QsMeasurementLines_Sheet] ON [QuantitySurveyMeasurementLines]([MeasurementSheetId]);

            CREATE UNIQUE INDEX [UX_QsMeasurementAttachments_Tenant_Request] ON [QuantitySurveyMeasurementAttachments]([TenantId],[ClientRequestId]);
            CREATE UNIQUE INDEX [UX_QsMeasurementAttachments_Tenant_DmsVersion] ON [QuantitySurveyMeasurementAttachments]([TenantId],[CentralDocumentVersionId]);
            CREATE INDEX [IX_QsMeasurementAttachments_Tenant_Sheet_Type_Date] ON [QuantitySurveyMeasurementAttachments]([TenantId],[MeasurementSheetId],[EvidenceType],[CreatedAt]);
            CREATE INDEX [IX_QsMeasurementAttachments_Upload] ON [QuantitySurveyMeasurementAttachments]([FileUploadRecordId]);
            CREATE INDEX [IX_QsMeasurementAttachments_Document] ON [QuantitySurveyMeasurementAttachments]([CentralDocumentRecordId]);
            CREATE INDEX [IX_QsMeasurementAttachments_UploadedBy] ON [QuantitySurveyMeasurementAttachments]([UploadedById]);

            CREATE INDEX [IX_QsMeasurementRevisions_Tenant_Sheet_Date] ON [QuantitySurveyMeasurementRevisions]([TenantId],[MeasurementSheetId],[CreatedAt]);
            CREATE INDEX [IX_QsMeasurementRevisions_Tenant_Correlation] ON [QuantitySurveyMeasurementRevisions]([TenantId],[CorrelationId]);
            CREATE INDEX [IX_QsMeasurementRevisions_Actor] ON [QuantitySurveyMeasurementRevisions]([ActorUserId]);
            """);

        CreateGuards(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsMeasurementRevisions_Guard];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsMeasurementAttachments_Guard];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsMeasurementLines_Guard];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsMeasurementSheets_Guard];");
        migrationBuilder.Sql("DROP TABLE [QuantitySurveyMeasurementAttachments]; DROP TABLE [QuantitySurveyMeasurementRevisions]; DROP TABLE [QuantitySurveyMeasurementLines]; DROP TABLE [QuantitySurveyMeasurementSheets];");
    }

    private static void CreateGuards(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsMeasurementSheets_Guard]
            ON [QuantitySurveyMeasurementSheets]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id]=d.[Id] WHERE i.[Id] IS NULL)
                    THROW 51082, 'Measurement sheets cannot be deleted; retain their governed history.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.[Id]=i.[Id]
                    WHERE (d.[Id] IS NULL AND (i.[Status]<>'Draft' OR i.[IsDeleted]=1))
                       OR (d.[Id] IS NOT NULL AND NOT (d.[Status]='Draft' AND i.[Status] IN ('Draft','Recorded'))))
                    THROW 51081, 'Invalid measurement-sheet lifecycle transition.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
                    WHERE EXISTS (
                        SELECT d.[TenantId],d.[ProjectId],d.[ProjectBoqVersionId],d.[ProjectBoqVersionLineId],d.[BoqLineKey],d.[SheetReference],d.[ClientRequestId],d.[RequestHash],d.[BoqLineNumberSnapshot],d.[BoqItemCodeSnapshot],d.[BoqDescriptionSnapshot],d.[UnitOfMeasureSnapshot],d.[MeasurementStandardSnapshot],d.[MeasurementCodeSnapshot],d.[MeasurementRuleSnapshot],d.[BoqQuantitySnapshot],d.[ConfigurationProfileId],d.[ConfigurationDecisionId],d.[EvidenceMetadataTemplateId],d.[EvidenceMetadataTemplateCodeSnapshot],d.[PolicyHash],d.[PreparedById],d.[PreparedByName],d.[PreparedAt],d.[CreatedAt],d.[CreatedById],d.[IsDeleted]
                        EXCEPT
                        SELECT i.[TenantId],i.[ProjectId],i.[ProjectBoqVersionId],i.[ProjectBoqVersionLineId],i.[BoqLineKey],i.[SheetReference],i.[ClientRequestId],i.[RequestHash],i.[BoqLineNumberSnapshot],i.[BoqItemCodeSnapshot],i.[BoqDescriptionSnapshot],i.[UnitOfMeasureSnapshot],i.[MeasurementStandardSnapshot],i.[MeasurementCodeSnapshot],i.[MeasurementRuleSnapshot],i.[BoqQuantitySnapshot],i.[ConfigurationProfileId],i.[ConfigurationDecisionId],i.[EvidenceMetadataTemplateId],i.[EvidenceMetadataTemplateCodeSnapshot],i.[PolicyHash],i.[PreparedById],i.[PreparedByName],i.[PreparedAt],i.[CreatedAt],i.[CreatedById],i.[IsDeleted]))
                    THROW 51080, 'Measurement project, approved BoQ, policy and preparation lineage is immutable.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    WHERE NOT EXISTS (SELECT 1 FROM [Projects] p WHERE p.[Id]=i.[ProjectId] AND p.[TenantId]=i.[TenantId] AND p.[IsDeleted]=0)
                       OR NOT EXISTS (SELECT 1 FROM [ProjectBoqVersionLines] l JOIN [ProjectBoqVersions] v ON v.[Id]=l.[ProjectBoqVersionId] AND v.[TenantId]=l.[TenantId] WHERE l.[Id]=i.[ProjectBoqVersionLineId] AND l.[TenantId]=i.[TenantId] AND l.[ProjectId]=i.[ProjectId] AND l.[LineKey]=i.[BoqLineKey] AND l.[ProjectBoqVersionId]=i.[ProjectBoqVersionId] AND l.[ItemType]='Item' AND l.[Description]=i.[BoqDescriptionSnapshot] AND l.[Quantity]=i.[BoqQuantitySnapshot] AND ISNULL(l.[MeasurementCode],'')=ISNULL(i.[MeasurementCodeSnapshot],'') AND ISNULL(l.[MeasurementRule],'')=ISNULL(i.[MeasurementRuleSnapshot],'') AND l.[IsDeleted]=0 AND v.[ProjectId]=i.[ProjectId] AND v.[Status]='Approved' AND v.[PublishedAt] IS NOT NULL AND v.[IsDeleted]=0)
                       OR (i.[SourceType]=0 AND NOT EXISTS (SELECT 1 FROM [ProjectDrawings] d WHERE d.[Id]=i.[ProjectDrawingId] AND d.[TenantId]=i.[TenantId] AND d.[ProjectId]=i.[ProjectId] AND d.[Status] IN ('ApprovedForConstruction','ApprovedAsBuilt') AND d.[Revision] IS NOT NULL AND d.[DrawingNumber]=i.[DrawingNumberSnapshot] AND d.[Revision]=i.[DrawingRevisionSnapshot] AND d.[IsDeleted]=0))
                       OR (i.[ProjectDrawingId] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [ProjectDrawings] d WHERE d.[Id]=i.[ProjectDrawingId] AND d.[TenantId]=i.[TenantId] AND d.[ProjectId]=i.[ProjectId] AND d.[Status] IN ('ApprovedForConstruction','ApprovedAsBuilt') AND d.[Revision] IS NOT NULL AND d.[IsDeleted]=0))
                       OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyConfigurationProfiles] p WHERE p.[Id]=i.[ConfigurationProfileId] AND p.[TenantId]=i.[TenantId] AND p.[LifecycleStatus]=1 AND p.[PublishedAt] IS NOT NULL AND p.[EffectiveFrom]<=i.[MeasurementDate] AND (p.[EffectiveTo] IS NULL OR p.[EffectiveTo]>=i.[MeasurementDate]) AND p.[IsDeleted]=0)
                       OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyConfigurationDecisions] d WHERE d.[Id]=i.[ConfigurationDecisionId] AND d.[TenantId]=i.[TenantId] AND d.[ProfileId]=i.[ConfigurationProfileId] AND d.[DecisionKey]='QS-DEC-007' AND d.[Status]=2 AND d.[ApprovalStatus]=1 AND d.[EvidenceStatus]=2 AND (d.[EffectiveFrom] IS NULL OR d.[EffectiveFrom]<=i.[MeasurementDate]) AND (d.[EffectiveTo] IS NULL OR d.[EffectiveTo]>=i.[MeasurementDate]) AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(d.[ValueJson],'$.metadataTemplateId'))=i.[EvidenceMetadataTemplateId] AND d.[IsDeleted]=0)
                       OR NOT EXISTS (SELECT 1 FROM [CentralDocumentMetadataTemplates] t WHERE t.[Id]=i.[EvidenceMetadataTemplateId] AND t.[TenantId]=i.[TenantId] AND t.[TemplateCode]=i.[EvidenceMetadataTemplateCodeSnapshot] AND t.[IsActive]=1 AND t.[PublishedAt] IS NOT NULL AND t.[IsDeleted]=0)
                       OR (i.[Status]='Recorded' AND (NOT EXISTS (SELECT 1 FROM [QuantitySurveyMeasurementLines] l WHERE l.[TenantId]=i.[TenantId] AND l.[MeasurementSheetId]=i.[Id] AND l.[IsDeleted]=0) OR i.[TotalMeasuredQuantity]<>(SELECT ROUND(SUM(l.[CalculatedQuantity]),4) FROM [QuantitySurveyMeasurementLines] l WHERE l.[TenantId]=i.[TenantId] AND l.[MeasurementSheetId]=i.[Id] AND l.[IsDeleted]=0) OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyMeasurementAttachments] a WHERE a.[TenantId]=i.[TenantId] AND a.[MeasurementSheetId]=i.[Id] AND a.[IsDeleted]=0))))
                    THROW 51080, 'Measurement tenant, approved BoQ, drawing, effective QS-DEC-007, metadata-template or recorded-evidence lineage is invalid.', 1;
            END
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsMeasurementLines_Guard]
            ON [QuantitySurveyMeasurementLines]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    WHERE NOT EXISTS (SELECT 1 FROM [QuantitySurveyMeasurementSheets] s WHERE s.[Id]=i.[MeasurementSheetId] AND s.[TenantId]=i.[TenantId] AND s.[Status]='Draft' AND s.[IsDeleted]=0)
                       OR i.[IsDeleted]=1
                       OR i.[CalculatedQuantity] <> ROUND((CASE WHEN i.[IsDeduction]=1 THEN -1 ELSE 1 END) * i.[Timesing] * (CASE WHEN i.[FormulaType]=0 THEN 1 ELSE i.[Length] END) * (CASE WHEN i.[FormulaType] IN (0,1) THEN 1 ELSE i.[Width] END) * (CASE WHEN i.[FormulaType] IN (0,1,2) THEN 1 ELSE i.[Height] END),4))
                    THROW 51081, 'Measurement rows require a Draft parent and exact server-calculated typed formula.', 1;
                IF EXISTS (
                    SELECT 1 FROM deleted d
                    WHERE NOT EXISTS (SELECT 1 FROM [QuantitySurveyMeasurementSheets] s WHERE s.[Id]=d.[MeasurementSheetId] AND s.[TenantId]=d.[TenantId] AND s.[Status]='Draft' AND s.[IsDeleted]=0))
                    THROW 51082, 'Recorded measurement rows are immutable.', 1;
            END
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsMeasurementAttachments_Guard]
            ON [QuantitySurveyMeasurementAttachments]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted)
                    THROW 51082, 'Measurement evidence is append-only.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    WHERE i.[IsDeleted]=1
                       OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyMeasurementSheets] s WHERE s.[Id]=i.[MeasurementSheetId] AND s.[TenantId]=i.[TenantId] AND s.[Status]='Draft' AND s.[IsDeleted]=0)
                       OR (SELECT COUNT_BIG(*) FROM [QuantitySurveyMeasurementAttachments] a WHERE a.[TenantId]=i.[TenantId] AND a.[MeasurementSheetId]=i.[MeasurementSheetId] AND a.[IsDeleted]=0)>20
                       OR NOT EXISTS (SELECT 1 FROM [FileUploadRecords] f WHERE f.[Id]=i.[FileUploadRecordId] AND f.[TenantId]=i.[TenantId] AND f.[Category]='quantity-survey-measurement-evidence' AND f.[VirusScanStatus]=2 AND f.[FileSize]=i.[FileSize] AND f.[IsDeleted]=0)
                       OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyMeasurementSheets] s JOIN [CentralDocumentRecords] r ON r.[TenantId]=s.[TenantId] JOIN [CentralDocumentVersions] v ON v.[DocumentRecordId]=r.[Id] AND v.[TenantId]=r.[TenantId] WHERE s.[Id]=i.[MeasurementSheetId] AND r.[Id]=i.[CentralDocumentRecordId] AND r.[SourceModule]='QuantitySurvey' AND r.[SourceEntityType]='QuantitySurveyMeasurementAttachment' AND r.[SourceRecordId]=i.[Id] AND r.[MetadataTemplateCode]=s.[EvidenceMetadataTemplateCodeSnapshot] AND r.[LifecycleStatus]='Active' AND r.[IsDeleted]=0 AND v.[Id]=i.[CentralDocumentVersionId] AND v.[FileUploadRecordId]=i.[FileUploadRecordId] AND v.[FileSize]=i.[FileSize] AND v.[Status]='Validated' AND v.[VersionNumber]=r.[CurrentVersion] AND v.[IsDeleted]=0))
                    THROW 51080, 'Measurement evidence tenant, Draft-parent, clean-upload or central-DMS lineage is invalid.', 1;
            END
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsMeasurementRevisions_Guard]
            ON [QuantitySurveyMeasurementRevisions]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted)
                    THROW 51082, 'Measurement revision history is append-only.', 1;
                IF EXISTS (SELECT 1 FROM inserted i WHERE i.[IsDeleted]=1 OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyMeasurementSheets] s WHERE s.[Id]=i.[MeasurementSheetId] AND s.[TenantId]=i.[TenantId] AND s.[IsDeleted]=0))
                    THROW 51082, 'Measurement revision tenant lineage is invalid.', 1;
            END
            """);
    }
}
