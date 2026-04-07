using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectUnitReleaseControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReleasedForMarket",
                table: "ProjectUnits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleasedAt",
                table: "ProjectUnits",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReleasedById",
                table: "ProjectUnits",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReleasedForMarket",
                table: "ProjectUnits");

            migrationBuilder.DropColumn(
                name: "ReleasedAt",
                table: "ProjectUnits");

            migrationBuilder.DropColumn(
                name: "ReleasedById",
                table: "ProjectUnits");
        }
    }
}
