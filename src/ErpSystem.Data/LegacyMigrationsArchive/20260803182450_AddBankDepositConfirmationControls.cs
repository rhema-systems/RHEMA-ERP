using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    // Routine Debug/deployment builds intentionally omit generated designer history. Keeping the
    // discovery metadata on this executable migration ensures the confirmation-control schema is
    // never silently skipped when the lightweight build profile applies the pending chain.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260803182450_AddBankDepositConfirmationControls")]
    /// <inheritdoc />
    public partial class AddBankDepositConfirmationControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BankConfirmationDate",
                table: "BankDepositBatches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BankConfirmationEvidenceFileId",
                table: "BankDepositBatches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankConfirmationNotes",
                table: "BankDepositBatches",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankConfirmationReference",
                table: "BankDepositBatches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BankConfirmedAt",
                table: "BankDepositBatches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BankConfirmedById",
                table: "BankDepositBatches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmationStatus",
                table: "BankDepositBatches",
                type: "int",
                nullable: false,
                // Pending is the honest initial state for every posted or not-yet-posted batch.
                // Zero is not a domain value and would make migrated development rows ambiguous.
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_BankDepositBatches_BankConfirmationEvidenceFileId",
                table: "BankDepositBatches",
                column: "BankConfirmationEvidenceFileId");

            migrationBuilder.CreateIndex(
                name: "IX_BankDepositBatches_TenantId_BankAccountId_BankConfirmationReference",
                table: "BankDepositBatches",
                columns: new[] { "TenantId", "BankAccountId", "BankConfirmationReference" },
                unique: true,
                filter: "[BankConfirmationReference] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BankDepositBatches_TenantId_ConfirmationStatus_BankConfirmationDate",
                table: "BankDepositBatches",
                columns: new[] { "TenantId", "ConfirmationStatus", "BankConfirmationDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_BankDepositBatches_FileUploadRecords_BankConfirmationEvidenceFileId",
                table: "BankDepositBatches",
                column: "BankConfirmationEvidenceFileId",
                principalTable: "FileUploadRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankDepositBatches_FileUploadRecords_BankConfirmationEvidenceFileId",
                table: "BankDepositBatches");

            migrationBuilder.DropIndex(
                name: "IX_BankDepositBatches_BankConfirmationEvidenceFileId",
                table: "BankDepositBatches");

            migrationBuilder.DropIndex(
                name: "IX_BankDepositBatches_TenantId_BankAccountId_BankConfirmationReference",
                table: "BankDepositBatches");

            migrationBuilder.DropIndex(
                name: "IX_BankDepositBatches_TenantId_ConfirmationStatus_BankConfirmationDate",
                table: "BankDepositBatches");

            migrationBuilder.DropColumn(
                name: "BankConfirmationDate",
                table: "BankDepositBatches");

            migrationBuilder.DropColumn(
                name: "BankConfirmationEvidenceFileId",
                table: "BankDepositBatches");

            migrationBuilder.DropColumn(
                name: "BankConfirmationNotes",
                table: "BankDepositBatches");

            migrationBuilder.DropColumn(
                name: "BankConfirmationReference",
                table: "BankDepositBatches");

            migrationBuilder.DropColumn(
                name: "BankConfirmedAt",
                table: "BankDepositBatches");

            migrationBuilder.DropColumn(
                name: "BankConfirmedById",
                table: "BankDepositBatches");

            migrationBuilder.DropColumn(
                name: "ConfirmationStatus",
                table: "BankDepositBatches");
        }
    }
}
