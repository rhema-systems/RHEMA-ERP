using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRfqAwardingAndPoLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourcePurchaseRequisitionItemId",
                table: "RequestForQuotationItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceRfqAwardType",
                table: "PurchaseOrders",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceRfqId",
                table: "PurchaseOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceRfqNumber",
                table: "PurchaseOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceRfqQuoteId",
                table: "PurchaseOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceRfqItemId",
                table: "PurchaseOrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceRfqQuoteId",
                table: "PurchaseOrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceRfqQuoteItemId",
                table: "PurchaseOrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RequestForQuotationAwardLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RfqId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RfqItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestForQuotationAwardLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationAwardLines_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationAwardLines_RequestForQuotationItems_RfqItemId",
                        column: x => x.RfqItemId,
                        principalTable: "RequestForQuotationItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationAwardLines_RequestForQuotationQuotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "RequestForQuotationQuotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationAwardLines_RequestForQuotations_RfqId",
                        column: x => x.RfqId,
                        principalTable: "RequestForQuotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequestForQuotationAwardLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationItems_SourcePurchaseRequisitionItemId",
                table: "RequestForQuotationItems",
                column: "SourcePurchaseRequisitionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationAwardLines_BusinessPartnerId",
                table: "RequestForQuotationAwardLines",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationAwardLines_QuoteId",
                table: "RequestForQuotationAwardLines",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationAwardLines_RfqId",
                table: "RequestForQuotationAwardLines",
                column: "RfqId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationAwardLines_RfqItemId",
                table: "RequestForQuotationAwardLines",
                column: "RfqItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationAwardLines_TenantId_RfqId_RfqItemId",
                table: "RequestForQuotationAwardLines",
                columns: new[] { "TenantId", "RfqId", "RfqItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequestForQuotationAwardLines");

            migrationBuilder.DropIndex(
                name: "IX_RequestForQuotationItems_SourcePurchaseRequisitionItemId",
                table: "RequestForQuotationItems");

            migrationBuilder.DropColumn(
                name: "SourcePurchaseRequisitionItemId",
                table: "RequestForQuotationItems");

            migrationBuilder.DropColumn(
                name: "SourceRfqAwardType",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SourceRfqId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SourceRfqNumber",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SourceRfqQuoteId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SourceRfqItemId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "SourceRfqQuoteId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "SourceRfqQuoteItemId",
                table: "PurchaseOrderItems");
        }
    }
}
