using System;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds IAS 36 impairment-reversal evidence and the maker-checker compensating-journal register
/// for posted valuation corrections. This migration is intentionally scoped because the repository's
/// broad design-time model currently contains unrelated snapshot drift that must not be deployed here.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260809193000_AddFixedAssetValuationCorrectionControls")]
public partial class AddFixedAssetValuationCorrectionControls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("SourceImpairmentValuationId", "AssetValuations", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<decimal>("OutstandingImpairmentBefore", "AssetValuations", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("UnimpairedCarryingAmountCap", "AssetValuations", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<int>("UsefulLifeMonthsBefore", "AssetValuations", "int", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>("RemainingUsefulLifeMonthsBefore", "AssetValuations", "int", nullable: true);
        migrationBuilder.AddColumn<bool>("IsCorrected", "AssetValuations", "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<Guid>("CorrectionId", "AssetValuations", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<DateTime>("CorrectedAt", "AssetValuations", "datetime2", nullable: true);

        migrationBuilder.CreateTable(
            name: "AssetValuationCorrections",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OriginalValuationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AssetValuationCorrections", x => x.Id);
                table.ForeignKey("FK_AssetValuationCorrections_AssetValuations_OriginalValuationId", x => x.OriginalValuationId, "AssetValuations", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_AssetValuationCorrections_FinancePostingEvents_OriginalPostingEventId", x => x.OriginalPostingEventId, "FinancePostingEvents", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_AssetValuationCorrections_FinancePostingEvents_ReversalPostingEventId", x => x.ReversalPostingEventId, "FinancePostingEvents", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_AssetValuationCorrections_JournalEntries_OriginalJournalEntryId", x => x.OriginalJournalEntryId, "JournalEntries", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_AssetValuationCorrections_JournalEntries_ReversalJournalEntryId", x => x.ReversalJournalEntryId, "JournalEntries", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_AssetValuationCorrections_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_AssetValuations_SourceImpairmentValuationId", "AssetValuations", "SourceImpairmentValuationId");
        migrationBuilder.AddForeignKey("FK_AssetValuations_AssetValuations_SourceImpairmentValuationId", "AssetValuations", "SourceImpairmentValuationId", "AssetValuations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.CreateIndex("IX_AssetValuationCorrections_OriginalJournalEntryId", "AssetValuationCorrections", "OriginalJournalEntryId");
        migrationBuilder.CreateIndex("IX_AssetValuationCorrections_OriginalPostingEventId", "AssetValuationCorrections", "OriginalPostingEventId");
        migrationBuilder.CreateIndex("IX_AssetValuationCorrections_OriginalValuationId", "AssetValuationCorrections", "OriginalValuationId");
        migrationBuilder.CreateIndex("IX_AssetValuationCorrections_ReversalJournalEntryId", "AssetValuationCorrections", "ReversalJournalEntryId");
        migrationBuilder.CreateIndex("IX_AssetValuationCorrections_ReversalPostingEventId", "AssetValuationCorrections", "ReversalPostingEventId");
        migrationBuilder.CreateIndex("IX_AssetValuationCorrections_TenantId_OriginalValuationId", "AssetValuationCorrections", new[] { "TenantId", "OriginalValuationId" });
        migrationBuilder.CreateIndex("IX_AssetValuationCorrections_TenantId_ReversalPostingEventId", "AssetValuationCorrections", new[] { "TenantId", "ReversalPostingEventId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("AssetValuationCorrections");
        migrationBuilder.DropForeignKey("FK_AssetValuations_AssetValuations_SourceImpairmentValuationId", "AssetValuations");
        migrationBuilder.DropIndex("IX_AssetValuations_SourceImpairmentValuationId", "AssetValuations");
        migrationBuilder.DropColumn("SourceImpairmentValuationId", "AssetValuations");
        migrationBuilder.DropColumn("OutstandingImpairmentBefore", "AssetValuations");
        migrationBuilder.DropColumn("UnimpairedCarryingAmountCap", "AssetValuations");
        migrationBuilder.DropColumn("UsefulLifeMonthsBefore", "AssetValuations");
        migrationBuilder.DropColumn("RemainingUsefulLifeMonthsBefore", "AssetValuations");
        migrationBuilder.DropColumn("IsCorrected", "AssetValuations");
        migrationBuilder.DropColumn("CorrectionId", "AssetValuations");
        migrationBuilder.DropColumn("CorrectedAt", "AssetValuations");
    }
}
