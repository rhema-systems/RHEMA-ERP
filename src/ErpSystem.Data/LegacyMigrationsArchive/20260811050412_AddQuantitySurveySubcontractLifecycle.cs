using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260811050412_AddQuantitySurveySubcontractLifecycle")]
public partial class AddQuantitySurveySubcontractLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("QuantitySurveySubcontractValuationId", "ProjectPaymentCertificates", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("SubcontractorBusinessPartnerId", "ProjectPaymentCertificates", "uniqueidentifier", nullable: true);

        migrationBuilder.CreateTable(
            name: "QuantitySurveySubcontracts",
            columns: table => new
            {
                Id = table.Column<Guid>("uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>("uniqueidentifier", nullable: false),
                ContractId = table.Column<Guid>("uniqueidentifier", nullable: false),
                SubcontractorBusinessPartnerId = table.Column<Guid>("uniqueidentifier", nullable: false),
                PaymentTermId = table.Column<Guid>("uniqueidentifier", nullable: false),
                ClientRequestId = table.Column<Guid>("uniqueidentifier", nullable: false),
                RequestHash = table.Column<string>("varchar(64)", unicode: false, maxLength: 64, nullable: false),
                LastMutationClientRequestId = table.Column<Guid>("uniqueidentifier", nullable: true),
                LastMutationRequestHash = table.Column<string>("varchar(64)", unicode: false, maxLength: 64, nullable: true),
                SubcontractNumber = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
                Title = table.Column<string>("nvarchar(200)", maxLength: 200, nullable: false),
                Scope = table.Column<string>("nvarchar(4000)", maxLength: 4000, nullable: false),
                SubcontractValue = table.Column<decimal>("decimal(18,4)", nullable: false),
                Currency = table.Column<string>("nvarchar(10)", maxLength: 10, nullable: false),
                RetentionPercentage = table.Column<decimal>("decimal(18,4)", nullable: false),
                StartDate = table.Column<DateTime>("datetime2", nullable: false),
                EndDate = table.Column<DateTime>("datetime2", nullable: true),
                Status = table.Column<string>("nvarchar(30)", maxLength: 30, nullable: false),
                ApprovalStatus = table.Column<string>("nvarchar(30)", maxLength: 30, nullable: false),
                ConfigurationProfileId = table.Column<Guid>("uniqueidentifier", nullable: false),
                ContractControlsDecisionId = table.Column<Guid>("uniqueidentifier", nullable: false),
                ApprovalWorkflowDefinitionId = table.Column<Guid>("uniqueidentifier", nullable: false),
                PolicyHash = table.Column<string>("varchar(64)", unicode: false, maxLength: 64, nullable: false),
                WorkflowInstanceId = table.Column<Guid>("uniqueidentifier", nullable: true),
                PreparedById = table.Column<Guid>("uniqueidentifier", nullable: false),
                PreparedAt = table.Column<DateTime>("datetime2", nullable: false),
                SubmittedById = table.Column<Guid>("uniqueidentifier", nullable: true),
                SubmittedAt = table.Column<DateTime>("datetime2", nullable: true),
                ApprovedById = table.Column<Guid>("uniqueidentifier", nullable: true),
                ApprovedAt = table.Column<DateTime>("datetime2", nullable: true),
                RejectionReason = table.Column<string>("nvarchar(2000)", maxLength: 2000, nullable: true),
                ClosedById = table.Column<Guid>("uniqueidentifier", nullable: true),
                ClosedAt = table.Column<DateTime>("datetime2", nullable: true),
                ClosureNote = table.Column<string>("nvarchar(2000)", maxLength: 2000, nullable: true),
                CorrelationId = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
                RowVersion = table.Column<byte[]>("rowversion", rowVersion: true, nullable: false),
                TenantId = table.Column<Guid>("uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>("datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>("datetime2", nullable: true),
                CreatedBy = table.Column<string>("nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>("nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>("uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>("uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>("bit", nullable: false),
                DeletedAt = table.Column<DateTime>("datetime2", nullable: true),
                DeletedBy = table.Column<string>("nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuantitySurveySubcontracts", value => value.Id);
                table.ForeignKey("FK_QuantitySurveySubcontracts_Projects_ProjectId", value => value.ProjectId, "Projects", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontracts_Contracts_ContractId", value => value.ContractId, "Contracts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontracts_BusinessPartners_SubcontractorBusinessPartnerId", value => value.SubcontractorBusinessPartnerId, "BusinessPartners", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontracts_PaymentTerms_PaymentTermId", value => value.PaymentTermId, "PaymentTerms", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontracts_QuantitySurveyConfigurationProfiles_ConfigurationProfileId", value => value.ConfigurationProfileId, "QuantitySurveyConfigurationProfiles", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontracts_QuantitySurveyConfigurationDecisions_ContractControlsDecisionId", value => value.ContractControlsDecisionId, "QuantitySurveyConfigurationDecisions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontracts_WorkflowDefinitions_ApprovalWorkflowDefinitionId", value => value.ApprovalWorkflowDefinitionId, "WorkflowDefinitions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontracts_Users_PreparedById", value => value.PreparedById, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontracts_Users_SubmittedById", value => value.SubmittedById, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontracts_Users_ApprovedById", value => value.ApprovedById, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontracts_Users_ClosedById", value => value.ClosedById, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontracts_Tenants_TenantId", value => value.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_QsSubcontracts_Status", "[Status] IN ('Draft','PendingApproval','Approved','Rejected','Closed')");
                table.CheckConstraint("CK_QsSubcontracts_Amounts", "[SubcontractValue] > 0 AND [RetentionPercentage] BETWEEN 0 AND 100");
                table.CheckConstraint("CK_QsSubcontracts_Dates", "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
                table.CheckConstraint("CK_QsSubcontracts_Hashes", "LEN([RequestHash]) = 64 AND LEN([PolicyHash]) = 64 AND ([LastMutationRequestHash] IS NULL OR LEN([LastMutationRequestHash]) = 64)");
                table.CheckConstraint("CK_QsSubcontracts_Lifecycle", "([Status] = 'Draft' AND [ApprovalStatus] = 'Draft' AND [WorkflowInstanceId] IS NULL AND [ApprovedAt] IS NULL) OR ([Status] = 'PendingApproval' AND [ApprovalStatus] = 'Pending' AND [WorkflowInstanceId] IS NOT NULL AND [ApprovedAt] IS NULL) OR ([Status] IN ('Approved','Closed') AND [ApprovalStatus] = 'Approved' AND [WorkflowInstanceId] IS NOT NULL AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL) OR ([Status] = 'Rejected' AND [ApprovalStatus] = 'Rejected' AND [WorkflowInstanceId] IS NOT NULL AND [RejectionReason] IS NOT NULL)");
                table.CheckConstraint("CK_QsSubcontracts_Closure", "[Status] <> 'Closed' OR ([ClosedById] IS NOT NULL AND [ClosedAt] IS NOT NULL AND LEN(LTRIM(RTRIM([ClosureNote]))) >= 5)");
            });

        migrationBuilder.CreateTable(
            name: "QuantitySurveySubcontractValuations",
            columns: table => new
            {
                Id = table.Column<Guid>("uniqueidentifier", nullable: false),
                SubcontractId = table.Column<Guid>("uniqueidentifier", nullable: false),
                ClientRequestId = table.Column<Guid>("uniqueidentifier", nullable: false),
                RequestHash = table.Column<string>("varchar(64)", unicode: false, maxLength: 64, nullable: false),
                LastMutationClientRequestId = table.Column<Guid>("uniqueidentifier", nullable: true),
                LastMutationRequestHash = table.Column<string>("varchar(64)", unicode: false, maxLength: 64, nullable: true),
                ValuationNumber = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
                ValuationDate = table.Column<DateTime>("datetime2", nullable: false),
                PeriodEndDate = table.Column<DateTime>("datetime2", nullable: false),
                ClaimedToDateAmount = table.Column<decimal>("decimal(18,2)", nullable: false),
                AssessedToDateAmount = table.Column<decimal>("decimal(18,2)", nullable: true),
                PreviouslyCertifiedAmount = table.Column<decimal>("decimal(18,2)", nullable: false),
                CurrentCertifiedAmount = table.Column<decimal>("decimal(18,2)", nullable: false),
                RetentionHeldAmount = table.Column<decimal>("decimal(18,2)", nullable: false),
                RetentionReleasedAmount = table.Column<decimal>("decimal(18,2)", nullable: false),
                ApprovedBackChargeAmount = table.Column<decimal>("decimal(18,2)", nullable: false),
                ApprovedContraChargeAmount = table.Column<decimal>("decimal(18,2)", nullable: false),
                TaxAmount = table.Column<decimal>("decimal(18,2)", nullable: false),
                NetCertifiedAmount = table.Column<decimal>("decimal(18,2)", nullable: false),
                IsFinal = table.Column<bool>("bit", nullable: false),
                Status = table.Column<string>("nvarchar(30)", maxLength: 30, nullable: false),
                ApprovalStatus = table.Column<string>("nvarchar(30)", maxLength: 30, nullable: false),
                SubmissionNote = table.Column<string>("nvarchar(2000)", maxLength: 2000, nullable: true),
                AssessmentNote = table.Column<string>("nvarchar(2000)", maxLength: 2000, nullable: true),
                ConfigurationProfileId = table.Column<Guid>("uniqueidentifier", nullable: false),
                ValuationDecisionId = table.Column<Guid>("uniqueidentifier", nullable: false),
                ApprovalWorkflowDefinitionId = table.Column<Guid>("uniqueidentifier", nullable: false),
                EvidenceMetadataTemplateId = table.Column<Guid>("uniqueidentifier", nullable: false),
                PolicyHash = table.Column<string>("varchar(64)", unicode: false, maxLength: 64, nullable: false),
                WorkflowInstanceId = table.Column<Guid>("uniqueidentifier", nullable: true),
                SubmittedById = table.Column<Guid>("uniqueidentifier", nullable: true),
                SubmittedBusinessPartnerId = table.Column<Guid>("uniqueidentifier", nullable: true),
                SubmittedAt = table.Column<DateTime>("datetime2", nullable: true),
                AssessedById = table.Column<Guid>("uniqueidentifier", nullable: true),
                AssessedAt = table.Column<DateTime>("datetime2", nullable: true),
                ApprovedById = table.Column<Guid>("uniqueidentifier", nullable: true),
                ApprovedAt = table.Column<DateTime>("datetime2", nullable: true),
                RejectionReason = table.Column<string>("nvarchar(2000)", maxLength: 2000, nullable: true),
                PaymentCertificateId = table.Column<Guid>("uniqueidentifier", nullable: true),
                CorrelationId = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
                RowVersion = table.Column<byte[]>("rowversion", rowVersion: true, nullable: false),
                TenantId = table.Column<Guid>("uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>("datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>("datetime2", nullable: true),
                CreatedBy = table.Column<string>("nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>("nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>("uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>("uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>("bit", nullable: false),
                DeletedAt = table.Column<DateTime>("datetime2", nullable: true),
                DeletedBy = table.Column<string>("nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuantitySurveySubcontractValuations", value => value.Id);
                table.ForeignKey("FK_QuantitySurveySubcontractValuations_QuantitySurveySubcontracts_SubcontractId", value => value.SubcontractId, "QuantitySurveySubcontracts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontractValuations_ProjectPaymentCertificates_PaymentCertificateId", value => value.PaymentCertificateId, "ProjectPaymentCertificates", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontractValuations_QuantitySurveyConfigurationProfiles_ConfigurationProfileId", value => value.ConfigurationProfileId, "QuantitySurveyConfigurationProfiles", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontractValuations_QuantitySurveyConfigurationDecisions_ValuationDecisionId", value => value.ValuationDecisionId, "QuantitySurveyConfigurationDecisions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontractValuations_WorkflowDefinitions_ApprovalWorkflowDefinitionId", value => value.ApprovalWorkflowDefinitionId, "WorkflowDefinitions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontractValuations_CentralDocumentMetadataTemplates_EvidenceMetadataTemplateId", value => value.EvidenceMetadataTemplateId, "CentralDocumentMetadataTemplates", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontractValuations_Users_SubmittedById", value => value.SubmittedById, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontractValuations_BusinessPartners_SubmittedBusinessPartnerId", value => value.SubmittedBusinessPartnerId, "BusinessPartners", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontractValuations_Users_AssessedById", value => value.AssessedById, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontractValuations_Users_ApprovedById", value => value.ApprovedById, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontractValuations_Tenants_TenantId", value => value.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_QsSubcontractValuations_Status", "[Status] IN ('Draft','Submitted','PendingApproval','Approved','Rejected','Paid')");
                table.CheckConstraint("CK_QsSubcontractValuations_Amounts", "[ClaimedToDateAmount] > 0 AND ([AssessedToDateAmount] IS NULL OR [AssessedToDateAmount] BETWEEN [PreviouslyCertifiedAmount] AND [ClaimedToDateAmount]) AND [CurrentCertifiedAmount] >= 0 AND [RetentionHeldAmount] >= 0 AND [RetentionReleasedAmount] >= 0 AND [ApprovedBackChargeAmount] >= 0 AND [ApprovedContraChargeAmount] >= 0 AND [TaxAmount] >= 0 AND [NetCertifiedAmount] >= 0");
                table.CheckConstraint("CK_QsSubcontractValuations_Hashes", "LEN([RequestHash]) = 64 AND LEN([PolicyHash]) = 64 AND ([LastMutationRequestHash] IS NULL OR LEN([LastMutationRequestHash]) = 64)");
                table.CheckConstraint("CK_QsSubcontractValuations_Lifecycle", "([Status] = 'Draft' AND [ApprovalStatus] = 'Draft' AND [WorkflowInstanceId] IS NULL) OR ([Status] = 'Submitted' AND [ApprovalStatus] = 'Draft' AND [SubmittedAt] IS NOT NULL AND [WorkflowInstanceId] IS NULL) OR ([Status] = 'PendingApproval' AND [ApprovalStatus] = 'Pending' AND [AssessedAt] IS NOT NULL AND [WorkflowInstanceId] IS NOT NULL) OR ([Status] IN ('Approved','Paid') AND [ApprovalStatus] = 'Approved' AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [PaymentCertificateId] IS NOT NULL) OR ([Status] = 'Rejected' AND [ApprovalStatus] = 'Rejected' AND [WorkflowInstanceId] IS NOT NULL AND [RejectionReason] IS NOT NULL)");
            });

        migrationBuilder.CreateTable(
            name: "QuantitySurveySubcontractEvidence",
            columns: table => new
            {
                Id = table.Column<Guid>("uniqueidentifier", nullable: false),
                SubcontractId = table.Column<Guid>("uniqueidentifier", nullable: false),
                ValuationId = table.Column<Guid>("uniqueidentifier", nullable: true),
                ClientRequestId = table.Column<Guid>("uniqueidentifier", nullable: false),
                RequestHash = table.Column<string>("varchar(64)", unicode: false, maxLength: 64, nullable: false),
                EvidenceType = table.Column<string>("nvarchar(30)", maxLength: 30, nullable: false),
                Title = table.Column<string>("nvarchar(200)", maxLength: 200, nullable: false),
                OriginalFileName = table.Column<string>("nvarchar(260)", maxLength: 260, nullable: false),
                ContentType = table.Column<string>("nvarchar(150)", maxLength: 150, nullable: false),
                FileSize = table.Column<long>("bigint", nullable: false),
                ChecksumSha256 = table.Column<string>("varchar(64)", unicode: false, maxLength: 64, nullable: false),
                FileUploadRecordId = table.Column<Guid>("uniqueidentifier", nullable: false),
                CentralDocumentRecordId = table.Column<Guid>("uniqueidentifier", nullable: false),
                CentralDocumentVersionId = table.Column<Guid>("uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>("uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>("datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>("datetime2", nullable: true),
                CreatedBy = table.Column<string>("nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>("nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>("uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>("uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>("bit", nullable: false),
                DeletedAt = table.Column<DateTime>("datetime2", nullable: true),
                DeletedBy = table.Column<string>("nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuantitySurveySubcontractEvidence", value => value.Id);
                table.ForeignKey("FK_QuantitySurveySubcontractEvidence_QuantitySurveySubcontracts_SubcontractId", value => value.SubcontractId, "QuantitySurveySubcontracts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontractEvidence_QuantitySurveySubcontractValuations_ValuationId", value => value.ValuationId, "QuantitySurveySubcontractValuations", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontractEvidence_FileUploadRecords_FileUploadRecordId", value => value.FileUploadRecordId, "FileUploadRecords", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontractEvidence_CentralDocumentRecords_CentralDocumentRecordId", value => value.CentralDocumentRecordId, "CentralDocumentRecords", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontractEvidence_CentralDocumentVersions_CentralDocumentVersionId", value => value.CentralDocumentVersionId, "CentralDocumentVersions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontractEvidence_Tenants_TenantId", value => value.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_QsSubcontractEvidence_File", "[FileSize] > 0 AND LEN([ChecksumSha256]) = 64 AND LEN([RequestHash]) = 64 AND [EvidenceType] IN ('Agreement','Valuation') AND (([EvidenceType] = 'Agreement' AND [ValuationId] IS NULL) OR ([EvidenceType] = 'Valuation' AND [ValuationId] IS NOT NULL))");
            });

        migrationBuilder.CreateTable(
            name: "QuantitySurveySubcontractRevisions",
            columns: table => new
            {
                Id = table.Column<Guid>("uniqueidentifier", nullable: false),
                SubcontractId = table.Column<Guid>("uniqueidentifier", nullable: false),
                ValuationId = table.Column<Guid>("uniqueidentifier", nullable: true),
                ClientRequestId = table.Column<Guid>("uniqueidentifier", nullable: false),
                RequestHash = table.Column<string>("varchar(64)", unicode: false, maxLength: 64, nullable: false),
                Action = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
                ActorUserId = table.Column<Guid>("uniqueidentifier", nullable: false),
                ActorBusinessPartnerId = table.Column<Guid>("uniqueidentifier", nullable: true),
                ActorName = table.Column<string>("nvarchar(300)", maxLength: 300, nullable: false),
                ActorRoles = table.Column<string>("nvarchar(500)", maxLength: 500, nullable: true),
                CorrelationId = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
                Reason = table.Column<string>("nvarchar(2000)", maxLength: 2000, nullable: true),
                BeforeJson = table.Column<string>("nvarchar(max)", nullable: true),
                AfterJson = table.Column<string>("nvarchar(max)", nullable: false),
                TenantId = table.Column<Guid>("uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>("datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>("datetime2", nullable: true),
                CreatedBy = table.Column<string>("nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>("nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>("uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>("uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>("bit", nullable: false),
                DeletedAt = table.Column<DateTime>("datetime2", nullable: true),
                DeletedBy = table.Column<string>("nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuantitySurveySubcontractRevisions", value => value.Id);
                table.ForeignKey("FK_QuantitySurveySubcontractRevisions_QuantitySurveySubcontracts_SubcontractId", value => value.SubcontractId, "QuantitySurveySubcontracts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontractRevisions_QuantitySurveySubcontractValuations_ValuationId", value => value.ValuationId, "QuantitySurveySubcontractValuations", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontractRevisions_Users_ActorUserId", value => value.ActorUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontractRevisions_BusinessPartners_ActorBusinessPartnerId", value => value.ActorBusinessPartnerId, "BusinessPartners", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveySubcontractRevisions_Tenants_TenantId", value => value.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_QsSubcontractRevisions_Request", "[ClientRequestId] <> '00000000-0000-0000-0000-000000000000' AND LEN([RequestHash]) = 64");
            });

        CreateIndexes(migrationBuilder);

        migrationBuilder.AddForeignKey("FK_ProjectPaymentCertificates_QuantitySurveySubcontractValuations_QuantitySurveySubcontractValuationId",
            "ProjectPaymentCertificates", "QuantitySurveySubcontractValuationId", "QuantitySurveySubcontractValuations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_ProjectPaymentCertificates_BusinessPartners_SubcontractorBusinessPartnerId",
            "ProjectPaymentCertificates", "SubcontractorBusinessPartnerId", "BusinessPartners", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddCheckConstraint("CK_ProjectPaymentCertificates_QsSubcontractLineage", "ProjectPaymentCertificates",
            "[QuantitySurveySubcontractValuationId] IS NULL OR ([QuantitySurveyValuationWorksheetId] IS NULL AND [SubcontractorBusinessPartnerId] IS NOT NULL AND [ContractId] IS NOT NULL AND [ConfigurationProfileId] IS NOT NULL AND [ValuationDecisionId] IS NOT NULL AND [ApprovalWorkflowDefinitionId] IS NOT NULL AND [ExpenseAccountId] IS NOT NULL AND [AccountsPayableAccountId] IS NOT NULL AND [PaymentTermId] IS NOT NULL AND [TaxGroupId] IS NOT NULL AND [CertificateNumber] IS NOT NULL AND LEN([PolicyHash]) = 64 AND LEN([RequestHash]) = 64)");

        AddGovernanceTriggers(migrationBuilder);
    }

    private static void CreateIndexes(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontracts_ApprovalWorkflowDefinitionId", "QuantitySurveySubcontracts", "ApprovalWorkflowDefinitionId");
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontracts_ApprovedById", "QuantitySurveySubcontracts", "ApprovedById");
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontracts_ClosedById", "QuantitySurveySubcontracts", "ClosedById");
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontracts_ConfigurationProfileId", "QuantitySurveySubcontracts", "ConfigurationProfileId");
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontracts_ContractControlsDecisionId", "QuantitySurveySubcontracts", "ContractControlsDecisionId");
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontracts_ContractId", "QuantitySurveySubcontracts", "ContractId");
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontracts_PaymentTermId", "QuantitySurveySubcontracts", "PaymentTermId");
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontracts_PreparedById", "QuantitySurveySubcontracts", "PreparedById");
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontracts_ProjectId", "QuantitySurveySubcontracts", "ProjectId");
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontracts_SubcontractorBusinessPartnerId", "QuantitySurveySubcontracts", "SubcontractorBusinessPartnerId");
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontracts_SubmittedById", "QuantitySurveySubcontracts", "SubmittedById");
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontracts_TenantId_ClientRequestId", "QuantitySurveySubcontracts", new[] { "TenantId", "ClientRequestId" }, unique: true);
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontracts_TenantId_LastMutationClientRequestId", "QuantitySurveySubcontracts", new[] { "TenantId", "LastMutationClientRequestId" }, unique: true, filter: "[LastMutationClientRequestId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontracts_TenantId_SubcontractNumber", "QuantitySurveySubcontracts", new[] { "TenantId", "SubcontractNumber" }, unique: true);
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontracts_TenantId_ProjectId_ContractId_SubcontractorBusinessPartnerId", "QuantitySurveySubcontracts", new[] { "TenantId", "ProjectId", "ContractId", "SubcontractorBusinessPartnerId" });

        foreach (var column in new[] { "ApprovalWorkflowDefinitionId", "ApprovedById", "AssessedById", "ConfigurationProfileId", "EvidenceMetadataTemplateId", "PaymentCertificateId", "SubcontractId", "SubmittedBusinessPartnerId", "SubmittedById", "ValuationDecisionId" })
            migrationBuilder.CreateIndex($"IX_QuantitySurveySubcontractValuations_{column}", "QuantitySurveySubcontractValuations", column);
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontractValuations_TenantId_ClientRequestId", "QuantitySurveySubcontractValuations", new[] { "TenantId", "ClientRequestId" }, unique: true);
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontractValuations_TenantId_LastMutationClientRequestId", "QuantitySurveySubcontractValuations", new[] { "TenantId", "LastMutationClientRequestId" }, unique: true, filter: "[LastMutationClientRequestId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontractValuations_TenantId_PaymentCertificateId", "QuantitySurveySubcontractValuations", new[] { "TenantId", "PaymentCertificateId" }, unique: true, filter: "[PaymentCertificateId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontractValuations_TenantId_ValuationNumber", "QuantitySurveySubcontractValuations", new[] { "TenantId", "ValuationNumber" }, unique: true);
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontractValuations_TenantId_SubcontractId_ValuationDate", "QuantitySurveySubcontractValuations", new[] { "TenantId", "SubcontractId", "ValuationDate" });

        foreach (var column in new[] { "CentralDocumentRecordId", "CentralDocumentVersionId", "FileUploadRecordId", "SubcontractId", "ValuationId" })
            migrationBuilder.CreateIndex($"IX_QuantitySurveySubcontractEvidence_{column}", "QuantitySurveySubcontractEvidence", column);
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontractEvidence_TenantId_ClientRequestId", "QuantitySurveySubcontractEvidence", new[] { "TenantId", "ClientRequestId" }, unique: true);
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontractEvidence_TenantId_CentralDocumentVersionId", "QuantitySurveySubcontractEvidence", new[] { "TenantId", "CentralDocumentVersionId" }, unique: true);

        foreach (var column in new[] { "ActorBusinessPartnerId", "ActorUserId", "SubcontractId", "ValuationId" })
            migrationBuilder.CreateIndex($"IX_QuantitySurveySubcontractRevisions_{column}", "QuantitySurveySubcontractRevisions", column);
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontractRevisions_TenantId_ClientRequestId", "QuantitySurveySubcontractRevisions", new[] { "TenantId", "ClientRequestId" }, unique: true);
        migrationBuilder.CreateIndex("IX_QuantitySurveySubcontractRevisions_TenantId_SubcontractId_CreatedAt", "QuantitySurveySubcontractRevisions", new[] { "TenantId", "SubcontractId", "CreatedAt" });

        migrationBuilder.CreateIndex("IX_ProjectPaymentCertificates_QuantitySurveySubcontractValuationId", "ProjectPaymentCertificates", "QuantitySurveySubcontractValuationId");
        migrationBuilder.CreateIndex("IX_ProjectPaymentCertificates_SubcontractorBusinessPartnerId", "ProjectPaymentCertificates", "SubcontractorBusinessPartnerId");
        migrationBuilder.CreateIndex("IX_ProjectPaymentCertificates_TenantId_QuantitySurveySubcontractValuationId", "ProjectPaymentCertificates", new[] { "TenantId", "QuantitySurveySubcontractValuationId" }, unique: true, filter: "[QuantitySurveySubcontractValuationId] IS NOT NULL AND [IsDeleted] = 0");
    }

    private static void AddGovernanceTriggers(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER [dbo].[TR_QS0521_Subcontracts_Governance]
            ON [dbo].[QuantitySurveySubcontracts]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                    THROW 52011, 'QS subcontract records cannot be physically deleted.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    LEFT JOIN dbo.Projects p ON p.Id = i.ProjectId AND p.TenantId = i.TenantId AND p.IsDeleted = 0
                    LEFT JOIN dbo.Contracts c ON c.Id = i.ContractId AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                    LEFT JOIN dbo.Tenders t ON t.Id = c.TenderId AND t.TenantId = i.TenantId AND t.IsDeleted = 0
                    LEFT JOIN dbo.PurchaseRequisitions pr ON pr.Id = t.SourcePurchaseRequisitionId AND pr.TenantId = i.TenantId AND pr.ProjectId = i.ProjectId AND pr.IsDeleted = 0
                    LEFT JOIN dbo.BusinessPartners bp ON bp.Id = i.SubcontractorBusinessPartnerId AND bp.TenantId = i.TenantId AND bp.IsDeleted = 0
                    LEFT JOIN dbo.PaymentTerms pt ON pt.Id = i.PaymentTermId AND pt.TenantId = i.TenantId AND pt.IsDeleted = 0
                    LEFT JOIN dbo.QuantitySurveyConfigurationProfiles cp ON cp.Id = i.ConfigurationProfileId AND cp.TenantId = i.TenantId AND cp.IsDeleted = 0
                    LEFT JOIN dbo.QuantitySurveyConfigurationDecisions cd ON cd.Id = i.ContractControlsDecisionId AND cd.TenantId = i.TenantId AND cd.IsDeleted = 0
                    LEFT JOIN dbo.WorkflowDefinitions wd ON wd.Id = i.ApprovalWorkflowDefinitionId AND wd.TenantId = i.TenantId AND wd.IsDeleted = 0
                    WHERE i.IsDeleted = 0 AND (p.Id IS NULL OR c.Id IS NULL OR pr.Id IS NULL OR bp.Id IS NULL OR pt.Id IS NULL OR cp.Id IS NULL OR cd.Id IS NULL OR wd.Id IS NULL
                        OR c.ContractType <> 'Works' OR c.Status <> 'Active' OR c.AllowSubcontracting = 0 OR c.SubcontractPaymentTermId <> i.PaymentTermId
                        OR bp.IsActive = 0 OR bp.IsBlacklisted = 1 OR bp.PartnerType NOT IN ('Supplier','Contractor','Both') OR pt.IsActive = 0))
                    THROW 52012, 'QS subcontract project, Works contract, subcontractor, payment-term, policy or workflow lineage is invalid.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE d.Status IN ('Approved','Closed') AND (
                        i.ProjectId <> d.ProjectId OR i.ContractId <> d.ContractId OR i.SubcontractorBusinessPartnerId <> d.SubcontractorBusinessPartnerId
                        OR i.PaymentTermId <> d.PaymentTermId OR i.SubcontractValue <> d.SubcontractValue OR i.Currency <> d.Currency
                        OR i.RetentionPercentage <> d.RetentionPercentage OR i.StartDate <> d.StartDate OR ISNULL(i.EndDate, '19000101') <> ISNULL(d.EndDate, '19000101')
                        OR i.ConfigurationProfileId <> d.ConfigurationProfileId OR i.ContractControlsDecisionId <> d.ContractControlsDecisionId
                        OR i.ApprovalWorkflowDefinitionId <> d.ApprovalWorkflowDefinitionId OR i.PolicyHash <> d.PolicyHash
                        OR (d.Status = 'Closed' AND i.Status <> d.Status)))
                    THROW 52013, 'Approved or closed QS subcontract commercial and policy lineage is immutable.', 1;
            END
            """);

        migrationBuilder.Sql("""
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
                    WHERE i.IsDeleted = 0 AND (s.Id IS NULL OR cp.Id IS NULL OR cd.Id IS NULL OR wd.Id IS NULL OR mt.Id IS NULL
                        OR (i.SubmittedBusinessPartnerId IS NOT NULL AND i.SubmittedBusinessPartnerId <> s.SubcontractorBusinessPartnerId)
                        OR i.ApprovedBackChargeAmount <> 0 OR i.ApprovedContraChargeAmount <> 0))
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

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER [dbo].[TR_QS0521_SubcontractEvidence_AppendOnly]
            ON [dbo].[QuantitySurveySubcontractEvidence]
            AFTER UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted)
                    THROW 52024, 'QS subcontract DMS evidence is append-only.', 1;
            END
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER [dbo].[TR_QS0521_SubcontractRevisions_AppendOnly]
            ON [dbo].[QuantitySurveySubcontractRevisions]
            AFTER UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted)
                    THROW 52025, 'QS subcontract revision history is append-only.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0521_SubcontractRevisions_AppendOnly];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0521_SubcontractEvidence_AppendOnly];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0521_SubcontractValuations_Governance];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0521_Subcontracts_Governance];");
        migrationBuilder.DropCheckConstraint("CK_ProjectPaymentCertificates_QsSubcontractLineage", "ProjectPaymentCertificates");
        migrationBuilder.DropForeignKey("FK_ProjectPaymentCertificates_QuantitySurveySubcontractValuations_QuantitySurveySubcontractValuationId", "ProjectPaymentCertificates");
        migrationBuilder.DropForeignKey("FK_ProjectPaymentCertificates_BusinessPartners_SubcontractorBusinessPartnerId", "ProjectPaymentCertificates");
        migrationBuilder.DropIndex("IX_ProjectPaymentCertificates_TenantId_QuantitySurveySubcontractValuationId", "ProjectPaymentCertificates");
        migrationBuilder.DropIndex("IX_ProjectPaymentCertificates_QuantitySurveySubcontractValuationId", "ProjectPaymentCertificates");
        migrationBuilder.DropIndex("IX_ProjectPaymentCertificates_SubcontractorBusinessPartnerId", "ProjectPaymentCertificates");
        migrationBuilder.DropTable("QuantitySurveySubcontractEvidence");
        migrationBuilder.DropTable("QuantitySurveySubcontractRevisions");
        migrationBuilder.DropTable("QuantitySurveySubcontractValuations");
        migrationBuilder.DropTable("QuantitySurveySubcontracts");
        migrationBuilder.DropColumn("QuantitySurveySubcontractValuationId", "ProjectPaymentCertificates");
        migrationBuilder.DropColumn("SubcontractorBusinessPartnerId", "ProjectPaymentCertificates");
    }
}
