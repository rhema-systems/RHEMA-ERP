using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class TDC0401FrameworkAgreements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementFrameworkAgreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreementKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreementNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AwardReadinessDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceIntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SupplierEligibilityDecisionHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PriceListReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PriceListVersion = table.Column<int>(type: "int", nullable: false),
                    CeilingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupersedesAgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupersededByAgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TermsSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReviewComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TerminatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TerminatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TerminationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreationCorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastOperationCorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastOperation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementFrameworkAgreements", x => x.Id);
                    table.CheckConstraint("CK_ProcurementFrameworkAgreements_State", "[Version] >= 1 AND [Status] BETWEEN 0 AND 6 AND [SourceType] BETWEEN 0 AND 2 AND [PriceListVersion] >= 1 AND [CeilingAmount] > 0 AND LEN([CurrencyCode]) = 3 AND [EffectiveToUtc] > [EffectiveFromUtc] AND LEN([SourceIntegrityHash]) = 64 AND LEN([SupplierEligibilityDecisionHash]) = 64 AND LEN([IntegrityHash]) = 64 AND LEN([CreationCorrelationId]) > 0 AND LEN([LastOperationCorrelationId]) > 0 AND ISJSON([SnapshotJson]) = 1 AND (([Status] = 1 AND [SubmittedById] IS NOT NULL AND [SubmittedAtUtc] IS NOT NULL) OR [Status] <> 1) AND (([Status] = 2 AND [PublishedById] IS NOT NULL AND [PublishedAtUtc] IS NOT NULL) OR [Status] <> 2) AND (([Status] = 3 AND [RejectedById] IS NOT NULL AND [RejectedAtUtc] IS NOT NULL) OR [Status] <> 3) AND (([Status] = 4 AND [SupersededByAgreementId] IS NOT NULL) OR [Status] <> 4) AND (([Status] = 6 AND [TerminatedById] IS NOT NULL AND [TerminatedAtUtc] IS NOT NULL AND LEN([TerminationReason]) > 0) OR [Status] <> 6)");
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreements_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreements_ProcurementAwardReadinessDecisions_AwardReadinessDecisionId",
                        column: x => x.AwardReadinessDecisionId,
                        principalTable: "ProcurementAwardReadinessDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreements_ProcurementFrameworkAgreements_SupersededByAgreementId",
                        column: x => x.SupersededByAgreementId,
                        principalTable: "ProcurementFrameworkAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreements_ProcurementFrameworkAgreements_SupersedesAgreementId",
                        column: x => x.SupersedesAgreementId,
                        principalTable: "ProcurementFrameworkAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreements_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreements_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreements_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementFrameworkAgreementCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CategoryName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementFrameworkAgreementCategories", x => x.Id);
                    table.CheckConstraint("CK_ProcurementFrameworkAgreementCategories_State", "LEN([CategoryCode]) > 0 AND LEN([CategoryName]) > 0 AND LEN([IntegrityHash]) = 64");
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreementCategories_PartnerCategories_PartnerCategoryId",
                        column: x => x.PartnerCategoryId,
                        principalTable: "PartnerCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreementCategories_ProcurementFrameworkAgreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "ProcurementFrameworkAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreementCategories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementFrameworkAgreementDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CentralDocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DmsReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    RetiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ProcurementFrameworkAgreementDocuments", x => x.Id);
                    table.CheckConstraint("CK_ProcurementFrameworkAgreementDocuments_State", "LEN([DocumentType]) > 0 AND LEN([Title]) > 0 AND LEN([DmsReference]) > 0 AND LEN([IntegrityHash]) = 64 AND (([IsCurrent] = 1 AND [RetiredAtUtc] IS NULL AND [RetiredById] IS NULL) OR ([IsCurrent] = 0 AND [RetiredAtUtc] IS NOT NULL AND [RetiredById] IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreementDocuments_CentralDocumentRecords_CentralDocumentRecordId",
                        column: x => x.CentralDocumentRecordId,
                        principalTable: "CentralDocumentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreementDocuments_CentralDocumentVersions_CentralDocumentVersionId",
                        column: x => x.CentralDocumentVersionId,
                        principalTable: "CentralDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreementDocuments_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreementDocuments_ProcurementFrameworkAgreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "ProcurementFrameworkAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreementDocuments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementFrameworkAgreementExtensions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PreviousEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProposedEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementFrameworkAgreementExtensions", x => x.Id);
                    table.CheckConstraint("CK_ProcurementFrameworkAgreementExtensions_State", "[SequenceNumber] >= 1 AND [Status] BETWEEN 0 AND 2 AND [ProposedEndUtc] > [PreviousEndUtc] AND LEN([Reason]) > 0 AND LEN([CorrelationId]) > 0 AND LEN([IntegrityHash]) = 64 AND ISJSON([SnapshotJson]) = 1 AND (([Status] = 0 AND [DecidedById] IS NULL AND [DecidedAtUtc] IS NULL) OR ([Status] IN (1, 2) AND [DecidedById] IS NOT NULL AND [DecidedAtUtc] IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreementExtensions_ProcurementFrameworkAgreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "ProcurementFrameworkAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreementExtensions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreementExtensions_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkAgreementExtensions_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementFrameworkCallOffAuthorities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorityKind = table.Column<int>(type: "int", nullable: false),
                    AuthorityUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuthorityValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    MaximumCallOffAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ValidFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidToUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementFrameworkCallOffAuthorities", x => x.Id);
                    table.CheckConstraint("CK_ProcurementFrameworkCallOffAuthorities_State", "[AuthorityKind] BETWEEN 0 AND 3 AND LEN([AuthorityValue]) > 0 AND LEN([DisplayName]) > 0 AND ([MaximumCallOffAmount] IS NULL OR [MaximumCallOffAmount] > 0) AND [ValidToUtc] > [ValidFromUtc] AND LEN([IntegrityHash]) = 64 AND (([AuthorityKind] = 0 AND [AuthorityUserId] IS NOT NULL) OR ([AuthorityKind] <> 0 AND [AuthorityUserId] IS NULL))");
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkCallOffAuthorities_ProcurementFrameworkAgreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "ProcurementFrameworkAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkCallOffAuthorities_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementFrameworkPriceListLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinimumQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    MaximumQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: false),
                    Specifications = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_ProcurementFrameworkPriceListLines", x => x.Id);
                    table.CheckConstraint("CK_ProcurementFrameworkPriceListLines_State", "[UnitPrice] > 0 AND [MinimumQuantity] > 0 AND ([MaximumQuantity] IS NULL OR [MaximumQuantity] >= [MinimumQuantity]) AND [LeadTimeDays] >= 0 AND LEN([ItemCode]) > 0 AND LEN([UnitOfMeasure]) > 0 AND LEN([IntegrityHash]) = 64");
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkPriceListLines_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkPriceListLines_ProcurementFrameworkAgreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "ProcurementFrameworkAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementFrameworkPriceListLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreementCategories_AgreementId",
                table: "ProcurementFrameworkAgreementCategories",
                column: "AgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreementCategories_PartnerCategoryId",
                table: "ProcurementFrameworkAgreementCategories",
                column: "PartnerCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreementCategories_TenantId_AgreementId_PartnerCategoryId",
                table: "ProcurementFrameworkAgreementCategories",
                columns: new[] { "TenantId", "AgreementId", "PartnerCategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreementDocuments_AgreementId",
                table: "ProcurementFrameworkAgreementDocuments",
                column: "AgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreementDocuments_CentralDocumentRecordId",
                table: "ProcurementFrameworkAgreementDocuments",
                column: "CentralDocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreementDocuments_CentralDocumentVersionId",
                table: "ProcurementFrameworkAgreementDocuments",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreementDocuments_FileUploadRecordId",
                table: "ProcurementFrameworkAgreementDocuments",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreementDocuments_TenantId_AgreementId_FileUploadRecordId",
                table: "ProcurementFrameworkAgreementDocuments",
                columns: new[] { "TenantId", "AgreementId", "FileUploadRecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreementDocuments_TenantId_CentralDocumentRecordId",
                table: "ProcurementFrameworkAgreementDocuments",
                columns: new[] { "TenantId", "CentralDocumentRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreementExtensions_AgreementId",
                table: "ProcurementFrameworkAgreementExtensions",
                column: "AgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreementExtensions_TenantId_AgreementId_SequenceNumber",
                table: "ProcurementFrameworkAgreementExtensions",
                columns: new[] { "TenantId", "AgreementId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreementExtensions_TenantId_CorrelationId",
                table: "ProcurementFrameworkAgreementExtensions",
                columns: new[] { "TenantId", "CorrelationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreementExtensions_TenantId_WorkflowInstanceId",
                table: "ProcurementFrameworkAgreementExtensions",
                columns: new[] { "TenantId", "WorkflowInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreementExtensions_WorkflowDefinitionId",
                table: "ProcurementFrameworkAgreementExtensions",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreementExtensions_WorkflowInstanceId",
                table: "ProcurementFrameworkAgreementExtensions",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "UX_ProcurementFrameworkAgreementExtensions_Pending",
                table: "ProcurementFrameworkAgreementExtensions",
                columns: new[] { "TenantId", "AgreementId" },
                unique: true,
                filter: "[Status] = 0 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreements_AwardReadinessDecisionId",
                table: "ProcurementFrameworkAgreements",
                column: "AwardReadinessDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreements_BusinessPartnerId",
                table: "ProcurementFrameworkAgreements",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreements_SupersededByAgreementId",
                table: "ProcurementFrameworkAgreements",
                column: "SupersededByAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreements_SupersedesAgreementId",
                table: "ProcurementFrameworkAgreements",
                column: "SupersedesAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreements_TenantId_AgreementKey_Version",
                table: "ProcurementFrameworkAgreements",
                columns: new[] { "TenantId", "AgreementKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreements_TenantId_AgreementNumber",
                table: "ProcurementFrameworkAgreements",
                columns: new[] { "TenantId", "AgreementNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreements_TenantId_BusinessPartnerId_Status",
                table: "ProcurementFrameworkAgreements",
                columns: new[] { "TenantId", "BusinessPartnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreements_TenantId_CreationCorrelationId",
                table: "ProcurementFrameworkAgreements",
                columns: new[] { "TenantId", "CreationCorrelationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreements_TenantId_SourceType_SourceId",
                table: "ProcurementFrameworkAgreements",
                columns: new[] { "TenantId", "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreements_TenantId_Status_EffectiveFromUtc_EffectiveToUtc",
                table: "ProcurementFrameworkAgreements",
                columns: new[] { "TenantId", "Status", "EffectiveFromUtc", "EffectiveToUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreements_TenantId_WorkflowInstanceId",
                table: "ProcurementFrameworkAgreements",
                columns: new[] { "TenantId", "WorkflowInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreements_WorkflowDefinitionId",
                table: "ProcurementFrameworkAgreements",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkAgreements_WorkflowInstanceId",
                table: "ProcurementFrameworkAgreements",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "UX_ProcurementFrameworkAgreements_OpenRevision",
                table: "ProcurementFrameworkAgreements",
                columns: new[] { "TenantId", "AgreementKey" },
                unique: true,
                filter: "[Status] IN (0, 1) AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffAuthorities_AgreementId",
                table: "ProcurementFrameworkCallOffAuthorities",
                column: "AgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffAuthorities_TenantId_AgreementId_AuthorityKind_AuthorityValue",
                table: "ProcurementFrameworkCallOffAuthorities",
                columns: new[] { "TenantId", "AgreementId", "AuthorityKind", "AuthorityValue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkCallOffAuthorities_TenantId_AgreementId_IsActive",
                table: "ProcurementFrameworkCallOffAuthorities",
                columns: new[] { "TenantId", "AgreementId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkPriceListLines_AgreementId",
                table: "ProcurementFrameworkPriceListLines",
                column: "AgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkPriceListLines_InventoryItemId",
                table: "ProcurementFrameworkPriceListLines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementFrameworkPriceListLines_TenantId_AgreementId_InventoryItemId",
                table: "ProcurementFrameworkPriceListLines",
                columns: new[] { "TenantId", "AgreementId", "InventoryItemId" },
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [TR_ProcurementFrameworkAgreements_Lifecycle]
                ON [ProcurementFrameworkAgreements]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM deleted d
                        LEFT JOIN inserted i ON i.Id = d.Id
                        WHERE i.Id IS NULL)
                        THROW 51001, 'Framework agreements cannot be physically deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [BusinessPartners] bp
                            ON bp.Id = i.BusinessPartnerId AND bp.TenantId = i.TenantId
                        LEFT JOIN [ProcurementAwardReadinessDecisions] rd
                            ON rd.Id = i.AwardReadinessDecisionId
                           AND rd.TenantId = i.TenantId
                           AND rd.SourceType = i.SourceType
                           AND rd.SourceId = i.SourceId
                           AND rd.SourceReference = i.SourceReference
                           AND rd.Status = 1
                           AND rd.SourceIntegrityHash = i.SourceIntegrityHash
                           AND rd.IsDeleted = 0
                           AND EXISTS (
                               SELECT 1
                               FROM OPENJSON(rd.RecommendedBusinessPartnerIdsJson) supplier
                               WHERE TRY_CONVERT(uniqueidentifier, supplier.[value])
                                   = i.BusinessPartnerId)
                        LEFT JOIN [WorkflowDefinitions] wd
                            ON wd.Id = i.WorkflowDefinitionId AND wd.TenantId = i.TenantId
                        LEFT JOIN [WorkflowInstances] wi
                            ON wi.Id = i.WorkflowInstanceId
                           AND wi.TenantId = i.TenantId
                           AND wi.WorkflowDefinitionId = i.WorkflowDefinitionId
                           AND wi.EntityId = i.Id
                        LEFT JOIN [ProcurementFrameworkAgreements] previous
                            ON previous.Id = i.SupersedesAgreementId
                           AND previous.TenantId = i.TenantId
                        LEFT JOIN [ProcurementFrameworkAgreements] replacement
                            ON replacement.Id = i.SupersededByAgreementId
                           AND replacement.TenantId = i.TenantId
                        WHERE bp.Id IS NULL OR rd.Id IS NULL OR wd.Id IS NULL
                           OR (i.WorkflowInstanceId IS NOT NULL AND wi.Id IS NULL)
                           OR (i.Status IN (2, 4, 5, 6)
                               AND (wi.Id IS NULL OR wi.Status <> 2))
                           OR (i.Status = 3
                               AND (wi.Id IS NULL OR wi.Status NOT IN (3, 4)))
                           OR (i.SupersedesAgreementId IS NOT NULL AND previous.Id IS NULL)
                           OR (i.SupersededByAgreementId IS NOT NULL AND replacement.Id IS NULL))
                        THROW 51002, 'Framework agreement references must belong to the same tenant.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId
                           OR i.AgreementKey <> d.AgreementKey
                           OR i.Version <> d.Version
                           OR i.AgreementNumber <> d.AgreementNumber
                           OR i.CreationCorrelationId <> d.CreationCorrelationId
                           OR i.IsDeleted <> d.IsDeleted)
                        THROW 51003, 'Framework agreement family identity and deletion state are immutable.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE i.Status <> d.Status
                          AND NOT (
                              (d.Status = 0 AND i.Status = 1)
                              OR (d.Status = 1 AND i.Status IN (2, 3))
                              OR (d.Status = 2 AND i.Status IN (4, 5, 6))))
                        THROW 51004, 'Invalid framework agreement lifecycle transition.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status IN (3, 4, 5, 6))
                        THROW 51005, 'Terminal framework agreement revisions are immutable.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status <> 0
                          AND (
                              i.BusinessPartnerId <> d.BusinessPartnerId
                              OR i.SourceType <> d.SourceType
                              OR i.SourceId <> d.SourceId
                              OR i.SourceReference <> d.SourceReference
                              OR i.PriceListReference <> d.PriceListReference
                              OR i.PriceListVersion <> d.PriceListVersion
                              OR i.CeilingAmount <> d.CeilingAmount
                              OR i.CurrencyCode <> d.CurrencyCode
                              OR i.EffectiveFromUtc <> d.EffectiveFromUtc
                              OR i.EffectiveToUtc <> d.EffectiveToUtc
                              OR i.WorkflowDefinitionId <> d.WorkflowDefinitionId
                              OR i.Title <> d.Title
                              OR ISNULL(i.Description, '') <> ISNULL(d.Description, '')
                              OR ISNULL(i.TermsSummary, '') <> ISNULL(d.TermsSummary, '')))
                        THROW 51006, 'Submitted framework agreement commercial terms are immutable.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status = 2
                          AND (
                              i.AwardReadinessDecisionId <> d.AwardReadinessDecisionId
                              OR i.SourceIntegrityHash <> d.SourceIntegrityHash
                              OR i.SupplierEligibilityDecisionHash <> d.SupplierEligibilityDecisionHash
                              OR ISNULL(i.SupersedesAgreementId, '00000000-0000-0000-0000-000000000000')
                                 <> ISNULL(d.SupersedesAgreementId, '00000000-0000-0000-0000-000000000000')))
                        THROW 51007, 'Published framework agreement lineage is immutable.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [TR_ProcurementFrameworkAgreementCategories_Protected]
                ON [ProcurementFrameworkAgreementCategories]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM (
                            SELECT AgreementId, TenantId, PartnerCategoryId FROM inserted
                            UNION
                            SELECT AgreementId, TenantId, PartnerCategoryId FROM deleted
                        ) affected
                        LEFT JOIN [ProcurementFrameworkAgreements] agreement
                            ON agreement.Id = affected.AgreementId
                           AND agreement.TenantId = affected.TenantId
                        LEFT JOIN [PartnerCategories] category
                            ON category.Id = affected.PartnerCategoryId
                           AND category.TenantId = affected.TenantId
                        WHERE agreement.Id IS NULL OR agreement.Status <> 0 OR category.Id IS NULL)
                        THROW 51011, 'Framework categories can change only on a same-tenant Draft agreement.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [TR_ProcurementFrameworkPriceListLines_Protected]
                ON [ProcurementFrameworkPriceListLines]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM (
                            SELECT AgreementId, TenantId, InventoryItemId FROM inserted
                            UNION
                            SELECT AgreementId, TenantId, InventoryItemId FROM deleted
                        ) affected
                        LEFT JOIN [ProcurementFrameworkAgreements] agreement
                            ON agreement.Id = affected.AgreementId
                           AND agreement.TenantId = affected.TenantId
                        LEFT JOIN [InventoryItems] inventoryItem
                            ON inventoryItem.Id = affected.InventoryItemId
                           AND inventoryItem.TenantId = affected.TenantId
                        WHERE agreement.Id IS NULL OR agreement.Status <> 0 OR inventoryItem.Id IS NULL)
                        THROW 51012, 'Framework prices can change only on a same-tenant Draft agreement.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [TR_ProcurementFrameworkCallOffAuthorities_Protected]
                ON [ProcurementFrameworkCallOffAuthorities]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM (
                            SELECT AgreementId, TenantId FROM inserted
                            UNION
                            SELECT AgreementId, TenantId FROM deleted
                        ) affected
                        LEFT JOIN [ProcurementFrameworkAgreements] agreement
                            ON agreement.Id = affected.AgreementId
                           AND agreement.TenantId = affected.TenantId
                        WHERE agreement.Id IS NULL OR agreement.Status <> 0)
                        THROW 51013, 'Framework call-off authorities can change only on a same-tenant Draft agreement.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [UserTenants] membership
                            ON membership.UserId = i.AuthorityUserId
                           AND membership.TenantId = i.TenantId
                           AND membership.IsDeleted = 0
                           AND membership.Status = 0
                           AND (membership.ExpiresAt IS NULL OR membership.ExpiresAt > SYSUTCDATETIME())
                        WHERE i.AuthorityKind = 0 AND membership.Id IS NULL)
                        THROW 51014, 'User call-off authorities require active access to the same tenant.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [TR_ProcurementFrameworkAgreementDocuments_Protected]
                ON [ProcurementFrameworkAgreementDocuments]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM (
                            SELECT AgreementId, TenantId FROM inserted
                            UNION
                            SELECT AgreementId, TenantId FROM deleted
                        ) affected
                        LEFT JOIN [ProcurementFrameworkAgreements] agreement
                            ON agreement.Id = affected.AgreementId
                           AND agreement.TenantId = affected.TenantId
                        WHERE agreement.Id IS NULL OR agreement.Status <> 0)
                        THROW 51015, 'Framework document links can change only on a same-tenant Draft agreement.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [FileUploadRecords] upload
                            ON upload.Id = i.FileUploadRecordId AND upload.TenantId = i.TenantId
                        LEFT JOIN [CentralDocumentRecords] documentRecord
                            ON documentRecord.Id = i.CentralDocumentRecordId
                           AND documentRecord.TenantId = i.TenantId
                        LEFT JOIN [CentralDocumentVersions] documentVersion
                            ON documentVersion.Id = i.CentralDocumentVersionId
                           AND documentVersion.TenantId = i.TenantId
                           AND documentVersion.DocumentRecordId = i.CentralDocumentRecordId
                           AND documentVersion.FileUploadRecordId = i.FileUploadRecordId
                        WHERE upload.Id IS NULL OR upload.VirusScanStatus <> 2
                           OR documentRecord.Id IS NULL OR documentVersion.Id IS NULL)
                        THROW 51016, 'Framework documents require clean, same-tenant central-DMS lineage.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [TR_ProcurementFrameworkAgreementExtensions_Lifecycle]
                ON [ProcurementFrameworkAgreementExtensions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM deleted d
                        LEFT JOIN inserted i ON i.Id = d.Id
                        WHERE i.Id IS NULL)
                        THROW 51021, 'Framework agreement extensions cannot be deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted prior ON prior.Id = i.Id
                        LEFT JOIN [ProcurementFrameworkAgreements] agreement
                            ON agreement.Id = i.AgreementId
                           AND agreement.TenantId = i.TenantId
                        LEFT JOIN [WorkflowDefinitions] workflowDefinition
                            ON workflowDefinition.Id = i.WorkflowDefinitionId
                           AND workflowDefinition.TenantId = i.TenantId
                        LEFT JOIN [WorkflowInstances] workflowInstance
                            ON workflowInstance.Id = i.WorkflowInstanceId
                           AND workflowInstance.TenantId = i.TenantId
                           AND workflowInstance.WorkflowDefinitionId
                               = i.WorkflowDefinitionId
                           AND workflowInstance.EntityId = i.Id
                        WHERE agreement.Id IS NULL
                           OR ((prior.Id IS NULL OR i.Status = 1)
                               AND agreement.Status <> 2)
                           OR workflowDefinition.Id IS NULL OR i.IsDeleted <> 0
                           OR (i.WorkflowInstanceId IS NOT NULL
                               AND workflowInstance.Id IS NULL)
                           OR (i.Status = 1
                               AND (workflowInstance.Id IS NULL
                                   OR workflowInstance.Status <> 2))
                           OR (i.Status = 2
                               AND (workflowInstance.Id IS NULL
                                   OR workflowInstance.Status NOT IN (3, 4))))
                        THROW 51022, 'Framework extensions require a same-tenant Published agreement and workflow.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId
                           OR i.AgreementId <> d.AgreementId
                           OR i.SequenceNumber <> d.SequenceNumber
                           OR i.PreviousEndUtc <> d.PreviousEndUtc
                           OR i.ProposedEndUtc <> d.ProposedEndUtc
                           OR i.Reason <> d.Reason
                           OR i.WorkflowDefinitionId <> d.WorkflowDefinitionId
                           OR i.SubmittedById <> d.SubmittedById
                           OR i.SubmittedByName <> d.SubmittedByName
                           OR i.SubmittedAtUtc <> d.SubmittedAtUtc
                           OR i.CorrelationId <> d.CorrelationId
                           OR i.IsDeleted <> d.IsDeleted)
                        THROW 51023, 'Submitted framework extension terms are immutable.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE i.Status <> d.Status
                          AND NOT (d.Status = 0 AND i.Status IN (1, 2)))
                        THROW 51024, 'Invalid framework extension lifecycle transition.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status IN (1, 2))
                        THROW 51025, 'Decided framework extensions are immutable.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcurementFrameworkAgreementCategories");

            migrationBuilder.DropTable(
                name: "ProcurementFrameworkAgreementDocuments");

            migrationBuilder.DropTable(
                name: "ProcurementFrameworkAgreementExtensions");

            migrationBuilder.DropTable(
                name: "ProcurementFrameworkCallOffAuthorities");

            migrationBuilder.DropTable(
                name: "ProcurementFrameworkPriceListLines");

            migrationBuilder.DropTable(
                name: "ProcurementFrameworkAgreements");
        }
    }
}
