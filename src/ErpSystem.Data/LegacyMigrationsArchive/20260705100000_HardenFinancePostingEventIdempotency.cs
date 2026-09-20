using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260705100000_HardenFinancePostingEventIdempotency")]
    public partial class HardenFinancePostingEventIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FinancePostingEvents_TenantId_SourceDocumentType_SourceDocumentId_PostingAction",
                table: "FinancePostingEvents",
                columns: new[] { "TenantId", "SourceDocumentType", "SourceDocumentId", "PostingAction" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FinancePostingEvents_TenantId_SourceDocumentType_SourceDocumentId_PostingAction",
                table: "FinancePostingEvents");
        }
    }
}
