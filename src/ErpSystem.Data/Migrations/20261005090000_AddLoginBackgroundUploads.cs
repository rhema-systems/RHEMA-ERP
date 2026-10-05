using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261005090000_AddLoginBackgroundUploads")]
public sealed class AddLoginBackgroundUploads : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "DarkLoginBackgroundFileUploadRecordId",
            table: "Securities",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "LightLoginBackgroundFileUploadRecordId",
            table: "Securities",
            type: "uniqueidentifier",
            nullable: true);

    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DarkLoginBackgroundFileUploadRecordId",
            table: "Securities");

        migrationBuilder.DropColumn(
            name: "LightLoginBackgroundFileUploadRecordId",
            table: "Securities");
    }
}
