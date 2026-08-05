using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class TDC0602InventoryMobileScanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryLabelProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Symbology = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WidthMm = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    HeightMm = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Dpi = table.Column<int>(type: "int", nullable: false),
                    IncludeItemCode = table.Column<bool>(type: "bit", nullable: false),
                    IncludeItemName = table.Column<bool>(type: "bit", nullable: false),
                    IncludeUnit = table.Column<bool>(type: "bit", nullable: false),
                    IncludeLot = table.Column<bool>(type: "bit", nullable: false),
                    IncludeSerial = table.Column<bool>(type: "bit", nullable: false),
                    IncludeExpiry = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_InventoryLabelProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryLabelProfiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryScanBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Operation = table.Column<int>(type: "int", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplyTransaction = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FailureCode = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FailureMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReconciliationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_InventoryScanBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryScanBatches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryScanBatches_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryLabelPrintEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabelProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Identifier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IdentifierKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LabelCount = table.Column<int>(type: "int", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PrinterName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PrintedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrintedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_InventoryLabelPrintEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryLabelPrintEvents_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLabelPrintEvents_InventoryLabelProfiles_LabelProfileId",
                        column: x => x.LabelProfileId,
                        principalTable: "InventoryLabelProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLabelPrintEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLabelPrintEvents_UnitsOfMeasure_UnitOfMeasureId",
                        column: x => x.UnitOfMeasureId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryScanLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScanBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    RawIdentifier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IdentifierKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScannedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ConversionToBase = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LocationIdentifier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ScannedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_InventoryScanLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryScanLines_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryScanLines_InventoryScanBatches_ScanBatchId",
                        column: x => x.ScanBatchId,
                        principalTable: "InventoryScanBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryScanLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryScanLines_UnitsOfMeasure_UnitOfMeasureId",
                        column: x => x.UnitOfMeasureId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryScanLines_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLabelPrintEvents_InventoryItemId",
                table: "InventoryLabelPrintEvents",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLabelPrintEvents_LabelProfileId",
                table: "InventoryLabelPrintEvents",
                column: "LabelProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLabelPrintEvents_TenantId_PrintedAtUtc",
                table: "InventoryLabelPrintEvents",
                columns: new[] { "TenantId", "PrintedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLabelPrintEvents_UnitOfMeasureId",
                table: "InventoryLabelPrintEvents",
                column: "UnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLabelProfiles_TenantId",
                table: "InventoryLabelProfiles",
                column: "TenantId",
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLabelProfiles_TenantId_Name",
                table: "InventoryLabelProfiles",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryScanBatches_TenantId_CapturedAtUtc",
                table: "InventoryScanBatches",
                columns: new[] { "TenantId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryScanBatches_TenantId_DeviceId_IdempotencyKey",
                table: "InventoryScanBatches",
                columns: new[] { "TenantId", "DeviceId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryScanBatches_TenantId_DocumentId_Operation",
                table: "InventoryScanBatches",
                columns: new[] { "TenantId", "DocumentId", "Operation" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryScanBatches_WarehouseId",
                table: "InventoryScanBatches",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryScanLines_InventoryItemId",
                table: "InventoryScanLines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryScanLines_LocationId",
                table: "InventoryScanLines",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryScanLines_ScanBatchId_ClientLineId",
                table: "InventoryScanLines",
                columns: new[] { "ScanBatchId", "ClientLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryScanLines_TenantId_InventoryItemId_ScannedAtUtc",
                table: "InventoryScanLines",
                columns: new[] { "TenantId", "InventoryItemId", "ScannedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryScanLines_UnitOfMeasureId",
                table: "InventoryScanLines",
                column: "UnitOfMeasureId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryLabelPrintEvents");

            migrationBuilder.DropTable(
                name: "InventoryScanLines");

            migrationBuilder.DropTable(
                name: "InventoryLabelProfiles");

            migrationBuilder.DropTable(
                name: "InventoryScanBatches");
        }
    }
}
