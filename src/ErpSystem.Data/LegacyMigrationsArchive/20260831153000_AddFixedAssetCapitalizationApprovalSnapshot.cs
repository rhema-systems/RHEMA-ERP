using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260831153000_AddFixedAssetCapitalizationApprovalSnapshot")]
public sealed class AddFixedAssetCapitalizationApprovalSnapshot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CapitalizationApprovalApprovedByUserId",
            table: "FixedAssets",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CapitalizationApprovalApprovedAt",
            table: "FixedAssets",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CapitalizationApprovalExchangeRateId",
            table: "FixedAssets",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CapitalizationApprovalInvalidatedAt",
            table: "FixedAssets",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CapitalizationApprovalInvalidationReason",
            table: "FixedAssets",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CapitalizationApprovalSnapshotHash",
            table: "FixedAssets",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CapitalizationApprovalSnapshotJson",
            table: "FixedAssets",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CapitalizationApprovalSubmittedAt",
            table: "FixedAssets",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CapitalizationApprovalSubmittedByUserId",
            table: "FixedAssets",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CapitalizationApprovalWorkflowInstanceId",
            table: "FixedAssets",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_FixedAssets_CapitalizationApprovalExchangeRateId",
            table: "FixedAssets",
            column: "CapitalizationApprovalExchangeRateId");

        migrationBuilder.AddForeignKey(
            name: "FK_FixedAssets_ExchangeRates_CapitalizationApprovalExchangeRateId",
            table: "FixedAssets",
            column: "CapitalizationApprovalExchangeRateId",
            principalTable: "ExchangeRates",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_FixedAssets_ExchangeRates_CapitalizationApprovalExchangeRateId",
            table: "FixedAssets");

        migrationBuilder.DropIndex(
            name: "IX_FixedAssets_CapitalizationApprovalExchangeRateId",
            table: "FixedAssets");

        migrationBuilder.DropColumn(name: "CapitalizationApprovalApprovedByUserId", table: "FixedAssets");
        migrationBuilder.DropColumn(name: "CapitalizationApprovalApprovedAt", table: "FixedAssets");
        migrationBuilder.DropColumn(name: "CapitalizationApprovalExchangeRateId", table: "FixedAssets");
        migrationBuilder.DropColumn(name: "CapitalizationApprovalInvalidatedAt", table: "FixedAssets");
        migrationBuilder.DropColumn(name: "CapitalizationApprovalInvalidationReason", table: "FixedAssets");
        migrationBuilder.DropColumn(name: "CapitalizationApprovalSnapshotHash", table: "FixedAssets");
        migrationBuilder.DropColumn(name: "CapitalizationApprovalSnapshotJson", table: "FixedAssets");
        migrationBuilder.DropColumn(name: "CapitalizationApprovalSubmittedAt", table: "FixedAssets");
        migrationBuilder.DropColumn(name: "CapitalizationApprovalSubmittedByUserId", table: "FixedAssets");
        migrationBuilder.DropColumn(name: "CapitalizationApprovalWorkflowInstanceId", table: "FixedAssets");
    }
}
