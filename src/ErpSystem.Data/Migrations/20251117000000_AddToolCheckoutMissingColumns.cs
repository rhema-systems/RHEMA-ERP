using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddToolCheckoutMissingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CheckoutDate",
                table: "ToolCheckouts",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedReturnDate",
                table: "ToolCheckouts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActualReturnDate",
                table: "ToolCheckouts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ToolCheckouts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "CheckedOut");

            migrationBuilder.AddColumn<string>(
                name: "CheckoutNotes",
                table: "ToolCheckouts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnNotes",
                table: "ToolCheckouts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConditionOnCheckout",
                table: "ToolCheckouts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConditionOnReturn",
                table: "ToolCheckouts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DamageReported",
                table: "ToolCheckouts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DamageDescription",
                table: "ToolCheckouts",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DamageCost",
                table: "ToolCheckouts",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ToolCheckouts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedById",
                table: "ToolCheckouts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ToolCheckouts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "ToolCheckouts",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckoutDate",
                table: "ToolCheckouts");

            migrationBuilder.DropColumn(
                name: "ExpectedReturnDate",
                table: "ToolCheckouts");

            migrationBuilder.DropColumn(
                name: "ActualReturnDate",
                table: "ToolCheckouts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ToolCheckouts");

            migrationBuilder.DropColumn(
                name: "CheckoutNotes",
                table: "ToolCheckouts");

            migrationBuilder.DropColumn(
                name: "ReturnNotes",
                table: "ToolCheckouts");

            migrationBuilder.DropColumn(
                name: "ConditionOnCheckout",
                table: "ToolCheckouts");

            migrationBuilder.DropColumn(
                name: "ConditionOnReturn",
                table: "ToolCheckouts");

            migrationBuilder.DropColumn(
                name: "DamageReported",
                table: "ToolCheckouts");

            migrationBuilder.DropColumn(
                name: "DamageDescription",
                table: "ToolCheckouts");

            migrationBuilder.DropColumn(
                name: "DamageCost",
                table: "ToolCheckouts");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ToolCheckouts");

            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "ToolCheckouts");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ToolCheckouts");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "ToolCheckouts");
        }
    }
}