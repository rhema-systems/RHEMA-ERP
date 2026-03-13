using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectResourceSubstitutionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReplacementAllocationId",
                table: "ProjectResourceAllocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceAllocationId",
                table: "ProjectResourceAllocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubstitutionReason",
                table: "ProjectResourceAllocations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReplacementAllocationId",
                table: "ProjectResourceAllocations");

            migrationBuilder.DropColumn(
                name: "SourceAllocationId",
                table: "ProjectResourceAllocations");

            migrationBuilder.DropColumn(
                name: "SubstitutionReason",
                table: "ProjectResourceAllocations");
        }
    }
}
