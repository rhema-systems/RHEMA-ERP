using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantitySurveyInterimValuationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_QsValuationWorksheets_Status",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "QuantitySurveyValuationWorksheets",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovalWorkflowDefinitionId",
                table: "QuantitySurveyValuationWorksheets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "QuantitySurveyValuationWorksheets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedById",
                table: "QuantitySurveyValuationWorksheets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CertificateReady",
                table: "QuantitySurveyValuationWorksheets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CertificateReadyAt",
                table: "QuantitySurveyValuationWorksheets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConfigurationProfileId",
                table: "QuantitySurveyValuationWorksheets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsultantAttestation",
                table: "QuantitySurveyValuationWorksheets",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConsultantBusinessPartnerId",
                table: "QuantitySurveyValuationWorksheets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsultantEndorsedAt",
                table: "QuantitySurveyValuationWorksheets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConsultantEndorsedById",
                table: "QuantitySurveyValuationWorksheets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsultantEndorsedByName",
                table: "QuantitySurveyValuationWorksheets",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ConsultantEndorsementRequired",
                table: "QuantitySurveyValuationWorksheets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ConsultantSignatureHash",
                table: "QuantitySurveyValuationWorksheets",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractorAttestation",
                table: "QuantitySurveyValuationWorksheets",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContractorBusinessPartnerId",
                table: "QuantitySurveyValuationWorksheets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractorSignatureHash",
                table: "QuantitySurveyValuationWorksheets",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ContractorSubmissionRequired",
                table: "QuantitySurveyValuationWorksheets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContractorSubmittedAt",
                table: "QuantitySurveyValuationWorksheets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContractorSubmittedById",
                table: "QuantitySurveyValuationWorksheets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractorSubmittedByName",
                table: "QuantitySurveyValuationWorksheets",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceMetadataTemplateCodeSnapshot",
                table: "QuantitySurveyValuationWorksheets",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EvidenceMetadataTemplateId",
                table: "QuantitySurveyValuationWorksheets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExternalSignatureRequired",
                table: "QuantitySurveyValuationWorksheets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ExternalSubmissionDecisionId",
                table: "QuantitySurveyValuationWorksheets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyHash",
                table: "QuantitySurveyValuationWorksheets",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PortalIdentityRequired",
                table: "QuantitySurveyValuationWorksheets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "QsReviewNote",
                table: "QuantitySurveyValuationWorksheets",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QsVettedAt",
                table: "QuantitySurveyValuationWorksheets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "QsVettedById",
                table: "QuantitySurveyValuationWorksheets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "QuantitySurveyValuationWorksheets",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SupportingEvidenceRequired",
                table: "QuantitySurveyValuationWorksheets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ValuationDecisionId",
                table: "QuantitySurveyValuationWorksheets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowInstanceId",
                table: "QuantitySurveyValuationWorksheets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActorBusinessPartnerId",
                table: "QuantitySurveyValuationWorksheetRevisions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QuantitySurveyValuationWorksheetEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorksheetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    EvidenceType = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ChecksumSha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CentralDocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyValuationWorksheetEvidence", x => x.Id);
                    table.CheckConstraint("CK_QsValuationEvidence_File", "[FileSize] > 0 AND LEN([ChecksumSha256]) = 64 AND LEN([RequestHash]) = 64");
                    table.CheckConstraint("CK_QsValuationEvidence_Type", "[EvidenceType] IN (0,1,2,3,4)");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyValuationWorksheetEvidence_CentralDocumentRecords_CentralDocumentRecordId",
                        column: x => x.CentralDocumentRecordId,
                        principalTable: "CentralDocumentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyValuationWorksheetEvidence_CentralDocumentVersions_CentralDocumentVersionId",
                        column: x => x.CentralDocumentVersionId,
                        principalTable: "CentralDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyValuationWorksheetEvidence_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyValuationWorksheetEvidence_QuantitySurveyValuationWorksheets_WorksheetId",
                        column: x => x.WorksheetId,
                        principalTable: "QuantitySurveyValuationWorksheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyValuationWorksheetEvidence_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyValuationWorksheetEvidence_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyValuationWorksheets_ApprovedById",
                table: "QuantitySurveyValuationWorksheets",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyValuationWorksheets_ConfigurationProfileId",
                table: "QuantitySurveyValuationWorksheets",
                column: "ConfigurationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyValuationWorksheets_ConsultantBusinessPartnerId",
                table: "QuantitySurveyValuationWorksheets",
                column: "ConsultantBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyValuationWorksheets_ConsultantEndorsedById",
                table: "QuantitySurveyValuationWorksheets",
                column: "ConsultantEndorsedById");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyValuationWorksheets_ContractorBusinessPartnerId",
                table: "QuantitySurveyValuationWorksheets",
                column: "ContractorBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyValuationWorksheets_ContractorSubmittedById",
                table: "QuantitySurveyValuationWorksheets",
                column: "ContractorSubmittedById");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyValuationWorksheets_EvidenceMetadataTemplateId",
                table: "QuantitySurveyValuationWorksheets",
                column: "EvidenceMetadataTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyValuationWorksheets_ExternalSubmissionDecisionId",
                table: "QuantitySurveyValuationWorksheets",
                column: "ExternalSubmissionDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyValuationWorksheets_QsVettedById",
                table: "QuantitySurveyValuationWorksheets",
                column: "QsVettedById");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyValuationWorksheets_ValuationDecisionId",
                table: "QuantitySurveyValuationWorksheets",
                column: "ValuationDecisionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_QsValuationWorksheets_Approval",
                table: "QuantitySurveyValuationWorksheets",
                sql: "[ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_QsValuationWorksheets_Lifecycle",
                table: "QuantitySurveyValuationWorksheets",
                sql: "([Status] = 'Draft' AND [WorkflowInstanceId] IS NULL AND [ApprovedAt] IS NULL AND [CertificateReady] = 0) OR ([Status] IN ('ContractorSubmitted','UnderQsReview','QsVetted','ConsultantEndorsed') AND [WorkflowInstanceId] IS NULL AND [ApprovedAt] IS NULL AND [CertificateReady] = 0) OR ([Status] = 'PendingApproval' AND [WorkflowInstanceId] IS NOT NULL AND [ApprovedAt] IS NULL AND [CertificateReady] = 0) OR ([Status] = 'Approved' AND [WorkflowInstanceId] IS NOT NULL AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [CertificateReady] = 1 AND [CertificateReadyAt] IS NOT NULL) OR ([Status] = 'Rejected' AND [WorkflowInstanceId] IS NOT NULL AND [RejectionReason] IS NOT NULL AND [CertificateReady] = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_QsValuationWorksheets_Policy",
                table: "QuantitySurveyValuationWorksheets",
                sql: "([ConfigurationProfileId] IS NULL AND [ValuationDecisionId] IS NULL AND [ExternalSubmissionDecisionId] IS NULL AND [ApprovalWorkflowDefinitionId] IS NULL AND [EvidenceMetadataTemplateId] IS NULL AND [PolicyHash] IS NULL) OR ([ConfigurationProfileId] IS NOT NULL AND [ValuationDecisionId] IS NOT NULL AND [ExternalSubmissionDecisionId] IS NOT NULL AND [ApprovalWorkflowDefinitionId] IS NOT NULL AND [EvidenceMetadataTemplateId] IS NOT NULL AND LEN([PolicyHash]) = 64)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_QsValuationWorksheets_Status",
                table: "QuantitySurveyValuationWorksheets",
                sql: "[Status] IN ('Draft','ContractorSubmitted','UnderQsReview','QsVetted','ConsultantEndorsed','PendingApproval','Approved','Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_QsValuationWorksheets_StateAlignment",
                table: "QuantitySurveyValuationWorksheets",
                sql: "([Status] IN ('Draft','ContractorSubmitted','UnderQsReview','QsVetted','ConsultantEndorsed') AND [ApprovalStatus] = 'Draft') OR ([Status] = 'PendingApproval' AND [ApprovalStatus] = 'Pending') OR ([Status] = 'Approved' AND [ApprovalStatus] = 'Approved') OR ([Status] = 'Rejected' AND [ApprovalStatus] = 'Rejected')");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyValuationWorksheetRevisions_ActorBusinessPartnerId",
                table: "QuantitySurveyValuationWorksheetRevisions",
                column: "ActorBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyValuationWorksheetEvidence_CentralDocumentRecordId",
                table: "QuantitySurveyValuationWorksheetEvidence",
                column: "CentralDocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyValuationWorksheetEvidence_CentralDocumentVersionId",
                table: "QuantitySurveyValuationWorksheetEvidence",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyValuationWorksheetEvidence_FileUploadRecordId",
                table: "QuantitySurveyValuationWorksheetEvidence",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyValuationWorksheetEvidence_TenantId_CentralDocumentVersionId",
                table: "QuantitySurveyValuationWorksheetEvidence",
                columns: new[] { "TenantId", "CentralDocumentVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyValuationWorksheetEvidence_TenantId_ClientRequestId",
                table: "QuantitySurveyValuationWorksheetEvidence",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyValuationWorksheetEvidence_TenantId_WorksheetId_EvidenceType_UploadedAt",
                table: "QuantitySurveyValuationWorksheetEvidence",
                columns: new[] { "TenantId", "WorksheetId", "EvidenceType", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyValuationWorksheetEvidence_UploadedById",
                table: "QuantitySurveyValuationWorksheetEvidence",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyValuationWorksheetEvidence_WorksheetId",
                table: "QuantitySurveyValuationWorksheetEvidence",
                column: "WorksheetId");

            migrationBuilder.AddForeignKey(
                name: "FK_QuantitySurveyValuationWorksheetRevisions_BusinessPartners_ActorBusinessPartnerId",
                table: "QuantitySurveyValuationWorksheetRevisions",
                column: "ActorBusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuantitySurveyValuationWorksheets_BusinessPartners_ConsultantBusinessPartnerId",
                table: "QuantitySurveyValuationWorksheets",
                column: "ConsultantBusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuantitySurveyValuationWorksheets_BusinessPartners_ContractorBusinessPartnerId",
                table: "QuantitySurveyValuationWorksheets",
                column: "ContractorBusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuantitySurveyValuationWorksheets_CentralDocumentMetadataTemplates_EvidenceMetadataTemplateId",
                table: "QuantitySurveyValuationWorksheets",
                column: "EvidenceMetadataTemplateId",
                principalTable: "CentralDocumentMetadataTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuantitySurveyValuationWorksheets_QuantitySurveyConfigurationDecisions_ExternalSubmissionDecisionId",
                table: "QuantitySurveyValuationWorksheets",
                column: "ExternalSubmissionDecisionId",
                principalTable: "QuantitySurveyConfigurationDecisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuantitySurveyValuationWorksheets_QuantitySurveyConfigurationDecisions_ValuationDecisionId",
                table: "QuantitySurveyValuationWorksheets",
                column: "ValuationDecisionId",
                principalTable: "QuantitySurveyConfigurationDecisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuantitySurveyValuationWorksheets_QuantitySurveyConfigurationProfiles_ConfigurationProfileId",
                table: "QuantitySurveyValuationWorksheets",
                column: "ConfigurationProfileId",
                principalTable: "QuantitySurveyConfigurationProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuantitySurveyValuationWorksheets_Users_ApprovedById",
                table: "QuantitySurveyValuationWorksheets",
                column: "ApprovedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuantitySurveyValuationWorksheets_Users_ConsultantEndorsedById",
                table: "QuantitySurveyValuationWorksheets",
                column: "ConsultantEndorsedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuantitySurveyValuationWorksheets_Users_ContractorSubmittedById",
                table: "QuantitySurveyValuationWorksheets",
                column: "ContractorSubmittedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuantitySurveyValuationWorksheets_Users_QsVettedById",
                table: "QuantitySurveyValuationWorksheets",
                column: "QsVettedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
CREATE OR ALTER TRIGGER [dbo].[TR_QsValuationWorksheets_Guard]
ON [dbo].[QuantitySurveyValuationWorksheets]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL)
        THROW 51960, 'QS_VALUATION_DELETE_BLOCKED: interim valuation worksheets are retained as governed history.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id
        WHERE NOT (
            i.Status=d.Status OR
            (d.Status='Draft' AND i.Status IN ('ContractorSubmitted','QsVetted')) OR
            (d.Status='ContractorSubmitted' AND i.Status IN ('UnderQsReview','QsVetted')) OR
            (d.Status='UnderQsReview' AND i.Status='QsVetted') OR
            (d.Status='QsVetted' AND i.Status IN ('ConsultantEndorsed','PendingApproval')) OR
            (d.Status='ConsultantEndorsed' AND i.Status='PendingApproval') OR
            (d.Status='PendingApproval' AND i.Status IN ('Approved','Rejected'))))
        THROW 51961, 'QS_VALUATION_TRANSITION_INVALID: the interim valuation lifecycle transition is not allowed.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i
        LEFT JOIN Projects p ON p.Id=i.ProjectId AND p.TenantId=i.TenantId AND p.IsDeleted=0
        LEFT JOIN ProjectInterimValuations v ON v.Id=i.ProjectInterimValuationId AND v.ProjectId=i.ProjectId AND v.TenantId=i.TenantId AND v.IsDeleted=0
        LEFT JOIN ProjectBoqVersions b ON b.Id=i.ProjectBoqVersionId AND b.ProjectId=i.ProjectId AND b.TenantId=i.TenantId AND b.IsDeleted=0
        LEFT JOIN Users prepared ON prepared.Id=i.PreparedById AND prepared.TenantId=i.TenantId
        LEFT JOIN Users submitter ON submitter.Id=i.ContractorSubmittedById AND submitter.TenantId=i.TenantId
        LEFT JOIN Users reviewer ON reviewer.Id=i.QsVettedById AND reviewer.TenantId=i.TenantId
        LEFT JOIN Users endorser ON endorser.Id=i.ConsultantEndorsedById AND endorser.TenantId=i.TenantId
        LEFT JOIN Users approver ON approver.Id=i.ApprovedById AND approver.TenantId=i.TenantId
        LEFT JOIN BusinessPartners contractor ON contractor.Id=i.ContractorBusinessPartnerId AND contractor.TenantId=i.TenantId AND contractor.IsDeleted=0
        LEFT JOIN BusinessPartners consultant ON consultant.Id=i.ConsultantBusinessPartnerId AND consultant.TenantId=i.TenantId AND consultant.IsDeleted=0
        LEFT JOIN BusinessPartnerUsers contractorLink ON contractorLink.TenantId=i.TenantId AND contractorLink.BusinessPartnerId=i.ContractorBusinessPartnerId AND contractorLink.UserId=i.ContractorSubmittedById AND contractorLink.IsActive=1 AND contractorLink.IsDeleted=0
        LEFT JOIN BusinessPartnerUsers consultantLink ON consultantLink.TenantId=i.TenantId AND consultantLink.BusinessPartnerId=i.ConsultantBusinessPartnerId AND consultantLink.UserId=i.ConsultantEndorsedById AND consultantLink.IsActive=1 AND consultantLink.IsDeleted=0
        LEFT JOIN QuantitySurveyConfigurationProfiles cp ON cp.Id=i.ConfigurationProfileId AND cp.TenantId=i.TenantId AND cp.IsDeleted=0
        LEFT JOIN QuantitySurveyConfigurationDecisions vd ON vd.Id=i.ValuationDecisionId AND vd.ProfileId=cp.Id AND vd.TenantId=i.TenantId AND vd.DecisionKey='QS-DEC-008' AND vd.IsDeleted=0
        LEFT JOIN QuantitySurveyConfigurationDecisions ed ON ed.Id=i.ExternalSubmissionDecisionId AND ed.ProfileId=cp.Id AND ed.TenantId=i.TenantId AND ed.DecisionKey='QS-DEC-013' AND ed.IsDeleted=0
        LEFT JOIN CentralDocumentMetadataTemplates mt ON mt.Id=i.EvidenceMetadataTemplateId AND mt.TenantId=i.TenantId AND mt.IsDeleted=0
        LEFT JOIN WorkflowDefinitions wd ON wd.Id=i.ApprovalWorkflowDefinitionId AND wd.TenantId=i.TenantId AND wd.IsDeleted=0
        LEFT JOIN WorkflowEntityTypes wet ON wet.Id=wd.EntityTypeId AND wet.TenantId=i.TenantId AND wet.Code='QS_VALUATION' AND wet.IsDeleted=0
        WHERE p.Id IS NULL OR v.Id IS NULL OR b.Id IS NULL OR b.Status<>'Approved' OR b.PublishedAt IS NULL OR prepared.Id IS NULL
           OR (i.ContractorBusinessPartnerId IS NOT NULL AND contractor.Id IS NULL)
           OR (i.ConsultantBusinessPartnerId IS NOT NULL AND consultant.Id IS NULL)
           OR (i.ContractorBusinessPartnerId IS NOT NULL AND i.ContractorBusinessPartnerId=i.ConsultantBusinessPartnerId)
           OR (i.ContractorSubmittedById IS NOT NULL AND (submitter.Id IS NULL OR contractorLink.Id IS NULL))
           OR (i.QsVettedById IS NOT NULL AND reviewer.Id IS NULL)
           OR (i.ConsultantEndorsedById IS NOT NULL AND (endorser.Id IS NULL OR consultantLink.Id IS NULL))
           OR (i.ApprovedById IS NOT NULL AND approver.Id IS NULL)
           OR (i.ConfigurationProfileId IS NOT NULL AND (cp.Id IS NULL OR vd.Id IS NULL OR ed.Id IS NULL OR mt.Id IS NULL OR wd.Id IS NULL OR wet.Id IS NULL)))
        THROW 51962, 'QS_VALUATION_LINEAGE_INVALID: tenant, project, valuation, approved BoQ, actor, contractor, or consultant lineage is invalid.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id
        WHERE i.Status<>'Draft' AND (
            i.TenantId<>d.TenantId OR i.ProjectId<>d.ProjectId OR i.ProjectInterimValuationId<>d.ProjectInterimValuationId OR
            i.ProjectBoqVersionId<>d.ProjectBoqVersionId OR i.PreparedById<>d.PreparedById OR
            ISNULL(i.ContractorBusinessPartnerId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.ContractorBusinessPartnerId,'00000000-0000-0000-0000-000000000000') OR
            ISNULL(i.ConsultantBusinessPartnerId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.ConsultantBusinessPartnerId,'00000000-0000-0000-0000-000000000000') OR
            ISNULL(i.ConfigurationProfileId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.ConfigurationProfileId,'00000000-0000-0000-0000-000000000000') OR
            ISNULL(i.ValuationDecisionId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.ValuationDecisionId,'00000000-0000-0000-0000-000000000000') OR
            ISNULL(i.ExternalSubmissionDecisionId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.ExternalSubmissionDecisionId,'00000000-0000-0000-0000-000000000000') OR
            ISNULL(i.ApprovalWorkflowDefinitionId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.ApprovalWorkflowDefinitionId,'00000000-0000-0000-0000-000000000000') OR
            ISNULL(i.EvidenceMetadataTemplateId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.EvidenceMetadataTemplateId,'00000000-0000-0000-0000-000000000000') OR
            ISNULL(i.PolicyHash,'')<>ISNULL(d.PolicyHash,'')))
        THROW 51963, 'QS_VALUATION_POLICY_IMMUTABLE: subject, partner, and frozen configuration lineage cannot change after submission.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i
        WHERE (i.Status<>'Draft' AND (i.ConfigurationProfileId IS NULL OR i.ValuationDecisionId IS NULL OR
               i.ExternalSubmissionDecisionId IS NULL OR i.ApprovalWorkflowDefinitionId IS NULL OR
               i.EvidenceMetadataTemplateId IS NULL OR LEN(ISNULL(i.PolicyHash,''))<>64))
           OR (i.ContractorSubmissionRequired=1 AND i.Status NOT IN ('Draft') AND
               (i.ContractorBusinessPartnerId IS NULL OR i.ContractorSubmittedById IS NULL OR i.ContractorSubmittedAt IS NULL OR LEN(ISNULL(i.ContractorSignatureHash,''))<>64))
           OR (i.Status IN ('QsVetted','ConsultantEndorsed','PendingApproval','Approved','Rejected') AND
               (i.QsVettedById IS NULL OR i.QsVettedAt IS NULL OR LEN(LTRIM(RTRIM(ISNULL(i.QsReviewNote,''))))<5))
           OR (i.ConsultantEndorsementRequired=1 AND i.Status IN ('PendingApproval','Approved','Rejected') AND
               (i.ConsultantBusinessPartnerId IS NULL OR i.ConsultantEndorsedById IS NULL OR i.ConsultantEndorsedAt IS NULL OR LEN(ISNULL(i.ConsultantSignatureHash,''))<>64))
           OR (i.Status='ConsultantEndorsed' AND
               (i.ConsultantBusinessPartnerId IS NULL OR i.ConsultantEndorsedById IS NULL OR i.ConsultantEndorsedAt IS NULL OR LEN(ISNULL(i.ConsultantSignatureHash,''))<>64))
           OR (i.Status IN ('PendingApproval','Approved','Rejected') AND i.SupportingEvidenceRequired=1 AND NOT EXISTS (
               SELECT 1 FROM QuantitySurveyValuationWorksheetEvidence e WHERE e.TenantId=i.TenantId AND e.WorksheetId=i.Id AND e.IsDeleted=0)))
        THROW 51964, 'QS_VALUATION_READINESS_BLOCKED: configured contractor, QS, consultant, or supporting-evidence controls are incomplete.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i
        LEFT JOIN WorkflowInstances wi ON wi.Id=i.WorkflowInstanceId AND wi.TenantId=i.TenantId AND wi.EntityId=i.Id AND wi.IsDeleted=0
        LEFT JOIN WorkflowEntityTypes wet ON wet.Id=wi.EntityTypeId AND wet.TenantId=i.TenantId AND wet.Code='QS_VALUATION' AND wet.IsDeleted=0
        WHERE i.Status IN ('PendingApproval','Approved','Rejected') AND (
            wi.Id IS NULL OR wet.Id IS NULL OR wi.WorkflowDefinitionId<>i.ApprovalWorkflowDefinitionId OR
            (i.Status='PendingApproval' AND wi.Status NOT IN (0,1,2)) OR
            (i.Status='Approved' AND wi.Status<>2) OR
            (i.Status='Rejected' AND wi.Status NOT IN (3,4))))
        THROW 51965, 'QS_VALUATION_WORKFLOW_INVALID: the exact shared-workflow instance and outcome do not support this state.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i
        WHERE i.Status='Approved' AND (i.ApprovedById=i.PreparedById OR i.ApprovedById=i.ContractorSubmittedById OR
              i.ApprovedById=i.QsVettedById OR i.ApprovedById=i.ConsultantEndorsedById))
        THROW 51966, 'QS_VALUATION_SOD_BLOCKED: an independent approver is required.', 1;
