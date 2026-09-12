using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912013000_AlignStockAdjustmentLocationValuation")]
public sealed class AlignStockAdjustmentLocationValuation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(UpgradeSql);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(DowngradeSql);

    // Keep this SQL reusable by the guarded local rollback tests and migration runner.
    public const string UpgradeSql = """
        ALTER TABLE dbo.StockAdjustmentItems ALTER COLUMN UnitCost decimal(18,4) NOT NULL;

        EXEC(N'CREATE OR ALTER FUNCTION dbo.InventoryAdjustmentExpectedUnitCost
        (
            @tenant uniqueidentifier, @item uniqueidentifier, @warehouse uniqueidentifier,
            @location uniqueidentifier, @delta decimal(18,4)
        )
        RETURNS decimal(18,4)
        AS
        BEGIN
            DECLARE @method int, @average decimal(18,4), @standard decimal(18,4),
                @last decimal(18,4), @fallback decimal(18,4), @balance decimal(18,4);
            SELECT @method=ValuationMethod,@average=AverageCost,@standard=StandardCost,@last=LastPurchaseCost
            FROM dbo.InventoryItems WHERE Id=@item AND TenantId=@tenant AND IsDeleted=0;
            IF @method IS NULL RETURN NULL;
            SET @fallback=CASE WHEN @average>0 THEN @average WHEN @standard>0 THEN @standard ELSE @last END;
            IF @method=4 RETURN CONVERT(decimal(18,4),ROUND(@standard,4));
            SELECT @balance=AverageUnitCost FROM dbo.InventoryBalances
            WHERE TenantId=@tenant AND InventoryItemId=@item AND WarehouseId=@warehouse
                AND LocationId=@location AND IsDeleted=0;
            IF @method=1 OR @delta>=0
                RETURN CONVERT(decimal(18,4),ROUND(CASE WHEN @balance>0 THEN @balance ELSE @fallback END,4));

            DECLARE @needed decimal(18,4)=ABS(@delta), @taken decimal(28,8)=0,
                @value decimal(28,8)=0, @cost decimal(28,8);
            IF @needed=0 RETURN NULL;
            ;WITH layers AS (
                SELECT RemainingQuantity,UnitCost,
                    ISNULL(SUM(RemainingQuantity) OVER
                        (ORDER BY LayerDate,CreatedAt,Id ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING),0) AS PreviousQuantity
                FROM dbo.InventoryLayers
                WHERE TenantId=@tenant AND InventoryItemId=@item AND WarehouseId=@warehouse AND LocationId=@location
                    AND IsDeleted=0 AND IsFullyConsumed=0 AND RemainingQuantity>0
            ), consumed AS (
                SELECT UnitCost,CASE WHEN PreviousQuantity>=@needed THEN 0
                    WHEN RemainingQuantity<=@needed-PreviousQuantity THEN RemainingQuantity
                    ELSE @needed-PreviousQuantity END AS Quantity
                FROM layers
            )
            SELECT @taken=ISNULL(SUM(Quantity),0),@value=ISNULL(SUM(Quantity*UnitCost),0) FROM consumed;
            SET @cost=(@value+(@needed-@taken)*@fallback)/@needed;
            RETURN CONVERT(decimal(18,4),ROUND(@cost,4));
        END');

        DECLARE @definition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_StockAdjustmentItems_ControlledMutation'));
        DECLARE @start int=CHARINDEX(N'i.UnitCost <> CASE WHEN item.AverageCost > 0 THEN item.AverageCost',@definition);
        DECLARE @tail nvarchar(100)=N'ELSE item.LastPurchaseCost END';
        DECLARE @finish int=CHARINDEX(@tail,@definition,@start);
        IF @definition IS NULL OR @start<=0 OR @finish<=@start
            THROW 51970,'INV_ADJUSTMENT_VALUATION_MIGRATION: expected original cost guard was not found.',1;
        DECLARE @old nvarchar(max)=SUBSTRING(@definition,@start,@finish+LEN(@tail)-@start);
        IF REPLACE(REPLACE(REPLACE(REPLACE(@old,N' ',N''),CHAR(13),N''),CHAR(10),N''),CHAR(9),N'')
           <> N'i.UnitCost<>CASEWHENitem.AverageCost>0THENitem.AverageCostWHENitem.StandardCost>0THENitem.StandardCostELSEitem.LastPurchaseCostEND'
            THROW 51970,'INV_ADJUSTMENT_VALUATION_MIGRATION: unrecognised cost guard; no guards were bypassed.',1;
        -- NULL/zero expected costs cannot validate any positive line cost. Preserve
        -- all existing opening-stock, tenant, active-location and immutable guards.
        SET @definition=STUFF(@definition,@start,LEN(@old),
            N'i.UnitCost <> ISNULL(dbo.InventoryAdjustmentExpectedUnitCost(i.TenantId,i.InventoryItemId,a.WarehouseId,i.LocationId,i.AdjustmentQuantity),0)');
        SET @definition=STUFF(@definition,1,CHARINDEX(N'TRIGGER',UPPER(@definition))-1,N'CREATE OR ALTER ');
        EXEC sys.sp_executesql @definition;
        """;

    public const string DowngradeSql = """
        IF EXISTS(SELECT 1 FROM dbo.StockAdjustmentItems WHERE UnitCost<>ROUND(UnitCost,2))
            THROW 51970,'Cannot narrow adjustment unit costs while four-decimal audit values exist.',1;
        DECLARE @definition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_StockAdjustmentItems_ControlledMutation'));
        DECLARE @current nvarchar(max)=N'i.UnitCost <> ISNULL(dbo.InventoryAdjustmentExpectedUnitCost(i.TenantId,i.InventoryItemId,a.WarehouseId,i.LocationId,i.AdjustmentQuantity),0)';
        IF @definition IS NULL OR CHARINDEX(@current,@definition)=0
            THROW 51970,'INV_ADJUSTMENT_VALUATION_MIGRATION: expected location-cost guard was not found.',1;
        SET @definition=REPLACE(@definition,@current,N'i.UnitCost <> CASE WHEN item.AverageCost > 0 THEN item.AverageCost WHEN item.StandardCost > 0 THEN item.StandardCost ELSE item.LastPurchaseCost END');
        SET @definition=STUFF(@definition,1,CHARINDEX(N'TRIGGER',UPPER(@definition))-1,N'CREATE OR ALTER ');
        EXEC sys.sp_executesql @definition;
        DROP FUNCTION dbo.InventoryAdjustmentExpectedUnitCost;
        ALTER TABLE dbo.StockAdjustmentItems ALTER COLUMN UnitCost decimal(18,2) NOT NULL;
        """;
}
