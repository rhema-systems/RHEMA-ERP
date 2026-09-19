using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Adds the FIN-LIM-0030 fixed-asset correction evidence and maker-checker register.
    /// This migration is deliberately hand-scoped, matching the recent Finance migration
    /// convention: the shared model snapshot remains authoritative while avoiding another very
    /// large generated designer that materially increases clean-build memory and compile time.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260807135347_AddFixedAssetCapitalizationReversalControls")]
    public sealed class AddFixedAssetCapitalizationReversalControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep this migration limited to FIN-LIM-0030. Foreign-currency allocation precision
            // changes belong to the separate cross-currency settlement migration/PR and must not
            // be replayed here when the two branches are eventually merged.
            migrationBuilder.AddColumn<Guid>(
                name: "CapitalizationReversalJournalEntryId",
                table: "VendorInvoiceLineItem",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CapitalizationReversalPostingEventId",
                table: "VendorInvoiceLineItem",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CapitalizationReversedAt",
                table: "VendorInvoiceLineItem",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CapitalizationReversalJournalEntryId",
                table: "FixedAssets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CapitalizationReversalPostingEventId",
                table: "FixedAssets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CapitalizationReversalReason",
                table: "FixedAssets",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CapitalizationReversedAt",
                table: "FixedAssets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CapitalizationReversalJournalEntryId",
                table: "FixedAssetBookValues",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CapitalizationReversalPostingEventId",
                table: "FixedAssetBookValues",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CapitalizationReversedAt",
                table: "FixedAssetBookValues",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FixedAssetCapitalizationReversals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FixedAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_FixedAssetCapitalizationReversals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FixedAssetCapitalizationReversals_FinancePostingEvents_OriginalPostingEventId",
                        column: x => x.OriginalPostingEventId,
                        principalTable: "FinancePostingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCapitalizationReversals_FinancePostingEvents_ReversalPostingEventId",
                        column: x => x.ReversalPostingEventId,
                        principalTable: "FinancePostingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCapitalizationReversals_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalTable: "FixedAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCapitalizationReversals_JournalEntries_OriginalJournalEntryId",
                        column: x => x.OriginalJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCapitalizationReversals_JournalEntries_ReversalJournalEntryId",
                        column: x => x.ReversalJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCapitalizationReversals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetBookValues_CapitalizationReversalJournalEntryId",
                table: "FixedAssetBookValues",
                column: "CapitalizationReversalJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetBookValues_CapitalizationReversalPostingEventId",
                table: "FixedAssetBookValues",
                column: "CapitalizationReversalPostingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetBookValues_TenantId_CapitalizationReversalJournalEntryId",
                table: "FixedAssetBookValues",
                columns: new[] { "TenantId", "CapitalizationReversalJournalEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetBookValues_TenantId_CapitalizationReversalPostingEventId",
                table: "FixedAssetBookValues",
                columns: new[] { "TenantId", "CapitalizationReversalPostingEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCapitalizationReversals_FixedAssetId",
                table: "FixedAssetCapitalizationReversals",
                column: "FixedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCapitalizationReversals_OriginalJournalEntryId",
                table: "FixedAssetCapitalizationReversals",
                column: "OriginalJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCapitalizationReversals_OriginalPostingEventId",
                table: "FixedAssetCapitalizationReversals",
                column: "OriginalPostingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCapitalizationReversals_ReversalJournalEntryId",
                table: "FixedAssetCapitalizationReversals",
                column: "ReversalJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCapitalizationReversals_ReversalPostingEventId",
                table: "FixedAssetCapitalizationReversals",
                column: "ReversalPostingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCapitalizationReversals_TenantId_FixedAssetId_Status",
                table: "FixedAssetCapitalizationReversals",
                columns: new[] { "TenantId", "FixedAssetId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCapitalizationReversals_TenantId_OriginalPostingEventId",
                table: "FixedAssetCapitalizationReversals",
                columns: new[] { "TenantId", "OriginalPostingEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCapitalizationReversals_TenantId_ReversalPostingEventId",
                table: "FixedAssetCapitalizationReversals",
                columns: new[] { "TenantId", "ReversalPostingEventId" },
                unique: true,
                filter: "[ReversalPostingEventId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_FixedAssetBookValues_FinancePostingEvents_CapitalizationReversalPostingEventId",
                table: "FixedAssetBookValues",
                column: "CapitalizationReversalPostingEventId",
                principalTable: "FinancePostingEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FixedAssetBookValues_JournalEntries_CapitalizationReversalJournalEntryId",
                table: "FixedAssetBookValues",
                column: "CapitalizationReversalJournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FixedAssetBookValues_FinancePostingEvents_CapitalizationReversalPostingEventId",
                table: "FixedAssetBookValues");

            migrationBuilder.DropForeignKey(
                name: "FK_FixedAssetBookValues_JournalEntries_CapitalizationReversalJournalEntryId",
                table: "FixedAssetBookValues");

            migrationBuilder.DropTable(
                name: "FixedAssetCapitalizationReversals");

            migrationBuilder.DropIndex(
                name: "IX_FixedAssetBookValues_CapitalizationReversalJournalEntryId",
                table: "FixedAssetBookValues");

            migrationBuilder.DropIndex(
                name: "IX_FixedAssetBookValues_CapitalizationReversalPostingEventId",
                table: "FixedAssetBookValues");

            migrationBuilder.DropIndex(
                name: "IX_FixedAssetBookValues_TenantId_CapitalizationReversalJournalEntryId",
                table: "FixedAssetBookValues");

            migrationBuilder.DropIndex(
                name: "IX_FixedAssetBookValues_TenantId_CapitalizationReversalPostingEventId",
                table: "FixedAssetBookValues");

            migrationBuilder.DropColumn(
                name: "CapitalizationReversalJournalEntryId",
                table: "VendorInvoiceLineItem");

            migrationBuilder.DropColumn(
                name: "CapitalizationReversalPostingEventId",
                table: "VendorInvoiceLineItem");

            migrationBuilder.DropColumn(
                name: "CapitalizationReversedAt",
                table: "VendorInvoiceLineItem");

            migrationBuilder.DropColumn(
                name: "CapitalizationReversalJournalEntryId",
                table: "FixedAssets");

            migrationBuilder.DropColumn(
                name: "CapitalizationReversalPostingEventId",
                table: "FixedAssets");

            migrationBuilder.DropColumn(
                name: "CapitalizationReversalReason",
                table: "FixedAssets");

            migrationBuilder.DropColumn(
                name: "CapitalizationReversedAt",
                table: "FixedAssets");

            migrationBuilder.DropColumn(
                name: "CapitalizationReversalJournalEntryId",
                table: "FixedAssetBookValues");

            migrationBuilder.DropColumn(
                name: "CapitalizationReversalPostingEventId",
                table: "FixedAssetBookValues");

            migrationBuilder.DropColumn(
                name: "CapitalizationReversedAt",
                table: "FixedAssetBookValues");

        }
    }
}
