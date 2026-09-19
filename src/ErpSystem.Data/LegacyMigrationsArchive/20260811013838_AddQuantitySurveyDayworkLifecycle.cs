using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantitySurveyDayworkLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuantitySurveyDayworkSheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariationOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractorBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    LastMutationClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastMutationRequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    SheetNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorkDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WorkLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariationDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceMetadataTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ContractorSignedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractorSignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContractorSignatureHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    VerifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifierSignatureHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    VerificationNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_QuantitySurveyDayworkSheets", x => x.Id);
                    table.CheckConstraint("CK_QsDayworkSheets_Amounts", "[TotalAmount] >= 0");
                    table.CheckConstraint("CK_QsDayworkSheets_Hashes", "LEN([RequestHash]) = 64 AND LEN([PolicyHash]) = 64 AND ([LastMutationRequestHash] IS NULL OR LEN([LastMutationRequestHash]) = 64)");
                    table.CheckConstraint("CK_QsDayworkSheets_Lifecycle", "([Status] = 0 AND [ContractorSignedById] IS NULL AND [ContractorSignedAt] IS NULL AND [ContractorSignatureHash] IS NULL AND [VerifiedById] IS NULL AND [VerifiedAt] IS NULL AND [VerifierSignatureHash] IS NULL AND [RejectionReason] IS NULL) OR ([Status] = 1 AND [ContractorSignedById] IS NOT NULL AND [ContractorSignedAt] IS NOT NULL AND LEN([ContractorSignatureHash]) = 64 AND [VerifiedById] IS NULL AND [VerifiedAt] IS NULL AND [VerifierSignatureHash] IS NULL AND [RejectionReason] IS NULL) OR ([Status] = 2 AND [ContractorSignedById] IS NOT NULL AND [ContractorSignedAt] IS NOT NULL AND LEN([ContractorSignatureHash]) = 64 AND [VerifiedById] IS NOT NULL AND [VerifiedAt] IS NOT NULL AND LEN([VerifierSignatureHash]) = 64 AND [RejectionReason] IS NULL) OR ([Status] = 3 AND [ContractorSignedById] IS NOT NULL AND [ContractorSignedAt] IS NOT NULL AND LEN([ContractorSignatureHash]) = 64 AND [VerifiedById] IS NOT NULL AND [VerifiedAt] IS NOT NULL AND LEN([VerifierSignatureHash]) = 64 AND LEN(LTRIM(RTRIM([RejectionReason]))) >= 5)");
                    table.CheckConstraint("CK_QsDayworkSheets_Status", "[Status] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkSheets_BusinessPartners_ContractorBusinessPartnerId",
                        column: x => x.ContractorBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkSheets_CentralDocumentMetadataTemplates_EvidenceMetadataTemplateId",
                        column: x => x.EvidenceMetadataTemplateId,
                        principalTable: "CentralDocumentMetadataTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkSheets_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkSheets_ProjectVariationOrders_VariationOrderId",
                        column: x => x.VariationOrderId,
                        principalTable: "ProjectVariationOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkSheets_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkSheets_QuantitySurveyConfigurationDecisions_VariationDecisionId",
                        column: x => x.VariationDecisionId,
                        principalTable: "QuantitySurveyConfigurationDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkSheets_QuantitySurveyConfigurationProfiles_ConfigurationProfileId",
                        column: x => x.ConfigurationProfileId,
                        principalTable: "QuantitySurveyConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkSheets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkSheets_Users_ContractorSignedById",
                        column: x => x.ContractorSignedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkSheets_Users_VerifiedById",
                        column: x => x.VerifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuantitySurveyDayworkEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayworkSheetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyDayworkEvidence", x => x.Id);
                    table.CheckConstraint("CK_QsDayworkEvidence_File", "[FileSize] > 0 AND LEN([ChecksumSha256]) = 64 AND LEN([RequestHash]) = 64");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkEvidence_CentralDocumentRecords_CentralDocumentRecordId",
                        column: x => x.CentralDocumentRecordId,
                        principalTable: "CentralDocumentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkEvidence_CentralDocumentVersions_CentralDocumentVersionId",
                        column: x => x.CentralDocumentVersionId,
                        principalTable: "CentralDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkEvidence_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkEvidence_QuantitySurveyDayworkSheets_DayworkSheetId",
                        column: x => x.DayworkSheetId,
                        principalTable: "QuantitySurveyDayworkSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkEvidence_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuantitySurveyDayworkLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayworkSheetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    LineType = table.Column<int>(type: "int", nullable: false),
                    RateLibraryRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RateLibraryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemCodeSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ItemNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SourceHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyDayworkLines", x => x.Id);
                    table.CheckConstraint("CK_QsDayworkLines_Amounts", "[Quantity] > 0 AND [UnitRate] >= 0 AND [Amount] = ROUND([Quantity] * [UnitRate], 2) AND LEN([SourceHash]) = 64");
                    table.CheckConstraint("CK_QsDayworkLines_Type", "[LineType] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkLines_QuantitySurveyDayworkSheets_DayworkSheetId",
                        column: x => x.DayworkSheetId,
                        principalTable: "QuantitySurveyDayworkSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkLines_QuantitySurveyRateLibraryRates_RateLibraryRateId",
                        column: x => x.RateLibraryRateId,
                        principalTable: "QuantitySurveyRateLibraryRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkLines_UnitsOfMeasure_UnitOfMeasureId",
                        column: x => x.UnitOfMeasureId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuantitySurveyDayworkRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayworkSheetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyDayworkRevisions", x => x.Id);
                    table.CheckConstraint("CK_QsDayworkRevisions_Request", "[ClientRequestId] <> '00000000-0000-0000-0000-000000000000' AND LEN([RequestHash]) = 64");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkRevisions_BusinessPartners_ActorBusinessPartnerId",
                        column: x => x.ActorBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkRevisions_QuantitySurveyDayworkSheets_DayworkSheetId",
                        column: x => x.DayworkSheetId,
                        principalTable: "QuantitySurveyDayworkSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkRevisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDayworkRevisions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkEvidence_CentralDocumentRecordId",
                table: "QuantitySurveyDayworkEvidence",
                column: "CentralDocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkEvidence_CentralDocumentVersionId",
                table: "QuantitySurveyDayworkEvidence",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkEvidence_DayworkSheetId",
                table: "QuantitySurveyDayworkEvidence",
                column: "DayworkSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkEvidence_FileUploadRecordId",
                table: "QuantitySurveyDayworkEvidence",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkEvidence_TenantId_CentralDocumentVersionId",
                table: "QuantitySurveyDayworkEvidence",
                columns: new[] { "TenantId", "CentralDocumentVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkEvidence_TenantId_ClientRequestId",
                table: "QuantitySurveyDayworkEvidence",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkLines_DayworkSheetId",
                table: "QuantitySurveyDayworkLines",
                column: "DayworkSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkLines_RateLibraryRateId",
                table: "QuantitySurveyDayworkLines",
                column: "RateLibraryRateId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkLines_TenantId_DayworkSheetId_Sequence",
                table: "QuantitySurveyDayworkLines",
                columns: new[] { "TenantId", "DayworkSheetId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkLines_UnitOfMeasureId",
                table: "QuantitySurveyDayworkLines",
                column: "UnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkRevisions_ActorBusinessPartnerId",
                table: "QuantitySurveyDayworkRevisions",
                column: "ActorBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkRevisions_ActorUserId",
                table: "QuantitySurveyDayworkRevisions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkRevisions_DayworkSheetId",
                table: "QuantitySurveyDayworkRevisions",
                column: "DayworkSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkRevisions_TenantId_ClientRequestId",
                table: "QuantitySurveyDayworkRevisions",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkRevisions_TenantId_DayworkSheetId_CreatedAt",
                table: "QuantitySurveyDayworkRevisions",
                columns: new[] { "TenantId", "DayworkSheetId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkSheets_ConfigurationProfileId",
                table: "QuantitySurveyDayworkSheets",
                column: "ConfigurationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkSheets_ContractId",
                table: "QuantitySurveyDayworkSheets",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkSheets_ContractorBusinessPartnerId",
                table: "QuantitySurveyDayworkSheets",
                column: "ContractorBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkSheets_ContractorSignedById",
                table: "QuantitySurveyDayworkSheets",
                column: "ContractorSignedById");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkSheets_EvidenceMetadataTemplateId",
                table: "QuantitySurveyDayworkSheets",
                column: "EvidenceMetadataTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkSheets_ProjectId",
                table: "QuantitySurveyDayworkSheets",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkSheets_TenantId_ClientRequestId",
                table: "QuantitySurveyDayworkSheets",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkSheets_TenantId_LastMutationClientRequestId",
                table: "QuantitySurveyDayworkSheets",
                columns: new[] { "TenantId", "LastMutationClientRequestId" },
                unique: true,
                filter: "[LastMutationClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkSheets_TenantId_SheetNumber",
                table: "QuantitySurveyDayworkSheets",
                columns: new[] { "TenantId", "SheetNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkSheets_TenantId_VariationOrderId_WorkDate",
                table: "QuantitySurveyDayworkSheets",
                columns: new[] { "TenantId", "VariationOrderId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkSheets_VariationDecisionId",
                table: "QuantitySurveyDayworkSheets",
                column: "VariationDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkSheets_VariationOrderId",
                table: "QuantitySurveyDayworkSheets",
                column: "VariationOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDayworkSheets_VerifiedById",
                table: "QuantitySurveyDayworkSheets",
                column: "VerifiedById");

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_QS0510_DayworkSheets_Governance]
                ON [dbo].[QuantitySurveyDayworkSheets]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 52000, 'QS_DAYWORK_DELETE_BLOCKED: governed daywork sheets are retained.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN dbo.Projects p ON p.Id = i.ProjectId AND p.TenantId = i.TenantId AND p.IsDeleted = 0
                        LEFT JOIN dbo.ProjectVariationOrders v ON v.Id = i.VariationOrderId AND v.TenantId = i.TenantId
                            AND v.ProjectId = i.ProjectId AND v.ContractId = i.ContractId
                            AND v.ContractorBusinessPartnerId = i.ContractorBusinessPartnerId
                            AND v.IsQuantitySurveyGoverned = 1 AND v.VariationType IN ('Daywork','AdditionalWork')
                            AND v.Status IN ('Draft','Rejected') AND v.IsDeleted = 0
                        LEFT JOIN dbo.Contracts c ON c.Id = i.ContractId AND c.TenantId = i.TenantId AND c.BusinessPartnerId = i.ContractorBusinessPartnerId
                            AND c.ContractType = 'Works' AND c.Status = 'Active' AND c.IsDeleted = 0
                        LEFT JOIN dbo.Tenders t ON t.Id = c.TenderId AND t.TenantId = i.TenantId AND t.IsDeleted = 0
                        LEFT JOIN dbo.PurchaseRequisitions pr ON pr.Id = t.SourcePurchaseRequisitionId AND pr.TenantId = i.TenantId
                            AND pr.ProjectId = i.ProjectId AND pr.IsDeleted = 0
                        LEFT JOIN dbo.BusinessPartners bp ON bp.Id = i.ContractorBusinessPartnerId AND bp.TenantId = i.TenantId AND bp.IsDeleted = 0
                        LEFT JOIN dbo.QuantitySurveyConfigurationProfiles cp ON cp.Id = i.ConfigurationProfileId AND cp.Id = v.ConfigurationProfileId
                            AND cp.TenantId = i.TenantId AND cp.LifecycleStatus = 1 AND cp.PublishedAt IS NOT NULL AND cp.IsDeleted = 0
                        LEFT JOIN dbo.QuantitySurveyConfigurationDecisions cd ON cd.Id = i.VariationDecisionId AND cd.Id = v.VariationDecisionId
                            AND cd.TenantId = i.TenantId AND cd.ProfileId = cp.Id AND cd.DecisionKey = 'QS-DEC-011'
                            AND cd.Status = 2 AND cd.ApprovalStatus = 1 AND cd.EvidenceStatus = 2 AND cd.IsDeleted = 0
                        LEFT JOIN dbo.CentralDocumentMetadataTemplates mt ON mt.Id = i.EvidenceMetadataTemplateId
                            AND mt.Id = v.EvidenceMetadataTemplateId AND mt.TenantId = i.TenantId
                            AND mt.IsActive = 1 AND mt.PublishedAt IS NOT NULL AND mt.IsDeleted = 0
                        LEFT JOIN dbo.Users signer ON signer.Id = i.ContractorSignedById AND signer.TenantId = i.TenantId AND signer.IsActive = 1
                        LEFT JOIN dbo.Users verifier ON verifier.Id = i.VerifiedById AND verifier.TenantId = i.TenantId AND verifier.IsActive = 1
                        WHERE p.Id IS NULL OR v.Id IS NULL OR c.Id IS NULL OR t.Id IS NULL OR pr.Id IS NULL OR bp.Id IS NULL
                           OR cp.Id IS NULL OR cd.Id IS NULL OR mt.Id IS NULL OR i.Currency <> v.Currency
                           OR v.PolicyHash IS NULL OR i.PolicyHash <> v.PolicyHash
                           OR NOT EXISTS (SELECT 1 FROM OPENJSON(cd.ValueJson, '$.allowedTypes') allowed
                               WHERE allowed.[value] = CASE v.VariationType WHEN 'Daywork' THEN 'Daywork' ELSE 'Additional Work' END)
                           OR (i.Status IN (1,2,3) AND (signer.Id IS NULL OR NOT EXISTS (
                               SELECT 1 FROM dbo.BusinessPartnerUsers bpu WHERE bpu.TenantId = i.TenantId
                                 AND bpu.UserId = i.ContractorSignedById AND bpu.BusinessPartnerId = i.ContractorBusinessPartnerId
                                 AND bpu.IsActive = 1 AND bpu.IsDeleted = 0)))
                           OR (i.Status IN (2,3) AND (verifier.Id IS NULL OR i.VerifiedById = i.ContractorSignedById))
                           OR (i.Status IN (1,2,3) AND (i.TotalAmount <= 0 OR i.TotalAmount <> ISNULL((
                               SELECT ROUND(SUM(line.Amount), 2) FROM dbo.QuantitySurveyDayworkLines line
                               WHERE line.TenantId = i.TenantId AND line.DayworkSheetId = i.Id AND line.IsDeleted = 0), 0)
                               OR NOT EXISTS (SELECT 1 FROM dbo.QuantitySurveyDayworkEvidence evidence
                                   WHERE evidence.TenantId = i.TenantId AND evidence.DayworkSheetId = i.Id AND evidence.IsDeleted = 0))))
                        THROW 52001, 'QS_DAYWORK_LINEAGE_INVALID: exact Works-contract, parent variation, policy, actor, rate total and evidence lineage are required.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id = i.Id
                        WHERE (d.Id IS NULL AND i.Status <> 0)
                           OR (d.Id IS NOT NULL AND (
                              i.TenantId <> d.TenantId OR i.ProjectId <> d.ProjectId OR i.VariationOrderId <> d.VariationOrderId
                              OR i.ContractId <> d.ContractId OR i.ContractorBusinessPartnerId <> d.ContractorBusinessPartnerId
                              OR i.ClientRequestId <> d.ClientRequestId OR i.RequestHash <> d.RequestHash
                              OR i.SheetNumber <> d.SheetNumber OR i.IsDeleted <> d.IsDeleted
                              OR NOT ((d.Status IN (0,3) AND i.Status IN (0,1)) OR (d.Status = 1 AND i.Status IN (2,3)))
                              OR (d.Status NOT IN (0,3) AND (i.WorkDate <> d.WorkDate OR i.WorkLocation <> d.WorkLocation
                                  OR i.Description <> d.Description OR i.Currency <> d.Currency OR i.TotalAmount <> d.TotalAmount
                                  OR i.ConfigurationProfileId <> d.ConfigurationProfileId OR i.VariationDecisionId <> d.VariationDecisionId
                                  OR i.EvidenceMetadataTemplateId <> d.EvidenceMetadataTemplateId OR i.PolicyHash <> d.PolicyHash)))))
                        THROW 52002, 'QS_DAYWORK_TRANSITION_INVALID: identity, frozen valuation or signature transition is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_QS0510_DayworkLines_Governance]
                ON [dbo].[QuantitySurveyDayworkLines]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id)
                        THROW 52003, 'QS_DAYWORK_LINE_UPDATE_BLOCKED: amend Draft detail by replacing its controlled lines.', 1;
                    IF EXISTS (
                        SELECT 1 FROM deleted d
                        LEFT JOIN dbo.QuantitySurveyDayworkSheets s ON s.Id = d.DayworkSheetId AND s.TenantId = d.TenantId
                            AND s.Status IN (0,3) AND s.IsDeleted = 0
                        WHERE s.Id IS NULL)
                        THROW 52003, 'QS_DAYWORK_LINE_DELETE_BLOCKED: signed or verified detail is immutable.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN dbo.QuantitySurveyDayworkSheets s ON s.Id = i.DayworkSheetId AND s.TenantId = i.TenantId
                            AND s.Status IN (0,3) AND s.IsDeleted = 0
                        LEFT JOIN dbo.QuantitySurveyRateLibraryRates r ON r.Id = i.RateLibraryRateId AND r.TenantId = i.TenantId
                            AND r.RateLibraryItemId = i.RateLibraryItemId AND r.LifecycleStatus = 1 AND r.PublishedAt IS NOT NULL
                            AND r.EffectiveFrom <= s.WorkDate AND (r.EffectiveTo IS NULL OR r.EffectiveTo >= s.WorkDate)
                            AND r.CurrencyCodeSnapshot = s.Currency AND r.IsDeleted = 0
                        LEFT JOIN dbo.QuantitySurveyRateLibraryItems item ON item.Id = i.RateLibraryItemId AND item.TenantId = i.TenantId
                            AND item.IsActive = 1 AND item.IsDeleted = 0
                        LEFT JOIN dbo.UnitsOfMeasure uom ON uom.Id = i.UnitOfMeasureId AND uom.Id = item.UnitOfMeasureId
                            AND uom.TenantId = i.TenantId AND uom.IsDeleted = 0
                        WHERE s.Id IS NULL OR r.Id IS NULL OR item.Id IS NULL OR uom.Id IS NULL
                           OR i.ItemCodeSnapshot <> item.Code OR i.ItemNameSnapshot <> item.Name OR i.UnitOfMeasureSnapshot <> uom.Code
                           OR i.UnitRate <> r.UnitRate OR i.Amount <> ROUND(i.Quantity * i.UnitRate, 2) OR LEN(i.SourceHash) <> 64
                           OR NOT ((item.Category = 2 AND i.LineType = 0) OR (item.Category = 1 AND i.LineType = 1)
                               OR (item.Category IN (3,4) AND i.LineType = 2)))
                        THROW 52004, 'QS_DAYWORK_RATE_INVALID: Published effective labour, material or plant rate and UOM snapshots are required.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_QS0510_DayworkEvidence_AppendOnly]
                ON [dbo].[QuantitySurveyDayworkEvidence]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 52005, 'QS_DAYWORK_EVIDENCE_IMMUTABLE: daywork evidence is append-only.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN dbo.QuantitySurveyDayworkSheets s ON s.Id = i.DayworkSheetId AND s.TenantId = i.TenantId
                            AND s.Status IN (0,3) AND s.IsDeleted = 0
                        LEFT JOIN dbo.FileUploadRecords f ON f.Id = i.FileUploadRecordId AND f.TenantId = i.TenantId
                            AND f.VirusScanStatus = 2 AND f.IsDeleted = 0
                        LEFT JOIN dbo.CentralDocumentRecords r ON r.Id = i.CentralDocumentRecordId AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                        LEFT JOIN dbo.CentralDocumentVersions v ON v.Id = i.CentralDocumentVersionId AND v.TenantId = i.TenantId
                            AND v.DocumentRecordId = r.Id AND v.FileUploadRecordId = f.Id AND v.IsDeleted = 0
                        WHERE s.Id IS NULL OR f.Id IS NULL OR r.Id IS NULL OR v.Id IS NULL
                           OR i.ClientRequestId = '00000000-0000-0000-0000-000000000000'
                           OR LEN(i.RequestHash) <> 64 OR LEN(i.ChecksumSha256) <> 64 OR i.FileSize <= 0)
                        THROW 52006, 'QS_DAYWORK_EVIDENCE_INVALID: clean central-DMS evidence and tenant lineage are required.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_QS0510_DayworkRevisions_AppendOnly]
                ON [dbo].[QuantitySurveyDayworkRevisions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 52007, 'QS_DAYWORK_REVISION_IMMUTABLE: daywork revisions are append-only.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN dbo.QuantitySurveyDayworkSheets s ON s.Id = i.DayworkSheetId AND s.TenantId = i.TenantId AND s.IsDeleted = 0
                        LEFT JOIN dbo.Users u ON u.Id = i.ActorUserId AND u.TenantId = i.TenantId
                        LEFT JOIN dbo.BusinessPartners bp ON bp.Id = i.ActorBusinessPartnerId AND bp.TenantId = i.TenantId AND bp.IsDeleted = 0
                        WHERE s.Id IS NULL OR u.Id IS NULL OR (i.ActorBusinessPartnerId IS NOT NULL AND bp.Id IS NULL)
                           OR i.ClientRequestId = '00000000-0000-0000-0000-000000000000' OR LEN(i.RequestHash) <> 64
                           OR NULLIF(LTRIM(RTRIM(i.Action)), '') IS NULL OR NULLIF(LTRIM(RTRIM(i.CorrelationId)), '') IS NULL)
                        THROW 52008, 'QS_DAYWORK_REVISION_INVALID: tenant, actor, retry and correlation lineage are required.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_QS0510_VariationDayworkEligibility]
                ON [dbo].[ProjectVariationOrders]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE i.IsQuantitySurveyGoverned = 1 AND i.VariationType IN ('Daywork','AdditionalWork')
                          AND i.Status IN ('PendingApproval','Approved')
                          AND (NOT EXISTS (SELECT 1 FROM dbo.QuantitySurveyDayworkSheets s
                                WHERE s.TenantId = i.TenantId AND s.VariationOrderId = i.Id AND s.IsDeleted = 0)
                               OR EXISTS (SELECT 1 FROM dbo.QuantitySurveyDayworkSheets s
                                WHERE s.TenantId = i.TenantId AND s.VariationOrderId = i.Id AND s.IsDeleted = 0 AND s.Status <> 2)
                               OR ISNULL((SELECT ROUND(SUM(s.TotalAmount), 2) FROM dbo.QuantitySurveyDayworkSheets s
                                WHERE s.TenantId = i.TenantId AND s.VariationOrderId = i.Id AND s.IsDeleted = 0), 0)
                                  <> ROUND(ISNULL(i.EstimatedAmount, 0), 2)))
                        THROW 52009, 'QS_DAYWORK_VARIATION_NOT_READY: Verified detail must reconcile exactly before variation approval.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0510_VariationDayworkEligibility];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0510_DayworkRevisions_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0510_DayworkEvidence_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0510_DayworkLines_Governance];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0510_DayworkSheets_Governance];");
            migrationBuilder.DropTable(
                name: "QuantitySurveyDayworkEvidence");

            migrationBuilder.DropTable(
                name: "QuantitySurveyDayworkLines");

            migrationBuilder.DropTable(
                name: "QuantitySurveyDayworkRevisions");

            migrationBuilder.DropTable(
                name: "QuantitySurveyDayworkSheets");
        }
    }
}
