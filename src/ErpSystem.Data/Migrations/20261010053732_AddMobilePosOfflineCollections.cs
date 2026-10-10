using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMobilePosOfflineCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MobilePosOfflineGrantId",
                table: "MobilePosCollections",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfflinePolicySnapshotHash",
                table: "MobilePosCollections",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MobilePosCollections_MobilePosOfflineGrantId",
                table: "MobilePosCollections",
                column: "MobilePosOfflineGrantId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MobilePosCollections_OfflinePolicyHash",
                table: "MobilePosCollections",
                sql: "[OfflinePolicySnapshotHash] IS NULL OR LEN([OfflinePolicySnapshotHash]) = 64");

            migrationBuilder.AddForeignKey(
                name: "FK_MobilePosCollections_MobilePosOfflineGrants_MobilePosOfflineGrantId",
                table: "MobilePosCollections",
                column: "MobilePosOfflineGrantId",
                principalTable: "MobilePosOfflineGrants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MobilePosCollections_MobilePosOfflineGrants_MobilePosOfflineGrantId",
                table: "MobilePosCollections");

            migrationBuilder.DropIndex(
                name: "IX_MobilePosCollections_MobilePosOfflineGrantId",
                table: "MobilePosCollections");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MobilePosCollections_OfflinePolicyHash",
                table: "MobilePosCollections");

            migrationBuilder.DropColumn(
                name: "MobilePosOfflineGrantId",
                table: "MobilePosCollections");

            migrationBuilder.DropColumn(
                name: "OfflinePolicySnapshotHash",
                table: "MobilePosCollections");
        }
    }
}
