using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260203112000_AddWorkOrderToolBillingExclusionFields")]
    /// <inheritdoc />
    public partial class AddWorkOrderToolBillingExclusionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingExcludedBy",
                table: "WorkOrderTools",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BillingExcludedAt",
                table: "WorkOrderTools",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingExclusionReason",
                table: "WorkOrderTools",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExcludedFromBilling",
                table: "WorkOrderTools",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingExcludedBy",
                table: "WorkOrderTools");

            migrationBuilder.DropColumn(
                name: "BillingExcludedAt",
                table: "WorkOrderTools");

            migrationBuilder.DropColumn(
                name: "BillingExclusionReason",
                table: "WorkOrderTools");

            migrationBuilder.DropColumn(
                name: "IsExcludedFromBilling",
                table: "WorkOrderTools");
        }
    }
}
