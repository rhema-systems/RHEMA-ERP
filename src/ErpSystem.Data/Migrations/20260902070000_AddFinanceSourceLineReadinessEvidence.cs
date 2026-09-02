using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds server-controlled document-date, account and complete source-line manifest evidence to the
/// generic Finance source-dimension assignment store. Existing rows remain explicit legacy evidence.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260902070000_AddFinanceSourceLineReadinessEvidence")]
public class AddFinanceSourceLineReadinessEvidence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExpectedSourceLineCount",
                table: "FinanceSourceDimensionAssignments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResolvedAccountId",
                table: "FinanceSourceDimensionAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceDocumentDate",
                table: "FinanceSourceDimensionAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceLineManifestHash",
                table: "FinanceSourceDimensionAssignments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSourceDimensionAssignments_TenantId_ResolvedAccountId",
                table: "FinanceSourceDimensionAssignments",
                columns: new[] { "TenantId", "ResolvedAccountId" });

            migrationBuilder.AddForeignKey(
                name: "FK_FinanceSourceDimensionAssignments_Accounts_ResolvedAccountId",
                table: "FinanceSourceDimensionAssignments",
                column: "ResolvedAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinanceSourceDimensionAssignments_Accounts_ResolvedAccountId",
                table: "FinanceSourceDimensionAssignments");

            migrationBuilder.DropIndex(
                name: "IX_FinanceSourceDimensionAssignments_TenantId_ResolvedAccountId",
                table: "FinanceSourceDimensionAssignments");

            migrationBuilder.DropColumn(
                name: "ExpectedSourceLineCount",
                table: "FinanceSourceDimensionAssignments");

            migrationBuilder.DropColumn(
                name: "ResolvedAccountId",
                table: "FinanceSourceDimensionAssignments");

            migrationBuilder.DropColumn(
                name: "SourceDocumentDate",
                table: "FinanceSourceDimensionAssignments");

            migrationBuilder.DropColumn(
                name: "SourceLineManifestHash",
                table: "FinanceSourceDimensionAssignments");
        }
}
