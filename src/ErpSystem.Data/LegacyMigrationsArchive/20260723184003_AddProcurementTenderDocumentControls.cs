using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementTenderDocumentControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementTenderDocumentTemplateVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DocumentTypeCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicySetCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PolicySetVersion = table.Column<int>(type: "int", nullable: false),
                    SourceConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentReference = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ContentWorkflowEvidenceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContentFileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContentChecksumSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupersedesVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangeSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ApprovalEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RetiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetiredByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LifecycleSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementTenderDocumentTemplateVersions", x => x.Id);
                    table.CheckConstraint("CK_ProcurementTenderDocumentTemplateVersions_ContentEvidence", "([ContentWorkflowEvidenceDocumentId] IS NULL OR [ContentFileUploadRecordId] IS NULL)");
                    table.CheckConstraint("CK_ProcurementTenderDocumentTemplateVersions_State", "[Version] >= 1 AND [PolicySetVersion] >= 1 AND [Status] BETWEEN 0 AND 3 AND ([EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]) AND LEN([ContentChecksumSha256]) = 64 AND LEN([IntegrityHash]) = 64 AND ISJSON([LifecycleSnapshotJson]) = 1");
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentTemplateVersions_FileUploadRecords_ContentFileUploadRecordId",
                        column: x => x.ContentFileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentTemplateVersions_ProcurementConfigurationProfiles_SourceConfigurationProfileId",
                        column: x => x.SourceConfigurationProfileId,
                        principalTable: "ProcurementConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentTemplateVersions_ProcurementPolicySets_PolicySetId",
                        column: x => x.PolicySetId,
                        principalTable: "ProcurementPolicySets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentTemplateVersions_ProcurementTenderDocumentTemplateVersions_SupersedesVersionId",
                        column: x => x.SupersedesVersionId,
                        principalTable: "ProcurementTenderDocumentTemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentTemplateVersions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentTemplateVersions_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentTemplateVersions_WorkflowEvidenceDocuments_ContentWorkflowEvidenceDocumentId",
                        column: x => x.ContentWorkflowEvidenceDocumentId,
                        principalTable: "WorkflowEvidenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentTemplateVersions_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementTenderDocumentRegisters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestForQuotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourcingCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MethodRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    MethodRuleCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicySetCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PolicySetVersion = table.Column<int>(type: "int", nullable: false),
                    SourceConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InitialTemplateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalSubmissionDeadlineUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OpeningScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OriginalBidValidityUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FeeMode = table.Column<int>(type: "int", nullable: false),
                    FeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    BoundAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BoundByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LifecycleSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementTenderDocumentRegisters", x => x.Id);
                    table.CheckConstraint("CK_ProcurementTenderDocumentRegisters_Source", "([SourceType] = 0 AND [TenderId] IS NOT NULL AND [RequestForQuotationId] IS NULL) OR ([SourceType] = 1 AND [TenderId] IS NULL AND [RequestForQuotationId] IS NOT NULL)");
                    table.CheckConstraint("CK_ProcurementTenderDocumentRegisters_State", "[SourceType] BETWEEN 0 AND 1 AND [Method] BETWEEN 0 AND 8 AND [PolicySetVersion] >= 1 AND [OriginalBidValidityUntilUtc] > [OriginalSubmissionDeadlineUtc] AND ([OpeningScheduledAtUtc] IS NULL OR [OpeningScheduledAtUtc] >= [OriginalSubmissionDeadlineUtc]) AND (([FeeMode] = 0 AND [FeeAmount] = 0) OR ([FeeMode] = 1 AND [FeeAmount] > 0)) AND LEN([CurrencyCode]) = 3 AND LEN([CorrelationId]) > 0 AND LEN([IntegrityHash]) = 64 AND ISJSON([LifecycleSnapshotJson]) = 1");
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentRegisters_ProcurementConfigurationProfiles_SourceConfigurationProfileId",
                        column: x => x.SourceConfigurationProfileId,
                        principalTable: "ProcurementConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentRegisters_ProcurementPolicyMethodRules_MethodRuleId",
                        column: x => x.MethodRuleId,
                        principalTable: "ProcurementPolicyMethodRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentRegisters_ProcurementPolicySets_PolicySetId",
                        column: x => x.PolicySetId,
                        principalTable: "ProcurementPolicySets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentRegisters_ProcurementSourcingCases_SourcingCaseId",
                        column: x => x.SourcingCaseId,
                        principalTable: "ProcurementSourcingCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentRegisters_ProcurementTenderDocumentTemplateVersions_InitialTemplateVersionId",
                        column: x => x.InitialTemplateVersionId,
                        principalTable: "ProcurementTenderDocumentTemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentRegisters_RequestForQuotations_RequestForQuotationId",
                        column: x => x.RequestForQuotationId,
                        principalTable: "RequestForQuotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentRegisters_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentRegisters_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementTenderDocumentTemplateMethods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementTenderDocumentTemplateMethods", x => x.Id);
                    table.CheckConstraint("CK_ProcurementTenderDocumentTemplateMethods_Method", "[Method] BETWEEN 0 AND 8 AND LEN([IntegrityHash]) = 64");
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentTemplateMethods_ProcurementTenderDocumentTemplateVersions_TemplateVersionId",
                        column: x => x.TemplateVersionId,
                        principalTable: "ProcurementTenderDocumentTemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentTemplateMethods_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementTenderDocumentChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegisterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ChangeType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PreviousTemplateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewTemplateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreviousValueUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NewValueUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequiresAcknowledgement = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkflowOutcome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovalReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EvidenceWorkflowDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvidenceFileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DispatchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DispatchedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DispatchEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LifecycleSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementTenderDocumentChanges", x => x.Id);
                    table.CheckConstraint("CK_ProcurementTenderDocumentChanges_Kind", "([ChangeType] = 0 AND [PreviousTemplateVersionId] IS NOT NULL AND [NewTemplateVersionId] IS NOT NULL AND [PreviousTemplateVersionId] <> [NewTemplateVersionId] AND [PreviousValueUtc] IS NULL AND [NewValueUtc] IS NULL) OR ([ChangeType] IN (1, 2) AND [PreviousTemplateVersionId] IS NULL AND [NewTemplateVersionId] IS NULL AND [PreviousValueUtc] IS NOT NULL AND [NewValueUtc] IS NOT NULL AND [NewValueUtc] > [PreviousValueUtc])");
                    table.CheckConstraint("CK_ProcurementTenderDocumentChanges_State", "[Sequence] >= 1 AND [ChangeType] BETWEEN 0 AND 2 AND [Status] BETWEEN 0 AND 2 AND (([Status] = 0 AND [DecidedAtUtc] IS NULL AND [DecidedByUserId] IS NULL AND [WorkflowOutcome] IS NULL AND [ApprovalReference] IS NULL AND [DispatchedAtUtc] IS NULL AND [DispatchedByUserId] IS NULL AND [DispatchEvidenceReference] IS NULL) OR ([Status] = 1 AND [DecidedAtUtc] IS NOT NULL AND [DecidedByUserId] IS NOT NULL AND LEN(LTRIM(RTRIM(ISNULL([WorkflowOutcome], '')))) > 0 AND LEN(LTRIM(RTRIM(ISNULL([ApprovalReference], '')))) > 0 AND (([DispatchedAtUtc] IS NULL AND [DispatchedByUserId] IS NULL AND [DispatchEvidenceReference] IS NULL) OR ([DispatchedAtUtc] IS NOT NULL AND [DispatchedByUserId] IS NOT NULL AND LEN(LTRIM(RTRIM(ISNULL([DispatchEvidenceReference], '')))) > 0))) OR ([Status] = 2 AND [DecidedAtUtc] IS NOT NULL AND [DecidedByUserId] IS NOT NULL AND LEN(LTRIM(RTRIM(ISNULL([WorkflowOutcome], '')))) > 0 AND LEN(LTRIM(RTRIM(ISNULL([ApprovalReference], '')))) > 0 AND [DispatchedAtUtc] IS NULL AND [DispatchedByUserId] IS NULL AND [DispatchEvidenceReference] IS NULL)) AND LEN([CorrelationId]) > 0 AND LEN([IntegrityHash]) = 64 AND ISJSON([LifecycleSnapshotJson]) = 1 AND ([EvidenceWorkflowDocumentId] IS NULL OR [EvidenceFileUploadRecordId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentChanges_FileUploadRecords_EvidenceFileUploadRecordId",
                        column: x => x.EvidenceFileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentChanges_ProcurementTenderDocumentRegisters_RegisterId",
                        column: x => x.RegisterId,
                        principalTable: "ProcurementTenderDocumentRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentChanges_ProcurementTenderDocumentTemplateVersions_NewTemplateVersionId",
                        column: x => x.NewTemplateVersionId,
                        principalTable: "ProcurementTenderDocumentTemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentChanges_ProcurementTenderDocumentTemplateVersions_PreviousTemplateVersionId",
                        column: x => x.PreviousTemplateVersionId,
                        principalTable: "ProcurementTenderDocumentTemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentChanges_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentChanges_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentChanges_WorkflowEvidenceDocuments_EvidenceWorkflowDocumentId",
                        column: x => x.EvidenceWorkflowDocumentId,
                        principalTable: "WorkflowEvidenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentChanges_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementTenderDocumentIssuances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegisterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecipientKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    RecipientPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FeeMode = table.Column<int>(type: "int", nullable: false),
                    FeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IssueChannel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EvidenceWorkflowDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvidenceFileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IssuanceSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementTenderDocumentIssuances", x => x.Id);
                    table.CheckConstraint("CK_ProcurementTenderDocumentIssuances_Fee", "([FeeMode] = 0 AND [FeeAmount] = 0 AND [AmountPaid] = 0 AND [PaymentReference] IS NULL) OR ([FeeMode] = 1 AND [FeeAmount] > 0 AND [AmountPaid] = [FeeAmount] AND LEN(LTRIM(RTRIM(ISNULL([PaymentReference], '')))) > 0)");
                    table.CheckConstraint("CK_ProcurementTenderDocumentIssuances_State", "[FeeMode] BETWEEN 0 AND 1 AND LEN([RecipientKey]) = 64 AND LEN([CurrencyCode]) = 3 AND LEN([CorrelationId]) > 0 AND LEN([IntegrityHash]) = 64 AND ISJSON([IssuanceSnapshotJson]) = 1 AND ([EvidenceWorkflowDocumentId] IS NULL OR [EvidenceFileUploadRecordId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentIssuances_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentIssuances_FileUploadRecords_EvidenceFileUploadRecordId",
                        column: x => x.EvidenceFileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentIssuances_ProcurementTenderDocumentRegisters_RegisterId",
                        column: x => x.RegisterId,
                        principalTable: "ProcurementTenderDocumentRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentIssuances_ProcurementTenderDocumentTemplateVersions_TemplateVersionId",
                        column: x => x.TemplateVersionId,
                        principalTable: "ProcurementTenderDocumentTemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentIssuances_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentIssuances_WorkflowEvidenceDocuments_EvidenceWorkflowDocumentId",
                        column: x => x.EvidenceWorkflowDocumentId,
                        principalTable: "WorkflowEvidenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementTenderDocumentChangeRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    IssuanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenderBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestForQuotationQuoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecipientKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    RecipientPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DispatchChannel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DispatchReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DispatchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DispatchedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DispatchEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DispatchSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementTenderDocumentChangeRecipients", x => x.Id);
                    table.CheckConstraint("CK_ProcurementTenderDocumentChangeRecipients_Source", "([SourceType] = 0 AND [IssuanceId] IS NOT NULL AND [TenderBidId] IS NULL AND [RequestForQuotationQuoteId] IS NULL) OR ([SourceType] = 1 AND [IssuanceId] IS NULL AND [TenderBidId] IS NOT NULL AND [RequestForQuotationQuoteId] IS NULL) OR ([SourceType] = 2 AND [IssuanceId] IS NULL AND [TenderBidId] IS NULL AND [RequestForQuotationQuoteId] IS NOT NULL)");
                    table.CheckConstraint("CK_ProcurementTenderDocumentChangeRecipients_State", "[SourceType] BETWEEN 0 AND 2 AND LEN([RecipientKey]) = 64 AND LEN([IntegrityHash]) = 64 AND ISJSON([DispatchSnapshotJson]) = 1");
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentChangeRecipients_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentChangeRecipients_ProcurementTenderDocumentChanges_ChangeId",
                        column: x => x.ChangeId,
                        principalTable: "ProcurementTenderDocumentChanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentChangeRecipients_ProcurementTenderDocumentIssuances_IssuanceId",
                        column: x => x.IssuanceId,
                        principalTable: "ProcurementTenderDocumentIssuances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentChangeRecipients_RequestForQuotationQuotes_RequestForQuotationQuoteId",
                        column: x => x.RequestForQuotationQuoteId,
                        principalTable: "RequestForQuotationQuotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentChangeRecipients_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentChangeRecipients_TenderBids_TenderBidId",
                        column: x => x.TenderBidId,
                        principalTable: "TenderBids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementTenderDocumentAcknowledgements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangeRecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcknowledgedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AcknowledgementChannel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AcknowledgementReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AcknowledgementSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementTenderDocumentAcknowledgements", x => x.Id);
                    table.CheckConstraint("CK_ProcurementTenderDocumentAcknowledgements_State", "[Outcome] BETWEEN 0 AND 1 AND LEN([CorrelationId]) > 0 AND LEN([IntegrityHash]) = 64 AND ISJSON([AcknowledgementSnapshotJson]) = 1");
                    table.CheckConstraint("CK_ProcurementTenderDocumentAcknowledgements_Target", "([IssuanceId] IS NOT NULL AND [ChangeRecipientId] IS NULL) OR ([IssuanceId] IS NULL AND [ChangeRecipientId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentAcknowledgements_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentAcknowledgements_ProcurementTenderDocumentChangeRecipients_ChangeRecipientId",
                        column: x => x.ChangeRecipientId,
                        principalTable: "ProcurementTenderDocumentChangeRecipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentAcknowledgements_ProcurementTenderDocumentIssuances_IssuanceId",
                        column: x => x.IssuanceId,
                        principalTable: "ProcurementTenderDocumentIssuances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentAcknowledgements_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentAcknowledgements_BusinessPartnerId",
                table: "ProcurementTenderDocumentAcknowledgements",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentAcknowledgements_ChangeRecipientId",
                table: "ProcurementTenderDocumentAcknowledgements",
                column: "ChangeRecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentAcknowledgements_IssuanceId",
                table: "ProcurementTenderDocumentAcknowledgements",
                column: "IssuanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentAcknowledgements_TenantId_BusinessPartnerId",
                table: "ProcurementTenderDocumentAcknowledgements",
                columns: new[] { "TenantId", "BusinessPartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentAcknowledgements_TenantId_ChangeRecipientId",
                table: "ProcurementTenderDocumentAcknowledgements",
                columns: new[] { "TenantId", "ChangeRecipientId" },
                unique: true,
                filter: "[ChangeRecipientId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentAcknowledgements_TenantId_CorrelationId",
                table: "ProcurementTenderDocumentAcknowledgements",
                columns: new[] { "TenantId", "CorrelationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentAcknowledgements_TenantId_IssuanceId",
                table: "ProcurementTenderDocumentAcknowledgements",
                columns: new[] { "TenantId", "IssuanceId" },
                unique: true,
                filter: "[IssuanceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChangeRecipients_BusinessPartnerId",
                table: "ProcurementTenderDocumentChangeRecipients",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChangeRecipients_ChangeId",
                table: "ProcurementTenderDocumentChangeRecipients",
                column: "ChangeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChangeRecipients_IssuanceId",
                table: "ProcurementTenderDocumentChangeRecipients",
                column: "IssuanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChangeRecipients_RequestForQuotationQuoteId",
                table: "ProcurementTenderDocumentChangeRecipients",
                column: "RequestForQuotationQuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChangeRecipients_TenantId_BusinessPartnerId",
                table: "ProcurementTenderDocumentChangeRecipients",
                columns: new[] { "TenantId", "BusinessPartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChangeRecipients_TenantId_ChangeId_RecipientKey",
                table: "ProcurementTenderDocumentChangeRecipients",
                columns: new[] { "TenantId", "ChangeId", "RecipientKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChangeRecipients_TenantId_IssuanceId",
                table: "ProcurementTenderDocumentChangeRecipients",
                columns: new[] { "TenantId", "IssuanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChangeRecipients_TenantId_RequestForQuotationQuoteId",
                table: "ProcurementTenderDocumentChangeRecipients",
                columns: new[] { "TenantId", "RequestForQuotationQuoteId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChangeRecipients_TenantId_TenderBidId",
                table: "ProcurementTenderDocumentChangeRecipients",
                columns: new[] { "TenantId", "TenderBidId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChangeRecipients_TenderBidId",
                table: "ProcurementTenderDocumentChangeRecipients",
                column: "TenderBidId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChanges_EvidenceFileUploadRecordId",
                table: "ProcurementTenderDocumentChanges",
                column: "EvidenceFileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChanges_EvidenceWorkflowDocumentId",
                table: "ProcurementTenderDocumentChanges",
                column: "EvidenceWorkflowDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChanges_NewTemplateVersionId",
                table: "ProcurementTenderDocumentChanges",
                column: "NewTemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChanges_PreviousTemplateVersionId",
                table: "ProcurementTenderDocumentChanges",
                column: "PreviousTemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChanges_RegisterId",
                table: "ProcurementTenderDocumentChanges",
                column: "RegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChanges_TenantId_RegisterId",
                table: "ProcurementTenderDocumentChanges",
                columns: new[] { "TenantId", "RegisterId" },
                unique: true,
                filter: "[Status] = 0 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChanges_TenantId_RegisterId_CorrelationId",
                table: "ProcurementTenderDocumentChanges",
                columns: new[] { "TenantId", "RegisterId", "CorrelationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChanges_TenantId_RegisterId_Sequence",
                table: "ProcurementTenderDocumentChanges",
                columns: new[] { "TenantId", "RegisterId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChanges_TenantId_WorkflowDefinitionId",
                table: "ProcurementTenderDocumentChanges",
                columns: new[] { "TenantId", "WorkflowDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChanges_TenantId_WorkflowInstanceId",
                table: "ProcurementTenderDocumentChanges",
                columns: new[] { "TenantId", "WorkflowInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChanges_WorkflowDefinitionId",
                table: "ProcurementTenderDocumentChanges",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentChanges_WorkflowInstanceId",
                table: "ProcurementTenderDocumentChanges",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentIssuances_BusinessPartnerId",
                table: "ProcurementTenderDocumentIssuances",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentIssuances_EvidenceFileUploadRecordId",
                table: "ProcurementTenderDocumentIssuances",
                column: "EvidenceFileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentIssuances_EvidenceWorkflowDocumentId",
                table: "ProcurementTenderDocumentIssuances",
                column: "EvidenceWorkflowDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentIssuances_RegisterId",
                table: "ProcurementTenderDocumentIssuances",
                column: "RegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentIssuances_TemplateVersionId",
                table: "ProcurementTenderDocumentIssuances",
                column: "TemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentIssuances_TenantId_BusinessPartnerId",
                table: "ProcurementTenderDocumentIssuances",
                columns: new[] { "TenantId", "BusinessPartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentIssuances_TenantId_RegisterId_CorrelationId",
                table: "ProcurementTenderDocumentIssuances",
                columns: new[] { "TenantId", "RegisterId", "CorrelationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentIssuances_TenantId_RegisterId_ReceiptNumber",
                table: "ProcurementTenderDocumentIssuances",
                columns: new[] { "TenantId", "RegisterId", "ReceiptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentIssuances_TenantId_RegisterId_RecipientKey",
                table: "ProcurementTenderDocumentIssuances",
                columns: new[] { "TenantId", "RegisterId", "RecipientKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentIssuances_TenantId_TemplateVersionId",
                table: "ProcurementTenderDocumentIssuances",
                columns: new[] { "TenantId", "TemplateVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentRegisters_InitialTemplateVersionId",
                table: "ProcurementTenderDocumentRegisters",
                column: "InitialTemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentRegisters_MethodRuleId",
                table: "ProcurementTenderDocumentRegisters",
                column: "MethodRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentRegisters_PolicySetId",
                table: "ProcurementTenderDocumentRegisters",
                column: "PolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentRegisters_RequestForQuotationId",
                table: "ProcurementTenderDocumentRegisters",
                column: "RequestForQuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentRegisters_SourceConfigurationProfileId",
                table: "ProcurementTenderDocumentRegisters",
                column: "SourceConfigurationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentRegisters_SourcingCaseId",
                table: "ProcurementTenderDocumentRegisters",
                column: "SourcingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentRegisters_TenantId_CorrelationId",
                table: "ProcurementTenderDocumentRegisters",
                columns: new[] { "TenantId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentRegisters_TenantId_InitialTemplateVersionId",
                table: "ProcurementTenderDocumentRegisters",
                columns: new[] { "TenantId", "InitialTemplateVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentRegisters_TenantId_MethodRuleId",
                table: "ProcurementTenderDocumentRegisters",
                columns: new[] { "TenantId", "MethodRuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentRegisters_TenantId_PolicySetId",
                table: "ProcurementTenderDocumentRegisters",
                columns: new[] { "TenantId", "PolicySetId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentRegisters_TenantId_RequestForQuotationId",
                table: "ProcurementTenderDocumentRegisters",
                columns: new[] { "TenantId", "RequestForQuotationId" },
                unique: true,
                filter: "[RequestForQuotationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentRegisters_TenantId_SourcingCaseId",
                table: "ProcurementTenderDocumentRegisters",
                columns: new[] { "TenantId", "SourcingCaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentRegisters_TenantId_TenderId",
                table: "ProcurementTenderDocumentRegisters",
                columns: new[] { "TenantId", "TenderId" },
                unique: true,
                filter: "[TenderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentRegisters_TenderId",
                table: "ProcurementTenderDocumentRegisters",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentTemplateMethods_TemplateVersionId",
                table: "ProcurementTenderDocumentTemplateMethods",
                column: "TemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentTemplateMethods_TenantId_Method_TemplateVersionId",
                table: "ProcurementTenderDocumentTemplateMethods",
                columns: new[] { "TenantId", "Method", "TemplateVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentTemplateMethods_TenantId_TemplateVersionId_Method",
                table: "ProcurementTenderDocumentTemplateMethods",
                columns: new[] { "TenantId", "TemplateVersionId", "Method" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentTemplateVersions_ContentFileUploadRecordId",
                table: "ProcurementTenderDocumentTemplateVersions",
                column: "ContentFileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentTemplateVersions_ContentWorkflowEvidenceDocumentId",
                table: "ProcurementTenderDocumentTemplateVersions",
                column: "ContentWorkflowEvidenceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentTemplateVersions_PolicySetId",
                table: "ProcurementTenderDocumentTemplateVersions",
                column: "PolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentTemplateVersions_SourceConfigurationProfileId",
                table: "ProcurementTenderDocumentTemplateVersions",
                column: "SourceConfigurationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentTemplateVersions_SupersedesVersionId",
                table: "ProcurementTenderDocumentTemplateVersions",
                column: "SupersedesVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentTemplateVersions_TenantId_PolicySetId",
                table: "ProcurementTenderDocumentTemplateVersions",
                columns: new[] { "TenantId", "PolicySetId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentTemplateVersions_TenantId_Status_EffectiveFromUtc",
                table: "ProcurementTenderDocumentTemplateVersions",
                columns: new[] { "TenantId", "Status", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentTemplateVersions_TenantId_TemplateCode_Version",
                table: "ProcurementTenderDocumentTemplateVersions",
                columns: new[] { "TenantId", "TemplateCode", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentTemplateVersions_TenantId_TemplateKey",
                table: "ProcurementTenderDocumentTemplateVersions",
                columns: new[] { "TenantId", "TemplateKey" },
                unique: true,
                filter: "[Status] IN (0, 1) AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentTemplateVersions_TenantId_TemplateKey_Version",
                table: "ProcurementTenderDocumentTemplateVersions",
                columns: new[] { "TenantId", "TemplateKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentTemplateVersions_TenantId_WorkflowDefinitionId",
                table: "ProcurementTenderDocumentTemplateVersions",
                columns: new[] { "TenantId", "WorkflowDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentTemplateVersions_WorkflowDefinitionId",
                table: "ProcurementTenderDocumentTemplateVersions",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentTemplateVersions_WorkflowInstanceId",
                table: "ProcurementTenderDocumentTemplateVersions",
                column: "WorkflowInstanceId");

            migrationBuilder.Sql(@"
CREATE OR ALTER TRIGGER [TR_ProcurementTenderDocumentTemplateVersions_Lifecycle]
ON [ProcurementTenderDocumentTemplateVersions]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM deleted d
        LEFT JOIN inserted i ON i.Id = d.Id
        WHERE i.Id IS NULL
    )
        THROW 51200, 'Controlled tender-document template versions cannot be physically deleted.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN deleted d ON d.Id = i.Id
        WHERE d.Id IS NULL AND (i.Status <> 0 OR i.IsDeleted = 1)
    )
        THROW 51202, 'Controlled tender-document template versions must be created as active Draft revisions.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE i.TenantId <> d.TenantId
           OR i.TemplateKey <> d.TemplateKey
           OR i.Version <> d.Version
           OR i.CreatedAt <> d.CreatedAt
           OR ISNULL(i.CreatedById, '00000000-0000-0000-0000-000000000000')
              <> ISNULL(d.CreatedById, '00000000-0000-0000-0000-000000000000')
           OR (d.IsDeleted = 1 AND i.IsDeleted = 0)
           OR (i.IsDeleted <> d.IsDeleted AND NOT (d.Status = 0 AND d.IsDeleted = 0 AND i.IsDeleted = 1))
    )
        THROW 51200, 'Tender-document template family, version, tenant, creation, and deletion lineage is immutable.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE NOT (
            (d.Status = 0 AND i.Status IN (0, 1))
            OR (d.Status = 1 AND i.Status IN (0, 1, 2))
            OR (d.Status = 2 AND i.Status IN (2, 3))
            OR (d.Status = 3 AND i.Status = 3)
        )
    )
        THROW 51202, 'Tender-document template lifecycle transition is invalid.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE d.Status = 3
    )
        THROW 51200, 'Retired tender-document template versions are immutable.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE (d.Status IN (1, 2)
               OR (d.Status = 0 AND i.Status <> 0))
          AND EXISTS (
              SELECT
                  d.TemplateCode, d.Name, d.Description, d.DocumentTypeCode,
                  d.PolicySetId, d.PolicySetCode, d.PolicySetVersion,
                  d.SourceConfigurationProfileId, d.ContentReference,
                  d.ContentWorkflowEvidenceDocumentId, d.ContentFileUploadRecordId,
                  d.ContentChecksumSha256, d.WorkflowDefinitionId, d.SupersedesVersionId,
                  d.ChangeSummary, d.EffectiveFromUtc
              EXCEPT
              SELECT
                  i.TemplateCode, i.Name, i.Description, i.DocumentTypeCode,
                  i.PolicySetId, i.PolicySetCode, i.PolicySetVersion,
                  i.SourceConfigurationProfileId, i.ContentReference,
                  i.ContentWorkflowEvidenceDocumentId, i.ContentFileUploadRecordId,
                  i.ContentChecksumSha256, i.WorkflowDefinitionId, i.SupersedesVersionId,
                  i.ChangeSummary, i.EffectiveFromUtc
          )
    )
        THROW 51200, 'Pending, Published, and Retired tender-document template content and control lineage are immutable.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE d.Status = 1 AND i.Status = 1
          AND EXISTS (
              SELECT d.EffectiveToUtc, d.SubmittedAtUtc, d.SubmittedById, d.SubmittedByName
              EXCEPT
              SELECT i.EffectiveToUtc, i.SubmittedAtUtc, i.SubmittedById, i.SubmittedByName
          )
    )
        THROW 51200, 'Pending tender-document template terms and submission identity are immutable.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE (ISNULL(i.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000')
               <> ISNULL(d.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000')
               AND NOT (d.Status = 1 AND i.Status = 1
                        AND d.WorkflowInstanceId IS NULL AND i.WorkflowInstanceId IS NOT NULL)
               AND NOT (d.Status = 1 AND i.Status = 0 AND i.WorkflowInstanceId IS NULL))
           OR (ISNULL(i.EffectiveToUtc, '9999-12-31') <> ISNULL(d.EffectiveToUtc, '9999-12-31')
               AND NOT (d.Status = 0 AND i.Status = 0)
               AND NOT (d.Status = 2 AND i.Status = 2))
           OR (d.Status = 2 AND i.Status = 2
               AND EXISTS (
                   SELECT d.SubmittedAtUtc, d.SubmittedById, d.SubmittedByName,
                          d.PublishedAtUtc, d.PublishedById, d.PublishedByName,
                          d.ApprovalEvidenceReference
                   EXCEPT
                   SELECT i.SubmittedAtUtc, i.SubmittedById, i.SubmittedByName,
                          i.PublishedAtUtc, i.PublishedById, i.PublishedByName,
                          i.ApprovalEvidenceReference
               ))
    )
        THROW 51200, 'Tender-document workflow linkage, effective period, and captured lifecycle evidence are immutable outside their exact service transition.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE d.Status = 2 AND i.Status = 2
          AND ISNULL(i.EffectiveToUtc, '9999-12-31') <> ISNULL(d.EffectiveToUtc, '9999-12-31')
          AND (i.EffectiveToUtc IS NULL
               OR i.EffectiveToUtc <= SYSUTCDATETIME()
               OR (d.EffectiveToUtc IS NOT NULL AND i.EffectiveToUtc > d.EffectiveToUtc))
    )
        THROW 51202, 'A Published template can only schedule an earlier future end date for a replacement.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN ProcurementPolicySets ps ON ps.Id = i.PolicySetId
        LEFT JOIN ProcurementConfigurationProfiles cp ON cp.Id = i.SourceConfigurationProfileId
        LEFT JOIN WorkflowDefinitions wd ON wd.Id = i.WorkflowDefinitionId
        LEFT JOIN ProcurementTenderDocumentTemplateVersions sv ON sv.Id = i.SupersedesVersionId
        LEFT JOIN WorkflowEvidenceDocuments we ON we.Id = i.ContentWorkflowEvidenceDocumentId
        LEFT JOIN FileUploadRecords fu ON fu.Id = i.ContentFileUploadRecordId
        LEFT JOIN deleted d ON d.Id = i.Id
        WHERE ps.Id IS NULL
           OR ps.TenantId <> i.TenantId
           OR ps.IsDeleted = 1
           OR ps.Code <> i.PolicySetCode
           OR ps.Version <> i.PolicySetVersion
           OR ps.SourceConfigurationProfileId <> i.SourceConfigurationProfileId
           OR cp.Id IS NULL
           OR cp.TenantId <> i.TenantId
           OR cp.IsDeleted = 1
           OR wd.Id IS NULL
           OR wd.TenantId <> i.TenantId
           OR wd.IsDeleted = 1
           OR (i.IsDeleted = 0 AND i.Status <> 3
               AND NOT (ISNULL(d.Status, -1) = 1 AND i.Status = 0)
               AND (ps.LifecycleStatus <> 1 OR cp.LifecycleStatus <> 1
                    OR wd.LifecycleStatus <> 1 OR wd.IsActive = 0))
           OR (i.SupersedesVersionId IS NOT NULL AND
               (sv.Id IS NULL OR sv.TenantId <> i.TenantId OR sv.TemplateKey <> i.TemplateKey
                OR sv.Version >= i.Version OR sv.IsDeleted = 1))
           OR (i.ContentWorkflowEvidenceDocumentId IS NOT NULL AND
               (we.Id IS NULL OR we.TenantId <> i.TenantId
                OR (i.IsDeleted = 0 AND i.Status <> 3
                    AND NOT (ISNULL(d.Status, -1) = 1 AND i.Status = 0) AND we.IsDeleted = 1)))
           OR (i.ContentFileUploadRecordId IS NOT NULL AND
               (fu.Id IS NULL OR fu.TenantId <> i.TenantId
                OR (i.IsDeleted = 0 AND i.Status <> 3
                    AND NOT (ISNULL(d.Status, -1) = 1 AND i.Status = 0) AND fu.IsDeleted = 1)))
           OR LEN(LTRIM(RTRIM(i.ContentReference))) = 0
           OR i.ContentChecksumSha256 LIKE '%[^0-9A-Fa-f]%'
    )
        THROW 51201, 'Tender-document template policy, configuration, workflow, evidence, checksum, or supersession lineage is invalid.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN WorkflowInstances wi ON wi.Id = i.WorkflowInstanceId
        WHERE i.WorkflowInstanceId IS NOT NULL
          AND (wi.Id IS NULL OR wi.TenantId <> i.TenantId OR wi.IsDeleted = 1
               OR wi.WorkflowDefinitionId <> i.WorkflowDefinitionId OR wi.EntityId <> i.Id)
    )
        THROW 51201, 'Tender-document template workflow-instance lineage must match the exact tenant, definition, and template revision.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        WHERE (i.Status = 0 AND
               (i.WorkflowInstanceId IS NOT NULL
                OR i.PublishedAtUtc IS NOT NULL OR i.PublishedById IS NOT NULL
                OR i.RetiredAtUtc IS NOT NULL OR i.RetiredById IS NOT NULL))
           OR (i.Status = 1 AND
               (i.SubmittedAtUtc IS NULL OR i.SubmittedById IS NULL
                OR LEN(LTRIM(RTRIM(ISNULL(i.SubmittedByName, '')))) = 0
                OR i.PublishedAtUtc IS NOT NULL OR i.PublishedById IS NOT NULL
                OR i.RetiredAtUtc IS NOT NULL OR i.RetiredById IS NOT NULL))
           OR (i.Status = 2 AND
               (i.SubmittedAtUtc IS NULL OR i.SubmittedById IS NULL
                OR i.PublishedAtUtc IS NULL OR i.PublishedById IS NULL
                OR LEN(LTRIM(RTRIM(ISNULL(i.PublishedByName, '')))) = 0
                OR LEN(LTRIM(RTRIM(ISNULL(i.ApprovalEvidenceReference, '')))) = 0
                OR i.SubmittedById = i.PublishedById
                OR i.RetiredAtUtc IS NOT NULL OR i.RetiredById IS NOT NULL))
           OR (i.Status = 3 AND
               (i.PublishedAtUtc IS NULL OR i.PublishedById IS NULL
                OR i.RetiredAtUtc IS NULL OR i.RetiredById IS NULL
                OR LEN(LTRIM(RTRIM(ISNULL(i.RetiredByName, '')))) = 0))
    )
        THROW 51202, 'Tender-document template lifecycle actors and timestamps are inconsistent.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN WorkflowInstances wi ON wi.Id = i.WorkflowInstanceId
        WHERE i.Status = 2
          AND (wi.Id IS NULL OR wi.TenantId <> i.TenantId OR wi.IsDeleted = 1
               OR wi.WorkflowDefinitionId <> i.WorkflowDefinitionId
               OR wi.EntityId <> i.Id OR wi.Status <> 2)
    )
        THROW 51202, 'Publishing a tender-document template requires the exact Completed shared workflow.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        LEFT JOIN WorkflowInstances wi ON wi.Id = d.WorkflowInstanceId
        WHERE d.Status = 1 AND i.Status = 0
          AND (wi.Id IS NULL OR wi.TenantId <> d.TenantId OR wi.IsDeleted = 1
               OR wi.WorkflowDefinitionId <> d.WorkflowDefinitionId OR wi.EntityId <> d.Id
               OR wi.Status NOT IN (3, 4) OR i.WorkflowInstanceId IS NOT NULL)
    )
        THROW 51202, 'Rejecting a tender-document template requires the exact Cancelled or Failed shared workflow.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        WHERE i.Status IN (2, 3)
          AND NOT EXISTS (
              SELECT 1
              FROM ProcurementTenderDocumentTemplateMethods tm
              WHERE tm.TemplateVersionId = i.Id
                AND tm.TenantId = i.TenantId
                AND tm.IsDeleted = 0
          )
    )
        THROW 51202, 'A Published tender-document template requires at least one controlled procurement method.', 1;

    IF EXISTS (
        SELECT 1
        FROM ProcurementTenderDocumentTemplateVersions a
        JOIN ProcurementTenderDocumentTemplateVersions b
          ON b.TenantId = a.TenantId
         AND b.TemplateKey = a.TemplateKey
         AND b.Id <> a.Id
         AND b.Status = 2
         AND b.IsDeleted = 0
        WHERE a.Status = 2
          AND a.IsDeleted = 0
          AND a.EffectiveFromUtc <= ISNULL(b.EffectiveToUtc, '9999-12-31')
          AND b.EffectiveFromUtc <= ISNULL(a.EffectiveToUtc, '9999-12-31')
    )
        THROW 51202, 'Published tender-document template effective periods cannot overlap within one tenant family.', 1;
END");

            migrationBuilder.Sql(@"
CREATE OR ALTER TRIGGER [TR_ProcurementTenderDocumentTemplateMethods_Immutable]
ON [ProcurementTenderDocumentTemplateMethods]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM deleted d
        LEFT JOIN inserted i ON i.Id = d.Id
        WHERE i.Id IS NULL
    )
        THROW 51203, 'Tender-document template-method rows cannot be physically deleted; Draft edits retain soft-deleted history.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN ProcurementTenderDocumentTemplateVersions tv ON tv.Id = i.TemplateVersionId
        WHERE tv.Id IS NULL
           OR tv.TenantId <> i.TenantId
           OR tv.IsDeleted = 1
           OR tv.Status <> 0
           OR (i.IsDeleted = 0 AND NOT EXISTS (
               SELECT 1
               FROM ProcurementPolicyMethodRules mr
               WHERE mr.PolicySetId = tv.PolicySetId
                 AND mr.TenantId = i.TenantId
                 AND mr.Method = i.Method
                 AND mr.IsEnabled = 1
                 AND mr.IsAllowed = 1
                 AND mr.IsDeleted = 0
           ))
    )
        THROW 51204, 'Tender-document template-method tenant, parent Draft, or allowed-policy lineage is invalid.', 1;

    IF EXISTS (
        SELECT 1
        FROM deleted d
        LEFT JOIN ProcurementTenderDocumentTemplateVersions tv ON tv.Id = d.TemplateVersionId
        WHERE tv.Id IS NULL OR tv.TenantId <> d.TenantId OR tv.Status <> 0
    )
        THROW 51203, 'Tender-document template methods can change or be removed only while the parent is Draft.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE i.TenantId <> d.TenantId
           OR i.TemplateVersionId <> d.TemplateVersionId
           OR i.Method <> d.Method
           OR i.IntegrityHash <> d.IntegrityHash
           OR i.CreatedAt <> d.CreatedAt
           OR ISNULL(i.CreatedById, '00000000-0000-0000-0000-000000000000')
              <> ISNULL(d.CreatedById, '00000000-0000-0000-0000-000000000000')
           OR NOT (i.IsDeleted = d.IsDeleted OR (d.IsDeleted = 0 AND i.IsDeleted = 1))
    )
        THROW 51203, 'Tender-document template-method identity and tenant lineage are immutable.', 1;
END");

            migrationBuilder.Sql(@"
CREATE OR ALTER TRIGGER [TR_ProcurementTenderDocumentRegisters_Immutable]
ON [ProcurementTenderDocumentRegisters]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM deleted d
        LEFT JOIN inserted i ON i.Id = d.Id
        WHERE i.Id IS NULL
    )
        THROW 51205, 'Controlled tender-document registers cannot be physically deleted.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN deleted d ON d.Id = i.Id
        WHERE d.Id IS NULL AND i.IsDeleted = 1
    )
        THROW 51205, 'Controlled tender-document registers cannot be created as deleted records.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE EXISTS (
            SELECT
                d.TenantId, d.SourceType, d.TenderId, d.RequestForQuotationId,
                d.SourcingCaseId, d.MethodRuleId, d.Method, d.MethodRuleCode,
                d.PolicySetId, d.PolicySetCode, d.PolicySetVersion,
                d.SourceConfigurationProfileId, d.InitialTemplateVersionId,
                d.OriginalSubmissionDeadlineUtc, d.OpeningScheduledAtUtc,
                d.OriginalBidValidityUntilUtc, d.FeeMode, d.FeeAmount,
                d.CurrencyCode, d.BoundAtUtc, d.BoundByUserId, d.CorrelationId,
                d.CreatedAt, d.CreatedBy, d.CreatedById, d.IsDeleted,
                d.DeletedAt, d.DeletedBy
            EXCEPT
            SELECT
                i.TenantId, i.SourceType, i.TenderId, i.RequestForQuotationId,
                i.SourcingCaseId, i.MethodRuleId, i.Method, i.MethodRuleCode,
                i.PolicySetId, i.PolicySetCode, i.PolicySetVersion,
                i.SourceConfigurationProfileId, i.InitialTemplateVersionId,
                i.OriginalSubmissionDeadlineUtc, i.OpeningScheduledAtUtc,
                i.OriginalBidValidityUntilUtc, i.FeeMode, i.FeeAmount,
                i.CurrencyCode, i.BoundAtUtc, i.BoundByUserId, i.CorrelationId,
                i.CreatedAt, i.CreatedBy, i.CreatedById, i.IsDeleted,
                i.DeletedAt, i.DeletedBy
        )
    )
        THROW 51207, 'Tender-document register business lineage is immutable; only integrity and audit refresh fields may change.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE i.UpdatedAt IS NULL OR i.LastModifiedById IS NULL
           OR (d.UpdatedAt IS NOT NULL AND i.UpdatedAt < d.UpdatedAt)
           OR ((i.LifecycleSnapshotJson = d.LifecycleSnapshotJson AND i.IntegrityHash <> d.IntegrityHash)
               OR (i.LifecycleSnapshotJson <> d.LifecycleSnapshotJson AND i.IntegrityHash = d.IntegrityHash))
    )
        THROW 51207, 'Tender-document register integrity refresh requires paired snapshot/hash changes and a current authenticated audit actor and timestamp.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN ProcurementSourcingCases sc ON sc.Id = i.SourcingCaseId
        LEFT JOIN ProcurementPolicyMethodRules mr ON mr.Id = i.MethodRuleId
        LEFT JOIN ProcurementPolicySets ps ON ps.Id = i.PolicySetId
        LEFT JOIN ProcurementConfigurationProfiles cp ON cp.Id = i.SourceConfigurationProfileId
        LEFT JOIN ProcurementTenderDocumentTemplateVersions tv ON tv.Id = i.InitialTemplateVersionId
        LEFT JOIN Tenders t ON t.Id = i.TenderId
        LEFT JOIN RequestForQuotations q ON q.Id = i.RequestForQuotationId
        LEFT JOIN deleted d ON d.Id = i.Id
        WHERE d.Id IS NULL
          AND (i.IsDeleted = 1
           OR i.BoundByUserId = '00000000-0000-0000-0000-000000000000'
           OR i.OriginalSubmissionDeadlineUtc <= i.BoundAtUtc
           OR i.OriginalBidValidityUntilUtc <= i.OriginalSubmissionDeadlineUtc
           OR sc.Id IS NULL OR sc.TenantId <> i.TenantId OR sc.IsDeleted = 1 OR sc.Status IN (2, 3)
           OR sc.MethodRuleId <> i.MethodRuleId OR sc.SelectedMethod <> i.Method
           OR sc.MethodRuleCode <> i.MethodRuleCode
           OR sc.PolicySetId <> i.PolicySetId OR sc.PolicyCode <> i.PolicySetCode
           OR sc.PolicyVersion <> i.PolicySetVersion
           OR mr.Id IS NULL OR mr.TenantId <> i.TenantId OR mr.IsDeleted = 1
           OR mr.PolicySetId <> i.PolicySetId OR mr.Method <> i.Method
           OR mr.RuleCode <> i.MethodRuleCode OR mr.IsEnabled = 0 OR mr.IsAllowed = 0
           OR ps.Id IS NULL OR ps.TenantId <> i.TenantId OR ps.IsDeleted = 1
           OR ps.LifecycleStatus <> 1 OR ps.Code <> i.PolicySetCode
           OR ps.Version <> i.PolicySetVersion
           OR ps.SourceConfigurationProfileId <> i.SourceConfigurationProfileId
           OR cp.Id IS NULL OR cp.TenantId <> i.TenantId OR cp.IsDeleted = 1 OR cp.LifecycleStatus <> 1
           OR tv.Id IS NULL OR tv.TenantId <> i.TenantId OR tv.IsDeleted = 1
           OR tv.Status <> 2 OR tv.PolicySetId <> i.PolicySetId
           OR tv.PolicySetCode <> i.PolicySetCode OR tv.PolicySetVersion <> i.PolicySetVersion
           OR tv.SourceConfigurationProfileId <> i.SourceConfigurationProfileId
           OR tv.EffectiveFromUtc > i.BoundAtUtc
           OR (tv.EffectiveToUtc IS NOT NULL AND tv.EffectiveToUtc < i.BoundAtUtc)
           OR NOT EXISTS (
               SELECT 1 FROM ProcurementTenderDocumentTemplateMethods tm
               WHERE tm.TemplateVersionId = tv.Id AND tm.TenantId = i.TenantId
                 AND tm.Method = i.Method AND tm.IsDeleted = 0
           )
           OR (i.SourceType = 0 AND
               (t.Id IS NULL OR t.TenantId <> i.TenantId OR t.IsDeleted = 1
                OR t.SourcingCaseId <> i.SourcingCaseId
                OR (d.Id IS NULL AND t.SubmissionDeadline <> i.OriginalSubmissionDeadlineUtc)
                OR (d.Id IS NULL AND
                    ISNULL(t.OpeningDate, '9999-12-31') <> ISNULL(i.OpeningScheduledAtUtc, '9999-12-31'))
                OR UPPER(ISNULL(t.Currency, '')) <> UPPER(i.CurrencyCode)))
           OR (i.SourceType = 1 AND
               (q.Id IS NULL OR q.TenantId <> i.TenantId OR q.IsDeleted = 1
                OR q.SourcingCaseId <> i.SourcingCaseId
                OR (d.Id IS NULL AND q.SubmissionDeadline <> i.OriginalSubmissionDeadlineUtc)
                OR i.OpeningScheduledAtUtc IS NOT NULL
                OR UPPER(q.Currency) <> UPPER(i.CurrencyCode))))
    )
        THROW 51206, 'Tender-document register source, method, policy, configuration, template, deadline, or tenant lineage is invalid.', 1;
END");

            migrationBuilder.Sql(@"
CREATE OR ALTER TRIGGER [TR_ProcurementTenderDocumentIssuances_Immutable]
ON [ProcurementTenderDocumentIssuances]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM deleted)
        THROW 51208, 'Tender-document issuance and sale records are append-only.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN ProcurementTenderDocumentRegisters r ON r.Id = i.RegisterId
        LEFT JOIN ProcurementTenderDocumentTemplateVersions tv ON tv.Id = i.TemplateVersionId
        LEFT JOIN ProcurementSourcingCases sc ON sc.Id = r.SourcingCaseId
        LEFT JOIN ProcurementPolicyMethodRules mr ON mr.Id = r.MethodRuleId
        LEFT JOIN ProcurementPolicySets ps ON ps.Id = r.PolicySetId
        LEFT JOIN ProcurementConfigurationProfiles cp ON cp.Id = r.SourceConfigurationProfileId
        LEFT JOIN BusinessPartners bp ON bp.Id = i.BusinessPartnerId
        LEFT JOIN WorkflowEvidenceDocuments we ON we.Id = i.EvidenceWorkflowDocumentId
        LEFT JOIN FileUploadRecords fu ON fu.Id = i.EvidenceFileUploadRecordId
        OUTER APPLY (
            SELECT TOP (1) c.NewTemplateVersionId
            FROM ProcurementTenderDocumentChanges c
            WHERE c.RegisterId = i.RegisterId AND c.TenantId = i.TenantId
              AND c.IsDeleted = 0 AND c.Status = 1 AND c.ChangeType = 0
            ORDER BY c.Sequence DESC
        ) atv
        OUTER APPLY (
            SELECT TOP (1) c.NewValueUtc
            FROM ProcurementTenderDocumentChanges c
            WHERE c.RegisterId = i.RegisterId AND c.TenantId = i.TenantId
              AND c.IsDeleted = 0 AND c.Status = 1 AND c.ChangeType = 1
            ORDER BY c.Sequence DESC
        ) adl
        WHERE i.IsDeleted = 1
           OR r.Id IS NULL OR r.TenantId <> i.TenantId OR r.IsDeleted = 1
           OR sc.Id IS NULL OR sc.TenantId <> i.TenantId OR sc.IsDeleted = 1 OR sc.Status IN (2, 3)
           OR sc.MethodRuleId <> r.MethodRuleId OR sc.SelectedMethod <> r.Method
           OR sc.PolicySetId <> r.PolicySetId OR sc.PolicyCode <> r.PolicySetCode
           OR sc.PolicyVersion <> r.PolicySetVersion
           OR mr.Id IS NULL OR mr.TenantId <> i.TenantId OR mr.IsDeleted = 1
           OR mr.PolicySetId <> r.PolicySetId OR mr.Method <> r.Method
           OR mr.RuleCode <> r.MethodRuleCode OR mr.IsEnabled = 0 OR mr.IsAllowed = 0
           OR ps.Id IS NULL OR ps.TenantId <> i.TenantId OR ps.IsDeleted = 1
           OR ps.LifecycleStatus <> 1 OR ps.Code <> r.PolicySetCode
           OR ps.Version <> r.PolicySetVersion
           OR ps.SourceConfigurationProfileId <> r.SourceConfigurationProfileId
           OR cp.Id IS NULL OR cp.TenantId <> i.TenantId OR cp.IsDeleted = 1 OR cp.LifecycleStatus <> 1
           OR tv.Id IS NULL OR tv.TenantId <> i.TenantId OR tv.IsDeleted = 1
           OR tv.Status <> 2
           OR i.TemplateVersionId <> COALESCE(atv.NewTemplateVersionId, r.InitialTemplateVersionId)
           OR tv.PolicySetId <> r.PolicySetId OR tv.PolicySetVersion <> r.PolicySetVersion
           OR tv.SourceConfigurationProfileId <> r.SourceConfigurationProfileId
           OR tv.EffectiveFromUtc > i.IssuedAtUtc
           OR (tv.EffectiveToUtc IS NOT NULL AND tv.EffectiveToUtc < i.IssuedAtUtc)
           OR NOT EXISTS (
               SELECT 1 FROM ProcurementTenderDocumentTemplateMethods tm
               WHERE tm.TemplateVersionId = tv.Id AND tm.TenantId = i.TenantId
                 AND tm.Method = r.Method AND tm.IsDeleted = 0)
           OR i.IssuedAtUtc < r.BoundAtUtc
           OR i.IssuedByUserId = '00000000-0000-0000-0000-000000000000'
           OR i.IssuedAtUtc > COALESCE(adl.NewValueUtc, r.OriginalSubmissionDeadlineUtc)
           OR i.FeeMode <> r.FeeMode OR i.FeeAmount <> r.FeeAmount
           OR UPPER(i.CurrencyCode) <> UPPER(r.CurrencyCode)
           OR (r.FeeMode = 0 AND
               (i.AmountPaid <> 0 OR i.PaymentReference IS NOT NULL))
           OR (r.FeeMode = 1 AND
               (i.AmountPaid <> r.FeeAmount OR LEN(LTRIM(RTRIM(ISNULL(i.PaymentReference, '')))) = 0))
           OR (i.BusinessPartnerId IS NOT NULL AND
               (bp.Id IS NULL OR bp.TenantId <> i.TenantId OR bp.IsDeleted = 1
                OR bp.IsActive = 0 OR bp.IsBlacklisted = 1
                OR bp.PartnerType NOT IN ('Supplier', 'Contractor', 'Both')))
           OR (i.BusinessPartnerId IS NULL AND LEN(LTRIM(RTRIM(ISNULL(i.RecipientEmail, '')))) = 0)
           OR LEN(LTRIM(RTRIM(i.RecipientName))) = 0
           OR LEN(LTRIM(RTRIM(i.ReceiptNumber))) = 0
           OR LEN(LTRIM(RTRIM(i.IssueChannel))) = 0
           OR LEN(LTRIM(RTRIM(i.EvidenceReference))) = 0
           OR (i.EvidenceWorkflowDocumentId IS NOT NULL AND
               (we.Id IS NULL OR we.TenantId <> i.TenantId OR we.IsDeleted = 1))
           OR (i.EvidenceFileUploadRecordId IS NOT NULL AND
               (fu.Id IS NULL OR fu.TenantId <> i.TenantId OR fu.IsDeleted = 1))
           OR (r.SourceType = 0 AND NOT EXISTS (
               SELECT 1 FROM Tenders t
               WHERE t.Id = r.TenderId AND t.TenantId = r.TenantId
                 AND t.SourcingCaseId = r.SourcingCaseId AND t.IsDeleted = 0
                 AND t.Status NOT IN ('Closed', 'Awarded', 'Cancelled')))
           OR (r.SourceType = 1 AND NOT EXISTS (
               SELECT 1 FROM RequestForQuotations q
               WHERE q.Id = r.RequestForQuotationId AND q.TenantId = r.TenantId
                 AND q.SourcingCaseId = r.SourcingCaseId AND q.IsDeleted = 0
                 AND q.Status NOT IN ('Closed', 'Awarded', 'Cancelled')))
           OR (r.SourceType = 0 AND EXISTS (
               SELECT 1 FROM ProcurementTenderControls tc
               WHERE tc.TenderId = r.TenderId AND tc.TenantId = r.TenantId
                 AND tc.IsDeleted = 0 AND tc.OpenedAtUtc IS NOT NULL))
           OR (r.SourceType = 1 AND EXISTS (
               SELECT 1 FROM ProcurementRfqOpeningRegisters ro
               WHERE ro.RfqId = r.RequestForQuotationId AND ro.TenantId = r.TenantId
                 AND ro.IsDeleted = 0))
    )
        THROW 51209, 'Tender-document issuance register, effective version, fee, recipient, evidence, window, or tenant lineage is invalid.', 1;
END");

            migrationBuilder.Sql(@"
CREATE OR ALTER TRIGGER [TR_ProcurementTenderDocumentChanges_Lifecycle]
ON [ProcurementTenderDocumentChanges]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM deleted d
        LEFT JOIN inserted i ON i.Id = d.Id
        WHERE i.Id IS NULL
    )
        THROW 51210, 'Tender-document change, addendum, and extension records cannot be deleted.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN deleted d ON d.Id = i.Id
        WHERE d.Id IS NULL
          AND (i.Status <> 0 OR i.IsDeleted = 1 OR i.WorkflowInstanceId IS NOT NULL
               OR i.Sequence <> (
                   SELECT ISNULL(MAX(c.Sequence), 0) + 1
                   FROM ProcurementTenderDocumentChanges c
                   WHERE c.RegisterId = i.RegisterId
                     AND c.TenantId = i.TenantId
                     AND c.Id <> i.Id))
    )
        THROW 51211, 'Tender-document changes must be created as active Pending records before workflow start.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE d.Status IN (1, 2)
    )
        THROW 51210, 'Approved and Rejected tender-document changes are immutable.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE d.Status = 0
          AND i.Status NOT IN (0, 1, 2)
    )
        THROW 51211, 'Tender-document change lifecycle transition is invalid.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE d.Status = 0
          AND EXISTS (
              SELECT
                  d.TenantId, d.RegisterId, d.Sequence, d.ChangeType,
                  d.PreviousTemplateVersionId, d.NewTemplateVersionId,
                  d.PreviousValueUtc, d.NewValueUtc, d.RequiresAcknowledgement,
                  d.Reason, d.WorkflowDefinitionId, d.EvidenceReference,
                  d.EvidenceWorkflowDocumentId, d.EvidenceFileUploadRecordId,
                  d.RequestedAtUtc, d.RequestedByUserId, d.CorrelationId,
                  d.CreatedAt, d.CreatedBy, d.CreatedById, d.IsDeleted
              EXCEPT
              SELECT
                  i.TenantId, i.RegisterId, i.Sequence, i.ChangeType,
                  i.PreviousTemplateVersionId, i.NewTemplateVersionId,
                  i.PreviousValueUtc, i.NewValueUtc, i.RequiresAcknowledgement,
                  i.Reason, i.WorkflowDefinitionId, i.EvidenceReference,
                  i.EvidenceWorkflowDocumentId, i.EvidenceFileUploadRecordId,
                  i.RequestedAtUtc, i.RequestedByUserId, i.CorrelationId,
                  i.CreatedAt, i.CreatedBy, i.CreatedById, i.IsDeleted
          )
    )
        THROW 51210, 'Tender-document change request, tenant, workflow, evidence, and before/after lineage are immutable.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE ISNULL(i.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000')
              <> ISNULL(d.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000')
          AND NOT (d.Status = 0 AND i.Status = 0
                   AND d.WorkflowInstanceId IS NULL AND i.WorkflowInstanceId IS NOT NULL)
    )
        THROW 51210, 'Tender-document change workflow linkage can be attached once and is then immutable.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN ProcurementTenderDocumentRegisters r ON r.Id = i.RegisterId
        LEFT JOIN ProcurementSourcingCases sc ON sc.Id = r.SourcingCaseId
        LEFT JOIN ProcurementPolicyMethodRules mr ON mr.Id = r.MethodRuleId
        LEFT JOIN ProcurementPolicySets ps ON ps.Id = r.PolicySetId
        LEFT JOIN ProcurementConfigurationProfiles cp ON cp.Id = r.SourceConfigurationProfileId
        LEFT JOIN WorkflowDefinitions wd ON wd.Id = i.WorkflowDefinitionId
        LEFT JOIN WorkflowEvidenceDocuments we ON we.Id = i.EvidenceWorkflowDocumentId
        LEFT JOIN FileUploadRecords fu ON fu.Id = i.EvidenceFileUploadRecordId
        LEFT JOIN WorkflowInstances wi ON wi.Id = i.WorkflowInstanceId
        WHERE r.Id IS NULL OR r.TenantId <> i.TenantId OR r.IsDeleted = 1
           OR i.RequestedAtUtc < r.BoundAtUtc
           OR (i.Status <> 2 AND
               (sc.Id IS NULL OR sc.TenantId <> i.TenantId OR sc.IsDeleted = 1 OR sc.Status IN (2, 3)
                OR sc.MethodRuleId <> r.MethodRuleId OR sc.SelectedMethod <> r.Method
                OR sc.PolicySetId <> r.PolicySetId OR sc.PolicyCode <> r.PolicySetCode
                OR sc.PolicyVersion <> r.PolicySetVersion
                OR mr.Id IS NULL OR mr.TenantId <> i.TenantId OR mr.IsDeleted = 1
                OR mr.PolicySetId <> r.PolicySetId OR mr.Method <> r.Method
                OR mr.RuleCode <> r.MethodRuleCode OR mr.IsEnabled = 0 OR mr.IsAllowed = 0
                OR ps.Id IS NULL OR ps.TenantId <> i.TenantId OR ps.IsDeleted = 1
                OR ps.LifecycleStatus <> 1 OR ps.Code <> r.PolicySetCode
                OR ps.Version <> r.PolicySetVersion
                OR ps.SourceConfigurationProfileId <> r.SourceConfigurationProfileId
                OR cp.Id IS NULL OR cp.TenantId <> i.TenantId
                OR cp.IsDeleted = 1 OR cp.LifecycleStatus <> 1))
           OR wd.Id IS NULL OR wd.TenantId <> i.TenantId OR wd.IsDeleted = 1
           OR (i.Status <> 2 AND (wd.LifecycleStatus <> 1 OR wd.IsActive = 0))
           OR LEN(LTRIM(RTRIM(i.Reason))) = 0
           OR LEN(LTRIM(RTRIM(i.EvidenceReference))) = 0
           OR (i.EvidenceWorkflowDocumentId IS NOT NULL AND
               (we.Id IS NULL OR we.TenantId <> i.TenantId
                OR (i.Status <> 2 AND we.IsDeleted = 1)))
           OR (i.EvidenceFileUploadRecordId IS NOT NULL AND
               (fu.Id IS NULL OR fu.TenantId <> i.TenantId
                OR (i.Status <> 2 AND fu.IsDeleted = 1)))
           OR (i.WorkflowInstanceId IS NOT NULL AND
               (wi.Id IS NULL OR wi.TenantId <> i.TenantId OR wi.IsDeleted = 1
                OR wi.WorkflowDefinitionId <> i.WorkflowDefinitionId OR wi.EntityId <> i.Id))
    )
        THROW 51212, 'Tender-document change register, workflow, evidence, or tenant lineage is invalid.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN ProcurementTenderDocumentRegisters r ON r.Id = i.RegisterId
        OUTER APPLY (
            SELECT TOP (1) c.NewTemplateVersionId
            FROM ProcurementTenderDocumentChanges c
            WHERE c.RegisterId = i.RegisterId AND c.TenantId = i.TenantId
              AND c.Id <> i.Id AND c.IsDeleted = 0 AND c.Status = 1 AND c.ChangeType = 0
            ORDER BY c.Sequence DESC
        ) atv
        LEFT JOIN ProcurementTenderDocumentTemplateVersions oldtv
          ON oldtv.Id = COALESCE(atv.NewTemplateVersionId, r.InitialTemplateVersionId)
        LEFT JOIN ProcurementTenderDocumentTemplateVersions newtv ON newtv.Id = i.NewTemplateVersionId
        WHERE i.Status <> 2
          AND i.ChangeType = 0
          AND (i.PreviousTemplateVersionId <> oldtv.Id
               OR newtv.Id IS NULL OR newtv.TenantId <> i.TenantId OR newtv.IsDeleted = 1
               OR newtv.Status <> 2 OR newtv.TemplateKey <> oldtv.TemplateKey
               OR newtv.Version <= oldtv.Version
               OR newtv.PolicySetId <> r.PolicySetId
               OR newtv.PolicySetVersion <> r.PolicySetVersion
               OR newtv.SourceConfigurationProfileId <> r.SourceConfigurationProfileId
               OR newtv.EffectiveFromUtc > i.RequestedAtUtc
               OR (newtv.EffectiveToUtc IS NOT NULL AND newtv.EffectiveToUtc < i.RequestedAtUtc)
               OR NOT EXISTS (
                   SELECT 1 FROM ProcurementTenderDocumentTemplateMethods tm
                   WHERE tm.TemplateVersionId = newtv.Id AND tm.TenantId = i.TenantId
                     AND tm.Method = r.Method AND tm.IsDeleted = 0
               ))
    )
        THROW 51212, 'Tender-document addendum previous/effective version, policy, method, or tenant lineage is invalid.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN ProcurementTenderDocumentRegisters r ON r.Id = i.RegisterId
        OUTER APPLY (
            SELECT TOP (1) c.NewValueUtc
            FROM ProcurementTenderDocumentChanges c
            WHERE c.RegisterId = i.RegisterId AND c.TenantId = i.TenantId
              AND c.Id <> i.Id AND c.IsDeleted = 0 AND c.Status = 1 AND c.ChangeType = 1
            ORDER BY c.Sequence DESC
        ) adl
        OUTER APPLY (
            SELECT TOP (1) c.NewValueUtc
            FROM ProcurementTenderDocumentChanges c
            WHERE c.RegisterId = i.RegisterId AND c.TenantId = i.TenantId
              AND c.Id <> i.Id AND c.IsDeleted = 0 AND c.Status = 1 AND c.ChangeType = 2
            ORDER BY c.Sequence DESC
        ) avl
        WHERE i.Status <> 2
          AND ((i.ChangeType = 1 AND
               (i.PreviousValueUtc <> COALESCE(adl.NewValueUtc, r.OriginalSubmissionDeadlineUtc)
                OR i.PreviousValueUtc <= i.RequestedAtUtc
                OR i.NewValueUtc <= i.PreviousValueUtc
                OR i.NewValueUtc >= COALESCE(avl.NewValueUtc, r.OriginalBidValidityUntilUtc)
                OR (r.OpeningScheduledAtUtc IS NOT NULL AND i.NewValueUtc > r.OpeningScheduledAtUtc)
                OR (i.Status = 1 AND i.DecidedAtUtc > i.PreviousValueUtc)))
           OR (i.ChangeType = 2 AND
               (i.PreviousValueUtc <> COALESCE(avl.NewValueUtc, r.OriginalBidValidityUntilUtc)
                OR i.PreviousValueUtc <= i.RequestedAtUtc
                OR i.NewValueUtc <= i.PreviousValueUtc
                OR i.NewValueUtc <= COALESCE(adl.NewValueUtc, r.OriginalSubmissionDeadlineUtc)
                OR (i.Status = 1 AND i.DecidedAtUtc > i.PreviousValueUtc))))
    )
        THROW 51212, 'Tender-document deadline or bid-validity extension is stale, retroactive, or invalid.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN ProcurementTenderDocumentRegisters r ON r.Id = i.RegisterId
        LEFT JOIN Tenders t ON t.Id = r.TenderId
        LEFT JOIN RequestForQuotations q ON q.Id = r.RequestForQuotationId
        WHERE i.Status <> 2
          AND ((r.SourceType = 0 AND
               (t.Id IS NULL OR t.TenantId <> i.TenantId OR t.IsDeleted = 1
                OR t.Status IN ('Awarded', 'Cancelled')
                OR (i.ChangeType IN (0, 1) AND
                    (t.Status = 'Closed' OR EXISTS (
                        SELECT 1 FROM ProcurementTenderControls tc
                        WHERE tc.TenderId = t.Id AND tc.TenantId = i.TenantId
                          AND tc.IsDeleted = 0 AND tc.OpenedAtUtc IS NOT NULL)
                     OR EXISTS (
                         SELECT 1 FROM TenderBids tb
                         WHERE tb.TenderId = t.Id AND tb.TenantId = i.TenantId
                           AND tb.IsDeleted = 0
                           AND (tb.OpenedDate IS NOT NULL
                                OR tb.Status IN ('Opened', 'UnderEvaluation', 'Accepted', 'Rejected')))))))
           OR (r.SourceType = 1 AND
               (q.Id IS NULL OR q.TenantId <> i.TenantId OR q.IsDeleted = 1
                OR q.Status IN ('Awarded', 'Cancelled')
                OR (i.ChangeType IN (0, 1) AND
                    (q.Status = 'Closed' OR EXISTS (
                        SELECT 1 FROM ProcurementRfqOpeningRegisters ro
                        WHERE ro.RfqId = q.Id AND ro.TenantId = i.TenantId AND ro.IsDeleted = 0))))))
    )
        THROW 51212, 'Tender-document change is not permitted after source opening or terminal award/cancellation.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN WorkflowInstances wi ON wi.Id = i.WorkflowInstanceId
        WHERE (i.Status = 1 AND
               (wi.Id IS NULL OR wi.TenantId <> i.TenantId OR wi.Status <> 2
                OR i.WorkflowOutcome <> 'Approved'
                OR i.DecidedByUserId = i.RequestedByUserId))
           OR (i.Status = 2 AND
               (wi.Id IS NULL OR wi.TenantId <> i.TenantId OR wi.Status NOT IN (3, 4)
                OR i.WorkflowOutcome <> 'Rejected'
                OR i.DecidedByUserId = i.RequestedByUserId))
    )
        THROW 51211, 'Tender-document change outcome contradicts the exact shared-workflow outcome.', 1;
