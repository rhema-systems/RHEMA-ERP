using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class PointTechnicianTeamMembersToEmployees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TechnicianTeamMembers_Technicians_TechnicianId",
                table: "TechnicianTeamMembers");

            migrationBuilder.Sql("""
                UPDATE ttm
                SET TechnicianId = tech.EmployeeId
                FROM dbo.TechnicianTeamMembers ttm
                INNER JOIN dbo.Technicians tech ON tech.Id = ttm.TechnicianId
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM dbo.Employees emp
                    WHERE emp.Id = ttm.TechnicianId
                );
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_TechnicianTeamMembers_Employees_TechnicianId",
                table: "TechnicianTeamMembers",
                column: "TechnicianId",
                principalTable: "Employees",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TechnicianTeamMembers_Employees_TechnicianId",
                table: "TechnicianTeamMembers");

            migrationBuilder.Sql("""
                UPDATE ttm
                SET TechnicianId = tech.Id
                FROM dbo.TechnicianTeamMembers ttm
                INNER JOIN dbo.Technicians tech ON tech.EmployeeId = ttm.TechnicianId;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_TechnicianTeamMembers_Technicians_TechnicianId",
                table: "TechnicianTeamMembers",
                column: "TechnicianId",
                principalTable: "Technicians",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
