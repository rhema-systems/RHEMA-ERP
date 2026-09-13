using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260901003000_AddOpeningBalanceBatchReversals")]
public sealed class AddOpeningBalanceBatchReversals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(name: "OpeningReversalJournalEntryId", table: "FixedAssetBookValues", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "OpeningReversalPostingEventId", table: "FixedAssetBookValues", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "OpeningReversedAt", table: "FixedAssetBookValues", type: "datetime2", nullable: true);

        migrationBuilder.CreateTable(
            name: "OpeningBalanceBatchReversals",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OpeningBalanceBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OriginalPostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OriginalJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReversalPostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReversalJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SourceKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                BookClassification = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                OriginalOpeningDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                OriginalTotalDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                OriginalTotalCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OpeningBalanceBatchReversals", x => x.Id);
                table.ForeignKey("FK_OpeningBalanceBatchReversals_FinancePostingEvents_OriginalPostingEventId", x => x.OriginalPostingEventId, "FinancePostingEvents", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_OpeningBalanceBatchReversals_FinancePostingEvents_ReversalPostingEventId", x => x.ReversalPostingEventId, "FinancePostingEvents", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_OpeningBalanceBatchReversals_JournalEntries_OriginalJournalEntryId", x => x.OriginalJournalEntryId, "JournalEntries", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_OpeningBalanceBatchReversals_JournalEntries_ReversalJournalEntryId", x => x.ReversalJournalEntryId, "JournalEntries", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_OpeningBalanceBatchReversals_OpeningBalanceBatches_OpeningBalanceBatchId", x => x.OpeningBalanceBatchId, "OpeningBalanceBatches", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_OpeningBalanceBatchReversals_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_OpeningBalanceBatchReversals_OpeningBalanceBatchId", table: "OpeningBalanceBatchReversals", column: "OpeningBalanceBatchId");
        migrationBuilder.CreateIndex(name: "IX_OpeningBalanceBatchReversals_OriginalJournalEntryId", table: "OpeningBalanceBatchReversals", column: "OriginalJournalEntryId");
        migrationBuilder.CreateIndex(name: "IX_OpeningBalanceBatchReversals_OriginalPostingEventId", table: "OpeningBalanceBatchReversals", column: "OriginalPostingEventId");
        migrationBuilder.CreateIndex(name: "IX_OpeningBalanceBatchReversals_ReversalJournalEntryId", table: "OpeningBalanceBatchReversals", column: "ReversalJournalEntryId");
        migrationBuilder.CreateIndex(name: "IX_OpeningBalanceBatchReversals_ReversalPostingEventId", table: "OpeningBalanceBatchReversals", column: "ReversalPostingEventId");
        migrationBuilder.CreateIndex(
            name: "IX_OpeningBalanceBatchReversals_TenantId_OpeningBalanceBatchId",
            table: "OpeningBalanceBatchReversals",
            columns: new[] { "TenantId", "OpeningBalanceBatchId" },
            unique: true,
            filter: "[IsDeleted] = 0 AND [Status] <> N'Rejected'");
        migrationBuilder.CreateIndex(name: "IX_OpeningBalanceBatchReversals_TenantId_Status", table: "OpeningBalanceBatchReversals", columns: new[] { "TenantId", "Status" });
        migrationBuilder.CreateIndex(name: "IX_OpeningBalanceBatchReversals_TenantId_ReversalPostingEventId", table: "OpeningBalanceBatchReversals", columns: new[] { "TenantId", "ReversalPostingEventId" });
        migrationBuilder.CreateIndex(name: "IX_OpeningBalanceBatchReversals_TenantId", table: "OpeningBalanceBatchReversals", column: "TenantId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OpeningBalanceBatchReversals");
        migrationBuilder.DropColumn(name: "OpeningReversalJournalEntryId", table: "FixedAssetBookValues");
        migrationBuilder.DropColumn(name: "OpeningReversalPostingEventId", table: "FixedAssetBookValues");
        migrationBuilder.DropColumn(name: "OpeningReversedAt", table: "FixedAssetBookValues");
    }
}