END");

            migrationBuilder.Sql(@"
CREATE OR ALTER TRIGGER [TR_ProcurementTenderDocumentChangeRecipients_Immutable]
ON [ProcurementTenderDocumentChangeRecipients]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM deleted)
        THROW 51213, 'Tender-document change-recipient dispatch records are append-only.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN ProcurementTenderDocumentChanges c ON c.Id = i.ChangeId
        LEFT JOIN ProcurementTenderDocumentRegisters r ON r.Id = c.RegisterId
        LEFT JOIN ProcurementTenderDocumentIssuances di ON di.Id = i.IssuanceId
        LEFT JOIN TenderBids tb ON tb.Id = i.TenderBidId
        LEFT JOIN RequestForQuotationQuotes rq ON rq.Id = i.RequestForQuotationQuoteId
        LEFT JOIN BusinessPartners bp ON bp.Id = i.BusinessPartnerId
        WHERE i.IsDeleted = 1
           OR c.Id IS NULL OR c.TenantId <> i.TenantId OR c.IsDeleted = 1 OR c.Status <> 1
           OR r.Id IS NULL OR r.TenantId <> i.TenantId OR r.IsDeleted = 1
           OR LEN(LTRIM(RTRIM(i.RecipientName))) = 0
           OR LEN(LTRIM(RTRIM(i.DispatchChannel))) = 0
           OR LEN(LTRIM(RTRIM(i.DispatchReference))) = 0
           OR LEN(LTRIM(RTRIM(i.DispatchEvidenceReference))) = 0
           OR c.DispatchedAtUtc IS NULL OR c.DispatchedByUserId IS NULL
           OR i.DispatchedAtUtc <> c.DispatchedAtUtc
           OR i.DispatchedByUserId <> c.DispatchedByUserId
           OR i.DispatchEvidenceReference <> c.DispatchEvidenceReference
           OR (i.BusinessPartnerId IS NOT NULL AND
               (bp.Id IS NULL OR bp.TenantId <> i.TenantId OR bp.IsDeleted = 1))
           OR (i.SourceType = 0 AND
               (di.Id IS NULL OR di.TenantId <> i.TenantId OR di.IsDeleted = 1 OR di.RegisterId <> r.Id
                OR ISNULL(di.BusinessPartnerId, '00000000-0000-0000-0000-000000000000')
                   <> ISNULL(i.BusinessPartnerId, '00000000-0000-0000-0000-000000000000')
                OR di.RecipientKey <> i.RecipientKey))
           OR (i.SourceType = 1 AND
               (tb.Id IS NULL OR tb.TenantId <> i.TenantId OR tb.IsDeleted = 1 OR tb.TenderId <> r.TenderId
                OR tb.BusinessPartnerId <> i.BusinessPartnerId))
           OR (i.SourceType = 2 AND
               (rq.Id IS NULL OR rq.TenantId <> i.TenantId OR rq.IsDeleted = 1
                OR rq.RfqId <> r.RequestForQuotationId
                OR rq.BusinessPartnerId <> i.BusinessPartnerId))
    )
        THROW 51214, 'Tender-document change-recipient source, dispatch, supplier, register, or tenant lineage is invalid.', 1;
