using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnhancedInventoryModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationTemplateCriteria_EvaluationCriteria_EvaluationCriterionId1",
                table: "EvaluationTemplateCriteria");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceSchedules_MaintenanceTypes_MaintenanceTypeId1",
                table: "MaintenanceSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_TechnicianSkillAssignments_Technicians_TechnicianId1",
                table: "TechnicianSkillAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_TechnicianTeams_Technicians_TechnicianId",
                table: "TechnicianTeams");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianTeams_TechnicianId",
                table: "TechnicianTeams");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianSkillAssignments_TechnicianId1",
                table: "TechnicianSkillAssignments");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceSchedules_MaintenanceTypeId1",
                table: "MaintenanceSchedules");

            migrationBuilder.DropIndex(
                name: "IX_EvaluationTemplateCriteria_EvaluationCriterionId1",
                table: "EvaluationTemplateCriteria");
            migrationBuilder.DropColumn(
                name: "TechnicianId",
                table: "TechnicianTeams");

            migrationBuilder.DropColumn(
                name: "TechnicianId1",
                table: "TechnicianSkillAssignments");

            migrationBuilder.DropColumn(
                name: "MaintenanceTypeId1",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "EvaluationCriterionId1",
                table: "EvaluationTemplateCriteria");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "CloseTime",
                table: "Warehouses",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CostCenter",
                table: "Warehouses",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultInTransitLocationId",
                table: "Warehouses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultQuarantineLocationId",
                table: "Warehouses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultReceivingLocationId",
                table: "Warehouses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultShippingLocationId",
                table: "Warehouses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GLAccountCode",
                table: "Warehouses",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasInspectionArea",
                table: "Warehouses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasQuarantineArea",
                table: "Warehouses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasReceivingDock",
                table: "Warehouses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasShippingDock",
                table: "Warehouses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTemperatureControlled",
                table: "Warehouses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ManagerId",
                table: "Warehouses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxTemperature",
                table: "Warehouses",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxWeightCapacity",
                table: "Warehouses",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinTemperature",
                table: "Warehouses",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "OpenTime",
                table: "Warehouses",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatingHours",
                table: "Warehouses",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemperatureUnit",
                table: "Warehouses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCapacityCubicFeet",
                table: "Warehouses",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCapacitySquareFeet",
                table: "Warehouses",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalLocations",
                table: "Warehouses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UsedCapacitySquareFeet",
                table: "Warehouses",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsedLocations",
                table: "Warehouses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ABCClass",
                table: "WarehouseLocations",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aisle",
                table: "WarehouseLocations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bin",
                table: "WarehouseLocations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ColumnNumber",
                table: "WarehouseLocations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DedicatedItemCode",
                table: "WarehouseLocations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DedicatedItemId",
                table: "WarehouseLocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDamageLocation",
                table: "WarehouseLocations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsInTransitLocation",
                table: "WarehouseLocations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsInspectionLocation",
                table: "WarehouseLocations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsQuarantineLocation",
                table: "WarehouseLocations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsReturnLocation",
                table: "WarehouseLocations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsShippingLocation",
                table: "WarehouseLocations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsStagingLocation",
                table: "WarehouseLocations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LevelNumber",
                table: "WarehouseLocations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationBarcode",
                table: "WarehouseLocations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LocationHierarchyType",
                table: "WarehouseLocations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PickSequence",
                table: "WarehouseLocations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Rack",
                table: "WarehouseLocations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RowNumber",
                table: "WarehouseLocations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Shelf",
                table: "WarehouseLocations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemperatureZone",
                table: "WarehouseLocations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Zone",
                table: "WarehouseLocations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AmendmentNotes",
                table: "PurchaseRequisitions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalHistory",
                table: "PurchaseRequisitions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalLevel",
                table: "PurchaseRequisitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "BudgetAllocated",
                table: "PurchaseRequisitions",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BudgetCode",
                table: "PurchaseRequisitions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BudgetId",
                table: "PurchaseRequisitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BudgetRemaining",
                table: "PurchaseRequisitions",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BudgetValidated",
                table: "PurchaseRequisitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "PurchaseRequisitions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentApproverId",
                table: "PurchaseRequisitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryAddress",
                table: "PurchaseRequisitions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryInstructions",
                table: "PurchaseRequisitions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryWarehouseId",
                table: "PurchaseRequisitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeneratedFrom",
                table: "PurchaseRequisitions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAutoGenerated",
                table: "PurchaseRequisitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAmendedAt",
                table: "PurchaseRequisitions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastAmendedById",
                table: "PurchaseRequisitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreferredSupplierId",
                table: "PurchaseRequisitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectCode",
                table: "PurchaseRequisitions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "PurchaseRequisitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectName",
                table: "PurchaseRequisitions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredApprovalLevel",
                table: "PurchaseRequisitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RequisitionType",
                table: "PurchaseRequisitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RevisionNumber",
                table: "PurchaseRequisitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SourcePlanId",
                table: "PurchaseRequisitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoCloseOnReceipt",
                table: "PurchaseOrders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BudgetCode",
                table: "PurchaseOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BudgetId",
                table: "PurchaseOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BudgetValidated",
                table: "PurchaseOrders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContractEndDate",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ContractRemainingValue",
                table: "PurchaseOrders",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContractStartDate",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ContractUsedValue",
                table: "PurchaseOrders",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ContractValue",
                table: "PurchaseOrders",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "PurchaseOrders",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "PurchaseOrders",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAmendedAt",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastAmendedById",
                table: "PurchaseOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderType",
                table: "PurchaseOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RevisionNumber",
                table: "PurchaseOrders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceRequisitionId",
                table: "PurchaseOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceRequisitionNumber",
                table: "PurchaseOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TolerancePercent",
                table: "PurchaseOrders",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowBackorder",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AlternateBarcode",
                table: "InventoryItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoReorder",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "InventoryItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BaseUnitOfMeasureEntityId",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BaseUnitOfMeasureId",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryOfOrigin",
                table: "InventoryItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultWarehouseId",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HSCode",
                table: "InventoryItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "InventoryItems",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTaxable",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LongDescription",
                table: "InventoryItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumOrderQuantity",
                table: "InventoryItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "MovementClass",
                table: "InventoryItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OrderMultiple",
                table: "InventoryItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimarySupplierId",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QRCode",
                table: "InventoryItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireApprovalForPurchase",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ShortDescription",
                table: "InventoryItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubCategoryId",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxCode",
                table: "InventoryItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailUrl",
                table: "InventoryItems",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValuationMethod",
                table: "InventoryItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "XYZClass",
                table: "InventoryItems",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GoodsReceiptNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GRNNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PurchaseOrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReceiptDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivingLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeliveryNoteNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CarrierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TrackingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VehicleNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DriverName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequiresInspection = table.Column<bool>(type: "bit", nullable: false),
                    ReceivedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InspectedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InspectionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InspectionResult = table.Column<int>(type: "int", nullable: false),
                    InspectionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TotalItems = table.Column<int>(type: "int", nullable: false),
                    TotalQuantityReceived = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalQuantityAccepted = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalQuantityRejected = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StockUpdated = table.Column<bool>(type: "bit", nullable: false),
                    StockUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_GoodsReceiptNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptNotes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptNotes_Users_InspectedById",
                        column: x => x.InspectedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GoodsReceiptNotes_Users_ReceivedById",
                        column: x => x.ReceivedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GoodsReceiptNotes_WarehouseLocations_ReceivingLocationId",
                        column: x => x.ReceivingLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GoodsReceiptNotes_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InventoryCostLayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LayerNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LayerDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SourceReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ConsumedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LandedCostPerUnit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalUnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RemainingValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsFullyConsumed = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    InventoryItemId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_InventoryCostLayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_InventoryItems_InventoryItemId1",
                        column: x => x.InventoryItemId1,
                        principalTable: "InventoryItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransferNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceWarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DestinationWarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinationLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InTransitLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShippedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ShippedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceivedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CarrierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TrackingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VehicleNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TotalItems = table.Column<int>(type: "int", nullable: false),
                    TotalQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_InventoryTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_Users_ReceivedById",
                        column: x => x.ReceivedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_Users_ShippedById",
                        column: x => x.ShippedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_WarehouseLocations_DestinationLocationId",
                        column: x => x.DestinationLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_WarehouseLocations_InTransitLocationId",
                        column: x => x.InTransitLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_WarehouseLocations_SourceLocationId",
                        column: x => x.SourceLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_Warehouses_DestinationWarehouseId",
                        column: x => x.DestinationWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryTransfers_Warehouses_SourceWarehouseId",
                        column: x => x.SourceWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ItemSuppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SupplierItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PriceEffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PriceExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MinimumOrderQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OrderMultiple = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: false),
                    IsPreferred = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_ItemSuppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemSuppliers_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemSuppliers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalCounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CountType = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CountDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FreezeInventory = table.Column<bool>(type: "bit", nullable: false),
                    IncludeZeroStock = table.Column<bool>(type: "bit", nullable: false),
                    BlindCount = table.Column<bool>(type: "bit", nullable: false),
                    InitiatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CountedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TotalItems = table.Column<int>(type: "int", nullable: false),
                    CountedItems = table.Column<int>(type: "int", nullable: false),
                    VarianceItems = table.Column<int>(type: "int", nullable: false),
                    TotalSystemQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCountedQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalVarianceQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalVarianceValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_PhysicalCounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhysicalCounts_InventoryCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "InventoryCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalCounts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCounts_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalCounts_Users_CountedById",
                        column: x => x.CountedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalCounts_Users_InitiatedById",
                        column: x => x.InitiatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalCounts_Users_PostedById",
                        column: x => x.PostedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalCounts_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalCounts_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UnitsOfMeasure",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IsBaseUnit = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_UnitsOfMeasure", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitsOfMeasure_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptNoteItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoodsReceiptNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OrderedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AcceptedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RejectedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    StorageLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ManufactureDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InspectionResult = table.Column<int>(type: "int", nullable: false),
                    InspectionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_GoodsReceiptNoteItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptNoteItems_GoodsReceiptNotes_GoodsReceiptNoteId",
                        column: x => x.GoodsReceiptNoteId,
                        principalTable: "GoodsReceiptNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptNoteItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptNoteItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptNoteItems_WarehouseLocations_StorageLocationId",
                        column: x => x.StorageLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LandedCosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandedCostNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GoodsReceiptNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GRNNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CostDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnallocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VendorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_LandedCosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandedCosts_GoodsReceiptNotes_GoodsReceiptNoteId",
                        column: x => x.GoodsReceiptNoteId,
                        principalTable: "GoodsReceiptNotes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LandedCosts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandedCosts_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LandedCosts_Users_PostedById",
                        column: x => x.PostedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReturns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GoodsReceiptNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GRNNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PurchaseOrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ShippedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReturnReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReturnReasonDetails = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CarrierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TrackingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DebitNoteRequired = table.Column<bool>(type: "bit", nullable: false),
                    DebitNoteNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DebitNoteDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalItems = table.Column<int>(type: "int", nullable: false),
                    TotalQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditNoteNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RefundReceived = table.Column<bool>(type: "bit", nullable: false),
                    RefundDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_PurchaseReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_GoodsReceiptNotes_GoodsReceiptNoteId",
                        column: x => x.GoodsReceiptNoteId,
                        principalTable: "GoodsReceiptNotes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransferItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryTransferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RequestedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ShippedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DamagedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SourceLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DestinationLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DamageNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_InventoryTransferItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryTransferItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferItems_InventoryTransfers_InventoryTransferId",
                        column: x => x.InventoryTransferId,
                        principalTable: "InventoryTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryTransferItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferItems_WarehouseLocations_DestinationLocationId",
                        column: x => x.DestinationLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryTransferItems_WarehouseLocations_SourceLocationId",
                        column: x => x.SourceLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PhysicalCountItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhysicalCountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SystemQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CountedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    VarianceQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VarianceValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsCounted = table.Column<bool>(type: "bit", nullable: false),
                    CountedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CountedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CountAttempts = table.Column<int>(type: "int", nullable: false),
                    RequiresRecount = table.Column<bool>(type: "bit", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VarianceReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_PhysicalCountItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhysicalCountItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCountItems_PhysicalCounts_PhysicalCountId",
                        column: x => x.PhysicalCountId,
                        principalTable: "PhysicalCounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PhysicalCountItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCountItems_Users_CountedById",
                        column: x => x.CountedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalCountItems_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ItemUnitsOfMeasure",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversionToBase = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsBaseUnit = table.Column<bool>(type: "bit", nullable: false),
                    IsPurchaseUnit = table.Column<bool>(type: "bit", nullable: false),
                    IsSalesUnit = table.Column<bool>(type: "bit", nullable: false),
                    IsStockingUnit = table.Column<bool>(type: "bit", nullable: false),
                    Barcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DefaultPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ItemUnitsOfMeasure", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemUnitsOfMeasure_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemUnitsOfMeasure_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemUnitsOfMeasure_UnitsOfMeasure_UnitOfMeasureId",
                        column: x => x.UnitOfMeasureId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnitOfMeasureConversions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversionFactor = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_UnitOfMeasureConversions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitOfMeasureConversions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitOfMeasureConversions_UnitsOfMeasure_FromUnitId",
                        column: x => x.FromUnitId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitOfMeasureConversions_UnitsOfMeasure_ToUnitId",
                        column: x => x.ToUnitId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LandedCostItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandedCostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CostType = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AmountInBaseCurrency = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AllocationMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_LandedCostItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandedCostItems_LandedCosts_LandedCostId",
                        column: x => x.LandedCostId,
                        principalTable: "LandedCosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LandedCostItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReturnItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoodsReceiptNoteItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReturnQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReturnReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReturnReasonDetails = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StockReversed = table.Column<bool>(type: "bit", nullable: false),
                    StockReversedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_PurchaseReturnItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_GoodsReceiptNoteItems_GoodsReceiptNoteItemId",
                        column: x => x.GoodsReceiptNoteItemId,
                        principalTable: "GoodsReceiptNoteItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_PurchaseReturns_PurchaseReturnId",
                        column: x => x.PurchaseReturnId,
                        principalTable: "PurchaseReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LandedCostAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandedCostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandedCostItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoodsReceiptNoteItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostPerUnit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostLayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_LandedCostAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandedCostAllocations_GoodsReceiptNoteItems_GoodsReceiptNoteItemId",
                        column: x => x.GoodsReceiptNoteItemId,
                        principalTable: "GoodsReceiptNoteItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LandedCostAllocations_InventoryCostLayers_CostLayerId",
                        column: x => x.CostLayerId,
                        principalTable: "InventoryCostLayers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LandedCostAllocations_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LandedCostAllocations_LandedCostItems_LandedCostItemId",
                        column: x => x.LandedCostItemId,
                        principalTable: "LandedCostItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LandedCostAllocations_LandedCosts_LandedCostId",
                        column: x => x.LandedCostId,
                        principalTable: "LandedCosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LandedCostAllocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3434));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3491));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3494));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3497));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3798));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3822));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3830));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3837));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3849));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3857));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3866));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3873));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3882));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3892));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3900));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3906));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3917));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3946));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3967));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3974));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4030));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4032));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4033));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4034));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4035));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4037));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4038));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4040));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4041));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4043));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4044));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4045));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4046));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4047));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4048));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4049));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4102));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4104));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4106));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4107));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4108));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4109));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4110));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4111));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4112));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4113));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4114));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4115));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4116));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4116));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4117));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4166));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4167));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4169));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4170));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4171));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4172));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4173));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4174));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4175));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4176));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4190));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4191));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(4192));
            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 4, 8, 7, 29, 58, DateTimeKind.Utc).AddTicks(3188));

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_DefaultInTransitLocationId",
                table: "Warehouses",
                column: "DefaultInTransitLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_DefaultQuarantineLocationId",
                table: "Warehouses",
                column: "DefaultQuarantineLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_DefaultReceivingLocationId",
                table: "Warehouses",
                column: "DefaultReceivingLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_DefaultShippingLocationId",
                table: "Warehouses",
                column: "DefaultShippingLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_ManagerId",
                table: "Warehouses",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_DedicatedItemId",
                table: "WarehouseLocations",
                column: "DedicatedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_EmployeeNumber",
                table: "Technicians",
                column: "EmployeeNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_IsActive",
                table: "Technicians",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_Specialization",
                table: "Technicians",
                column: "Specialization");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_CurrentApproverId",
                table: "PurchaseRequisitions",
                column: "CurrentApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_LastAmendedById",
                table: "PurchaseRequisitions",
                column: "LastAmendedById");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_LastAmendedById",
                table: "PurchaseOrders",
                column: "LastAmendedById");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_SourceRequisitionId",
                table: "PurchaseOrders",
                column: "SourceRequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_BaseUnitOfMeasureEntityId",
                table: "InventoryItems",
                column: "BaseUnitOfMeasureEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_DefaultWarehouseId",
                table: "InventoryItems",
                column: "DefaultWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_SubCategoryId",
                table: "InventoryItems",
                column: "SubCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNoteItems_GoodsReceiptNoteId",
                table: "GoodsReceiptNoteItems",
                column: "GoodsReceiptNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNoteItems_InventoryItemId",
                table: "GoodsReceiptNoteItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNoteItems_StorageLocationId",
                table: "GoodsReceiptNoteItems",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNoteItems_TenantId",
                table: "GoodsReceiptNoteItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_GRNNumber",
                table: "GoodsReceiptNotes",
                column: "GRNNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_InspectedById",
                table: "GoodsReceiptNotes",
                column: "InspectedById");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_ReceiptDate",
                table: "GoodsReceiptNotes",
                column: "ReceiptDate");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_ReceivedById",
                table: "GoodsReceiptNotes",
                column: "ReceivedById");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_ReceivingLocationId",
                table: "GoodsReceiptNotes",
                column: "ReceivingLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_Status",
                table: "GoodsReceiptNotes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_SupplierId",
                table: "GoodsReceiptNotes",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_TenantId",
                table: "GoodsReceiptNotes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptNotes_WarehouseId",
                table: "GoodsReceiptNotes",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_InventoryItemId",
                table: "InventoryCostLayers",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_InventoryItemId1",
                table: "InventoryCostLayers",
                column: "InventoryItemId1");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_IsFullyConsumed",
                table: "InventoryCostLayers",
                column: "IsFullyConsumed");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_LayerDate",
                table: "InventoryCostLayers",
                column: "LayerDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_LocationId",
                table: "InventoryCostLayers",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_TenantId",
                table: "InventoryCostLayers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_WarehouseId",
                table: "InventoryCostLayers",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferItems_DestinationLocationId",
                table: "InventoryTransferItems",
                column: "DestinationLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferItems_InventoryItemId",
                table: "InventoryTransferItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferItems_InventoryTransferId",
                table: "InventoryTransferItems",
                column: "InventoryTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferItems_SourceLocationId",
                table: "InventoryTransferItems",
                column: "SourceLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferItems_TenantId",
                table: "InventoryTransferItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_ApprovedById",
                table: "InventoryTransfers",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_DestinationLocationId",
                table: "InventoryTransfers",
                column: "DestinationLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_DestinationWarehouseId",
                table: "InventoryTransfers",
                column: "DestinationWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_InTransitLocationId",
                table: "InventoryTransfers",
                column: "InTransitLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_ReceivedById",
                table: "InventoryTransfers",
                column: "ReceivedById");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_RequestDate",
                table: "InventoryTransfers",
                column: "RequestDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_RequestedById",
                table: "InventoryTransfers",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_ShippedById",
                table: "InventoryTransfers",
                column: "ShippedById");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_SourceLocationId",
                table: "InventoryTransfers",
                column: "SourceLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_SourceWarehouseId",
                table: "InventoryTransfers",
                column: "SourceWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_Status",
                table: "InventoryTransfers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_TenantId",
                table: "InventoryTransfers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_TransferNumber",
                table: "InventoryTransfers",
                column: "TransferNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemSuppliers_InventoryItemId",
                table: "ItemSuppliers",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSuppliers_TenantId",
                table: "ItemSuppliers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemUnitsOfMeasure_InventoryItemId_UnitOfMeasureId",
                table: "ItemUnitsOfMeasure",
                columns: new[] { "InventoryItemId", "UnitOfMeasureId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemUnitsOfMeasure_IsActive",
                table: "ItemUnitsOfMeasure",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ItemUnitsOfMeasure_TenantId",
                table: "ItemUnitsOfMeasure",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemUnitsOfMeasure_UnitOfMeasureId",
                table: "ItemUnitsOfMeasure",
                column: "UnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostAllocations_CostLayerId",
                table: "LandedCostAllocations",
                column: "CostLayerId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostAllocations_GoodsReceiptNoteItemId",
                table: "LandedCostAllocations",
                column: "GoodsReceiptNoteItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostAllocations_InventoryItemId",
                table: "LandedCostAllocations",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostAllocations_LandedCostId",
                table: "LandedCostAllocations",
                column: "LandedCostId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostAllocations_LandedCostItemId",
                table: "LandedCostAllocations",
                column: "LandedCostItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostAllocations_TenantId",
                table: "LandedCostAllocations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostItems_LandedCostId",
                table: "LandedCostItems",
                column: "LandedCostId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostItems_TenantId",
                table: "LandedCostItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCosts_ApprovedById",
                table: "LandedCosts",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCosts_GoodsReceiptNoteId",
                table: "LandedCosts",
                column: "GoodsReceiptNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCosts_LandedCostNumber",
                table: "LandedCosts",
                column: "LandedCostNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandedCosts_PostedById",
                table: "LandedCosts",
                column: "PostedById");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCosts_Status",
                table: "LandedCosts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCosts_TenantId",
                table: "LandedCosts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountItems_CountedById",
                table: "PhysicalCountItems",
                column: "CountedById");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountItems_InventoryItemId",
                table: "PhysicalCountItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountItems_LocationId",
                table: "PhysicalCountItems",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountItems_PhysicalCountId",
                table: "PhysicalCountItems",
                column: "PhysicalCountId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountItems_TenantId",
                table: "PhysicalCountItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_ApprovedById",
                table: "PhysicalCounts",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_CategoryId",
                table: "PhysicalCounts",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_CountDate",
                table: "PhysicalCounts",
                column: "CountDate");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_CountedById",
                table: "PhysicalCounts",
                column: "CountedById");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_CountNumber",
                table: "PhysicalCounts",
                column: "CountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_InitiatedById",
                table: "PhysicalCounts",
                column: "InitiatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_LocationId",
                table: "PhysicalCounts",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_PostedById",
                table: "PhysicalCounts",
                column: "PostedById");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_Status",
                table: "PhysicalCounts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_TenantId",
                table: "PhysicalCounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_WarehouseId",
                table: "PhysicalCounts",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_GoodsReceiptNoteItemId",
                table: "PurchaseReturnItems",
                column: "GoodsReceiptNoteItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_InventoryItemId",
                table: "PurchaseReturnItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_LocationId",
                table: "PurchaseReturnItems",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_PurchaseReturnId",
                table: "PurchaseReturnItems",
                column: "PurchaseReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_TenantId",
                table: "PurchaseReturnItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_ApprovedById",
                table: "PurchaseReturns",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_GoodsReceiptNoteId",
                table: "PurchaseReturns",
                column: "GoodsReceiptNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_RequestedById",
                table: "PurchaseReturns",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_ReturnDate",
                table: "PurchaseReturns",
                column: "ReturnDate");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_ReturnNumber",
                table: "PurchaseReturns",
                column: "ReturnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_Status",
                table: "PurchaseReturns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_SupplierId",
                table: "PurchaseReturns",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_TenantId",
                table: "PurchaseReturns",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_WarehouseId",
                table: "PurchaseReturns",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureConversions_FromUnitId_ToUnitId",
                table: "UnitOfMeasureConversions",
                columns: new[] { "FromUnitId", "ToUnitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureConversions_IsActive",
                table: "UnitOfMeasureConversions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureConversions_TenantId",
                table: "UnitOfMeasureConversions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasureConversions_ToUnitId",
                table: "UnitOfMeasureConversions",
                column: "ToUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_Category",
                table: "UnitsOfMeasure",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_Code",
                table: "UnitsOfMeasure",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_IsActive",
                table: "UnitsOfMeasure",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_TenantId",
                table: "UnitsOfMeasure",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_InventoryCategories_SubCategoryId",
                table: "InventoryItems",
                column: "SubCategoryId",
                principalTable: "InventoryCategories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_UnitsOfMeasure_BaseUnitOfMeasureEntityId",
                table: "InventoryItems",
                column: "BaseUnitOfMeasureEntityId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_Warehouses_DefaultWarehouseId",
                table: "InventoryItems",
                column: "DefaultWarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_PurchaseRequisitions_SourceRequisitionId",
                table: "PurchaseOrders",
                column: "SourceRequisitionId",
                principalTable: "PurchaseRequisitions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Users_LastAmendedById",
                table: "PurchaseOrders",
                column: "LastAmendedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitions_Users_CurrentApproverId",
                table: "PurchaseRequisitions",
                column: "CurrentApproverId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitions_Users_LastAmendedById",
                table: "PurchaseRequisitions",
                column: "LastAmendedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseLocations_InventoryItems_DedicatedItemId",
                table: "WarehouseLocations",
                column: "DedicatedItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_Users_ManagerId",
                table: "Warehouses",
                column: "ManagerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_WarehouseLocations_DefaultInTransitLocationId",
                table: "Warehouses",
                column: "DefaultInTransitLocationId",
                principalTable: "WarehouseLocations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_WarehouseLocations_DefaultQuarantineLocationId",
                table: "Warehouses",
                column: "DefaultQuarantineLocationId",
                principalTable: "WarehouseLocations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_WarehouseLocations_DefaultReceivingLocationId",
                table: "Warehouses",
                column: "DefaultReceivingLocationId",
                principalTable: "WarehouseLocations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_WarehouseLocations_DefaultShippingLocationId",
                table: "Warehouses",
                column: "DefaultShippingLocationId",
                principalTable: "WarehouseLocations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_InventoryCategories_SubCategoryId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_UnitsOfMeasure_BaseUnitOfMeasureEntityId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_Warehouses_DefaultWarehouseId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_PurchaseRequisitions_SourceRequisitionId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Users_LastAmendedById",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitions_Users_CurrentApproverId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitions_Users_LastAmendedById",
                table: "PurchaseRequisitions");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseLocations_InventoryItems_DedicatedItemId",
                table: "WarehouseLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_Users_ManagerId",
                table: "Warehouses");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_WarehouseLocations_DefaultInTransitLocationId",
                table: "Warehouses");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_WarehouseLocations_DefaultQuarantineLocationId",
                table: "Warehouses");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_WarehouseLocations_DefaultReceivingLocationId",
                table: "Warehouses");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_WarehouseLocations_DefaultShippingLocationId",
                table: "Warehouses");

            migrationBuilder.DropTable(
                name: "InventoryTransferItems");

            migrationBuilder.DropTable(
                name: "ItemSuppliers");

            migrationBuilder.DropTable(
                name: "ItemUnitsOfMeasure");

            migrationBuilder.DropTable(
                name: "LandedCostAllocations");

            migrationBuilder.DropTable(
                name: "PhysicalCountItems");

            migrationBuilder.DropTable(
                name: "PurchaseReturnItems");

            migrationBuilder.DropTable(
                name: "UnitOfMeasureConversions");

            migrationBuilder.DropTable(
                name: "InventoryTransfers");

            migrationBuilder.DropTable(
                name: "InventoryCostLayers");

            migrationBuilder.DropTable(
                name: "LandedCostItems");

            migrationBuilder.DropTable(
                name: "PhysicalCounts");

            migrationBuilder.DropTable(
                name: "GoodsReceiptNoteItems");

            migrationBuilder.DropTable(
                name: "PurchaseReturns");

            migrationBuilder.DropTable(
                name: "UnitsOfMeasure");

            migrationBuilder.DropTable(
                name: "LandedCosts");

            migrationBuilder.DropTable(
                name: "GoodsReceiptNotes");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_DefaultInTransitLocationId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_DefaultQuarantineLocationId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_DefaultReceivingLocationId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_DefaultShippingLocationId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_ManagerId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseLocations_DedicatedItemId",
                table: "WarehouseLocations");

            migrationBuilder.DropIndex(
                name: "IX_Technicians_EmployeeNumber",
                table: "Technicians");

            migrationBuilder.DropIndex(
                name: "IX_Technicians_IsActive",
                table: "Technicians");

            migrationBuilder.DropIndex(
                name: "IX_Technicians_Specialization",
                table: "Technicians");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_CurrentApproverId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_LastAmendedById",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_LastAmendedById",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_SourceRequisitionId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_BaseUnitOfMeasureEntityId",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_DefaultWarehouseId",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_SubCategoryId",
                table: "InventoryItems");
            migrationBuilder.DropColumn(
                name: "CloseTime",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "CostCenter",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "DefaultInTransitLocationId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "DefaultQuarantineLocationId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "DefaultReceivingLocationId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "DefaultShippingLocationId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "GLAccountCode",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "HasInspectionArea",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "HasQuarantineArea",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "HasReceivingDock",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "HasShippingDock",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "IsTemperatureControlled",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "MaxTemperature",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "MaxWeightCapacity",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "MinTemperature",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "OpenTime",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "OperatingHours",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "TemperatureUnit",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "TotalCapacityCubicFeet",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "TotalCapacitySquareFeet",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "TotalLocations",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "UsedCapacitySquareFeet",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "UsedLocations",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "ABCClass",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "Aisle",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "Bin",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "ColumnNumber",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "DedicatedItemCode",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "DedicatedItemId",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "IsDamageLocation",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "IsInTransitLocation",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "IsInspectionLocation",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "IsQuarantineLocation",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "IsReturnLocation",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "IsShippingLocation",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "IsStagingLocation",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "LevelNumber",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "LocationBarcode",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "LocationHierarchyType",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "PickSequence",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "Rack",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "RowNumber",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "Shelf",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "TemperatureZone",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "Zone",
                table: "WarehouseLocations");

            migrationBuilder.DropColumn(
                name: "AmendmentNotes",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ApprovalHistory",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ApprovalLevel",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "BudgetAllocated",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "BudgetCode",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "BudgetRemaining",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "BudgetValidated",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "CurrentApproverId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "DeliveryAddress",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "DeliveryInstructions",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "DeliveryWarehouseId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "GeneratedFrom",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "IsAutoGenerated",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "LastAmendedAt",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "LastAmendedById",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "PreferredSupplierId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ProjectCode",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ProjectName",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "RequiredApprovalLevel",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "RequisitionType",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "RevisionNumber",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "SourcePlanId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "AutoCloseOnReceipt",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "BudgetCode",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "BudgetValidated",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ContractEndDate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ContractRemainingValue",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ContractStartDate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ContractUsedValue",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ContractValue",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "LastAmendedAt",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "LastAmendedById",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "OrderType",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "RevisionNumber",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SourceRequisitionId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SourceRequisitionNumber",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "TolerancePercent",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "AllowBackorder",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "AlternateBarcode",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "AutoReorder",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "BaseUnitOfMeasureEntityId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "BaseUnitOfMeasureId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "CountryOfOrigin",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "DefaultWarehouseId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "HSCode",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "IsTaxable",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "LongDescription",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "MinimumOrderQuantity",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "MovementClass",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "OrderMultiple",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "PrimarySupplierId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "QRCode",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "RequireApprovalForPurchase",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "ShortDescription",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "SubCategoryId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "TaxCode",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "ThumbnailUrl",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "ValuationMethod",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "XYZClass",
                table: "InventoryItems");

            migrationBuilder.AddColumn<Guid>(
                name: "TechnicianId",
                table: "TechnicianTeams",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TechnicianId1",
                table: "TechnicianSkillAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MaintenanceTypeId1",
                table: "MaintenanceSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EvaluationCriterionId1",
                table: "EvaluationTemplateCriteria",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 906, DateTimeKind.Utc).AddTicks(9930));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 906, DateTimeKind.Utc).AddTicks(9990));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 906, DateTimeKind.Utc).AddTicks(9993));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 906, DateTimeKind.Utc).AddTicks(9995));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(240));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(266));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(273));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(279));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(291));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(299));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(305));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(312));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(322));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(339));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(346));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(353));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(363));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(387));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(399));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(406));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(464));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(466));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(467));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(468));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(469));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(471));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(472));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(472));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(473));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(475));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(476));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(477));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(477));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(478));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(479));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(480));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(540));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(542));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(543));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(544));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(545));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(546));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(547));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(548));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(548));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(549));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(550));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(551));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(552));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(553));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(554));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(625));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(626));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(628));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(629));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(630));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(630));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(631));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(632));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(633));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(634));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(654));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(656));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 907, DateTimeKind.Utc).AddTicks(656));
            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 22, 19, 26, 906, DateTimeKind.Utc).AddTicks(9614));

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianTeams_TechnicianId",
                table: "TechnicianTeams",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSkillAssignments_TechnicianId1",
                table: "TechnicianSkillAssignments",
                column: "TechnicianId1");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_MaintenanceTypeId1",
                table: "MaintenanceSchedules",
                column: "MaintenanceTypeId1");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationTemplateCriteria_EvaluationCriterionId1",
                table: "EvaluationTemplateCriteria",
                column: "EvaluationCriterionId1");

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationTemplateCriteria_EvaluationCriteria_EvaluationCriterionId1",
                table: "EvaluationTemplateCriteria",
                column: "EvaluationCriterionId1",
                principalTable: "EvaluationCriteria",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceSchedules_MaintenanceTypes_MaintenanceTypeId1",
                table: "MaintenanceSchedules",
                column: "MaintenanceTypeId1",
                principalTable: "MaintenanceTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TechnicianSkillAssignments_Technicians_TechnicianId1",
                table: "TechnicianSkillAssignments",
                column: "TechnicianId1",
                principalTable: "Technicians",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TechnicianTeams_Technicians_TechnicianId",
                table: "TechnicianTeams",
                column: "TechnicianId",
                principalTable: "Technicians",
                principalColumn: "Id");
        }
    }
}
