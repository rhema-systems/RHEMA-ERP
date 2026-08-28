using System;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Extends the existing fixed-asset transfer register with immutable category, account, book,
/// and balance evidence for controlled GL reclassification. The columns are nullable/defaulted
/// so ordinary physical transfers remain lightweight; no legacy interpretation or data rewrite
/// is attempted because only new GL reclassification requests populate this evidence.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260809223000_AddFixedAssetGlReclassificationControls")]
public partial class AddFixedAssetGlReclassificationControls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("AccountingBookId", "AssetTransfers", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>("BookClassification", "AssetTransfers", "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "IFRS");
        migrationBuilder.AddColumn<Guid>("FromFixedAssetCategoryId", "AssetTransfers", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("ToFixedAssetCategoryId", "AssetTransfers", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("FromAssetAccountId", "AssetTransfers", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("ToAssetAccountId", "AssetTransfers", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("FromAccumulatedDepreciationAccountId", "AssetTransfers", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("ToAccumulatedDepreciationAccountId", "AssetTransfers", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("FromAccumulatedImpairmentAccountId", "AssetTransfers", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("ToAccumulatedImpairmentAccountId", "AssetTransfers", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("FromRevaluationSurplusAccountId", "AssetTransfers", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("ToRevaluationSurplusAccountId", "AssetTransfers", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<decimal>("ReclassificationAssetCarryingAmount", "AssetTransfers", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("ReclassificationAccumulatedDepreciation", "AssetTransfers", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("ReclassificationAccumulatedImpairment", "AssetTransfers", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("ReclassificationRevaluationSurplus", "AssetTransfers", "decimal(18,2)", nullable: false, defaultValue: 0m);

        migrationBuilder.CreateIndex("IX_AssetTransfers_AccountingBookId", "AssetTransfers", "AccountingBookId");
        migrationBuilder.CreateIndex("IX_AssetTransfers_FromFixedAssetCategoryId", "AssetTransfers", "FromFixedAssetCategoryId");
        migrationBuilder.CreateIndex("IX_AssetTransfers_ToFixedAssetCategoryId", "AssetTransfers", "ToFixedAssetCategoryId");
        migrationBuilder.CreateIndex("IX_AssetTransfers_FromAssetAccountId", "AssetTransfers", "FromAssetAccountId");
        migrationBuilder.CreateIndex("IX_AssetTransfers_ToAssetAccountId", "AssetTransfers", "ToAssetAccountId");
        migrationBuilder.CreateIndex("IX_AssetTransfers_FromAccumulatedDepreciationAccountId", "AssetTransfers", "FromAccumulatedDepreciationAccountId");
        migrationBuilder.CreateIndex("IX_AssetTransfers_ToAccumulatedDepreciationAccountId", "AssetTransfers", "ToAccumulatedDepreciationAccountId");
        migrationBuilder.CreateIndex("IX_AssetTransfers_FromAccumulatedImpairmentAccountId", "AssetTransfers", "FromAccumulatedImpairmentAccountId");
        migrationBuilder.CreateIndex("IX_AssetTransfers_ToAccumulatedImpairmentAccountId", "AssetTransfers", "ToAccumulatedImpairmentAccountId");
        migrationBuilder.CreateIndex("IX_AssetTransfers_FromRevaluationSurplusAccountId", "AssetTransfers", "FromRevaluationSurplusAccountId");
        migrationBuilder.CreateIndex("IX_AssetTransfers_ToRevaluationSurplusAccountId", "AssetTransfers", "ToRevaluationSurplusAccountId");
        migrationBuilder.CreateIndex("IX_AssetTransfers_TenantId_AccountingBookId", "AssetTransfers", new[] { "TenantId", "AccountingBookId" });
        migrationBuilder.CreateIndex("IX_AssetTransfers_TenantId_ToFixedAssetCategoryId", "AssetTransfers", new[] { "TenantId", "ToFixedAssetCategoryId" });

        migrationBuilder.AddForeignKey("FK_AssetTransfers_AccountingBooks_AccountingBookId", "AssetTransfers", "AccountingBookId", "AccountingBooks", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_AssetTransfers_FixedAssetCategories_FromFixedAssetCategoryId", "AssetTransfers", "FromFixedAssetCategoryId", "FixedAssetCategories", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_AssetTransfers_FixedAssetCategories_ToFixedAssetCategoryId", "AssetTransfers", "ToFixedAssetCategoryId", "FixedAssetCategories", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_AssetTransfers_Accounts_FromAssetAccountId", "AssetTransfers", "FromAssetAccountId", "Accounts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_AssetTransfers_Accounts_ToAssetAccountId", "AssetTransfers", "ToAssetAccountId", "Accounts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_AssetTransfers_Accounts_FromAccumulatedDepreciationAccountId", "AssetTransfers", "FromAccumulatedDepreciationAccountId", "Accounts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_AssetTransfers_Accounts_ToAccumulatedDepreciationAccountId", "AssetTransfers", "ToAccumulatedDepreciationAccountId", "Accounts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_AssetTransfers_Accounts_FromAccumulatedImpairmentAccountId", "AssetTransfers", "FromAccumulatedImpairmentAccountId", "Accounts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_AssetTransfers_Accounts_ToAccumulatedImpairmentAccountId", "AssetTransfers", "ToAccumulatedImpairmentAccountId", "Accounts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_AssetTransfers_Accounts_FromRevaluationSurplusAccountId", "AssetTransfers", "FromRevaluationSurplusAccountId", "Accounts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_AssetTransfers_Accounts_ToRevaluationSurplusAccountId", "AssetTransfers", "ToRevaluationSurplusAccountId", "Accounts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_AssetTransfers_AccountingBooks_AccountingBookId", "AssetTransfers");
        migrationBuilder.DropForeignKey("FK_AssetTransfers_FixedAssetCategories_FromFixedAssetCategoryId", "AssetTransfers");
        migrationBuilder.DropForeignKey("FK_AssetTransfers_FixedAssetCategories_ToFixedAssetCategoryId", "AssetTransfers");
        migrationBuilder.DropForeignKey("FK_AssetTransfers_Accounts_FromAssetAccountId", "AssetTransfers");
        migrationBuilder.DropForeignKey("FK_AssetTransfers_Accounts_ToAssetAccountId", "AssetTransfers");
        migrationBuilder.DropForeignKey("FK_AssetTransfers_Accounts_FromAccumulatedDepreciationAccountId", "AssetTransfers");
        migrationBuilder.DropForeignKey("FK_AssetTransfers_Accounts_ToAccumulatedDepreciationAccountId", "AssetTransfers");
        migrationBuilder.DropForeignKey("FK_AssetTransfers_Accounts_FromAccumulatedImpairmentAccountId", "AssetTransfers");
        migrationBuilder.DropForeignKey("FK_AssetTransfers_Accounts_ToAccumulatedImpairmentAccountId", "AssetTransfers");
        migrationBuilder.DropForeignKey("FK_AssetTransfers_Accounts_FromRevaluationSurplusAccountId", "AssetTransfers");
        migrationBuilder.DropForeignKey("FK_AssetTransfers_Accounts_ToRevaluationSurplusAccountId", "AssetTransfers");

        migrationBuilder.DropIndex("IX_AssetTransfers_AccountingBookId", "AssetTransfers");
        migrationBuilder.DropIndex("IX_AssetTransfers_FromFixedAssetCategoryId", "AssetTransfers");
        migrationBuilder.DropIndex("IX_AssetTransfers_ToFixedAssetCategoryId", "AssetTransfers");
        migrationBuilder.DropIndex("IX_AssetTransfers_FromAssetAccountId", "AssetTransfers");
        migrationBuilder.DropIndex("IX_AssetTransfers_ToAssetAccountId", "AssetTransfers");
        migrationBuilder.DropIndex("IX_AssetTransfers_FromAccumulatedDepreciationAccountId", "AssetTransfers");
        migrationBuilder.DropIndex("IX_AssetTransfers_ToAccumulatedDepreciationAccountId", "AssetTransfers");
        migrationBuilder.DropIndex("IX_AssetTransfers_FromAccumulatedImpairmentAccountId", "AssetTransfers");
        migrationBuilder.DropIndex("IX_AssetTransfers_ToAccumulatedImpairmentAccountId", "AssetTransfers");
        migrationBuilder.DropIndex("IX_AssetTransfers_FromRevaluationSurplusAccountId", "AssetTransfers");
        migrationBuilder.DropIndex("IX_AssetTransfers_ToRevaluationSurplusAccountId", "AssetTransfers");
        migrationBuilder.DropIndex("IX_AssetTransfers_TenantId_AccountingBookId", "AssetTransfers");
        migrationBuilder.DropIndex("IX_AssetTransfers_TenantId_ToFixedAssetCategoryId", "AssetTransfers");

        migrationBuilder.DropColumn("AccountingBookId", "AssetTransfers");
        migrationBuilder.DropColumn("BookClassification", "AssetTransfers");
        migrationBuilder.DropColumn("FromFixedAssetCategoryId", "AssetTransfers");
        migrationBuilder.DropColumn("ToFixedAssetCategoryId", "AssetTransfers");
        migrationBuilder.DropColumn("FromAssetAccountId", "AssetTransfers");
        migrationBuilder.DropColumn("ToAssetAccountId", "AssetTransfers");
        migrationBuilder.DropColumn("FromAccumulatedDepreciationAccountId", "AssetTransfers");
        migrationBuilder.DropColumn("ToAccumulatedDepreciationAccountId", "AssetTransfers");
        migrationBuilder.DropColumn("FromAccumulatedImpairmentAccountId", "AssetTransfers");
        migrationBuilder.DropColumn("ToAccumulatedImpairmentAccountId", "AssetTransfers");
        migrationBuilder.DropColumn("FromRevaluationSurplusAccountId", "AssetTransfers");
        migrationBuilder.DropColumn("ToRevaluationSurplusAccountId", "AssetTransfers");
        migrationBuilder.DropColumn("ReclassificationAssetCarryingAmount", "AssetTransfers");
        migrationBuilder.DropColumn("ReclassificationAccumulatedDepreciation", "AssetTransfers");
        migrationBuilder.DropColumn("ReclassificationAccumulatedImpairment", "AssetTransfers");
        migrationBuilder.DropColumn("ReclassificationRevaluationSurplus", "AssetTransfers");
    }
}
