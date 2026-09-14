using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260811063100_AddQuantitySurveySubcontractChargeLifecycle")]
public partial class AddQuantitySurveySubcontractChargeLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE [dbo].[QuantitySurveySubcontractChargeNotices]
            (
                [Id] uniqueidentifier NOT NULL,
                [SubcontractId] uniqueidentifier NOT NULL,
                [AppliedValuationId] uniqueidentifier NULL,
                [ClientRequestId] uniqueidentifier NOT NULL,
                [RequestHash] varchar(64) NOT NULL,
                [LastMutationClientRequestId] uniqueidentifier NULL,
                [LastMutationRequestHash] varchar(64) NULL,
                [NoticeNumber] nvarchar(100) NOT NULL,
                [ChargeType] nvarchar(20) NOT NULL,
                [Title] nvarchar(200) NOT NULL,
                [Reason] nvarchar(4000) NOT NULL,
                [NoticeDate] datetime2 NOT NULL,
                [ResponseDueDate] datetime2 NOT NULL,
                [ProposedAmount] decimal(18,2) NOT NULL,
                [ApprovedAmount] decimal(18,2) NULL,
                [Currency] nvarchar(10) NOT NULL,
                [Status] nvarchar(30) NOT NULL,
                [ApprovalStatus] nvarchar(30) NOT NULL,
                [ResponseStatus] nvarchar(30) NOT NULL,
                [ResponseNote] nvarchar(2000) NULL,
                [RespondedByBusinessPartnerId] uniqueidentifier NULL,
                [RespondedAt] datetime2 NULL,
                [ConfigurationProfileId] uniqueidentifier NOT NULL,
                [ContractControlsDecisionId] uniqueidentifier NOT NULL,
                [ApprovalWorkflowDefinitionId] uniqueidentifier NOT NULL,
                [EvidenceMetadataTemplateId] uniqueidentifier NOT NULL,
                [PolicyHash] varchar(64) NOT NULL,
                [WorkflowInstanceId] uniqueidentifier NULL,
                [PreparedById] uniqueidentifier NOT NULL,
                [PreparedAt] datetime2 NOT NULL,
                [IssuedById] uniqueidentifier NULL,
                [IssuedAt] datetime2 NULL,
                [SubmittedById] uniqueidentifier NULL,
                [SubmittedAt] datetime2 NULL,
                [ApprovedById] uniqueidentifier NULL,
                [ApprovedAt] datetime2 NULL,
                [RejectionReason] nvarchar(2000) NULL,
                [AllocatedAt] datetime2 NULL,
                [AppliedAt] datetime2 NULL,
                [CommunicationStatus] nvarchar(30) NOT NULL,
                [CommunicationRequestedAt] datetime2 NULL,
                [CommunicationRequestCount] int NOT NULL,
                [LastCommunicationTopic] nvarchar(150) NULL,
                [CorrelationId] nvarchar(100) NOT NULL,
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
                CONSTRAINT [PK_QuantitySurveySubcontractChargeNotices] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_QsSubcontractCharges_Type] CHECK ([ChargeType] IN ('BackCharge','ContraCharge')),
                CONSTRAINT [CK_QsSubcontractCharges_Status] CHECK ([Status] IN ('Draft','Issued','Responded','PendingApproval','Approved','Rejected','Allocated','Applied') AND [ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')),
                CONSTRAINT [CK_QsSubcontractCharges_Response] CHECK ([ResponseStatus] IN ('Pending','Accepted','Disputed','NoResponse')),
                CONSTRAINT [CK_QsSubcontractCharges_Amounts] CHECK ([ProposedAmount] > 0 AND ([ApprovedAmount] IS NULL OR ([ApprovedAmount] > 0 AND [ApprovedAmount] <= [ProposedAmount]))),
                CONSTRAINT [CK_QsSubcontractCharges_Dates] CHECK ([ResponseDueDate] >= [NoticeDate] AND [ResponseDueDate] <= DATEADD(day, 90, [NoticeDate])),
                CONSTRAINT [CK_QsSubcontractCharges_Hashes] CHECK (LEN([RequestHash]) = 64 AND LEN([PolicyHash]) = 64 AND ([LastMutationRequestHash] IS NULL OR LEN([LastMutationRequestHash]) = 64)),
                CONSTRAINT [CK_QsSubcontractCharges_Lifecycle] CHECK (([Status] = 'Draft' AND [ApprovalStatus] = 'Draft' AND [IssuedAt] IS NULL AND [WorkflowInstanceId] IS NULL) OR ([Status] IN ('Issued','Responded') AND [ApprovalStatus] = 'Draft' AND [IssuedById] IS NOT NULL AND [IssuedAt] IS NOT NULL AND [WorkflowInstanceId] IS NULL) OR ([Status] = 'PendingApproval' AND [ApprovalStatus] = 'Pending' AND [IssuedAt] IS NOT NULL AND [SubmittedById] IS NOT NULL AND [SubmittedAt] IS NOT NULL AND [WorkflowInstanceId] IS NOT NULL) OR ([Status] IN ('Approved','Allocated','Applied') AND [ApprovalStatus] = 'Approved' AND [ApprovedAmount] IS NOT NULL AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [WorkflowInstanceId] IS NOT NULL) OR ([Status] = 'Rejected' AND [ApprovalStatus] = 'Rejected' AND [RejectionReason] IS NOT NULL AND [WorkflowInstanceId] IS NOT NULL)),
                CONSTRAINT [CK_QsSubcontractCharges_Application] CHECK (([Status] NOT IN ('Allocated','Applied') AND [AppliedValuationId] IS NULL AND [AllocatedAt] IS NULL AND [AppliedAt] IS NULL) OR ([Status] = 'Allocated' AND [AppliedValuationId] IS NOT NULL AND [AllocatedAt] IS NOT NULL AND [AppliedAt] IS NULL) OR ([Status] = 'Applied' AND [AppliedValuationId] IS NOT NULL AND [AllocatedAt] IS NOT NULL AND [AppliedAt] IS NOT NULL)),
                CONSTRAINT [CK_QsSubcontractCharges_Communication] CHECK ([CommunicationStatus] IN ('NotRequested','Requested') AND [CommunicationRequestCount] >= 0 AND (([CommunicationRequestCount] = 0 AND [CommunicationRequestedAt] IS NULL) OR ([CommunicationRequestCount] > 0 AND [CommunicationRequestedAt] IS NOT NULL))),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeNotices_QuantitySurveySubcontracts_SubcontractId] FOREIGN KEY ([SubcontractId]) REFERENCES [dbo].[QuantitySurveySubcontracts] ([Id]),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeNotices_QuantitySurveySubcontractValuations_AppliedValuationId] FOREIGN KEY ([AppliedValuationId]) REFERENCES [dbo].[QuantitySurveySubcontractValuations] ([Id]),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeNotices_BusinessPartners_RespondedByBusinessPartnerId] FOREIGN KEY ([RespondedByBusinessPartnerId]) REFERENCES [dbo].[BusinessPartners] ([Id]),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeNotices_QuantitySurveyConfigurationProfiles_ConfigurationProfileId] FOREIGN KEY ([ConfigurationProfileId]) REFERENCES [dbo].[QuantitySurveyConfigurationProfiles] ([Id]),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeNotices_QuantitySurveyConfigurationDecisions_ContractControlsDecisionId] FOREIGN KEY ([ContractControlsDecisionId]) REFERENCES [dbo].[QuantitySurveyConfigurationDecisions] ([Id]),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeNotices_WorkflowDefinitions_ApprovalWorkflowDefinitionId] FOREIGN KEY ([ApprovalWorkflowDefinitionId]) REFERENCES [dbo].[WorkflowDefinitions] ([Id]),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeNotices_CentralDocumentMetadataTemplates_EvidenceMetadataTemplateId] FOREIGN KEY ([EvidenceMetadataTemplateId]) REFERENCES [dbo].[CentralDocumentMetadataTemplates] ([Id]),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeNotices_Users_PreparedById] FOREIGN KEY ([PreparedById]) REFERENCES [dbo].[Users] ([Id]),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeNotices_Users_IssuedById] FOREIGN KEY ([IssuedById]) REFERENCES [dbo].[Users] ([Id]),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeNotices_Users_SubmittedById] FOREIGN KEY ([SubmittedById]) REFERENCES [dbo].[Users] ([Id]),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeNotices_Users_ApprovedById] FOREIGN KEY ([ApprovedById]) REFERENCES [dbo].[Users] ([Id]),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeNotices_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id])
            );

            CREATE UNIQUE INDEX [IX_QuantitySurveySubcontractChargeNotices_TenantId_ClientRequestId] ON [dbo].[QuantitySurveySubcontractChargeNotices] ([TenantId], [ClientRequestId]);
            CREATE UNIQUE INDEX [IX_QuantitySurveySubcontractChargeNotices_TenantId_LastMutationClientRequestId] ON [dbo].[QuantitySurveySubcontractChargeNotices] ([TenantId], [LastMutationClientRequestId]) WHERE [LastMutationClientRequestId] IS NOT NULL;
            CREATE UNIQUE INDEX [IX_QuantitySurveySubcontractChargeNotices_TenantId_NoticeNumber] ON [dbo].[QuantitySurveySubcontractChargeNotices] ([TenantId], [NoticeNumber]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeNotices_TenantId_SubcontractId_Status_NoticeDate] ON [dbo].[QuantitySurveySubcontractChargeNotices] ([TenantId], [SubcontractId], [Status], [NoticeDate]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeNotices_TenantId_AppliedValuationId_Status] ON [dbo].[QuantitySurveySubcontractChargeNotices] ([TenantId], [AppliedValuationId], [Status]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeNotices_SubcontractId] ON [dbo].[QuantitySurveySubcontractChargeNotices] ([SubcontractId]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeNotices_AppliedValuationId] ON [dbo].[QuantitySurveySubcontractChargeNotices] ([AppliedValuationId]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeNotices_RespondedByBusinessPartnerId] ON [dbo].[QuantitySurveySubcontractChargeNotices] ([RespondedByBusinessPartnerId]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeNotices_ConfigurationProfileId] ON [dbo].[QuantitySurveySubcontractChargeNotices] ([ConfigurationProfileId]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeNotices_ContractControlsDecisionId] ON [dbo].[QuantitySurveySubcontractChargeNotices] ([ContractControlsDecisionId]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeNotices_ApprovalWorkflowDefinitionId] ON [dbo].[QuantitySurveySubcontractChargeNotices] ([ApprovalWorkflowDefinitionId]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeNotices_EvidenceMetadataTemplateId] ON [dbo].[QuantitySurveySubcontractChargeNotices] ([EvidenceMetadataTemplateId]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeNotices_PreparedById] ON [dbo].[QuantitySurveySubcontractChargeNotices] ([PreparedById]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeNotices_IssuedById] ON [dbo].[QuantitySurveySubcontractChargeNotices] ([IssuedById]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeNotices_SubmittedById] ON [dbo].[QuantitySurveySubcontractChargeNotices] ([SubmittedById]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeNotices_ApprovedById] ON [dbo].[QuantitySurveySubcontractChargeNotices] ([ApprovedById]);

            CREATE TABLE [dbo].[QuantitySurveySubcontractChargeEvidence]
            (
                [Id] uniqueidentifier NOT NULL,
                [ChargeNoticeId] uniqueidentifier NOT NULL,
                [ClientRequestId] uniqueidentifier NOT NULL,
                [RequestHash] varchar(64) NOT NULL,
                [Title] nvarchar(200) NOT NULL,
                [OriginalFileName] nvarchar(260) NOT NULL,
                [ContentType] nvarchar(150) NOT NULL,
                [FileSize] bigint NOT NULL,
                [ChecksumSha256] varchar(64) NOT NULL,
                [FileUploadRecordId] uniqueidentifier NOT NULL,
                [CentralDocumentRecordId] uniqueidentifier NOT NULL,
                [CentralDocumentVersionId] uniqueidentifier NOT NULL,
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
                CONSTRAINT [PK_QuantitySurveySubcontractChargeEvidence] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_QsSubcontractChargeEvidence_File] CHECK ([FileSize] > 0 AND LEN([ChecksumSha256]) = 64 AND LEN([RequestHash]) = 64),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeEvidence_QuantitySurveySubcontractChargeNotices_ChargeNoticeId] FOREIGN KEY ([ChargeNoticeId]) REFERENCES [dbo].[QuantitySurveySubcontractChargeNotices] ([Id]),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeEvidence_FileUploadRecords_FileUploadRecordId] FOREIGN KEY ([FileUploadRecordId]) REFERENCES [dbo].[FileUploadRecords] ([Id]),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeEvidence_CentralDocumentRecords_CentralDocumentRecordId] FOREIGN KEY ([CentralDocumentRecordId]) REFERENCES [dbo].[CentralDocumentRecords] ([Id]),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeEvidence_CentralDocumentVersions_CentralDocumentVersionId] FOREIGN KEY ([CentralDocumentVersionId]) REFERENCES [dbo].[CentralDocumentVersions] ([Id]),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeEvidence_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id])
            );
            CREATE UNIQUE INDEX [IX_QuantitySurveySubcontractChargeEvidence_TenantId_ClientRequestId] ON [dbo].[QuantitySurveySubcontractChargeEvidence] ([TenantId], [ClientRequestId]);
            CREATE UNIQUE INDEX [IX_QuantitySurveySubcontractChargeEvidence_TenantId_CentralDocumentVersionId] ON [dbo].[QuantitySurveySubcontractChargeEvidence] ([TenantId], [CentralDocumentVersionId]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeEvidence_TenantId_ChargeNoticeId_CreatedAt] ON [dbo].[QuantitySurveySubcontractChargeEvidence] ([TenantId], [ChargeNoticeId], [CreatedAt]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeEvidence_ChargeNoticeId] ON [dbo].[QuantitySurveySubcontractChargeEvidence] ([ChargeNoticeId]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeEvidence_FileUploadRecordId] ON [dbo].[QuantitySurveySubcontractChargeEvidence] ([FileUploadRecordId]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeEvidence_CentralDocumentRecordId] ON [dbo].[QuantitySurveySubcontractChargeEvidence] ([CentralDocumentRecordId]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeEvidence_CentralDocumentVersionId] ON [dbo].[QuantitySurveySubcontractChargeEvidence] ([CentralDocumentVersionId]);

            CREATE TABLE [dbo].[QuantitySurveySubcontractChargeRevisions]
            (
                [Id] uniqueidentifier NOT NULL,
                [ChargeNoticeId] uniqueidentifier NOT NULL,
                [ClientRequestId] uniqueidentifier NOT NULL,
                [RequestHash] varchar(64) NOT NULL,
                [Action] nvarchar(100) NOT NULL,
                [ActorUserId] uniqueidentifier NOT NULL,
                [ActorBusinessPartnerId] uniqueidentifier NULL,
                [ActorName] nvarchar(300) NOT NULL,
                [ActorRoles] nvarchar(500) NULL,
                [CorrelationId] nvarchar(100) NOT NULL,
                [Reason] nvarchar(2000) NULL,
                [BeforeJson] nvarchar(max) NULL,
                [AfterJson] nvarchar(max) NOT NULL,
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
                CONSTRAINT [PK_QuantitySurveySubcontractChargeRevisions] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_QsSubcontractChargeRevisions_Request] CHECK ([ClientRequestId] <> '00000000-0000-0000-0000-000000000000' AND LEN([RequestHash]) = 64),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeRevisions_QuantitySurveySubcontractChargeNotices_ChargeNoticeId] FOREIGN KEY ([ChargeNoticeId]) REFERENCES [dbo].[QuantitySurveySubcontractChargeNotices] ([Id]),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeRevisions_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [dbo].[Users] ([Id]),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeRevisions_BusinessPartners_ActorBusinessPartnerId] FOREIGN KEY ([ActorBusinessPartnerId]) REFERENCES [dbo].[BusinessPartners] ([Id]),
                CONSTRAINT [FK_QuantitySurveySubcontractChargeRevisions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id])
            );
            CREATE UNIQUE INDEX [IX_QuantitySurveySubcontractChargeRevisions_TenantId_ChargeNoticeId_ClientRequestId] ON [dbo].[QuantitySurveySubcontractChargeRevisions] ([TenantId], [ChargeNoticeId], [ClientRequestId]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeRevisions_TenantId_ChargeNoticeId_CreatedAt] ON [dbo].[QuantitySurveySubcontractChargeRevisions] ([TenantId], [ChargeNoticeId], [CreatedAt]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeRevisions_ChargeNoticeId] ON [dbo].[QuantitySurveySubcontractChargeRevisions] ([ChargeNoticeId]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeRevisions_ActorUserId] ON [dbo].[QuantitySurveySubcontractChargeRevisions] ([ActorUserId]);
            CREATE INDEX [IX_QuantitySurveySubcontractChargeRevisions_ActorBusinessPartnerId] ON [dbo].[QuantitySurveySubcontractChargeRevisions] ([ActorBusinessPartnerId]);
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER [dbo].[TR_QS0522_SubcontractCharges_Governance]
            ON [dbo].[QuantitySurveySubcontractChargeNotices]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                    THROW 52041, 'QS subcontract charge notices cannot be physically deleted.', 1;

                IF EXISTS (SELECT 1 FROM inserted WHERE IsDeleted = 1)
                    THROW 52042, 'QS subcontract charge notices cannot be soft-deleted.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN dbo.QuantitySurveySubcontracts s ON s.Id = i.SubcontractId AND s.TenantId = i.TenantId AND s.IsDeleted = 0
                    LEFT JOIN dbo.QuantitySurveyConfigurationProfiles cp ON cp.Id = i.ConfigurationProfileId AND cp.TenantId = i.TenantId AND cp.IsDeleted = 0
                    LEFT JOIN dbo.QuantitySurveyConfigurationDecisions cd ON cd.Id = i.ContractControlsDecisionId AND cd.TenantId = i.TenantId AND cd.IsDeleted = 0
                    LEFT JOIN dbo.WorkflowDefinitions wd ON wd.Id = i.ApprovalWorkflowDefinitionId AND wd.TenantId = i.TenantId AND wd.IsDeleted = 0
                    LEFT JOIN dbo.CentralDocumentMetadataTemplates mt ON mt.Id = i.EvidenceMetadataTemplateId AND mt.TenantId = i.TenantId AND mt.IsDeleted = 0
                    LEFT JOIN dbo.BusinessPartners bp ON bp.Id = i.RespondedByBusinessPartnerId AND bp.TenantId = i.TenantId AND bp.IsDeleted = 0
                    LEFT JOIN dbo.QuantitySurveySubcontractValuations v ON v.Id = i.AppliedValuationId AND v.TenantId = i.TenantId AND v.SubcontractId = i.SubcontractId AND v.IsDeleted = 0
                    WHERE s.Id IS NULL OR cp.Id IS NULL OR cd.Id IS NULL OR wd.Id IS NULL OR mt.Id IS NULL
                       OR (i.RespondedByBusinessPartnerId IS NOT NULL AND (bp.Id IS NULL OR bp.Id <> s.SubcontractorBusinessPartnerId))
                       OR (i.AppliedValuationId IS NOT NULL AND v.Id IS NULL))
                    THROW 52043, 'QS subcontract charge tenant, subcontract, policy, workflow, evidence or valuation lineage is invalid.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id = i.Id
                    WHERE d.Id IS NULL AND i.Status <> 'Draft')
                    THROW 52044, 'A QS subcontract charge must be created as Draft.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE NOT (
                        i.Status = d.Status OR
                        (d.Status = 'Draft' AND i.Status = 'Issued') OR
                        (d.Status = 'Issued' AND i.Status IN ('Responded','PendingApproval')) OR
                        (d.Status = 'Responded' AND i.Status = 'PendingApproval') OR
                        (d.Status = 'PendingApproval' AND i.Status IN ('Approved','Rejected')) OR
                        (d.Status = 'Rejected' AND i.Status = 'Draft') OR
                        (d.Status = 'Approved' AND i.Status = 'Allocated') OR
                        (d.Status = 'Allocated' AND i.Status IN ('Approved','Applied')) OR
                        (d.Status = 'Applied' AND i.Status = 'Applied')))
                    THROW 52045, 'Invalid QS subcontract charge lifecycle transition.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE d.Status <> 'Draft' AND (
                        i.TenantId <> d.TenantId OR i.SubcontractId <> d.SubcontractId OR
                        i.NoticeNumber <> d.NoticeNumber OR i.ChargeType <> d.ChargeType OR
                        i.Title <> d.Title OR i.Reason <> d.Reason OR i.NoticeDate <> d.NoticeDate OR
                        i.ResponseDueDate <> d.ResponseDueDate OR i.ProposedAmount <> d.ProposedAmount OR
                        i.Currency <> d.Currency OR i.ConfigurationProfileId <> d.ConfigurationProfileId OR
                        i.ContractControlsDecisionId <> d.ContractControlsDecisionId OR
                        i.ApprovalWorkflowDefinitionId <> d.ApprovalWorkflowDefinitionId OR
                        i.EvidenceMetadataTemplateId <> d.EvidenceMetadataTemplateId OR i.PolicyHash <> d.PolicyHash))
                    THROW 52046, 'Issued QS subcontract charge commercial and policy lineage is immutable.', 1;
            END
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER [dbo].[TR_QS0522_SubcontractChargeEvidence_AppendOnly]
            ON [dbo].[QuantitySurveySubcontractChargeEvidence]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted)
                    THROW 52047, 'QS subcontract charge DMS evidence is append-only.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    LEFT JOIN dbo.QuantitySurveySubcontractChargeNotices c ON c.Id = i.ChargeNoticeId AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                    LEFT JOIN dbo.FileUploadRecords f ON f.Id = i.FileUploadRecordId AND f.TenantId = i.TenantId AND f.IsDeleted = 0
                    LEFT JOIN dbo.CentralDocumentRecords d ON d.Id = i.CentralDocumentRecordId AND d.TenantId = i.TenantId AND d.IsDeleted = 0
                    LEFT JOIN dbo.CentralDocumentVersions v ON v.Id = i.CentralDocumentVersionId AND v.TenantId = i.TenantId AND v.DocumentRecordId = i.CentralDocumentRecordId AND v.IsDeleted = 0
                    WHERE c.Id IS NULL OR f.Id IS NULL OR d.Id IS NULL OR v.Id IS NULL)
                    THROW 52049, 'QS subcontract charge evidence must use same-tenant central DMS lineage.', 1;
            END
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER [dbo].[TR_QS0522_SubcontractChargeRevisions_AppendOnly]
            ON [dbo].[QuantitySurveySubcontractChargeRevisions]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted)
                    THROW 52048, 'QS subcontract charge revision history is append-only.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    LEFT JOIN dbo.QuantitySurveySubcontractChargeNotices c ON c.Id = i.ChargeNoticeId AND c.TenantId = i.TenantId
                    LEFT JOIN dbo.BusinessPartners bp ON bp.Id = i.ActorBusinessPartnerId AND bp.TenantId = i.TenantId AND bp.IsDeleted = 0
                    WHERE c.Id IS NULL OR (i.ActorBusinessPartnerId IS NOT NULL AND bp.Id IS NULL))
                    THROW 52050, 'QS subcontract charge revision tenant or actor lineage is invalid.', 1;
            END
            """);

        ApplyValuationTrigger(migrationBuilder, allowApprovedCharges: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ApplyValuationTrigger(migrationBuilder, allowApprovedCharges: false);
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0522_SubcontractChargeRevisions_AppendOnly];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0522_SubcontractChargeEvidence_AppendOnly];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0522_SubcontractCharges_Governance];");
        migrationBuilder.Sql("DROP TABLE [dbo].[QuantitySurveySubcontractChargeRevisions]; DROP TABLE [dbo].[QuantitySurveySubcontractChargeEvidence]; DROP TABLE [dbo].[QuantitySurveySubcontractChargeNotices];");
    }

    private static void ApplyValuationTrigger(MigrationBuilder migrationBuilder, bool allowApprovedCharges)
    {
        var chargeValidation = allowApprovedCharges
            ? """
                OR (i.Status IN ('Draft','Submitted') AND (i.ApprovedBackChargeAmount <> 0 OR i.ApprovedContraChargeAmount <> 0))
                OR (i.Status IN ('PendingApproval','Approved','Paid') AND
                    (ISNULL(ch.BackChargeAmount, 0) <> i.ApprovedBackChargeAmount
                     OR ISNULL(ch.ContraChargeAmount, 0) <> i.ApprovedContraChargeAmount))
              """
            : """
                OR i.ApprovedBackChargeAmount <> 0 OR i.ApprovedContraChargeAmount <> 0
              """;
        var chargeJoin = allowApprovedCharges
            ? """
                OUTER APPLY (
                    SELECT
                        SUM(CASE WHEN c.ChargeType = 'BackCharge' THEN c.ApprovedAmount ELSE 0 END) AS BackChargeAmount,
                        SUM(CASE WHEN c.ChargeType = 'ContraCharge' THEN c.ApprovedAmount ELSE 0 END) AS ContraChargeAmount
                    FROM dbo.QuantitySurveySubcontractChargeNotices c
                    WHERE c.TenantId = i.TenantId AND c.SubcontractId = i.SubcontractId
                      AND c.AppliedValuationId = i.Id AND c.Status IN ('Allocated','Applied') AND c.IsDeleted = 0
                ) ch
              """
            : string.Empty;

        migrationBuilder.Sql($$"""
            CREATE OR ALTER TRIGGER [dbo].[TR_QS0521_SubcontractValuations_Governance]
            ON [dbo].[QuantitySurveySubcontractValuations]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                    THROW 52021, 'QS subcontract valuation records cannot be physically deleted.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    LEFT JOIN dbo.QuantitySurveySubcontracts s ON s.Id = i.SubcontractId AND s.TenantId = i.TenantId AND s.IsDeleted = 0
                    LEFT JOIN dbo.QuantitySurveyConfigurationProfiles cp ON cp.Id = i.ConfigurationProfileId AND cp.TenantId = i.TenantId AND cp.IsDeleted = 0
                    LEFT JOIN dbo.QuantitySurveyConfigurationDecisions cd ON cd.Id = i.ValuationDecisionId AND cd.TenantId = i.TenantId AND cd.IsDeleted = 0
                    LEFT JOIN dbo.WorkflowDefinitions wd ON wd.Id = i.ApprovalWorkflowDefinitionId AND wd.TenantId = i.TenantId AND wd.IsDeleted = 0
                    LEFT JOIN dbo.CentralDocumentMetadataTemplates mt ON mt.Id = i.EvidenceMetadataTemplateId AND mt.TenantId = i.TenantId AND mt.IsDeleted = 0
                    {{chargeJoin}}
                    WHERE i.IsDeleted = 0 AND (s.Id IS NULL OR cp.Id IS NULL OR cd.Id IS NULL OR wd.Id IS NULL OR mt.Id IS NULL
                        OR (i.SubmittedBusinessPartnerId IS NOT NULL AND i.SubmittedBusinessPartnerId <> s.SubcontractorBusinessPartnerId)
                        {{chargeValidation}}))
                    THROW 52022, 'QS subcontract valuation, policy, workflow, evidence or approved-charge lineage is invalid.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE d.Status IN ('Approved','Paid') AND (
                        i.SubcontractId <> d.SubcontractId OR i.ClaimedToDateAmount <> d.ClaimedToDateAmount
                        OR ISNULL(i.AssessedToDateAmount, -1) <> ISNULL(d.AssessedToDateAmount, -1)
                        OR i.PreviouslyCertifiedAmount <> d.PreviouslyCertifiedAmount OR i.CurrentCertifiedAmount <> d.CurrentCertifiedAmount
                        OR i.RetentionHeldAmount <> d.RetentionHeldAmount OR i.RetentionReleasedAmount <> d.RetentionReleasedAmount
                        OR i.ApprovedBackChargeAmount <> d.ApprovedBackChargeAmount OR i.ApprovedContraChargeAmount <> d.ApprovedContraChargeAmount
                        OR i.TaxAmount <> d.TaxAmount OR i.NetCertifiedAmount <> d.NetCertifiedAmount OR i.IsFinal <> d.IsFinal
                        OR i.ConfigurationProfileId <> d.ConfigurationProfileId OR i.ValuationDecisionId <> d.ValuationDecisionId
                        OR i.ApprovalWorkflowDefinitionId <> d.ApprovalWorkflowDefinitionId OR i.EvidenceMetadataTemplateId <> d.EvidenceMetadataTemplateId
                        OR i.PolicyHash <> d.PolicyHash OR i.PaymentCertificateId <> d.PaymentCertificateId
                        OR (d.Status = 'Paid' AND i.Status <> d.Status)
                        OR (d.Status = 'Approved' AND i.Status NOT IN ('Approved','Paid'))))
                    THROW 52023, 'Approved or paid QS subcontract valuation financial and policy lineage is immutable.', 1;
            END
            """);
    }
}
