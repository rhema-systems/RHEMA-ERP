using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260810041753_AddQuantitySurveyJointMeasurements")]
public sealed class AddQuantitySurveyJointMeasurements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE [QuantitySurveyJointMeasurementRequests] (
                [Id] uniqueidentifier NOT NULL, [TenantId] uniqueidentifier NOT NULL,
                [ProjectId] uniqueidentifier NOT NULL, [ProjectBoqVersionId] uniqueidentifier NOT NULL,
                [ProjectBoqVersionLineId] uniqueidentifier NOT NULL, [BoqLineKey] uniqueidentifier NOT NULL,
                [ContractorBusinessPartnerId] uniqueidentifier NOT NULL, [ConsultantBusinessPartnerId] uniqueidentifier NULL,
                [ClientRequestId] uniqueidentifier NOT NULL, [RequestHash] varchar(64) NOT NULL,
                [LastMutationClientRequestId] uniqueidentifier NULL, [LastMutationRequestHash] nvarchar(64) NULL,
                [RequestNumber] nvarchar(50) NOT NULL, [Title] nvarchar(200) NOT NULL, [Reason] nvarchar(2000) NOT NULL,
                [PreviousQuantity] decimal(18,4) NOT NULL, [ContractorProposedQuantity] decimal(18,4) NULL,
                [RequestedSiteLocation] nvarchar(300) NULL, [PreferredStartAt] datetime2 NULL, [PreferredEndAt] datetime2 NULL,
                [Status] nvarchar(30) NOT NULL, [ApprovalStatus] nvarchar(30) NOT NULL,
                [ConfigurationProfileId] uniqueidentifier NOT NULL, [MeasurementDecisionId] uniqueidentifier NOT NULL,
                [ExternalSubmissionDecisionId] uniqueidentifier NOT NULL, [ApprovalWorkflowDefinitionId] uniqueidentifier NOT NULL,
                [EvidenceMetadataTemplateId] uniqueidentifier NOT NULL, [EvidenceMetadataTemplateCodeSnapshot] nvarchar(80) NOT NULL,
                [PolicyHash] varchar(64) NOT NULL, [JointAttendanceRoleIdsJson] nvarchar(2000) NOT NULL,
                [ConsultantRoleIdsJson] nvarchar(2000) NOT NULL, [ContractorSignatureRequired] bit NOT NULL,
                [ConsultantSignatureRequired] bit NOT NULL, [PortalIdentityRequired] bit NOT NULL,
                [EvidenceRequired] bit NOT NULL, [ExternalSignatureRequired] bit NOT NULL,
                [RequestedByUserId] uniqueidentifier NOT NULL, [RequestedByName] nvarchar(300) NOT NULL, [RequestedAt] datetime2 NOT NULL,
                [SubmittedByUserId] uniqueidentifier NULL, [SubmittedAt] datetime2 NULL,
                [ScheduledStartAt] datetime2 NULL, [ScheduledEndAt] datetime2 NULL, [ScheduledSiteLocation] nvarchar(300) NULL,
                [ScheduledByUserId] uniqueidentifier NULL, [ScheduledAt] datetime2 NULL,
                [MeasurementSheetId] uniqueidentifier NULL, [MeasurementLinkedAt] datetime2 NULL,
                [WorkflowInstanceId] uniqueidentifier NULL, [ReviewedById] uniqueidentifier NULL, [ReviewedAt] datetime2 NULL,
                [ApprovedById] uniqueidentifier NULL, [ApprovedAt] datetime2 NULL, [RejectionReason] nvarchar(2000) NULL,
                [RemeasurementVersionId] uniqueidentifier NULL, [RemeasurementClientRequestId] uniqueidentifier NOT NULL, [AppliedAt] datetime2 NULL,
                [AuditAction] nvarchar(100) NOT NULL, [CorrelationId] nvarchar(100) NOT NULL, [RowVersion] rowversion NOT NULL,
                [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NULL, [CreatedBy] nvarchar(max) NULL,
                [UpdatedBy] nvarchar(max) NULL, [CreatedById] uniqueidentifier NULL, [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL, [DeletedAt] datetime2 NULL, [DeletedBy] nvarchar(max) NULL,
                CONSTRAINT [PK_QuantitySurveyJointMeasurementRequests] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_QsJointMeasurement_Status] CHECK ([Status] IN ('Draft','Submitted','Scheduled','AwaitingAttendance','AwaitingEndorsements','ReadyForReview','PendingApproval','ApprovedPendingBoqRevision','BoqWorkflowPending','Applied','Rejected','Cancelled')),
                CONSTRAINT [CK_QsJointMeasurement_Approval] CHECK ([ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')),
                CONSTRAINT [CK_QsJointMeasurement_Quantities] CHECK ([PreviousQuantity]>=0 AND ([ContractorProposedQuantity] IS NULL OR [ContractorProposedQuantity]>0)),
                CONSTRAINT [CK_QsJointMeasurement_Schedule] CHECK (([ScheduledStartAt] IS NULL AND [ScheduledEndAt] IS NULL) OR ([ScheduledStartAt] IS NOT NULL AND [ScheduledEndAt]>[ScheduledStartAt] AND [ConsultantBusinessPartnerId] IS NOT NULL)),
                CONSTRAINT [CK_QsJointMeasurement_Hashes] CHECK (LEN([RequestHash])=64 AND LEN([PolicyHash])=64 AND ([LastMutationRequestHash] IS NULL OR LEN([LastMutationRequestHash])=64)),
                CONSTRAINT [CK_QsJointMeasurement_Applied] CHECK ([Status]<>'Applied' OR ([RemeasurementVersionId] IS NOT NULL AND [AppliedAt] IS NOT NULL)),
                CONSTRAINT [FK_QsJointRequest_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id]),
                CONSTRAINT [FK_QsJointRequest_Project] FOREIGN KEY ([ProjectId]) REFERENCES [Projects]([Id]),
                CONSTRAINT [FK_QsJointRequest_BoqVersion] FOREIGN KEY ([ProjectBoqVersionId]) REFERENCES [ProjectBoqVersions]([Id]),
                CONSTRAINT [FK_QsJointRequest_BoqLine] FOREIGN KEY ([ProjectBoqVersionLineId]) REFERENCES [ProjectBoqVersionLines]([Id]),
                CONSTRAINT [FK_QsJointRequest_Contractor] FOREIGN KEY ([ContractorBusinessPartnerId]) REFERENCES [BusinessPartners]([Id]),
                CONSTRAINT [FK_QsJointRequest_Consultant] FOREIGN KEY ([ConsultantBusinessPartnerId]) REFERENCES [BusinessPartners]([Id]),
                CONSTRAINT [FK_QsJointRequest_Profile] FOREIGN KEY ([ConfigurationProfileId]) REFERENCES [QuantitySurveyConfigurationProfiles]([Id]),
                CONSTRAINT [FK_QsJointRequest_MeasurementDecision] FOREIGN KEY ([MeasurementDecisionId]) REFERENCES [QuantitySurveyConfigurationDecisions]([Id]),
                CONSTRAINT [FK_QsJointRequest_ExternalDecision] FOREIGN KEY ([ExternalSubmissionDecisionId]) REFERENCES [QuantitySurveyConfigurationDecisions]([Id]),
                CONSTRAINT [FK_QsJointRequest_Workflow] FOREIGN KEY ([ApprovalWorkflowDefinitionId]) REFERENCES [WorkflowDefinitions]([Id]),
                CONSTRAINT [FK_QsJointRequest_Template] FOREIGN KEY ([EvidenceMetadataTemplateId]) REFERENCES [CentralDocumentMetadataTemplates]([Id]),
                CONSTRAINT [FK_QsJointRequest_Measurement] FOREIGN KEY ([MeasurementSheetId]) REFERENCES [QuantitySurveyMeasurementSheets]([Id]),
                CONSTRAINT [FK_QsJointRequest_Remeasurement] FOREIGN KEY ([RemeasurementVersionId]) REFERENCES [ProjectBoqVersions]([Id]),
                CONSTRAINT [FK_QsJointRequest_RequestedBy] FOREIGN KEY ([RequestedByUserId]) REFERENCES [Users]([Id]),
                CONSTRAINT [FK_QsJointRequest_SubmittedBy] FOREIGN KEY ([SubmittedByUserId]) REFERENCES [Users]([Id]),
                CONSTRAINT [FK_QsJointRequest_ScheduledBy] FOREIGN KEY ([ScheduledByUserId]) REFERENCES [Users]([Id]),
                CONSTRAINT [FK_QsJointRequest_ReviewedBy] FOREIGN KEY ([ReviewedById]) REFERENCES [Users]([Id]),
                CONSTRAINT [FK_QsJointRequest_ApprovedBy] FOREIGN KEY ([ApprovedById]) REFERENCES [Users]([Id])
            );

            CREATE TABLE [QuantitySurveyJointMeasurementParticipants] (
                [Id] uniqueidentifier NOT NULL, [TenantId] uniqueidentifier NOT NULL, [RequestId] uniqueidentifier NOT NULL,
                [ParticipantType] nvarchar(30) NOT NULL, [BusinessPartnerId] uniqueidentifier NULL, [RequiredRoleId] uniqueidentifier NULL,
                [RequiredRoleNameSnapshot] nvarchar(200) NULL, [IsRequired] bit NOT NULL, [AttendanceStatus] nvarchar(30) NOT NULL,
                [AttendedByUserId] uniqueidentifier NULL, [AttendedByName] nvarchar(300) NULL, [AttendedAt] datetime2 NULL,
                [AttendanceNotes] nvarchar(1000) NULL, [AttendanceHash] nvarchar(64) NULL,
                [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NULL, [CreatedBy] nvarchar(max) NULL,
                [UpdatedBy] nvarchar(max) NULL, [CreatedById] uniqueidentifier NULL, [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL, [DeletedAt] datetime2 NULL, [DeletedBy] nvarchar(max) NULL,
                CONSTRAINT [PK_QuantitySurveyJointMeasurementParticipants] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_QsJointParticipant_Type] CHECK ([ParticipantType] IN ('Contractor','Consultant','InternalRole')),
                CONSTRAINT [CK_QsJointParticipant_Assignment] CHECK (([ParticipantType] IN ('Contractor','Consultant') AND [BusinessPartnerId] IS NOT NULL AND [RequiredRoleId] IS NULL) OR ([ParticipantType]='InternalRole' AND [BusinessPartnerId] IS NULL AND [RequiredRoleId] IS NOT NULL)),
                CONSTRAINT [CK_QsJointParticipant_Attendance] CHECK ([AttendanceStatus] IN ('Invited','Attended','Absent') AND ([AttendanceStatus]<>'Attended' OR ([AttendedByUserId] IS NOT NULL AND [AttendedAt] IS NOT NULL AND LEN([AttendanceHash])=64))),
                CONSTRAINT [FK_QsJointParticipant_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id]),
                CONSTRAINT [FK_QsJointParticipant_Request] FOREIGN KEY ([RequestId]) REFERENCES [QuantitySurveyJointMeasurementRequests]([Id]),
                CONSTRAINT [FK_QsJointParticipant_Partner] FOREIGN KEY ([BusinessPartnerId]) REFERENCES [BusinessPartners]([Id]),
                CONSTRAINT [FK_QsJointParticipant_Role] FOREIGN KEY ([RequiredRoleId]) REFERENCES [AspNetRoles]([Id]),
                CONSTRAINT [FK_QsJointParticipant_User] FOREIGN KEY ([AttendedByUserId]) REFERENCES [Users]([Id])
            );

            CREATE TABLE [QuantitySurveyJointMeasurementEndorsements] (
                [Id] uniqueidentifier NOT NULL, [TenantId] uniqueidentifier NOT NULL, [RequestId] uniqueidentifier NOT NULL,
                [ParticipantId] uniqueidentifier NOT NULL, [SignerType] nvarchar(30) NOT NULL, [BusinessPartnerId] uniqueidentifier NULL,
                [SignedByUserId] uniqueidentifier NOT NULL, [SignedByName] nvarchar(300) NOT NULL, [SignatureMethod] nvarchar(30) NOT NULL,
                [AttestationSnapshot] nvarchar(1000) NOT NULL, [CertificateThumbprint] nvarchar(200) NULL,
                [ExternalSignatureReference] nvarchar(300) NULL, [SignatureHash] varchar(64) NOT NULL,
                [Notes] nvarchar(2000) NULL, [SignedAt] datetime2 NOT NULL,
                [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NULL, [CreatedBy] nvarchar(max) NULL,
                [UpdatedBy] nvarchar(max) NULL, [CreatedById] uniqueidentifier NULL, [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL, [DeletedAt] datetime2 NULL, [DeletedBy] nvarchar(max) NULL,
                CONSTRAINT [PK_QuantitySurveyJointMeasurementEndorsements] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_QsJointEndorsement_Type] CHECK ([SignerType] IN ('Contractor','Consultant')),
                CONSTRAINT [CK_QsJointEndorsement_Method] CHECK ([SignatureMethod] IN ('Attestation','DigitalCertificate','ExternalProvider')),
                CONSTRAINT [CK_QsJointEndorsement_Hash] CHECK (LEN([SignatureHash])=64),
                CONSTRAINT [FK_QsJointEndorsement_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id]),
                CONSTRAINT [FK_QsJointEndorsement_Request] FOREIGN KEY ([RequestId]) REFERENCES [QuantitySurveyJointMeasurementRequests]([Id]),
                CONSTRAINT [FK_QsJointEndorsement_Participant] FOREIGN KEY ([ParticipantId]) REFERENCES [QuantitySurveyJointMeasurementParticipants]([Id]),
                CONSTRAINT [FK_QsJointEndorsement_Partner] FOREIGN KEY ([BusinessPartnerId]) REFERENCES [BusinessPartners]([Id]),
                CONSTRAINT [FK_QsJointEndorsement_User] FOREIGN KEY ([SignedByUserId]) REFERENCES [Users]([Id])
            );

            CREATE TABLE [QuantitySurveyJointMeasurementEvidence] (
                [Id] uniqueidentifier NOT NULL, [TenantId] uniqueidentifier NOT NULL, [RequestId] uniqueidentifier NOT NULL,
                [ClientRequestId] uniqueidentifier NOT NULL, [RequestHash] varchar(64) NOT NULL, [EvidenceType] int NOT NULL,
                [Title] nvarchar(200) NOT NULL, [OriginalFileName] nvarchar(260) NOT NULL, [ContentType] nvarchar(150) NOT NULL,
                [FileSize] bigint NOT NULL, [ChecksumSha256] varchar(64) NOT NULL, [FileUploadRecordId] uniqueidentifier NOT NULL,
                [CentralDocumentRecordId] uniqueidentifier NOT NULL, [CentralDocumentVersionId] uniqueidentifier NOT NULL,
                [UploadedByUserId] uniqueidentifier NOT NULL, [UploadedByName] nvarchar(300) NOT NULL, [UploadedAt] datetime2 NOT NULL,
                [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NULL, [CreatedBy] nvarchar(max) NULL,
                [UpdatedBy] nvarchar(max) NULL, [CreatedById] uniqueidentifier NULL, [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL, [DeletedAt] datetime2 NULL, [DeletedBy] nvarchar(max) NULL,
                CONSTRAINT [PK_QuantitySurveyJointMeasurementEvidence] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_QsJointEvidence_Type] CHECK ([EvidenceType] IN (0,1,2,3,4)),
                CONSTRAINT [CK_QsJointEvidence_File] CHECK ([FileSize]>0 AND LEN([ChecksumSha256])=64 AND LEN([RequestHash])=64),
                CONSTRAINT [FK_QsJointEvidence_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id]),
                CONSTRAINT [FK_QsJointEvidence_Request] FOREIGN KEY ([RequestId]) REFERENCES [QuantitySurveyJointMeasurementRequests]([Id]),
                CONSTRAINT [FK_QsJointEvidence_Upload] FOREIGN KEY ([FileUploadRecordId]) REFERENCES [FileUploadRecords]([Id]),
                CONSTRAINT [FK_QsJointEvidence_Document] FOREIGN KEY ([CentralDocumentRecordId]) REFERENCES [CentralDocumentRecords]([Id]),
                CONSTRAINT [FK_QsJointEvidence_Version] FOREIGN KEY ([CentralDocumentVersionId]) REFERENCES [CentralDocumentVersions]([Id]),
                CONSTRAINT [FK_QsJointEvidence_User] FOREIGN KEY ([UploadedByUserId]) REFERENCES [Users]([Id])
            );

            CREATE TABLE [QuantitySurveyJointMeasurementRevisions] (
                [Id] uniqueidentifier NOT NULL, [TenantId] uniqueidentifier NOT NULL, [RequestId] uniqueidentifier NOT NULL,
                [Action] nvarchar(100) NOT NULL, [ActorUserId] uniqueidentifier NOT NULL, [ActorBusinessPartnerId] uniqueidentifier NULL,
                [ActorName] nvarchar(300) NOT NULL, [ActorRoles] nvarchar(500) NULL, [CorrelationId] nvarchar(100) NOT NULL,
                [Reason] nvarchar(2000) NULL, [BeforeJson] nvarchar(max) NULL, [AfterJson] nvarchar(max) NOT NULL,
                [CreatedAt] datetime2 NOT NULL, [UpdatedAt] datetime2 NULL, [CreatedBy] nvarchar(max) NULL,
                [UpdatedBy] nvarchar(max) NULL, [CreatedById] uniqueidentifier NULL, [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL, [DeletedAt] datetime2 NULL, [DeletedBy] nvarchar(max) NULL,
                CONSTRAINT [PK_QuantitySurveyJointMeasurementRevisions] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_QsJointRevision_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [Tenants]([Id]),
                CONSTRAINT [FK_QsJointRevision_Request] FOREIGN KEY ([RequestId]) REFERENCES [QuantitySurveyJointMeasurementRequests]([Id]),
                CONSTRAINT [FK_QsJointRevision_User] FOREIGN KEY ([ActorUserId]) REFERENCES [Users]([Id]),
                CONSTRAINT [FK_QsJointRevision_Partner] FOREIGN KEY ([ActorBusinessPartnerId]) REFERENCES [BusinessPartners]([Id])
            );

            CREATE UNIQUE INDEX [UX_QsJointRequest_Number] ON [QuantitySurveyJointMeasurementRequests]([TenantId],[RequestNumber]);
            CREATE UNIQUE INDEX [UX_QsJointRequest_Client] ON [QuantitySurveyJointMeasurementRequests]([TenantId],[ClientRequestId]);
            CREATE UNIQUE INDEX [UX_QsJointRequest_Mutation] ON [QuantitySurveyJointMeasurementRequests]([TenantId],[LastMutationClientRequestId]) WHERE [LastMutationClientRequestId] IS NOT NULL;
            CREATE UNIQUE INDEX [UX_QsJointRequest_RemeasurementClient] ON [QuantitySurveyJointMeasurementRequests]([TenantId],[RemeasurementClientRequestId]);
            CREATE UNIQUE INDEX [UX_QsJointRequest_Measurement] ON [QuantitySurveyJointMeasurementRequests]([TenantId],[MeasurementSheetId]) WHERE [MeasurementSheetId] IS NOT NULL;
            CREATE UNIQUE INDEX [UX_QsJointRequest_Remeasurement] ON [QuantitySurveyJointMeasurementRequests]([TenantId],[RemeasurementVersionId]) WHERE [RemeasurementVersionId] IS NOT NULL;
            CREATE INDEX [IX_QsJointRequest_ProjectStatus] ON [QuantitySurveyJointMeasurementRequests]([TenantId],[ProjectId],[Status],[RequestedAt]);
            CREATE INDEX [IX_QsJointRequest_LineStatus] ON [QuantitySurveyJointMeasurementRequests]([TenantId],[ProjectBoqVersionLineId],[Status]);
            CREATE UNIQUE INDEX [UX_QsJointParticipant_Assignment] ON [QuantitySurveyJointMeasurementParticipants]([TenantId],[RequestId],[ParticipantType],[BusinessPartnerId],[RequiredRoleId]);
            CREATE INDEX [IX_QsJointParticipant_Attendance] ON [QuantitySurveyJointMeasurementParticipants]([TenantId],[RequestId],[AttendanceStatus]);
            CREATE UNIQUE INDEX [UX_QsJointEndorsement_Signer] ON [QuantitySurveyJointMeasurementEndorsements]([TenantId],[RequestId],[SignerType]);
            CREATE UNIQUE INDEX [UX_QsJointEndorsement_Participant] ON [QuantitySurveyJointMeasurementEndorsements]([TenantId],[ParticipantId]);
            CREATE UNIQUE INDEX [UX_QsJointEvidence_Client] ON [QuantitySurveyJointMeasurementEvidence]([TenantId],[ClientRequestId]);
            CREATE UNIQUE INDEX [UX_QsJointEvidence_DmsVersion] ON [QuantitySurveyJointMeasurementEvidence]([TenantId],[CentralDocumentVersionId]);
            CREATE INDEX [IX_QsJointEvidence_Request] ON [QuantitySurveyJointMeasurementEvidence]([TenantId],[RequestId],[EvidenceType],[UploadedAt]);
            CREATE INDEX [IX_QsJointRevision_Request] ON [QuantitySurveyJointMeasurementRevisions]([TenantId],[RequestId],[CreatedAt]);
            CREATE INDEX [IX_QsJointRevision_Correlation] ON [QuantitySurveyJointMeasurementRevisions]([TenantId],[CorrelationId]);
            """);

        CreateGuards(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsJointRevision_Guard]; DROP TRIGGER IF EXISTS [TR_QsJointEvidence_Guard]; DROP TRIGGER IF EXISTS [TR_QsJointEndorsement_Guard]; DROP TRIGGER IF EXISTS [TR_QsJointParticipant_Guard]; DROP TRIGGER IF EXISTS [TR_QsJointRequest_Guard];");
        migrationBuilder.Sql("DROP TABLE [QuantitySurveyJointMeasurementEvidence]; DROP TABLE [QuantitySurveyJointMeasurementEndorsements]; DROP TABLE [QuantitySurveyJointMeasurementRevisions]; DROP TABLE [QuantitySurveyJointMeasurementParticipants]; DROP TABLE [QuantitySurveyJointMeasurementRequests];");
    }

    private static void CreateGuards(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsJointRequest_Guard] ON [QuantitySurveyJointMeasurementRequests]
            AFTER INSERT, UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id]=d.[Id] WHERE i.[Id] IS NULL)
                    THROW 51120, 'Joint-measurement requests cannot be deleted; retain their governed history.', 1;
                IF EXISTS (SELECT 1 FROM inserted WHERE [IsDeleted]=1)
                    THROW 51120, 'Joint-measurement requests cannot be soft-deleted.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    WHERE NOT EXISTS (SELECT 1 FROM [Projects] p WHERE p.[Id]=i.[ProjectId] AND p.[TenantId]=i.[TenantId] AND p.[IsDeleted]=0)
                       OR NOT EXISTS (SELECT 1 FROM [ProjectBoqVersionLines] l JOIN [ProjectBoqVersions] v ON v.[Id]=l.[ProjectBoqVersionId] AND v.[TenantId]=l.[TenantId]
                           WHERE l.[Id]=i.[ProjectBoqVersionLineId] AND l.[TenantId]=i.[TenantId] AND l.[ProjectId]=i.[ProjectId]
                             AND l.[ProjectBoqVersionId]=i.[ProjectBoqVersionId] AND l.[LineKey]=i.[BoqLineKey] AND l.[Quantity]=i.[PreviousQuantity]
                             AND l.[IsDeleted]=0 AND v.[Status]='Approved' AND v.[PublishedAt] IS NOT NULL AND v.[IsDeleted]=0)
                       OR NOT EXISTS (SELECT 1 FROM [BusinessPartners] b WHERE b.[Id]=i.[ContractorBusinessPartnerId] AND b.[TenantId]=i.[TenantId]
                             AND b.[IsActive]=1 AND b.[IsBlacklisted]=0 AND b.[RegistrationStatus]='Approved' AND b.[PartnerType] IN ('Contractor','Both') AND b.[IsDeleted]=0)
                       OR (i.[ConsultantBusinessPartnerId] IS NOT NULL AND (i.[ConsultantBusinessPartnerId]=i.[ContractorBusinessPartnerId]
                             OR NOT EXISTS (SELECT 1 FROM [BusinessPartners] b WHERE b.[Id]=i.[ConsultantBusinessPartnerId] AND b.[TenantId]=i.[TenantId]
                                 AND b.[IsActive]=1 AND b.[IsBlacklisted]=0 AND b.[RegistrationStatus]='Approved' AND b.[IsDeleted]=0)))
                       OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyConfigurationProfiles] p WHERE p.[Id]=i.[ConfigurationProfileId] AND p.[TenantId]=i.[TenantId]
                             AND p.[LifecycleStatus]=1 AND p.[PublishedAt] IS NOT NULL AND p.[IsDeleted]=0)
                       OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyConfigurationDecisions] d WHERE d.[Id]=i.[MeasurementDecisionId] AND d.[TenantId]=i.[TenantId]
                             AND d.[ProfileId]=i.[ConfigurationProfileId] AND d.[DecisionKey]='QS-DEC-007' AND d.[Status]=2 AND d.[ApprovalStatus]=1 AND d.[EvidenceStatus]=2 AND d.[IsDeleted]=0)
                       OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyConfigurationDecisions] d WHERE d.[Id]=i.[ExternalSubmissionDecisionId] AND d.[TenantId]=i.[TenantId]
                             AND d.[ProfileId]=i.[ConfigurationProfileId] AND d.[DecisionKey]='QS-DEC-013' AND d.[Status]=2 AND d.[ApprovalStatus]=1 AND d.[EvidenceStatus]=2 AND d.[IsDeleted]=0)
                       OR NOT EXISTS (SELECT 1 FROM [WorkflowDefinitions] w JOIN [WorkflowEntityTypes] e ON e.[Id]=w.[EntityTypeId]
                             WHERE w.[Id]=i.[ApprovalWorkflowDefinitionId] AND w.[TenantId]=i.[TenantId] AND w.[LifecycleStatus]=1 AND w.[IsActive]=1 AND w.[IsDeleted]=0 AND e.[Code]='QS_MEASUREMENT')
                       OR NOT EXISTS (SELECT 1 FROM [CentralDocumentMetadataTemplates] t WHERE t.[Id]=i.[EvidenceMetadataTemplateId] AND t.[TenantId]=i.[TenantId]
                             AND t.[TemplateCode]=i.[EvidenceMetadataTemplateCodeSnapshot] AND t.[IsActive]=1 AND t.[PublishedAt] IS NOT NULL AND t.[IsDeleted]=0)
                       OR (i.[MeasurementSheetId] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [QuantitySurveyMeasurementSheets] m WHERE m.[Id]=i.[MeasurementSheetId]
                             AND m.[TenantId]=i.[TenantId] AND m.[ProjectId]=i.[ProjectId] AND m.[ProjectBoqVersionLineId]=i.[ProjectBoqVersionLineId] AND m.[Status]='Recorded' AND m.[IsDeleted]=0))
                       OR (i.[RemeasurementVersionId] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [ProjectBoqVersions] r WHERE r.[Id]=i.[RemeasurementVersionId]
                             AND r.[TenantId]=i.[TenantId] AND r.[ProjectId]=i.[ProjectId] AND r.[VersionType]=4 AND r.[SourceVersionId]=i.[ProjectBoqVersionId] AND r.[IsDeleted]=0))
                ) THROW 51121, 'Joint-measurement tenant, BoQ, partner, policy, workflow, DMS, measurement or remeasurement lineage is invalid.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
                    WHERE i.[TenantId]<>d.[TenantId] OR i.[ProjectId]<>d.[ProjectId] OR i.[ProjectBoqVersionId]<>d.[ProjectBoqVersionId]
                       OR i.[ProjectBoqVersionLineId]<>d.[ProjectBoqVersionLineId] OR i.[BoqLineKey]<>d.[BoqLineKey]
                       OR i.[ContractorBusinessPartnerId]<>d.[ContractorBusinessPartnerId] OR i.[ClientRequestId]<>d.[ClientRequestId]
                       OR (d.[ConsultantBusinessPartnerId] IS NOT NULL AND (i.[ConsultantBusinessPartnerId] IS NULL OR i.[ConsultantBusinessPartnerId]<>d.[ConsultantBusinessPartnerId]))
                       OR i.[RequestHash]<>d.[RequestHash] OR i.[RequestNumber]<>d.[RequestNumber] OR i.[PreviousQuantity]<>d.[PreviousQuantity]
                       OR i.[ConfigurationProfileId]<>d.[ConfigurationProfileId] OR i.[MeasurementDecisionId]<>d.[MeasurementDecisionId]
                       OR i.[ExternalSubmissionDecisionId]<>d.[ExternalSubmissionDecisionId] OR i.[ApprovalWorkflowDefinitionId]<>d.[ApprovalWorkflowDefinitionId]
                       OR i.[EvidenceMetadataTemplateId]<>d.[EvidenceMetadataTemplateId] OR i.[PolicyHash]<>d.[PolicyHash]
                       OR i.[RequestedByUserId]<>d.[RequestedByUserId] OR i.[RemeasurementClientRequestId]<>d.[RemeasurementClientRequestId]
                       OR (d.[MeasurementSheetId] IS NOT NULL AND (i.[MeasurementSheetId] IS NULL OR i.[MeasurementSheetId]<>d.[MeasurementSheetId]))
                       OR (d.[RemeasurementVersionId] IS NOT NULL AND (i.[RemeasurementVersionId] IS NULL OR i.[RemeasurementVersionId]<>d.[RemeasurementVersionId]))
                ) THROW 51122, 'Joint-measurement subject, policy and approved lineage is immutable.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id]
                    WHERE i.[Status]<>d.[Status] AND NOT (
                        (d.[Status]='Draft' AND i.[Status] IN ('Submitted','Cancelled')) OR
                        (d.[Status]='Submitted' AND i.[Status] IN ('Scheduled','Cancelled')) OR
                        (d.[Status]='Scheduled' AND i.[Status] IN ('AwaitingAttendance','AwaitingEndorsements','ReadyForReview','Cancelled')) OR
                        (d.[Status]='AwaitingAttendance' AND i.[Status] IN ('AwaitingEndorsements','ReadyForReview','Cancelled')) OR
                        (d.[Status]='AwaitingEndorsements' AND i.[Status] IN ('AwaitingAttendance','ReadyForReview','Cancelled')) OR
                        (d.[Status]='ReadyForReview' AND i.[Status] IN ('PendingApproval','Cancelled')) OR
                        (d.[Status]='PendingApproval' AND i.[Status] IN ('ApprovedPendingBoqRevision','Rejected')) OR
                        (d.[Status]='ApprovedPendingBoqRevision' AND i.[Status]='BoqWorkflowPending') OR
                        (d.[Status]='BoqWorkflowPending' AND i.[Status]='Applied')
                    )
                ) THROW 51123, 'Invalid joint-measurement lifecycle transition.', 1;
                IF EXISTS (SELECT 1 FROM inserted i WHERE i.[Status]='PendingApproval' AND (i.[WorkflowInstanceId] IS NULL OR i.[ReviewedById] IS NULL OR i.[ReviewedAt] IS NULL OR i.[ApprovalStatus]<>'Pending'))
                    THROW 51124, 'Pending joint measurement requires workflow and review lineage.', 1;
                IF EXISTS (SELECT 1 FROM inserted i WHERE i.[Status] IN ('ApprovedPendingBoqRevision','BoqWorkflowPending','Applied') AND
                    (i.[ApprovedById] IS NULL OR i.[ApprovedAt] IS NULL OR i.[ApprovalStatus]<>'Approved' OR i.[ApprovedById]=i.[ReviewedById] OR i.[ApprovedById]=i.[RequestedByUserId]))
                    THROW 51125, 'Approved joint measurement requires independent maker-checker lineage.', 1;
            END
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsJointParticipant_Guard] ON [QuantitySurveyJointMeasurementParticipants]
            AFTER INSERT, UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id]=d.[Id] WHERE i.[Id] IS NULL)
                    THROW 51126, 'Joint-measurement participant history cannot be deleted.', 1;
                IF EXISTS (SELECT 1 FROM inserted i WHERE i.[IsDeleted]=1 OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyJointMeasurementRequests] r WHERE r.[Id]=i.[RequestId] AND r.[TenantId]=i.[TenantId] AND r.[IsDeleted]=0)
                    OR (i.[BusinessPartnerId] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [BusinessPartners] b WHERE b.[Id]=i.[BusinessPartnerId] AND b.[TenantId]=i.[TenantId] AND b.[IsActive]=1 AND b.[IsDeleted]=0)))
                    THROW 51127, 'Joint-measurement participant tenant or assignment lineage is invalid.', 1;
                IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.[Id]=i.[Id] WHERE i.[TenantId]<>d.[TenantId] OR i.[RequestId]<>d.[RequestId]
                    OR i.[ParticipantType]<>d.[ParticipantType] OR ISNULL(i.[BusinessPartnerId],'00000000-0000-0000-0000-000000000000')<>ISNULL(d.[BusinessPartnerId],'00000000-0000-0000-0000-000000000000')
                    OR ISNULL(i.[RequiredRoleId],'00000000-0000-0000-0000-000000000000')<>ISNULL(d.[RequiredRoleId],'00000000-0000-0000-0000-000000000000')
                    OR d.[AttendanceStatus]='Attended' OR (d.[AttendanceStatus]='Invited' AND i.[AttendanceStatus] NOT IN ('Invited','Attended','Absent')))
                    THROW 51128, 'Joint-measurement participant assignment and recorded attendance are immutable.', 1;
                IF EXISTS (SELECT 1 FROM inserted i WHERE i.[AttendanceStatus]='Attended' AND NOT EXISTS (SELECT 1 FROM [Users] u WHERE u.[Id]=i.[AttendedByUserId] AND u.[TenantId]=i.[TenantId] AND u.[IsActive]=1))
                    THROW 51129, 'Joint-measurement attendance actor is invalid.', 1;
            END
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsJointEndorsement_Guard] ON [QuantitySurveyJointMeasurementEndorsements]
            AFTER INSERT, UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted) THROW 51130, 'Joint-measurement endorsements are append-only.', 1;
                IF EXISTS (SELECT 1 FROM inserted i WHERE i.[IsDeleted]=1 OR NOT EXISTS (
                    SELECT 1 FROM [QuantitySurveyJointMeasurementParticipants] p JOIN [QuantitySurveyJointMeasurementRequests] r ON r.[Id]=p.[RequestId] AND r.[TenantId]=p.[TenantId]
                    WHERE p.[Id]=i.[ParticipantId] AND p.[RequestId]=i.[RequestId] AND p.[TenantId]=i.[TenantId]
                      AND p.[ParticipantType]=i.[SignerType] AND p.[AttendanceStatus]='Attended' AND p.[AttendedByUserId]=i.[SignedByUserId]
                      AND ISNULL(p.[BusinessPartnerId],'00000000-0000-0000-0000-000000000000')=ISNULL(i.[BusinessPartnerId],'00000000-0000-0000-0000-000000000000')
                      AND r.[MeasurementSheetId] IS NOT NULL AND r.[Status] IN ('AwaitingAttendance','AwaitingEndorsements','ReadyForReview') AND r.[IsDeleted]=0))
                    THROW 51131, 'Joint-measurement endorsement attendance, actor, partner or measurement lineage is invalid.', 1;
            END
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsJointEvidence_Guard] ON [QuantitySurveyJointMeasurementEvidence]
            AFTER INSERT, UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted) THROW 51132, 'Joint-measurement evidence is append-only.', 1;
                IF EXISTS (SELECT 1 FROM inserted i WHERE i.[IsDeleted]=1
                    OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyJointMeasurementRequests] r WHERE r.[Id]=i.[RequestId] AND r.[TenantId]=i.[TenantId] AND r.[Status] NOT IN ('Applied','Rejected','Cancelled') AND r.[IsDeleted]=0)
                    OR (SELECT COUNT_BIG(*) FROM [QuantitySurveyJointMeasurementEvidence] e WHERE e.[TenantId]=i.[TenantId] AND e.[RequestId]=i.[RequestId] AND e.[IsDeleted]=0)>30
                    OR NOT EXISTS (SELECT 1 FROM [FileUploadRecords] f WHERE f.[Id]=i.[FileUploadRecordId] AND f.[TenantId]=i.[TenantId] AND f.[Category]='quantity-survey-joint-measurement-evidence' AND f.[VirusScanStatus]=2 AND f.[FileSize]=i.[FileSize] AND f.[IsDeleted]=0)
                    OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyJointMeasurementRequests] r JOIN [CentralDocumentRecords] d ON d.[TenantId]=r.[TenantId]
                        JOIN [CentralDocumentVersions] v ON v.[DocumentRecordId]=d.[Id] AND v.[TenantId]=d.[TenantId]
                        WHERE r.[Id]=i.[RequestId] AND d.[Id]=i.[CentralDocumentRecordId] AND d.[SourceModule]='QuantitySurvey'
                          AND d.[SourceEntityType]='QuantitySurveyJointMeasurementEvidence' AND d.[SourceRecordId]=i.[Id]
                          AND d.[MetadataTemplateCode]=r.[EvidenceMetadataTemplateCodeSnapshot] AND d.[LifecycleStatus]='Active' AND d.[IsDeleted]=0
                          AND v.[Id]=i.[CentralDocumentVersionId] AND v.[FileUploadRecordId]=i.[FileUploadRecordId] AND v.[FileSize]=i.[FileSize]
                          AND v.[Status]='Validated' AND v.[VersionNumber]=d.[CurrentVersion] AND v.[IsDeleted]=0))
                    THROW 51133, 'Joint-measurement evidence tenant, clean-upload or central-DMS lineage is invalid.', 1;
            END
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsJointRevision_Guard] ON [QuantitySurveyJointMeasurementRevisions]
            AFTER INSERT, UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted) THROW 51134, 'Joint-measurement revision history is append-only.', 1;
                IF EXISTS (SELECT 1 FROM inserted i WHERE i.[IsDeleted]=1 OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyJointMeasurementRequests] r WHERE r.[Id]=i.[RequestId] AND r.[TenantId]=i.[TenantId] AND r.[IsDeleted]=0)
                    OR NOT EXISTS (SELECT 1 FROM [Users] u WHERE u.[Id]=i.[ActorUserId] AND u.[TenantId]=i.[TenantId]))
                    THROW 51135, 'Joint-measurement revision tenant, request or actor lineage is invalid.', 1;
            END
            """);
    }
}
