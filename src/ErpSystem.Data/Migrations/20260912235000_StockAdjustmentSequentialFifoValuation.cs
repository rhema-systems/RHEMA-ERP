using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912235000_StockAdjustmentSequentialFifoValuation")]
public sealed class StockAdjustmentSequentialFifoValuation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(ExpectedValueFunction);
        Patch(migrationBuilder, "TR_StockAdjustmentItems_ControlledMutation",
            "SET NOCOUNT ON;", "SET NOCOUNT ON;\n" + DraftRetirementGuard);
        Patch(migrationBuilder, "TR_StockAdjustmentItems_ControlledMutation",
            "i.UnitCost <> ISNULL(dbo.InventoryAdjustmentExpectedUnitCost(i.TenantId,i.InventoryItemId,a.WarehouseId,i.LocationId,i.AdjustmentQuantity),0)",
            "i.UnitCost <> CASE WHEN i.IsDeleted=1 THEN i.UnitCost WHEN item.ValuationMethod=2 AND i.AdjustmentQuantity<0 THEN ISNULL(CONVERT(decimal(18,4),ROUND(ABS(dbo.InventoryAdjustmentExpectedLineValue(i.TenantId,i.AdjustmentId,i.Id)/NULLIF(i.AdjustmentQuantity,0)),4)),0) ELSE ISNULL(dbo.InventoryAdjustmentExpectedUnitCost(i.TenantId,i.InventoryItemId,a.WarehouseId,i.LocationId,i.AdjustmentQuantity),0) END");
        Patch(migrationBuilder, "TR_StockAdjustmentItems_ControlledMutation",
            "i.AdjustmentValue <> ROUND(i.AdjustmentQuantity * i.UnitCost, 2)",
            "i.AdjustmentValue <> CASE WHEN i.IsDeleted=1 THEN i.AdjustmentValue WHEN a.ReasonCode<>N'INITIAL_STOCK' AND item.ValuationMethod=2 AND i.AdjustmentQuantity<0 THEN ISNULL(dbo.InventoryAdjustmentExpectedLineValue(i.TenantId,i.AdjustmentId,i.Id),0) ELSE ROUND(i.AdjustmentQuantity * i.UnitCost, 2) END");
        migrationBuilder.Sql(SubmissionGuard);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "THROW 51979, 'FIFO valuation evidence requires a reviewed forward migration; historical costs must not be reinterpreted.', 1;");

    // EF replaces editable draft lines by soft-deleting the old evidence first.
    // Those unchanged retired rows must not be repriced against a sequence from
    // which they have just been removed. Every other existing line guard remains.
    public const string DraftRetirementGuard = """
    IF EXISTS (
        SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
        WHERE d.IsDeleted=1 OR (i.IsDeleted=1 AND (d.Id IS NULL OR EXISTS (
            SELECT i.TenantId,i.AdjustmentId,i.InventoryItemId,i.LocationId,i.CreatedAt,i.CreatedBy,i.CreatedById,
                i.SystemQuantity,i.PhysicalQuantity,i.AdjustmentQuantity,i.UnitCost,i.AdjustmentValue,
                i.SerialNumber,i.LotNumber,i.BatchNumber,i.ManufactureDate,i.ExpiryDate,i.Notes,i.Reason
            EXCEPT
            SELECT d.TenantId,d.AdjustmentId,d.InventoryItemId,d.LocationId,d.CreatedAt,d.CreatedBy,d.CreatedById,
                d.SystemQuantity,d.PhysicalQuantity,d.AdjustmentQuantity,d.UnitCost,d.AdjustmentValue,
                d.SerialNumber,d.LotNumber,d.BatchNumber,d.ManufactureDate,d.ExpiryDate,d.Notes,d.Reason))))
        THROW 51980, 'INV_ADJUSTMENT_DRAFT_RETIREMENT_INVALID: retire only unchanged existing Draft lines; retired evidence cannot be edited or restored.', 1;
""";

    // Replays only this adjustment's prior same-item/exact-bin lines. Physical layer
    // rows and historical adjustment evidence are never changed by this function.
    public const string ExpectedValueFunction = """
CREATE OR ALTER FUNCTION dbo.InventoryAdjustmentExpectedLineValue
(@tenant uniqueidentifier,@adjustment uniqueidentifier,@line uniqueidentifier)
RETURNS decimal(28,8)
AS
BEGIN
    DECLARE @item uniqueidentifier,@warehouse uniqueidentifier,@location uniqueidentifier,
        @created datetime2(7),@delta decimal(18,4),@method int,@fallback decimal(28,8),
        @average decimal(28,8),@standard decimal(28,8),@last decimal(28,8),
        @opening decimal(28,8)=0,@execution datetime2(7)=SYSUTCDATETIME();
    SELECT @item=l.InventoryItemId,@location=l.LocationId,@created=l.CreatedAt,@delta=l.AdjustmentQuantity,
        @warehouse=a.WarehouseId,@method=p.ValuationMethod,@average=p.AverageCost,
        @standard=p.StandardCost,@last=p.LastPurchaseCost
    FROM dbo.StockAdjustmentItems l
    JOIN dbo.StockAdjustments a ON a.Id=l.AdjustmentId AND a.TenantId=l.TenantId AND a.IsDeleted=0
    JOIN dbo.InventoryItems p ON p.Id=l.InventoryItemId AND p.TenantId=l.TenantId AND p.IsDeleted=0
    WHERE l.Id=@line AND l.AdjustmentId=@adjustment AND l.TenantId=@tenant AND l.IsDeleted=0;
    IF @item IS NULL OR @location IS NULL OR @delta=0 RETURN NULL;
    IF @method<>2 OR @delta>0 RETURN CONVERT(decimal(28,8),ROUND(@delta*
        dbo.InventoryAdjustmentExpectedUnitCost(@tenant,@item,@warehouse,@location,@delta),2));
    SET @fallback=CASE WHEN @average>0 THEN @average WHEN @standard>0 THEN @standard ELSE @last END;
    SET @fallback=ISNULL(@fallback,0);
    SELECT @opening=Quantity FROM dbo.InventoryLocations WHERE TenantId=@tenant
        AND InventoryItemId=@item AND LocationId=@location AND IsDeleted=0;

    DECLARE @layers TABLE (Ordinal int IDENTITY(1,1),LayerDate datetime2(7),CreatedAt datetime2(7),
        Quantity decimal(28,8),UnitCost decimal(28,8),RemainingValue decimal(28,8));
    INSERT @layers (LayerDate,CreatedAt,Quantity,UnitCost,RemainingValue)
    SELECT TOP(2147483647) LayerDate,CreatedAt,RemainingQuantity,UnitCost,RemainingValue
    FROM dbo.InventoryLayers WHERE TenantId=@tenant AND InventoryItemId=@item AND WarehouseId=@warehouse
        AND LocationId=@location AND IsDeleted=0 AND IsActive=1 AND IsFullyConsumed=0 AND RemainingQuantity>0
    ORDER BY LayerDate,CreatedAt,Id;

    DECLARE @lines TABLE (Ordinal int,Id uniqueidentifier,Delta decimal(28,8),UnitCost decimal(28,8));
    INSERT @lines
    SELECT ROW_NUMBER() OVER(ORDER BY CreatedAt,Id),Id,AdjustmentQuantity,UnitCost
    FROM dbo.StockAdjustmentItems WHERE TenantId=@tenant AND AdjustmentId=@adjustment
        AND InventoryItemId=@item AND LocationId=@location AND IsDeleted=0
        AND (CreatedAt<@created OR (CreatedAt=@created AND Id<=@line));
    DECLARE @number int=1,@count int=(SELECT COUNT(*) FROM @lines),@current uniqueidentifier,
        @quantity decimal(28,8),@cost decimal(28,8),@amount decimal(28,8),@missing decimal(28,8),
        @remaining decimal(28,8),@layer int,@layerQuantity decimal(28,8),@layerCost decimal(28,8),
        @layerValue decimal(28,8),@taken decimal(28,8),@takenValue decimal(28,8);
    WHILE @number<=@count
    BEGIN
        SELECT @current=Id,@quantity=Delta,@cost=UnitCost FROM @lines WHERE Ordinal=@number;
        IF @quantity>0
        BEGIN
            INSERT @layers VALUES(@execution,@execution,@quantity,ROUND(@cost,2),ROUND(@quantity*@cost,4));
            SET @opening+=@quantity;
        END
        ELSE
        BEGIN
            SELECT @missing=@opening-ISNULL(SUM(Quantity),0) FROM @layers;
            IF @missing>0
            BEGIN
                IF @fallback<=0 RETURN NULL;
                INSERT @layers VALUES(@execution,@execution,@missing,ROUND(@fallback,2),ROUND(@missing*@fallback,4));
            END
            SET @remaining=ABS(@quantity); SET @amount=0;
            WHILE @remaining>0
            BEGIN
                SET @layer=NULL;
                SELECT TOP(1) @layer=Ordinal,@layerQuantity=Quantity,@layerCost=UnitCost,@layerValue=RemainingValue
                FROM @layers WHERE Quantity>0 ORDER BY LayerDate,CreatedAt,Ordinal;
                IF @layer IS NULL BREAK;
                SET @taken=CASE WHEN @remaining<@layerQuantity THEN @remaining ELSE @layerQuantity END;
                SET @takenValue=CASE WHEN @taken=@layerQuantity THEN @layerValue
                    WHEN ROUND(@taken*@layerCost,2)<@layerValue THEN ROUND(@taken*@layerCost,2) ELSE @layerValue END;
                UPDATE @layers SET Quantity=Quantity-@taken,RemainingValue=RemainingValue-@takenValue WHERE Ordinal=@layer;
                SET @amount+=@takenValue; SET @remaining-=@taken;
            END
            IF @remaining>0 AND @fallback<=0 RETURN NULL;
            SET @amount+=@remaining*@fallback; SET @opening+=@quantity;
            IF @current=@line RETURN -ROUND(@amount,2);
        END
        SET @number+=1;
    END
    RETURN NULL;
END;
""";

    public const string SubmissionGuard = """
CREATE OR ALTER TRIGGER dbo.TR_StockAdjustments_FifoSequenceGuard
ON dbo.StockAdjustments AFTER INSERT,UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id AND d.TenantId=i.TenantId
        JOIN dbo.StockAdjustmentItems l ON l.AdjustmentId=i.Id AND l.TenantId=i.TenantId AND l.IsDeleted=0
        JOIN dbo.InventoryItems p ON p.Id=l.InventoryItemId AND p.TenantId=l.TenantId AND p.IsDeleted=0
        WHERE d.Status=N'Draft' AND i.Status IN(N'PendingApproval',N'ReadyToPost')
            AND i.ReasonCode<>N'INITIAL_STOCK' AND p.ValuationMethod=2 AND l.AdjustmentQuantity<0
            AND (l.AdjustmentValue<>ISNULL(dbo.InventoryAdjustmentExpectedLineValue(l.TenantId,l.AdjustmentId,l.Id),0)
                OR l.UnitCost<>ISNULL(CONVERT(decimal(18,4),ROUND(ABS(
                    dbo.InventoryAdjustmentExpectedLineValue(l.TenantId,l.AdjustmentId,l.Id)/NULLIF(l.AdjustmentQuantity,0)),4)),0)))
        THROW 51978, 'INV_ADJUSTMENT_FIFO_SEQUENCE_INVALID: save the draft again to refresh its complete FIFO line sequence before submission.', 1;
END;
""";

    private static void Patch(MigrationBuilder migrationBuilder,string trigger,string before,string after)
    {
        static string Literal(string value) => value.Replace("'","''",StringComparison.Ordinal);
        migrationBuilder.Sql($$"""
DECLARE @definition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.{{trigger}}',N'TR'));
DECLARE @before nvarchar(max)=N'{{Literal(before)}}',@after nvarchar(max)=N'{{Literal(after)}}';
SET @definition=REPLACE(@definition,CHAR(13),N'');
SET @before=REPLACE(@before,CHAR(13),N''); SET @after=REPLACE(@after,CHAR(13),N'');
IF @definition IS NULL OR (LEN(@definition)-LEN(REPLACE(@definition,@before,N'')))/NULLIF(LEN(@before),0)<>1
    THROW 51977,'INV_ADJUSTMENT_FIFO_GUARD_DRIFT: expected one protected line-value predicate.',1;
SET @definition=REPLACE(@definition,@before,@after);
SET @definition=STUFF(@definition,1,CHARINDEX(N'TRIGGER',UPPER(@definition))-1,N'CREATE OR ALTER ');
EXEC sys.sp_executesql @definition;
""");
    }
}
