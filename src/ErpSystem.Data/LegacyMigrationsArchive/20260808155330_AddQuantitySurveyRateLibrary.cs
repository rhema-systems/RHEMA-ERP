using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantitySurveyRateLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuantitySurveyRateLibraryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectCatalogEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyRateLibraryItems", x => x.Id);
                    table.CheckConstraint("CK_QsRateLibraryItems_Category", "[Category] IN (0, 1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateLibraryItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateLibraryItems_ProjectCatalogEntries_ProjectCatalogEntryId",
                        column: x => x.ProjectCatalogEntryId,
                        principalTable: "ProjectCatalogEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateLibraryItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateLibraryItems_UnitsOfMeasure_UnitOfMeasureId",
                        column: x => x.UnitOfMeasureId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuantitySurveyRateLibraryRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RateLibraryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    UnitRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyCodeSnapshot = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProjectTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    SourceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CentralDocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LifecycleStatus = table.Column<int>(type: "int", nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PreparedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreparedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetiredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditAction = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyRateLibraryRates", x => x.Id);
                    table.CheckConstraint("CK_QsRateLibraryRates_EvidencePair", "([CentralDocumentRecordId] IS NULL AND [CentralDocumentVersionId] IS NULL) OR ([CentralDocumentRecordId] IS NOT NULL AND [CentralDocumentVersionId] IS NOT NULL)");
                    table.CheckConstraint("CK_QsRateLibraryRates_Period", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_QsRateLibraryRates_Source", "[SourceType] IN (0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11)");
                    table.CheckConstraint("CK_QsRateLibraryRates_Status", "[LifecycleStatus] IN (0, 1, 2)");
                    table.CheckConstraint("CK_QsRateLibraryRates_UnitRate", "[UnitRate] >= 0");
                    table.CheckConstraint("CK_QsRateLibraryRates_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateLibraryRates_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateLibraryRates_CentralDocumentRecords_CentralDocumentRecordId",
                        column: x => x.CentralDocumentRecordId,
                        principalTable: "CentralDocumentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateLibraryRates_CentralDocumentVersions_CentralDocumentVersionId",
                        column: x => x.CentralDocumentVersionId,
                        principalTable: "CentralDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateLibraryRates_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateLibraryRates_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateLibraryRates_ProjectTypes_ProjectTypeId",
                        column: x => x.ProjectTypeId,
                        principalTable: "ProjectTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateLibraryRates_QuantitySurveyRateLibraryItems_RateLibraryItemId",
                        column: x => x.RateLibraryItemId,
                        principalTable: "QuantitySurveyRateLibraryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateLibraryRates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuantitySurveyRateLibraryRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RateLibraryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_QuantitySurveyRateLibraryRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateLibraryRevisions_QuantitySurveyRateLibraryItems_RateLibraryItemId",
                        column: x => x.RateLibraryItemId,
                        principalTable: "QuantitySurveyRateLibraryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateLibraryRevisions_QuantitySurveyRateLibraryRates_RateId",
                        column: x => x.RateId,
                        principalTable: "QuantitySurveyRateLibraryRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyRateLibraryRevisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryItems_InventoryItemId",
                table: "QuantitySurveyRateLibraryItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryItems_ProjectCatalogEntryId",
                table: "QuantitySurveyRateLibraryItems",
                column: "ProjectCatalogEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryItems_TenantId_Category_IsActive",
                table: "QuantitySurveyRateLibraryItems",
                columns: new[] { "TenantId", "Category", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryItems_TenantId_Code",
                table: "QuantitySurveyRateLibraryItems",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryItems_TenantId_InventoryItemId",
                table: "QuantitySurveyRateLibraryItems",
                columns: new[] { "TenantId", "InventoryItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryItems_UnitOfMeasureId",
                table: "QuantitySurveyRateLibraryItems",
                column: "UnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryRates_BusinessPartnerId",
                table: "QuantitySurveyRateLibraryRates",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryRates_CentralDocumentRecordId",
                table: "QuantitySurveyRateLibraryRates",
                column: "CentralDocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryRates_CentralDocumentVersionId",
                table: "QuantitySurveyRateLibraryRates",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryRates_CurrencyId",
                table: "QuantitySurveyRateLibraryRates",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryRates_LocationId",
                table: "QuantitySurveyRateLibraryRates",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryRates_ProjectTypeId",
                table: "QuantitySurveyRateLibraryRates",
                column: "ProjectTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryRates_RateLibraryItemId",
                table: "QuantitySurveyRateLibraryRates",
                column: "RateLibraryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryRates_TenantId_CentralDocumentVersionId",
                table: "QuantitySurveyRateLibraryRates",
                columns: new[] { "TenantId", "CentralDocumentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryRates_TenantId_ProjectTypeId_LocationId_BusinessPartnerId",
                table: "QuantitySurveyRateLibraryRates",
                columns: new[] { "TenantId", "ProjectTypeId", "LocationId", "BusinessPartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryRates_TenantId_RateLibraryItemId_LifecycleStatus_EffectiveFrom",
                table: "QuantitySurveyRateLibraryRates",
                columns: new[] { "TenantId", "RateLibraryItemId", "LifecycleStatus", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryRates_TenantId_RateLibraryItemId_Version",
                table: "QuantitySurveyRateLibraryRates",
                columns: new[] { "TenantId", "RateLibraryItemId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryRevisions_RateId",
                table: "QuantitySurveyRateLibraryRevisions",
                column: "RateId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryRevisions_RateLibraryItemId",
                table: "QuantitySurveyRateLibraryRevisions",
                column: "RateLibraryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryRevisions_TenantId_CorrelationId",
                table: "QuantitySurveyRateLibraryRevisions",
                columns: new[] { "TenantId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyRateLibraryRevisions_TenantId_RateLibraryItemId_CreatedAt",
                table: "QuantitySurveyRateLibraryRevisions",
                columns: new[] { "TenantId", "RateLibraryItemId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuantitySurveyRateLibraryRevisions");

            migrationBuilder.DropTable(
                name: "QuantitySurveyRateLibraryRates");

            migrationBuilder.DropTable(
                name: "QuantitySurveyRateLibraryItems");
        }
    }
}
