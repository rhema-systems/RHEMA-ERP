using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectResourceRoutingRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequiredCertificationsJson",
                table: "ProjectResourceAllocations",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredSkillsJson",
                table: "ProjectResourceAllocations",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoutingPolicy",
                table: "ProjectResourceAllocations",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiredCertificationsJson",
                table: "ProjectResourceAllocations");

            migrationBuilder.DropColumn(
                name: "RequiredSkillsJson",
                table: "ProjectResourceAllocations");

            migrationBuilder.DropColumn(
                name: "RoutingPolicy",
                table: "ProjectResourceAllocations");
        }
    }
}
