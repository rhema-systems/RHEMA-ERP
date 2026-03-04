using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class _20260213_AddEhcRootCauseCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResolutionSummary",
                table: "EhcTickets",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RootCauseDetails",
                table: "EhcTickets",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RootCauseId",
                table: "EhcTickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EhcRootCauseCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_EhcRootCauseCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EhcRootCauseCodes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EhcTickets_RootCauseId",
                table: "EhcTickets",
                column: "RootCauseId");

            migrationBuilder.CreateIndex(
                name: "IX_EhcTickets_TenantId_RootCauseId",
                table: "EhcTickets",
                columns: new[] { "TenantId", "RootCauseId" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcRootCauseCodes_TenantId_Code",
                table: "EhcRootCauseCodes",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EhcRootCauseCodes_TenantId_IsActive",
                table: "EhcRootCauseCodes",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EhcRootCauseCodes_TenantId_Name",
                table: "EhcRootCauseCodes",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_EhcTickets_EhcRootCauseCodes_RootCauseId",
                table: "EhcTickets",
                column: "RootCauseId",
                principalTable: "EhcRootCauseCodes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EhcTickets_EhcRootCauseCodes_RootCauseId",
                table: "EhcTickets");

            migrationBuilder.DropTable(
                name: "EhcRootCauseCodes");

            migrationBuilder.DropIndex(
                name: "IX_EhcTickets_RootCauseId",
                table: "EhcTickets");

            migrationBuilder.DropIndex(
                name: "IX_EhcTickets_TenantId_RootCauseId",
                table: "EhcTickets");

            migrationBuilder.DropColumn(
                name: "ResolutionSummary",
                table: "EhcTickets");

            migrationBuilder.DropColumn(
                name: "RootCauseDetails",
                table: "EhcTickets");

            migrationBuilder.DropColumn(
                name: "RootCauseId",
                table: "EhcTickets");
        }
    }
}
