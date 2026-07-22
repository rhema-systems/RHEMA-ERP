using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobCardCustomerBusinessPartner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerBusinessPartnerId",
                table: "JobCard",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_CustomerBusinessPartnerId",
                table: "JobCard",
                column: "CustomerBusinessPartnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_JobCard_BusinessPartners_CustomerBusinessPartnerId",
                table: "JobCard",
                column: "CustomerBusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobCard_BusinessPartners_CustomerBusinessPartnerId",
                table: "JobCard");

            migrationBuilder.DropIndex(
                name: "IX_JobCard_CustomerBusinessPartnerId",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "CustomerBusinessPartnerId",
                table: "JobCard");
        }
    }
}
