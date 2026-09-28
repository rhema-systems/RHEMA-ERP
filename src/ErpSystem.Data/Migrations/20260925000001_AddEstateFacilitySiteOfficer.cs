using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260925000001_AddEstateFacilitySiteOfficer")]
public partial class AddEstateFacilitySiteOfficer : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ResponsibleOfficerEmployeeId", table: "EstateManagedAssets",
            type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "ResponsibleOfficerEmployeeNumber", table: "EstateManagedAssets",
            type: "nvarchar(80)", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "ResponsibleOfficerName", table: "EstateManagedAssets",
            type: "nvarchar(240)", maxLength: 240, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ResponsibleOfficerEmployeeId", table: "EstateManagedAssets");
        migrationBuilder.DropColumn(name: "ResponsibleOfficerEmployeeNumber", table: "EstateManagedAssets");
        migrationBuilder.DropColumn(name: "ResponsibleOfficerName", table: "EstateManagedAssets");
    }
}