END
""");

            migrationBuilder.Sql("""
CREATE OR ALTER TRIGGER [dbo].[TR_QsValuationWorksheetLines_Guard]
ON [dbo].[QuantitySurveyValuationWorksheetLines]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL)
        THROW 51120, 'Valuation worksheet lines cannot be deleted outside their governed lifecycle.', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i
        LEFT JOIN QuantitySurveyValuationWorksheets w ON w.Id=i.WorksheetId AND w.TenantId=i.TenantId AND w.IsDeleted=0
        LEFT JOIN ProjectBoqVersionLines l ON l.Id=i.ProjectBoqVersionLineId AND l.ProjectBoqVersionId=w.ProjectBoqVersionId AND l.ProjectId=w.ProjectId AND l.TenantId=i.TenantId AND l.LineKey=i.BoqLineKey AND l.IsDeleted=0
        WHERE w.Id IS NULL OR l.Id IS NULL)
        THROW 51120, 'Invalid valuation worksheet line tenant, worksheet, approved BoQ, project, or stable-line lineage.', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id
        JOIN QuantitySurveyValuationWorksheets w ON w.Id=i.WorksheetId AND w.TenantId=i.TenantId
        WHERE i.TenantId<>d.TenantId OR i.WorksheetId<>d.WorksheetId OR i.ProjectBoqVersionLineId<>d.ProjectBoqVersionLineId OR
              i.BoqLineKey<>d.BoqLineKey OR i.Sequence<>d.Sequence OR i.BoqQuantitySnapshot<>d.BoqQuantitySnapshot OR
              i.UnitRateSnapshot<>d.UnitRateSnapshot OR i.MeasuredToDateQuantity<>d.MeasuredToDateQuantity OR
              i.PreviouslyCertifiedQuantity<>d.PreviouslyCertifiedQuantity OR i.PreviouslyCertifiedValue<>d.PreviouslyCertifiedValue OR
              i.PreviousRetentionValue<>d.PreviousRetentionValue OR
              (w.Status<>'Draft' AND i.CurrentClaimedQuantity<>d.CurrentClaimedQuantity) OR
              (w.Status NOT IN ('Draft','ContractorSubmitted','UnderQsReview') AND (
                  i.CurrentCertifiedQuantity<>d.CurrentCertifiedQuantity OR ISNULL(i.ReviewNote,'')<>ISNULL(d.ReviewNote,''))))
        THROW 51967, 'QS_VALUATION_LINE_IMMUTABLE: source, contractor claim, or vetted values cannot be altered outside their permitted stage.', 1;
