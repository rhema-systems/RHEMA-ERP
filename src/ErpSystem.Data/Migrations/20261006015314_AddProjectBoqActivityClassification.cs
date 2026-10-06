using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectBoqActivityClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActivityNodeType",
                table: "ProjectBoqVersionLines",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActivityTitle",
                table: "ProjectBoqVersionLines",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectWorkItemId",
                table: "ProjectBoqVersionLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectWorkItemId",
                table: "ProjectBoqItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoqItems_ProjectWorkItemId",
                table: "ProjectBoqItems",
                column: "ProjectWorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoqItems_TenantId_ProjectWorkItemId",
                table: "ProjectBoqItems",
                columns: new[] { "TenantId", "ProjectWorkItemId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectBoqItems_ProjectWorkItems_ProjectWorkItemId",
                table: "ProjectBoqItems",
                column: "ProjectWorkItemId",
                principalTable: "ProjectWorkItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectBoqItems_ProjectWorkItems_ProjectWorkItemId",
                table: "ProjectBoqItems");

            migrationBuilder.DropIndex(
                name: "IX_ProjectBoqItems_ProjectWorkItemId",
                table: "ProjectBoqItems");

            migrationBuilder.DropIndex(
                name: "IX_ProjectBoqItems_TenantId_ProjectWorkItemId",
                table: "ProjectBoqItems");

            migrationBuilder.DropColumn(
                name: "ActivityNodeType",
                table: "ProjectBoqVersionLines");

            migrationBuilder.DropColumn(
                name: "ActivityTitle",
                table: "ProjectBoqVersionLines");

            migrationBuilder.DropColumn(
                name: "ProjectWorkItemId",
                table: "ProjectBoqVersionLines");

            migrationBuilder.DropColumn(
                name: "ProjectWorkItemId",
                table: "ProjectBoqItems");

        }
    }
}
