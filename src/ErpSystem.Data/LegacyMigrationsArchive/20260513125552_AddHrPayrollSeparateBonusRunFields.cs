using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHrPayrollSeparateBonusRunFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSeparateBonusRun",
                table: "PayrollRuns",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SeparateBonusCode",
                table: "PayrollRuns",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSeparateBonusRun",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "SeparateBonusCode",
                table: "PayrollRuns");
        }
    }
}
