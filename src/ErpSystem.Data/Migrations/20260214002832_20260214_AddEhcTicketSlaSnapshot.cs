using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class _20260214_AddEhcTicketSlaSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AppliedFirstResponseMinutes",
                table: "EhcTickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AppliedResolutionMinutes",
                table: "EhcTickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppliedSlaCalendarConfigurationJson",
                table: "EhcTickets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AppliedSlaTemplateId",
                table: "EhcTickets",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppliedFirstResponseMinutes",
                table: "EhcTickets");

            migrationBuilder.DropColumn(
                name: "AppliedResolutionMinutes",
                table: "EhcTickets");

            migrationBuilder.DropColumn(
                name: "AppliedSlaCalendarConfigurationJson",
                table: "EhcTickets");

            migrationBuilder.DropColumn(
                name: "AppliedSlaTemplateId",
                table: "EhcTickets");
        }
    }
}
