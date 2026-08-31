using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    // This repository omits generated migration designers from focused Finance migrations.
    // Keep discovery metadata on the executable migration for Debug and Release builds.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260830232009_AddFinanceSourceDimensionsToApAr")]
    public partial class AddFinanceSourceDimensionsToApAr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "FinanceDimensionSetId",
                table: "FinanceSourceDimensionAssignments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "BudgetEvaluationHash",
                table: "FinanceSourceDimensionAssignments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BudgetEvidenceStatus",
                table: "FinanceSourceDimensionAssignments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotApplicable");

            migrationBuilder.AddColumn<DateTime>(
                name: "BudgetEvidenceUpdatedAt",
                table: "FinanceSourceDimensionAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EvidenceFrozenAt",
                table: "FinanceSourceDimensionAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE assignment
                SET assignment.[EvidenceFrozenAt] = COALESCE(snapshot.[SnapshotCapturedAt], assignment.[UpdatedAt], assignment.[CreatedAt])
                FROM [FinanceSourceDimensionAssignments] assignment
                LEFT JOIN [FinanceDimensionSnapshots] snapshot
                    ON snapshot.[Id] = assignment.[FinanceDimensionSnapshotId]
                WHERE assignment.[FinanceDimensionSnapshotId] IS NOT NULL;
                """);

            migrationBuilder.CreateTable(
                name: "FinanceSourceDimensionChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RouteId = table.Column<int>(type: "int", nullable: false),
                    ProducerModule = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SourceRoute = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SourceDocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContractVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreviousFinanceDimensionSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewFinanceDimensionSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreviousFinanceDimensionSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewFinanceDimensionSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreviousCombinationHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    NewCombinationHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BudgetEvidenceBecameStale = table.Column<bool>(type: "bit", nullable: false),
                    ReleasedBudgetReservationIdsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_FinanceSourceDimensionChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceSourceDimensionChanges_FinanceDimensionSets_NewFinanceDimensionSetId",
                        column: x => x.NewFinanceDimensionSetId,
                        principalTable: "FinanceDimensionSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceSourceDimensionChanges_FinanceDimensionSets_PreviousFinanceDimensionSetId",
                        column: x => x.PreviousFinanceDimensionSetId,
                        principalTable: "FinanceDimensionSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceSourceDimensionChanges_FinanceDimensionSnapshots_NewFinanceDimensionSnapshotId",
                        column: x => x.NewFinanceDimensionSnapshotId,
                        principalTable: "FinanceDimensionSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceSourceDimensionChanges_FinanceDimensionSnapshots_PreviousFinanceDimensionSnapshotId",
                        column: x => x.PreviousFinanceDimensionSnapshotId,
                        principalTable: "FinanceDimensionSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceSourceDimensionChanges_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSourceDimensionChanges_NewFinanceDimensionSetId",
                table: "FinanceSourceDimensionChanges",
                column: "NewFinanceDimensionSetId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSourceDimensionChanges_NewFinanceDimensionSnapshotId",
                table: "FinanceSourceDimensionChanges",
                column: "NewFinanceDimensionSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSourceDimensionChanges_PreviousFinanceDimensionSetId",
                table: "FinanceSourceDimensionChanges",
                column: "PreviousFinanceDimensionSetId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSourceDimensionChanges_PreviousFinanceDimensionSnapshotId",
                table: "FinanceSourceDimensionChanges",
                column: "PreviousFinanceDimensionSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSourceDimensionChanges_TenantId_RouteId_SourceDocumentId_ChangedAt",
                table: "FinanceSourceDimensionChanges",
                columns: new[] { "TenantId", "RouteId", "SourceDocumentId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSourceDimensionChanges_TenantId_SourceDocumentType_SourceDocumentId_SourceLineId",
                table: "FinanceSourceDimensionChanges",
                columns: new[] { "TenantId", "SourceDocumentType", "SourceDocumentId", "SourceLineId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinanceSourceDimensionChanges");

            migrationBuilder.DropColumn(
                name: "BudgetEvaluationHash",
                table: "FinanceSourceDimensionAssignments");

            migrationBuilder.DropColumn(
                name: "BudgetEvidenceStatus",
                table: "FinanceSourceDimensionAssignments");

            migrationBuilder.DropColumn(
                name: "BudgetEvidenceUpdatedAt",
                table: "FinanceSourceDimensionAssignments");

            migrationBuilder.DropColumn(
                name: "EvidenceFrozenAt",
                table: "FinanceSourceDimensionAssignments");

            migrationBuilder.Sql(
                "DELETE FROM [FinanceSourceDimensionAssignments] WHERE [FinanceDimensionSetId] IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "FinanceDimensionSetId",
                table: "FinanceSourceDimensionAssignments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
