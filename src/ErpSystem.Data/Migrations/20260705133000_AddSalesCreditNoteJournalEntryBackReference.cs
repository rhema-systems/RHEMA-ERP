using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260705133000_AddSalesCreditNoteJournalEntryBackReference")]
    public partial class AddSalesCreditNoteJournalEntryBackReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "JournalEntryId",
                table: "CreditNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_JournalEntryId",
                table: "CreditNotes",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_TenantId_JournalEntryId",
                table: "CreditNotes",
                columns: new[] { "TenantId", "JournalEntryId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CreditNotes_JournalEntries_JournalEntryId",
                table: "CreditNotes",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CreditNotes_JournalEntries_JournalEntryId",
                table: "CreditNotes");

            migrationBuilder.DropIndex(
                name: "IX_CreditNotes_JournalEntryId",
                table: "CreditNotes");

            migrationBuilder.DropIndex(
                name: "IX_CreditNotes_TenantId_JournalEntryId",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "CreditNotes");
        }
    }
}
