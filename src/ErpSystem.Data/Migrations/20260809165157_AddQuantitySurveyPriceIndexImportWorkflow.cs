using System;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260809165157_AddQuantitySurveyPriceIndexImportWorkflow")]
public partial class AddQuantitySurveyPriceIndexImportWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "QuantitySurveyPriceIndexImportBatches",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IndexFamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IndexSource = table.Column<int>(type: "int", nullable: false),
                ImportFormat = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                FileHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                NormalizedPayloadHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                NormalizedPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                IssuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CentralDocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConfigurationDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ApprovalWorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                AuthorityRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuthorityRoleNameSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LineCount = table.Column<int>(type: "int", nullable: false),
                ErrorCount = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                ApprovalStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                PreparedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PreparedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                AuditAction = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ChangeReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                ActorRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuantitySurveyPriceIndexImportBatches", x => x.Id);
                table.CheckConstraint("CK_QsPriceIndexImportBatches_Approval", "[ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
                table.CheckConstraint("CK_QsPriceIndexImportBatches_Approved", "[Status] <> 'Approved' OR ([ApprovalStatus] = 'Approved' AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [ErrorCount] = 0 AND [LineCount] > 0)");
                table.CheckConstraint("CK_QsPriceIndexImportBatches_Counts", "[LineCount] >= 0 AND [ErrorCount] >= 0");
                table.CheckConstraint("CK_QsPriceIndexImportBatches_Format", "[ImportFormat] IN ('Controlled Excel','CSV')");
                table.CheckConstraint("CK_QsPriceIndexImportBatches_Source", "[IndexSource] IN (0,1,2)");
                table.CheckConstraint("CK_QsPriceIndexImportBatches_Status", "[Status] IN ('Staged','Invalid','PendingApproval','Approved','Rejected')");
                table.ForeignKey("FK_QuantitySurveyPriceIndexImportBatches_AspNetRoles_AuthorityRoleId", x => x.AuthorityRoleId, "AspNetRoles", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyPriceIndexImportBatches_CentralDocumentRecords_CentralDocumentRecordId", x => x.CentralDocumentRecordId, "CentralDocumentRecords", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyPriceIndexImportBatches_CentralDocumentVersions_CentralDocumentVersionId", x => x.CentralDocumentVersionId, "CentralDocumentVersions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyPriceIndexImportBatches_QuantitySurveyConfigurationDecisions_ConfigurationDecisionId", x => x.ConfigurationDecisionId, "QuantitySurveyConfigurationDecisions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyPriceIndexImportBatches_QuantitySurveyConfigurationProfiles_ConfigurationProfileId", x => x.ConfigurationProfileId, "QuantitySurveyConfigurationProfiles", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyPriceIndexImportBatches_QuantitySurveyPriceIndexFamilies_IndexFamilyId", x => x.IndexFamilyId, "QuantitySurveyPriceIndexFamilies", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyPriceIndexImportBatches_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyPriceIndexImportBatches_WorkflowDefinitions_ApprovalWorkflowDefinitionId", x => x.ApprovalWorkflowDefinitionId, "WorkflowDefinitions", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "QuantitySurveyPriceIndexImportRevisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ImportBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                ActorRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuantitySurveyPriceIndexImportRevisions", x => x.Id);
                table.ForeignKey("FK_QuantitySurveyPriceIndexImportRevisions_QuantitySurveyPriceIndexImportBatches_ImportBatchId", x => x.ImportBatchId, "QuantitySurveyPriceIndexImportBatches", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyPriceIndexImportRevisions_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "QuantitySurveyPriceIndexValues",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ImportBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IndexFamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<int>(type: "int", nullable: false),
                IndexPeriod = table.Column<DateTime>(type: "datetime2", nullable: false),
                IndexValue = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                PublicationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                SourceReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                ValueKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Version = table.Column<int>(type: "int", nullable: false),
                SupersedesValueId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuantitySurveyPriceIndexValues", x => x.Id);
                table.CheckConstraint("CK_QsPriceIndexValues_Current", "[IsCurrent] = 0 OR [Status] = 'Approved'");
                table.CheckConstraint("CK_QsPriceIndexValues_Period", "DAY([IndexPeriod]) = 1");
                table.CheckConstraint("CK_QsPriceIndexValues_Publication", "[PublicationDate] >= [IndexPeriod]");
                table.CheckConstraint("CK_QsPriceIndexValues_Sequence", "[Sequence] > 0");
                table.CheckConstraint("CK_QsPriceIndexValues_Status", "[Status] IN ('Staged','Approved','Superseded','Rejected')");
                table.CheckConstraint("CK_QsPriceIndexValues_Value", "[IndexValue] > 0");
                table.CheckConstraint("CK_QsPriceIndexValues_Version", "[Version] > 0");
                table.ForeignKey("FK_QuantitySurveyPriceIndexValues_QuantitySurveyPriceIndexFamilies_IndexFamilyId", x => x.IndexFamilyId, "QuantitySurveyPriceIndexFamilies", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyPriceIndexValues_QuantitySurveyPriceIndexImportBatches_ImportBatchId", x => x.ImportBatchId, "QuantitySurveyPriceIndexImportBatches", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_QuantitySurveyPriceIndexValues_QuantitySurveyPriceIndexValues_SupersedesValueId", x => x.SupersedesValueId, "QuantitySurveyPriceIndexValues", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyPriceIndexValues_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        CreateIndexes(migrationBuilder);

        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsPriceIndexImportBatches_Guard]
            ON [QuantitySurveyPriceIndexImportBatches]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id] = d.[Id] WHERE i.[Id] IS NULL)
                    THROW 51030, 'Price-index import batches cannot be deleted; retain their governed history.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    LEFT JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE (d.[Id] IS NULL AND (i.[IsDeleted] = 1 OR NOT ((i.[Status] = 'Staged' AND i.[ApprovalStatus] = 'Draft' AND i.[ErrorCount] = 0 AND i.[LineCount] > 0)
                                                   OR (i.[Status] = 'Invalid' AND i.[ApprovalStatus] = 'Draft' AND i.[ErrorCount] > 0))))
                       OR (d.[Id] IS NOT NULL AND NOT (
                              (d.[Status] = 'Staged' AND i.[Status] IN ('PendingApproval','Rejected'))
                           OR (d.[Status] = 'PendingApproval' AND i.[Status] IN ('Approved','Rejected')))))
                    THROW 51030, 'Invalid price-index import lifecycle transition.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    LEFT JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE d.[Id] IS NOT NULL AND EXISTS (
                        SELECT d.[TenantId],d.[IndexFamilyId],d.[IndexSource],d.[ImportFormat],d.[OriginalFileName],d.[FileHash],
                               d.[NormalizedPayloadHash],d.[NormalizedPayloadJson],d.[IssuesJson],d.[CentralDocumentRecordId],
                               d.[CentralDocumentVersionId],d.[FileUploadRecordId],d.[ConfigurationProfileId],d.[ConfigurationDecisionId],
                               d.[ApprovalWorkflowDefinitionId],d.[AuthorityRoleId],d.[AuthorityRoleNameSnapshot],d.[ClientRequestId],
                               d.[LineCount],d.[ErrorCount],d.[PreparedById],d.[PreparedAt],d.[CreatedAt],d.[CreatedById],d.[IsDeleted]
                        EXCEPT
                        SELECT i.[TenantId],i.[IndexFamilyId],i.[IndexSource],i.[ImportFormat],i.[OriginalFileName],i.[FileHash],
                               i.[NormalizedPayloadHash],i.[NormalizedPayloadJson],i.[IssuesJson],i.[CentralDocumentRecordId],
                               i.[CentralDocumentVersionId],i.[FileUploadRecordId],i.[ConfigurationProfileId],i.[ConfigurationDecisionId],
                               i.[ApprovalWorkflowDefinitionId],i.[AuthorityRoleId],i.[AuthorityRoleNameSnapshot],i.[ClientRequestId],
                               i.[LineCount],i.[ErrorCount],i.[PreparedById],i.[PreparedAt],i.[CreatedAt],i.[CreatedById],i.[IsDeleted]))
                    THROW 51031, 'Price-index source, policy, authority, evidence and preparer inputs are immutable after staging.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    WHERE (i.[Status] IN ('Staged','Invalid') AND (i.[ApprovalStatus] <> 'Draft' OR i.[WorkflowInstanceId] IS NOT NULL OR i.[SubmittedById] IS NOT NULL OR i.[SubmittedAt] IS NOT NULL OR i.[ApprovedById] IS NOT NULL OR i.[ApprovedAt] IS NOT NULL OR i.[RejectionReason] IS NOT NULL))
                       OR (i.[Status] = 'PendingApproval' AND (i.[ApprovalStatus] <> 'Pending' OR i.[WorkflowInstanceId] IS NULL OR i.[SubmittedById] IS NULL OR i.[SubmittedAt] IS NULL OR i.[SubmittedById] <> i.[LastModifiedById] OR i.[ApprovedById] IS NOT NULL OR i.[ApprovedAt] IS NOT NULL OR i.[RejectionReason] IS NOT NULL))
                       OR (i.[Status] = 'Approved' AND (i.[ApprovalStatus] <> 'Approved' OR i.[WorkflowInstanceId] IS NULL OR i.[SubmittedById] IS NULL OR i.[SubmittedAt] IS NULL OR i.[ApprovedById] IS NULL OR i.[ApprovedAt] IS NULL OR i.[ApprovedById] <> i.[LastModifiedById] OR i.[RejectionReason] IS NOT NULL))
                       OR (i.[Status] = 'Rejected' AND (i.[ApprovalStatus] <> 'Rejected' OR i.[WorkflowInstanceId] IS NULL OR i.[SubmittedById] IS NULL OR i.[SubmittedAt] IS NULL OR i.[LastModifiedById] IS NULL OR NULLIF(LTRIM(RTRIM(i.[RejectionReason])), '') IS NULL OR i.[ApprovedById] IS NOT NULL OR i.[ApprovedAt] IS NOT NULL)))
                    THROW 51030, 'Price-index lifecycle, workflow and approval state are inconsistent.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE d.[Status] = 'PendingApproval'
                      AND i.[Status] IN ('Approved','Rejected')
                      AND (i.[LastModifiedById] IS NULL OR i.[LastModifiedById] = i.[PreparedById]))
                    THROW 51030, 'Price-index approval and rejection require an independent checker.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    WHERE (i.[Status] <> 'Rejected' AND (
                       NOT EXISTS (
                        SELECT 1 FROM [QuantitySurveyPriceIndexFamilies] f
                        WHERE f.[Id] = i.[IndexFamilyId] AND f.[TenantId] = i.[TenantId]
                          AND f.[Source] = i.[IndexSource] AND f.[IsActive] = 1 AND f.[IsDeleted] = 0)
                       OR NOT EXISTS (
                        SELECT 1 FROM [QuantitySurveyConfigurationProfiles] p
                        WHERE p.[Id] = i.[ConfigurationProfileId] AND p.[TenantId] = i.[TenantId]
                          AND p.[LifecycleStatus] = 1 AND p.[IsDeleted] = 0
                          AND p.[EffectiveFrom] <= CASE WHEN i.[Status] IN ('PendingApproval','Approved') THEN CONVERT(date, SYSUTCDATETIME()) ELSE CONVERT(date, i.[PreparedAt]) END
                          AND (p.[EffectiveTo] IS NULL OR p.[EffectiveTo] >= CASE WHEN i.[Status] IN ('PendingApproval','Approved') THEN CONVERT(date, SYSUTCDATETIME()) ELSE CONVERT(date, i.[PreparedAt]) END))
                       OR NOT EXISTS (
                        SELECT 1 FROM [QuantitySurveyConfigurationDecisions] d6
                        WHERE d6.[Id] = i.[ConfigurationDecisionId]
                          AND d6.[ProfileId] = i.[ConfigurationProfileId]
                          AND d6.[TenantId] = i.[TenantId]
                          AND d6.[DecisionKey] = 'QS-DEC-006'
                          AND d6.[Status] = 2 AND d6.[ApprovalStatus] = 1 AND d6.[EvidenceStatus] = 2 AND d6.[IsDeleted] = 0
                          AND (d6.[EffectiveFrom] IS NULL OR d6.[EffectiveFrom] <= CASE WHEN i.[Status] IN ('PendingApproval','Approved') THEN CONVERT(date, SYSUTCDATETIME()) ELSE CONVERT(date, i.[PreparedAt]) END)
                          AND (d6.[EffectiveTo] IS NULL OR d6.[EffectiveTo] >= CASE WHEN i.[Status] IN ('PendingApproval','Approved') THEN CONVERT(date, SYSUTCDATETIME()) ELSE CONVERT(date, i.[PreparedAt]) END)
                          AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(d6.[ValueJson], '$.approvalWorkflowDefinitionId')) = i.[ApprovalWorkflowDefinitionId]
                          AND LOWER(JSON_VALUE(d6.[ValueJson], '$.importFormat')) = LOWER(i.[ImportFormat])
                          AND EXISTS (
                            SELECT 1 FROM OPENJSON(JSON_QUERY(d6.[ValueJson], '$.indexSources')) allowed
                            WHERE LOWER(allowed.[value]) = CASE i.[IndexSource]
                                WHEN 0 THEN 'gsspbci' WHEN 1 THEN 'roadsinfrastructure' WHEN 2 THEN 'controlledmanualimport' END))
                       OR NOT EXISTS (
                        SELECT 1 FROM [QuantitySurveyConfigurationDecisions] d1
                        CROSS APPLY OPENJSON(JSON_QUERY(d1.[ValueJson], '$.approverRoleIds')) roles
                        WHERE d1.[ProfileId] = i.[ConfigurationProfileId] AND d1.[TenantId] = i.[TenantId]
                          AND d1.[DecisionKey] = 'QS-DEC-001'
                          AND d1.[Status] = 2 AND d1.[ApprovalStatus] = 1 AND d1.[EvidenceStatus] = 2 AND d1.[IsDeleted] = 0
                          AND (d1.[EffectiveFrom] IS NULL OR d1.[EffectiveFrom] <= CASE WHEN i.[Status] IN ('PendingApproval','Approved') THEN CONVERT(date, SYSUTCDATETIME()) ELSE CONVERT(date, i.[PreparedAt]) END)
                          AND (d1.[EffectiveTo] IS NULL OR d1.[EffectiveTo] >= CASE WHEN i.[Status] IN ('PendingApproval','Approved') THEN CONVERT(date, SYSUTCDATETIME()) ELSE CONVERT(date, i.[PreparedAt]) END)
                          AND TRY_CONVERT(uniqueidentifier, roles.[value]) = i.[AuthorityRoleId])
                       OR NOT EXISTS (
                        SELECT 1 FROM [AspNetRoles] r
                        WHERE r.[Id] = i.[AuthorityRoleId] AND r.[Name] = i.[AuthorityRoleNameSnapshot])
                       OR NOT EXISTS (
                        SELECT 1 FROM [WorkflowDefinitions] wd
                        JOIN [WorkflowEntityTypes] wet ON wet.[Id] = wd.[EntityTypeId]
                        WHERE wd.[Id] = i.[ApprovalWorkflowDefinitionId] AND wd.[TenantId] = i.[TenantId]
                          AND wd.[LifecycleStatus] = 1 AND wd.[IsActive] = 1 AND wd.[IsDeleted] = 0
                          AND wet.[TenantId] = i.[TenantId] AND wet.[Code] = 'QS_ESCALATION'
                          AND wet.[IsActive] = 1 AND wet.[IsDeleted] = 0)
                       OR NOT EXISTS (
                        SELECT 1 FROM [CentralDocumentRecords] dr
                        JOIN [CentralDocumentVersions] dv ON dv.[Id] = i.[CentralDocumentVersionId]
                          AND dv.[DocumentRecordId] = dr.[Id] AND dv.[TenantId] = dr.[TenantId]
                          AND dv.[FileUploadRecordId] = i.[FileUploadRecordId]
                          AND ((i.[Status] = 'Invalid' AND dv.[Status] = 'Validation failed')
                               OR (i.[Status] <> 'Invalid' AND dv.[Status] = 'Validated'))
                          AND dv.[IsDeleted] = 0
                        JOIN [FileUploadRecords] fu ON fu.[Id] = i.[FileUploadRecordId]
                          AND fu.[TenantId] = i.[TenantId] AND fu.[VirusScanStatus] = 2 AND fu.[IsDeleted] = 0
                        WHERE dr.[Id] = i.[CentralDocumentRecordId] AND dr.[TenantId] = i.[TenantId]
                          AND dr.[SourceModule] = 'QuantitySurvey'
                          AND dr.[SourceEntityType] = 'QuantitySurveyPriceIndexImportBatch'
                          AND dr.[Notes] LIKE 'Document type: PriceIndexSource%'
                          AND dr.[CurrentVersion] = dv.[VersionNumber]
                          AND ((i.[Status] = 'Invalid' AND dr.[VersionStatus] = 'Validation failed')
                               OR (i.[Status] <> 'Invalid' AND dr.[VersionStatus] = 'Validated'))
                          AND dr.[SourceRecordId] = i.[Id] AND dr.[LifecycleStatus] = 'Active' AND dr.[IsDeleted] = 0)))
                       OR (i.[WorkflowInstanceId] IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM [WorkflowInstances] wi
                        WHERE wi.[Id] = i.[WorkflowInstanceId] AND wi.[TenantId] = i.[TenantId]
                          AND wi.[WorkflowDefinitionId] = i.[ApprovalWorkflowDefinitionId]
                          AND wi.[EntityId] = i.[Id] AND wi.[IsDeleted] = 0)))
                    THROW 51031, 'Invalid price-index tenant, family, policy, authority, workflow or clean central-DMS evidence.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    WHERE i.[Status] IN ('PendingApproval','Approved','Rejected')
                      AND (i.[ErrorCount] <> 0 OR i.[LineCount] <= 0 OR
                           (SELECT COUNT_BIG(*) FROM [QuantitySurveyPriceIndexValues] v
                            WHERE v.[ImportBatchId] = i.[Id] AND v.[TenantId] = i.[TenantId] AND v.[IsDeleted] = 0) <> i.[LineCount]))
                    THROW 51031, 'Submitted price-index imports require an unchanged, error-free staged row set.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    JOIN [WorkflowInstances] wi ON wi.[Id] = i.[WorkflowInstanceId]
                    WHERE (i.[Status] = 'Approved' AND wi.[Status] <> 2)
                       OR (i.[Status] = 'Rejected' AND wi.[Status] NOT IN (3,4)))
                    THROW 51030, 'Approved and rejected index imports must match the final shared-workflow outcome.', 1;
            END
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsPriceIndexValues_Guard]
            ON [QuantitySurveyPriceIndexValues]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    WHERE NOT EXISTS (
                        SELECT 1 FROM [QuantitySurveyPriceIndexImportBatches] b
                        JOIN [QuantitySurveyPriceIndexFamilies] f ON f.[Id] = i.[IndexFamilyId]
                        WHERE b.[Id] = i.[ImportBatchId] AND b.[TenantId] = i.[TenantId]
                          AND b.[IndexFamilyId] = i.[IndexFamilyId] AND b.[IndexSource] = f.[Source]
                          AND f.[TenantId] = i.[TenantId] AND f.[IsDeleted] = 0))
                    THROW 51032, 'Price-index values must remain tenant-bound to their import family and lifecycle.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    LEFT JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE d.[Id] IS NULL AND (i.[IsDeleted] = 1 OR NOT EXISTS (
                        SELECT 1 FROM [QuantitySurveyPriceIndexImportBatches] b
                        WHERE b.[Id] = i.[ImportBatchId] AND b.[TenantId] = i.[TenantId]
                          AND b.[Status] IN ('Staged','Invalid')
                          AND i.[Status] = 'Staged' AND i.[IsCurrent] = 0)))
                    THROW 51032, 'New price-index values can be staged only through an editable import batch.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE EXISTS (
                        SELECT d.[TenantId],d.[ImportBatchId],d.[IndexFamilyId],d.[Sequence],d.[IndexPeriod],d.[IndexValue],
                               d.[PublicationDate],d.[SourceReference],d.[CreatedAt],d.[CreatedById],d.[IsDeleted]
                        EXCEPT
                        SELECT i.[TenantId],i.[ImportBatchId],i.[IndexFamilyId],i.[Sequence],i.[IndexPeriod],i.[IndexValue],
                               i.[PublicationDate],i.[SourceReference],i.[CreatedAt],i.[CreatedById],i.[IsDeleted])
                       OR NOT (
                            (d.[Status] = 'Staged' AND i.[Status] = 'Approved' AND i.[IsCurrent] = 1 AND EXISTS (
                                SELECT 1 FROM [QuantitySurveyPriceIndexImportBatches] b
                                WHERE b.[Id] = i.[ImportBatchId] AND b.[TenantId] = i.[TenantId] AND b.[Status] = 'Approved'))
                         OR (d.[Status] = 'Staged' AND i.[Status] = 'Rejected' AND i.[IsCurrent] = 0 AND EXISTS (
                                SELECT 1 FROM [QuantitySurveyPriceIndexImportBatches] b
                                WHERE b.[Id] = i.[ImportBatchId] AND b.[TenantId] = i.[TenantId] AND b.[Status] = 'Rejected'))
                         OR (d.[Status] = 'Approved' AND d.[IsCurrent] = 1 AND i.[Status] = 'Superseded' AND i.[IsCurrent] = 0
                             AND d.[ValueKey] = i.[ValueKey] AND d.[Version] = i.[Version] AND
                             (d.[SupersedesValueId] = i.[SupersedesValueId] OR (d.[SupersedesValueId] IS NULL AND i.[SupersedesValueId] IS NULL)))))
                    THROW 51032, 'Submitted or Approved price-index input rows are immutable.', 1;

                IF EXISTS (
                    SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id] = d.[Id]
                    WHERE i.[Id] IS NULL AND NOT EXISTS (
                        SELECT 1 FROM [QuantitySurveyPriceIndexImportBatches] b
                        WHERE b.[Id] = d.[ImportBatchId] AND b.[TenantId] = d.[TenantId]
                          AND b.[Status] IN ('Staged','Invalid')))
                    THROW 51032, 'Values belonging to submitted index imports cannot be deleted.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    WHERE i.[Status] = 'Approved' AND i.[IsCurrent] = 1 AND
                      ((i.[SupersedesValueId] IS NULL AND i.[Version] <> 1)
                       OR (i.[SupersedesValueId] IS NOT NULL AND NOT EXISTS (
                            SELECT 1 FROM [QuantitySurveyPriceIndexValues] p
                            WHERE p.[Id] = i.[SupersedesValueId] AND p.[TenantId] = i.[TenantId]
                              AND p.[IndexFamilyId] = i.[IndexFamilyId] AND p.[IndexPeriod] = i.[IndexPeriod]
                              AND p.[ValueKey] = i.[ValueKey] AND p.[Version] + 1 = i.[Version]
                              AND p.[Status] = 'Superseded' AND p.[IsCurrent] = 0 AND p.[IsDeleted] = 0))))
                    THROW 51033, 'Approved index revisions must preserve the prior family, period, value key and version lineage.', 1;
            END
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsPriceIndexImportRevisions_AppendOnly]
            ON [QuantitySurveyPriceIndexImportRevisions]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted)
                    THROW 51034, 'Price-index import revision history is append-only.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    WHERE i.[IsDeleted] = 1 OR NOT EXISTS (
                        SELECT 1 FROM [QuantitySurveyPriceIndexImportBatches] b
                        WHERE b.[Id] = i.[ImportBatchId] AND b.[TenantId] = i.[TenantId]))
                    THROW 51034, 'Price-index import revision history must remain tenant-bound to its batch.', 1;
            END
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsPriceIndexFamilies_ImportInUseGuard]
            ON [QuantitySurveyPriceIndexFamilies]
            AFTER UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM deleted d
                    LEFT JOIN inserted i ON i.[Id] = d.[Id]
                    WHERE (i.[Id] IS NULL OR i.[IsDeleted] = 1 OR i.[IsActive] = 0 OR i.[Source] <> d.[Source])
                      AND EXISTS (
                        SELECT 1 FROM [QuantitySurveyPriceIndexImportBatches] b
                        WHERE b.[IndexFamilyId] = d.[Id] AND b.[TenantId] = d.[TenantId]
                          AND b.[IsDeleted] = 0 AND b.[Status] <> 'Invalid'))
                    THROW 51035, 'An index family with retained import history cannot be deleted, deactivated or reassigned to another source.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsPriceIndexFamilies_ImportInUseGuard];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsPriceIndexImportRevisions_AppendOnly];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsPriceIndexValues_Guard];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsPriceIndexImportBatches_Guard];");
        migrationBuilder.DropTable(name: "QuantitySurveyPriceIndexImportRevisions");
        migrationBuilder.DropTable(name: "QuantitySurveyPriceIndexValues");
        migrationBuilder.DropTable(name: "QuantitySurveyPriceIndexImportBatches");
    }

    private static void CreateIndexes(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex("IX_QuantitySurveyPriceIndexImportBatches_ApprovalWorkflowDefinitionId", "QuantitySurveyPriceIndexImportBatches", "ApprovalWorkflowDefinitionId");
        migrationBuilder.CreateIndex("IX_QuantitySurveyPriceIndexImportBatches_AuthorityRoleId", "QuantitySurveyPriceIndexImportBatches", "AuthorityRoleId");
        migrationBuilder.CreateIndex("IX_QuantitySurveyPriceIndexImportBatches_CentralDocumentRecordId", "QuantitySurveyPriceIndexImportBatches", "CentralDocumentRecordId");
        migrationBuilder.CreateIndex("IX_QuantitySurveyPriceIndexImportBatches_CentralDocumentVersionId", "QuantitySurveyPriceIndexImportBatches", "CentralDocumentVersionId");
        migrationBuilder.CreateIndex("IX_QuantitySurveyPriceIndexImportBatches_ConfigurationDecisionId", "QuantitySurveyPriceIndexImportBatches", "ConfigurationDecisionId");
        migrationBuilder.CreateIndex("IX_QuantitySurveyPriceIndexImportBatches_ConfigurationProfileId", "QuantitySurveyPriceIndexImportBatches", "ConfigurationProfileId");
        migrationBuilder.CreateIndex("IX_QuantitySurveyPriceIndexImportBatches_IndexFamilyId", "QuantitySurveyPriceIndexImportBatches", "IndexFamilyId");
        migrationBuilder.CreateIndex("IX_QuantitySurveyPriceIndexImportBatches_TenantId_ClientRequestId", "QuantitySurveyPriceIndexImportBatches", new[] { "TenantId", "ClientRequestId" }, unique: true);
        migrationBuilder.CreateIndex("IX_QuantitySurveyPriceIndexImportBatches_TenantId_ConfigurationDecisionId_Status", "QuantitySurveyPriceIndexImportBatches", new[] { "TenantId", "ConfigurationDecisionId", "Status" });
        migrationBuilder.CreateIndex("IX_QuantitySurveyPriceIndexImportBatches_TenantId_IndexFamilyId_Status_PreparedAt", "QuantitySurveyPriceIndexImportBatches", new[] { "TenantId", "IndexFamilyId", "Status", "PreparedAt" });
        migrationBuilder.CreateIndex("IX_QuantitySurveyPriceIndexImportRevisions_ImportBatchId", "QuantitySurveyPriceIndexImportRevisions", "ImportBatchId");
        migrationBuilder.CreateIndex("IX_QuantitySurveyPriceIndexImportRevisions_TenantId_CorrelationId", "QuantitySurveyPriceIndexImportRevisions", new[] { "TenantId", "CorrelationId" });
        migrationBuilder.CreateIndex("IX_QuantitySurveyPriceIndexImportRevisions_TenantId_ImportBatchId_CreatedAt", "QuantitySurveyPriceIndexImportRevisions", new[] { "TenantId", "ImportBatchId", "CreatedAt" });
        migrationBuilder.CreateIndex("IX_QuantitySurveyPriceIndexValues_ImportBatchId", "QuantitySurveyPriceIndexValues", "ImportBatchId");
        migrationBuilder.CreateIndex("IX_QuantitySurveyPriceIndexValues_IndexFamilyId", "QuantitySurveyPriceIndexValues", "IndexFamilyId");
        migrationBuilder.CreateIndex("IX_QuantitySurveyPriceIndexValues_SupersedesValueId", "QuantitySurveyPriceIndexValues", "SupersedesValueId");
        migrationBuilder.CreateIndex("IX_QuantitySurveyPriceIndexValues_TenantId_ImportBatchId_IndexPeriod", "QuantitySurveyPriceIndexValues", new[] { "TenantId", "ImportBatchId", "IndexPeriod" }, unique: true);
        migrationBuilder.CreateIndex("IX_QuantitySurveyPriceIndexValues_TenantId_ImportBatchId_Sequence", "QuantitySurveyPriceIndexValues", new[] { "TenantId", "ImportBatchId", "Sequence" }, unique: true);
        migrationBuilder.CreateIndex("IX_QuantitySurveyPriceIndexValues_TenantId_IndexFamilyId_IndexPeriod", "QuantitySurveyPriceIndexValues", new[] { "TenantId", "IndexFamilyId", "IndexPeriod" }, unique: true, filter: "[IsDeleted] = 0 AND [IsCurrent] = 1 AND [Status] = 'Approved'");
        migrationBuilder.CreateIndex("IX_QuantitySurveyPriceIndexValues_TenantId_ValueKey_Version", "QuantitySurveyPriceIndexValues", new[] { "TenantId", "ValueKey", "Version" }, unique: true);
    }
}
