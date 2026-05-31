using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinanceAppAPTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinancePurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_FinancePurchaseOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancePurchaseOrders_BusinessPartners_VendorId",
                        column: x => x.VendorId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FinancePurchaseOrders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancePurchaseOrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancePurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineType = table.Column<int>(type: "int", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GlAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OrderedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    InvoicedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CancelledQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TaxCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TaxRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_FinancePurchaseOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancePurchaseOrderItems_Accounts_GlAccountId",
                        column: x => x.GlAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FinancePurchaseOrderItems_FinancePurchaseOrders_FinancePurchaseOrderId",
                        column: x => x.FinancePurchaseOrderId,
                        principalTable: "FinancePurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FinancePurchaseOrderItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FinancePurchaseOrderItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancePurchaseOrderReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancePurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReceiptDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VendorInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_FinancePurchaseOrderReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancePurchaseOrderReceipts_FinancePurchaseOrders_FinancePurchaseOrderId",
                        column: x => x.FinancePurchaseOrderId,
                        principalTable: "FinancePurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FinancePurchaseOrderReceipts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePurchaseOrderReceipts_VendorInvoice_VendorInvoiceId",
                        column: x => x.VendorInvoiceId,
                        principalTable: "VendorInvoice",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FinancePurchaseOrderReceiptItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancePurchaseOrderReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancePurchaseOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantityReceived = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    InvoicedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_FinancePurchaseOrderReceiptItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancePurchaseOrderReceiptItems_FinancePurchaseOrderItems_FinancePurchaseOrderItemId",
                        column: x => x.FinancePurchaseOrderItemId,
                        principalTable: "FinancePurchaseOrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FinancePurchaseOrderReceiptItems_FinancePurchaseOrderReceipts_FinancePurchaseOrderReceiptId",
                        column: x => x.FinancePurchaseOrderReceiptId,
                        principalTable: "FinancePurchaseOrderReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePurchaseOrderReceiptItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancePurchaseOrderItems_FinancePurchaseOrderId",
                table: "FinancePurchaseOrderItems",
                column: "FinancePurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePurchaseOrderItems_GlAccountId",
                table: "FinancePurchaseOrderItems",
                column: "GlAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePurchaseOrderItems_InventoryItemId",
                table: "FinancePurchaseOrderItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePurchaseOrderItems_TenantId",
                table: "FinancePurchaseOrderItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePurchaseOrderReceiptItems_FinancePurchaseOrderItemId",
                table: "FinancePurchaseOrderReceiptItems",
                column: "FinancePurchaseOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePurchaseOrderReceiptItems_FinancePurchaseOrderReceiptId",
                table: "FinancePurchaseOrderReceiptItems",
                column: "FinancePurchaseOrderReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePurchaseOrderReceiptItems_TenantId",
                table: "FinancePurchaseOrderReceiptItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePurchaseOrderReceipts_FinancePurchaseOrderId",
                table: "FinancePurchaseOrderReceipts",
                column: "FinancePurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePurchaseOrderReceipts_TenantId",
                table: "FinancePurchaseOrderReceipts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePurchaseOrderReceipts_VendorInvoiceId",
                table: "FinancePurchaseOrderReceipts",
                column: "VendorInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePurchaseOrders_TenantId",
                table: "FinancePurchaseOrders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePurchaseOrders_VendorId",
                table: "FinancePurchaseOrders",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinancePurchaseOrderReceiptItems");

            migrationBuilder.DropTable(
                name: "FinancePurchaseOrderItems");

            migrationBuilder.DropTable(
                name: "FinancePurchaseOrderReceipts");

            migrationBuilder.DropTable(
                name: "FinancePurchaseOrders");
        }
    }
}
