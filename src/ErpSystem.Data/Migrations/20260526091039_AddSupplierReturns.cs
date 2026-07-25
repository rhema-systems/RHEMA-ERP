using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierReturns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OriginalVendorInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalFinancePurchaseOrderReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BaseCurrencyAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_SupplierReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierReturns_BusinessPartners_VendorId",
                        column: x => x.VendorId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierReturns_FinancePurchaseOrderReceipts_OriginalFinancePurchaseOrderReceiptId",
                        column: x => x.OriginalFinancePurchaseOrderReceiptId,
                        principalTable: "FinancePurchaseOrderReceipts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupplierReturns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReturns_VendorInvoice_OriginalVendorInvoiceId",
                        column: x => x.OriginalVendorInvoiceId,
                        principalTable: "VendorInvoice",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SupplierDebitNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DebitNoteNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalVendorInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DebitNoteDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BaseCurrencyAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_SupplierDebitNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierDebitNotes_BusinessPartners_VendorId",
                        column: x => x.VendorId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierDebitNotes_SupplierReturns_SupplierReturnId",
                        column: x => x.SupplierReturnId,
                        principalTable: "SupplierReturns",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupplierDebitNotes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierDebitNotes_VendorInvoice_OriginalVendorInvoiceId",
                        column: x => x.OriginalVendorInvoiceId,
                        principalTable: "VendorInvoice",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SupplierReturnLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalVendorInvoiceLineItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalFinancePurchaseOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    QuantityReturned = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_SupplierReturnLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierReturnLineItems_SupplierReturns_SupplierReturnId",
                        column: x => x.SupplierReturnId,
                        principalTable: "SupplierReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReturnLineItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierDebitNoteLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierDebitNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_SupplierDebitNoteLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierDebitNoteLineItems_SupplierDebitNotes_SupplierDebitNoteId",
                        column: x => x.SupplierDebitNoteId,
                        principalTable: "SupplierDebitNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierDebitNoteLineItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDebitNoteLineItems_SupplierDebitNoteId",
                table: "SupplierDebitNoteLineItems",
                column: "SupplierDebitNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDebitNoteLineItems_TenantId",
                table: "SupplierDebitNoteLineItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDebitNotes_OriginalVendorInvoiceId",
                table: "SupplierDebitNotes",
                column: "OriginalVendorInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDebitNotes_SupplierReturnId",
                table: "SupplierDebitNotes",
                column: "SupplierReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDebitNotes_TenantId",
                table: "SupplierDebitNotes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDebitNotes_VendorId",
                table: "SupplierDebitNotes",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLineItems_SupplierReturnId",
                table: "SupplierReturnLineItems",
                column: "SupplierReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLineItems_TenantId",
                table: "SupplierReturnLineItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturns_OriginalFinancePurchaseOrderReceiptId",
                table: "SupplierReturns",
                column: "OriginalFinancePurchaseOrderReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturns_OriginalVendorInvoiceId",
                table: "SupplierReturns",
                column: "OriginalVendorInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturns_TenantId",
                table: "SupplierReturns",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturns_VendorId",
                table: "SupplierReturns",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierDebitNoteLineItems");

            migrationBuilder.DropTable(
                name: "SupplierReturnLineItems");

            migrationBuilder.DropTable(
                name: "SupplierDebitNotes");

            migrationBuilder.DropTable(
                name: "SupplierReturns");
        }
    }
}
