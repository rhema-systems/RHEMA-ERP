using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancePurchaseOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExchangeRateDate",
                table: "CashTransaction",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExchangeRateSource",
                table: "CashTransaction",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JournalEntryAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_JournalEntryAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalEntryAttachments_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JournalEntryAttachments_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JournalEntryAttachments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryAttachments_FileUploadRecordId",
                table: "JournalEntryAttachments",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryAttachments_JournalEntryId",
                table: "JournalEntryAttachments",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryAttachments_TenantId",
                table: "JournalEntryAttachments",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalEntryAttachments");

            migrationBuilder.DropColumn(
                name: "ExchangeRateDate",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "ExchangeRateSource",
                table: "CashTransaction");
        }
    }
}
