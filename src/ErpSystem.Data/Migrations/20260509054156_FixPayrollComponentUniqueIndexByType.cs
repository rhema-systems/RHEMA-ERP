using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixPayrollComponentUniqueIndexByType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollComponents_TenantId_Code",
                table: "PayrollComponents");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollComponents_TenantId_ComponentType_Code",
                table: "PayrollComponents",
                columns: new[] { "TenantId", "ComponentType", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollComponents_TenantId_ComponentType_Code",
                table: "PayrollComponents");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollComponents_TenantId_Code",
                table: "PayrollComponents",
                columns: new[] { "TenantId", "Code" },
                unique: true);
        }
    }
}
