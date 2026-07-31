using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260728210000_AddBudgetOfficialAdoption")]
    public partial class AddBudgetOfficialAdoption : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AdoptedAt",
                table: "BudgetScenarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AdoptedByUserId",
                table: "BudgetScenarios",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AdoptionEffectiveDate",
                table: "BudgetScenarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdoptionReason",
                table: "BudgetScenarios",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SupersededAt",
                table: "BudgetScenarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersededByUserId",
                table: "BudgetScenarios",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupersessionReason",
                table: "BudgetScenarios",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetScenarios_AdoptedByUserId",
                table: "BudgetScenarios",
                column: "AdoptedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetScenarios_SupersededByUserId",
                table: "BudgetScenarios",
                column: "SupersededByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetScenarios_Users_AdoptedByUserId",
                table: "BudgetScenarios",
                column: "AdoptedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetScenarios_Users_SupersededByUserId",
                table: "BudgetScenarios",
                column: "SupersededByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetScenarios_Users_AdoptedByUserId",
                table: "BudgetScenarios");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetScenarios_Users_SupersededByUserId",
                table: "BudgetScenarios");

            migrationBuilder.DropIndex(
                name: "IX_BudgetScenarios_AdoptedByUserId",
                table: "BudgetScenarios");

            migrationBuilder.DropIndex(
                name: "IX_BudgetScenarios_SupersededByUserId",
                table: "BudgetScenarios");

            migrationBuilder.DropColumn(name: "AdoptedAt", table: "BudgetScenarios");
            migrationBuilder.DropColumn(name: "AdoptedByUserId", table: "BudgetScenarios");
            migrationBuilder.DropColumn(name: "AdoptionEffectiveDate", table: "BudgetScenarios");
            migrationBuilder.DropColumn(name: "AdoptionReason", table: "BudgetScenarios");
            migrationBuilder.DropColumn(name: "SupersededAt", table: "BudgetScenarios");
            migrationBuilder.DropColumn(name: "SupersededByUserId", table: "BudgetScenarios");
            migrationBuilder.DropColumn(name: "SupersessionReason", table: "BudgetScenarios");
        }
    }
}
