using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class MaintenanceAssetSourceIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FixedAssets_MaintenanceAssets_MaintenanceAssetId",
                table: "FixedAssets");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceAssets_TenantId",
                table: "MaintenanceAssets");

            migrationBuilder.DropIndex(
                name: "IX_JobCard_TenantId",
                table: "JobCard");

            migrationBuilder.AddColumn<Guid>(
                name: "EstateManagedAssetId",
                table: "MaintenanceAssets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FixedAssetId",
                table: "MaintenanceAssets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "MaintenanceAssets",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "LegacyMaintenanceAsset");

            migrationBuilder.AddColumn<string>(
                name: "AssetSource",
                table: "JobCard",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "LegacyMaintenanceAsset");

            migrationBuilder.AddColumn<Guid>(
                name: "EstateManagedAssetId",
                table: "JobCard",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FixedAssetId",
                table: "JobCard",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE ma
                SET ma.FixedAssetId = source.Id,
                    ma.SourceType = 'FixedAsset'
                FROM MaintenanceAssets ma
                CROSS APPLY
                (
                    SELECT TOP (1) fa.Id
                    FROM FixedAssets fa
                    WHERE fa.TenantId = ma.TenantId
                      AND fa.MaintenanceAssetId = ma.Id
                      AND fa.IsDeleted = 0
                    ORDER BY fa.CreatedAt, fa.Id
                ) source
                WHERE ma.IsDeleted = 0;

                UPDATE jc
                SET jc.AssetSource = ma.SourceType,
                    jc.FixedAssetId = ma.FixedAssetId,
                    jc.EstateManagedAssetId = ma.EstateManagedAssetId
                FROM JobCard jc
                INNER JOIN MaintenanceAssets ma
                    ON ma.TenantId = jc.TenantId
                   AND ma.Id = jc.AssetId
                WHERE jc.IsDeleted = 0;
                """);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_MaintenanceAssets_TenantId_Id",
                table: "MaintenanceAssets",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EstateManagedAssets_TenantId_Id",
                table: "EstateManagedAssets",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_TenantId_EstateManagedAssetId",
                table: "MaintenanceAssets",
                columns: new[] { "TenantId", "EstateManagedAssetId" },
                unique: true,
                filter: "[EstateManagedAssetId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_TenantId_FixedAssetId",
                table: "MaintenanceAssets",
                columns: new[] { "TenantId", "FixedAssetId" },
                unique: true,
                filter: "[FixedAssetId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaintenanceAssets_SourceReference",
                table: "MaintenanceAssets",
                sql: "([SourceType] = 'LegacyMaintenanceAsset' AND [FixedAssetId] IS NULL AND [EstateManagedAssetId] IS NULL) OR ([SourceType] = 'FixedAsset' AND [FixedAssetId] IS NOT NULL AND [EstateManagedAssetId] IS NULL) OR ([SourceType] = 'EstateManagedAsset' AND [FixedAssetId] IS NULL AND [EstateManagedAssetId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_TenantId_AssetSource_FixedAssetId_EstateManagedAssetId",
                table: "JobCard",
                columns: new[] { "TenantId", "AssetSource", "FixedAssetId", "EstateManagedAssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_TenantId_EstateManagedAssetId",
                table: "JobCard",
                columns: new[] { "TenantId", "EstateManagedAssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_TenantId_FixedAssetId",
                table: "JobCard",
                columns: new[] { "TenantId", "FixedAssetId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_JobCard_AssetSourceReference",
                table: "JobCard",
                sql: "([AssetSource] = 'LegacyMaintenanceAsset' AND [FixedAssetId] IS NULL AND [EstateManagedAssetId] IS NULL) OR ([AssetSource] = 'FixedAsset' AND [FixedAssetId] IS NOT NULL AND [EstateManagedAssetId] IS NULL) OR ([AssetSource] = 'EstateManagedAsset' AND [FixedAssetId] IS NULL AND [EstateManagedAssetId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_FixedAssets_MaintenanceAssets_MaintenanceAssetId",
                table: "FixedAssets",
                column: "MaintenanceAssetId",
                principalTable: "MaintenanceAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_JobCard_EstateManagedAssets_TenantId_EstateManagedAssetId",
                table: "JobCard",
                columns: new[] { "TenantId", "EstateManagedAssetId" },
                principalTable: "EstateManagedAssets",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JobCard_FixedAssets_TenantId_FixedAssetId",
                table: "JobCard",
                columns: new[] { "TenantId", "FixedAssetId" },
                principalTable: "FixedAssets",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceAssets_EstateManagedAssets_TenantId_EstateManagedAssetId",
                table: "MaintenanceAssets",
                columns: new[] { "TenantId", "EstateManagedAssetId" },
                principalTable: "EstateManagedAssets",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceAssets_FixedAssets_TenantId_FixedAssetId",
                table: "MaintenanceAssets",
                columns: new[] { "TenantId", "FixedAssetId" },
                principalTable: "FixedAssets",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FixedAssets_MaintenanceAssets_MaintenanceAssetId",
                table: "FixedAssets");

            migrationBuilder.DropForeignKey(
                name: "FK_JobCard_EstateManagedAssets_TenantId_EstateManagedAssetId",
                table: "JobCard");

            migrationBuilder.DropForeignKey(
                name: "FK_JobCard_FixedAssets_TenantId_FixedAssetId",
                table: "JobCard");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceAssets_EstateManagedAssets_TenantId_EstateManagedAssetId",
                table: "MaintenanceAssets");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceAssets_FixedAssets_TenantId_FixedAssetId",
                table: "MaintenanceAssets");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_MaintenanceAssets_TenantId_Id",
                table: "MaintenanceAssets");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceAssets_TenantId_EstateManagedAssetId",
                table: "MaintenanceAssets");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceAssets_TenantId_FixedAssetId",
                table: "MaintenanceAssets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaintenanceAssets_SourceReference",
                table: "MaintenanceAssets");

            migrationBuilder.DropIndex(
                name: "IX_JobCard_TenantId_AssetSource_FixedAssetId_EstateManagedAssetId",
                table: "JobCard");

            migrationBuilder.DropIndex(
                name: "IX_JobCard_TenantId_EstateManagedAssetId",
                table: "JobCard");

            migrationBuilder.DropIndex(
                name: "IX_JobCard_TenantId_FixedAssetId",
                table: "JobCard");

            migrationBuilder.DropCheckConstraint(
                name: "CK_JobCard_AssetSourceReference",
                table: "JobCard");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EstateManagedAssets_TenantId_Id",
                table: "EstateManagedAssets");

            migrationBuilder.DropColumn(
                name: "EstateManagedAssetId",
                table: "MaintenanceAssets");

            migrationBuilder.DropColumn(
                name: "FixedAssetId",
                table: "MaintenanceAssets");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "MaintenanceAssets");

            migrationBuilder.DropColumn(
                name: "AssetSource",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "EstateManagedAssetId",
                table: "JobCard");

            migrationBuilder.DropColumn(
                name: "FixedAssetId",
                table: "JobCard");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_TenantId",
                table: "MaintenanceAssets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_TenantId",
                table: "JobCard",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_FixedAssets_MaintenanceAssets_MaintenanceAssetId",
                table: "FixedAssets",
                column: "MaintenanceAssetId",
                principalTable: "MaintenanceAssets",
                principalColumn: "Id");
        }
    }
}
