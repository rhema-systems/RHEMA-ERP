using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    // Fast Debug and deployment builds omit the multi-megabyte generated designer history. The
    // discovery attributes therefore live on the executable migration as well, ensuring TDC's
    // controlled-document audit table is included whenever the pending chain is applied.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260803193523_AddControlledCashBankDocumentIssuance")]
    /// <inheritdoc />
    public partial class AddControlledCashBankDocumentIssuance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinanceControlledDocumentIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceDocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CopyNumber = table.Column<int>(type: "int", nullable: false),
                    CopyType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReplacementReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IssuedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuedByName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_FinanceControlledDocumentIssues", x => x.Id);
                    table.CheckConstraint("CK_FinanceControlledDocumentIssues_ContentSha256", "LEN([ContentSha256]) = 64");
                    table.CheckConstraint("CK_FinanceControlledDocumentIssues_CopyNumber", "[CopyNumber] >= 1");
                    table.CheckConstraint("CK_FinanceControlledDocumentIssues_CopyType", "[CopyType] IN ('Original', 'Replacement')");
                    table.CheckConstraint("CK_FinanceControlledDocumentIssues_ReplacementReason", "([CopyType] = 'Original' AND [ReplacementReason] IS NULL) OR ([CopyType] = 'Replacement' AND LEN([ReplacementReason]) >= 20)");
                    table.ForeignKey(
                        name: "FK_FinanceControlledDocumentIssues_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceControlledDocumentIssues_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceControlledDocumentIssues_Users_IssuedById",
                        column: x => x.IssuedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceControlledDocumentIssues_IssuedById",
                table: "FinanceControlledDocumentIssues",
                column: "IssuedById");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceControlledDocumentIssues_JournalEntryId",
                table: "FinanceControlledDocumentIssues",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceControlledDocumentIssues_TenantId_DocumentType_IssuedAtUtc",
                table: "FinanceControlledDocumentIssues",
                columns: new[] { "TenantId", "DocumentType", "IssuedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceControlledDocumentIssues_TenantId_DocumentType_SourceDocumentId_CopyNumber",
                table: "FinanceControlledDocumentIssues",
                columns: new[] { "TenantId", "DocumentType", "SourceDocumentId", "CopyNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceControlledDocumentIssues_TenantId_JournalEntryId",
                table: "FinanceControlledDocumentIssues",
                columns: new[] { "TenantId", "JournalEntryId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinanceControlledDocumentIssues");
        }
    }
}