END
""");

            migrationBuilder.Sql("""
CREATE OR ALTER TRIGGER [dbo].[TR_QsValuationWorksheetEvidence_AppendOnly]
ON [dbo].[QuantitySurveyValuationWorksheetEvidence]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted)
        THROW 51968, 'QS_VALUATION_EVIDENCE_IMMUTABLE: evidence records are append-only.', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i
        LEFT JOIN QuantitySurveyValuationWorksheets w ON w.Id=i.WorksheetId AND w.TenantId=i.TenantId AND w.IsDeleted=0
        LEFT JOIN FileUploadRecords f ON f.Id=i.FileUploadRecordId AND f.TenantId=i.TenantId AND f.IsDeleted=0 AND f.VirusScanStatus=2
        LEFT JOIN CentralDocumentRecords r ON r.Id=i.CentralDocumentRecordId AND r.TenantId=i.TenantId AND r.IsDeleted=0 AND r.SourceModule='QuantitySurvey' AND r.SourceRecordId=i.Id
        LEFT JOIN CentralDocumentVersions v ON v.Id=i.CentralDocumentVersionId AND v.TenantId=i.TenantId AND v.IsDeleted=0 AND v.DocumentRecordId=r.Id AND v.FileUploadRecordId=f.Id AND v.Status='Validated'
        LEFT JOIN Users u ON u.Id=i.UploadedById AND u.TenantId=i.TenantId
        WHERE w.Id IS NULL OR f.Id IS NULL OR r.Id IS NULL OR v.Id IS NULL OR u.Id IS NULL OR
              w.Status IN ('PendingApproval','Approved','Rejected'))
        THROW 51969, 'QS_VALUATION_EVIDENCE_LINEAGE_INVALID: clean central-DMS evidence and tenant lineage are required before approval.', 1;
