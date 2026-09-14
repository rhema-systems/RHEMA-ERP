using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260906050000_SeparateAwardVerificationDocumentRequirement")]
public sealed class SeparateAwardVerificationDocumentRequirement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<bool>(
            name: "RequiresDocument", table: "AwardVerificationChecklistItems",
            type: "bit", nullable: false, defaultValue: false);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "RequiresDocument", table: "AwardVerificationChecklistItems");
}
