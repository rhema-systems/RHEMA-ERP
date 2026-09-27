using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260925000000_AddEstateFacilityDutyAttendance")]
public partial class AddEstateFacilityDutyAttendance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EstateFacilityDutyAttendances",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DutyRosterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DutyDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                AttendanceStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                CompletionStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                QualityStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                LinkedMaintenanceReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                LinkedComplaintReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                table.PrimaryKey("PK_EstateFacilityDutyAttendances", x => x.Id);
                table.ForeignKey(
                    name: "FK_EstateFacilityDutyAttendances_EstateFacilityDutyRosters_DutyRosterId",
                    column: x => x.DutyRosterId,
                    principalTable: "EstateFacilityDutyRosters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EstateFacilityDutyAttendances_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EstateFacilityDutyAttendances_TenantId_DutyRosterId_DutyDate",
            table: "EstateFacilityDutyAttendances",
            columns: new[] { "TenantId", "DutyRosterId", "DutyDate" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EstateFacilityDutyAttendances_DutyRosterId",
            table: "EstateFacilityDutyAttendances",
            column: "DutyRosterId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "EstateFacilityDutyAttendances");
    }
}