END
""");

            migrationBuilder.Sql("""
CREATE OR ALTER TRIGGER [dbo].[TR_QsValuationWorksheetRevisions_AppendOnly]
ON [dbo].[QuantitySurveyValuationWorksheetRevisions]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted)
        THROW 51122, 'Valuation worksheet revisions are append-only.', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i
        LEFT JOIN QuantitySurveyValuationWorksheets w ON w.Id=i.WorksheetId AND w.TenantId=i.TenantId AND w.IsDeleted=0
        LEFT JOIN Users u ON u.Id=i.ActorUserId AND u.TenantId=i.TenantId
        LEFT JOIN BusinessPartners bp ON bp.Id=i.ActorBusinessPartnerId AND bp.TenantId=i.TenantId AND bp.IsDeleted=0
        WHERE w.Id IS NULL OR u.Id IS NULL OR (i.ActorBusinessPartnerId IS NOT NULL AND bp.Id IS NULL))
        THROW 51969, 'QS_VALUATION_REVISION_LINEAGE_INVALID: revision actor, partner, worksheet, and tenant lineage are required.', 1;
END
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QsValuationWorksheetEvidence_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QsValuationWorksheets_Guard];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QsValuationWorksheetLines_Guard];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QsValuationWorksheetRevisions_AppendOnly];");
            migrationBuilder.DropForeignKey(
                name: "FK_QuantitySurveyValuationWorksheetRevisions_BusinessPartners_ActorBusinessPartnerId",
                table: "QuantitySurveyValuationWorksheetRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_QuantitySurveyValuationWorksheets_BusinessPartners_ConsultantBusinessPartnerId",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropForeignKey(
                name: "FK_QuantitySurveyValuationWorksheets_BusinessPartners_ContractorBusinessPartnerId",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropForeignKey(
                name: "FK_QuantitySurveyValuationWorksheets_CentralDocumentMetadataTemplates_EvidenceMetadataTemplateId",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropForeignKey(
                name: "FK_QuantitySurveyValuationWorksheets_QuantitySurveyConfigurationDecisions_ExternalSubmissionDecisionId",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropForeignKey(
                name: "FK_QuantitySurveyValuationWorksheets_QuantitySurveyConfigurationDecisions_ValuationDecisionId",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropForeignKey(
                name: "FK_QuantitySurveyValuationWorksheets_QuantitySurveyConfigurationProfiles_ConfigurationProfileId",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropForeignKey(
                name: "FK_QuantitySurveyValuationWorksheets_Users_ApprovedById",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropForeignKey(
                name: "FK_QuantitySurveyValuationWorksheets_Users_ConsultantEndorsedById",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropForeignKey(
                name: "FK_QuantitySurveyValuationWorksheets_Users_ContractorSubmittedById",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropForeignKey(
                name: "FK_QuantitySurveyValuationWorksheets_Users_QsVettedById",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropTable(
                name: "QuantitySurveyValuationWorksheetEvidence");

            migrationBuilder.DropIndex(
                name: "IX_QuantitySurveyValuationWorksheets_ApprovedById",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropIndex(
                name: "IX_QuantitySurveyValuationWorksheets_ConfigurationProfileId",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropIndex(
                name: "IX_QuantitySurveyValuationWorksheets_ConsultantBusinessPartnerId",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropIndex(
                name: "IX_QuantitySurveyValuationWorksheets_ConsultantEndorsedById",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropIndex(
                name: "IX_QuantitySurveyValuationWorksheets_ContractorBusinessPartnerId",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropIndex(
                name: "IX_QuantitySurveyValuationWorksheets_ContractorSubmittedById",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropIndex(
                name: "IX_QuantitySurveyValuationWorksheets_EvidenceMetadataTemplateId",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropIndex(
                name: "IX_QuantitySurveyValuationWorksheets_ExternalSubmissionDecisionId",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropIndex(
                name: "IX_QuantitySurveyValuationWorksheets_QsVettedById",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropIndex(
                name: "IX_QuantitySurveyValuationWorksheets_ValuationDecisionId",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_QsValuationWorksheets_Approval",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_QsValuationWorksheets_Lifecycle",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_QsValuationWorksheets_Policy",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_QsValuationWorksheets_Status",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_QsValuationWorksheets_StateAlignment",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropIndex(
                name: "IX_QuantitySurveyValuationWorksheetRevisions_ActorBusinessPartnerId",
                table: "QuantitySurveyValuationWorksheetRevisions");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ApprovalWorkflowDefinitionId",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "CertificateReady",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "CertificateReadyAt",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ConfigurationProfileId",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ConsultantAttestation",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ConsultantBusinessPartnerId",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ConsultantEndorsedAt",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ConsultantEndorsedById",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ConsultantEndorsedByName",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ConsultantEndorsementRequired",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ConsultantSignatureHash",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ContractorAttestation",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ContractorBusinessPartnerId",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ContractorSignatureHash",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ContractorSubmissionRequired",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ContractorSubmittedAt",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ContractorSubmittedById",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ContractorSubmittedByName",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "EvidenceMetadataTemplateCodeSnapshot",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "EvidenceMetadataTemplateId",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ExternalSignatureRequired",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ExternalSubmissionDecisionId",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "PolicyHash",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "PortalIdentityRequired",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "QsReviewNote",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "QsVettedAt",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "QsVettedById",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "SupportingEvidenceRequired",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ValuationDecisionId",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "WorkflowInstanceId",
                table: "QuantitySurveyValuationWorksheets");

            migrationBuilder.DropColumn(
                name: "ActorBusinessPartnerId",
                table: "QuantitySurveyValuationWorksheetRevisions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_QsValuationWorksheets_Status",
                table: "QuantitySurveyValuationWorksheets",
                sql: "[Status] = 'Draft'");

            migrationBuilder.Sql("""
CREATE OR ALTER TRIGGER [dbo].[TR_QsValuationWorksheets_Guard]
ON [dbo].[QuantitySurveyValuationWorksheets]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL)
        THROW 51120, 'Valuation worksheets cannot be deleted outside their governed lifecycle.', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i
        LEFT JOIN Projects p ON p.Id=i.ProjectId AND p.TenantId=i.TenantId AND p.IsDeleted=0
        LEFT JOIN ProjectInterimValuations v ON v.Id=i.ProjectInterimValuationId AND v.ProjectId=i.ProjectId AND v.TenantId=i.TenantId AND v.IsDeleted=0
        LEFT JOIN ProjectBoqVersions b ON b.Id=i.ProjectBoqVersionId AND b.ProjectId=i.ProjectId AND b.TenantId=i.TenantId AND b.IsDeleted=0
        LEFT JOIN Users u ON u.Id=i.PreparedById AND u.TenantId=i.TenantId
        WHERE p.Id IS NULL OR v.Id IS NULL OR v.Status<>'Draft' OR b.Id IS NULL OR b.Status<>'Approved' OR b.PublishedAt IS NULL OR u.Id IS NULL)
        THROW 51120, 'Invalid valuation worksheet tenant, project, interim valuation, approved BoQ, or actor lineage.', 1;
END
""");
            migrationBuilder.Sql("""
CREATE OR ALTER TRIGGER [dbo].[TR_QsValuationWorksheetLines_Guard]
ON [dbo].[QuantitySurveyValuationWorksheetLines]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL)
        THROW 51120, 'Valuation worksheet lines cannot be deleted outside their governed lifecycle.', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i
        LEFT JOIN QuantitySurveyValuationWorksheets w ON w.Id=i.WorksheetId AND w.TenantId=i.TenantId AND w.IsDeleted=0
        LEFT JOIN ProjectBoqVersionLines l ON l.Id=i.ProjectBoqVersionLineId AND l.ProjectBoqVersionId=w.ProjectBoqVersionId AND l.ProjectId=w.ProjectId AND l.TenantId=i.TenantId AND l.LineKey=i.BoqLineKey AND l.IsDeleted=0
        WHERE w.Id IS NULL OR l.Id IS NULL)
        THROW 51120, 'Invalid valuation worksheet line tenant, worksheet, approved BoQ, project, or stable-line lineage.', 1;
END
""");
            migrationBuilder.Sql("""
CREATE OR ALTER TRIGGER [dbo].[TR_QsValuationWorksheetRevisions_AppendOnly]
ON [dbo].[QuantitySurveyValuationWorksheetRevisions]
AFTER UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51122, 'Valuation worksheet revisions are append-only.', 1;
END
""");
        }
    }
}
