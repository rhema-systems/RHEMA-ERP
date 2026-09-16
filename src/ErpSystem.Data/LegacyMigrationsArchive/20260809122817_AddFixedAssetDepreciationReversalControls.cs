using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds the FIN-LIM-0033 maker-checker register and immutable posting lineage used to
/// reverse a posted fixed-asset depreciation run. The migration is deliberately hand-scoped:
/// this repository omits generated migration designers from recent fast builds, so allowing EF
/// to infer the previous snapshot from a stale assembly can incorrectly scaffold unrelated modules.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260809122817_AddFixedAssetDepreciationReversalControls")]
public sealed class AddFixedAssetDepreciationReversalControls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Sequence zero is the original run. A corrected run receives the next sequence only
        // after the previous revision is fully reversed, preserving every historical schedule.
        migrationBuilder.AddColumn<int>(
            name: "CorrectionSequence",
            table: "FixedAssetDepreciationRuns",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "CorrectionSequence",
            table: "AssetDepreciationSchedules",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<Guid>(
            name: "DepreciationReversalId",
            table: "AssetDepreciationSchedules",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsReversed",
            table: "AssetDepreciationSchedules",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<Guid>(
            name: "ReversalJournalEntryId",
            table: "AssetDepreciationSchedules",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ReversalPostingEventId",
            table: "AssetDepreciationSchedules",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ReversedAt",
            table: "AssetDepreciationSchedules",
            type: "datetime2",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "FixedAssetDepreciationReversals",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OriginalDepreciationRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OriginalPostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OriginalJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReversalPostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReversalJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                ImpactAssessment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                RequestedReversalDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestedByUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReviewedByUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ReviewComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                table.PrimaryKey("PK_FixedAssetDepreciationReversals", x => x.Id);
                table.ForeignKey(
                    name: "FK_FixedAssetDepreciationReversals_FinancePostingEvents_OriginalPostingEventId",
                    column: x => x.OriginalPostingEventId,
                    principalTable: "FinancePostingEvents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FixedAssetDepreciationReversals_FinancePostingEvents_ReversalPostingEventId",
                    column: x => x.ReversalPostingEventId,
                    principalTable: "FinancePostingEvents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FixedAssetDepreciationReversals_FixedAssetDepreciationRuns_OriginalDepreciationRunId",
                    column: x => x.OriginalDepreciationRunId,
                    principalTable: "FixedAssetDepreciationRuns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FixedAssetDepreciationReversals_JournalEntries_OriginalJournalEntryId",
                    column: x => x.OriginalJournalEntryId,
                    principalTable: "JournalEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FixedAssetDepreciationReversals_JournalEntries_ReversalJournalEntryId",
                    column: x => x.ReversalJournalEntryId,
                    principalTable: "JournalEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FixedAssetDepreciationReversals_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.DropIndex(
            name: "IX_AssetDepreciationSchedules_TenantId_FixedAssetId_FiscalPeriodId_BookClassification",
            table: "AssetDepreciationSchedules");

        migrationBuilder.CreateIndex(
            name: "IX_AssetDepreciationSchedules_DepreciationReversalId",
            table: "AssetDepreciationSchedules",
            column: "DepreciationReversalId");

        migrationBuilder.CreateIndex(
            name: "IX_AssetDepreciationSchedules_ReversalJournalEntryId",
            table: "AssetDepreciationSchedules",
            column: "ReversalJournalEntryId");

        migrationBuilder.CreateIndex(
            name: "IX_AssetDepreciationSchedules_ReversalPostingEventId",
            table: "AssetDepreciationSchedules",
            column: "ReversalPostingEventId");

        migrationBuilder.CreateIndex(
            name: "IX_AssetDepreciationSchedules_TenantId_DepreciationReversalId",
            table: "AssetDepreciationSchedules",
            columns: new[] { "TenantId", "DepreciationReversalId" });

        migrationBuilder.CreateIndex(
            name: "IX_AssetDepreciationSchedules_TenantId_ReversalPostingEventId",
            table: "AssetDepreciationSchedules",
            columns: new[] { "TenantId", "ReversalPostingEventId" });

        migrationBuilder.CreateIndex(
            name: "IX_AssetDepreciationSchedules_TenantId_FixedAssetId_FiscalPeriodId_BookClassification_CorrectionSequence",
            table: "AssetDepreciationSchedules",
            columns: new[] { "TenantId", "FixedAssetId", "FiscalPeriodId", "BookClassification", "CorrectionSequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FixedAssetDepreciationReversals_OriginalDepreciationRunId",
            table: "FixedAssetDepreciationReversals",
            column: "OriginalDepreciationRunId");

        migrationBuilder.CreateIndex(
            name: "IX_FixedAssetDepreciationReversals_OriginalJournalEntryId",
            table: "FixedAssetDepreciationReversals",
            column: "OriginalJournalEntryId");

        migrationBuilder.CreateIndex(
            name: "IX_FixedAssetDepreciationReversals_OriginalPostingEventId",
            table: "FixedAssetDepreciationReversals",
            column: "OriginalPostingEventId");

        migrationBuilder.CreateIndex(
            name: "IX_FixedAssetDepreciationReversals_ReversalJournalEntryId",
            table: "FixedAssetDepreciationReversals",
            column: "ReversalJournalEntryId");

        migrationBuilder.CreateIndex(
            name: "IX_FixedAssetDepreciationReversals_ReversalPostingEventId",
            table: "FixedAssetDepreciationReversals",
            column: "ReversalPostingEventId");

        migrationBuilder.CreateIndex(
            name: "IX_FixedAssetDepreciationReversals_TenantId_OriginalDepreciationRunId_Status",
            table: "FixedAssetDepreciationReversals",
            columns: new[] { "TenantId", "OriginalDepreciationRunId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_FixedAssetDepreciationReversals_TenantId_OriginalPostingEventId",
            table: "FixedAssetDepreciationReversals",
            columns: new[] { "TenantId", "OriginalPostingEventId" });

        migrationBuilder.CreateIndex(
            name: "IX_FixedAssetDepreciationReversals_TenantId_ReversalPostingEventId",
            table: "FixedAssetDepreciationReversals",
            columns: new[] { "TenantId", "ReversalPostingEventId" },
            unique: true,
            filter: "[ReversalPostingEventId] IS NOT NULL");

        migrationBuilder.AddForeignKey(
            name: "FK_AssetDepreciationSchedules_FixedAssetDepreciationReversals_DepreciationReversalId",
            table: "AssetDepreciationSchedules",
            column: "DepreciationReversalId",
            principalTable: "FixedAssetDepreciationReversals",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_AssetDepreciationSchedules_JournalEntries_ReversalJournalEntryId",
            table: "AssetDepreciationSchedules",
            column: "ReversalJournalEntryId",
            principalTable: "JournalEntries",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_AssetDepreciationSchedules_FinancePostingEvents_ReversalPostingEventId",
            table: "AssetDepreciationSchedules",
            column: "ReversalPostingEventId",
            principalTable: "FinancePostingEvents",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_AssetDepreciationSchedules_FixedAssetDepreciationReversals_DepreciationReversalId",
            table: "AssetDepreciationSchedules");

        migrationBuilder.DropForeignKey(
            name: "FK_AssetDepreciationSchedules_JournalEntries_ReversalJournalEntryId",
            table: "AssetDepreciationSchedules");

        migrationBuilder.DropForeignKey(
            name: "FK_AssetDepreciationSchedules_FinancePostingEvents_ReversalPostingEventId",
            table: "AssetDepreciationSchedules");

        migrationBuilder.DropTable(name: "FixedAssetDepreciationReversals");

        migrationBuilder.DropIndex(
            name: "IX_AssetDepreciationSchedules_DepreciationReversalId",
            table: "AssetDepreciationSchedules");
        migrationBuilder.DropIndex(
            name: "IX_AssetDepreciationSchedules_ReversalJournalEntryId",
            table: "AssetDepreciationSchedules");
        migrationBuilder.DropIndex(
            name: "IX_AssetDepreciationSchedules_ReversalPostingEventId",
            table: "AssetDepreciationSchedules");
        migrationBuilder.DropIndex(
            name: "IX_AssetDepreciationSchedules_TenantId_DepreciationReversalId",
            table: "AssetDepreciationSchedules");
        migrationBuilder.DropIndex(
            name: "IX_AssetDepreciationSchedules_TenantId_ReversalPostingEventId",
            table: "AssetDepreciationSchedules");
        migrationBuilder.DropIndex(
            name: "IX_AssetDepreciationSchedules_TenantId_FixedAssetId_FiscalPeriodId_BookClassification_CorrectionSequence",
            table: "AssetDepreciationSchedules");

        migrationBuilder.DropColumn(name: "CorrectionSequence", table: "FixedAssetDepreciationRuns");
        migrationBuilder.DropColumn(name: "CorrectionSequence", table: "AssetDepreciationSchedules");
        migrationBuilder.DropColumn(name: "DepreciationReversalId", table: "AssetDepreciationSchedules");
        migrationBuilder.DropColumn(name: "IsReversed", table: "AssetDepreciationSchedules");
        migrationBuilder.DropColumn(name: "ReversalJournalEntryId", table: "AssetDepreciationSchedules");
        migrationBuilder.DropColumn(name: "ReversalPostingEventId", table: "AssetDepreciationSchedules");
        migrationBuilder.DropColumn(name: "ReversedAt", table: "AssetDepreciationSchedules");

        migrationBuilder.CreateIndex(
            name: "IX_AssetDepreciationSchedules_TenantId_FixedAssetId_FiscalPeriodId_BookClassification",
            table: "AssetDepreciationSchedules",
            columns: new[] { "TenantId", "FixedAssetId", "FiscalPeriodId", "BookClassification" },
            unique: true);
    }
}