END");

            migrationBuilder.Sql(@"
CREATE OR ALTER TRIGGER [TR_ProcurementTenderDocumentAcknowledgements_Immutable]
ON [ProcurementTenderDocumentAcknowledgements]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM deleted)
        THROW 51215, 'Tender-document acknowledgement records are append-only.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN ProcurementTenderDocumentIssuances di ON di.Id = i.IssuanceId
        LEFT JOIN ProcurementTenderDocumentChangeRecipients cr ON cr.Id = i.ChangeRecipientId
        LEFT JOIN ProcurementTenderDocumentChanges c ON c.Id = cr.ChangeId
        LEFT JOIN BusinessPartners bp ON bp.Id = i.BusinessPartnerId
        WHERE i.IsDeleted = 1
           OR LEN(LTRIM(RTRIM(i.AcknowledgementChannel))) = 0
           OR LEN(LTRIM(RTRIM(i.AcknowledgementReference))) = 0
           OR LEN(LTRIM(RTRIM(i.EvidenceReference))) = 0
           OR i.AcknowledgedByUserId = '00000000-0000-0000-0000-000000000000'
           OR (i.BusinessPartnerId IS NOT NULL AND
               (bp.Id IS NULL OR bp.TenantId <> i.TenantId OR bp.IsDeleted = 1))
           OR (i.IssuanceId IS NOT NULL AND
               (di.Id IS NULL OR di.TenantId <> i.TenantId OR di.IsDeleted = 1
                OR i.AcknowledgedAtUtc < di.IssuedAtUtc
                OR ISNULL(di.BusinessPartnerId, '00000000-0000-0000-0000-000000000000')
                   <> ISNULL(i.BusinessPartnerId, '00000000-0000-0000-0000-000000000000')
                OR di.IssuedByUserId = i.AcknowledgedByUserId))
           OR (i.ChangeRecipientId IS NOT NULL AND
               (cr.Id IS NULL OR cr.TenantId <> i.TenantId OR cr.IsDeleted = 1
                OR c.Id IS NULL OR c.IsDeleted = 1 OR c.Status <> 1
                OR i.AcknowledgedAtUtc < cr.DispatchedAtUtc
                OR ISNULL(cr.BusinessPartnerId, '00000000-0000-0000-0000-000000000000')
                   <> ISNULL(i.BusinessPartnerId, '00000000-0000-0000-0000-000000000000')
                OR c.RequestedByUserId = i.AcknowledgedByUserId))
    )
        THROW 51216, 'Tender-document acknowledgement target, supplier, actor, evidence, or tenant lineage is invalid.', 1;
