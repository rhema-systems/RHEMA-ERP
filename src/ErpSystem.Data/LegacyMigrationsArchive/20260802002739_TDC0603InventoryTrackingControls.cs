using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class TDC0603InventoryTrackingControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BatchNumber",
                table: "StockMovements",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryTrackingExceptionId",
                table: "StockMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManufactureDate",
                table: "StockMovements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchNumber",
                table: "PurchaseOrderReceiptItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryTrackingExceptionId",
                table: "PurchaseOrderReceiptItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManufactureDate",
                table: "PurchaseOrderReceiptItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                table: "InventoryTransferItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryTrackingExceptionId",
                table: "InventoryTransferItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManufactureDate",
                table: "InventoryTransferItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrackingSequence",
                table: "InventoryTransferItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BatchNumber",
                table: "InventoryScanLines",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                table: "InventoryScanLines",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryTrackingExceptionId",
                table: "InventoryScanLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManufactureDate",
                table: "InventoryScanLines",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                table: "InventoryRequisitionItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryTrackingExceptionId",
                table: "InventoryRequisitionItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManufactureDate",
                table: "InventoryRequisitionItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrackingSequence",
                table: "InventoryRequisitionItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsBatchTracked",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsManufactureDateTracked",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultBatchTracking",
                table: "InventoryCategories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultExpirationTracking",
                table: "InventoryCategories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultManufactureDateTracking",
                table: "InventoryCategories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnforceFifoIssue",
                table: "InventoryCategories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MinimumShelfLifeDays",
                table: "InventoryCategories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryTrackingExceptionId",
                table: "GoodsReceiptNoteItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InventoryTrackingExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReferenceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ExceptionCodesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowEvidenceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsumedByReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConsumedByEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_InventoryTrackingExceptions", x => x.Id);
                    table.CheckConstraint("CK_InventoryTrackingExceptions_Expiry", "[ExpiresAtUtc] > [ApprovedAtUtc]");
                    table.CheckConstraint("CK_InventoryTrackingExceptions_IntegrityHash", "LEN([IntegrityHash]) = 64");
                    table.ForeignKey(
                        name: "FK_InventoryTrackingExceptions_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTrackingExceptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTrackingExceptions_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTrackingExceptions_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTrackingExceptions_WorkflowEvidenceDocuments_WorkflowEvidenceDocumentId",
                        column: x => x.WorkflowEvidenceDocumentId,
                        principalTable: "WorkflowEvidenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTrackingExceptions_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTraceabilityEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ManufactureDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TrackingExceptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_InventoryTraceabilityEvents", x => x.Id);
                    table.CheckConstraint("CK_InventoryTraceabilityEvents_PayloadHash", "LEN([PayloadHash]) = 64");
                    table.CheckConstraint("CK_InventoryTraceabilityEvents_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_InventoryTraceabilityEvents_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTraceabilityEvents_InventoryTrackingExceptions_TrackingExceptionId",
                        column: x => x.TrackingExceptionId,
                        principalTable: "InventoryTrackingExceptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTraceabilityEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTraceabilityEvents_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTraceabilityEvents_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTraceabilityEvents_InventoryItemId",
                table: "InventoryTraceabilityEvents",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTraceabilityEvents_LocationId",
                table: "InventoryTraceabilityEvents",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTraceabilityEvents_TenantId_EventKey",
                table: "InventoryTraceabilityEvents",
                columns: new[] { "TenantId", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTraceabilityEvents_TenantId_InventoryItemId_WarehouseId_OccurredAtUtc",
                table: "InventoryTraceabilityEvents",
                columns: new[] { "TenantId", "InventoryItemId", "WarehouseId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTraceabilityEvents_TenantId_LotNumber_BatchNumber",
                table: "InventoryTraceabilityEvents",
                columns: new[] { "TenantId", "LotNumber", "BatchNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTraceabilityEvents_TenantId_SerialNumber",
                table: "InventoryTraceabilityEvents",
                columns: new[] { "TenantId", "SerialNumber" },
                filter: "[SerialNumber] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTraceabilityEvents_TrackingExceptionId",
                table: "InventoryTraceabilityEvents",
                column: "TrackingExceptionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTraceabilityEvents_WarehouseId",
                table: "InventoryTraceabilityEvents",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTrackingExceptions_InventoryItemId",
                table: "InventoryTrackingExceptions",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTrackingExceptions_LocationId",
                table: "InventoryTrackingExceptions",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTrackingExceptions_TenantId_InventoryItemId_WarehouseId_ExpiresAtUtc",
                table: "InventoryTrackingExceptions",
                columns: new[] { "TenantId", "InventoryItemId", "WarehouseId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTrackingExceptions_TenantId_ReferenceId_ReferenceLineId",
                table: "InventoryTrackingExceptions",
                columns: new[] { "TenantId", "ReferenceId", "ReferenceLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTrackingExceptions_TenantId_WorkflowInstanceId",
                table: "InventoryTrackingExceptions",
                columns: new[] { "TenantId", "WorkflowInstanceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTrackingExceptions_WarehouseId",
                table: "InventoryTrackingExceptions",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTrackingExceptions_WorkflowEvidenceDocumentId",
                table: "InventoryTrackingExceptions",
                column: "WorkflowEvidenceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTrackingExceptions_WorkflowInstanceId",
                table: "InventoryTrackingExceptions",
                column: "WorkflowInstanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryTraceabilityEvents");

            migrationBuilder.DropTable(
                name: "InventoryTrackingExceptions");

            migrationBuilder.DropColumn(
                name: "BatchNumber",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "InventoryTrackingExceptionId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "ManufactureDate",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "BatchNumber",
                table: "PurchaseOrderReceiptItems");

            migrationBuilder.DropColumn(
                name: "InventoryTrackingExceptionId",
                table: "PurchaseOrderReceiptItems");

            migrationBuilder.DropColumn(
                name: "ManufactureDate",
                table: "PurchaseOrderReceiptItems");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "InventoryTransferItems");

            migrationBuilder.DropColumn(
                name: "InventoryTrackingExceptionId",
                table: "InventoryTransferItems");

            migrationBuilder.DropColumn(
                name: "ManufactureDate",
                table: "InventoryTransferItems");

            migrationBuilder.DropColumn(
                name: "TrackingSequence",
                table: "InventoryTransferItems");

            migrationBuilder.DropColumn(
                name: "BatchNumber",
                table: "InventoryScanLines");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "InventoryScanLines");

            migrationBuilder.DropColumn(
                name: "InventoryTrackingExceptionId",
                table: "InventoryScanLines");

            migrationBuilder.DropColumn(
                name: "ManufactureDate",
                table: "InventoryScanLines");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "InventoryRequisitionItems");

            migrationBuilder.DropColumn(
                name: "InventoryTrackingExceptionId",
                table: "InventoryRequisitionItems");

            migrationBuilder.DropColumn(
                name: "ManufactureDate",
                table: "InventoryRequisitionItems");

            migrationBuilder.DropColumn(
                name: "TrackingSequence",
                table: "InventoryRequisitionItems");

            migrationBuilder.DropColumn(
                name: "IsBatchTracked",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "IsManufactureDateTracked",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "DefaultBatchTracking",
                table: "InventoryCategories");

            migrationBuilder.DropColumn(
                name: "DefaultExpirationTracking",
                table: "InventoryCategories");

            migrationBuilder.DropColumn(
                name: "DefaultManufactureDateTracking",
                table: "InventoryCategories");

            migrationBuilder.DropColumn(
                name: "EnforceFifoIssue",
                table: "InventoryCategories");

            migrationBuilder.DropColumn(
                name: "MinimumShelfLifeDays",
                table: "InventoryCategories");

            migrationBuilder.DropColumn(
                name: "InventoryTrackingExceptionId",
                table: "GoodsReceiptNoteItems");
        }
    }
}
