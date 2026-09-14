using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260825235055_AddFinanceBudgetControlDimensions")]
    /// <inheritdoc />
    public class AddFinanceBudgetControlDimensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BudgetEntries_TenantId_BudgetReturnId_AccountId_FiscalPeriodId",
                table: "BudgetEntries");

            migrationBuilder.AddColumn<string>(
                name: "DimensionCombinationHashSnapshot",
                table: "FinanceBudgetReservations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceDimensionSetId",
                table: "FinanceBudgetReservations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceDimensionSetId",
                table: "BudgetEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BudgetScenarioControlDimensions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BudgetScenarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceDimensionDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_BudgetScenarioControlDimensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetScenarioControlDimensions_BudgetScenarios_BudgetScenarioId",
                        column: x => x.BudgetScenarioId,
                        principalTable: "BudgetScenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetScenarioControlDimensions_FinanceDimensionDefinitions_FinanceDimensionDefinitionId",
                        column: x => x.FinanceDimensionDefinitionId,
                        principalTable: "FinanceDimensionDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetScenarioControlDimensions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBudgetReservations_FinanceDimensionSetId",
                table: "FinanceBudgetReservations",
                column: "FinanceDimensionSetId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBudgetReservations_TenantId_FinanceDimensionSetId_Status",
                table: "FinanceBudgetReservations",
                columns: new[] { "TenantId", "FinanceDimensionSetId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_FinanceDimensionSetId",
                table: "BudgetEntries",
                column: "FinanceDimensionSetId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_TenantId_BudgetReturnId_AccountId_FiscalPeriodId_FinanceDimensionSetId",
                table: "BudgetEntries",
                columns: new[] { "TenantId", "BudgetReturnId", "AccountId", "FiscalPeriodId", "FinanceDimensionSetId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetScenarioControlDimensions_BudgetScenarioId",
                table: "BudgetScenarioControlDimensions",
                column: "BudgetScenarioId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetScenarioControlDimensions_FinanceDimensionDefinitionId",
                table: "BudgetScenarioControlDimensions",
                column: "FinanceDimensionDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetScenarioControlDimensions_TenantId_BudgetScenarioId_FinanceDimensionDefinitionId",
                table: "BudgetScenarioControlDimensions",
                columns: new[] { "TenantId", "BudgetScenarioId", "FinanceDimensionDefinitionId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_FinanceDimensionSets_FinanceDimensionSetId",
                table: "BudgetEntries",
                column: "FinanceDimensionSetId",
                principalTable: "FinanceDimensionSets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FinanceBudgetReservations_FinanceDimensionSets_FinanceDimensionSetId",
                table: "FinanceBudgetReservations",
                column: "FinanceDimensionSetId",
                principalTable: "FinanceDimensionSets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_FinanceDimensionSets_FinanceDimensionSetId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_FinanceBudgetReservations_FinanceDimensionSets_FinanceDimensionSetId",
                table: "FinanceBudgetReservations");

            migrationBuilder.DropTable(
                name: "BudgetScenarioControlDimensions");

            migrationBuilder.DropIndex(
                name: "IX_FinanceBudgetReservations_FinanceDimensionSetId",
                table: "FinanceBudgetReservations");

            migrationBuilder.DropIndex(
                name: "IX_FinanceBudgetReservations_TenantId_FinanceDimensionSetId_Status",
                table: "FinanceBudgetReservations");

            migrationBuilder.DropIndex(
                name: "IX_BudgetEntries_FinanceDimensionSetId",
                table: "BudgetEntries");

            migrationBuilder.DropIndex(
                name: "IX_BudgetEntries_TenantId_BudgetReturnId_AccountId_FiscalPeriodId_FinanceDimensionSetId",
                table: "BudgetEntries");

            migrationBuilder.DropColumn(
                name: "DimensionCombinationHashSnapshot",
                table: "FinanceBudgetReservations");

            migrationBuilder.DropColumn(
                name: "FinanceDimensionSetId",
                table: "FinanceBudgetReservations");

            migrationBuilder.DropColumn(
                name: "FinanceDimensionSetId",
                table: "BudgetEntries");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_TenantId_BudgetReturnId_AccountId_FiscalPeriodId",
                table: "BudgetEntries",
                columns: new[] { "TenantId", "BudgetReturnId", "AccountId", "FiscalPeriodId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
