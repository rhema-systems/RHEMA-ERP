using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260808035054_AddQuantitySurveyBoqImportStaging")]
    public partial class AddQuantitySurveyBoqImportStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectBoqItems_TenantId",
                table: "ProjectBoqItems");

            migrationBuilder.CreateTable(
                name: "QuantitySurveyBoqImportSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CentralDocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreviewTokenHash = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    FileHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    NormalizedPayloadHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    TemplateVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    CommittedLineCount = table.Column<int>(type: "int", nullable: false),
                    NormalizedPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IssuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReconciledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReconciledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReconciliationDeclaration = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CommittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_QuantitySurveyBoqImportSessions", x => x.Id);
                    table.CheckConstraint("CK_QsBoqImportSessions_Counts", "[LineCount] >= 0 AND [ErrorCount] >= 0 AND [CommittedLineCount] >= 0");
                    table.CheckConstraint("CK_QsBoqImportSessions_Status", "[Status] IN (0, 1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyBoqImportSessions_CentralDocumentRecords_CentralDocumentRecordId",
                        column: x => x.CentralDocumentRecordId,
                        principalTable: "CentralDocumentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyBoqImportSessions_CentralDocumentVersions_CentralDocumentVersionId",
                        column: x => x.CentralDocumentVersionId,
                        principalTable: "CentralDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyBoqImportSessions_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyBoqImportSessions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyBoqImportSessions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoqItems_CostCodeCatalogEntryId",
                table: "ProjectBoqItems",
                column: "CostCodeCatalogEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoqItems_MeasurementCodeCatalogEntryId",
                table: "ProjectBoqItems",
                column: "MeasurementCodeCatalogEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoqItems_SectionCatalogEntryId",
                table: "ProjectBoqItems",
                column: "SectionCatalogEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoqItems_TradeCatalogEntryId",
                table: "ProjectBoqItems",
                column: "TradeCatalogEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyBoqImportSessions_CentralDocumentRecordId",
                table: "QuantitySurveyBoqImportSessions",
                column: "CentralDocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyBoqImportSessions_CentralDocumentVersionId",
                table: "QuantitySurveyBoqImportSessions",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyBoqImportSessions_FileUploadRecordId",
                table: "QuantitySurveyBoqImportSessions",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyBoqImportSessions_ProjectId",
                table: "QuantitySurveyBoqImportSessions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyBoqImportSessions_TenantId_IdempotencyKey",
                table: "QuantitySurveyBoqImportSessions",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyBoqImportSessions_TenantId_ProjectId_CreatedAt",
                table: "QuantitySurveyBoqImportSessions",
                columns: new[] { "TenantId", "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyBoqImportSessions_TenantId_ProjectId_Status",
                table: "QuantitySurveyBoqImportSessions",
                columns: new[] { "TenantId", "ProjectId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuantitySurveyBoqImportSessions");

            migrationBuilder.DropIndex(
                name: "IX_ProjectBoqItems_CostCodeCatalogEntryId",
                table: "ProjectBoqItems");

            migrationBuilder.DropIndex(
                name: "IX_ProjectBoqItems_MeasurementCodeCatalogEntryId",
                table: "ProjectBoqItems");

            migrationBuilder.DropIndex(
                name: "IX_ProjectBoqItems_SectionCatalogEntryId",
                table: "ProjectBoqItems");

            migrationBuilder.DropIndex(
                name: "IX_ProjectBoqItems_TradeCatalogEntryId",
                table: "ProjectBoqItems");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoqItems_TenantId",
                table: "ProjectBoqItems",
                column: "TenantId");
        }
    }
}
