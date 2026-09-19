using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinanceBookClassificationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_TenantId",
                table: "JournalEntries");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountClassificationId",
                table: "AccountAccountingBooks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AccountAccountingBooks",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "AccountClassifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountingBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentClassificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CoreAccountType = table.Column<int>(type: "int", nullable: false),
                    DefaultRevaluationTreatment = table.Column<int>(type: "int", nullable: false),
                    SystemRole = table.Column<int>(type: "int", nullable: true),
                    IsPostingClassification = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    RetirementReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RetiredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AccountClassifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountClassifications_AccountClassifications_ParentClassificationId",
                        column: x => x.ParentClassificationId,
                        principalTable: "AccountClassifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountClassifications_AccountingBooks_AccountingBookId",
                        column: x => x.AccountingBookId,
                        principalTable: "AccountingBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountClassifications_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TenantId_OriginalJournalEntryId",
                table: "JournalEntries",
                columns: new[] { "TenantId", "OriginalJournalEntryId" },
                unique: true,
                filter: "[OriginalJournalEntryId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AccountAccountingBooks_AccountClassificationId",
                table: "AccountAccountingBooks",
                column: "AccountClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountClassifications_AccountingBookId",
                table: "AccountClassifications",
                column: "AccountingBookId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountClassifications_ParentClassificationId",
                table: "AccountClassifications",
                column: "ParentClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountClassifications_TenantId_AccountingBookId_Code",
                table: "AccountClassifications",
                columns: new[] { "TenantId", "AccountingBookId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AccountClassifications_TenantId_AccountingBookId_ParentClassificationId_DisplayOrder",
                table: "AccountClassifications",
                columns: new[] { "TenantId", "AccountingBookId", "ParentClassificationId", "DisplayOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_AccountAccountingBooks_AccountClassifications_AccountClassificationId",
                table: "AccountAccountingBooks",
                column: "AccountClassificationId",
                principalTable: "AccountClassifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountAccountingBooks_AccountClassifications_AccountClassificationId",
                table: "AccountAccountingBooks");

            migrationBuilder.DropTable(
                name: "AccountClassifications");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_TenantId_OriginalJournalEntryId",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_AccountAccountingBooks_AccountClassificationId",
                table: "AccountAccountingBooks");

            migrationBuilder.DropColumn(
                name: "AccountClassificationId",
                table: "AccountAccountingBooks");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AccountAccountingBooks");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TenantId",
                table: "JournalEntries",
                column: "TenantId");
        }
    }
}
