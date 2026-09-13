using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectBoqBudgetWorksheetFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BudgetQuantity",
                table: "ProjectBoqItems",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BudgetUnitRate",
                table: "ProjectBoqItems",
                type: "decimal(18,4)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BudgetQuantity",
                table: "ProjectBoqItems");

            migrationBuilder.DropColumn(
                name: "BudgetUnitRate",
                table: "ProjectBoqItems");
        }
    }
}
