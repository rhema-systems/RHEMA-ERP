using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260808215000_AddEstateFacilityDutyRoster")]
public partial class AddEstateFacilityDutyRoster : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EstateFacilityDutyRosters",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RosterReference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                EmployeeProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EmployeeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                StaffName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                StaffType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                DutyType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                PropertyReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                PropertyUnit = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                ServiceAreaType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                ServiceAreaName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Frequency = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                DayPattern = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                ShiftStart = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                ShiftEnd = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                SupervisorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ToolsIssued = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                SuppliesIssued = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Checklist = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                AttendanceStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                CompletionStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                QualityStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                LinkedMaintenanceReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                LinkedComplaintReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                LinkedProcedureCaseReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                LastAttendanceAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EstateFacilityDutyRosters", x => x.Id);
                table.ForeignKey(
                    name: "FK_EstateFacilityDutyRosters_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EstateFacilityDutyRosters_TenantId_AttendanceStatus_CompletionStatus",
            table: "EstateFacilityDutyRosters",
            columns: new[] { "TenantId", "AttendanceStatus", "CompletionStatus" });

        migrationBuilder.CreateIndex(
            name: "IX_EstateFacilityDutyRosters_TenantId_LinkedComplaintReference",
            table: "EstateFacilityDutyRosters",
            columns: new[] { "TenantId", "LinkedComplaintReference" });

        migrationBuilder.CreateIndex(
            name: "IX_EstateFacilityDutyRosters_TenantId_LinkedMaintenanceReference",
            table: "EstateFacilityDutyRosters",
            columns: new[] { "TenantId", "LinkedMaintenanceReference" });

        migrationBuilder.CreateIndex(
            name: "IX_EstateFacilityDutyRosters_TenantId_PropertyReference_PropertyUnit",
            table: "EstateFacilityDutyRosters",
            columns: new[] { "TenantId", "PropertyReference", "PropertyUnit" });

        migrationBuilder.CreateIndex(
            name: "IX_EstateFacilityDutyRosters_TenantId_RosterReference",
            table: "EstateFacilityDutyRosters",
            columns: new[] { "TenantId", "RosterReference" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EstateFacilityDutyRosters_TenantId_ServiceAreaType_ServiceAreaName",
            table: "EstateFacilityDutyRosters",
            columns: new[] { "TenantId", "ServiceAreaType", "ServiceAreaName" });

        migrationBuilder.CreateIndex(
            name: "IX_EstateFacilityDutyRosters_TenantId_StaffName_StartDate",
            table: "EstateFacilityDutyRosters",
            columns: new[] { "TenantId", "StaffName", "StartDate" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EstateFacilityDutyRosters");
    }
}
