using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeImportUpdateMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreateCount",
                table: "EmployeeImportSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CreatedCount",
                table: "EmployeeImportSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Sessions that exist already were create-only (EmployeeImportMode.CreateOnly = 1);
            // 0 is not a value of the enum.
            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "EmployeeImportSessions",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "UpdateCount",
                table: "EmployeeImportSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedCount",
                table: "EmployeeImportSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Existing rows were all creates (EmployeeImportRowAction.Create = 1).
            migrationBuilder.AddColumn<int>(
                name: "Action",
                table: "EmployeeImportRows",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetEmployeeId",
                table: "EmployeeImportRows",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeImportRows_TargetEmployeeId",
                table: "EmployeeImportRows",
                column: "TargetEmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmployeeImportRows_TargetEmployeeId",
                table: "EmployeeImportRows");

            migrationBuilder.DropColumn(
                name: "CreateCount",
                table: "EmployeeImportSessions");

            migrationBuilder.DropColumn(
                name: "CreatedCount",
                table: "EmployeeImportSessions");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "EmployeeImportSessions");

            migrationBuilder.DropColumn(
                name: "UpdateCount",
                table: "EmployeeImportSessions");

            migrationBuilder.DropColumn(
                name: "UpdatedCount",
                table: "EmployeeImportSessions");

            migrationBuilder.DropColumn(
                name: "Action",
                table: "EmployeeImportRows");

            migrationBuilder.DropColumn(
                name: "TargetEmployeeId",
                table: "EmployeeImportRows");
        }
    }
}
