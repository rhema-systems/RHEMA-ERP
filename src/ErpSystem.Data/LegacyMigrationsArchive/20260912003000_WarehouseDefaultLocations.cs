using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912003000_WarehouseDefaultLocations")]
public sealed class WarehouseDefaultLocations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "IsDefault", table: "WarehouseLocations", type: "bit", nullable: false, defaultValue: false);
        migrationBuilder.CreateIndex(name: "UX_WarehouseLocations_Default", table: "WarehouseLocations",
            columns: new[] { "TenantId", "WarehouseId" }, unique: true,
            filter: "[IsDefault] = 1 AND [IsActive] = 1 AND [IsDeleted] = 0");
        migrationBuilder.Sql("""
            -- Configure metadata only. Existing item/bin/valuation/stock balances remain untouched.
            ;WITH eligible AS (
                SELECT l.Id, COUNT(*) OVER(PARTITION BY l.TenantId,l.WarehouseId) AS CandidateCount
                FROM dbo.WarehouseLocations l INNER JOIN dbo.Warehouses w ON w.Id=l.WarehouseId AND w.TenantId=l.TenantId
                WHERE w.IsDeleted=0 AND l.IsDeleted=0 AND l.IsActive=1 AND l.LocationType=N'Bin'
                    AND l.IsConsignmentBin=0 AND l.ConsignmentWarehouseId IS NULL
                    AND l.IsQuarantineLocation=0 AND l.IsInspectionLocation=0 AND l.IsInTransitLocation=0
                    AND l.IsShippingLocation=0 AND l.IsStagingLocation=0 AND l.IsReturnLocation=0 AND l.IsDamageLocation=0
            )
            UPDATE l SET IsDefault=1,UpdatedAt=SYSUTCDATETIME(),UpdatedBy=N'migration:WarehouseDefaultLocations'
            FROM dbo.WarehouseLocations l INNER JOIN eligible e ON e.Id=l.Id WHERE e.CandidateCount=1;

            INSERT INTO dbo.WarehouseLocations
                (Id,TenantId,WarehouseId,LocationCode,Name,Description,LocationType,IsDefault,IsActive,IsDeleted,
                 IsPickingLocation,IsReceivingLocation,IsConsignmentBin,IsQuarantineLocation,IsInspectionLocation,
                 IsInTransitLocation,IsShippingLocation,IsStagingLocation,IsReturnLocation,IsDamageLocation,
                 PickSequence,CurrentWeight,CurrentVolume,CurrentItemCount,CreatedAt,CreatedBy)
            SELECT NEWID(),w.TenantId,w.Id,
                CASE WHEN EXISTS (SELECT 1 FROM dbo.WarehouseLocations l WHERE l.TenantId=w.TenantId AND l.WarehouseId=w.Id AND l.LocationCode=N'DEFAULT')
                    THEN N'DEFAULT-'+LEFT(REPLACE(CONVERT(nvarchar(36),NEWID()),N'-',N''),8) ELSE N'DEFAULT' END,
                N'Default bin',N'Default storage bin',N'Bin',1,1,0,1,1,0,0,0,0,0,0,0,0,0,0,0,0,SYSUTCDATETIME(),N'migration:WarehouseDefaultLocations'
            FROM dbo.Warehouses w WHERE w.IsDeleted=0 AND NOT EXISTS
                (SELECT 1 FROM dbo.WarehouseLocations l WHERE l.TenantId=w.TenantId AND l.WarehouseId=w.Id AND l.IsDefault=1 AND l.IsDeleted=0);

            ALTER TABLE dbo.PhysicalCountActions DROP CONSTRAINT CK_PhysicalCountActions_ActionType;
            ALTER TABLE dbo.PhysicalCountActions ADD CONSTRAINT CK_PhysicalCountActions_ActionType CHECK(ActionType BETWEEN 1 AND 15);
            DECLARE @actions nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_PhysicalCountActions_AppendOnly'));
            IF @actions IS NULL OR CHARINDEX(N'i.ActionType NOT BETWEEN 1 AND 14',@actions)=0
                THROW 51928,'INV_COUNT_DEFAULT_LOCATION_MIGRATION: unexpected action guard.',1;
            SET @actions=REPLACE(@actions,N'i.ActionType NOT BETWEEN 1 AND 14',N'i.ActionType NOT BETWEEN 1 AND 15 OR (i.ActionType=15 AND p.Status NOT IN (N''Draft'',N''InProgress'',N''UnderReview''))');
            SET @actions=STUFF(@actions,1,CHARINDEX(N'TRIGGER',UPPER(@actions))-1,N'CREATE OR ALTER ');
            EXEC sys.sp_executesql @actions;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS(SELECT 1 FROM dbo.PhysicalCountActions WHERE ActionType=15)
                THROW 51928,'Default-location history exists; this migration cannot be rolled back without losing supported audit history.',1;
            ALTER TABLE dbo.PhysicalCountActions DROP CONSTRAINT CK_PhysicalCountActions_ActionType;
            ALTER TABLE dbo.PhysicalCountActions ADD CONSTRAINT CK_PhysicalCountActions_ActionType CHECK(ActionType BETWEEN 1 AND 14);
            DECLARE @actions nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_PhysicalCountActions_AppendOnly'));
            SET @actions=REPLACE(@actions,N'i.ActionType NOT BETWEEN 1 AND 15 OR (i.ActionType=15 AND p.Status NOT IN (N''Draft'',N''InProgress'',N''UnderReview''))',N'i.ActionType NOT BETWEEN 1 AND 14');
            SET @actions=STUFF(@actions,1,CHARINDEX(N'TRIGGER',UPPER(@actions))-1,N'CREATE OR ALTER ');
            EXEC sys.sp_executesql @actions;
            """);
        migrationBuilder.DropIndex(name: "UX_WarehouseLocations_Default", table: "WarehouseLocations");
        migrationBuilder.DropColumn(name: "IsDefault", table: "WarehouseLocations");
    }
}
