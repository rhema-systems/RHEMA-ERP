using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ErpSystem.Data.ApplicationDbContext))]
[Migration("20260730204500_TDC0403CommercialCapacityHardStops")]
public partial class TDC0403CommercialCapacityHardStops : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [TR_PurchaseOrders_ApprovedCommercialCapacity]
            ON [PurchaseOrders]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                -- Serialize every contract family touched by this statement so
                -- concurrent direct integrations cannot both observe the same
                -- remaining capacity.
                IF EXISTS (
                    SELECT 1
                    FROM Contracts contract WITH (UPDLOCK, HOLDLOCK)
                    JOIN inserted i
                      ON i.TenantId = contract.TenantId
                     AND i.ProcurementSourceType = 2
                     AND i.ProcurementSourceId = contract.Id
                    WHERE contract.IsDeleted = 0)
                BEGIN
                    -- The lock acquired above is intentionally held until the
                    -- surrounding transaction commits.
                    SET NOCOUNT ON;
                END;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN RequestForQuotations rfq
                      ON rfq.Id = i.ProcurementSourceId
                     AND rfq.TenantId = i.TenantId
                     AND rfq.IsDeleted = 0
                    WHERE i.ProcurementSourceType = 0
                      AND (
                           rfq.Id IS NULL
                        OR UPPER(LTRIM(RTRIM(ISNULL(i.Currency, '')))) <>
                           UPPER(LTRIM(RTRIM(ISNULL(rfq.Currency, ''))))
                        OR ROUND(i.TotalAmount, 2) <> ISNULL((
                            SELECT ROUND(SUM(awardLine.LineTotal), 2)
                            FROM RequestForQuotationAwardLines awardLine
                            WHERE awardLine.TenantId = i.TenantId
                              AND awardLine.RfqId = rfq.Id
                              AND awardLine.BusinessPartnerId =
                                  i.BusinessPartnerId
                              AND awardLine.IsDeleted = 0), -1)))
                    THROW 51222, 'The RFQ purchase-order currency or total differs from the awarded commercial terms.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    WHERE i.ProcurementSourceType = 0
                      AND i.Status IN (
                          'Submitted', 'Pending Approval', 'Approved',
                          'Sent', 'Acknowledged')
                      AND (
                          EXISTS (
                              SELECT
                                  approved.LineIdentity,
                                  approved.UnitOfMeasure,
                                  approved.UnitPrice,
                                  approved.Quantity,
                                  approved.LineTotal
                              FROM (
                                  SELECT
                                      awardLine.TenantId,
                                      awardLine.RfqId,
                                      awardLine.BusinessPartnerId,
                                      CASE
                                          WHEN rfqItem.InventoryItemId IS NOT NULL
                                              THEN CONCAT(
                                                  'inventory:',
                                                  LOWER(CONVERT(
                                                      varchar(36),
                                                      rfqItem.InventoryItemId)))
                                          ELSE CONCAT(
                                              'description:',
                                              LOWER(LTRIM(RTRIM(
                                                  ISNULL(
                                                      rfqItem.Description,
                                                      '')))))
                                      END AS LineIdentity,
                                      ISNULL(NULLIF(
                                          UPPER(LTRIM(RTRIM(
                                              rfqItem.UnitOfMeasure))),
                                          ''), 'EA') AS UnitOfMeasure,
                                      ROUND(awardLine.UnitPrice, 4) AS UnitPrice,
                                      ROUND(SUM(rfqItem.Quantity), 4) AS Quantity,
                                      ROUND(SUM(awardLine.LineTotal), 2)
                                          AS LineTotal
                                  FROM RequestForQuotationAwardLines awardLine
                                  JOIN RequestForQuotationItems rfqItem
                                    ON rfqItem.Id = awardLine.RfqItemId
                                   AND rfqItem.RfqId = awardLine.RfqId
                                   AND rfqItem.TenantId = awardLine.TenantId
                                   AND rfqItem.IsDeleted = 0
                                  WHERE awardLine.IsDeleted = 0
                                  GROUP BY
                                      awardLine.TenantId,
                                      awardLine.RfqId,
                                      awardLine.BusinessPartnerId,
                                      CASE
                                          WHEN rfqItem.InventoryItemId IS NOT NULL
                                              THEN CONCAT(
                                                  'inventory:',
                                                  LOWER(CONVERT(
                                                      varchar(36),
                                                      rfqItem.InventoryItemId)))
                                          ELSE CONCAT(
                                              'description:',
                                              LOWER(LTRIM(RTRIM(
                                                  ISNULL(
                                                      rfqItem.Description,
                                                      '')))))
                                      END,
                                      ISNULL(NULLIF(
                                          UPPER(LTRIM(RTRIM(
                                              rfqItem.UnitOfMeasure))),
                                          ''), 'EA'),
                                      ROUND(awardLine.UnitPrice, 4)
                              ) approved
                              WHERE approved.TenantId = i.TenantId
                                AND approved.RfqId = i.ProcurementSourceId
                                AND approved.BusinessPartnerId =
                                    i.BusinessPartnerId
                              EXCEPT
                              SELECT
                                  ordered.LineIdentity,
                                  ordered.UnitOfMeasure,
                                  ordered.UnitPrice,
                                  ordered.Quantity,
                                  ordered.LineTotal
                              FROM (
                                  SELECT
                                      item.TenantId,
                                      item.PurchaseOrderId,
                                      CASE
                                          WHEN item.InventoryItemId IS NOT NULL
                                              THEN CONCAT(
                                                  'inventory:',
                                                  LOWER(CONVERT(
                                                      varchar(36),
                                                      item.InventoryItemId)))
                                          ELSE CONCAT(
                                              'description:',
                                              LOWER(LTRIM(RTRIM(
                                                  ISNULL(
                                                      item.ItemDescription,
                                                      '')))))
                                      END AS LineIdentity,
                                      ISNULL(NULLIF(
                                          UPPER(LTRIM(RTRIM(
                                              item.UnitOfMeasure))),
                                          ''), 'EA') AS UnitOfMeasure,
                                      ROUND(item.UnitPrice, 4) AS UnitPrice,
                                      ROUND(SUM(item.OrderedQuantity), 4)
                                          AS Quantity,
                                      ROUND(SUM(ROUND(
                                          item.OrderedQuantity *
                                          item.UnitPrice, 2)), 2)
                                          AS LineTotal
                                  FROM PurchaseOrderItems item
                                  WHERE item.IsDeleted = 0
                                  GROUP BY
                                      item.TenantId,
                                      item.PurchaseOrderId,
                                      CASE
                                          WHEN item.InventoryItemId IS NOT NULL
                                              THEN CONCAT(
                                                  'inventory:',
                                                  LOWER(CONVERT(
                                                      varchar(36),
                                                      item.InventoryItemId)))
                                          ELSE CONCAT(
                                              'description:',
                                              LOWER(LTRIM(RTRIM(
                                                  ISNULL(
                                                      item.ItemDescription,
                                                      '')))))
                                      END,
                                      ISNULL(NULLIF(
                                          UPPER(LTRIM(RTRIM(
                                              item.UnitOfMeasure))),
                                          ''), 'EA'),
                                      ROUND(item.UnitPrice, 4)
                              ) ordered
                              WHERE ordered.TenantId = i.TenantId
                                AND ordered.PurchaseOrderId = i.Id)
                       OR EXISTS (
                              SELECT
                                  ordered.LineIdentity,
                                  ordered.UnitOfMeasure,
                                  ordered.UnitPrice,
                                  ordered.Quantity,
                                  ordered.LineTotal
                              FROM (
                                  SELECT
                                      item.TenantId,
                                      item.PurchaseOrderId,
                                      CASE
                                          WHEN item.InventoryItemId IS NOT NULL
                                              THEN CONCAT(
                                                  'inventory:',
                                                  LOWER(CONVERT(
                                                      varchar(36),
                                                      item.InventoryItemId)))
                                          ELSE CONCAT(
                                              'description:',
                                              LOWER(LTRIM(RTRIM(
                                                  ISNULL(
                                                      item.ItemDescription,
                                                      '')))))
                                      END AS LineIdentity,
                                      ISNULL(NULLIF(
                                          UPPER(LTRIM(RTRIM(
                                              item.UnitOfMeasure))),
                                          ''), 'EA') AS UnitOfMeasure,
                                      ROUND(item.UnitPrice, 4) AS UnitPrice,
                                      ROUND(SUM(item.OrderedQuantity), 4)
                                          AS Quantity,
                                      ROUND(SUM(ROUND(
                                          item.OrderedQuantity *
                                          item.UnitPrice, 2)), 2)
                                          AS LineTotal
                                  FROM PurchaseOrderItems item
                                  WHERE item.IsDeleted = 0
                                  GROUP BY
                                      item.TenantId,
                                      item.PurchaseOrderId,
                                      CASE
                                          WHEN item.InventoryItemId IS NOT NULL
                                              THEN CONCAT(
                                                  'inventory:',
                                                  LOWER(CONVERT(
                                                      varchar(36),
                                                      item.InventoryItemId)))
                                          ELSE CONCAT(
                                              'description:',
                                              LOWER(LTRIM(RTRIM(
                                                  ISNULL(
                                                      item.ItemDescription,
                                                      '')))))
                                      END,
                                      ISNULL(NULLIF(
                                          UPPER(LTRIM(RTRIM(
                                              item.UnitOfMeasure))),
                                          ''), 'EA'),
                                      ROUND(item.UnitPrice, 4)
                              ) ordered
                              WHERE ordered.TenantId = i.TenantId
                                AND ordered.PurchaseOrderId = i.Id
                              EXCEPT
                              SELECT
                                  approved.LineIdentity,
                                  approved.UnitOfMeasure,
                                  approved.UnitPrice,
                                  approved.Quantity,
                                  approved.LineTotal
                              FROM (
                                  SELECT
                                      awardLine.TenantId,
                                      awardLine.RfqId,
                                      awardLine.BusinessPartnerId,
                                      CASE
                                          WHEN rfqItem.InventoryItemId IS NOT NULL
                                              THEN CONCAT(
                                                  'inventory:',
                                                  LOWER(CONVERT(
                                                      varchar(36),
                                                      rfqItem.InventoryItemId)))
                                          ELSE CONCAT(
                                              'description:',
                                              LOWER(LTRIM(RTRIM(
                                                  ISNULL(
                                                      rfqItem.Description,
                                                      '')))))
                                      END AS LineIdentity,
                                      ISNULL(NULLIF(
                                          UPPER(LTRIM(RTRIM(
                                              rfqItem.UnitOfMeasure))),
                                          ''), 'EA') AS UnitOfMeasure,
                                      ROUND(awardLine.UnitPrice, 4) AS UnitPrice,
                                      ROUND(SUM(rfqItem.Quantity), 4) AS Quantity,
                                      ROUND(SUM(awardLine.LineTotal), 2)
                                          AS LineTotal
                                  FROM RequestForQuotationAwardLines awardLine
                                  JOIN RequestForQuotationItems rfqItem
                                    ON rfqItem.Id = awardLine.RfqItemId
                                   AND rfqItem.RfqId = awardLine.RfqId
                                   AND rfqItem.TenantId = awardLine.TenantId
                                   AND rfqItem.IsDeleted = 0
                                  WHERE awardLine.IsDeleted = 0
                                  GROUP BY
                                      awardLine.TenantId,
                                      awardLine.RfqId,
                                      awardLine.BusinessPartnerId,
                                      CASE
                                          WHEN rfqItem.InventoryItemId IS NOT NULL
                                              THEN CONCAT(
                                                  'inventory:',
                                                  LOWER(CONVERT(
                                                      varchar(36),
                                                      rfqItem.InventoryItemId)))
                                          ELSE CONCAT(
                                              'description:',
                                              LOWER(LTRIM(RTRIM(
                                                  ISNULL(
                                                      rfqItem.Description,
                                                      '')))))
                                      END,
                                      ISNULL(NULLIF(
                                          UPPER(LTRIM(RTRIM(
                                              rfqItem.UnitOfMeasure))),
                                          ''), 'EA'),
                                      ROUND(awardLine.UnitPrice, 4)
                              ) approved
                              WHERE approved.TenantId = i.TenantId
                                AND approved.RfqId = i.ProcurementSourceId
                                AND approved.BusinessPartnerId =
                                    i.BusinessPartnerId)
                       OR EXISTS (
                              SELECT 1
                              FROM PurchaseOrderItems item
                              WHERE item.TenantId = i.TenantId
                                AND item.PurchaseOrderId = i.Id
                                AND item.IsDeleted = 0
                                AND ROUND(item.LineTotal, 2) <>
                                    ROUND(
                                        item.OrderedQuantity *
                                        item.UnitPrice,
                                        2))))
                    THROW 51223, 'The RFQ purchase-order lines differ from the awarded commercial terms.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    JOIN Contracts contract WITH (UPDLOCK, HOLDLOCK)
                      ON contract.Id = i.ProcurementSourceId
                     AND contract.TenantId = i.TenantId
                     AND contract.IsDeleted = 0
                    WHERE i.ProcurementSourceType = 2
                      AND (
                           UPPER(LTRIM(RTRIM(ISNULL(i.Currency, '')))) <>
                           UPPER(LTRIM(RTRIM(
                               ISNULL(contract.Currency, ''))))
                        OR (
                            SELECT ROUND(SUM(existing.TotalAmount), 2)
                            FROM PurchaseOrders existing
                            WHERE existing.TenantId = i.TenantId
                              AND existing.ProcurementSourceType = 2
                              AND existing.ProcurementSourceId = contract.Id
                              AND existing.IsDeleted = 0
                              AND existing.Status NOT IN (
                                  'Cancelled', 'Rejected')
                        ) > ROUND(contract.ContractValue, 2)
                        OR EXISTS (
                            SELECT 1
                            FROM PurchaseOrders existing
                            JOIN PurchaseOrderItems item
                              ON item.PurchaseOrderId = existing.Id
                             AND item.TenantId = existing.TenantId
                             AND item.IsDeleted = 0
                            WHERE existing.TenantId = i.TenantId
                              AND existing.ProcurementSourceType = 2
                              AND existing.ProcurementSourceId =
                                  contract.Id
                              AND existing.IsDeleted = 0
                              AND existing.Status NOT IN (
                                  'Cancelled', 'Rejected')
                              AND ROUND(item.LineTotal, 2) <>
                                  ROUND(
                                      item.OrderedQuantity *
                                      item.UnitPrice,
                                      2))))
                    THROW 51224, 'The contract purchase orders exceed the contract value or use a different currency.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM (
                        SELECT
                            purchaseOrder.TenantId,
                            purchaseOrder.ProcurementSourceId AS ContractId,
                            CASE
                                WHEN item.InventoryItemId IS NOT NULL
                                    THEN CONCAT(
                                        'inventory:',
                                        LOWER(CONVERT(
                                            varchar(36),
                                            item.InventoryItemId)))
                                ELSE CONCAT(
                                    'description:',
                                    LOWER(LTRIM(RTRIM(
                                        ISNULL(item.ItemDescription, '')))))
                            END AS LineIdentity,
                            ISNULL(NULLIF(
                                UPPER(LTRIM(RTRIM(item.UnitOfMeasure))),
                                ''), 'EA') AS UnitOfMeasure,
                            ROUND(item.UnitPrice, 4) AS UnitPrice,
                            ROUND(SUM(item.OrderedQuantity), 4) AS Quantity,
                            ROUND(SUM(ROUND(
                                item.OrderedQuantity * item.UnitPrice, 2)), 2)
                                AS LineTotal
                        FROM PurchaseOrders purchaseOrder
                        JOIN PurchaseOrderItems item
                          ON item.PurchaseOrderId = purchaseOrder.Id
                         AND item.TenantId = purchaseOrder.TenantId
                         AND item.IsDeleted = 0
                        WHERE purchaseOrder.ProcurementSourceType = 2
                          AND purchaseOrder.IsDeleted = 0
                          AND purchaseOrder.Status NOT IN (
                              'Cancelled', 'Rejected')
                          AND purchaseOrder.ProcurementSourceId IN (
                              SELECT changed.ProcurementSourceId
                              FROM inserted changed
                              WHERE changed.ProcurementSourceType = 2)
                        GROUP BY
                            purchaseOrder.TenantId,
                            purchaseOrder.ProcurementSourceId,
                            CASE
                                WHEN item.InventoryItemId IS NOT NULL
                                    THEN CONCAT(
                                        'inventory:',
                                        LOWER(CONVERT(
                                            varchar(36),
                                            item.InventoryItemId)))
                                ELSE CONCAT(
                                    'description:',
                                    LOWER(LTRIM(RTRIM(
                                        ISNULL(item.ItemDescription, '')))))
                            END,
                            ISNULL(NULLIF(
                                UPPER(LTRIM(RTRIM(item.UnitOfMeasure))),
                                ''), 'EA'),
                            ROUND(item.UnitPrice, 4)
                    ) actual
                    LEFT JOIN (
                        SELECT
                            contract.TenantId,
                            contract.Id AS ContractId,
                            CONCAT(
                                'description:',
                                LOWER(LTRIM(RTRIM(
                                    ISNULL(tenderItem.Description, '')))))
                                AS LineIdentity,
                            ISNULL(NULLIF(
                                UPPER(LTRIM(RTRIM(
                                    tenderItem.UnitOfMeasure))),
                                ''), 'EA') AS UnitOfMeasure,
                            ROUND(COALESCE(
                                negotiationItem.NegotiatedUnitPrice,
                                bidItem.UnitPrice), 4) AS UnitPrice,
                            ROUND(SUM(CASE
                                WHEN negotiationItem.Quantity > 0
                                    THEN negotiationItem.Quantity
                                ELSE bidItem.OfferedQuantity
                            END), 4) AS Quantity,
                            ROUND(SUM(COALESCE(
                                negotiationItem.NegotiatedTotalPrice,
                                ROUND(
                                    (CASE
                                        WHEN negotiationItem.Quantity > 0
                                            THEN negotiationItem.Quantity
                                        ELSE bidItem.OfferedQuantity
                                     END) *
                                    COALESCE(
                                        negotiationItem.NegotiatedUnitPrice,
                                        bidItem.UnitPrice),
                                    2))), 2) AS LineTotal
                        FROM Contracts contract WITH (UPDLOCK, HOLDLOCK)
                        JOIN TenderAwards award
                          ON award.Id = contract.TenderAwardId
                         AND award.TenantId = contract.TenantId
                         AND award.IsDeleted = 0
                        JOIN TenderBidItems bidItem
                          ON bidItem.TenderBidId = award.TenderBidId
                         AND bidItem.TenantId = award.TenantId
                         AND bidItem.IsDeleted = 0
                         AND (
                             award.BidLotId IS NULL OR
                             bidItem.BidLotId = award.BidLotId)
                        JOIN TenderItems tenderItem
                          ON tenderItem.Id = bidItem.TenderItemId
                         AND tenderItem.TenantId = bidItem.TenantId
                         AND tenderItem.IsDeleted = 0
                        LEFT JOIN TenderNegotiationItems negotiationItem
                          ON negotiationItem.NegotiationId =
                             award.NegotiationId
                         AND negotiationItem.TenderBidItemId = bidItem.Id
                         AND negotiationItem.TenantId = bidItem.TenantId
                         AND negotiationItem.IsDeleted = 0
                        WHERE contract.IsDeleted = 0
                          AND contract.Id IN (
                              SELECT changed.ProcurementSourceId
                              FROM inserted changed
                              WHERE changed.ProcurementSourceType = 2)
                        GROUP BY
                            contract.TenantId,
                            contract.Id,
                            CONCAT(
                                'description:',
                                LOWER(LTRIM(RTRIM(
                                    ISNULL(tenderItem.Description, ''))))),
                            ISNULL(NULLIF(
                                UPPER(LTRIM(RTRIM(
                                    tenderItem.UnitOfMeasure))),
                                ''), 'EA'),
                            ROUND(COALESCE(
                                negotiationItem.NegotiatedUnitPrice,
                                bidItem.UnitPrice), 4)
                    ) approved
                      ON approved.TenantId = actual.TenantId
                     AND approved.ContractId = actual.ContractId
                     AND approved.LineIdentity = actual.LineIdentity
                     AND approved.UnitOfMeasure = actual.UnitOfMeasure
                     AND approved.UnitPrice = actual.UnitPrice
                    WHERE approved.ContractId IS NULL
                       OR actual.Quantity > approved.Quantity
                       OR actual.LineTotal > approved.LineTotal)
                    THROW 51225, 'The cumulative contract purchase-order lines exceed the awarded quantities or values.', 1;
            END
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [TR_PurchaseOrderItems_ApprovedCommercialCapacity]
            ON [PurchaseOrderItems]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                SELECT
                    purchaseOrder.Id AS PurchaseOrderId,
                    CASE
                        WHEN rfqItem.InventoryItemId IS NOT NULL
                            THEN CONCAT(
                                'inventory:',
                                LOWER(CONVERT(
                                    varchar(36),
                                    rfqItem.InventoryItemId)))
                        ELSE CONCAT(
                            'description:',
                            LOWER(LTRIM(RTRIM(
                                ISNULL(rfqItem.Description, '')))))
                    END AS LineIdentity,
                    ISNULL(NULLIF(
                        UPPER(LTRIM(RTRIM(rfqItem.UnitOfMeasure))),
                        ''), 'EA') AS UnitOfMeasure,
                    ROUND(awardLine.UnitPrice, 4) AS UnitPrice,
                    ROUND(SUM(rfqItem.Quantity), 4) AS Quantity,
                    ROUND(SUM(awardLine.LineTotal), 2) AS LineTotal
                INTO #AffectedRfqApproved
                FROM PurchaseOrders purchaseOrder
                JOIN (
                    SELECT PurchaseOrderId FROM inserted
                    UNION
                    SELECT PurchaseOrderId FROM deleted
                ) affected ON affected.PurchaseOrderId = purchaseOrder.Id
                JOIN RequestForQuotationAwardLines awardLine
                  ON awardLine.TenantId = purchaseOrder.TenantId
                 AND awardLine.RfqId =
                     purchaseOrder.ProcurementSourceId
                 AND awardLine.BusinessPartnerId =
                     purchaseOrder.BusinessPartnerId
                 AND awardLine.IsDeleted = 0
                JOIN RequestForQuotationItems rfqItem
                  ON rfqItem.Id = awardLine.RfqItemId
                 AND rfqItem.RfqId = awardLine.RfqId
                 AND rfqItem.TenantId = awardLine.TenantId
                 AND rfqItem.IsDeleted = 0
                WHERE purchaseOrder.ProcurementSourceType = 0
                  AND purchaseOrder.Status IN (
                      'Submitted', 'Pending Approval', 'Approved',
                      'Sent', 'Acknowledged')
                GROUP BY
                    purchaseOrder.Id,
                    CASE
                        WHEN rfqItem.InventoryItemId IS NOT NULL
                            THEN CONCAT(
                                'inventory:',
                                LOWER(CONVERT(
                                    varchar(36),
                                    rfqItem.InventoryItemId)))
                        ELSE CONCAT(
                            'description:',
                            LOWER(LTRIM(RTRIM(
                                ISNULL(rfqItem.Description, '')))))
                    END,
                    ISNULL(NULLIF(
                        UPPER(LTRIM(RTRIM(rfqItem.UnitOfMeasure))),
                        ''), 'EA'),
                    ROUND(awardLine.UnitPrice, 4);

                SELECT
                    purchaseOrder.Id AS PurchaseOrderId,
                    CASE
                        WHEN item.InventoryItemId IS NOT NULL
                            THEN CONCAT(
                                'inventory:',
                                LOWER(CONVERT(
                                    varchar(36),
                                    item.InventoryItemId)))
                        ELSE CONCAT(
                            'description:',
                            LOWER(LTRIM(RTRIM(
                                ISNULL(item.ItemDescription, '')))))
                    END AS LineIdentity,
                    ISNULL(NULLIF(
                        UPPER(LTRIM(RTRIM(item.UnitOfMeasure))),
                        ''), 'EA') AS UnitOfMeasure,
                    ROUND(item.UnitPrice, 4) AS UnitPrice,
                    ROUND(SUM(item.OrderedQuantity), 4) AS Quantity,
                    ROUND(SUM(ROUND(
                        item.OrderedQuantity * item.UnitPrice, 2)), 2)
                        AS LineTotal
                INTO #AffectedRfqActual
                FROM PurchaseOrders purchaseOrder
                JOIN (
                    SELECT PurchaseOrderId FROM inserted
                    UNION
                    SELECT PurchaseOrderId FROM deleted
                ) affected ON affected.PurchaseOrderId = purchaseOrder.Id
                JOIN PurchaseOrderItems item
                  ON item.PurchaseOrderId = purchaseOrder.Id
                 AND item.TenantId = purchaseOrder.TenantId
                 AND item.IsDeleted = 0
                WHERE purchaseOrder.ProcurementSourceType = 0
                  AND purchaseOrder.Status IN (
                      'Submitted', 'Pending Approval', 'Approved',
                      'Sent', 'Acknowledged')
                GROUP BY
                    purchaseOrder.Id,
                    CASE
                        WHEN item.InventoryItemId IS NOT NULL
                            THEN CONCAT(
                                'inventory:',
                                LOWER(CONVERT(
                                    varchar(36),
                                    item.InventoryItemId)))
                        ELSE CONCAT(
                            'description:',
                            LOWER(LTRIM(RTRIM(
                                ISNULL(item.ItemDescription, '')))))
                    END,
                    ISNULL(NULLIF(
                        UPPER(LTRIM(RTRIM(item.UnitOfMeasure))),
                        ''), 'EA'),
                    ROUND(item.UnitPrice, 4);

                IF EXISTS (
                    SELECT 1
                    FROM PurchaseOrderItems changed
                    JOIN (
                        SELECT PurchaseOrderId FROM inserted
                        UNION
                        SELECT PurchaseOrderId FROM deleted
                    ) affected ON affected.PurchaseOrderId =
                                  changed.PurchaseOrderId
                    JOIN PurchaseOrders purchaseOrder
                      ON purchaseOrder.Id = changed.PurchaseOrderId
                     AND purchaseOrder.TenantId = changed.TenantId
                    WHERE changed.IsDeleted = 0
                      AND (
                          (
                              purchaseOrder.ProcurementSourceType = 0
                              AND purchaseOrder.Status IN (
                                  'Submitted', 'Pending Approval', 'Approved',
                                  'Sent', 'Acknowledged')
                          )
                          OR (
                              purchaseOrder.ProcurementSourceType = 2
                              AND purchaseOrder.Status NOT IN (
                                  'Cancelled', 'Rejected')
                          ))
                      AND ROUND(changed.LineTotal, 2) <>
                          ROUND(
                              changed.OrderedQuantity *
                              changed.UnitPrice,
                              2))
                    THROW 51226, 'A governed purchase-order line total differs from its quantity and awarded unit price.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM #AffectedRfqApproved approved
                    FULL OUTER JOIN #AffectedRfqActual actual
                      ON actual.PurchaseOrderId = approved.PurchaseOrderId
                     AND actual.LineIdentity = approved.LineIdentity
                     AND actual.UnitOfMeasure = approved.UnitOfMeasure
                     AND actual.UnitPrice = approved.UnitPrice
                    WHERE approved.PurchaseOrderId IS NULL
                       OR actual.PurchaseOrderId IS NULL
                       OR actual.Quantity <> approved.Quantity
                       OR actual.LineTotal <> approved.LineTotal)
                    THROW 51227, 'Protected RFQ purchase-order lines must exactly match the awarded commercial terms.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM PurchaseOrders purchaseOrder
                    JOIN (
                        SELECT PurchaseOrderId FROM inserted
                        UNION
                        SELECT PurchaseOrderId FROM deleted
                    ) affected ON affected.PurchaseOrderId =
                                  purchaseOrder.Id
                    WHERE purchaseOrder.ProcurementSourceType = 0
                      AND purchaseOrder.Status IN (
                          'Submitted', 'Pending Approval', 'Approved',
                          'Sent', 'Acknowledged')
                      AND NOT EXISTS (
                          SELECT 1
                          FROM #AffectedRfqApproved approved
                          WHERE approved.PurchaseOrderId =
                                purchaseOrder.Id))
                    THROW 51227, 'Protected RFQ purchase orders require authoritative awarded commercial lines.', 1;

                -- Contract line changes are checked across every non-terminal
                -- order for the same contract, not only the current PO.
                IF EXISTS (
                    SELECT 1
                    FROM (
                        SELECT
                            purchaseOrder.TenantId,
                            purchaseOrder.ProcurementSourceId AS ContractId,
                            CASE
                                WHEN item.InventoryItemId IS NOT NULL
                                    THEN CONCAT(
                                        'inventory:',
                                        LOWER(CONVERT(
                                            varchar(36),
                                            item.InventoryItemId)))
                                ELSE CONCAT(
                                    'description:',
                                    LOWER(LTRIM(RTRIM(
                                        ISNULL(item.ItemDescription, '')))))
                            END AS LineIdentity,
                            ISNULL(NULLIF(
                                UPPER(LTRIM(RTRIM(item.UnitOfMeasure))),
                                ''), 'EA') AS UnitOfMeasure,
                            ROUND(item.UnitPrice, 4) AS UnitPrice,
                            ROUND(SUM(item.OrderedQuantity), 4) AS Quantity,
                            ROUND(SUM(ROUND(
                                item.OrderedQuantity * item.UnitPrice, 2)), 2)
                                AS LineTotal
                        FROM PurchaseOrders purchaseOrder
                        JOIN PurchaseOrderItems item
                          ON item.PurchaseOrderId = purchaseOrder.Id
                         AND item.TenantId = purchaseOrder.TenantId
                         AND item.IsDeleted = 0
                        WHERE purchaseOrder.ProcurementSourceType = 2
                          AND purchaseOrder.IsDeleted = 0
                          AND purchaseOrder.Status NOT IN (
                              'Cancelled', 'Rejected')
                          AND purchaseOrder.ProcurementSourceId IN (
                              SELECT changedOrder.ProcurementSourceId
                              FROM PurchaseOrders changedOrder
                              JOIN (
                                  SELECT PurchaseOrderId FROM inserted
                                  UNION
                                  SELECT PurchaseOrderId FROM deleted
                              ) affected
                                ON affected.PurchaseOrderId =
                                   changedOrder.Id
                              WHERE changedOrder.ProcurementSourceType = 2)
                        GROUP BY
                            purchaseOrder.TenantId,
                            purchaseOrder.ProcurementSourceId,
                            CASE
                                WHEN item.InventoryItemId IS NOT NULL
                                    THEN CONCAT(
                                        'inventory:',
                                        LOWER(CONVERT(
                                            varchar(36),
                                            item.InventoryItemId)))
                                ELSE CONCAT(
                                    'description:',
                                    LOWER(LTRIM(RTRIM(
                                        ISNULL(item.ItemDescription, '')))))
                            END,
                            ISNULL(NULLIF(
                                UPPER(LTRIM(RTRIM(item.UnitOfMeasure))),
                                ''), 'EA'),
                            ROUND(item.UnitPrice, 4)
                    ) actual
                    LEFT JOIN (
                        SELECT
                            contract.TenantId,
                            contract.Id AS ContractId,
                            CONCAT(
                                'description:',
                                LOWER(LTRIM(RTRIM(
                                    ISNULL(tenderItem.Description, '')))))
                                AS LineIdentity,
                            ISNULL(NULLIF(
                                UPPER(LTRIM(RTRIM(
                                    tenderItem.UnitOfMeasure))),
                                ''), 'EA') AS UnitOfMeasure,
                            ROUND(COALESCE(
                                negotiationItem.NegotiatedUnitPrice,
                                bidItem.UnitPrice), 4) AS UnitPrice,
                            ROUND(SUM(CASE
                                WHEN negotiationItem.Quantity > 0
                                    THEN negotiationItem.Quantity
                                ELSE bidItem.OfferedQuantity
                            END), 4) AS Quantity,
                            ROUND(SUM(COALESCE(
                                negotiationItem.NegotiatedTotalPrice,
                                ROUND(
                                    (CASE
                                        WHEN negotiationItem.Quantity > 0
                                            THEN negotiationItem.Quantity
                                        ELSE bidItem.OfferedQuantity
                                     END) *
                                    COALESCE(
                                        negotiationItem.NegotiatedUnitPrice,
                                        bidItem.UnitPrice),
                                    2))), 2) AS LineTotal
                        FROM Contracts contract WITH (UPDLOCK, HOLDLOCK)
                        JOIN TenderAwards award
                          ON award.Id = contract.TenderAwardId
                         AND award.TenantId = contract.TenantId
                         AND award.IsDeleted = 0
                        JOIN TenderBidItems bidItem
                          ON bidItem.TenderBidId = award.TenderBidId
                         AND bidItem.TenantId = award.TenantId
                         AND bidItem.IsDeleted = 0
                         AND (
                             award.BidLotId IS NULL OR
                             bidItem.BidLotId = award.BidLotId)
                        JOIN TenderItems tenderItem
                          ON tenderItem.Id = bidItem.TenderItemId
                         AND tenderItem.TenantId = bidItem.TenantId
                         AND tenderItem.IsDeleted = 0
                        LEFT JOIN TenderNegotiationItems negotiationItem
                          ON negotiationItem.NegotiationId =
                             award.NegotiationId
                         AND negotiationItem.TenderBidItemId = bidItem.Id
                         AND negotiationItem.TenantId = bidItem.TenantId
                         AND negotiationItem.IsDeleted = 0
                        WHERE contract.IsDeleted = 0
                          AND contract.Id IN (
                              SELECT changedOrder.ProcurementSourceId
                              FROM PurchaseOrders changedOrder
                              JOIN (
                                  SELECT PurchaseOrderId FROM inserted
                                  UNION
                                  SELECT PurchaseOrderId FROM deleted
                              ) affected
                                ON affected.PurchaseOrderId =
                                   changedOrder.Id
                              WHERE changedOrder.ProcurementSourceType = 2)
                        GROUP BY
                            contract.TenantId,
                            contract.Id,
                            CONCAT(
                                'description:',
                                LOWER(LTRIM(RTRIM(
                                    ISNULL(tenderItem.Description, ''))))),
                            ISNULL(NULLIF(
                                UPPER(LTRIM(RTRIM(
                                    tenderItem.UnitOfMeasure))),
                                ''), 'EA'),
                            ROUND(COALESCE(
                                negotiationItem.NegotiatedUnitPrice,
                                bidItem.UnitPrice), 4)
                    ) approved
                      ON approved.TenantId = actual.TenantId
                     AND approved.ContractId = actual.ContractId
                     AND approved.LineIdentity = actual.LineIdentity
                     AND approved.UnitOfMeasure = actual.UnitOfMeasure
                     AND approved.UnitPrice = actual.UnitPrice
                    WHERE approved.ContractId IS NULL
                       OR actual.Quantity > approved.Quantity
                       OR actual.LineTotal > approved.LineTotal)
                    THROW 51228, 'The cumulative contract purchase-order lines exceed the awarded quantities or values.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // These controls intentionally remain installed on downgrade. Removing
        // them would reopen direct-SQL commercial and capacity bypasses.
    }
}
