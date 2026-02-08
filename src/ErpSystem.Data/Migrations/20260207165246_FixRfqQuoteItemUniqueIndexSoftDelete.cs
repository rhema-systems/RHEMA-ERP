using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixRfqQuoteItemUniqueIndexSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RequestForQuotationQuoteItems_TenantId_QuoteId_RfqItemId",
                table: "RequestForQuotationQuoteItems");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationQuoteItems_TenantId_QuoteId_RfqItemId",
                table: "RequestForQuotationQuoteItems",
                columns: new[] { "TenantId", "QuoteId", "RfqItemId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RequestForQuotationQuoteItems_TenantId_QuoteId_RfqItemId",
                table: "RequestForQuotationQuoteItems");

            migrationBuilder.CreateIndex(
                name: "IX_RequestForQuotationQuoteItems_TenantId_QuoteId_RfqItemId",
                table: "RequestForQuotationQuoteItems",
                columns: new[] { "TenantId", "QuoteId", "RfqItemId" },
                unique: true);
        }
    }
}

