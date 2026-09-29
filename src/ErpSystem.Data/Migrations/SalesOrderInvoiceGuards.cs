using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

internal static class SalesOrderInvoiceGuards
{
    internal static void Install(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(OrderGuard);
        migrationBuilder.Sql(SourceLineGuard);
        migrationBuilder.Sql(InvoiceGuard);
        migrationBuilder.Sql(InvoiceLineGuard);
    }
    internal static void Remove(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF EXISTS(SELECT 1 FROM dbo.SalesOrders WHERE InvoiceGenerationKey IS NOT NULL) THROW 51995, 'Retained Sales invoice lineage prevents downgrade.', 1;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.TR_SalesOrders_InvoiceSource; DROP TRIGGER IF EXISTS dbo.TR_SalesOrderLines_InvoiceSource; DROP TRIGGER IF EXISTS dbo.TR_Invoices_SalesSource; DROP TRIGGER IF EXISTS dbo.TR_InvoiceLineItem_SalesSource;");
    }
    private const string OrderGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_SalesOrders_InvoiceSource ON dbo.SalesOrders AFTER INSERT, UPDATE, DELETE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE d.InvoiceId IS NOT NULL AND
                (i.Id IS NULL OR i.OrderStatus NOT IN(3,4,5,6,7) OR EXISTS(SELECT d.TenantId,d.InvoiceId,d.BusinessPartnerId,d.DocumentNumber,d.OrderType,d.Currency,
                    d.ExchangeRate,d.SubTotal,d.DiscountAmount,d.DiscountPercentage,d.ShippingAmount,d.TaxAmount,d.TotalAmount,d.PaymentTermId,d.TaxGroupId,d.WarehouseId,
                    d.InvoiceGenerationKey,d.InvoiceGenerationHash,d.InvoiceGeneratedById,d.InvoiceEconomicsJson,d.InvoiceSourceJson,d.IsDeleted
                    EXCEPT SELECT i.TenantId,i.InvoiceId,i.BusinessPartnerId,i.DocumentNumber,i.OrderType,i.Currency,
                    i.ExchangeRate,i.SubTotal,i.DiscountAmount,i.DiscountPercentage,i.ShippingAmount,i.TaxAmount,i.TotalAmount,i.PaymentTermId,i.TaxGroupId,i.WarehouseId,
                    i.InvoiceGenerationKey,i.InvoiceGenerationHash,i.InvoiceGeneratedById,i.InvoiceEconomicsJson,i.InvoiceSourceJson,i.IsDeleted)))
                THROW 51996, 'A linked Sales order and its retained invoice economics are immutable.', 1;
            IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id LEFT JOIN dbo.Invoices v ON v.Id=i.InvoiceId AND v.TenantId=i.TenantId
                WHERE i.InvoiceId IS NOT NULL AND (d.Id IS NULL OR d.InvoiceId IS NULL) AND
                (v.Id IS NULL OR v.IsDeleted=1 OR i.IsDeleted=1 OR i.OrderType<>1 OR i.OrderStatus NOT IN(3,4,5) OR
                    i.InvoiceGenerationKey IS NULL OR i.InvoiceGenerationHash IS NULL OR i.InvoiceGeneratedById IS NULL OR
                    ISNULL(ISJSON(i.InvoiceEconomicsJson),0)<>1 OR ISNULL(ISJSON(i.InvoiceSourceJson),0)<>1 OR
                    LEN(i.InvoiceGenerationKey)=0 OR LEN(i.InvoiceGenerationHash)<>64 OR i.ShippingAmount<0 OR

                    v.BusinessPartnerId<>i.BusinessPartnerId OR v.CurrencyCode<>i.Currency OR (v.Reference IS NULL OR v.Reference<>i.DocumentNumber) OR
                    v.TotalAmount<>i.TotalAmount OR v.TaxAmount<>i.TaxAmount OR v.DiscountAmount<>i.DiscountAmount OR
                    v.SubTotal<>i.SubTotal+i.ShippingAmount))
                THROW 51996, 'A new Sales invoice link must retain the same tenant, customer and approved source totals.', 1;
            IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id JOIN dbo.SalesOrderLines s ON s.SalesOrderId=i.Id AND s.IsDeleted=0
                LEFT JOIN dbo.InvoiceLineItem v ON v.Id=s.Id AND v.InvoiceId=i.InvoiceId AND v.TenantId=i.TenantId AND v.IsDeleted=0
                WHERE i.InvoiceId IS NOT NULL AND (d.Id IS NULL OR d.InvoiceId IS NULL) AND
                (v.Id IS NULL OR s.TenantId<>i.TenantId OR s.Quantity<=0 OR s.UnitPrice<0 OR v.Quantity<>s.Quantity OR v.UnitPrice<>s.UnitPrice OR
                    v.LineItemType<>CASE WHEN s.InventoryItemId IS NOT NULL THEN 3 WHEN s.ProductId IS NOT NULL THEN 1 ELSE 2 END OR
                    (s.InventoryItemId IS NOT NULL AND (v.LocationId IS NULL OR v.WarehouseId IS NULL)) OR
                    (s.GLAccountId IS NOT NULL AND (v.GLAccountId IS NULL OR v.GLAccountId<>s.GLAccountId)) OR
                    (s.TaxGroupId IS NOT NULL AND (v.TaxGroupId IS NULL OR v.TaxGroupId<>s.TaxGroupId)) OR
                    EXISTS(SELECT s.InventoryItemId,s.ProductId,s.LocationId,s.LotNumber,s.SerialNumber,s.ExpirationDate,s.Description,s.Unit,
                            s.DiscountAmount,s.DiscountPercentage,CASE WHEN s.InventoryItemId IS NOT NULL THEN COALESCE(s.WarehouseId,i.WarehouseId) END
                        EXCEPT SELECT v.InventoryItemId,v.ProductId,v.LocationId,v.LotNumber,v.SerialNumber,v.ExpirationDate,v.Description,v.Unit,
                            v.DiscountAmount,v.DiscountPercentage,v.WarehouseId)))
                THROW 51996, 'Every Sales invoice line must retain its exact source quantity and stock identity.', 1;

            DECLARE @links TABLE(OrderId uniqueidentifier,InvoiceId uniqueidentifier,Economics nvarchar(max),SourceJson nvarchar(max),StockJson nvarchar(max));
            INSERT @links SELECT i.Id,i.InvoiceId,j.Economics,i.InvoiceSourceJson,i.InvoiceEconomicsJson
                FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                OUTER APPLY OPENJSON(i.InvoiceEconomicsJson) WITH(Economics nvarchar(max)) j
                WHERE i.InvoiceId IS NOT NULL AND (d.Id IS NULL OR d.InvoiceId IS NULL);
            IF EXISTS(SELECT 1 FROM @links WHERE ISNULL(ISJSON(Economics),0)<>1)
                THROW 51999, 'Sales invoice snapshots must retain complete source and invoice economics.', 1;
            IF EXISTS(SELECT 1 FROM @links l JOIN dbo.SalesOrders o ON o.Id=l.OrderId WHERE
                (SELECT COUNT(*) FROM dbo.InvoiceLineItem v WHERE v.InvoiceId=l.InvoiceId AND v.IsDeleted=0)<>
                (SELECT COUNT(*) FROM dbo.SalesOrderLines s WHERE s.SalesOrderId=l.OrderId AND s.IsDeleted=0)+CASE WHEN o.ShippingAmount>0 THEN 1 ELSE 0 END
                OR NOT EXISTS(SELECT 1 FROM dbo.SalesOrderLines s WHERE s.SalesOrderId=l.OrderId AND s.IsDeleted=0))
                THROW 51996, 'The Sales invoice must contain exactly its source lines and retained freight.', 1;
            IF EXISTS(SELECT 1 FROM @links l JOIN dbo.SalesOrders o ON o.Id=l.OrderId
                JOIN dbo.InvoiceLineItem v ON v.InvoiceId=l.InvoiceId AND v.IsDeleted=0
                LEFT JOIN dbo.SalesOrderLines s ON s.Id=v.Id AND s.SalesOrderId=l.OrderId AND s.IsDeleted=0
                WHERE s.Id IS NULL AND (o.ShippingAmount<=0 OR v.Id<>CONVERT(uniqueidentifier,SUBSTRING(HASHBYTES('SHA2_256',
                    CONVERT(varchar(max),'SALES:INVOICE:FREIGHT:1:'+LOWER(REPLACE(CONVERT(varchar(36),o.Id),'-','')))),1,16))
                    OR v.Quantity<>1 OR v.UnitPrice<>o.ShippingAmount OR v.LineItemType<>2 OR v.InventoryItemId IS NOT NULL
                    OR v.ProductId IS NOT NULL OR v.DiscountPercentage<>0 OR v.DiscountAmount<>0))
                THROW 51996, 'The Sales freight line must retain its complete source amount and identity.', 1;
            IF EXISTS(SELECT 1 FROM @links l JOIN dbo.Invoices v ON v.Id=l.InvoiceId
                OUTER APPLY OPENJSON(l.Economics) WITH(Id uniqueidentifier,TenantId uniqueidentifier,BusinessPartnerId uniqueidentifier,
                    BusinessPartnerRoleId uniqueidentifier,CurrencyCode nvarchar(3),ExchangeRate decimal(18,4),ExchangeRateId uniqueidentifier,
                    InvoiceDate date,DueDate date,SubTotal decimal(18,2),TaxAmount decimal(18,2),DiscountAmount decimal(18,2),
                    TotalAmount decimal(18,2),Reference nvarchar(100),IsOpeningBalance bit) j
                WHERE j.Id IS NULL OR EXISTS(SELECT v.Id,v.TenantId,v.BusinessPartnerId,v.BusinessPartnerRoleId,v.CurrencyCode,
                    v.ExchangeRate,v.ExchangeRateId,CONVERT(date,v.InvoiceDate),CONVERT(date,v.DueDate),v.SubTotal,v.TaxAmount,
                    v.DiscountAmount,v.TotalAmount,v.Reference,v.IsOpeningBalance
                    EXCEPT SELECT j.Id,j.TenantId,j.BusinessPartnerId,j.BusinessPartnerRoleId,j.CurrencyCode,j.ExchangeRate,j.ExchangeRateId,
                    j.InvoiceDate,j.DueDate,j.SubTotal,j.TaxAmount,j.DiscountAmount,j.TotalAmount,j.Reference,j.IsOpeningBalance))
                THROW 51999, 'The retained Sales invoice header snapshot does not match its document.', 1;
            IF EXISTS(SELECT 1 FROM @links l WHERE
                (SELECT COUNT(*) FROM OPENJSON(l.Economics,'$.Lines'))<>(SELECT COUNT(*) FROM dbo.InvoiceLineItem v WHERE v.InvoiceId=l.InvoiceId AND v.IsDeleted=0)
                OR (SELECT COUNT(*) FROM OPENJSON(l.StockJson,'$.Stock'))<>(SELECT COUNT(*) FROM dbo.InvoiceLineItem v WHERE v.InvoiceId=l.InvoiceId AND v.IsDeleted=0)
                OR (SELECT COUNT(*) FROM OPENJSON(l.SourceJson,'$.Lines'))<>(SELECT COUNT(*) FROM dbo.SalesOrderLines s WHERE s.SalesOrderId=l.OrderId AND s.IsDeleted=0))
                THROW 51999, 'The retained Sales snapshots must contain every line exactly once.', 1;
            IF EXISTS(SELECT 1 FROM @links l JOIN dbo.InvoiceLineItem v ON v.InvoiceId=l.InvoiceId AND v.IsDeleted=0
                WHERE NOT EXISTS(SELECT 1 FROM OPENJSON(l.Economics,'$.Lines') WITH(Id uniqueidentifier,LineItemType int,GLAccountId uniqueidentifier,
                    ProductId uniqueidentifier,Description nvarchar(200),Quantity decimal(18,4),UnitPrice decimal(18,2),TaxAmount decimal(18,2),
                    TaxRate decimal(18,4),TaxTreatment int,TaxGroupId uniqueidentifier,DiscountAmount decimal(18,2),DiscountPercentage decimal(18,4)) j
                    WHERE j.Id=v.Id AND NOT EXISTS(SELECT v.LineItemType,v.GLAccountId,v.ProductId,v.Description,v.Quantity,v.UnitPrice,
                        v.TaxAmount,v.TaxRate,v.TaxTreatment,v.TaxGroupId,v.DiscountAmount,v.DiscountPercentage
                        EXCEPT SELECT j.LineItemType,j.GLAccountId,j.ProductId,j.Description,j.Quantity,j.UnitPrice,j.TaxAmount,j.TaxRate,
                        j.TaxTreatment,j.TaxGroupId,j.DiscountAmount,j.DiscountPercentage))
                OR NOT EXISTS(SELECT 1 FROM OPENJSON(l.StockJson,'$.Stock') WITH(Id uniqueidentifier,InventoryItemId uniqueidentifier,
                    WarehouseId uniqueidentifier,LocationId uniqueidentifier,LotNumber nvarchar(100),SerialNumber nvarchar(100),
                    ExpirationDate datetime2,Unit nvarchar(50),TaxCode nvarchar(50)) j WHERE j.Id=v.Id AND NOT EXISTS(
                    SELECT v.InventoryItemId,v.WarehouseId,v.LocationId,v.LotNumber,v.SerialNumber,v.ExpirationDate,v.Unit,v.TaxCode
                    EXCEPT SELECT j.InventoryItemId,j.WarehouseId,j.LocationId,j.LotNumber,j.SerialNumber,j.ExpirationDate,j.Unit,j.TaxCode)))
                THROW 51999, 'The retained Sales invoice line snapshot does not match its document.', 1;
            IF EXISTS(SELECT 1 FROM @links l JOIN dbo.SalesOrders o ON o.Id=l.OrderId
                OUTER APPLY OPENJSON(l.SourceJson) WITH(Id uniqueidentifier,TenantId uniqueidentifier,BusinessPartnerId uniqueidentifier,
                    DocumentNumber nvarchar(50),OrderType int,Currency nvarchar(3),ExchangeRate decimal(18,4),DiscountPercentage decimal(18,4),
                    SubTotal decimal(18,2),Discount decimal(18,2),Shipping decimal(18,2),Tax decimal(18,2),Total decimal(18,2),
                    PaymentTermId uniqueidentifier,TaxGroupId uniqueidentifier,WarehouseId uniqueidentifier) j WHERE j.Id IS NULL OR EXISTS(
                    SELECT o.Id,o.TenantId,o.BusinessPartnerId,o.DocumentNumber,o.OrderType,o.Currency,o.ExchangeRate,o.DiscountPercentage,
                        o.SubTotal,o.DiscountAmount,o.ShippingAmount,o.TaxAmount,o.TotalAmount,o.PaymentTermId,o.TaxGroupId,o.WarehouseId
                    EXCEPT SELECT j.Id,j.TenantId,j.BusinessPartnerId,j.DocumentNumber,j.OrderType,j.Currency,j.ExchangeRate,j.DiscountPercentage,
                        j.SubTotal,j.Discount,j.Shipping,j.Tax,j.Total,j.PaymentTermId,j.TaxGroupId,j.WarehouseId))
                THROW 51999, 'The retained Sales source header does not match its order.', 1;
            IF EXISTS(SELECT 1 FROM @links l JOIN dbo.SalesOrderLines s ON s.SalesOrderId=l.OrderId AND s.IsDeleted=0
                WHERE NOT EXISTS(SELECT 1 FROM OPENJSON(l.SourceJson,'$.Lines') WITH(Id uniqueidentifier,TenantId uniqueidentifier,
                    InventoryItemId uniqueidentifier,ProductId uniqueidentifier,Description nvarchar(200),Quantity decimal(18,4),Price decimal(18,2),
                    Discount decimal(18,2),DiscountPercentage decimal(18,4),Tax decimal(18,2),GLAccountId uniqueidentifier,TaxGroupId uniqueidentifier,
                    TaxCode nvarchar(50),Unit nvarchar(50),WarehouseId uniqueidentifier,LocationId uniqueidentifier,LotNumber nvarchar(100),
                    SerialNumber nvarchar(100),ExpirationDate datetime2) j WHERE j.Id=s.Id AND NOT EXISTS(
                    SELECT s.TenantId,s.InventoryItemId,s.ProductId,s.Description,s.Quantity,s.UnitPrice,s.DiscountAmount,s.DiscountPercentage,s.TaxAmount,
                        s.GLAccountId,s.TaxGroupId,s.TaxCode,s.Unit,s.WarehouseId,s.LocationId,s.LotNumber,s.SerialNumber,s.ExpirationDate
                    EXCEPT SELECT j.TenantId,j.InventoryItemId,j.ProductId,j.Description,j.Quantity,j.Price,j.Discount,j.DiscountPercentage,j.Tax,
                        j.GLAccountId,j.TaxGroupId,j.TaxCode,j.Unit,j.WarehouseId,j.LocationId,j.LotNumber,j.SerialNumber,j.ExpirationDate)))
                THROW 51999, 'The retained Sales source line does not match its order.', 1;
        END
        """;
    private const string SourceLineGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_SalesOrderLines_InvoiceSource ON dbo.SalesOrderLines AFTER INSERT, UPDATE, DELETE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS(SELECT 1 FROM (SELECT Id,SalesOrderId FROM inserted UNION SELECT Id,SalesOrderId FROM deleted) a
                JOIN dbo.SalesOrders o ON o.Id=a.SalesOrderId AND o.InvoiceId IS NOT NULL
                LEFT JOIN inserted i ON i.Id=a.Id LEFT JOIN deleted d ON d.Id=a.Id WHERE i.Id IS NULL OR d.Id IS NULL OR
                EXISTS(SELECT d.SalesOrderId,d.TenantId,d.InventoryItemId,d.ProductId,d.Description,d.Quantity,d.UnitPrice,d.DiscountAmount,
                    d.DiscountPercentage,d.TaxAmount,d.TaxRate,d.TaxGroupId,d.TaxCode,d.GLAccountId,d.WarehouseId,d.LocationId,d.LotNumber,d.SerialNumber,d.ExpirationDate,d.Unit,d.IsDeleted
                    EXCEPT SELECT i.SalesOrderId,i.TenantId,i.InventoryItemId,i.ProductId,i.Description,i.Quantity,i.UnitPrice,i.DiscountAmount,
                    i.DiscountPercentage,i.TaxAmount,i.TaxRate,i.TaxGroupId,i.TaxCode,i.GLAccountId,i.WarehouseId,i.LocationId,i.LotNumber,i.SerialNumber,i.ExpirationDate,i.Unit,i.IsDeleted))
                THROW 51997, 'Sales invoice source lines cannot be changed, inserted or removed after linking.', 1;
        END
        """;
    private const string InvoiceGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_Invoices_SalesSource ON dbo.Invoices AFTER UPDATE, DELETE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS(SELECT 1 FROM deleted d JOIN dbo.SalesOrders o ON o.InvoiceId=d.Id
                LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL OR i.Status=6 OR
                EXISTS(SELECT d.TenantId,d.BusinessPartnerId,d.BusinessPartnerRoleId,d.CurrencyCode,d.ExchangeRate,d.ExchangeRateId,
                    d.InvoiceDate,d.DueDate,d.SubTotal,d.TaxAmount,d.DiscountAmount,d.TotalAmount,d.Reference,d.IsOpeningBalance,d.IsDeleted
                    EXCEPT SELECT i.TenantId,i.BusinessPartnerId,i.BusinessPartnerRoleId,i.CurrencyCode,i.ExchangeRate,i.ExchangeRateId,
                    i.InvoiceDate,i.DueDate,i.SubTotal,i.TaxAmount,i.DiscountAmount,i.TotalAmount,i.Reference,i.IsOpeningBalance,i.IsDeleted))
                THROW 51998, 'A generated Sales invoice must retain its approved source economics.', 1;
        END
        """;
    private const string InvoiceLineGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_InvoiceLineItem_SalesSource ON dbo.InvoiceLineItem AFTER INSERT, UPDATE, DELETE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS(SELECT 1 FROM (SELECT Id,InvoiceId FROM inserted UNION SELECT Id,InvoiceId FROM deleted) a
                JOIN dbo.SalesOrders o ON o.InvoiceId=a.InvoiceId
                LEFT JOIN inserted i ON i.Id=a.Id LEFT JOIN deleted d ON d.Id=a.Id WHERE i.Id IS NULL OR d.Id IS NULL OR
                EXISTS(SELECT d.InvoiceId,d.TenantId,d.LineItemType,d.GLAccountId,d.ProductId,d.InventoryItemId,d.Description,d.Quantity,d.UnitPrice,
                    d.DiscountAmount,d.DiscountPercentage,d.TaxAmount,d.TaxRate,d.TaxTreatment,d.TaxGroupId,d.TaxCode,d.WarehouseId,d.LocationId,d.LotNumber,d.SerialNumber,d.ExpirationDate,d.Unit,d.IsDeleted
                    EXCEPT SELECT i.InvoiceId,i.TenantId,i.LineItemType,i.GLAccountId,i.ProductId,i.InventoryItemId,i.Description,i.Quantity,i.UnitPrice,
                    i.DiscountAmount,i.DiscountPercentage,i.TaxAmount,i.TaxRate,i.TaxTreatment,i.TaxGroupId,i.TaxCode,i.WarehouseId,i.LocationId,i.LotNumber,i.SerialNumber,i.ExpirationDate,i.Unit,i.IsDeleted))
                THROW 51998, 'Generated Sales invoice lines are immutable; canonical valuation may capture posting costs only.', 1;
        END
        """;
}
