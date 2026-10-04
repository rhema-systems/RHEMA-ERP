using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext)), Migration("20260930202402_UniqueEstateLandDemarcationChildReference")]
public partial class UniqueEstateLandDemarcationChildReference : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
UPDATE parcel
SET ChildFixedAssetReference = CONCAT(asset.AssetCode, '-D', FORMAT(parcel.DemarcationNumber, '000', 'en-US'))
FROM dbo.EstateLandDemarcations AS parcel
JOIN dbo.EstateManagedAssets AS asset ON asset.Id = parcel.EstateManagedAssetId
WHERE parcel.ParentDemarcationId IS NULL
  AND NULLIF(LTRIM(RTRIM(parcel.ChildFixedAssetReference)), '') IS NULL;

WHILE EXISTS (
    SELECT 1
    FROM dbo.EstateLandDemarcations AS parcel
    JOIN dbo.EstateLandDemarcations AS parent ON parent.Id = parcel.ParentDemarcationId
    WHERE NULLIF(LTRIM(RTRIM(parcel.ChildFixedAssetReference)), '') IS NULL
      AND NULLIF(LTRIM(RTRIM(parent.ChildFixedAssetReference)), '') IS NOT NULL)
BEGIN
    UPDATE parcel
    SET ChildFixedAssetReference = CONCAT(parent.ChildFixedAssetReference, '-D', FORMAT(parcel.DemarcationNumber, '000', 'en-US'))
    FROM dbo.EstateLandDemarcations AS parcel
    JOIN dbo.EstateLandDemarcations AS parent ON parent.Id = parcel.ParentDemarcationId
    WHERE NULLIF(LTRIM(RTRIM(parcel.ChildFixedAssetReference)), '') IS NULL
      AND NULLIF(LTRIM(RTRIM(parent.ChildFixedAssetReference)), '') IS NOT NULL;
END;

IF EXISTS (SELECT 1 FROM dbo.EstateLandDemarcations
           WHERE NULLIF(LTRIM(RTRIM(ChildFixedAssetReference)), '') IS NULL)
    THROW 51021, 'A demarcated land parcel has no resolvable child fixed-asset reference.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.EstateLandDemarcations
    GROUP BY TenantId, ChildFixedAssetReference
    HAVING COUNT(*) > 1)
    THROW 51022, 'Duplicate child fixed-asset references must be resolved before this migration.', 1;
");

        migrationBuilder.CreateIndex(
            name: "IX_EstateLandDemarcations_TenantId_ChildFixedAssetReference",
            table: "EstateLandDemarcations",
            columns: new[] { "TenantId", "ChildFixedAssetReference" },
            unique: true,
            filter: "[ChildFixedAssetReference] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_EstateLandDemarcations_TenantId_ChildFixedAssetReference",
            table: "EstateLandDemarcations");
    }
}
