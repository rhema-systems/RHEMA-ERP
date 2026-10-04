using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260929123000_AddEstateDutyRosterSupervisorEmployee")]
    public partial class AddEstateDutyRosterSupervisorEmployee : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SupervisorEmployeeId",
                table: "EstateFacilityDutyRosters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EstateFacilityDutyRosters_TenantId_SupervisorEmployeeId",
                table: "EstateFacilityDutyRosters",
                columns: new[] { "TenantId", "SupervisorEmployeeId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EstateFacilityDutyRosters_TenantId_SupervisorEmployeeId",
                table: "EstateFacilityDutyRosters");

            migrationBuilder.DropColumn(
                name: "SupervisorEmployeeId",
                table: "EstateFacilityDutyRosters");
        }
    }
}