END");

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementTenderControls_Lifecycle]
                ON [dbo].[ProcurementTenderControls]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id] = d.[Id] WHERE i.[Id] IS NULL)
                        THROW 51100, 'Statutory NCT/ICT tender controls cannot be deleted.', 1;
                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[Tenders] t ON t.[Id] = i.[TenderId] AND t.[TenantId] = i.[TenantId] AND t.[IsDeleted] = 0
                        LEFT JOIN [dbo].[ProcurementSourcingCases] sc ON sc.[Id] = i.[SourcingCaseId] AND sc.[TenantId] = i.[TenantId] AND sc.[IsDeleted] = 0
                        LEFT JOIN [dbo].[ProcurementPolicyMethodRules] mr ON mr.[Id] = i.[MethodRuleId] AND mr.[TenantId] = i.[TenantId] AND mr.[IsDeleted] = 0
                        LEFT JOIN [dbo].[ProcurementRequisitionAuthorityRoutes] ar ON ar.[Id] = i.[AuthorityRouteId] AND ar.[TenantId] = i.[TenantId] AND ar.[IsDeleted] = 0
                        WHERE i.[IsDeleted] = 1 OR t.[Id] IS NULL OR sc.[Id] IS NULL OR mr.[Id] IS NULL OR ar.[Id] IS NULL
                           OR t.[SourcingCaseId] <> i.[SourcingCaseId]
                           OR t.[SourcePurchaseRequisitionId] <> sc.[PurchaseRequisitionId]
                           OR t.[SourcingReleaseId] <> sc.[SourcingReleaseId]
                           OR t.[EstimatedValue] <> sc.[EstimatedValue]
                           OR UPPER(LTRIM(RTRIM(t.[Currency]))) <> sc.[CurrencyCode]
                           OR sc.[SelectedMethod] NOT IN (1, 2) OR sc.[SelectedMethod] <> i.[Method]
                           OR sc.[MethodRuleId] <> i.[MethodRuleId] OR sc.[MethodRuleCode] <> i.[MethodRuleCode]
                           OR sc.[AuthorityRouteId] <> i.[AuthorityRouteId] OR sc.[AuthorityRouteReference] <> i.[AuthorityRouteReference]
                           OR mr.[Method] <> i.[Method] OR mr.[RuleCode] <> i.[MethodRuleCode]
                           OR mr.[IsAllowed] = 0 OR mr.[IsEnabled] = 0
                           OR ar.[RouteReference] <> i.[AuthorityRouteReference]
                           OR ar.[PurchaseRequisitionId] <> sc.[PurchaseRequisitionId]
                    )
                        THROW 51101, 'Statutory tender tenant, sourcing-case, method-rule, or authority-route lineage is invalid.', 1;
                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE (d.[Id] IS NULL AND
                               (i.[Status] <> 0 OR i.[OpenedAtUtc] IS NOT NULL
                                OR i.[TechnicalEvaluatedAtUtc] IS NOT NULL OR i.[FinancialEvaluatedAtUtc] IS NOT NULL
                                OR i.[SubmittedForApprovalAtUtc] IS NOT NULL OR i.[ApprovedAtUtc] IS NOT NULL
                                OR i.[AwardedAtUtc] IS NOT NULL OR i.[ContractedAtUtc] IS NOT NULL OR i.[AcceptedAtUtc] IS NOT NULL))
                           OR (d.[Id] IS NOT NULL AND
                               (i.[TenantId] <> d.[TenantId] OR i.[TenderId] <> d.[TenderId]
                                OR i.[SourcingCaseId] <> d.[SourcingCaseId] OR i.[MethodRuleId] <> d.[MethodRuleId]
                                OR i.[AuthorityRouteId] <> d.[AuthorityRouteId] OR i.[Method] <> d.[Method]
                                OR i.[MethodRuleCode] <> d.[MethodRuleCode] OR i.[AuthorityRouteReference] <> d.[AuthorityRouteReference]
                                OR i.[AdvertisementReference] <> d.[AdvertisementReference] OR i.[PublicationChannel] <> d.[PublicationChannel]
                                OR i.[TenderDocumentReference] <> d.[TenderDocumentReference] OR i.[TenderDocumentVersion] <> d.[TenderDocumentVersion]
                                OR i.[DocumentFee] <> d.[DocumentFee] OR i.[AdvertisementEvidenceReference] <> d.[AdvertisementEvidenceReference]
                                OR i.[AdvertisedAtUtc] <> d.[AdvertisedAtUtc]
                                OR
                                   (
                                       i.[SubmissionDeadlineUtc] <> d.[SubmissionDeadlineUtc]
                                       AND
                                       (
                                           i.[SubmissionDeadlineUtc] <= d.[SubmissionDeadlineUtc]
                                           OR NOT EXISTS
                                              (
                                                  SELECT 1
                                                  FROM [dbo].[ProcurementTenderDocumentRegisters] r
                                                  JOIN [dbo].[ProcurementTenderDocumentChanges] c
                                                    ON c.[RegisterId] = r.[Id]
                                                   AND c.[TenantId] = r.[TenantId]
                                                   AND c.[IsDeleted] = 0
                                                  LEFT JOIN [dbo].[WorkflowInstances] wi
                                                    ON wi.[Id] = c.[WorkflowInstanceId]
                                                   AND wi.[TenantId] = c.[TenantId]
                                                   AND wi.[WorkflowDefinitionId] = c.[WorkflowDefinitionId]
                                                   AND wi.[EntityId] = c.[Id]
                                                   AND wi.[IsDeleted] = 0
                                                  WHERE r.[TenderId] = i.[TenderId]
                                                    AND r.[TenantId] = i.[TenantId]
                                                    AND r.[SourceType] = 0
                                                    AND r.[IsDeleted] = 0
                                                    AND c.[ChangeType] = 1
                                                    AND c.[PreviousValueUtc] = d.[SubmissionDeadlineUtc]
                                                    AND c.[NewValueUtc] = i.[SubmissionDeadlineUtc]
                                                    AND
                                                       (
                                                           (c.[Status] = 1
                                                            AND c.[WorkflowOutcome] = 'Approved')
                                                           OR
                                                           (c.[Status] = 0
                                                            AND wi.[Id] IS NOT NULL
                                                            AND wi.[Status] = 2)
                                                       )
                                              )
                                       )
                                   )
                                OR i.[OpeningScheduledAtUtc] <> d.[OpeningScheduledAtUtc] OR i.[CreatedAt] <> d.[CreatedAt]
                                OR i.[IsDeleted] <> d.[IsDeleted]
                                OR NOT (i.[Status] = d.[Status]
                                    OR (d.[Status] = 0 AND i.[Status] = 1)
                                    OR (d.[Status] = 1 AND i.[Status] = 2)
                                    OR (d.[Status] = 2 AND i.[Status] = 3)
                                    OR (d.[Status] = 3 AND i.[Status] = 4)
                                    OR (d.[Status] = 4 AND i.[Status] IN (5, 6))
                                    OR (d.[Status] = 5 AND i.[Status] = 7)
                                    OR (d.[Status] = 7 AND i.[Status] = 8)
                                    OR (d.[Status] = 8 AND i.[Status] = 9))
                                OR (d.[Status] = 0 AND i.[Status] = 1
                                    AND (i.[OpenedAtUtc] IS NULL
                                         OR i.[OpenedAtUtc] < i.[SubmissionDeadlineUtc]))
                                OR (d.[Status] >= 1 AND
                                    (ISNULL(i.[OpenedAtUtc], '19000101') <> ISNULL(d.[OpenedAtUtc], '19000101')
                                     OR ISNULL(i.[OpeningSnapshotJson], '') <> ISNULL(d.[OpeningSnapshotJson], '')
                                     OR ISNULL(i.[OpeningIntegrityHash], '') <> ISNULL(d.[OpeningIntegrityHash], '')
                                     OR ISNULL(i.[OpeningEvidenceReference], '') <> ISNULL(d.[OpeningEvidenceReference], '')))
                                OR (d.[Status] >= 2 AND
                                    (ISNULL(i.[TechnicalEvaluatedAtUtc], '19000101') <> ISNULL(d.[TechnicalEvaluatedAtUtc], '19000101')
                                     OR ISNULL(i.[TechnicalEvaluationSnapshotJson], '') <> ISNULL(d.[TechnicalEvaluationSnapshotJson], '')
                                     OR ISNULL(i.[TechnicalEvaluationIntegrityHash], '') <> ISNULL(d.[TechnicalEvaluationIntegrityHash], '')
                                     OR ISNULL(i.[TechnicalEvaluationEvidenceReference], '') <> ISNULL(d.[TechnicalEvaluationEvidenceReference], '')))
                                OR (d.[Status] >= 3 AND
                                    (ISNULL(i.[FinancialEvaluatedAtUtc], '19000101') <> ISNULL(d.[FinancialEvaluatedAtUtc], '19000101')
                                     OR ISNULL(i.[FinancialEvaluationSnapshotJson], '') <> ISNULL(d.[FinancialEvaluationSnapshotJson], '')
                                     OR ISNULL(i.[FinancialEvaluationIntegrityHash], '') <> ISNULL(d.[FinancialEvaluationIntegrityHash], '')
                                     OR ISNULL(i.[FinancialEvaluationEvidenceReference], '') <> ISNULL(d.[FinancialEvaluationEvidenceReference], '')
                                     OR ISNULL(i.[RecommendedBidId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[RecommendedBidId], '00000000-0000-0000-0000-000000000000')))
                                OR (d.[Status] >= 4 AND
                                    (ISNULL(i.[WorkflowDefinitionId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[WorkflowDefinitionId], '00000000-0000-0000-0000-000000000000')
                                     OR (d.[WorkflowInstanceId] IS NOT NULL AND
                                         ISNULL(i.[WorkflowInstanceId], '00000000-0000-0000-0000-000000000000') <> d.[WorkflowInstanceId])
                                     OR (d.[WorkflowInstanceId] IS NULL AND i.[WorkflowInstanceId] IS NOT NULL
                                         AND NOT (d.[Status] = 4 AND i.[Status] = 4))
                                     OR ISNULL(i.[SubmittedForApprovalAtUtc], '19000101') <> ISNULL(d.[SubmittedForApprovalAtUtc], '19000101')
                                     OR ISNULL(i.[SubmittedForApprovalById], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[SubmittedForApprovalById], '00000000-0000-0000-0000-000000000000')))
                                OR (d.[Status] >= 5 AND
                                    (ISNULL(i.[AuthorityApprovalReference], '') <> ISNULL(d.[AuthorityApprovalReference], '')
                                     OR ISNULL(i.[PpaApprovalReference], '') <> ISNULL(d.[PpaApprovalReference], '')
                                     OR ISNULL(i.[ApprovedAtUtc], '19000101') <> ISNULL(d.[ApprovedAtUtc], '19000101')
                                     OR ISNULL(i.[ApprovedById], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[ApprovedById], '00000000-0000-0000-0000-000000000000')))
                                OR (d.[Status] >= 7 AND
                                    (ISNULL(i.[AwardBidId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[AwardBidId], '00000000-0000-0000-0000-000000000000')
                                     OR ISNULL(i.[AwardReference], '') <> ISNULL(d.[AwardReference], '')
                                     OR ISNULL(i.[AwardEvidenceReference], '') <> ISNULL(d.[AwardEvidenceReference], '')
                                     OR ISNULL(i.[AwardedAtUtc], '19000101') <> ISNULL(d.[AwardedAtUtc], '19000101')))
                                OR (d.[Status] >= 8 AND
                                    (ISNULL(i.[ContractReference], '') <> ISNULL(d.[ContractReference], '')
                                     OR ISNULL(i.[ContractEvidenceReference], '') <> ISNULL(d.[ContractEvidenceReference], '')
                                     OR ISNULL(i.[ContractedAtUtc], '19000101') <> ISNULL(d.[ContractedAtUtc], '19000101')))
                                OR (d.[Status] >= 9 AND
                                    (ISNULL(i.[BidderAcceptanceReference], '') <> ISNULL(d.[BidderAcceptanceReference], '')
                                     OR ISNULL(i.[BidderAcceptanceEvidenceReference], '') <> ISNULL(d.[BidderAcceptanceEvidenceReference], '')
                                     OR ISNULL(i.[AcceptedAtUtc], '19000101') <> ISNULL(d.[AcceptedAtUtc], '19000101')))))
                    )
                        THROW 51102, 'Statutory tender immutable fields or lifecycle transition are invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_RequestForQuotations_StatutoryLifecycleGuard]
                ON [dbo].[RequestForQuotations]
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE (d.Status <> 'Draft' AND
                              ((ISNULL(i.SubmissionDeadline, '19000101') <> ISNULL(d.SubmissionDeadline, '19000101')
                                AND
                                (
                                    i.SubmissionDeadline IS NULL OR d.SubmissionDeadline IS NULL
                                    OR i.SubmissionDeadline <= d.SubmissionDeadline
                                    OR NOT EXISTS
                                       (
                                           SELECT 1
                                           FROM [dbo].[ProcurementTenderDocumentRegisters] r
                                           JOIN [dbo].[ProcurementTenderDocumentChanges] c
                                             ON c.[RegisterId] = r.[Id]
                                            AND c.[TenantId] = r.[TenantId]
                                            AND c.[IsDeleted] = 0
                                           LEFT JOIN [dbo].[WorkflowInstances] wi
                                             ON wi.[Id] = c.[WorkflowInstanceId]
                                            AND wi.[TenantId] = c.[TenantId]
                                            AND wi.[WorkflowDefinitionId] = c.[WorkflowDefinitionId]
                                            AND wi.[EntityId] = c.[Id]
                                            AND wi.[IsDeleted] = 0
                                           WHERE r.[RequestForQuotationId] = i.[Id]
                                             AND r.[TenantId] = i.[TenantId]
                                             AND r.[SourceType] = 1
                                             AND r.[IsDeleted] = 0
                                             AND c.[ChangeType] = 1
                                             AND c.[PreviousValueUtc] = d.[SubmissionDeadline]
                                             AND c.[NewValueUtc] = i.[SubmissionDeadline]
                                             AND
                                                (
                                                    (c.[Status] = 1
                                                     AND c.[WorkflowOutcome] = 'Approved')
                                                    OR
                                                    (c.[Status] = 0
                                                     AND wi.[Id] IS NOT NULL
                                                     AND wi.[Status] = 2)
                                                )
                                       )
                                ))
                               OR i.Title <> d.Title OR ISNULL(i.Description, '') <> ISNULL(d.Description, '')
                               OR ISNULL(i.ExternalRecipientEmails, '') <> ISNULL(d.ExternalRecipientEmails, '')
                               OR i.Currency <> d.Currency OR i.EstimatedValue <> d.EstimatedValue
                               OR i.IsDeleted <> d.IsDeleted))
                           OR (d.Status = 'Draft' AND i.Status NOT IN ('Draft','Sent','Cancelled'))
                           OR (d.Status = 'Sent' AND i.Status NOT IN ('Sent','Evaluation','Cancelled'))
                           OR (d.Status = 'Evaluation' AND i.Status NOT IN ('Evaluation','PendingApproval','Cancelled'))
                           OR (d.Status = 'PendingApproval' AND i.Status NOT IN ('PendingApproval','Approved','Rejected'))
                           OR (d.Status = 'Approved' AND i.Status NOT IN ('Approved','Awarded'))
                           OR (d.Status IN ('Awarded','Rejected','Cancelled','Closed') AND i.Status <> d.Status))
                        THROW 51094, 'RFQ issue terms or statutory lifecycle transition is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementTenderSubmissionReceipts_EffectiveDeadline]
                ON [dbo].[ProcurementTenderSubmissionReceipts]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        LEFT JOIN [dbo].[ProcurementTenderControls] c
                          ON c.[Id] = i.[TenderControlId] AND c.[TenantId] = i.[TenantId]
                        WHERE c.[Id] IS NULL
                           OR (d.[Id] IS NULL AND i.[SubmissionDeadlineUtc] <> c.[SubmissionDeadlineUtc])
                           OR (d.[Id] IS NOT NULL
                               AND i.[OpenedAtUtc] IS NOT NULL
                               AND i.[OpenedAtUtc] < c.[SubmissionDeadlineUtc])
                    )
                        THROW 51217, 'Tender receipt classification and opening must use the effective controlled submission deadline.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementRfqReceipts_EffectiveDeadline]
                ON [dbo].[ProcurementRfqReceipts]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        LEFT JOIN [dbo].[RequestForQuotations] r
                          ON r.[Id] = i.[RfqId] AND r.[TenantId] = i.[TenantId] AND r.[IsDeleted] = 0
                        WHERE r.[Id] IS NULL OR r.[SubmissionDeadline] IS NULL
                           OR (d.[Id] IS NULL AND i.[SubmissionDeadlineUtc] <> r.[SubmissionDeadline])
                           OR (d.[Id] IS NOT NULL
                               AND i.[OpenedAtUtc] IS NOT NULL
                               AND i.[OpenedAtUtc] < r.[SubmissionDeadline])
                    )
                        THROW 51218, 'RFQ receipt classification and opening must use the effective controlled submission deadline.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementRfqOpeningRegisters_EffectiveDeadline]
                ON [dbo].[ProcurementRfqOpeningRegisters]
                AFTER INSERT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[RequestForQuotations] r
                          ON r.[Id] = i.[RfqId] AND r.[TenantId] = i.[TenantId] AND r.[IsDeleted] = 0
                        WHERE r.[Id] IS NULL OR r.[SubmissionDeadline] IS NULL
                           OR i.[OpenedAtUtc] < r.[SubmissionDeadline]
                    )
                        THROW 51219, 'The RFQ opening register cannot be created before the effective controlled submission deadline.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementRfqOpeningRegisters_EffectiveDeadline];");
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementRfqReceipts_EffectiveDeadline];");
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementTenderSubmissionReceipts_EffectiveDeadline];");

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_RequestForQuotations_StatutoryLifecycleGuard]
                ON [dbo].[RequestForQuotations]
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE (d.Status <> 'Draft' AND
                              (ISNULL(i.SubmissionDeadline, '19000101') <> ISNULL(d.SubmissionDeadline, '19000101')
                               OR i.Title <> d.Title OR ISNULL(i.Description, '') <> ISNULL(d.Description, '')
                               OR ISNULL(i.ExternalRecipientEmails, '') <> ISNULL(d.ExternalRecipientEmails, '')
                               OR i.Currency <> d.Currency OR i.EstimatedValue <> d.EstimatedValue
                               OR i.IsDeleted <> d.IsDeleted))
                           OR (d.Status = 'Draft' AND i.Status NOT IN ('Draft','Sent','Cancelled'))
                           OR (d.Status = 'Sent' AND i.Status NOT IN ('Sent','Evaluation','Cancelled'))
                           OR (d.Status = 'Evaluation' AND i.Status NOT IN ('Evaluation','PendingApproval','Cancelled'))
                           OR (d.Status = 'PendingApproval' AND i.Status NOT IN ('PendingApproval','Approved','Rejected'))
                           OR (d.Status = 'Approved' AND i.Status NOT IN ('Approved','Awarded'))
                           OR (d.Status IN ('Awarded','Rejected','Cancelled','Closed') AND i.Status <> d.Status))
                        THROW 51094, 'RFQ issue terms or statutory lifecycle transition is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementTenderControls_Lifecycle]
                ON [dbo].[ProcurementTenderControls]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id] = d.[Id] WHERE i.[Id] IS NULL)
                        THROW 51100, 'Statutory NCT/ICT tender controls cannot be deleted.', 1;
                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[Tenders] t ON t.[Id] = i.[TenderId] AND t.[TenantId] = i.[TenantId] AND t.[IsDeleted] = 0
                        LEFT JOIN [dbo].[ProcurementSourcingCases] sc ON sc.[Id] = i.[SourcingCaseId] AND sc.[TenantId] = i.[TenantId] AND sc.[IsDeleted] = 0
                        LEFT JOIN [dbo].[ProcurementPolicyMethodRules] mr ON mr.[Id] = i.[MethodRuleId] AND mr.[TenantId] = i.[TenantId] AND mr.[IsDeleted] = 0
                        LEFT JOIN [dbo].[ProcurementRequisitionAuthorityRoutes] ar ON ar.[Id] = i.[AuthorityRouteId] AND ar.[TenantId] = i.[TenantId] AND ar.[IsDeleted] = 0
                        WHERE i.[IsDeleted] = 1 OR t.[Id] IS NULL OR sc.[Id] IS NULL OR mr.[Id] IS NULL OR ar.[Id] IS NULL
                           OR t.[SourcingCaseId] <> i.[SourcingCaseId]
                           OR t.[SourcePurchaseRequisitionId] <> sc.[PurchaseRequisitionId]
                           OR t.[SourcingReleaseId] <> sc.[SourcingReleaseId]
                           OR t.[EstimatedValue] <> sc.[EstimatedValue]
                           OR UPPER(LTRIM(RTRIM(t.[Currency]))) <> sc.[CurrencyCode]
                           OR sc.[SelectedMethod] NOT IN (1, 2) OR sc.[SelectedMethod] <> i.[Method]
                           OR sc.[MethodRuleId] <> i.[MethodRuleId] OR sc.[MethodRuleCode] <> i.[MethodRuleCode]
                           OR sc.[AuthorityRouteId] <> i.[AuthorityRouteId] OR sc.[AuthorityRouteReference] <> i.[AuthorityRouteReference]
                           OR mr.[Method] <> i.[Method] OR mr.[RuleCode] <> i.[MethodRuleCode]
                           OR mr.[IsAllowed] = 0 OR mr.[IsEnabled] = 0
                           OR ar.[RouteReference] <> i.[AuthorityRouteReference]
                           OR ar.[PurchaseRequisitionId] <> sc.[PurchaseRequisitionId]
                    )
                        THROW 51101, 'Statutory tender tenant, sourcing-case, method-rule, or authority-route lineage is invalid.', 1;
                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE (d.[Id] IS NULL AND
                               (i.[Status] <> 0 OR i.[OpenedAtUtc] IS NOT NULL
                                OR i.[TechnicalEvaluatedAtUtc] IS NOT NULL OR i.[FinancialEvaluatedAtUtc] IS NOT NULL
                                OR i.[SubmittedForApprovalAtUtc] IS NOT NULL OR i.[ApprovedAtUtc] IS NOT NULL
                                OR i.[AwardedAtUtc] IS NOT NULL OR i.[ContractedAtUtc] IS NOT NULL OR i.[AcceptedAtUtc] IS NOT NULL))
                           OR (d.[Id] IS NOT NULL AND
                               (i.[TenantId] <> d.[TenantId] OR i.[TenderId] <> d.[TenderId]
                                OR i.[SourcingCaseId] <> d.[SourcingCaseId] OR i.[MethodRuleId] <> d.[MethodRuleId]
                                OR i.[AuthorityRouteId] <> d.[AuthorityRouteId] OR i.[Method] <> d.[Method]
                                OR i.[MethodRuleCode] <> d.[MethodRuleCode] OR i.[AuthorityRouteReference] <> d.[AuthorityRouteReference]
                                OR i.[AdvertisementReference] <> d.[AdvertisementReference] OR i.[PublicationChannel] <> d.[PublicationChannel]
                                OR i.[TenderDocumentReference] <> d.[TenderDocumentReference] OR i.[TenderDocumentVersion] <> d.[TenderDocumentVersion]
                                OR i.[DocumentFee] <> d.[DocumentFee] OR i.[AdvertisementEvidenceReference] <> d.[AdvertisementEvidenceReference]
                                OR i.[AdvertisedAtUtc] <> d.[AdvertisedAtUtc] OR i.[SubmissionDeadlineUtc] <> d.[SubmissionDeadlineUtc]
                                OR i.[OpeningScheduledAtUtc] <> d.[OpeningScheduledAtUtc] OR i.[CreatedAt] <> d.[CreatedAt]
                                OR i.[IsDeleted] <> d.[IsDeleted]
                                OR NOT (i.[Status] = d.[Status]
                                    OR (d.[Status] = 0 AND i.[Status] = 1)
                                    OR (d.[Status] = 1 AND i.[Status] = 2)
                                    OR (d.[Status] = 2 AND i.[Status] = 3)
                                    OR (d.[Status] = 3 AND i.[Status] = 4)
                                    OR (d.[Status] = 4 AND i.[Status] IN (5, 6))
                                    OR (d.[Status] = 5 AND i.[Status] = 7)
                                    OR (d.[Status] = 7 AND i.[Status] = 8)
                                    OR (d.[Status] = 8 AND i.[Status] = 9))
                                OR (d.[Status] >= 1 AND
                                    (ISNULL(i.[OpenedAtUtc], '19000101') <> ISNULL(d.[OpenedAtUtc], '19000101')
                                     OR ISNULL(i.[OpeningSnapshotJson], '') <> ISNULL(d.[OpeningSnapshotJson], '')
                                     OR ISNULL(i.[OpeningIntegrityHash], '') <> ISNULL(d.[OpeningIntegrityHash], '')
                                     OR ISNULL(i.[OpeningEvidenceReference], '') <> ISNULL(d.[OpeningEvidenceReference], '')))
                                OR (d.[Status] >= 2 AND
                                    (ISNULL(i.[TechnicalEvaluatedAtUtc], '19000101') <> ISNULL(d.[TechnicalEvaluatedAtUtc], '19000101')
                                     OR ISNULL(i.[TechnicalEvaluationSnapshotJson], '') <> ISNULL(d.[TechnicalEvaluationSnapshotJson], '')
                                     OR ISNULL(i.[TechnicalEvaluationIntegrityHash], '') <> ISNULL(d.[TechnicalEvaluationIntegrityHash], '')
                                     OR ISNULL(i.[TechnicalEvaluationEvidenceReference], '') <> ISNULL(d.[TechnicalEvaluationEvidenceReference], '')))
                                OR (d.[Status] >= 3 AND
                                    (ISNULL(i.[FinancialEvaluatedAtUtc], '19000101') <> ISNULL(d.[FinancialEvaluatedAtUtc], '19000101')
                                     OR ISNULL(i.[FinancialEvaluationSnapshotJson], '') <> ISNULL(d.[FinancialEvaluationSnapshotJson], '')
                                     OR ISNULL(i.[FinancialEvaluationIntegrityHash], '') <> ISNULL(d.[FinancialEvaluationIntegrityHash], '')
                                     OR ISNULL(i.[FinancialEvaluationEvidenceReference], '') <> ISNULL(d.[FinancialEvaluationEvidenceReference], '')
                                     OR ISNULL(i.[RecommendedBidId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[RecommendedBidId], '00000000-0000-0000-0000-000000000000')))
                                OR (d.[Status] >= 4 AND
                                    (ISNULL(i.[WorkflowDefinitionId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[WorkflowDefinitionId], '00000000-0000-0000-0000-000000000000')
                                     OR (d.[WorkflowInstanceId] IS NOT NULL AND
                                         ISNULL(i.[WorkflowInstanceId], '00000000-0000-0000-0000-000000000000') <> d.[WorkflowInstanceId])
                                     OR (d.[WorkflowInstanceId] IS NULL AND i.[WorkflowInstanceId] IS NOT NULL
                                         AND NOT (d.[Status] = 4 AND i.[Status] = 4))
                                     OR ISNULL(i.[SubmittedForApprovalAtUtc], '19000101') <> ISNULL(d.[SubmittedForApprovalAtUtc], '19000101')
                                     OR ISNULL(i.[SubmittedForApprovalById], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[SubmittedForApprovalById], '00000000-0000-0000-0000-000000000000')))
                                OR (d.[Status] >= 5 AND
                                    (ISNULL(i.[AuthorityApprovalReference], '') <> ISNULL(d.[AuthorityApprovalReference], '')
                                     OR ISNULL(i.[PpaApprovalReference], '') <> ISNULL(d.[PpaApprovalReference], '')
                                     OR ISNULL(i.[ApprovedAtUtc], '19000101') <> ISNULL(d.[ApprovedAtUtc], '19000101')
                                     OR ISNULL(i.[ApprovedById], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[ApprovedById], '00000000-0000-0000-0000-000000000000')))
                                OR (d.[Status] >= 7 AND
                                    (ISNULL(i.[AwardBidId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[AwardBidId], '00000000-0000-0000-0000-000000000000')
                                     OR ISNULL(i.[AwardReference], '') <> ISNULL(d.[AwardReference], '')
                                     OR ISNULL(i.[AwardEvidenceReference], '') <> ISNULL(d.[AwardEvidenceReference], '')
                                     OR ISNULL(i.[AwardedAtUtc], '19000101') <> ISNULL(d.[AwardedAtUtc], '19000101')))
                                OR (d.[Status] >= 8 AND
                                    (ISNULL(i.[ContractReference], '') <> ISNULL(d.[ContractReference], '')
                                     OR ISNULL(i.[ContractEvidenceReference], '') <> ISNULL(d.[ContractEvidenceReference], '')
                                     OR ISNULL(i.[ContractedAtUtc], '19000101') <> ISNULL(d.[ContractedAtUtc], '19000101')))
                                OR (d.[Status] >= 9 AND
                                    (ISNULL(i.[BidderAcceptanceReference], '') <> ISNULL(d.[BidderAcceptanceReference], '')
                                     OR ISNULL(i.[BidderAcceptanceEvidenceReference], '') <> ISNULL(d.[BidderAcceptanceEvidenceReference], '')
                                     OR ISNULL(i.[AcceptedAtUtc], '19000101') <> ISNULL(d.[AcceptedAtUtc], '19000101')))))
                    )
                        THROW 51102, 'Statutory tender immutable fields or lifecycle transition are invalid.', 1;
                END
                """);

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_ProcurementTenderDocumentAcknowledgements_Immutable];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_ProcurementTenderDocumentChangeRecipients_Immutable];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_ProcurementTenderDocumentChanges_Lifecycle];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_ProcurementTenderDocumentIssuances_Immutable];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_ProcurementTenderDocumentRegisters_Immutable];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_ProcurementTenderDocumentTemplateMethods_Immutable];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_ProcurementTenderDocumentTemplateVersions_Lifecycle];");

            migrationBuilder.DropTable(
                name: "ProcurementTenderDocumentAcknowledgements");

            migrationBuilder.DropTable(
                name: "ProcurementTenderDocumentTemplateMethods");

            migrationBuilder.DropTable(
                name: "ProcurementTenderDocumentChangeRecipients");

            migrationBuilder.DropTable(
                name: "ProcurementTenderDocumentChanges");

            migrationBuilder.DropTable(
                name: "ProcurementTenderDocumentIssuances");

            migrationBuilder.DropTable(
                name: "ProcurementTenderDocumentRegisters");

            migrationBuilder.DropTable(
                name: "ProcurementTenderDocumentTemplateVersions");
        }
    }
}
