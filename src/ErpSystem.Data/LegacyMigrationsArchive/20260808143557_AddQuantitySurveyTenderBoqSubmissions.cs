using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantitySurveyTenderBoqSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuantitySurveyTenderBoqSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderBoqVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    VettingStatus = table.Column<int>(type: "int", nullable: false),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CentralDocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    NormalizedPayloadHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    PreviewTokenHash = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    TenderBoqSnapshotHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    WarningCount = table.Column<int>(type: "int", nullable: false),
                    CommittedLineCount = table.Column<int>(type: "int", nullable: false),
                    TenderBoqTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SubmittedTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NormalizedPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IssuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuditAction = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorRoles = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReconciliationDeclaration = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SignatoryName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CommittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CommittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VettedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VettedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VettingNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_QuantitySurveyTenderBoqSubmissions", x => x.Id);
                    table.CheckConstraint("CK_QsTenderBoqSubmissions_Channel", "[Channel] IN (0, 1, 2, 3)");
                    table.CheckConstraint("CK_QsTenderBoqSubmissions_Counts", "[LineCount] >= 0 AND [ErrorCount] >= 0 AND [WarningCount] >= 0 AND [CommittedLineCount] >= 0");
                    table.CheckConstraint("CK_QsTenderBoqSubmissions_Status", "[Status] IN (0, 1, 2, 3, 4)");
                    table.CheckConstraint("CK_QsTenderBoqSubmissions_Totals", "[TenderBoqTotal] >= 0 AND [SubmittedTotal] >= 0");
                    table.CheckConstraint("CK_QsTenderBoqSubmissions_VettingStatus", "[VettingStatus] IN (0, 1, 2)");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyTenderBoqSubmissions_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyTenderBoqSubmissions_CentralDocumentRecords_CentralDocumentRecordId",
                        column: x => x.CentralDocumentRecordId,
                        principalTable: "CentralDocumentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyTenderBoqSubmissions_CentralDocumentVersions_CentralDocumentVersionId",
                        column: x => x.CentralDocumentVersionId,
                        principalTable: "CentralDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyTenderBoqSubmissions_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyTenderBoqSubmissions_ProjectBoqVersions_TenderBoqVersionId",
                        column: x => x.TenderBoqVersionId,
                        principalTable: "ProjectBoqVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyTenderBoqSubmissions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyTenderBoqSubmissions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyTenderBoqSubmissions_TenderBids_TenderBidId",
                        column: x => x.TenderBidId,
                        principalTable: "TenderBids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyTenderBoqSubmissions_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuantitySurveyTenderBoqSubmissionLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectBoqVersionLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    LineNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ItemCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TenderQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OfferedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SubmittedLineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CalculatedLineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ArithmeticDifference = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ComparisonStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FindingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyTenderBoqSubmissionLines", x => x.Id);
                    table.CheckConstraint("CK_QsTenderBoqSubmissionLines_Amounts", "[TenderQuantity] >= 0 AND [OfferedQuantity] >= 0 AND [UnitPrice] >= 0 AND [SubmittedLineTotal] >= 0 AND [CalculatedLineTotal] >= 0");
                    table.CheckConstraint("CK_QsTenderBoqSubmissionLines_RowNumber", "[RowNumber] > 0");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyTenderBoqSubmissionLines_ProjectBoqVersionLines_ProjectBoqVersionLineId",
                        column: x => x.ProjectBoqVersionLineId,
                        principalTable: "ProjectBoqVersionLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyTenderBoqSubmissionLines_QuantitySurveyTenderBoqSubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "QuantitySurveyTenderBoqSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyTenderBoqSubmissionLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyTenderBoqSubmissionLines_TenderBids_TenderBidId",
                        column: x => x.TenderBidId,
                        principalTable: "TenderBids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyTenderBoqSubmissionLines_TenderItems_TenderItemId",
                        column: x => x.TenderItemId,
                        principalTable: "TenderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyTenderBoqSubmissionLines_ProjectBoqVersionLineId",
                table: "QuantitySurveyTenderBoqSubmissionLines",
                column: "ProjectBoqVersionLineId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyTenderBoqSubmissionLines_SubmissionId",
                table: "QuantitySurveyTenderBoqSubmissionLines",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyTenderBoqSubmissionLines_TenantId_SubmissionId_LineKey",
                table: "QuantitySurveyTenderBoqSubmissionLines",
                columns: new[] { "TenantId", "SubmissionId", "LineKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyTenderBoqSubmissionLines_TenantId_SubmissionId_TenderItemId",
                table: "QuantitySurveyTenderBoqSubmissionLines",
                columns: new[] { "TenantId", "SubmissionId", "TenderItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyTenderBoqSubmissionLines_TenantId_TenderBidId",
                table: "QuantitySurveyTenderBoqSubmissionLines",
                columns: new[] { "TenantId", "TenderBidId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyTenderBoqSubmissionLines_TenderBidId",
                table: "QuantitySurveyTenderBoqSubmissionLines",
                column: "TenderBidId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyTenderBoqSubmissionLines_TenderItemId",
                table: "QuantitySurveyTenderBoqSubmissionLines",
                column: "TenderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyTenderBoqSubmissions_BusinessPartnerId",
                table: "QuantitySurveyTenderBoqSubmissions",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyTenderBoqSubmissions_CentralDocumentRecordId",
                table: "QuantitySurveyTenderBoqSubmissions",
                column: "CentralDocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyTenderBoqSubmissions_CentralDocumentVersionId",
                table: "QuantitySurveyTenderBoqSubmissions",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyTenderBoqSubmissions_FileUploadRecordId",
                table: "QuantitySurveyTenderBoqSubmissions",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyTenderBoqSubmissions_ProjectId",
                table: "QuantitySurveyTenderBoqSubmissions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyTenderBoqSubmissions_TenantId_IdempotencyKey",
                table: "QuantitySurveyTenderBoqSubmissions",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyTenderBoqSubmissions_TenantId_TenderBidId_CreatedAt",
                table: "QuantitySurveyTenderBoqSubmissions",
                columns: new[] { "TenantId", "TenderBidId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyTenderBoqSubmissions_TenantId_TenderBidId_Status",
                table: "QuantitySurveyTenderBoqSubmissions",
                columns: new[] { "TenantId", "TenderBidId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyTenderBoqSubmissions_TenantId_TenderBoqVersionId",
                table: "QuantitySurveyTenderBoqSubmissions",
                columns: new[] { "TenantId", "TenderBoqVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyTenderBoqSubmissions_TenderBidId",
                table: "QuantitySurveyTenderBoqSubmissions",
                column: "TenderBidId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyTenderBoqSubmissions_TenderBoqVersionId",
                table: "QuantitySurveyTenderBoqSubmissions",
                column: "TenderBoqVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyTenderBoqSubmissions_TenderId",
                table: "QuantitySurveyTenderBoqSubmissions",
                column: "TenderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuantitySurveyTenderBoqSubmissionLines");

            migrationBuilder.DropTable(
                name: "QuantitySurveyTenderBoqSubmissions");

        }
    }
}
