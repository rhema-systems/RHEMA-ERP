using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenBudgetingAssignmentsAndIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [BudgetScenarios]
                    WHERE [IsDeleted] = 0
                    GROUP BY [TenantId], [FiscalYearId], [Name]
                    HAVING COUNT(*) > 1)
                    THROW 51000, 'Duplicate active budget scenario names must be resolved before applying this migration.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM [BudgetScenarios]
                    WHERE [IsDeleted] = 0 AND [IsActive] = 1
                    GROUP BY [TenantId], [FiscalYearId]
                    HAVING COUNT(*) > 1)
                    THROW 51000, 'Multiple active budget scenarios for a fiscal year must be resolved before applying this migration.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM [BudgetReturns]
                    WHERE [IsDeleted] = 0
                    GROUP BY [TenantId], [BudgetScenarioId], [SegmentValueId]
                    HAVING COUNT(*) > 1)
                    THROW 51000, 'Duplicate budget returns for the same scenario and segment must be resolved before applying this migration.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM [BudgetEntries]
                    WHERE [IsDeleted] = 0
                    GROUP BY [TenantId], [BudgetReturnId], [AccountId], [FiscalPeriodId]
                    HAVING COUNT(*) > 1)
                    THROW 51000, 'Duplicate budget entry cells must be resolved before applying this migration.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM [BudgetReturns] budgetReturn
                    LEFT JOIN [Users] assignedUser ON assignedUser.[Id] = budgetReturn.[AssignedToUserId]
                    LEFT JOIN [Users] approverUser ON approverUser.[Id] = budgetReturn.[ApproverUserId]
                    WHERE (budgetReturn.[AssignedToUserId] IS NOT NULL AND assignedUser.[Id] IS NULL)
                       OR (budgetReturn.[ApproverUserId] IS NOT NULL AND approverUser.[Id] IS NULL))
                    THROW 51000, 'Invalid budget return user assignments must be resolved before applying this migration.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM [BudgetScenarios] scenario
                    LEFT JOIN [Users] lockedByUser ON lockedByUser.[Id] = scenario.[LockedByUserId]
                    WHERE scenario.[LockedByUserId] IS NOT NULL AND lockedByUser.[Id] IS NULL)
                    THROW 51000, 'Invalid budget scenario lock users must be resolved before applying this migration.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_BudgetScenarios_TenantId",
                table: "BudgetScenarios");

            migrationBuilder.DropIndex(
                name: "IX_BudgetReturns_TenantId",
                table: "BudgetReturns");

            migrationBuilder.DropIndex(
                name: "IX_BudgetEntries_TenantId",
                table: "BudgetEntries");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetScenarios_LockedByUserId",
                table: "BudgetScenarios",
                column: "LockedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetScenarios_TenantId_FiscalYearId_IsActive",
                table: "BudgetScenarios",
                columns: new[] { "TenantId", "FiscalYearId", "IsActive" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetScenarios_TenantId_FiscalYearId_Name",
                table: "BudgetScenarios",
                columns: new[] { "TenantId", "FiscalYearId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetReturns_ApproverUserId",
                table: "BudgetReturns",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetReturns_AssignedToUserId",
                table: "BudgetReturns",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetReturns_TenantId_BudgetScenarioId_SegmentValueId",
                table: "BudgetReturns",
                columns: new[] { "TenantId", "BudgetScenarioId", "SegmentValueId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_TenantId_BudgetReturnId_AccountId_FiscalPeriodId",
                table: "BudgetEntries",
                columns: new[] { "TenantId", "BudgetReturnId", "AccountId", "FiscalPeriodId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetReturns_Users_ApproverUserId",
                table: "BudgetReturns",
                column: "ApproverUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetReturns_Users_AssignedToUserId",
                table: "BudgetReturns",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetScenarios_Users_LockedByUserId",
                table: "BudgetScenarios",
                column: "LockedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetReturns_Users_ApproverUserId",
                table: "BudgetReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetReturns_Users_AssignedToUserId",
                table: "BudgetReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetScenarios_Users_LockedByUserId",
                table: "BudgetScenarios");

            migrationBuilder.DropIndex(
                name: "IX_BudgetScenarios_LockedByUserId",
                table: "BudgetScenarios");

            migrationBuilder.DropIndex(
                name: "IX_BudgetScenarios_TenantId_FiscalYearId_IsActive",
                table: "BudgetScenarios");

            migrationBuilder.DropIndex(
                name: "IX_BudgetScenarios_TenantId_FiscalYearId_Name",
                table: "BudgetScenarios");

            migrationBuilder.DropIndex(
                name: "IX_BudgetReturns_ApproverUserId",
                table: "BudgetReturns");

            migrationBuilder.DropIndex(
                name: "IX_BudgetReturns_AssignedToUserId",
                table: "BudgetReturns");

            migrationBuilder.DropIndex(
                name: "IX_BudgetReturns_TenantId_BudgetScenarioId_SegmentValueId",
                table: "BudgetReturns");

            migrationBuilder.DropIndex(
                name: "IX_BudgetEntries_TenantId_BudgetReturnId_AccountId_FiscalPeriodId",
                table: "BudgetEntries");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetScenarios_TenantId",
                table: "BudgetScenarios",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetReturns_TenantId",
                table: "BudgetReturns",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_TenantId",
                table: "BudgetEntries",
                column: "TenantId");
        }
    }
}
