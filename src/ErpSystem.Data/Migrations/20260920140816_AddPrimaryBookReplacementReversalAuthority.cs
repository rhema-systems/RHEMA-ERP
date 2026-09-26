using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPrimaryBookReplacementReversalAuthority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReversalDecisionReason",
                table: "AccountingBookPrimaryDesignations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                table: "AccountingBookPrimaryDesignations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReversalRequestedAtUtc",
                table: "AccountingBookPrimaryDesignations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversalRequestedByUserId",
                table: "AccountingBookPrimaryDesignations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversalWorkflowInstanceId",
                table: "AccountingBookPrimaryDesignations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReversedAtUtc",
                table: "AccountingBookPrimaryDesignations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversedByUserId",
                table: "AccountingBookPrimaryDesignations",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReversalDecisionReason",
                table: "AccountingBookPrimaryDesignations");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                table: "AccountingBookPrimaryDesignations");

            migrationBuilder.DropColumn(
                name: "ReversalRequestedAtUtc",
                table: "AccountingBookPrimaryDesignations");

            migrationBuilder.DropColumn(
                name: "ReversalRequestedByUserId",
                table: "AccountingBookPrimaryDesignations");

            migrationBuilder.DropColumn(
                name: "ReversalWorkflowInstanceId",
                table: "AccountingBookPrimaryDesignations");

            migrationBuilder.DropColumn(
                name: "ReversedAtUtc",
                table: "AccountingBookPrimaryDesignations");

            migrationBuilder.DropColumn(
                name: "ReversedByUserId",
                table: "AccountingBookPrimaryDesignations");
        }
    }
}
