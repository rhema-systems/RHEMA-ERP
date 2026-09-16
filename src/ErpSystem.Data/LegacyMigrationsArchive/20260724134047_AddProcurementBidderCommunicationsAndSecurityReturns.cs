using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementBidderCommunicationsAndSecurityReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementBidderCommunicationRegisters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AwardFamily = table.Column<int>(type: "int", nullable: false),
                    AwardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AwardReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AwardedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AwardReadinessDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AwardReadinessDecisionSequence = table.Column<int>(type: "int", nullable: false),
                    AwardReadinessIntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AwardReadinessSourceIntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StandstillStartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StandstillEndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppealWindowEndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StandstillAuthorityReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AwardSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecipientSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecipientSnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InitializedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InitializedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InitializedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementBidderCommunicationRegisters", x => x.Id);
                    table.UniqueConstraint("AK_ProcurementBidderCommunicationRegisters_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ProcurementBidderCommunicationRegisters_State", "[SourceType] BETWEEN 0 AND 2 AND [AwardFamily] BETWEEN 0 AND 3 AND [AwardReadinessDecisionSequence] >= 1 AND [StandstillStartsAtUtc] >= [AwardedAtUtc] AND [StandstillEndsAtUtc] > [StandstillStartsAtUtc] AND [AppealWindowEndsAtUtc] >= [StandstillEndsAtUtc] AND LEN([AwardReadinessIntegrityHash]) = 64 AND LEN([AwardReadinessSourceIntegrityHash]) = 64 AND LEN([RecipientSnapshotHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ISJSON([AwardSnapshotJson]) = 1 AND ISJSON([RecipientSnapshotJson]) = 1");
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationRegisters_ProcurementAwardReadinessDecisions_AwardReadinessDecisionId",
                        column: x => x.AwardReadinessDecisionId,
                        principalTable: "ProcurementAwardReadinessDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationRegisters_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementBidderCommunicationRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegisterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    BidOrQuoteIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PartnerCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PartnerName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    RecipientPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    LineageHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementBidderCommunicationRecipients", x => x.Id);
                    table.UniqueConstraint("AK_ProcurementBidderCommunicationRecipients_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ProcurementBidderCommunicationRecipients_State", "[Outcome] BETWEEN 0 AND 1 AND ISJSON([BidOrQuoteIdsJson]) = 1 AND LEN([LineageHash]) = 64 AND LEN([IntegrityHash]) = 64");
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationRecipients_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationRecipients_ProcurementBidderCommunicationRegisters_TenantId_RegisterId",
                        columns: x => new { x.TenantId, x.RegisterId },
                        principalTable: "ProcurementBidderCommunicationRegisters",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationRecipients_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementBidderAppeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Grounds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EvidenceWorkflowDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvidenceFileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FiledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FiledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiledByBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementBidderAppeals", x => x.Id);
                    table.UniqueConstraint("AK_ProcurementBidderAppeals_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ProcurementBidderAppeals_State", "[Sequence] >= 1 AND LEN([IntegrityHash]) = 64 AND ([EvidenceWorkflowDocumentId] IS NULL OR [EvidenceFileUploadRecordId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ProcurementBidderAppeals_BusinessPartners_FiledByBusinessPartnerId",
                        column: x => x.FiledByBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderAppeals_FileUploadRecords_EvidenceFileUploadRecordId",
                        column: x => x.EvidenceFileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderAppeals_ProcurementBidderCommunicationRecipients_TenantId_RecipientId",
                        columns: x => new { x.TenantId, x.RecipientId },
                        principalTable: "ProcurementBidderCommunicationRecipients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderAppeals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderAppeals_WorkflowEvidenceDocuments_EvidenceWorkflowDocumentId",
                        column: x => x.EvidenceWorkflowDocumentId,
                        principalTable: "WorkflowEvidenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementBidderCommunicationLetterVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    TemplateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateReference = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TemplateChecksumSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContentReference = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ContentChecksumSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ApprovalEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ApprovalWorkflowEvidenceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalFileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementBidderCommunicationLetterVersions", x => x.Id);
                    table.UniqueConstraint("AK_ProcurementBidderCommunicationLetterVersions_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ProcurementBidderCommunicationLetterVersions_State", "[Version] >= 1 AND LEN([TemplateChecksumSha256]) = 64 AND LEN([ContentChecksumSha256]) = 64 AND LEN([IntegrityHash]) = 64 AND ([ApprovalWorkflowEvidenceDocumentId] IS NULL OR [ApprovalFileUploadRecordId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationLetterVersions_FileUploadRecords_ApprovalFileUploadRecordId",
                        column: x => x.ApprovalFileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationLetterVersions_ProcurementBidderCommunicationRecipients_TenantId_RecipientId",
                        columns: x => new { x.TenantId, x.RecipientId },
                        principalTable: "ProcurementBidderCommunicationRecipients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationLetterVersions_ProcurementTenderDocumentTemplateVersions_TemplateVersionId",
                        column: x => x.TemplateVersionId,
                        principalTable: "ProcurementTenderDocumentTemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationLetterVersions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationLetterVersions_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationLetterVersions_WorkflowEvidenceDocuments_ApprovalWorkflowEvidenceDocumentId",
                        column: x => x.ApprovalWorkflowEvidenceDocumentId,
                        principalTable: "WorkflowEvidenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationLetterVersions_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementTenderSecurityInstruments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestForQuotationQuoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InstrumentType = table.Column<int>(type: "int", nullable: false),
                    InstrumentReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IssuerName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EvidenceWorkflowDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvidenceFileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RegisteredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegisteredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementTenderSecurityInstruments", x => x.Id);
                    table.UniqueConstraint("AK_ProcurementTenderSecurityInstruments_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ProcurementTenderSecurityInstruments_State", "[InstrumentType] BETWEEN 0 AND 4 AND [Amount] > 0 AND LEN([CurrencyCode]) = 3 AND [ExpiresAtUtc] > [IssuedAtUtc] AND LEN([IntegrityHash]) = 64 AND ([EvidenceWorkflowDocumentId] IS NULL OR [EvidenceFileUploadRecordId] IS NULL)");
                    table.CheckConstraint("CK_ProcurementTenderSecurityInstruments_Subject", "([TenderBidId] IS NOT NULL AND [RequestForQuotationQuoteId] IS NULL) OR ([TenderBidId] IS NULL AND [RequestForQuotationQuoteId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ProcurementTenderSecurityInstruments_FileUploadRecords_EvidenceFileUploadRecordId",
                        column: x => x.EvidenceFileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderSecurityInstruments_ProcurementBidderCommunicationRecipients_TenantId_RecipientId",
                        columns: x => new { x.TenantId, x.RecipientId },
                        principalTable: "ProcurementBidderCommunicationRecipients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderSecurityInstruments_RequestForQuotationQuotes_RequestForQuotationQuoteId",
                        column: x => x.RequestForQuotationQuoteId,
                        principalTable: "RequestForQuotationQuotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderSecurityInstruments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderSecurityInstruments_TenderBids_TenderBidId",
                        column: x => x.TenderBidId,
                        principalTable: "TenderBids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderSecurityInstruments_WorkflowEvidenceDocuments_EvidenceWorkflowDocumentId",
                        column: x => x.EvidenceWorkflowDocumentId,
                        principalTable: "WorkflowEvidenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementBidderAppealDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecisionReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EvidenceWorkflowDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvidenceFileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecidedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementBidderAppealDecisions", x => x.Id);
                    table.CheckConstraint("CK_ProcurementBidderAppealDecisions_State", "[Outcome] BETWEEN 0 AND 2 AND LEN([IntegrityHash]) = 64 AND ([EvidenceWorkflowDocumentId] IS NULL OR [EvidenceFileUploadRecordId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ProcurementBidderAppealDecisions_FileUploadRecords_EvidenceFileUploadRecordId",
                        column: x => x.EvidenceFileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderAppealDecisions_ProcurementBidderAppeals_TenantId_AppealId",
                        columns: x => new { x.TenantId, x.AppealId },
                        principalTable: "ProcurementBidderAppeals",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderAppealDecisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderAppealDecisions_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderAppealDecisions_WorkflowEvidenceDocuments_EvidenceWorkflowDocumentId",
                        column: x => x.EvidenceWorkflowDocumentId,
                        principalTable: "WorkflowEvidenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderAppealDecisions_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementBidderCommunicationDispatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LetterVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    DispatchReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DispatchEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DispatchWorkflowEvidenceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DispatchFileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DispatchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DispatchedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DispatchedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementBidderCommunicationDispatches", x => x.Id);
                    table.UniqueConstraint("AK_ProcurementBidderCommunicationDispatches_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ProcurementBidderCommunicationDispatches_State", "[Sequence] >= 1 AND [Channel] BETWEEN 0 AND 4 AND LEN([IntegrityHash]) = 64 AND ([DispatchWorkflowEvidenceDocumentId] IS NULL OR [DispatchFileUploadRecordId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationDispatches_FileUploadRecords_DispatchFileUploadRecordId",
                        column: x => x.DispatchFileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationDispatches_ProcurementBidderCommunicationLetterVersions_TenantId_LetterVersionId",
                        columns: x => new { x.TenantId, x.LetterVersionId },
                        principalTable: "ProcurementBidderCommunicationLetterVersions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationDispatches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationDispatches_WorkflowEvidenceDocuments_DispatchWorkflowEvidenceDocumentId",
                        column: x => x.DispatchWorkflowEvidenceDocumentId,
                        principalTable: "WorkflowEvidenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementTenderSecurityActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SecurityInstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EvidenceWorkflowDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvidenceFileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActionedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActionedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementTenderSecurityActions", x => x.Id);
                    table.CheckConstraint("CK_ProcurementTenderSecurityActions_State", "[Sequence] >= 1 AND [ActionType] BETWEEN 0 AND 2 AND LEN([IntegrityHash]) = 64 AND ([EvidenceWorkflowDocumentId] IS NULL OR [EvidenceFileUploadRecordId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ProcurementTenderSecurityActions_FileUploadRecords_EvidenceFileUploadRecordId",
                        column: x => x.EvidenceFileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderSecurityActions_ProcurementTenderSecurityInstruments_TenantId_SecurityInstrumentId",
                        columns: x => new { x.TenantId, x.SecurityInstrumentId },
                        principalTable: "ProcurementTenderSecurityInstruments",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderSecurityActions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderSecurityActions_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderSecurityActions_WorkflowEvidenceDocuments_EvidenceWorkflowDocumentId",
                        column: x => x.EvidenceWorkflowDocumentId,
                        principalTable: "WorkflowEvidenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderSecurityActions_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementBidderCommunicationAcknowledgements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DispatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcknowledgedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcknowledgedByBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcknowledgementChannel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AcknowledgementReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementBidderCommunicationAcknowledgements", x => x.Id);
                    table.CheckConstraint("CK_ProcurementBidderCommunicationAcknowledgements_State", "[Sequence] >= 1 AND [Outcome] BETWEEN 0 AND 2 AND LEN([IntegrityHash]) = 64");
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationAcknowledgements_BusinessPartners_AcknowledgedByBusinessPartnerId",
                        column: x => x.AcknowledgedByBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationAcknowledgements_ProcurementBidderCommunicationDispatches_TenantId_DispatchId",
                        columns: x => new { x.TenantId, x.DispatchId },
                        principalTable: "ProcurementBidderCommunicationDispatches",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationAcknowledgements_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementBidderCommunicationDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DispatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementBidderCommunicationDeliveries", x => x.Id);
                    table.CheckConstraint("CK_ProcurementBidderCommunicationDeliveries_State", "[Sequence] >= 1 AND [Outcome] BETWEEN 0 AND 3 AND LEN([IntegrityHash]) = 64");
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationDeliveries_ProcurementBidderCommunicationDispatches_TenantId_DispatchId",
                        columns: x => new { x.TenantId, x.DispatchId },
                        principalTable: "ProcurementBidderCommunicationDispatches",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBidderCommunicationDeliveries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderAppealDecisions_EvidenceFileUploadRecordId",
                table: "ProcurementBidderAppealDecisions",
                column: "EvidenceFileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderAppealDecisions_EvidenceWorkflowDocumentId",
                table: "ProcurementBidderAppealDecisions",
                column: "EvidenceWorkflowDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderAppealDecisions_TenantId_AppealId",
                table: "ProcurementBidderAppealDecisions",
                columns: new[] { "TenantId", "AppealId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderAppealDecisions_TenantId_AppealId_IdempotencyKey",
                table: "ProcurementBidderAppealDecisions",
                columns: new[] { "TenantId", "AppealId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderAppealDecisions_TenantId_WorkflowInstanceId",
                table: "ProcurementBidderAppealDecisions",
                columns: new[] { "TenantId", "WorkflowInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderAppealDecisions_WorkflowDefinitionId",
                table: "ProcurementBidderAppealDecisions",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderAppealDecisions_WorkflowInstanceId",
                table: "ProcurementBidderAppealDecisions",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderAppeals_EvidenceFileUploadRecordId",
                table: "ProcurementBidderAppeals",
                column: "EvidenceFileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderAppeals_EvidenceWorkflowDocumentId",
                table: "ProcurementBidderAppeals",
                column: "EvidenceWorkflowDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderAppeals_FiledByBusinessPartnerId",
                table: "ProcurementBidderAppeals",
                column: "FiledByBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderAppeals_TenantId_FiledByBusinessPartnerId",
                table: "ProcurementBidderAppeals",
                columns: new[] { "TenantId", "FiledByBusinessPartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderAppeals_TenantId_RecipientId_IdempotencyKey",
                table: "ProcurementBidderAppeals",
                columns: new[] { "TenantId", "RecipientId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderAppeals_TenantId_RecipientId_Sequence",
                table: "ProcurementBidderAppeals",
                columns: new[] { "TenantId", "RecipientId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationAcknowledgements_AcknowledgedByBusinessPartnerId",
                table: "ProcurementBidderCommunicationAcknowledgements",
                column: "AcknowledgedByBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationAcknowledgements_TenantId_AcknowledgedByBusinessPartnerId",
                table: "ProcurementBidderCommunicationAcknowledgements",
                columns: new[] { "TenantId", "AcknowledgedByBusinessPartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationAcknowledgements_TenantId_DispatchId_IdempotencyKey",
                table: "ProcurementBidderCommunicationAcknowledgements",
                columns: new[] { "TenantId", "DispatchId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationAcknowledgements_TenantId_DispatchId_Sequence",
                table: "ProcurementBidderCommunicationAcknowledgements",
                columns: new[] { "TenantId", "DispatchId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationDeliveries_TenantId_DispatchId_IdempotencyKey",
                table: "ProcurementBidderCommunicationDeliveries",
                columns: new[] { "TenantId", "DispatchId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationDeliveries_TenantId_DispatchId_Sequence",
                table: "ProcurementBidderCommunicationDeliveries",
                columns: new[] { "TenantId", "DispatchId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationDispatches_DispatchFileUploadRecordId",
                table: "ProcurementBidderCommunicationDispatches",
                column: "DispatchFileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationDispatches_DispatchWorkflowEvidenceDocumentId",
                table: "ProcurementBidderCommunicationDispatches",
                column: "DispatchWorkflowEvidenceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationDispatches_TenantId_DispatchedAtUtc",
                table: "ProcurementBidderCommunicationDispatches",
                columns: new[] { "TenantId", "DispatchedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationDispatches_TenantId_LetterVersionId_IdempotencyKey",
                table: "ProcurementBidderCommunicationDispatches",
                columns: new[] { "TenantId", "LetterVersionId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationDispatches_TenantId_LetterVersionId_Sequence",
                table: "ProcurementBidderCommunicationDispatches",
                columns: new[] { "TenantId", "LetterVersionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationLetterVersions_ApprovalFileUploadRecordId",
                table: "ProcurementBidderCommunicationLetterVersions",
                column: "ApprovalFileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationLetterVersions_ApprovalWorkflowEvidenceDocumentId",
                table: "ProcurementBidderCommunicationLetterVersions",
                column: "ApprovalWorkflowEvidenceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationLetterVersions_TemplateVersionId",
                table: "ProcurementBidderCommunicationLetterVersions",
                column: "TemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationLetterVersions_TenantId_RecipientId_IdempotencyKey",
                table: "ProcurementBidderCommunicationLetterVersions",
                columns: new[] { "TenantId", "RecipientId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationLetterVersions_TenantId_RecipientId_Version",
                table: "ProcurementBidderCommunicationLetterVersions",
                columns: new[] { "TenantId", "RecipientId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationLetterVersions_TenantId_TemplateVersionId",
                table: "ProcurementBidderCommunicationLetterVersions",
                columns: new[] { "TenantId", "TemplateVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationLetterVersions_TenantId_WorkflowInstanceId",
                table: "ProcurementBidderCommunicationLetterVersions",
                columns: new[] { "TenantId", "WorkflowInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationLetterVersions_WorkflowDefinitionId",
                table: "ProcurementBidderCommunicationLetterVersions",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationLetterVersions_WorkflowInstanceId",
                table: "ProcurementBidderCommunicationLetterVersions",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationRecipients_BusinessPartnerId",
                table: "ProcurementBidderCommunicationRecipients",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationRecipients_TenantId_BusinessPartnerId_Outcome",
                table: "ProcurementBidderCommunicationRecipients",
                columns: new[] { "TenantId", "BusinessPartnerId", "Outcome" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationRecipients_TenantId_RegisterId_BusinessPartnerId",
                table: "ProcurementBidderCommunicationRecipients",
                columns: new[] { "TenantId", "RegisterId", "BusinessPartnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationRegisters_AwardReadinessDecisionId",
                table: "ProcurementBidderCommunicationRegisters",
                column: "AwardReadinessDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationRegisters_TenantId_AwardFamily_AwardId",
                table: "ProcurementBidderCommunicationRegisters",
                columns: new[] { "TenantId", "AwardFamily", "AwardId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationRegisters_TenantId_AwardReadinessDecisionId",
                table: "ProcurementBidderCommunicationRegisters",
                columns: new[] { "TenantId", "AwardReadinessDecisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationRegisters_TenantId_SourceType_SourceId",
                table: "ProcurementBidderCommunicationRegisters",
                columns: new[] { "TenantId", "SourceType", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationRegisters_TenantId_SourceType_SourceId_IdempotencyKey",
                table: "ProcurementBidderCommunicationRegisters",
                columns: new[] { "TenantId", "SourceType", "SourceId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBidderCommunicationRegisters_TenantId_StandstillEndsAtUtc_AppealWindowEndsAtUtc",
                table: "ProcurementBidderCommunicationRegisters",
                columns: new[] { "TenantId", "StandstillEndsAtUtc", "AppealWindowEndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSecurityActions_EvidenceFileUploadRecordId",
                table: "ProcurementTenderSecurityActions",
                column: "EvidenceFileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSecurityActions_EvidenceWorkflowDocumentId",
                table: "ProcurementTenderSecurityActions",
                column: "EvidenceWorkflowDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSecurityActions_TenantId_SecurityInstrumentId",
                table: "ProcurementTenderSecurityActions",
                columns: new[] { "TenantId", "SecurityInstrumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSecurityActions_TenantId_SecurityInstrumentId_IdempotencyKey",
                table: "ProcurementTenderSecurityActions",
                columns: new[] { "TenantId", "SecurityInstrumentId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSecurityActions_TenantId_SecurityInstrumentId_Sequence",
                table: "ProcurementTenderSecurityActions",
                columns: new[] { "TenantId", "SecurityInstrumentId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSecurityActions_TenantId_WorkflowInstanceId",
                table: "ProcurementTenderSecurityActions",
                columns: new[] { "TenantId", "WorkflowInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSecurityActions_WorkflowDefinitionId",
                table: "ProcurementTenderSecurityActions",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSecurityActions_WorkflowInstanceId",
                table: "ProcurementTenderSecurityActions",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSecurityInstruments_EvidenceFileUploadRecordId",
                table: "ProcurementTenderSecurityInstruments",
                column: "EvidenceFileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSecurityInstruments_EvidenceWorkflowDocumentId",
                table: "ProcurementTenderSecurityInstruments",
                column: "EvidenceWorkflowDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSecurityInstruments_RequestForQuotationQuoteId",
                table: "ProcurementTenderSecurityInstruments",
                column: "RequestForQuotationQuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSecurityInstruments_TenantId_ExpiresAtUtc",
                table: "ProcurementTenderSecurityInstruments",
                columns: new[] { "TenantId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSecurityInstruments_TenantId_RecipientId_IdempotencyKey",
                table: "ProcurementTenderSecurityInstruments",
                columns: new[] { "TenantId", "RecipientId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSecurityInstruments_TenantId_RecipientId_InstrumentReference",
                table: "ProcurementTenderSecurityInstruments",
                columns: new[] { "TenantId", "RecipientId", "InstrumentReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSecurityInstruments_TenantId_RequestForQuotationQuoteId",
                table: "ProcurementTenderSecurityInstruments",
                columns: new[] { "TenantId", "RequestForQuotationQuoteId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSecurityInstruments_TenantId_TenderBidId",
                table: "ProcurementTenderSecurityInstruments",
                columns: new[] { "TenantId", "TenderBidId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSecurityInstruments_TenderBidId",
                table: "ProcurementTenderSecurityInstruments",
                column: "TenderBidId");

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementBidderCommunicationRegisters_Immutable]
                ON [dbo].[ProcurementBidderCommunicationRegisters]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51500, 'Bidder communication registers are append-only.', 1;

                    IF (SELECT COUNT(*) FROM inserted) <> 1
                        THROW 51501, 'A bidder communication register must be appended one source at a time.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.IsDeleted = 1
                           OR i.SourceId = '00000000-0000-0000-0000-000000000000'
                           OR i.AwardId = '00000000-0000-0000-0000-000000000000'
                           OR i.InitializedByUserId = '00000000-0000-0000-0000-000000000000'
                           OR i.CreatedById IS NULL OR i.CreatedById <> i.InitializedByUserId
                           OR i.CreatedAt <> i.InitializedAtUtc
                           OR i.AwardReadinessIntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR i.AwardReadinessSourceIntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR i.RecipientSnapshotHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR i.IntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR LEFT(LTRIM(i.AwardSnapshotJson), 1) <> '{'
                           OR LEFT(LTRIM(i.RecipientSnapshotJson), 1) <> '['
                           OR NOT EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementAwardReadinessDecisions] d
                               WHERE d.Id = i.AwardReadinessDecisionId
                                 AND d.TenantId = i.TenantId
                                 AND d.SourceType = i.SourceType
                                 AND d.SourceId = i.SourceId
                                 AND d.DecisionSequence = i.AwardReadinessDecisionSequence
                                 AND d.Status = 1
                                 AND d.IntegrityHash = i.AwardReadinessIntegrityHash
                                 AND d.SourceIntegrityHash = i.AwardReadinessSourceIntegrityHash
                                 AND d.IsDeleted = 0
                           )
                           OR (i.SourceType IN (1, 2) AND NOT EXISTS (
                                SELECT 1 FROM [dbo].[Tenders] t
                                WHERE t.Id = i.SourceId AND t.TenantId = i.TenantId
                                  AND t.IsDeleted = 0
                            ))
                           OR (i.SourceType = 0 AND NOT EXISTS (
                                SELECT 1 FROM [dbo].[RequestForQuotations] r
                                WHERE r.Id = i.SourceId AND r.TenantId = i.TenantId
                                  AND r.IsDeleted = 0
                            ))
                           OR (i.AwardFamily = 0 AND (
                               i.SourceType <> 0 OR i.AwardId <> i.SourceId))
                           OR (i.AwardFamily = 1 AND (
                               i.SourceType <> 1 OR NOT EXISTS (
                                   SELECT 1
                                   FROM [dbo].[ProcurementTenderControls] c
                                   WHERE c.Id = i.AwardId AND c.TenderId = i.SourceId
                                     AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                               )))
                           OR (i.AwardFamily = 2 AND (
                               i.SourceType <> 2 OR NOT EXISTS (
                                   SELECT 1
                                   FROM [dbo].[ProcurementExceptionalSourcingControls] c
                                   WHERE c.Id = i.AwardId AND c.TenderId = i.SourceId
                                     AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                               )))
                           OR (i.AwardFamily = 3 AND (
                               i.SourceType <> 1 OR i.AwardId <> i.SourceId
                               OR NOT EXISTS (
                                   SELECT 1 FROM [dbo].[TenderAwards] a
                                   WHERE a.TenderId = i.SourceId
                                     AND a.TenantId = i.TenantId
                                     AND a.IsDeleted = 0 AND a.Status <> 'Cancelled'
                               )))
                    )
                        THROW 51501, 'Bidder communication source, award, readiness, actor, hash, or tenant lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementBidderCommunicationRecipients_Immutable]
                ON [dbo].[ProcurementBidderCommunicationRecipients]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51502, 'Bidder communication recipients are append-only.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.IsDeleted = 1
                           OR i.BusinessPartnerId = '00000000-0000-0000-0000-000000000000'
                           OR i.LineageHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR i.IntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR LEFT(LTRIM(i.BidOrQuoteIdsJson), 1) <> '['
                           OR NOT EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementBidderCommunicationRegisters] r
                               WHERE r.Id = i.RegisterId AND r.TenantId = i.TenantId
                           )
                           OR NOT EXISTS (
                               SELECT 1 FROM [dbo].[BusinessPartners] bp
                               WHERE bp.Id = i.BusinessPartnerId
                                 AND bp.TenantId = i.TenantId AND bp.IsDeleted = 0
                           )
                           OR EXISTS (
                               SELECT 1 FROM OPENJSON(i.BidOrQuoteIdsJson) j
                               WHERE j.[type] <> 1
                                  OR TRY_CONVERT(uniqueidentifier, j.[value]) IS NULL
                           )
                           OR NOT EXISTS (SELECT 1 FROM OPENJSON(i.BidOrQuoteIdsJson))
                           OR EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementBidderCommunicationRegisters] r
                               WHERE r.Id = i.RegisterId
                                 AND (
                                      (r.SourceType = 0 AND EXISTS (
                                          SELECT 1
                                          FROM OPENJSON(i.BidOrQuoteIdsJson) j
                                          WHERE NOT EXISTS (
                                             SELECT 1
                                             FROM [dbo].[RequestForQuotationQuotes] q
                                             WHERE q.Id = TRY_CONVERT(uniqueidentifier, j.[value])
                                                AND q.RfqId = r.SourceId
                                               AND q.BusinessPartnerId = i.BusinessPartnerId
                                               AND q.TenantId = i.TenantId
                                               AND q.IsDeleted = 0
                                         )
                                     ))
                                      OR (r.SourceType IN (1, 2) AND EXISTS (
                                          SELECT 1
                                          FROM OPENJSON(i.BidOrQuoteIdsJson) j
                                          WHERE NOT EXISTS (
                                             SELECT 1
                                             FROM [dbo].[TenderBids] b
                                             WHERE b.Id = TRY_CONVERT(uniqueidentifier, j.[value])
                                               AND b.TenderId = r.SourceId
                                               AND b.BusinessPartnerId = i.BusinessPartnerId
                                               AND b.TenantId = i.TenantId
                                                AND b.IsDeleted = 0
                                          )
                                      ))
                                      OR (r.AwardFamily = 1 AND EXISTS (
                                          SELECT 1
                                          FROM OPENJSON(i.BidOrQuoteIdsJson) j
                                          WHERE NOT EXISTS (
                                              SELECT 1
                                              FROM [dbo].[ProcurementTenderSubmissionReceipts] sr
                                              WHERE sr.TenderControlId = r.AwardId
                                                AND sr.TenderBidId = TRY_CONVERT(uniqueidentifier, j.[value])
                                                AND sr.BusinessPartnerId = i.BusinessPartnerId
                                                AND sr.TenantId = i.TenantId
                                                AND sr.Disposition = 0
                                                AND sr.IsDeleted = 0
                                          )
                                      ))
                                  )
                            )
                           OR EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementBidderCommunicationRegisters] r
                               JOIN [dbo].[ProcurementAwardReadinessDecisions] d
                                 ON d.Id = r.AwardReadinessDecisionId
                               WHERE r.Id = i.RegisterId
                                 AND (
                                     (i.Outcome = 0 AND NOT EXISTS (
                                         SELECT 1
                                         FROM OPENJSON(d.RecommendedBusinessPartnerIdsJson) j
                                         WHERE TRY_CONVERT(uniqueidentifier, j.[value]) = i.BusinessPartnerId
                                     ))
                                     OR (i.Outcome = 1 AND EXISTS (
                                         SELECT 1
                                         FROM OPENJSON(d.RecommendedBusinessPartnerIdsJson) j
                                         WHERE TRY_CONVERT(uniqueidentifier, j.[value]) = i.BusinessPartnerId
                                     ))
                                 )
                           )
                    )
                        THROW 51503, 'Bidder recipient supplier, source subject, outcome, hash, or tenant lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementBidderCommunicationLetterVersions_Immutable]
                ON [dbo].[ProcurementBidderCommunicationLetterVersions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51504, 'Approved bidder letter versions are append-only.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.IsDeleted = 1
                           OR i.TemplateChecksumSha256 COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR i.ContentChecksumSha256 COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR i.IntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR NOT EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementBidderCommunicationRecipients] r
                               WHERE r.Id = i.RecipientId AND r.TenantId = i.TenantId
                           )
                           OR NOT EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementTenderDocumentTemplateVersions] t
                               JOIN [dbo].[ProcurementBidderCommunicationRecipients] r
                                 ON r.Id = i.RecipientId
                               WHERE t.Id = i.TemplateVersionId
                                 AND t.TenantId = i.TenantId
                                 AND t.IsDeleted = 0 AND t.Status = 2
                                 AND t.ContentChecksumSha256 = i.TemplateChecksumSha256
                                 AND ((r.Outcome = 0 AND t.DocumentTypeCode = 'SUCCESSFUL_BIDDER_LETTER')
                                   OR (r.Outcome = 1 AND t.DocumentTypeCode = 'UNSUCCESSFUL_BIDDER_LETTER'))
                           )
                           OR NOT EXISTS (
                               SELECT 1 FROM [dbo].[WorkflowInstances] w
                               WHERE w.Id = i.WorkflowInstanceId
                                 AND w.WorkflowDefinitionId = i.WorkflowDefinitionId
                                 AND w.EntityId = i.RecipientId
                                 AND w.TenantId = i.TenantId
                                 AND w.Status = 2 AND w.CompletedDate IS NOT NULL
                                 AND w.IsDeleted = 0
                           )
                           OR (i.ApprovalWorkflowEvidenceDocumentId IS NOT NULL AND NOT EXISTS (
                               SELECT 1 FROM [dbo].[WorkflowEvidenceDocuments] e
                               WHERE e.Id = i.ApprovalWorkflowEvidenceDocumentId
                                 AND e.TenantId = i.TenantId AND e.IsDeleted = 0
                           ))
                           OR (i.ApprovalFileUploadRecordId IS NOT NULL AND NOT EXISTS (
                               SELECT 1 FROM [dbo].[FileUploadRecords] f
                               WHERE f.Id = i.ApprovalFileUploadRecordId
                                 AND f.TenantId = i.TenantId AND f.IsDeleted = 0
                           ))
                           OR i.Version <> (
                               SELECT ISNULL(MAX(prior.Version), 0) + 1
                               FROM [dbo].[ProcurementBidderCommunicationLetterVersions] prior
                               WHERE prior.TenantId = i.TenantId
                                 AND prior.RecipientId = i.RecipientId
                                 AND prior.Id <> i.Id
                           )
                    )
                        THROW 51505, 'Approved bidder letter template, workflow, evidence, sequence, hash, or tenant lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementBidderCommunicationDispatches_Immutable]
                ON [dbo].[ProcurementBidderCommunicationDispatches]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51506, 'Bidder letter dispatch records are append-only.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [dbo].[ProcurementBidderCommunicationLetterVersions] l
                          ON l.Id = i.LetterVersionId
                        JOIN [dbo].[ProcurementBidderCommunicationRecipients] r
                          ON r.Id = l.RecipientId
                        WHERE i.IsDeleted = 1
                           OR l.TenantId <> i.TenantId OR r.TenantId <> i.TenantId
                           OR i.IntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR i.Sequence <> (
                               SELECT ISNULL(MAX(prior.Sequence), 0) + 1
                               FROM [dbo].[ProcurementBidderCommunicationDispatches] prior
                               WHERE prior.TenantId = i.TenantId
                                 AND prior.LetterVersionId = i.LetterVersionId
                                 AND prior.Id <> i.Id
                           )
                            OR (i.Channel = 0 AND LOWER(LTRIM(RTRIM(ISNULL(r.RecipientEmail, ''))))
                                <> LOWER(LTRIM(RTRIM(i.Destination))))
                            OR (i.Channel = 1
                                AND REPLACE(REPLACE(REPLACE(REPLACE(ISNULL(r.RecipientPhone, ''), ' ', ''), '-', ''), '(', ''), ')', '')
                                <> REPLACE(REPLACE(REPLACE(REPLACE(i.Destination, ' ', ''), '-', ''), '(', ''), ')', ''))
                            OR (i.Channel = 2
                                AND LOWER(LTRIM(RTRIM(i.Destination))) NOT IN ('portal', LOWER(CONVERT(nvarchar(36), r.BusinessPartnerId))))
                           OR (i.DispatchWorkflowEvidenceDocumentId IS NOT NULL AND NOT EXISTS (
                               SELECT 1 FROM [dbo].[WorkflowEvidenceDocuments] e
                               WHERE e.Id = i.DispatchWorkflowEvidenceDocumentId
                                 AND e.TenantId = i.TenantId AND e.IsDeleted = 0
                           ))
                           OR (i.DispatchFileUploadRecordId IS NOT NULL AND NOT EXISTS (
                               SELECT 1 FROM [dbo].[FileUploadRecords] f
                               WHERE f.Id = i.DispatchFileUploadRecordId
                                 AND f.TenantId = i.TenantId AND f.IsDeleted = 0
                           ))
                    )
                        THROW 51507, 'Bidder dispatch recipient, destination, evidence, sequence, hash, or tenant lineage is invalid.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE NOT EXISTS (
                            SELECT 1
                            FROM [dbo].[ProcurementBidderCommunicationLetterVersions] l
                            WHERE l.Id = i.LetterVersionId AND l.TenantId = i.TenantId
                        )
                    )
                        THROW 51507, 'Bidder dispatch letter tenant lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementBidderCommunicationDeliveries_Immutable]
                ON [dbo].[ProcurementBidderCommunicationDeliveries]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51508, 'Bidder delivery events are append-only.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [dbo].[ProcurementBidderCommunicationDispatches] d
                          ON d.Id = i.DispatchId
                        WHERE i.IsDeleted = 1 OR d.TenantId <> i.TenantId
                           OR i.OccurredAtUtc < d.DispatchedAtUtc
                           OR i.IntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR i.Sequence <> (
                               SELECT ISNULL(MAX(prior.Sequence), 0) + 1
                               FROM [dbo].[ProcurementBidderCommunicationDeliveries] prior
                               WHERE prior.TenantId = i.TenantId
                                 AND prior.DispatchId = i.DispatchId
                                 AND prior.Id <> i.Id
                           )
                           OR EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementBidderCommunicationDeliveries] prior
                               WHERE prior.TenantId = i.TenantId
                                 AND prior.DispatchId = i.DispatchId
                                 AND prior.Id <> i.Id
                                 AND prior.Outcome <> 0
                           )
                    )
                        THROW 51509, 'Bidder delivery dispatch, chronology, terminal state, sequence, hash, or tenant lineage is invalid.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE NOT EXISTS (
                            SELECT 1 FROM [dbo].[ProcurementBidderCommunicationDispatches] d
                            WHERE d.Id = i.DispatchId AND d.TenantId = i.TenantId
                        )
                    )
                        THROW 51509, 'Bidder delivery dispatch tenant lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementBidderCommunicationAcknowledgements_Immutable]
                ON [dbo].[ProcurementBidderCommunicationAcknowledgements]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51510, 'Bidder acknowledgement records are append-only.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [dbo].[ProcurementBidderCommunicationDispatches] d
                          ON d.Id = i.DispatchId
                        JOIN [dbo].[ProcurementBidderCommunicationLetterVersions] l
                          ON l.Id = d.LetterVersionId
                        JOIN [dbo].[ProcurementBidderCommunicationRecipients] r
                          ON r.Id = l.RecipientId
                        WHERE i.IsDeleted = 1
                           OR d.TenantId <> i.TenantId OR l.TenantId <> i.TenantId
                           OR r.TenantId <> i.TenantId
                           OR i.AcknowledgedAtUtc < d.DispatchedAtUtc
                           OR i.IntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR i.Sequence <> (
                               SELECT ISNULL(MAX(prior.Sequence), 0) + 1
                               FROM [dbo].[ProcurementBidderCommunicationAcknowledgements] prior
                               WHERE prior.TenantId = i.TenantId
                                 AND prior.DispatchId = i.DispatchId
                                 AND prior.Id <> i.Id
                           )
                           OR (i.AcknowledgedByBusinessPartnerId IS NOT NULL
                               AND i.AcknowledgedByBusinessPartnerId <> r.BusinessPartnerId)
                    )
                        THROW 51511, 'Bidder acknowledgement recipient, chronology, sequence, hash, or tenant lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementBidderAppeals_Immutable]
                ON [dbo].[ProcurementBidderAppeals]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51512, 'Bidder appeals are append-only.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [dbo].[ProcurementBidderCommunicationRecipients] r
                          ON r.Id = i.RecipientId
                        JOIN [dbo].[ProcurementBidderCommunicationRegisters] g
                          ON g.Id = r.RegisterId
                        WHERE i.IsDeleted = 1 OR r.TenantId <> i.TenantId
                           OR g.TenantId <> i.TenantId OR r.Outcome <> 1
                           OR i.FiledAtUtc < g.StandstillStartsAtUtc
                           OR i.FiledAtUtc > g.AppealWindowEndsAtUtc
                           OR i.IntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR i.Sequence <> (
                               SELECT ISNULL(MAX(prior.Sequence), 0) + 1
                               FROM [dbo].[ProcurementBidderAppeals] prior
                               WHERE prior.TenantId = i.TenantId
                                 AND prior.RecipientId = i.RecipientId
                                 AND prior.Id <> i.Id
                           )
                           OR (i.FiledByBusinessPartnerId IS NOT NULL
                               AND i.FiledByBusinessPartnerId <> r.BusinessPartnerId)
                           OR (i.EvidenceWorkflowDocumentId IS NOT NULL AND NOT EXISTS (
                               SELECT 1 FROM [dbo].[WorkflowEvidenceDocuments] e
                               WHERE e.Id = i.EvidenceWorkflowDocumentId
                                 AND e.TenantId = i.TenantId AND e.IsDeleted = 0
                           ))
                           OR (i.EvidenceFileUploadRecordId IS NOT NULL AND NOT EXISTS (
                               SELECT 1 FROM [dbo].[FileUploadRecords] f
                               WHERE f.Id = i.EvidenceFileUploadRecordId
                                 AND f.TenantId = i.TenantId AND f.IsDeleted = 0
                           ))
                    )
                        THROW 51513, 'Bidder appeal recipient, window, evidence, sequence, hash, or tenant lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementBidderAppealDecisions_Immutable]
                ON [dbo].[ProcurementBidderAppealDecisions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51514, 'Bidder appeal decisions are append-only.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.IsDeleted = 1
                           OR i.IntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR NOT EXISTS (
                               SELECT 1 FROM [dbo].[ProcurementBidderAppeals] a
                               WHERE a.Id = i.AppealId AND a.TenantId = i.TenantId
                           )
                           OR NOT EXISTS (
                               SELECT 1 FROM [dbo].[WorkflowInstances] w
                               WHERE w.Id = i.WorkflowInstanceId
                                 AND w.WorkflowDefinitionId = i.WorkflowDefinitionId
                                 AND w.EntityId = i.AppealId
                                 AND w.TenantId = i.TenantId
                                 AND w.Status = 2 AND w.CompletedDate IS NOT NULL
                                 AND w.IsDeleted = 0
                           )
                           OR (i.EvidenceWorkflowDocumentId IS NOT NULL AND NOT EXISTS (
                               SELECT 1 FROM [dbo].[WorkflowEvidenceDocuments] e
                               WHERE e.Id = i.EvidenceWorkflowDocumentId
                                 AND e.TenantId = i.TenantId AND e.IsDeleted = 0
                           ))
                           OR (i.EvidenceFileUploadRecordId IS NOT NULL AND NOT EXISTS (
                               SELECT 1 FROM [dbo].[FileUploadRecords] f
                               WHERE f.Id = i.EvidenceFileUploadRecordId
                                 AND f.TenantId = i.TenantId AND f.IsDeleted = 0
                           ))
                    )
                        THROW 51515, 'Bidder appeal decision workflow, evidence, hash, or tenant lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementTenderSecurityInstruments_Immutable]
                ON [dbo].[ProcurementTenderSecurityInstruments]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51516, 'Tender security instruments are append-only.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [dbo].[ProcurementBidderCommunicationRecipients] r
                          ON r.Id = i.RecipientId
                        JOIN [dbo].[ProcurementBidderCommunicationRegisters] g
                          ON g.Id = r.RegisterId
                        WHERE i.IsDeleted = 1 OR r.TenantId <> i.TenantId
                           OR g.TenantId <> i.TenantId
                           OR i.IntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR NOT EXISTS (
                               SELECT 1
                               FROM OPENJSON(r.BidOrQuoteIdsJson) j
                               WHERE TRY_CONVERT(uniqueidentifier, j.[value]) =
                                   COALESCE(i.RequestForQuotationQuoteId, i.TenderBidId)
                           )
                           OR (g.SourceType = 0 AND (
                               i.RequestForQuotationQuoteId IS NULL OR i.TenderBidId IS NOT NULL
                               OR NOT EXISTS (
                                   SELECT 1
                                   FROM [dbo].[RequestForQuotationQuotes] q
                                   WHERE q.Id = i.RequestForQuotationQuoteId
                                      AND q.RfqId = g.SourceId
                                     AND q.BusinessPartnerId = r.BusinessPartnerId
                                     AND q.TenantId = i.TenantId AND q.IsDeleted = 0
                               )))
                           OR (g.SourceType IN (1, 2) AND (
                               i.TenderBidId IS NULL OR i.RequestForQuotationQuoteId IS NOT NULL
                               OR NOT EXISTS (
                                   SELECT 1
                                   FROM [dbo].[TenderBids] b
                                   WHERE b.Id = i.TenderBidId
                                     AND b.TenderId = g.SourceId
                                     AND b.BusinessPartnerId = r.BusinessPartnerId
                                     AND b.TenantId = i.TenantId AND b.IsDeleted = 0
                               )))
                           OR (i.EvidenceWorkflowDocumentId IS NOT NULL AND NOT EXISTS (
                               SELECT 1 FROM [dbo].[WorkflowEvidenceDocuments] e
                               WHERE e.Id = i.EvidenceWorkflowDocumentId
                                 AND e.TenantId = i.TenantId AND e.IsDeleted = 0
                           ))
                           OR (i.EvidenceFileUploadRecordId IS NOT NULL AND NOT EXISTS (
                               SELECT 1 FROM [dbo].[FileUploadRecords] f
                               WHERE f.Id = i.EvidenceFileUploadRecordId
                                 AND f.TenantId = i.TenantId AND f.IsDeleted = 0
                           ))
                    )
                        THROW 51517, 'Tender security source subject, supplier, evidence, hash, or tenant lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementTenderSecurityActions_Immutable]
                ON [dbo].[ProcurementTenderSecurityActions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51518, 'Tender security actions are append-only terminal records.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [dbo].[ProcurementTenderSecurityInstruments] s
                          ON s.Id = i.SecurityInstrumentId
                        JOIN [dbo].[ProcurementBidderCommunicationRecipients] r
                          ON r.Id = s.RecipientId
                        JOIN [dbo].[ProcurementBidderCommunicationRegisters] g
                          ON g.Id = r.RegisterId
                        WHERE i.IsDeleted = 1 OR s.TenantId <> i.TenantId
                           OR r.TenantId <> i.TenantId OR g.TenantId <> i.TenantId
                           OR i.Sequence <> 1
                           OR i.ActionedAtUtc < g.StandstillEndsAtUtc
                           OR i.ActionedAtUtc < g.AppealWindowEndsAtUtc
                           OR i.IntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR (r.Outcome = 0 AND i.ActionType <> 0)
                           OR (r.Outcome = 1 AND i.ActionType = 0)
                           OR EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementBidderAppeals] a
                               WHERE a.RecipientId = r.Id AND a.TenantId = i.TenantId
                                 AND NOT EXISTS (
                                     SELECT 1
                                     FROM [dbo].[ProcurementBidderAppealDecisions] d
                                     WHERE d.AppealId = a.Id AND d.TenantId = i.TenantId
                                 )
                           )
                           OR EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementBidderAppeals] a
                               JOIN [dbo].[ProcurementBidderAppealDecisions] d
                                 ON d.AppealId = a.Id AND d.TenantId = a.TenantId
                               WHERE a.RecipientId = r.Id AND a.TenantId = i.TenantId
                                 AND d.Outcome = 0
                           )
                           OR NOT EXISTS (
                               SELECT 1 FROM [dbo].[WorkflowInstances] w
                               WHERE w.Id = i.WorkflowInstanceId
                                 AND w.WorkflowDefinitionId = i.WorkflowDefinitionId
                                 AND w.EntityId = i.SecurityInstrumentId
                                 AND w.TenantId = i.TenantId
                                 AND w.Status = 2 AND w.CompletedDate IS NOT NULL
                                 AND w.IsDeleted = 0
                           )
                           OR (i.EvidenceWorkflowDocumentId IS NOT NULL AND NOT EXISTS (
                               SELECT 1 FROM [dbo].[WorkflowEvidenceDocuments] e
                               WHERE e.Id = i.EvidenceWorkflowDocumentId
                                 AND e.TenantId = i.TenantId AND e.IsDeleted = 0
                           ))
                           OR (i.EvidenceFileUploadRecordId IS NOT NULL AND NOT EXISTS (
                               SELECT 1 FROM [dbo].[FileUploadRecords] f
                               WHERE f.Id = i.EvidenceFileUploadRecordId
                                 AND f.TenantId = i.TenantId AND f.IsDeleted = 0
                           ))
                    )
                        THROW 51519, 'Tender security action outcome, window, appeal, workflow, evidence, hash, or tenant lineage is invalid.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementTenderSecurityActions_Immutable];
                DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementTenderSecurityInstruments_Immutable];
                DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementBidderAppealDecisions_Immutable];
                DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementBidderAppeals_Immutable];
                DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementBidderCommunicationAcknowledgements_Immutable];
                DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementBidderCommunicationDeliveries_Immutable];
                DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementBidderCommunicationDispatches_Immutable];
                DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementBidderCommunicationLetterVersions_Immutable];
                DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementBidderCommunicationRecipients_Immutable];
                DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementBidderCommunicationRegisters_Immutable];
                """);

            migrationBuilder.DropTable(
                name: "ProcurementBidderAppealDecisions");

            migrationBuilder.DropTable(
                name: "ProcurementBidderCommunicationAcknowledgements");

            migrationBuilder.DropTable(
                name: "ProcurementBidderCommunicationDeliveries");

            migrationBuilder.DropTable(
                name: "ProcurementTenderSecurityActions");

            migrationBuilder.DropTable(
                name: "ProcurementBidderAppeals");

            migrationBuilder.DropTable(
                name: "ProcurementBidderCommunicationDispatches");

            migrationBuilder.DropTable(
                name: "ProcurementTenderSecurityInstruments");

            migrationBuilder.DropTable(
                name: "ProcurementBidderCommunicationLetterVersions");

            migrationBuilder.DropTable(
                name: "ProcurementBidderCommunicationRecipients");

            migrationBuilder.DropTable(
                name: "ProcurementBidderCommunicationRegisters");
        }
    }
}
