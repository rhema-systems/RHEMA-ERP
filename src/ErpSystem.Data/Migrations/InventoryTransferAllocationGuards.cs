using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

internal static class InventoryTransferAllocationGuards
{
    public static void Install(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(DispatchGuard);
        migrationBuilder.Sql(ReceiptGuard);
        migrationBuilder.Sql(MovementGuard);
        migrationBuilder.Sql(ProjectionGuard);
        migrationBuilder.Sql(LegacyProjectionPatch);
        migrationBuilder.Sql(TransitLocationGuard);
        migrationBuilder.Sql(TransitWarehouseGuard);
    }

    public static void Remove(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS(SELECT 1 FROM dbo.InventoryTransferDispatchAllocations)
                OR EXISTS(SELECT 1 FROM dbo.InventoryTransferReceiptAllocations)
                THROW 51917,'INV_TRANSFER_ALLOCATION_ROLLBACK_BLOCKED: retained allocation evidence must not be removed.',1;
            DROP TRIGGER IF EXISTS dbo.TR_InventoryTransferDispatchAllocations_Guard;
            DROP TRIGGER IF EXISTS dbo.TR_InventoryTransferReceiptAllocations_Guard;
            DROP TRIGGER IF EXISTS dbo.TR_InventoryMovements_TransferLegGuard;
            DROP TRIGGER IF EXISTS dbo.TR_StockMovements_TransferLegGuard;
            DROP TRIGGER IF EXISTS dbo.TR_WarehouseLocations_TransitIdentity;
            DROP TRIGGER IF EXISTS dbo.TR_Warehouses_TransitIdentity;
            DECLARE @definition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_StockMovements_GovernedInventoryTransfer'));
            DECLARE @before nvarchar(max)=N'WHERE i.TransferDispatchAllocationId IS NULL AND i.MovementType IN';
            IF @definition IS NULL OR (LEN(@definition)-LEN(REPLACE(@definition,@before,N'')))/LEN(@before)<>1
              THROW 51912,'INV_TRANSFER_PROJECTION_TRIGGER_DRIFT: expected allocated source guard.',1;
            SET @definition=REPLACE(@definition,@before,N'WHERE i.MovementType IN');
            SET @definition=STUFF(@definition,1,CHARINDEX(N'TRIGGER',UPPER(@definition))-1,N'CREATE OR ALTER ');
            EXEC sys.sp_executesql @definition;
            """);
    }

    public const string DispatchGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_InventoryTransferDispatchAllocations_Guard
        ON dbo.InventoryTransferDispatchAllocations AFTER INSERT,UPDATE,DELETE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS(SELECT 1 FROM deleted) THROW 51901,'INV_TRANSFER_PICK_IMMUTABLE: retained picks cannot be edited or removed.',1;
          IF EXISTS(
            SELECT 1 FROM inserted i
            LEFT JOIN dbo.InventoryTransferActions a ON a.Id=i.InventoryTransferActionId AND a.TenantId=i.TenantId AND a.IsDeleted=0
            LEFT JOIN dbo.InventoryTransferItems l ON l.Id=i.InventoryTransferItemId AND l.TenantId=i.TenantId AND l.IsDeleted=0
            LEFT JOIN dbo.InventoryTransfers t ON t.Id=l.InventoryTransferId AND t.TenantId=i.TenantId AND t.IsDeleted=0
            LEFT JOIN dbo.WarehouseLocations s ON s.Id=i.SourceLocationId AND s.TenantId=i.TenantId AND s.IsDeleted=0 AND s.IsActive=1
            LEFT JOIN dbo.Warehouses sw ON sw.Id=i.SourceInventoryWarehouseId AND sw.TenantId=i.TenantId AND sw.IsDeleted=0 AND sw.IsActive=1
            LEFT JOIN dbo.WarehouseLocations v ON v.Id=i.InTransitLocationId AND v.TenantId=i.TenantId AND v.IsDeleted=0 AND v.IsActive=1
            LEFT JOIN dbo.Warehouses vw ON vw.Id=v.WarehouseId AND vw.TenantId=i.TenantId AND vw.IsDeleted=0 AND vw.IsActive=1
            LEFT JOIN dbo.BusinessPartners bp ON bp.Id=i.CarrierBusinessPartnerId AND bp.TenantId=i.TenantId AND bp.IsDeleted=0 AND bp.IsActive=1
            LEFT JOIN dbo.InventoryTransferActionLines al ON al.InventoryTransferActionId=a.Id AND al.InventoryTransferItemId=l.Id AND al.TenantId=i.TenantId AND al.IsDeleted=0
            WHERE i.IsDeleted=1 OR i.Quantity<=0 OR a.Id IS NULL OR l.Id IS NULL OR t.Id IS NULL OR al.Id IS NULL
              OR a.InventoryTransferId<>t.Id OR a.ActionType<>5 OR t.Status NOT IN(3,5)
              OR EXISTS(SELECT 1 FROM dbo.InventoryTransferActions later WHERE later.TenantId=i.TenantId AND later.InventoryTransferId=t.Id AND later.IsDeleted=0 AND later.Sequence>a.Sequence)
              OR s.Id IS NULL OR s.WarehouseId<>t.SourceWarehouseId OR s.IsPickingLocation<>1 OR s.IsInTransitLocation<>0
              OR s.IsQuarantineLocation<>0 OR s.IsInspectionLocation<>0 OR s.IsDamageLocation<>0
              OR i.SourceInventoryWarehouseId<>CASE WHEN s.IsConsignmentBin=1 AND s.ConsignmentWarehouseId IS NOT NULL THEN s.ConsignmentWarehouseId ELSE s.WarehouseId END
              OR sw.Id IS NULL OR v.Id IS NULL OR vw.Id IS NULL OR v.IsInTransitLocation<>1 OR vw.WarehouseType<>N'Transit'
              OR v.IsPickingLocation<>0 OR v.IsReceivingLocation<>0 OR sw.IsConsignmentWarehouse<>vw.IsConsignmentWarehouse
              OR (i.CarrierBusinessPartnerId IS NOT NULL AND (bp.Id IS NULL OR bp.PartnerType NOT IN(N'Supplier',N'Vendor',N'Manufacturer',N'Both',N'CustomerAndSupplier') OR ISNULL(i.CarrierName,N'')<>bp.PartnerName OR ISNULL(i.CarrierAccountNumber,N'')<>bp.PartnerCode))
              OR (SELECT SUM(p.Quantity) FROM dbo.InventoryTransferDispatchAllocations p WITH(UPDLOCK,HOLDLOCK)
                  WHERE p.TenantId=i.TenantId AND p.InventoryTransferActionId=a.Id AND p.InventoryTransferItemId=l.Id)>al.DispatchedQuantity)
            THROW 51902,'INV_TRANSFER_PICK_INVALID: picks require the governed dispatch, exact operational bin, preserved ownership and carrier identity.',1;
        END
        """;

    public const string ReceiptGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_InventoryTransferReceiptAllocations_Guard
        ON dbo.InventoryTransferReceiptAllocations AFTER INSERT,UPDATE,DELETE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS(SELECT 1 FROM deleted) THROW 51903,'INV_TRANSFER_RECEIPT_ALLOCATION_IMMUTABLE: retained receipts cannot be edited or removed.',1;
          DECLARE @locked int;
          SELECT @locked=COUNT(*) FROM dbo.InventoryTransferDispatchAllocations p WITH(UPDLOCK,HOLDLOCK)
              WHERE EXISTS(SELECT 1 FROM inserted i WHERE i.DispatchAllocationId=p.Id);
          IF EXISTS(
            SELECT 1 FROM inserted i
            LEFT JOIN dbo.InventoryTransferDispatchAllocations p ON p.Id=i.DispatchAllocationId AND p.TenantId=i.TenantId AND p.IsDeleted=0
            LEFT JOIN dbo.InventoryTransferItems l ON l.Id=p.InventoryTransferItemId AND l.TenantId=i.TenantId AND l.IsDeleted=0
            LEFT JOIN dbo.InventoryTransfers t ON t.Id=l.InventoryTransferId AND t.TenantId=i.TenantId AND t.IsDeleted=0
            LEFT JOIN dbo.InventoryTransferActions a ON a.Id=i.InventoryTransferActionId AND a.TenantId=i.TenantId AND a.IsDeleted=0
            LEFT JOIN dbo.InventoryTransferActionLines al ON al.InventoryTransferActionId=a.Id AND al.InventoryTransferItemId=l.Id AND al.TenantId=i.TenantId AND al.IsDeleted=0
            LEFT JOIN dbo.WarehouseLocations d ON d.Id=i.DestinationLocationId AND d.TenantId=i.TenantId AND d.IsDeleted=0 AND d.IsActive=1
            LEFT JOIN dbo.Warehouses dw ON dw.Id=i.DestinationInventoryWarehouseId AND dw.TenantId=i.TenantId AND dw.IsDeleted=0 AND dw.IsActive=1
            LEFT JOIN dbo.Warehouses sw ON sw.Id=p.SourceInventoryWarehouseId AND sw.TenantId=i.TenantId
            WHERE i.IsDeleted=1 OR i.Quantity<=0 OR p.Id IS NULL OR l.Id IS NULL OR t.Id IS NULL OR a.Id IS NULL OR al.Id IS NULL
              OR a.InventoryTransferId<>t.Id OR (a.ActionType IN(6,9) AND t.Status<>5) OR (a.ActionType=7 AND t.Status<>6)
              OR EXISTS(SELECT 1 FROM dbo.InventoryTransferActions later WHERE later.TenantId=i.TenantId AND later.InventoryTransferId=t.Id AND later.IsDeleted=0 AND later.Sequence>a.Sequence)
              OR (i.ReturnedToSource=0 AND a.ActionType NOT IN(6,7)) OR (i.ReturnedToSource=1 AND a.ActionType NOT IN(7,9))
              OR (a.ActionType=7 AND ISNULL(JSON_VALUE(a.SnapshotJson,'$.Metadata.ResolutionCode'),N'')<>
                    CASE WHEN i.ReturnedToSource=1 THEN N'RETURNED_TO_SOURCE' ELSE N'REPLACEMENT_RECEIVED' END)
              OR (a.ActionType=9 AND (i.Quantity<>p.Quantity OR EXISTS(SELECT 1 FROM dbo.InventoryTransferReceiptAllocations prior
                    JOIN dbo.InventoryTransferActions pa ON pa.Id=prior.InventoryTransferActionId
                    WHERE prior.TenantId=i.TenantId AND prior.IsDeleted=0 AND pa.InventoryTransferId=t.Id AND pa.Id<>a.Id)))
              OR d.Id IS NULL OR dw.Id IS NULL OR sw.Id IS NULL OR d.IsInTransitLocation<>0 OR dw.WarehouseType=N'Transit'
              OR dw.IsConsignmentWarehouse<>sw.IsConsignmentWarehouse
              OR d.WarehouseId<>CASE WHEN i.ReturnedToSource=1 THEN t.SourceWarehouseId ELSE t.DestinationWarehouseId END
              OR i.DestinationInventoryWarehouseId<>CASE WHEN d.IsConsignmentBin=1 AND d.ConsignmentWarehouseId IS NOT NULL THEN d.ConsignmentWarehouseId ELSE d.WarehouseId END
              OR (i.ReturnedToSource=1 AND i.DestinationLocationId<>p.SourceLocationId)
              OR (i.ReturnedToSource=0 AND (d.IsReceivingLocation<>1 OR i.DestinationLocationId=p.SourceLocationId))
              OR (SELECT SUM(r.Quantity) FROM dbo.InventoryTransferReceiptAllocations r WHERE r.DispatchAllocationId=p.Id AND r.TenantId=i.TenantId)>p.Quantity
              OR (SELECT SUM(r.Quantity) FROM dbo.InventoryTransferReceiptAllocations r
                    INNER JOIN dbo.InventoryTransferDispatchAllocations rp ON rp.Id=r.DispatchAllocationId
                    WHERE r.InventoryTransferActionId=a.Id AND r.TenantId=i.TenantId AND rp.InventoryTransferItemId=l.Id)
                 >CASE WHEN a.ActionType=6 THEN al.ReceivedQuantity WHEN a.ActionType=9 THEN al.DispatchedQuantity ELSE al.DamagedQuantity+al.ShortageQuantity END)
            THROW 51904,'INV_TRANSFER_RECEIPT_ALLOCATION_INVALID: receipt must retain original pick, authorized direction, ownership and outstanding quantities.',1;
        END
        """;

    public static string MovementGuard => $$"""
        CREATE OR ALTER TRIGGER dbo.TR_InventoryMovements_TransferLegGuard
        ON dbo.InventoryMovements AFTER INSERT,UPDATE,DELETE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS(SELECT 1 FROM deleted WHERE TransferDispatchAllocationId IS NOT NULL OR TransferReceiptAllocationId IS NOT NULL OR TransferLeg IS NOT NULL)
            THROW 51905,'INV_TRANSFER_LEDGER_IMMUTABLE: allocation legs are immutable.',1;
          IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN dbo.WarehouseLocations l ON l.Id=i.LocationId LEFT JOIN dbo.Warehouses w ON w.Id=i.WarehouseId
            WHERE (l.IsInTransitLocation=1 OR w.WarehouseType=N'Transit' OR i.TransferReceiptAllocationId IS NOT NULL OR i.TransferLeg IS NOT NULL)
              AND i.TransferDispatchAllocationId IS NULL)
            THROW 51906,'INV_TRANSIT_CONTROLLED_ONLY: physical transit stock requires typed transfer allocation authority.',1;
          IF EXISTS(
            SELECT 1 FROM inserted i
            LEFT JOIN dbo.InventoryTransferDispatchAllocations p ON p.Id=i.TransferDispatchAllocationId AND p.TenantId=i.TenantId AND p.IsDeleted=0
            LEFT JOIN dbo.InventoryTransferReceiptAllocations r ON r.Id=i.TransferReceiptAllocationId AND r.TenantId=i.TenantId AND r.IsDeleted=0
            LEFT JOIN dbo.InventoryTransferItems l ON l.Id=p.InventoryTransferItemId AND l.TenantId=i.TenantId AND l.IsDeleted=0
            LEFT JOIN dbo.InventoryTransfers t ON t.Id=l.InventoryTransferId AND t.TenantId=i.TenantId AND t.IsDeleted=0
            LEFT JOIN dbo.WarehouseLocations v ON v.Id=p.InTransitLocationId AND v.TenantId=i.TenantId
            LEFT JOIN dbo.InventoryTransferActions a ON a.Id=COALESCE(r.InventoryTransferActionId,p.InventoryTransferActionId) AND a.TenantId=i.TenantId
            WHERE i.TransferDispatchAllocationId IS NOT NULL AND
             (p.Id IS NULL OR l.Id IS NULL OR t.Id IS NULL OR a.Id IS NULL OR v.Id IS NULL OR i.TransferLeg IS NULL OR i.LocationId IS NULL OR i.IsDeleted<>0 OR i.IsPosted<>1 OR i.TotalValue<0
              OR i.InventoryItemId<>l.InventoryItemId OR i.ReferenceType<>{{(int)ReferenceType.Transfer}} OR i.ReferenceId IS NULL OR i.ReferenceId<>l.Id
              OR ISNULL(i.ReferenceNumber,N'')<>t.TransferNumber OR ISNULL(i.CreatedById,'00000000-0000-0000-0000-000000000000')<>a.ActorUserId
              OR NOT (
                (i.TransferReceiptAllocationId IS NULL AND i.TransferLeg=N'SourceOut' AND i.Quantity=p.Quantity AND i.WarehouseId=p.SourceInventoryWarehouseId AND i.LocationId=p.SourceLocationId AND i.MovementType={{(int)InventoryMovementType.TransferOut}} AND i.Direction={{(int)MovementDirection.Out}})
                OR (i.TransferReceiptAllocationId IS NULL AND i.TransferLeg=N'TransitIn' AND i.Quantity=p.Quantity AND i.WarehouseId=v.WarehouseId AND i.LocationId=p.InTransitLocationId AND i.MovementType={{(int)InventoryMovementType.TransferIn}} AND i.Direction={{(int)MovementDirection.In}}
                    AND EXISTS(SELECT 1 FROM dbo.InventoryMovements s WHERE s.TransferDispatchAllocationId=p.Id AND s.TransferLeg=N'SourceOut' AND s.TenantId=i.TenantId AND s.IsPosted=1 AND s.IsDeleted=0 AND s.Quantity=i.Quantity AND s.TotalValue=i.TotalValue))
                OR (r.Id IS NOT NULL AND r.DispatchAllocationId=p.Id AND i.Quantity=r.Quantity AND
                    ((i.TransferLeg=N'TransitOut' AND i.WarehouseId=v.WarehouseId AND i.LocationId=p.InTransitLocationId AND i.MovementType={{(int)InventoryMovementType.TransferOut}} AND i.Direction={{(int)MovementDirection.Out}})
                    OR (i.TransferLeg=CASE WHEN r.ReturnedToSource=1 THEN N'SourceReturn' ELSE N'DestinationIn' END
                        AND i.WarehouseId=r.DestinationInventoryWarehouseId AND i.LocationId=r.DestinationLocationId AND i.MovementType={{(int)InventoryMovementType.TransferIn}} AND i.Direction={{(int)MovementDirection.In}}
                        AND EXISTS(SELECT 1 FROM dbo.InventoryMovements s WHERE s.TransferReceiptAllocationId=r.Id AND s.TransferLeg=N'TransitOut' AND s.TenantId=i.TenantId AND s.IsPosted=1 AND s.IsDeleted=0 AND s.Quantity=i.Quantity AND s.TotalValue=i.TotalValue)))))))
            THROW 51907,'INV_TRANSFER_LEDGER_SOURCE_INVALID: every physical leg must match its original immutable allocation and carrying value.',1;
          DECLARE @locked int;
          SELECT @locked=COUNT(*) FROM dbo.InventoryTransferDispatchAllocations p WITH(UPDLOCK,HOLDLOCK)
              WHERE EXISTS(SELECT 1 FROM inserted i WHERE i.TransferDispatchAllocationId=p.Id);
          IF EXISTS(SELECT 1 FROM inserted i WHERE i.TransferLeg=N'TransitOut' AND (
              ((SELECT COALESCE(SUM(m.Quantity),0) FROM dbo.InventoryMovements m WHERE m.TransferDispatchAllocationId=i.TransferDispatchAllocationId AND m.TransferLeg=N'TransitOut')
                =(SELECT COALESCE(SUM(m.Quantity),0) FROM dbo.InventoryMovements m WHERE m.TransferDispatchAllocationId=i.TransferDispatchAllocationId AND m.TransferLeg=N'TransitIn')
                AND (SELECT COALESCE(SUM(m.TotalValue),0) FROM dbo.InventoryMovements m WHERE m.TransferDispatchAllocationId=i.TransferDispatchAllocationId AND m.TransferLeg=N'TransitOut')
                <>(SELECT COALESCE(SUM(m.TotalValue),0) FROM dbo.InventoryMovements m WHERE m.TransferDispatchAllocationId=i.TransferDispatchAllocationId AND m.TransferLeg=N'TransitIn'))
              OR
              (SELECT COALESCE(SUM(m.Quantity),0) FROM dbo.InventoryMovements m WHERE m.TransferDispatchAllocationId=i.TransferDispatchAllocationId AND m.TransferLeg=N'TransitOut')
                >(SELECT COALESCE(SUM(m.Quantity),0) FROM dbo.InventoryMovements m WHERE m.TransferDispatchAllocationId=i.TransferDispatchAllocationId AND m.TransferLeg=N'TransitIn')
              OR (SELECT COALESCE(SUM(m.TotalValue),0) FROM dbo.InventoryMovements m WHERE m.TransferDispatchAllocationId=i.TransferDispatchAllocationId AND m.TransferLeg=N'TransitOut')
                >(SELECT COALESCE(SUM(m.TotalValue),0) FROM dbo.InventoryMovements m WHERE m.TransferDispatchAllocationId=i.TransferDispatchAllocationId AND m.TransferLeg=N'TransitIn')))
            THROW 51908,'INV_TRANSIT_OVER_RELEASE: quantity/value cannot exceed the original transit entry.',1;
        END
        """;

    public static string ProjectionGuard => $$"""
        CREATE OR ALTER TRIGGER dbo.TR_StockMovements_TransferLegGuard
        ON dbo.StockMovements AFTER INSERT,UPDATE,DELETE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS(SELECT 1 FROM deleted WHERE TransferDispatchAllocationId IS NOT NULL OR TransferReceiptAllocationId IS NOT NULL OR TransferLeg IS NOT NULL)
            THROW 51909,'INV_TRANSFER_PROJECTION_IMMUTABLE: allocated movement projections are immutable.',1;
          IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN dbo.WarehouseLocations l ON l.Id=i.LocationId LEFT JOIN dbo.Warehouses w ON w.Id=i.WarehouseId
            WHERE (l.IsInTransitLocation=1 OR w.WarehouseType=N'Transit' OR i.TransferReceiptAllocationId IS NOT NULL OR i.TransferLeg IS NOT NULL)
              AND i.TransferDispatchAllocationId IS NULL)
            THROW 51910,'INV_TRANSIT_PROJECTION_CONTROLLED_ONLY: transit projections require exact ledger authority.',1;
          IF EXISTS(SELECT 1 FROM inserted i WHERE i.TransferDispatchAllocationId IS NOT NULL AND NOT EXISTS(
              SELECT 1 FROM dbo.InventoryMovements m
              INNER JOIN dbo.InventoryTransferDispatchAllocations p ON p.Id=m.TransferDispatchAllocationId
              INNER JOIN dbo.InventoryTransferItems l ON l.Id=p.InventoryTransferItemId
              WHERE m.TenantId=i.TenantId AND m.IsDeleted=0 AND m.IsPosted=1 AND i.IsDeleted=0
                AND m.TransferDispatchAllocationId=i.TransferDispatchAllocationId AND m.TransferLeg=i.TransferLeg
                AND ISNULL(m.TransferReceiptAllocationId,'00000000-0000-0000-0000-000000000000')=ISNULL(i.TransferReceiptAllocationId,'00000000-0000-0000-0000-000000000000')
                AND m.InventoryItemId=i.InventoryItemId AND m.WarehouseId=i.WarehouseId AND m.LocationId=i.LocationId
                AND i.Quantity=m.Quantity*CASE WHEN m.Direction={{(int)MovementDirection.Out}} THEN -1 ELSE 1 END
                AND i.TotalValue=m.TotalValue*CASE WHEN m.Direction={{(int)MovementDirection.Out}} THEN -1 ELSE 1 END
                AND i.ReferenceType={{(int)ReferenceType.Transfer}} AND i.ReferenceId=l.InventoryTransferId AND i.ReferenceNumber=m.ReferenceNumber AND i.ProcessedById=m.CreatedById))
            THROW 51911,'INV_TRANSFER_PROJECTION_MISMATCH: projection must match its posted allocation ledger leg.',1;
        END
        """;

    public const string LegacyProjectionPatch = """
        DECLARE @definition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_StockMovements_GovernedInventoryTransfer'));
        DECLARE @before nvarchar(max)=N'WHERE i.MovementType IN (N''TransferOut'', N''TransferIn'', N''TransferReversal'', N''TransferDiscrepancyReturn'', N''TransferReplacementIn'')';
        IF @definition IS NULL OR (LEN(@definition)-LEN(REPLACE(@definition,@before,N'')))/LEN(@before)<>1
          THROW 51912,'INV_TRANSFER_PROJECTION_TRIGGER_DRIFT: expected original source guard.',1;
        SET @definition=REPLACE(@definition,@before,N'WHERE i.TransferDispatchAllocationId IS NULL AND i.MovementType IN (N''TransferOut'', N''TransferIn'', N''TransferReversal'', N''TransferDiscrepancyReturn'', N''TransferReplacementIn'')');
        SET @definition=STUFF(@definition,1,CHARINDEX(N'TRIGGER',UPPER(@definition))-1,N'CREATE OR ALTER ');
        EXEC sys.sp_executesql @definition;
        """;

    public const string TransitLocationGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_WarehouseLocations_TransitIdentity
        ON dbo.WarehouseLocations AFTER UPDATE,DELETE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id
            WHERE (d.IsInTransitLocation=1 OR EXISTS(SELECT 1 FROM dbo.Warehouses w WHERE w.Id=d.WarehouseId AND w.WarehouseType=N'Transit'))
              AND (i.Id IS NULL OR i.TenantId<>d.TenantId OR i.WarehouseId<>d.WarehouseId OR i.LocationCode<>d.LocationCode
                   OR i.IsInTransitLocation<>d.IsInTransitLocation OR i.IsActive<>d.IsActive OR i.IsDeleted<>d.IsDeleted
                   OR i.IsPickingLocation<>d.IsPickingLocation OR i.IsReceivingLocation<>d.IsReceivingLocation
                   OR i.IsConsignmentBin<>d.IsConsignmentBin OR ISNULL(i.ConsignmentWarehouseId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.ConsignmentWarehouseId,'00000000-0000-0000-0000-000000000000')))
            THROW 51913,'INV_TRANSIT_LOCATION_IMMUTABLE: preserve system transit identity and ownership.',1;
          IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id WHERE d.IsInTransitLocation=0 AND i.IsInTransitLocation=1)
            THROW 51914,'INV_TRANSIT_LOCATION_CONVERSION_BLOCKED: create dedicated transit identity; never convert an operational bin.',1;
        END
        """;

    public const string TransitWarehouseGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_Warehouses_TransitIdentity
        ON dbo.Warehouses AFTER UPDATE,DELETE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE d.WarehouseType=N'Transit'
            AND (i.Id IS NULL OR i.TenantId<>d.TenantId OR i.Code<>d.Code OR i.WarehouseType<>d.WarehouseType
                OR i.IsActive<>d.IsActive OR i.IsDeleted<>d.IsDeleted OR i.IsConsignmentWarehouse<>d.IsConsignmentWarehouse))
            THROW 51915,'INV_TRANSIT_WAREHOUSE_IMMUTABLE: preserve system transit ownership.',1;
          IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id WHERE d.WarehouseType<>N'Transit' AND i.WarehouseType=N'Transit')
            THROW 51916,'INV_TRANSIT_WAREHOUSE_CONVERSION_BLOCKED: create a dedicated system warehouse.',1;
        END
        """;
}
