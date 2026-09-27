using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class InventoryControlledWorkflowsAndAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS(SELECT 1 FROM dbo.SalesOrders WHERE InvoiceId IS NOT NULL
                    GROUP BY TenantId,InvoiceId HAVING COUNT_BIG(*)>1)
                    THROW 51996,'SALES_INVOICE_LEGACY_LINK_RECONCILIATION: Multiple historical Sales orders reference the same invoice. Reconcile the links before applying this migration; no history is rewritten.',1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_VendorInvoiceReceiptAllocations_TenantId_VendorInvoiceLineItemId",
                table: "VendorInvoiceReceiptAllocations");

            migrationBuilder.DropIndex(
                name: "UX_SupplierDebitNotes_Tenant_InventoryReturn",
                table: "SupplierDebitNotes");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_TenantId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_TenantId",
                table: "SalesOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalCountActions_ActionType",
                table: "PhysicalCountActions");

            migrationBuilder.AddColumn<Guid>(
                name: "InventorySupplierReturnAccountingGroupId",
                table: "SupplierDebitNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransferDispatchAllocationId",
                table: "StockMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferLeg",
                table: "StockMovements",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransferReceiptAllocationId",
                table: "StockMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceEconomicsJson",
                table: "SalesOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceGeneratedById",
                table: "SalesOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceGenerationHash",
                table: "SalesOrders",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceGenerationKey",
                table: "SalesOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceSourceJson",
                table: "SalesOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SalesOrders",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "AccountingAllocationVersion",
                table: "PurchaseReturns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PurchasePriceDifferenceHandling",
                table: "ProcurementSettings",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ObservationSubmittedAtUtc",
                table: "PhysicalCounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentPhysicalCountId",
                table: "PhysicalCounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecountAttempt",
                table: "PhysicalCounts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RecountRequestHash",
                table: "PhysicalCounts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecountRequestKey",
                table: "PhysicalCounts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RootPhysicalCountId",
                table: "PhysicalCounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefectiveNotes",
                table: "PhysicalCountItems",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefectiveQuantity",
                table: "PhysicalCountItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "PredecessorPhysicalCountItemId",
                table: "PhysicalCountItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecountReason",
                table: "PhysicalCountItems",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RootPhysicalCountItemId",
                table: "PhysicalCountItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersededByPhysicalCountId",
                table: "PhysicalCountItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CarrierBusinessPartnerId",
                table: "InventoryTransfers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransferDispatchAllocationId",
                table: "InventoryMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferLeg",
                table: "InventoryMovements",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransferReceiptAllocationId",
                table: "InventoryMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryDisposalAccountId",
                table: "InventoryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountingVersion",
                table: "InventoryDisposalCases",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PreparedStockAdjustmentId",
                table: "InventoryDisposalCases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InventoryDisposalAuctionInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryDisposalCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InvoiceEconomicsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_InventoryDisposalAuctionInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryDisposalAuctionInvoices_InventoryDisposalCases_InventoryDisposalCaseId",
                        column: x => x.InventoryDisposalCaseId,
                        principalTable: "InventoryDisposalCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDisposalAuctionInvoices_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDisposalAuctionInvoices_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventorySupplierReturnAccountingGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryPurchaseReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalVendorInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DispatchPostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DispatchJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClearingAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CarryingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OriginalAccrualAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FunctionalCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_InventorySupplierReturnAccountingGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAccountingGroups_Accounts_ClearingAccountId",
                        column: x => x.ClearingAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAccountingGroups_FinancePostingEvents_DispatchPostingEventId",
                        column: x => x.DispatchPostingEventId,
                        principalTable: "FinancePostingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAccountingGroups_JournalEntries_DispatchJournalEntryId",
                        column: x => x.DispatchJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAccountingGroups_PurchaseReturns_InventoryPurchaseReturnId",
                        column: x => x.InventoryPurchaseReturnId,
                        principalTable: "PurchaseReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAccountingGroups_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAccountingGroups_VendorInvoice_OriginalVendorInvoiceId",
                        column: x => x.OriginalVendorInvoiceId,
                        principalTable: "VendorInvoice",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransferDispatchAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryTransferActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryTransferItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceInventoryWarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InTransitLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CarrierBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CarrierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CarrierAccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VehicleNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TrackingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TrackingSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_InventoryTransferDispatchAllocations", x => x.Id);
                    table.CheckConstraint("CK_InventoryTransferDispatchAllocations_Quantity", "[Quantity]>0");
                    table.ForeignKey(
                        name: "FK_InventoryTransferDispatchAllocations_BusinessPartners_CarrierBusinessPartnerId",
                        column: x => x.CarrierBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDispatchAllocations_InventoryTransferActions_InventoryTransferActionId",
                        column: x => x.InventoryTransferActionId,
                        principalTable: "InventoryTransferActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDispatchAllocations_InventoryTransferItems_InventoryTransferItemId",
                        column: x => x.InventoryTransferItemId,
                        principalTable: "InventoryTransferItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDispatchAllocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDispatchAllocations_WarehouseLocations_InTransitLocationId",
                        column: x => x.InTransitLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDispatchAllocations_WarehouseLocations_SourceLocationId",
                        column: x => x.SourceLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDispatchAllocations_Warehouses_SourceInventoryWarehouseId",
                        column: x => x.SourceInventoryWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalCountAdjustmentClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RootPhysicalCountItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhysicalCountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhysicalCountItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockAdjustmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StockAdjustmentItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaimedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CountedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    VarianceQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_PhysicalCountAdjustmentClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhysicalCountAdjustmentClaims_PhysicalCountItems_PhysicalCountItemId",
                        column: x => x.PhysicalCountItemId,
                        principalTable: "PhysicalCountItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalCountAdjustmentClaims_PhysicalCountItems_RootPhysicalCountItemId",
                        column: x => x.RootPhysicalCountItemId,
                        principalTable: "PhysicalCountItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalCountAdjustmentClaims_PhysicalCounts_PhysicalCountId",
                        column: x => x.PhysicalCountId,
                        principalTable: "PhysicalCounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalCountAdjustmentClaims_StockAdjustmentItems_StockAdjustmentItemId",
                        column: x => x.StockAdjustmentItemId,
                        principalTable: "StockAdjustmentItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalCountAdjustmentClaims_StockAdjustments_StockAdjustmentId",
                        column: x => x.StockAdjustmentId,
                        principalTable: "StockAdjustments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalCountAdjustmentClaims_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCountAdjustmentClaims_Users_ClaimedById",
                        column: x => x.ClaimedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PhysicalCountCounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhysicalCountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    EmailAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AssignedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RemovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InAppNotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmailNotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_PhysicalCountCounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhysicalCountCounters_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCountCounters_Notifications_EmailNotificationId",
                        column: x => x.EmailNotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCountCounters_Notifications_InAppNotificationId",
                        column: x => x.InAppNotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCountCounters_PhysicalCounts_PhysicalCountId",
                        column: x => x.PhysicalCountId,
                        principalTable: "PhysicalCounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCountCounters_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCountCounters_Users_AssignedById",
                        column: x => x.AssignedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCountCounters_Users_RemovedById",
                        column: x => x.RemovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCountCounters_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementReceiptCostBases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    PurchaseOrderReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderReceiptItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ConversionToBase = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    PurchaseCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    FunctionalCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExchangeRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExchangeRateToFunctional = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    ExchangeRateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PurchaseUnitCost = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    PurchaseAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FunctionalAccrualAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FunctionalInventoryAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementReceiptCostBases", x => x.Id);
                    table.CheckConstraint("CK_ProcurementReceiptCostBases_Quantity", "[PurchaseQuantity]>0 AND [BaseQuantity]>0 AND [ConversionToBase]>0 AND [ExchangeRateToFunctional]>0 AND [PurchaseAmount]>=0 AND [FunctionalAccrualAmount]>=0 AND [FunctionalInventoryAmount]>=0");
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptCostBases_ExchangeRates_ExchangeRateId",
                        column: x => x.ExchangeRateId,
                        principalTable: "ExchangeRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptCostBases_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptCostBases_InventoryMovements_InventoryMovementId",
                        column: x => x.InventoryMovementId,
                        principalTable: "InventoryMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptCostBases_PurchaseOrderItems_PurchaseOrderItemId",
                        column: x => x.PurchaseOrderItemId,
                        principalTable: "PurchaseOrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptCostBases_PurchaseOrderReceiptItems_PurchaseOrderReceiptItemId",
                        column: x => x.PurchaseOrderReceiptItemId,
                        principalTable: "PurchaseOrderReceiptItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptCostBases_PurchaseOrderReceipts_PurchaseOrderReceiptId",
                        column: x => x.PurchaseOrderReceiptId,
                        principalTable: "PurchaseOrderReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptCostBases_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptCostBases_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptCostBases_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransferReceiptAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackingSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InventoryTransferActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DispatchAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinationLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinationInventoryWarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReturnedToSource = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_InventoryTransferReceiptAllocations", x => x.Id);
                    table.CheckConstraint("CK_InventoryTransferReceiptAllocations_Quantity", "[Quantity]>0");
                    table.ForeignKey(
                        name: "FK_InventoryTransferReceiptAllocations_InventoryTransferActions_InventoryTransferActionId",
                        column: x => x.InventoryTransferActionId,
                        principalTable: "InventoryTransferActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferReceiptAllocations_InventoryTransferDispatchAllocations_DispatchAllocationId",
                        column: x => x.DispatchAllocationId,
                        principalTable: "InventoryTransferDispatchAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferReceiptAllocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferReceiptAllocations_WarehouseLocations_DestinationLocationId",
                        column: x => x.DestinationLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferReceiptAllocations_Warehouses_DestinationInventoryWarehouseId",
                        column: x => x.DestinationInventoryWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventorySupplierReturnAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryPurchaseReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryPurchaseReturnItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoodsReceiptNoteItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderReceiptItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcurementReceiptCostBasisId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccountingGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorInvoiceReceiptAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalVendorInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalVendorInvoiceLineItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalReceiptJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PurchaseQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ConversionToBase = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    OriginalAccrualAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OriginalAccrualForeignAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PurchaseCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CarryingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FunctionalCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_InventorySupplierReturnAllocations", x => x.Id);
                    table.CheckConstraint("CK_InventorySupplierReturnAllocations_Quantity", "[BaseQuantity] > 0 AND [PurchaseQuantity] > 0 AND [ConversionToBase] > 0");
                    table.CheckConstraint("CK_InventorySupplierReturnAllocations_Stage", "([VendorInvoiceReceiptAllocationId] IS NULL AND [OriginalVendorInvoiceId] IS NULL AND [OriginalVendorInvoiceLineItemId] IS NULL) OR ([VendorInvoiceReceiptAllocationId] IS NOT NULL AND [OriginalVendorInvoiceId] IS NOT NULL AND [OriginalVendorInvoiceLineItemId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAllocations_GoodsReceiptNoteItems_GoodsReceiptNoteItemId",
                        column: x => x.GoodsReceiptNoteItemId,
                        principalTable: "GoodsReceiptNoteItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAllocations_InventorySupplierReturnAccountingGroups_AccountingGroupId",
                        column: x => x.AccountingGroupId,
                        principalTable: "InventorySupplierReturnAccountingGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAllocations_JournalEntries_OriginalReceiptJournalEntryId",
                        column: x => x.OriginalReceiptJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAllocations_ProcurementReceiptCostBases_ProcurementReceiptCostBasisId",
                        column: x => x.ProcurementReceiptCostBasisId,
                        principalTable: "ProcurementReceiptCostBases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAllocations_PurchaseOrderReceiptItems_PurchaseOrderReceiptItemId",
                        column: x => x.PurchaseOrderReceiptItemId,
                        principalTable: "PurchaseOrderReceiptItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAllocations_PurchaseReturnItems_InventoryPurchaseReturnItemId",
                        column: x => x.InventoryPurchaseReturnItemId,
                        principalTable: "PurchaseReturnItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAllocations_PurchaseReturns_InventoryPurchaseReturnId",
                        column: x => x.InventoryPurchaseReturnId,
                        principalTable: "PurchaseReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAllocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAllocations_VendorInvoiceLineItem_OriginalVendorInvoiceLineItemId",
                        column: x => x.OriginalVendorInvoiceLineItemId,
                        principalTable: "VendorInvoiceLineItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAllocations_VendorInvoiceReceiptAllocations_VendorInvoiceReceiptAllocationId",
                        column: x => x.VendorInvoiceReceiptAllocationId,
                        principalTable: "VendorInvoiceReceiptAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAllocations_VendorInvoice_OriginalVendorInvoiceId",
                        column: x => x.OriginalVendorInvoiceId,
                        principalTable: "VendorInvoice",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VendorInvoiceReceiptCostAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorInvoiceLineItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorInvoiceReceiptAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcurementReceiptCostBasisId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GoodsReceiptNoteItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountingBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReceiptForeignAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReceiptFunctionalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InvoiceNetForeignAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InvoiceFunctionalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PriceDifferenceFunctionalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExchangeDifferenceFunctionalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InventoryAdjustmentAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RevaluedReceiptBaseQuantity = table.Column<decimal>(type: "decimal(28,12)", nullable: false),
                    PurchasePriceVarianceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Policy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PurchaseCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    FunctionalCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    InvoiceExchangeRateToFunctional = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    PurchasePriceVarianceAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReversalPostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalReclassificationPostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalReclassificationJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_VendorInvoiceReceiptCostAllocations", x => x.Id);
                    table.CheckConstraint("CK_VendorInvoiceReceiptCostAllocations_Conservation", "[PurchaseQuantity]>0 AND [BaseQuantity]>0 AND [ReceiptFunctionalAmount]+[ExchangeDifferenceFunctionalAmount]+[InventoryAdjustmentAmount]+[PurchasePriceVarianceAmount]=[InvoiceFunctionalAmount] AND [InventoryAdjustmentAmount]+[PurchasePriceVarianceAmount]=[PriceDifferenceFunctionalAmount]");
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostAllocations_AccountingBooks_AccountingBookId",
                        column: x => x.AccountingBookId,
                        principalTable: "AccountingBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostAllocations_Accounts_PurchasePriceVarianceAccountId",
                        column: x => x.PurchasePriceVarianceAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostAllocations_FinancePostingEvents_PostingEventId",
                        column: x => x.PostingEventId,
                        principalTable: "FinancePostingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostAllocations_FinancePostingEvents_ReversalPostingEventId",
                        column: x => x.ReversalPostingEventId,
                        principalTable: "FinancePostingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostAllocations_FinancePostingEvents_ReversalReclassificationPostingEventId",
                        column: x => x.ReversalReclassificationPostingEventId,
                        principalTable: "FinancePostingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostAllocations_GoodsReceiptNoteItems_GoodsReceiptNoteItemId",
                        column: x => x.GoodsReceiptNoteItemId,
                        principalTable: "GoodsReceiptNoteItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostAllocations_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostAllocations_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostAllocations_JournalEntries_ReceiptJournalEntryId",
                        column: x => x.ReceiptJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostAllocations_JournalEntries_ReversalJournalEntryId",
                        column: x => x.ReversalJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostAllocations_JournalEntries_ReversalReclassificationJournalEntryId",
                        column: x => x.ReversalReclassificationJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostAllocations_ProcurementReceiptCostBases_ProcurementReceiptCostBasisId",
                        column: x => x.ProcurementReceiptCostBasisId,
                        principalTable: "ProcurementReceiptCostBases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostAllocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostAllocations_VendorInvoiceLineItem_VendorInvoiceLineItemId",
                        column: x => x.VendorInvoiceLineItemId,
                        principalTable: "VendorInvoiceLineItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostAllocations_VendorInvoiceReceiptAllocations_VendorInvoiceReceiptAllocationId",
                        column: x => x.VendorInvoiceReceiptAllocationId,
                        principalTable: "VendorInvoiceReceiptAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostAllocations_VendorInvoice_VendorInvoiceId",
                        column: x => x.VendorInvoiceId,
                        principalTable: "VendorInvoice",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventorySupplierReturnAccrualShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventorySupplierReturnAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalReceiptAccountTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_InventorySupplierReturnAccrualShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAccrualShares_AccountTransactions_OriginalReceiptAccountTransactionId",
                        column: x => x.OriginalReceiptAccountTransactionId,
                        principalTable: "AccountTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAccrualShares_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAccrualShares_InventorySupplierReturnAllocations_InventorySupplierReturnAllocationId",
                        column: x => x.InventorySupplierReturnAllocationId,
                        principalTable: "InventorySupplierReturnAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventorySupplierReturnAccrualShares_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VendorInvoiceReceiptCostPostingLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CostAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalReceiptAccountTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ForeignAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FunctionalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_VendorInvoiceReceiptCostPostingLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostPostingLines_AccountTransactions_OriginalReceiptAccountTransactionId",
                        column: x => x.OriginalReceiptAccountTransactionId,
                        principalTable: "AccountTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostPostingLines_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostPostingLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostPostingLines_VendorInvoiceReceiptCostAllocations_CostAllocationId",
                        column: x => x.CostAllocationId,
                        principalTable: "VendorInvoiceReceiptCostAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VendorInvoiceReceiptCostValuations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CostAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryLayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InventoryMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttributedReceiptBaseQuantity = table.Column<decimal>(type: "decimal(28,12)", nullable: false),
                    ValueChange = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReversesValuationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsReversal = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_VendorInvoiceReceiptCostValuations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostValuations_InventoryLayers_InventoryLayerId",
                        column: x => x.InventoryLayerId,
                        principalTable: "InventoryLayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostValuations_InventoryMovements_InventoryMovementId",
                        column: x => x.InventoryMovementId,
                        principalTable: "InventoryMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostValuations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostValuations_VendorInvoiceReceiptCostAllocations_CostAllocationId",
                        column: x => x.CostAllocationId,
                        principalTable: "VendorInvoiceReceiptCostAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostValuations_VendorInvoiceReceiptCostValuations_ReversesValuationId",
                        column: x => x.ReversesValuationId,
                        principalTable: "VendorInvoiceReceiptCostValuations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostValuations_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceReceiptCostValuations_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptAllocations_TenantId_VendorInvoiceLineItemId_GoodsReceiptNoteItemId",
                table: "VendorInvoiceReceiptAllocations",
                columns: new[] { "TenantId", "VendorInvoiceLineItemId", "GoodsReceiptNoteItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDebitNotes_InventorySupplierReturnAccountingGroupId",
                table: "SupplierDebitNotes",
                column: "InventorySupplierReturnAccountingGroupId");

            migrationBuilder.CreateIndex(
                name: "UX_SupplierDebitNotes_Tenant_InventoryReturn",
                table: "SupplierDebitNotes",
                columns: new[] { "TenantId", "InventoryPurchaseReturnId" },
                unique: true,
                filter: "[InventoryPurchaseReturnId] IS NOT NULL AND [InventorySupplierReturnAccountingGroupId] IS NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_SupplierDebitNotes_Tenant_InventoryReturnGroup",
                table: "SupplierDebitNotes",
                columns: new[] { "TenantId", "InventorySupplierReturnAccountingGroupId" },
                unique: true,
                filter: "[InventorySupplierReturnAccountingGroupId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_TenantId_TransferDispatchAllocationId_TransferLeg",
                table: "StockMovements",
                columns: new[] { "TenantId", "TransferDispatchAllocationId", "TransferLeg" },
                unique: true,
                filter: "[TransferDispatchAllocationId] IS NOT NULL AND [TransferReceiptAllocationId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_TenantId_TransferReceiptAllocationId_TransferLeg",
                table: "StockMovements",
                columns: new[] { "TenantId", "TransferReceiptAllocationId", "TransferLeg" },
                unique: true,
                filter: "[TransferReceiptAllocationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_TransferDispatchAllocationId",
                table: "StockMovements",
                column: "TransferDispatchAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_TransferReceiptAllocationId",
                table: "StockMovements",
                column: "TransferReceiptAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_TenantId_InvoiceId",
                table: "SalesOrders",
                columns: new[] { "TenantId", "InvoiceId" },
                unique: true,
                filter: "[InvoiceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_ParentPhysicalCountId",
                table: "PhysicalCounts",
                column: "ParentPhysicalCountId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_RootPhysicalCountId",
                table: "PhysicalCounts",
                column: "RootPhysicalCountId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_TenantId_ParentPhysicalCountId_RecountRequestKey",
                table: "PhysicalCounts",
                columns: new[] { "TenantId", "ParentPhysicalCountId", "RecountRequestKey" },
                unique: true,
                filter: "[ParentPhysicalCountId] IS NOT NULL AND [RecountRequestKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_TenantId_RootPhysicalCountId_RecountAttempt",
                table: "PhysicalCounts",
                columns: new[] { "TenantId", "RootPhysicalCountId", "RecountAttempt" },
                unique: true,
                filter: "[RootPhysicalCountId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountItems_PredecessorPhysicalCountItemId",
                table: "PhysicalCountItems",
                column: "PredecessorPhysicalCountItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountItems_RootPhysicalCountItemId",
                table: "PhysicalCountItems",
                column: "RootPhysicalCountItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountItems_SupersededByPhysicalCountId",
                table: "PhysicalCountItems",
                column: "SupersededByPhysicalCountId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalCountItems_DefectiveQuantity",
                table: "PhysicalCountItems",
                sql: "[DefectiveQuantity] >= 0 AND [DefectiveQuantity] <= [CountedQuantity]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalCountActions_ActionType",
                table: "PhysicalCountActions",
                sql: "[ActionType] BETWEEN 1 AND 17");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransfers_CarrierBusinessPartnerId",
                table: "InventoryTransfers",
                column: "CarrierBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_TenantId_TransferDispatchAllocationId_TransferLeg",
                table: "InventoryMovements",
                columns: new[] { "TenantId", "TransferDispatchAllocationId", "TransferLeg" },
                unique: true,
                filter: "[TransferDispatchAllocationId] IS NOT NULL AND [TransferReceiptAllocationId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_TenantId_TransferReceiptAllocationId_TransferLeg",
                table: "InventoryMovements",
                columns: new[] { "TenantId", "TransferReceiptAllocationId", "TransferLeg" },
                unique: true,
                filter: "[TransferReceiptAllocationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_TransferDispatchAllocationId",
                table: "InventoryMovements",
                column: "TransferDispatchAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_TransferReceiptAllocationId",
                table: "InventoryMovements",
                column: "TransferReceiptAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_InventoryDisposalAccountId",
                table: "InventoryItems",
                column: "InventoryDisposalAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalCases_PreparedStockAdjustmentId",
                table: "InventoryDisposalCases",
                column: "PreparedStockAdjustmentId",
                unique: true,
                filter: "[PreparedStockAdjustmentId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryDisposalCases_AccountingVersion",
                table: "InventoryDisposalCases",
                sql: "[AccountingVersion] IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalAuctionInvoices_InventoryDisposalCaseId",
                table: "InventoryDisposalAuctionInvoices",
                column: "InventoryDisposalCaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalAuctionInvoices_InvoiceId",
                table: "InventoryDisposalAuctionInvoices",
                column: "InvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalAuctionInvoices_TenantId_IdempotencyKey",
                table: "InventoryDisposalAuctionInvoices",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAccountingGroups_ClearingAccountId",
                table: "InventorySupplierReturnAccountingGroups",
                column: "ClearingAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAccountingGroups_DispatchJournalEntryId",
                table: "InventorySupplierReturnAccountingGroups",
                column: "DispatchJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAccountingGroups_DispatchPostingEventId",
                table: "InventorySupplierReturnAccountingGroups",
                column: "DispatchPostingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAccountingGroups_InventoryPurchaseReturnId",
                table: "InventorySupplierReturnAccountingGroups",
                column: "InventoryPurchaseReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAccountingGroups_OriginalVendorInvoiceId",
                table: "InventorySupplierReturnAccountingGroups",
                column: "OriginalVendorInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAccountingGroups_TenantId_InventoryPurchaseReturnId_OriginalVendorInvoiceId",
                table: "InventorySupplierReturnAccountingGroups",
                columns: new[] { "TenantId", "InventoryPurchaseReturnId", "OriginalVendorInvoiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAccrualShares_AccountId",
                table: "InventorySupplierReturnAccrualShares",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAccrualShares_InventorySupplierReturnAllocationId",
                table: "InventorySupplierReturnAccrualShares",
                column: "InventorySupplierReturnAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAccrualShares_OriginalReceiptAccountTransactionId",
                table: "InventorySupplierReturnAccrualShares",
                column: "OriginalReceiptAccountTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAccrualShares_TenantId_InventorySupplierReturnAllocationId_OriginalReceiptAccountTransactionId",
                table: "InventorySupplierReturnAccrualShares",
                columns: new[] { "TenantId", "InventorySupplierReturnAllocationId", "OriginalReceiptAccountTransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAllocations_AccountingGroupId",
                table: "InventorySupplierReturnAllocations",
                column: "AccountingGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAllocations_GoodsReceiptNoteItemId",
                table: "InventorySupplierReturnAllocations",
                column: "GoodsReceiptNoteItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAllocations_InventoryPurchaseReturnId",
                table: "InventorySupplierReturnAllocations",
                column: "InventoryPurchaseReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAllocations_InventoryPurchaseReturnItemId",
                table: "InventorySupplierReturnAllocations",
                column: "InventoryPurchaseReturnItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAllocations_OriginalReceiptJournalEntryId",
                table: "InventorySupplierReturnAllocations",
                column: "OriginalReceiptJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAllocations_OriginalVendorInvoiceId",
                table: "InventorySupplierReturnAllocations",
                column: "OriginalVendorInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAllocations_OriginalVendorInvoiceLineItemId",
                table: "InventorySupplierReturnAllocations",
                column: "OriginalVendorInvoiceLineItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAllocations_ProcurementReceiptCostBasisId",
                table: "InventorySupplierReturnAllocations",
                column: "ProcurementReceiptCostBasisId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAllocations_PurchaseOrderReceiptItemId",
                table: "InventorySupplierReturnAllocations",
                column: "PurchaseOrderReceiptItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAllocations_TenantId_InventoryPurchaseReturnItemId_VendorInvoiceReceiptAllocationId",
                table: "InventorySupplierReturnAllocations",
                columns: new[] { "TenantId", "InventoryPurchaseReturnItemId", "VendorInvoiceReceiptAllocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventorySupplierReturnAllocations_VendorInvoiceReceiptAllocationId",
                table: "InventorySupplierReturnAllocations",
                column: "VendorInvoiceReceiptAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDispatchAllocations_CarrierBusinessPartnerId",
                table: "InventoryTransferDispatchAllocations",
                column: "CarrierBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDispatchAllocations_InTransitLocationId",
                table: "InventoryTransferDispatchAllocations",
                column: "InTransitLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDispatchAllocations_InventoryTransferActionId",
                table: "InventoryTransferDispatchAllocations",
                column: "InventoryTransferActionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDispatchAllocations_InventoryTransferItemId",
                table: "InventoryTransferDispatchAllocations",
                column: "InventoryTransferItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDispatchAllocations_SourceInventoryWarehouseId",
                table: "InventoryTransferDispatchAllocations",
                column: "SourceInventoryWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDispatchAllocations_SourceLocationId",
                table: "InventoryTransferDispatchAllocations",
                column: "SourceLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDispatchAllocations_TenantId_InventoryTransferActionId_InventoryTransferItemId_SourceLocationId",
                table: "InventoryTransferDispatchAllocations",
                columns: new[] { "TenantId", "InventoryTransferActionId", "InventoryTransferItemId", "SourceLocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferReceiptAllocations_DestinationInventoryWarehouseId",
                table: "InventoryTransferReceiptAllocations",
                column: "DestinationInventoryWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferReceiptAllocations_DestinationLocationId",
                table: "InventoryTransferReceiptAllocations",
                column: "DestinationLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferReceiptAllocations_DispatchAllocationId",
                table: "InventoryTransferReceiptAllocations",
                column: "DispatchAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferReceiptAllocations_InventoryTransferActionId",
                table: "InventoryTransferReceiptAllocations",
                column: "InventoryTransferActionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferReceiptAllocations_TenantId_InventoryTransferActionId_DispatchAllocationId_DestinationLocationId",
                table: "InventoryTransferReceiptAllocations",
                columns: new[] { "TenantId", "InventoryTransferActionId", "DispatchAllocationId", "DestinationLocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountAdjustmentClaims_ClaimedById",
                table: "PhysicalCountAdjustmentClaims",
                column: "ClaimedById");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountAdjustmentClaims_PhysicalCountId",
                table: "PhysicalCountAdjustmentClaims",
                column: "PhysicalCountId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountAdjustmentClaims_PhysicalCountItemId",
                table: "PhysicalCountAdjustmentClaims",
                column: "PhysicalCountItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountAdjustmentClaims_RootPhysicalCountItemId",
                table: "PhysicalCountAdjustmentClaims",
                column: "RootPhysicalCountItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountAdjustmentClaims_StockAdjustmentId",
                table: "PhysicalCountAdjustmentClaims",
                column: "StockAdjustmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountAdjustmentClaims_StockAdjustmentItemId",
                table: "PhysicalCountAdjustmentClaims",
                column: "StockAdjustmentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountAdjustmentClaims_TenantId_RootPhysicalCountItemId",
                table: "PhysicalCountAdjustmentClaims",
                columns: new[] { "TenantId", "RootPhysicalCountItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountCounters_AssignedById",
                table: "PhysicalCountCounters",
                column: "AssignedById");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountCounters_EmailNotificationId",
                table: "PhysicalCountCounters",
                column: "EmailNotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountCounters_EmployeeId",
                table: "PhysicalCountCounters",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountCounters_InAppNotificationId",
                table: "PhysicalCountCounters",
                column: "InAppNotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountCounters_PhysicalCountId",
                table: "PhysicalCountCounters",
                column: "PhysicalCountId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountCounters_RemovedById",
                table: "PhysicalCountCounters",
                column: "RemovedById");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountCounters_TenantId_PhysicalCountId_EmployeeId",
                table: "PhysicalCountCounters",
                columns: new[] { "TenantId", "PhysicalCountId", "EmployeeId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountCounters_TenantId_PhysicalCountId_UserId",
                table: "PhysicalCountCounters",
                columns: new[] { "TenantId", "PhysicalCountId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountCounters_UserId",
                table: "PhysicalCountCounters",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptCostBases_ExchangeRateId",
                table: "ProcurementReceiptCostBases",
                column: "ExchangeRateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptCostBases_InventoryItemId",
                table: "ProcurementReceiptCostBases",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptCostBases_InventoryMovementId",
                table: "ProcurementReceiptCostBases",
                column: "InventoryMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptCostBases_LocationId",
                table: "ProcurementReceiptCostBases",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptCostBases_PurchaseOrderItemId",
                table: "ProcurementReceiptCostBases",
                column: "PurchaseOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptCostBases_PurchaseOrderReceiptId",
                table: "ProcurementReceiptCostBases",
                column: "PurchaseOrderReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptCostBases_PurchaseOrderReceiptItemId",
                table: "ProcurementReceiptCostBases",
                column: "PurchaseOrderReceiptItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptCostBases_TenantId_InventoryMovementId",
                table: "ProcurementReceiptCostBases",
                columns: new[] { "TenantId", "InventoryMovementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptCostBases_TenantId_PurchaseOrderReceiptItemId",
                table: "ProcurementReceiptCostBases",
                columns: new[] { "TenantId", "PurchaseOrderReceiptItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptCostBases_WarehouseId",
                table: "ProcurementReceiptCostBases",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostAllocations_AccountingBookId",
                table: "VendorInvoiceReceiptCostAllocations",
                column: "AccountingBookId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostAllocations_GoodsReceiptNoteItemId",
                table: "VendorInvoiceReceiptCostAllocations",
                column: "GoodsReceiptNoteItemId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostAllocations_InventoryItemId",
                table: "VendorInvoiceReceiptCostAllocations",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostAllocations_JournalEntryId",
                table: "VendorInvoiceReceiptCostAllocations",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostAllocations_PostingEventId",
                table: "VendorInvoiceReceiptCostAllocations",
                column: "PostingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostAllocations_ProcurementReceiptCostBasisId",
                table: "VendorInvoiceReceiptCostAllocations",
                column: "ProcurementReceiptCostBasisId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostAllocations_PurchasePriceVarianceAccountId",
                table: "VendorInvoiceReceiptCostAllocations",
                column: "PurchasePriceVarianceAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostAllocations_ReceiptJournalEntryId",
                table: "VendorInvoiceReceiptCostAllocations",
                column: "ReceiptJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostAllocations_ReversalJournalEntryId",
                table: "VendorInvoiceReceiptCostAllocations",
                column: "ReversalJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostAllocations_ReversalPostingEventId",
                table: "VendorInvoiceReceiptCostAllocations",
                column: "ReversalPostingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostAllocations_ReversalReclassificationJournalEntryId",
                table: "VendorInvoiceReceiptCostAllocations",
                column: "ReversalReclassificationJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostAllocations_ReversalReclassificationPostingEventId",
                table: "VendorInvoiceReceiptCostAllocations",
                column: "ReversalReclassificationPostingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostAllocations_TenantId_GoodsReceiptNoteItemId",
                table: "VendorInvoiceReceiptCostAllocations",
                columns: new[] { "TenantId", "GoodsReceiptNoteItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostAllocations_TenantId_VendorInvoiceReceiptAllocationId",
                table: "VendorInvoiceReceiptCostAllocations",
                columns: new[] { "TenantId", "VendorInvoiceReceiptAllocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostAllocations_VendorInvoiceId",
                table: "VendorInvoiceReceiptCostAllocations",
                column: "VendorInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostAllocations_VendorInvoiceLineItemId",
                table: "VendorInvoiceReceiptCostAllocations",
                column: "VendorInvoiceLineItemId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostAllocations_VendorInvoiceReceiptAllocationId",
                table: "VendorInvoiceReceiptCostAllocations",
                column: "VendorInvoiceReceiptAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostPostingLines_AccountId",
                table: "VendorInvoiceReceiptCostPostingLines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostPostingLines_CostAllocationId",
                table: "VendorInvoiceReceiptCostPostingLines",
                column: "CostAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostPostingLines_OriginalReceiptAccountTransactionId",
                table: "VendorInvoiceReceiptCostPostingLines",
                column: "OriginalReceiptAccountTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostPostingLines_TenantId",
                table: "VendorInvoiceReceiptCostPostingLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostValuations_CostAllocationId",
                table: "VendorInvoiceReceiptCostValuations",
                column: "CostAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostValuations_InventoryLayerId",
                table: "VendorInvoiceReceiptCostValuations",
                column: "InventoryLayerId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostValuations_InventoryMovementId",
                table: "VendorInvoiceReceiptCostValuations",
                column: "InventoryMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostValuations_LocationId",
                table: "VendorInvoiceReceiptCostValuations",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostValuations_ReversesValuationId",
                table: "VendorInvoiceReceiptCostValuations",
                column: "ReversesValuationId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostValuations_TenantId_InventoryMovementId",
                table: "VendorInvoiceReceiptCostValuations",
                columns: new[] { "TenantId", "InventoryMovementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostValuations_TenantId_ReversesValuationId",
                table: "VendorInvoiceReceiptCostValuations",
                columns: new[] { "TenantId", "ReversesValuationId" },
                unique: true,
                filter: "[ReversesValuationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptCostValuations_WarehouseId",
                table: "VendorInvoiceReceiptCostValuations",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_Accounts_InventoryDisposalAccountId",
                table: "InventoryItems",
                column: "InventoryDisposalAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_InventoryTransferDispatchAllocations_TransferDispatchAllocationId",
                table: "InventoryMovements",
                column: "TransferDispatchAllocationId",
                principalTable: "InventoryTransferDispatchAllocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_InventoryTransferReceiptAllocations_TransferReceiptAllocationId",
                table: "InventoryMovements",
                column: "TransferReceiptAllocationId",
                principalTable: "InventoryTransferReceiptAllocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransfers_BusinessPartners_CarrierBusinessPartnerId",
                table: "InventoryTransfers",
                column: "CarrierBusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PhysicalCountItems_PhysicalCountItems_PredecessorPhysicalCountItemId",
                table: "PhysicalCountItems",
                column: "PredecessorPhysicalCountItemId",
                principalTable: "PhysicalCountItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PhysicalCountItems_PhysicalCountItems_RootPhysicalCountItemId",
                table: "PhysicalCountItems",
                column: "RootPhysicalCountItemId",
                principalTable: "PhysicalCountItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PhysicalCountItems_PhysicalCounts_SupersededByPhysicalCountId",
                table: "PhysicalCountItems",
                column: "SupersededByPhysicalCountId",
                principalTable: "PhysicalCounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PhysicalCounts_PhysicalCounts_ParentPhysicalCountId",
                table: "PhysicalCounts",
                column: "ParentPhysicalCountId",
                principalTable: "PhysicalCounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PhysicalCounts_PhysicalCounts_RootPhysicalCountId",
                table: "PhysicalCounts",
                column: "RootPhysicalCountId",
                principalTable: "PhysicalCounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_InventoryTransferDispatchAllocations_TransferDispatchAllocationId",
                table: "StockMovements",
                column: "TransferDispatchAllocationId",
                principalTable: "InventoryTransferDispatchAllocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_InventoryTransferReceiptAllocations_TransferReceiptAllocationId",
                table: "StockMovements",
                column: "TransferReceiptAllocationId",
                principalTable: "InventoryTransferReceiptAllocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierDebitNotes_InventorySupplierReturnAccountingGroups_InventorySupplierReturnAccountingGroupId",
                table: "SupplierDebitNotes",
                column: "InventorySupplierReturnAccountingGroupId",
                principalTable: "InventorySupplierReturnAccountingGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            InventoryTransferAllocationGuards.Install(migrationBuilder);
            PhysicalCountCommitteeGuards.Install(migrationBuilder);
            PhysicalCountRecountGuards.Install(migrationBuilder);
            InventorySupplierInvoiceCostGuards.Install(migrationBuilder);
            InventorySupplierReturnAllocationGuards.Install(migrationBuilder);
            InventoryDisposalAccountingGuards.Install(migrationBuilder);
            SalesOrderInvoiceGuards.Install(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            SalesOrderInvoiceGuards.Remove(migrationBuilder);
            InventoryDisposalAccountingGuards.Remove(migrationBuilder);
            InventorySupplierReturnAllocationGuards.Remove(migrationBuilder);
            InventorySupplierInvoiceCostGuards.Remove(migrationBuilder);
            PhysicalCountRecountGuards.Remove(migrationBuilder);
            PhysicalCountCommitteeGuards.Remove(migrationBuilder);
            InventoryTransferAllocationGuards.Remove(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_Accounts_InventoryDisposalAccountId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_InventoryTransferDispatchAllocations_TransferDispatchAllocationId",
                table: "InventoryMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_InventoryTransferReceiptAllocations_TransferReceiptAllocationId",
                table: "InventoryMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransfers_BusinessPartners_CarrierBusinessPartnerId",
                table: "InventoryTransfers");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysicalCountItems_PhysicalCountItems_PredecessorPhysicalCountItemId",
                table: "PhysicalCountItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysicalCountItems_PhysicalCountItems_RootPhysicalCountItemId",
                table: "PhysicalCountItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysicalCountItems_PhysicalCounts_SupersededByPhysicalCountId",
                table: "PhysicalCountItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysicalCounts_PhysicalCounts_ParentPhysicalCountId",
                table: "PhysicalCounts");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysicalCounts_PhysicalCounts_RootPhysicalCountId",
                table: "PhysicalCounts");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_InventoryTransferDispatchAllocations_TransferDispatchAllocationId",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_InventoryTransferReceiptAllocations_TransferReceiptAllocationId",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierDebitNotes_InventorySupplierReturnAccountingGroups_InventorySupplierReturnAccountingGroupId",
                table: "SupplierDebitNotes");

            migrationBuilder.DropTable(
                name: "InventoryDisposalAuctionInvoices");

            migrationBuilder.DropTable(
                name: "InventorySupplierReturnAccrualShares");

            migrationBuilder.DropTable(
                name: "InventoryTransferReceiptAllocations");

            migrationBuilder.DropTable(
                name: "PhysicalCountAdjustmentClaims");

            migrationBuilder.DropTable(
                name: "PhysicalCountCounters");

            migrationBuilder.DropTable(
                name: "VendorInvoiceReceiptCostPostingLines");

            migrationBuilder.DropTable(
                name: "VendorInvoiceReceiptCostValuations");

            migrationBuilder.DropTable(
                name: "InventorySupplierReturnAllocations");

            migrationBuilder.DropTable(
                name: "InventoryTransferDispatchAllocations");

            migrationBuilder.DropTable(
                name: "VendorInvoiceReceiptCostAllocations");

            migrationBuilder.DropTable(
                name: "InventorySupplierReturnAccountingGroups");

            migrationBuilder.DropTable(
                name: "ProcurementReceiptCostBases");

            migrationBuilder.DropIndex(
                name: "IX_VendorInvoiceReceiptAllocations_TenantId_VendorInvoiceLineItemId_GoodsReceiptNoteItemId",
                table: "VendorInvoiceReceiptAllocations");

            migrationBuilder.DropIndex(
                name: "IX_SupplierDebitNotes_InventorySupplierReturnAccountingGroupId",
                table: "SupplierDebitNotes");

            migrationBuilder.DropIndex(
                name: "UX_SupplierDebitNotes_Tenant_InventoryReturn",
                table: "SupplierDebitNotes");

            migrationBuilder.DropIndex(
                name: "UX_SupplierDebitNotes_Tenant_InventoryReturnGroup",
                table: "SupplierDebitNotes");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_TenantId_TransferDispatchAllocationId_TransferLeg",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_TenantId_TransferReceiptAllocationId_TransferLeg",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_TransferDispatchAllocationId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_TransferReceiptAllocationId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_TenantId_InvoiceId",
                table: "SalesOrders");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalCounts_ParentPhysicalCountId",
                table: "PhysicalCounts");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalCounts_RootPhysicalCountId",
                table: "PhysicalCounts");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalCounts_TenantId_ParentPhysicalCountId_RecountRequestKey",
                table: "PhysicalCounts");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalCounts_TenantId_RootPhysicalCountId_RecountAttempt",
                table: "PhysicalCounts");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalCountItems_PredecessorPhysicalCountItemId",
                table: "PhysicalCountItems");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalCountItems_RootPhysicalCountItemId",
                table: "PhysicalCountItems");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalCountItems_SupersededByPhysicalCountId",
                table: "PhysicalCountItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalCountItems_DefectiveQuantity",
                table: "PhysicalCountItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalCountActions_ActionType",
                table: "PhysicalCountActions");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransfers_CarrierBusinessPartnerId",
                table: "InventoryTransfers");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_TenantId_TransferDispatchAllocationId_TransferLeg",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_TenantId_TransferReceiptAllocationId_TransferLeg",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_TransferDispatchAllocationId",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_TransferReceiptAllocationId",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_InventoryDisposalAccountId",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryDisposalCases_PreparedStockAdjustmentId",
                table: "InventoryDisposalCases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryDisposalCases_AccountingVersion",
                table: "InventoryDisposalCases");

            migrationBuilder.DropColumn(
                name: "InventorySupplierReturnAccountingGroupId",
                table: "SupplierDebitNotes");

            migrationBuilder.DropColumn(
                name: "TransferDispatchAllocationId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "TransferLeg",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "TransferReceiptAllocationId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "InvoiceEconomicsJson",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "InvoiceGeneratedById",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "InvoiceGenerationHash",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "InvoiceGenerationKey",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "InvoiceSourceJson",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "AccountingAllocationVersion",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "PurchasePriceDifferenceHandling",
                table: "ProcurementSettings");

            migrationBuilder.DropColumn(
                name: "ObservationSubmittedAtUtc",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "ParentPhysicalCountId",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "RecountAttempt",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "RecountRequestHash",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "RecountRequestKey",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "RootPhysicalCountId",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "DefectiveNotes",
                table: "PhysicalCountItems");

            migrationBuilder.DropColumn(
                name: "DefectiveQuantity",
                table: "PhysicalCountItems");

            migrationBuilder.DropColumn(
                name: "PredecessorPhysicalCountItemId",
                table: "PhysicalCountItems");

            migrationBuilder.DropColumn(
                name: "RecountReason",
                table: "PhysicalCountItems");

            migrationBuilder.DropColumn(
                name: "RootPhysicalCountItemId",
                table: "PhysicalCountItems");

            migrationBuilder.DropColumn(
                name: "SupersededByPhysicalCountId",
                table: "PhysicalCountItems");

            migrationBuilder.DropColumn(
                name: "CarrierBusinessPartnerId",
                table: "InventoryTransfers");

            migrationBuilder.DropColumn(
                name: "TransferDispatchAllocationId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "TransferLeg",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "TransferReceiptAllocationId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "InventoryDisposalAccountId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "AccountingVersion",
                table: "InventoryDisposalCases");

            migrationBuilder.DropColumn(
                name: "PreparedStockAdjustmentId",
                table: "InventoryDisposalCases");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceReceiptAllocations_TenantId_VendorInvoiceLineItemId",
                table: "VendorInvoiceReceiptAllocations",
                columns: new[] { "TenantId", "VendorInvoiceLineItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SupplierDebitNotes_Tenant_InventoryReturn",
                table: "SupplierDebitNotes",
                columns: new[] { "TenantId", "InventoryPurchaseReturnId" },
                unique: true,
                filter: "[InventoryPurchaseReturnId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_TenantId",
                table: "StockMovements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_TenantId",
                table: "SalesOrders",
                column: "TenantId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalCountActions_ActionType",
                table: "PhysicalCountActions",
                sql: "ActionType BETWEEN 1 AND 16");
        }
    }
}
