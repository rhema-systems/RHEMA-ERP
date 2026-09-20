using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260707103000_AddFixedAssetDepreciationFoundation")]
    public partial class AddFixedAssetDepreciationFoundation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FixedAssetDepreciationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FixedAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BookClassification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PostingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TotalDepreciationAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_FixedAssetDepreciationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FixedAssetDepreciationRuns_FinancePostingEvents_PostingEventId",
                        column: x => x.PostingEventId,
                        principalTable: "FinancePostingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetDepreciationRuns_FiscalPeriods_FiscalPeriodId",
                        column: x => x.FiscalPeriodId,
                        principalTable: "FiscalPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetDepreciationRuns_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalTable: "FixedAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetDepreciationRuns_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetDepreciationRuns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<decimal>(
                name: "AccumulatedDepreciationBefore",
                table: "AssetDepreciationSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DepreciableAmount",
                table: "AssetDepreciationSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DepreciationMethodSnapshot",
                table: "AssetDepreciationSchedules",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "FixedAssetDepreciationRunId",
                table: "AssetDepreciationSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetBookValueBefore",
                table: "AssetDepreciationSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlacedInServiceDateSnapshot",
                table: "AssetDepreciationSchedules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PostingDate",
                table: "AssetDepreciationSchedules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PostingEventId",
                table: "AssetDepreciationSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ResidualValueSnapshot",
                table: "AssetDepreciationSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "UsefulLifeMonthsSnapshot",
                table: "AssetDepreciationSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDepreciationRuns_FiscalPeriodId",
                table: "FixedAssetDepreciationRuns",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDepreciationRuns_FixedAssetId",
                table: "FixedAssetDepreciationRuns",
                column: "FixedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDepreciationRuns_JournalEntryId",
                table: "FixedAssetDepreciationRuns",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDepreciationRuns_PostingEventId",
                table: "FixedAssetDepreciationRuns",
                column: "PostingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDepreciationRuns_TenantId",
                table: "FixedAssetDepreciationRuns",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDepreciationRuns_TenantId_FiscalPeriodId_BookClassification",
                table: "FixedAssetDepreciationRuns",
                columns: new[] { "TenantId", "FiscalPeriodId", "BookClassification" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDepreciationRuns_TenantId_IdempotencyKey",
                table: "FixedAssetDepreciationRuns",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDepreciationRuns_TenantId_PostingEventId",
                table: "FixedAssetDepreciationRuns",
                columns: new[] { "TenantId", "PostingEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationSchedules_FixedAssetDepreciationRunId",
                table: "AssetDepreciationSchedules",
                column: "FixedAssetDepreciationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationSchedules_PostingEventId",
                table: "AssetDepreciationSchedules",
                column: "PostingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationSchedules_TenantId_FixedAssetDepreciationRunId",
                table: "AssetDepreciationSchedules",
                columns: new[] { "TenantId", "FixedAssetDepreciationRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationSchedules_TenantId_JournalEntryId",
                table: "AssetDepreciationSchedules",
                columns: new[] { "TenantId", "JournalEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationSchedules_TenantId_PostingEventId",
                table: "AssetDepreciationSchedules",
                columns: new[] { "TenantId", "PostingEventId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AssetDepreciationSchedules_FinancePostingEvents_PostingEventId",
                table: "AssetDepreciationSchedules",
                column: "PostingEventId",
                principalTable: "FinancePostingEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssetDepreciationSchedules_FixedAssetDepreciationRuns_FixedAssetDepreciationRunId",
                table: "AssetDepreciationSchedules",
                column: "FixedAssetDepreciationRunId",
                principalTable: "FixedAssetDepreciationRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetDepreciationSchedules_FinancePostingEvents_PostingEventId",
                table: "AssetDepreciationSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_AssetDepreciationSchedules_FixedAssetDepreciationRuns_FixedAssetDepreciationRunId",
                table: "AssetDepreciationSchedules");

            migrationBuilder.DropTable(
                name: "FixedAssetDepreciationRuns");

            migrationBuilder.DropIndex(
                name: "IX_AssetDepreciationSchedules_FixedAssetDepreciationRunId",
                table: "AssetDepreciationSchedules");

            migrationBuilder.DropIndex(
                name: "IX_AssetDepreciationSchedules_PostingEventId",
                table: "AssetDepreciationSchedules");

            migrationBuilder.DropIndex(
                name: "IX_AssetDepreciationSchedules_TenantId_FixedAssetDepreciationRunId",
                table: "AssetDepreciationSchedules");

            migrationBuilder.DropIndex(
                name: "IX_AssetDepreciationSchedules_TenantId_JournalEntryId",
                table: "AssetDepreciationSchedules");

            migrationBuilder.DropIndex(
                name: "IX_AssetDepreciationSchedules_TenantId_PostingEventId",
                table: "AssetDepreciationSchedules");

            migrationBuilder.DropColumn(
                name: "AccumulatedDepreciationBefore",
                table: "AssetDepreciationSchedules");

            migrationBuilder.DropColumn(
                name: "DepreciableAmount",
                table: "AssetDepreciationSchedules");

            migrationBuilder.DropColumn(
                name: "DepreciationMethodSnapshot",
                table: "AssetDepreciationSchedules");

            migrationBuilder.DropColumn(
                name: "FixedAssetDepreciationRunId",
                table: "AssetDepreciationSchedules");

            migrationBuilder.DropColumn(
                name: "NetBookValueBefore",
                table: "AssetDepreciationSchedules");

            migrationBuilder.DropColumn(
                name: "PlacedInServiceDateSnapshot",
                table: "AssetDepreciationSchedules");

            migrationBuilder.DropColumn(
                name: "PostingDate",
                table: "AssetDepreciationSchedules");

            migrationBuilder.DropColumn(
                name: "PostingEventId",
                table: "AssetDepreciationSchedules");

            migrationBuilder.DropColumn(
                name: "ResidualValueSnapshot",
                table: "AssetDepreciationSchedules");

            migrationBuilder.DropColumn(
                name: "UsefulLifeMonthsSnapshot",
                table: "AssetDepreciationSchedules");
        }
    }
}
