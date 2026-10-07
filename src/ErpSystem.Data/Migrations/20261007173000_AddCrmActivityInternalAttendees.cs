using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261007173000_AddCrmActivityInternalAttendees")]
public sealed class AddCrmActivityInternalAttendees : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "InternalAttendeeEmployeeIdsJson",
            table: "CrmActivities",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "InternalAttendeeEmployeeIdsJson",
            table: "CrmActivities");
    }
}
