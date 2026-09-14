using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantitySurveyContractClaimLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuantitySurveyContractClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractorBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedBoqVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VariationOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExtensionOfTimeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    LastMutationClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastMutationRequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    ClaimNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ClaimType = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Basis = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ClaimedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QsAssessedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ApprovedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RejectedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    QsReviewNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DisputeStatus = table.Column<int>(type: "int", nullable: false),
                    DisputeReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DisputeResolution = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SettlementStatus = table.Column<int>(type: "int", nullable: false),
                    SettledAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SettlementReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SettlementDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariationDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalWorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceMetadataTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QsVettedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QsVettedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyContractClaims", x => x.Id);
                    table.CheckConstraint("CK_QsContractClaims_Amounts", "[ClaimedAmount] > 0 AND ([QsAssessedAmount] IS NULL OR ([QsAssessedAmount] >= 0 AND [QsAssessedAmount] <= [ClaimedAmount])) AND ([ApprovedAmount] IS NULL OR ([ApprovedAmount] >= 0 AND [ApprovedAmount] <= [ClaimedAmount])) AND ([RejectedAmount] IS NULL OR ([RejectedAmount] >= 0 AND [RejectedAmount] <= [ClaimedAmount])) AND [SettledAmount] >= 0 AND ([ApprovedAmount] IS NULL OR [SettledAmount] <= [ApprovedAmount])");
                    table.CheckConstraint("CK_QsContractClaims_Approval", "[ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
                    table.CheckConstraint("CK_QsContractClaims_Dispute", "[DisputeStatus] BETWEEN 0 AND 3 AND ([DisputeStatus] = 0 OR [Status] IN ('Approved','Rejected')) AND ([DisputeStatus] = 0 OR LEN(LTRIM(RTRIM([DisputeReason]))) > 0) AND ([DisputeStatus] IN (0,1) OR LEN(LTRIM(RTRIM([DisputeResolution]))) > 0)");
                    table.CheckConstraint("CK_QsContractClaims_Hashes", "LEN([RequestHash]) = 64 AND LEN([PolicyHash]) = 64 AND ([LastMutationRequestHash] IS NULL OR LEN([LastMutationRequestHash]) = 64)");
                    table.CheckConstraint("CK_QsContractClaims_Lifecycle", "([Status] = 'Draft' AND [WorkflowInstanceId] IS NULL AND [SubmittedAt] IS NULL AND [QsVettedAt] IS NULL AND [ApprovedAt] IS NULL) OR ([Status] = 'Submitted' AND [WorkflowInstanceId] IS NULL AND [SubmittedById] IS NOT NULL AND [SubmittedAt] IS NOT NULL AND [QsVettedAt] IS NULL AND [ApprovedAt] IS NULL) OR ([Status] = 'Vetted' AND [WorkflowInstanceId] IS NULL AND [SubmittedById] IS NOT NULL AND [SubmittedAt] IS NOT NULL AND [QsVettedById] IS NOT NULL AND [QsVettedAt] IS NOT NULL AND [QsAssessedAmount] IS NOT NULL AND [ApprovedAt] IS NULL) OR ([Status] = 'PendingApproval' AND [WorkflowInstanceId] IS NOT NULL AND [QsVettedAt] IS NOT NULL AND [ApprovedAt] IS NULL) OR ([Status] = 'Approved' AND [WorkflowInstanceId] IS NOT NULL AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [ApprovedAmount] IS NOT NULL) OR ([Status] = 'Rejected' AND [WorkflowInstanceId] IS NOT NULL AND [RejectionReason] IS NOT NULL)");
                    table.CheckConstraint("CK_QsContractClaims_Settlement", "[SettlementStatus] BETWEEN 0 AND 3 AND ([SettlementStatus] = 0 OR [Status] = 'Approved') AND ([SettlementStatus] IN (0,1) OR ([SettledAmount] > 0 AND [SettlementReference] IS NOT NULL AND [SettlementDate] IS NOT NULL)) AND ([SettlementStatus] <> 3 OR [SettledAmount] = [ApprovedAmount])");
                    table.CheckConstraint("CK_QsContractClaims_Source", "([ClaimType] = 0 AND [ExtensionOfTimeId] IS NOT NULL AND [VariationOrderId] IS NULL) OR ([ClaimType] IN (2,3,4) AND [VariationOrderId] IS NOT NULL AND [ExtensionOfTimeId] IS NULL) OR ([ClaimType] IN (1,5) AND [VariationOrderId] IS NULL AND [ExtensionOfTimeId] IS NULL)");
                    table.CheckConstraint("CK_QsContractClaims_StateAlignment", "([Status] IN ('Draft','Submitted','Vetted') AND [ApprovalStatus] = 'Draft') OR ([Status] = 'PendingApproval' AND [ApprovalStatus] = 'Pending') OR ([Status] = 'Approved' AND [ApprovalStatus] = 'Approved') OR ([Status] = 'Rejected' AND [ApprovalStatus] = 'Rejected')");
                    table.CheckConstraint("CK_QsContractClaims_Status", "[Status] IN ('Draft','Submitted','Vetted','PendingApproval','Approved','Rejected')");
                    table.CheckConstraint("CK_QsContractClaims_Type", "[ClaimType] BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaims_BusinessPartners_ContractorBusinessPartnerId",
                        column: x => x.ContractorBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaims_BusinessPartners_SubmittedBusinessPartnerId",
                        column: x => x.SubmittedBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaims_CentralDocumentMetadataTemplates_EvidenceMetadataTemplateId",
                        column: x => x.EvidenceMetadataTemplateId,
                        principalTable: "CentralDocumentMetadataTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaims_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaims_ProjectBoqVersions_ApprovedBoqVersionId",
                        column: x => x.ApprovedBoqVersionId,
                        principalTable: "ProjectBoqVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaims_ProjectExtensionOfTimeRequests_ExtensionOfTimeId",
                        column: x => x.ExtensionOfTimeId,
                        principalTable: "ProjectExtensionOfTimeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaims_ProjectVariationOrders_VariationOrderId",
                        column: x => x.VariationOrderId,
                        principalTable: "ProjectVariationOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaims_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaims_QuantitySurveyConfigurationDecisions_VariationDecisionId",
                        column: x => x.VariationDecisionId,
                        principalTable: "QuantitySurveyConfigurationDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaims_QuantitySurveyConfigurationProfiles_ConfigurationProfileId",
                        column: x => x.ConfigurationProfileId,
                        principalTable: "QuantitySurveyConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaims_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaims_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaims_Users_QsVettedById",
                        column: x => x.QsVettedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaims_Users_SubmittedById",
                        column: x => x.SubmittedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaims_WorkflowDefinitions_ApprovalWorkflowDefinitionId",
                        column: x => x.ApprovalWorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuantitySurveyContractClaimEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ChecksumSha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CentralDocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyContractClaimEvidence", x => x.Id);
                    table.CheckConstraint("CK_QsContractClaimEvidence_File", "[FileSize] > 0 AND LEN([ChecksumSha256]) = 64 AND LEN([RequestHash]) = 64");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaimEvidence_CentralDocumentRecords_CentralDocumentRecordId",
                        column: x => x.CentralDocumentRecordId,
                        principalTable: "CentralDocumentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaimEvidence_CentralDocumentVersions_CentralDocumentVersionId",
                        column: x => x.CentralDocumentVersionId,
                        principalTable: "CentralDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaimEvidence_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaimEvidence_QuantitySurveyContractClaims_ContractClaimId",
                        column: x => x.ContractClaimId,
                        principalTable: "QuantitySurveyContractClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaimEvidence_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuantitySurveyContractClaimRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ActorRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyContractClaimRevisions", x => x.Id);
                    table.CheckConstraint("CK_QsContractClaimRevisions_Request", "[ClientRequestId] <> '00000000-0000-0000-0000-000000000000' AND LEN([RequestHash]) = 64");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaimRevisions_BusinessPartners_ActorBusinessPartnerId",
                        column: x => x.ActorBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaimRevisions_QuantitySurveyContractClaims_ContractClaimId",
                        column: x => x.ContractClaimId,
                        principalTable: "QuantitySurveyContractClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaimRevisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyContractClaimRevisions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaimEvidence_CentralDocumentRecordId",
                table: "QuantitySurveyContractClaimEvidence",
                column: "CentralDocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaimEvidence_CentralDocumentVersionId",
                table: "QuantitySurveyContractClaimEvidence",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaimEvidence_ContractClaimId",
                table: "QuantitySurveyContractClaimEvidence",
                column: "ContractClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaimEvidence_FileUploadRecordId",
                table: "QuantitySurveyContractClaimEvidence",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaimEvidence_TenantId_CentralDocumentVersionId",
                table: "QuantitySurveyContractClaimEvidence",
                columns: new[] { "TenantId", "CentralDocumentVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaimEvidence_TenantId_ClientRequestId",
                table: "QuantitySurveyContractClaimEvidence",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaimEvidence_TenantId_ContractClaimId_CreatedAt",
                table: "QuantitySurveyContractClaimEvidence",
                columns: new[] { "TenantId", "ContractClaimId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaimRevisions_ActorBusinessPartnerId",
                table: "QuantitySurveyContractClaimRevisions",
                column: "ActorBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaimRevisions_ActorUserId",
                table: "QuantitySurveyContractClaimRevisions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaimRevisions_ContractClaimId",
                table: "QuantitySurveyContractClaimRevisions",
                column: "ContractClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaimRevisions_TenantId_ClientRequestId",
                table: "QuantitySurveyContractClaimRevisions",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaimRevisions_TenantId_ContractClaimId_CreatedAt",
                table: "QuantitySurveyContractClaimRevisions",
                columns: new[] { "TenantId", "ContractClaimId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaimRevisions_TenantId_CorrelationId",
                table: "QuantitySurveyContractClaimRevisions",
                columns: new[] { "TenantId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaims_ApprovalWorkflowDefinitionId",
                table: "QuantitySurveyContractClaims",
                column: "ApprovalWorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaims_ApprovedBoqVersionId",
                table: "QuantitySurveyContractClaims",
                column: "ApprovedBoqVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaims_ApprovedById",
                table: "QuantitySurveyContractClaims",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaims_ConfigurationProfileId",
                table: "QuantitySurveyContractClaims",
                column: "ConfigurationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaims_ContractId",
                table: "QuantitySurveyContractClaims",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaims_ContractorBusinessPartnerId",
                table: "QuantitySurveyContractClaims",
                column: "ContractorBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaims_EvidenceMetadataTemplateId",
                table: "QuantitySurveyContractClaims",
                column: "EvidenceMetadataTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaims_ExtensionOfTimeId",
                table: "QuantitySurveyContractClaims",
                column: "ExtensionOfTimeId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaims_ProjectId",
                table: "QuantitySurveyContractClaims",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaims_QsVettedById",
                table: "QuantitySurveyContractClaims",
                column: "QsVettedById");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaims_SubmittedBusinessPartnerId",
                table: "QuantitySurveyContractClaims",
                column: "SubmittedBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaims_SubmittedById",
                table: "QuantitySurveyContractClaims",
                column: "SubmittedById");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaims_TenantId_ClaimNumber",
                table: "QuantitySurveyContractClaims",
                columns: new[] { "TenantId", "ClaimNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaims_TenantId_ClientRequestId",
                table: "QuantitySurveyContractClaims",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaims_TenantId_ContractId_ContractorBusinessPartnerId",
                table: "QuantitySurveyContractClaims",
                columns: new[] { "TenantId", "ContractId", "ContractorBusinessPartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaims_TenantId_LastMutationClientRequestId",
                table: "QuantitySurveyContractClaims",
                columns: new[] { "TenantId", "LastMutationClientRequestId" },
                unique: true,
                filter: "[LastMutationClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaims_TenantId_ProjectId_Status_CreatedAt",
                table: "QuantitySurveyContractClaims",
                columns: new[] { "TenantId", "ProjectId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaims_VariationDecisionId",
                table: "QuantitySurveyContractClaims",
                column: "VariationDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyContractClaims_VariationOrderId",
                table: "QuantitySurveyContractClaims",
                column: "VariationOrderId");

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_QS0509_ContractClaims_Governance]
                ON [dbo].[QuantitySurveyContractClaims]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51990, 'QS_CLAIM_DELETE_BLOCKED: governed contractor claims are retained as immutable history.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN dbo.Projects p ON p.Id = i.ProjectId AND p.TenantId = i.TenantId AND p.IsDeleted = 0
                        LEFT JOIN dbo.Contracts c ON c.Id = i.ContractId AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                            AND c.ContractType = 'Works' AND c.Status = 'Active'
                        LEFT JOIN dbo.Tenders t ON t.Id = c.TenderId AND t.TenantId = i.TenantId AND t.IsDeleted = 0
                        LEFT JOIN dbo.PurchaseRequisitions pr ON pr.Id = t.SourcePurchaseRequisitionId AND pr.TenantId = i.TenantId
                            AND pr.ProjectId = i.ProjectId AND pr.IsDeleted = 0
                        LEFT JOIN dbo.BusinessPartners bp ON bp.Id = i.ContractorBusinessPartnerId AND bp.TenantId = i.TenantId
                            AND bp.Id = c.BusinessPartnerId AND bp.IsDeleted = 0
                        LEFT JOIN dbo.BusinessPartners sbp ON sbp.Id = i.SubmittedBusinessPartnerId AND sbp.TenantId = i.TenantId
                            AND sbp.Id = i.ContractorBusinessPartnerId AND sbp.IsDeleted = 0
                        LEFT JOIN dbo.QuantitySurveyConfigurationProfiles cp ON cp.Id = i.ConfigurationProfileId
                            AND cp.TenantId = i.TenantId AND cp.LifecycleStatus = 1 AND cp.PublishedAt IS NOT NULL AND cp.IsDeleted = 0
                        LEFT JOIN dbo.QuantitySurveyConfigurationDecisions cd ON cd.Id = i.VariationDecisionId
                            AND cd.TenantId = i.TenantId AND cd.ProfileId = cp.Id AND cd.DecisionKey = 'QS-DEC-011'
                            AND cd.Status = 2 AND cd.ApprovalStatus = 1 AND cd.EvidenceStatus = 2 AND cd.IsDeleted = 0
                        LEFT JOIN dbo.WorkflowDefinitions wd ON wd.Id = i.ApprovalWorkflowDefinitionId AND wd.TenantId = i.TenantId
                            AND wd.LifecycleStatus = 1 AND wd.IsActive = 1 AND wd.IsDeleted = 0
                        LEFT JOIN dbo.WorkflowEntityTypes wet ON wet.Id = wd.EntityTypeId AND wet.TenantId = i.TenantId
                            AND wet.Code = 'QS_CLAIM' AND wet.IsActive = 1 AND wet.IsDeleted = 0
                        LEFT JOIN dbo.CentralDocumentMetadataTemplates mt ON mt.Id = i.EvidenceMetadataTemplateId
                            AND mt.TenantId = i.TenantId AND mt.IsActive = 1 AND mt.PublishedAt IS NOT NULL AND mt.IsDeleted = 0
                        LEFT JOIN dbo.Users su ON su.Id = i.SubmittedById AND su.TenantId = i.TenantId
                        LEFT JOIN dbo.Users qu ON qu.Id = i.QsVettedById AND qu.TenantId = i.TenantId
                        LEFT JOIN dbo.Users au ON au.Id = i.ApprovedById AND au.TenantId = i.TenantId
                        WHERE p.Id IS NULL OR c.Id IS NULL OR t.Id IS NULL OR pr.Id IS NULL OR bp.Id IS NULL OR sbp.Id IS NULL
                           OR cp.Id IS NULL OR cd.Id IS NULL OR wd.Id IS NULL OR wet.Id IS NULL OR mt.Id IS NULL
                           OR i.Currency <> c.Currency
                           OR TRY_CONVERT(uniqueidentifier, JSON_VALUE(cd.ValueJson, '$.claimWorkflowDefinitionId')) <> i.ApprovalWorkflowDefinitionId
                           OR TRY_CONVERT(uniqueidentifier, JSON_VALUE(cd.ValueJson, '$.variationEvidenceMetadataTemplateId')) <> i.EvidenceMetadataTemplateId
                           OR NOT EXISTS (SELECT 1 FROM OPENJSON(cd.ValueJson, '$.allowedTypes') WHERE [value] = 'Claim')
                           OR (i.SubmittedById IS NOT NULL AND su.Id IS NULL) OR (i.QsVettedById IS NOT NULL AND qu.Id IS NULL)
                           OR (i.ApprovedById IS NOT NULL AND au.Id IS NULL)
                           OR (i.ApprovedBoqVersionId IS NOT NULL AND NOT EXISTS (
                                SELECT 1 FROM dbo.ProjectBoqVersions b WHERE b.Id = i.ApprovedBoqVersionId AND b.TenantId = i.TenantId
                                  AND b.ProjectId = i.ProjectId AND b.ApprovalStatus = 'Approved' AND b.PublishedAt IS NOT NULL AND b.IsDeleted = 0))
                           OR (i.VariationOrderId IS NOT NULL AND NOT EXISTS (
                                SELECT 1 FROM dbo.ProjectVariationOrders v WHERE v.Id = i.VariationOrderId AND v.TenantId = i.TenantId
                                  AND v.ProjectId = i.ProjectId AND v.ContractId = i.ContractId AND v.IsQuantitySurveyGoverned = 1
                                  AND v.Status = 'Approved' AND v.IsDeleted = 0
                                  AND (i.ClaimType NOT IN (3,4) OR v.VariationType = CASE i.ClaimType WHEN 3 THEN 'Daywork' ELSE 'AdditionalWork' END)))
                           OR (i.ExtensionOfTimeId IS NOT NULL AND NOT EXISTS (
                                SELECT 1 FROM dbo.ProjectExtensionOfTimeRequests e WHERE e.Id = i.ExtensionOfTimeId AND e.TenantId = i.TenantId
                                  AND e.ProjectId = i.ProjectId AND e.ContractId = i.ContractId
                                  AND e.Status IN ('Submitted','UnderReview','Approved','Implemented') AND e.IsDeleted = 0)))
                        THROW 51991, 'QS_CLAIM_LINEAGE_INVALID: tenant-owned Works contract, contractor, source, policy, workflow, DMS and actor lineage are required.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN deleted d ON d.Id = i.Id
                        WHERE (d.Id IS NULL AND i.Status <> 'Draft')
                           OR (d.Id IS NOT NULL AND (
                               i.TenantId <> d.TenantId OR i.ProjectId <> d.ProjectId OR i.ClientRequestId <> d.ClientRequestId
                               OR i.RequestHash <> d.RequestHash OR i.ClaimNumber <> d.ClaimNumber OR i.IsDeleted <> d.IsDeleted
                               OR i.SubmittedBusinessPartnerId <> d.SubmittedBusinessPartnerId
                               OR (i.Status <> d.Status AND NOT (
                                  (d.Status = 'Draft' AND i.Status = 'Submitted')
                                  OR (d.Status = 'Rejected' AND i.Status = 'Draft')
                                  OR (d.Status = 'Submitted' AND i.Status = 'Vetted')
                                  OR (d.Status = 'Vetted' AND i.Status = 'PendingApproval')
                                  OR (d.Status = 'PendingApproval' AND i.Status IN ('Approved','Rejected'))))
                               OR (d.Status NOT IN ('Draft','Rejected') AND (
                                  i.ContractId <> d.ContractId OR i.ContractorBusinessPartnerId <> d.ContractorBusinessPartnerId
                                  OR ISNULL(i.ApprovedBoqVersionId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.ApprovedBoqVersionId, '00000000-0000-0000-0000-000000000000')
                                  OR ISNULL(i.VariationOrderId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.VariationOrderId, '00000000-0000-0000-0000-000000000000')
                                  OR ISNULL(i.ExtensionOfTimeId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.ExtensionOfTimeId, '00000000-0000-0000-0000-000000000000')
                                  OR i.ClaimType <> d.ClaimType OR i.Title <> d.Title OR i.Basis <> d.Basis OR i.ClaimedAmount <> d.ClaimedAmount
                                  OR i.Currency <> d.Currency OR i.ConfigurationProfileId <> d.ConfigurationProfileId
                                  OR i.VariationDecisionId <> d.VariationDecisionId OR i.ApprovalWorkflowDefinitionId <> d.ApprovalWorkflowDefinitionId
                                  OR i.EvidenceMetadataTemplateId <> d.EvidenceMetadataTemplateId OR i.PolicyHash <> d.PolicyHash)))))
                        THROW 51992, 'QS_CLAIM_TRANSITION_INVALID: claim identity, frozen lineage or lifecycle transition is invalid.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN dbo.WorkflowInstances wi ON wi.Id = i.WorkflowInstanceId AND wi.TenantId = i.TenantId
                            AND wi.WorkflowDefinitionId = i.ApprovalWorkflowDefinitionId AND wi.EntityId = i.Id AND wi.IsDeleted = 0
                        LEFT JOIN dbo.WorkflowEntityTypes wit ON wit.Id = wi.EntityTypeId AND wit.TenantId = i.TenantId
                            AND wit.Code = 'QS_CLAIM' AND wit.IsActive = 1 AND wit.IsDeleted = 0
                        WHERE (i.Status = 'PendingApproval' AND (wi.Id IS NULL OR wit.Id IS NULL OR wi.Status NOT IN (0,1,5,6)))
                           OR (i.Status = 'Approved' AND (wi.Id IS NULL OR wit.Id IS NULL OR wi.Status <> 2
                               OR i.ApprovedById = i.SubmittedById OR i.ApprovedById = i.QsVettedById
                               OR i.ApprovedAmount <> i.QsAssessedAmount OR i.RejectedAmount <> ROUND(i.ClaimedAmount - i.ApprovedAmount, 2)))
                           OR (i.Status = 'Rejected' AND (wi.Id IS NULL OR wit.Id IS NULL OR wi.Status NOT IN (3,4)
                               OR i.ApprovedAmount <> 0 OR i.RejectedAmount <> i.ClaimedAmount)))
                        THROW 51993, 'QS_CLAIM_WORKFLOW_INVALID: exact shared-workflow outcome, server-derived amounts and independent approval are required.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_QS0509_ContractClaimEvidence_AppendOnly]
                ON [dbo].[QuantitySurveyContractClaimEvidence]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51994, 'QS_CLAIM_EVIDENCE_IMMUTABLE: contractor-claim evidence is append-only.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN dbo.QuantitySurveyContractClaims c ON c.Id = i.ContractClaimId AND c.TenantId = i.TenantId
                            AND c.Status IN ('Draft','Rejected') AND c.IsDeleted = 0
                        LEFT JOIN dbo.FileUploadRecords f ON f.Id = i.FileUploadRecordId AND f.TenantId = i.TenantId
                            AND f.VirusScanStatus = 2 AND f.IsDeleted = 0
                        LEFT JOIN dbo.CentralDocumentRecords r ON r.Id = i.CentralDocumentRecordId AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                        LEFT JOIN dbo.CentralDocumentVersions v ON v.Id = i.CentralDocumentVersionId AND v.TenantId = i.TenantId
                            AND v.DocumentRecordId = r.Id AND v.FileUploadRecordId = f.Id AND v.IsDeleted = 0
                        WHERE c.Id IS NULL OR f.Id IS NULL OR r.Id IS NULL OR v.Id IS NULL
                           OR i.ClientRequestId = '00000000-0000-0000-0000-000000000000'
                           OR LEN(i.RequestHash) <> 64 OR LEN(i.ChecksumSha256) <> 64 OR i.FileSize <= 0)
                        THROW 51995, 'QS_CLAIM_EVIDENCE_LINEAGE_INVALID: clean central-DMS evidence and tenant lineage are required.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_QS0509_ContractClaimRevisions_AppendOnly]
                ON [dbo].[QuantitySurveyContractClaimRevisions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51996, 'QS_CLAIM_REVISION_IMMUTABLE: contractor-claim revisions are append-only.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN dbo.QuantitySurveyContractClaims c ON c.Id = i.ContractClaimId AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                        LEFT JOIN dbo.Users u ON u.Id = i.ActorUserId AND u.TenantId = i.TenantId
                        LEFT JOIN dbo.BusinessPartners bp ON bp.Id = i.ActorBusinessPartnerId AND bp.TenantId = i.TenantId AND bp.IsDeleted = 0
                        WHERE c.Id IS NULL OR u.Id IS NULL OR (i.ActorBusinessPartnerId IS NOT NULL AND bp.Id IS NULL)
                           OR i.ClientRequestId = '00000000-0000-0000-0000-000000000000' OR LEN(i.RequestHash) <> 64
                           OR NULLIF(LTRIM(RTRIM(i.Action)), '') IS NULL OR NULLIF(LTRIM(RTRIM(i.CorrelationId)), '') IS NULL)
                        THROW 51997, 'QS_CLAIM_REVISION_LINEAGE_INVALID: tenant, actor, partner, idempotency and correlation lineage are required.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0509_ContractClaimRevisions_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0509_ContractClaimEvidence_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0509_ContractClaims_Governance];");

            migrationBuilder.DropTable(
                name: "QuantitySurveyContractClaimEvidence");

            migrationBuilder.DropTable(
                name: "QuantitySurveyContractClaimRevisions");

            migrationBuilder.DropTable(
                name: "QuantitySurveyContractClaims");
        }
    }
}
