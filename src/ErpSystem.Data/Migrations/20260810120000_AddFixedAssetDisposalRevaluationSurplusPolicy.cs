using System;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds immutable equity-transfer evidence to the fixed-asset disposal register. This migration
/// is intentionally narrow because the repository's design-time snapshot contains unrelated
/// drift; generating the migration automatically would attempt to recreate unrelated tables.
/// Existing development disposals remain valid with no transfer evidence, while every new
/// disposal records the approved reserve and retained-earnings accounts when a transfer applies.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260810120000_AddFixedAssetDisposalRevaluationSurplusPolicy")]
public partial class AddFixedAssetDisposalRevaluationSurplusPolicy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "RevaluationSurplusAccountId",
            table: "AssetDisposals",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "RetainedEarningsAccountId",
            table: "AssetDisposals",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "RevaluationSurplusTransferAmount",
            table: "AssetDisposals",
            type: "decimal(18,2)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.CreateIndex(
            name: "IX_AssetDisposals_TenantId_RevaluationSurplusAccountId",
            table: "AssetDisposals",
            columns: new[] { "TenantId", "RevaluationSurplusAccountId" });

        migrationBuilder.CreateIndex(
            name: "IX_AssetDisposals_TenantId_RetainedEarningsAccountId",
            table: "AssetDisposals",
            columns: new[] { "TenantId", "RetainedEarningsAccountId" });

        // Restrict deletion because the selected equity accounts are part of the permanent
        // maker-checker evidence supporting the completed disposal journal.
        migrationBuilder.AddForeignKey(
            name: "FK_AssetDisposals_Accounts_RevaluationSurplusAccountId",
            table: "AssetDisposals",
            column: "RevaluationSurplusAccountId",
            principalTable: "Accounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_AssetDisposals_Accounts_RetainedEarningsAccountId",
            table: "AssetDisposals",
            column: "RetainedEarningsAccountId",
            principalTable: "Accounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_AssetDisposals_Accounts_RevaluationSurplusAccountId",
            table: "AssetDisposals");

        migrationBuilder.DropForeignKey(
            name: "FK_AssetDisposals_Accounts_RetainedEarningsAccountId",
            table: "AssetDisposals");

        migrationBuilder.DropIndex(
            name: "IX_AssetDisposals_TenantId_RevaluationSurplusAccountId",
            table: "AssetDisposals");

        migrationBuilder.DropIndex(
            name: "IX_AssetDisposals_TenantId_RetainedEarningsAccountId",
            table: "AssetDisposals");

        migrationBuilder.DropColumn(
            name: "RevaluationSurplusAccountId",
            table: "AssetDisposals");

        migrationBuilder.DropColumn(
            name: "RetainedEarningsAccountId",
            table: "AssetDisposals");

        migrationBuilder.DropColumn(
            name: "RevaluationSurplusTransferAmount",
            table: "AssetDisposals");
    }
}
