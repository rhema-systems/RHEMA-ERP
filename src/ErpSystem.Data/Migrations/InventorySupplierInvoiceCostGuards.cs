using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

internal static class InventorySupplierInvoiceCostGuards
{
    public static void Install(MigrationBuilder migration)
    {
        migration.Sql(ReceiptBasis);
        migration.Sql(CostAllocation);
        migration.Sql(CostLines);
        migration.Sql(CostValuation);
        migration.Sql(InvoiceSeal);
        migration.Sql(ReceiptAllocation);
        migration.Sql(ReceiptLine);
    }

    public static void Remove(MigrationBuilder migration)
    {
        migration.Sql("""
            IF EXISTS(SELECT 1 FROM dbo.ProcurementReceiptCostBases)
              OR EXISTS(SELECT 1 FROM dbo.VendorInvoiceReceiptCostAllocations)
              OR EXISTS(SELECT 1 FROM dbo.VendorInvoiceReceiptCostPostingLines)
              OR EXISTS(SELECT 1 FROM dbo.VendorInvoiceReceiptCostValuations)
              THROW 51950, 'Cannot remove immutable original receipt or invoice cost evidence. Retain this migration and use a forward corrective release.', 1;
            """);
        migration.Sql(ProcurementAutoInvoiceReceipts.AllocationTrigger);
        migration.Sql(ProcurementAutoInvoiceReceipts.LineTrigger);
        migration.Sql("""
            DROP TRIGGER IF EXISTS dbo.TR_ProcurementReceiptCostBases_Guard;
            DROP TRIGGER IF EXISTS dbo.TR_VendorInvoiceReceiptCostAllocations_Guard;
            DROP TRIGGER IF EXISTS dbo.TR_VendorInvoiceReceiptCostPostingLines_Guard;
            DROP TRIGGER IF EXISTS dbo.TR_VendorInvoiceReceiptCostValuations_Guard;
            DROP TRIGGER IF EXISTS dbo.TR_VendorInvoice_ReceiptCostSeal;
            """);
    }

    internal const string ReceiptBasis = """
        CREATE OR ALTER TRIGGER dbo.TR_ProcurementReceiptCostBases_Guard ON dbo.ProcurementReceiptCostBases
        AFTER INSERT, UPDATE, DELETE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS(SELECT 1 FROM deleted) THROW 51930, 'Original accepted receipt cost evidence is immutable.', 1;
          IF EXISTS(SELECT 1 FROM inserted b
            LEFT JOIN dbo.PurchaseOrderReceipts r ON r.Id=b.PurchaseOrderReceiptId AND r.TenantId=b.TenantId AND r.IsDeleted=0
            LEFT JOIN dbo.PurchaseOrderReceiptItems l ON l.Id=b.PurchaseOrderReceiptItemId AND l.TenantId=b.TenantId AND l.ReceiptId=r.Id AND l.IsDeleted=0
            LEFT JOIN dbo.PurchaseOrderItems p ON p.Id=b.PurchaseOrderItemId AND p.Id=l.PurchaseOrderItemId AND p.TenantId=b.TenantId AND p.PurchaseOrderId=r.PurchaseOrderId AND p.IsDeleted=0
            LEFT JOIN dbo.PurchaseOrders po ON po.Id=p.PurchaseOrderId AND po.TenantId=b.TenantId AND po.IsDeleted=0
            LEFT JOIN dbo.InventoryMovements m ON m.Id=b.InventoryMovementId AND m.TenantId=b.TenantId AND m.IsDeleted=0
            LEFT JOIN dbo.WarehouseLocations loc ON loc.Id=b.LocationId AND loc.TenantId=b.TenantId
              AND CASE WHEN loc.IsConsignmentBin=1 AND loc.ConsignmentWarehouseId IS NOT NULL
                AND loc.ConsignmentWarehouseId<>'00000000-0000-0000-0000-000000000000' THEN loc.ConsignmentWarehouseId ELSE loc.WarehouseId END=b.WarehouseId AND loc.IsDeleted=0
            LEFT JOIN dbo.ExchangeRates fx ON fx.Id=b.ExchangeRateId AND fx.TenantId=b.TenantId AND fx.IsDeleted=0
            WHERE r.Id IS NULL OR l.Id IS NULL OR p.Id IS NULL OR po.Id IS NULL OR m.Id IS NULL OR loc.Id IS NULL OR b.IsDeleted=1 OR b.Version<>1
             OR b.InventoryItemId<>p.InventoryItemId OR b.PurchaseCurrency<>po.Currency OR b.PurchaseQuantity<>l.AcceptedQuantity OR b.PurchaseUnitCost<>p.UnitPrice
             OR b.BaseQuantity<>b.PurchaseQuantity*b.ConversionToBase OR b.PurchaseAmount<>ROUND(b.PurchaseQuantity*b.PurchaseUnitCost,2)
             OR m.InventoryItemId<>b.InventoryItemId OR m.WarehouseId<>b.WarehouseId OR ISNULL(m.LocationId,'00000000-0000-0000-0000-000000000000')<>b.LocationId
             OR m.MovementType<>1 OR m.Direction<>1 OR m.IsPosted<>1 OR ISNULL(m.ReferenceId,'00000000-0000-0000-0000-000000000000')<>r.Id OR m.Quantity<>b.BaseQuantity
             OR b.FunctionalInventoryAmount<>ROUND(m.TotalValue,2) OR b.FunctionalAccrualAmount<>ROUND(m.TotalValue+ISNULL(m.VarianceAmount,0),2)
             OR (b.PurchaseCurrency=b.FunctionalCurrency AND (b.ExchangeRateId IS NOT NULL OR b.ExchangeRateToFunctional<>1))
             OR (b.PurchaseCurrency<>b.FunctionalCurrency AND (fx.Id IS NULL OR fx.BaseCurrencyCode<>b.FunctionalCurrency OR fx.TargetCurrencyCode<>b.PurchaseCurrency
                 OR fx.InverseRate<>b.ExchangeRateToFunctional OR fx.HasBeenUsedInTransactions<>1 OR fx.IsActive<>1 OR fx.ApprovalStatus NOT IN(2,4)
                 OR fx.RateType<>1 OR CAST(fx.EffectiveDate AS date)>CAST(r.ReceiptDate AS date) OR (fx.EndDate IS NOT NULL AND CAST(fx.EndDate AS date)<CAST(r.ReceiptDate AS date)))))
            THROW 51931, 'Receipt cost basis must match the exact accepted quantity, valuation and retained currency evidence.', 1;
        END
        """;

    internal const string CostAllocation = """
        CREATE OR ALTER TRIGGER dbo.TR_VendorInvoiceReceiptCostAllocations_Guard ON dbo.VendorInvoiceReceiptCostAllocations
        AFTER INSERT, UPDATE, DELETE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL)
            THROW 51932, 'Invoice receipt cost evidence cannot be deleted.', 1;
          DECLARE @receiptLock uniqueidentifier;
          SELECT @receiptLock=g.Id FROM dbo.GoodsReceiptNoteItems g WITH(UPDLOCK,HOLDLOCK)
            JOIN inserted i ON i.GoodsReceiptNoteItemId=g.Id AND i.TenantId=g.TenantId ORDER BY g.Id;
          IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.GoodsReceiptNoteItems g ON g.Id=i.GoodsReceiptNoteItemId AND g.TenantId=i.TenantId
            WHERE ISNULL((SELECT SUM(c.BaseQuantity) FROM dbo.VendorInvoiceReceiptCostAllocations c
              WHERE c.TenantId=i.TenantId AND c.GoodsReceiptNoteItemId=g.Id AND c.IsDeleted=0 AND c.ReversalJournalEntryId IS NULL),0)
              + ISNULL((SELECT SUM(a.BaseQuantity) FROM dbo.InventorySupplierReturnAllocations a
                JOIN dbo.PurchaseReturns r ON r.Id=a.InventoryPurchaseReturnId AND r.TenantId=a.TenantId
                WHERE a.TenantId=i.TenantId AND a.GoodsReceiptNoteItemId=g.Id AND a.IsDeleted=0 AND a.OriginalVendorInvoiceId IS NULL
                  AND r.IsDeleted=0 AND r.AccountingAllocationVersion=1 AND r.Status IN('Shipped','Acknowledged')),0)>g.AcceptedQuantity)
            THROW 51944, 'Invoice and uninvoiced return claims exceed the original accepted receipt quantity.', 1;
          IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.ProcurementReceiptCostBases b ON b.Id=i.ProcurementReceiptCostBasisId AND b.TenantId=i.TenantId
            WHERE ISNULL((SELECT SUM(c.ReceiptForeignAmount) FROM dbo.VendorInvoiceReceiptCostAllocations c
              WHERE c.TenantId=i.TenantId AND c.ProcurementReceiptCostBasisId=b.Id AND c.IsDeleted=0 AND c.ReversalJournalEntryId IS NULL),0)
              + ISNULL((SELECT SUM(a.OriginalAccrualForeignAmount) FROM dbo.InventorySupplierReturnAllocations a
                JOIN dbo.PurchaseReturns r ON r.Id=a.InventoryPurchaseReturnId AND r.TenantId=a.TenantId
                WHERE a.TenantId=i.TenantId AND a.ProcurementReceiptCostBasisId=b.Id AND a.IsDeleted=0 AND a.OriginalVendorInvoiceId IS NULL
                  AND r.IsDeleted=0 AND r.AccountingAllocationVersion=1 AND r.Status IN('Shipped','Acknowledged')),0)>b.PurchaseAmount)
            THROW 51945, 'Invoice and uninvoiced return claims exceed original receipt purchase value.', 1;
          IF EXISTS(SELECT 1 FROM inserted i WHERE i.ReversalJournalEntryId IS NOT NULL AND EXISTS(
            SELECT 1 FROM dbo.InventorySupplierReturnAllocations a WHERE a.TenantId=i.TenantId
              AND a.OriginalVendorInvoiceId=i.VendorInvoiceId AND a.IsDeleted=0))
            THROW 51946, 'An invoice retained by supplier return accounting cannot release its receipt claims.', 1;
          IF EXISTS(SELECT TenantId,VendorInvoiceId,VendorInvoiceLineItemId,VendorInvoiceReceiptAllocationId,ProcurementReceiptCostBasisId,
            [GoodsReceiptNoteItemId],InventoryItemId,ReceiptJournalEntryId,AccountingBookId,PurchaseQuantity,BaseQuantity,ReceiptForeignAmount,
            ReceiptFunctionalAmount,InvoiceNetForeignAmount,InvoiceFunctionalAmount,PriceDifferenceFunctionalAmount,ExchangeDifferenceFunctionalAmount,
            InventoryAdjustmentAmount,RevaluedReceiptBaseQuantity,PurchasePriceVarianceAmount,Policy,PurchaseCurrency,FunctionalCurrency,InvoiceExchangeRateToFunctional,
            PurchasePriceVarianceAccountId,PostingEventId,JournalEntryId,SourceFingerprint,IsDeleted FROM deleted
            EXCEPT SELECT TenantId,VendorInvoiceId,VendorInvoiceLineItemId,VendorInvoiceReceiptAllocationId,ProcurementReceiptCostBasisId,
            [GoodsReceiptNoteItemId],InventoryItemId,ReceiptJournalEntryId,AccountingBookId,PurchaseQuantity,BaseQuantity,ReceiptForeignAmount,
            ReceiptFunctionalAmount,InvoiceNetForeignAmount,InvoiceFunctionalAmount,PriceDifferenceFunctionalAmount,ExchangeDifferenceFunctionalAmount,
            InventoryAdjustmentAmount,RevaluedReceiptBaseQuantity,PurchasePriceVarianceAmount,Policy,PurchaseCurrency,FunctionalCurrency,InvoiceExchangeRateToFunctional,
            PurchasePriceVarianceAccountId,PostingEventId,JournalEntryId,SourceFingerprint,IsDeleted FROM inserted)
            THROW 51933, 'Invoice original receipt cost and posting evidence is immutable.', 1;
          IF EXISTS(SELECT 1 FROM inserted c
            LEFT JOIN dbo.VendorInvoiceReceiptAllocations a ON a.Id=c.VendorInvoiceReceiptAllocationId AND a.TenantId=c.TenantId AND a.IsDeleted=0
            LEFT JOIN dbo.VendorInvoice i ON i.Id=c.VendorInvoiceId AND i.TenantId=c.TenantId AND i.IsDeleted=0
            LEFT JOIN dbo.VendorInvoiceLineItem l ON l.Id=c.VendorInvoiceLineItemId AND l.VendorInvoiceId=i.Id AND l.TenantId=c.TenantId AND l.IsDeleted=0
            LEFT JOIN dbo.GoodsReceiptNoteItems g ON g.Id=c.GoodsReceiptNoteItemId AND g.TenantId=c.TenantId AND g.IsDeleted=0
            LEFT JOIN dbo.JournalEntries j ON j.Id=c.ReceiptJournalEntryId AND j.TenantId=c.TenantId AND j.AccountingBookId=c.AccountingBookId AND j.IsDeleted=0
            LEFT JOIN dbo.ProcurementReceiptCostBases b ON b.Id=c.ProcurementReceiptCostBasisId AND b.TenantId=c.TenantId AND b.IsDeleted=0
            LEFT JOIN dbo.FinancePostingEvents e ON e.Id=c.PostingEventId AND e.TenantId=c.TenantId AND e.SourceDocumentId=i.Id AND e.SourceDocumentType='VendorInvoice'
              AND e.PostingAction='Post' AND e.PostingStatus='Posted' AND e.JournalEntryId=c.JournalEntryId AND e.AccountingBookId=c.AccountingBookId AND e.IsDeleted=0
            WHERE a.Id IS NULL OR i.Id IS NULL OR l.Id IS NULL OR g.Id IS NULL OR j.Id IS NULL OR e.Id IS NULL OR c.IsDeleted=1
              OR a.VendorInvoiceId<>i.Id OR a.VendorInvoiceLineItemId<>l.Id OR a.GoodsReceiptNoteItemId<>g.Id OR g.InventoryItemId<>c.InventoryItemId
              OR (i.JournalEntryId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM deleted d WHERE d.Id=c.Id))
              OR (SELECT COUNT(*) FROM dbo.FinancePostingEvents r WHERE r.TenantId=c.TenantId AND r.IsDeleted=0
                AND r.SourceDocumentType='ProcurementPurchaseOrderReceipt' AND r.SourceDocumentId=a.PurchaseOrderReceiptId
                AND r.PostingAction='PostAcceptedInventoryReceipt' AND r.PostingStatus='Posted' AND r.JournalEntryId=j.Id AND r.AccountingBookId=c.AccountingBookId)<>1
              OR (c.ProcurementReceiptCostBasisId IS NOT NULL AND (b.Id IS NULL OR b.PurchaseOrderReceiptItemId<>a.PurchaseOrderReceiptItemId
                OR b.PurchaseOrderReceiptId<>a.PurchaseOrderReceiptId OR b.InventoryItemId<>c.InventoryItemId OR b.PurchaseOrderItemId<>a.PurchaseOrderItemId
                OR b.FunctionalCurrency<>c.FunctionalCurrency OR b.PurchaseCurrency<>c.PurchaseCurrency OR c.BaseQuantity<>c.PurchaseQuantity*b.ConversionToBase))
              OR a.Quantity<>c.PurchaseQuantity OR ISNULL(LEN(c.SourceFingerprint),0)<>64 OR c.InvoiceExchangeRateToFunctional<=0
              OR c.PurchaseCurrency<>i.CurrencyCode OR c.FunctionalCurrency<>e.FunctionalCurrencyCode OR c.ReceiptForeignAmount<0 OR c.ReceiptFunctionalAmount<0
              OR c.Policy NOT IN('NoDifference','RevalueInventory','PurchasePriceVariance')
              OR (c.Policy='NoDifference' AND c.PriceDifferenceFunctionalAmount<>0) OR (c.Policy='PurchasePriceVariance' AND c.InventoryAdjustmentAmount<>0))
            THROW 51934, 'Invoice receipt cost evidence must retain same-tenant source, book, quantity, currency and Finance posting.', 1;
          IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
            LEFT JOIN dbo.FinancePostingEvents e ON e.Id=i.ReversalPostingEventId AND e.TenantId=i.TenantId AND e.SourceDocumentId=i.VendorInvoiceId
              AND e.SourceDocumentType='VendorInvoice' AND e.PostingAction='Reverse' AND e.PostingStatus='Posted' AND e.JournalEntryId=i.ReversalJournalEntryId AND e.IsDeleted=0
            LEFT JOIN dbo.JournalEntries j ON j.Id=i.JournalEntryId AND j.TenantId=i.TenantId AND j.IsReversed=1 AND j.ReversalJournalEntryId=i.ReversalJournalEntryId
            WHERE ((i.ReversalPostingEventId IS NULL AND i.ReversalJournalEntryId IS NOT NULL) OR (i.ReversalPostingEventId IS NOT NULL AND (e.Id IS NULL OR j.Id IS NULL)))
              OR (d.ReversalPostingEventId IS NOT NULL AND (ISNULL(i.ReversalPostingEventId,'00000000-0000-0000-0000-000000000000')<>d.ReversalPostingEventId OR
                  ISNULL(i.ReversalJournalEntryId,'00000000-0000-0000-0000-000000000000')<>d.ReversalJournalEntryId)))
            THROW 51935, 'Receipt cost claim release requires the exact original invoice journal reversal.', 1;
          IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
            LEFT JOIN dbo.FinancePostingEvents e ON e.Id=i.ReversalReclassificationPostingEventId AND e.TenantId=i.TenantId AND e.SourceDocumentId=i.VendorInvoiceId
              AND e.SourceDocumentType='VendorInvoiceReceiptCostReclassification' AND e.PostingAction='Post' AND e.PostingStatus='Posted'
              AND e.JournalEntryId=i.ReversalReclassificationJournalEntryId AND e.AccountingBookId=i.AccountingBookId AND e.IsDeleted=0
            WHERE (i.ReversalReclassificationPostingEventId IS NULL AND i.ReversalReclassificationJournalEntryId IS NOT NULL)
              OR (i.ReversalReclassificationPostingEventId IS NOT NULL AND (e.Id IS NULL OR i.ReversalPostingEventId IS NULL))
              OR (d.ReversalReclassificationPostingEventId IS NOT NULL AND (ISNULL(i.ReversalReclassificationPostingEventId,'00000000-0000-0000-0000-000000000000')<>d.ReversalReclassificationPostingEventId
                OR ISNULL(i.ReversalReclassificationJournalEntryId,'00000000-0000-0000-0000-000000000000')<>d.ReversalReclassificationJournalEntryId)))
            THROW 51941, 'Consumed receipt cost reclassification must retain its exact same-tenant Finance event.', 1;
        END
        """;

    internal const string CostLines = """
        CREATE OR ALTER TRIGGER dbo.TR_VendorInvoiceReceiptCostPostingLines_Guard ON dbo.VendorInvoiceReceiptCostPostingLines
        AFTER INSERT, UPDATE, DELETE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS(SELECT 1 FROM deleted) THROW 51936, 'Invoice original receipt account distributions are immutable.', 1;
          DECLARE @accountLock uniqueidentifier;
          SELECT @accountLock=t.Id FROM dbo.AccountTransactions t WITH(UPDLOCK,HOLDLOCK)
            JOIN inserted i ON i.OriginalReceiptAccountTransactionId=t.Id AND i.TenantId=t.TenantId ORDER BY t.Id;
          IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.AccountTransactions t ON t.Id=i.OriginalReceiptAccountTransactionId AND t.TenantId=i.TenantId
            WHERE ISNULL((SELECT SUM(l.FunctionalAmount) FROM dbo.VendorInvoiceReceiptCostPostingLines l
              JOIN dbo.VendorInvoiceReceiptCostAllocations c ON c.Id=l.CostAllocationId AND c.TenantId=l.TenantId
              WHERE l.TenantId=i.TenantId AND l.OriginalReceiptAccountTransactionId=t.Id AND l.IsDeleted=0
                AND c.IsDeleted=0 AND c.ReversalJournalEntryId IS NULL),0)
              + ISNULL((SELECT SUM(s.Amount) FROM dbo.InventorySupplierReturnAccrualShares s
                JOIN dbo.InventorySupplierReturnAllocations a ON a.Id=s.InventorySupplierReturnAllocationId AND a.TenantId=s.TenantId
                JOIN dbo.PurchaseReturns r ON r.Id=a.InventoryPurchaseReturnId AND r.TenantId=a.TenantId
                WHERE s.TenantId=i.TenantId AND s.OriginalReceiptAccountTransactionId=t.Id AND s.IsDeleted=0
                  AND a.IsDeleted=0 AND a.OriginalVendorInvoiceId IS NULL AND r.IsDeleted=0 AND r.AccountingAllocationVersion=1
                  AND r.Status IN('Shipped','Acknowledged')),0)>t.CreditAmount-t.DebitAmount)
            THROW 51947, 'Invoice and uninvoiced return account shares exceed original GRNI value.', 1;
          IF EXISTS(SELECT 1 FROM inserted l
            LEFT JOIN dbo.VendorInvoiceReceiptCostAllocations c ON c.Id=l.CostAllocationId AND c.TenantId=l.TenantId AND c.IsDeleted=0
            LEFT JOIN dbo.Accounts a ON a.Id=l.AccountId AND a.TenantId=l.TenantId AND a.IsDeleted=0
            LEFT JOIN dbo.AccountTransactions t ON t.Id=l.OriginalReceiptAccountTransactionId AND t.TenantId=l.TenantId AND t.JournalEntryId=c.ReceiptJournalEntryId AND t.AccountId=l.AccountId
            LEFT JOIN dbo.VendorInvoice i ON i.Id=c.VendorInvoiceId AND i.TenantId=c.TenantId
            WHERE c.Id IS NULL OR a.Id IS NULL OR i.Id IS NULL OR i.JournalEntryId IS NOT NULL OR l.IsDeleted=1 OR l.FunctionalAmount=0
              OR l.Purpose NOT IN('Accrual','Inventory','Ppv','Fx')
              OR (l.Purpose='Accrual' AND (t.Id IS NULL OR t.TransactionTag<>'INV-RECEIPT-GRV-ACCRUAL' OR l.FunctionalAmount<0
                OR ISNULL(t.SourceDocumentLineId,'00000000-0000-0000-0000-000000000000')<>c.InventoryItemId))
              OR (l.Purpose<>'Accrual' AND l.OriginalReceiptAccountTransactionId IS NOT NULL)
              OR (l.Purpose='Ppv' AND ISNULL(c.PurchasePriceVarianceAccountId,'00000000-0000-0000-0000-000000000000')<>l.AccountId))
            THROW 51937, 'Invoice receipt distribution must retain its exact original account purpose and sign.', 1;
        END
        """;

    internal const string CostValuation = """
        CREATE OR ALTER TRIGGER dbo.TR_VendorInvoiceReceiptCostValuations_Guard ON dbo.VendorInvoiceReceiptCostValuations
        AFTER INSERT, UPDATE, DELETE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS(SELECT 1 FROM deleted) THROW 51938, 'Invoice receipt valuation evidence is immutable.', 1;
          IF EXISTS(SELECT 1 FROM inserted v
            LEFT JOIN dbo.VendorInvoiceReceiptCostAllocations c ON c.Id=v.CostAllocationId AND c.TenantId=v.TenantId AND c.IsDeleted=0
            LEFT JOIN dbo.InventoryMovements m ON m.Id=v.InventoryMovementId AND m.TenantId=v.TenantId AND m.InventoryItemId=c.InventoryItemId AND m.IsDeleted=0
            LEFT JOIN dbo.WarehouseLocations loc ON loc.Id=v.LocationId AND loc.TenantId=v.TenantId
              AND CASE WHEN loc.IsConsignmentBin=1 AND loc.ConsignmentWarehouseId IS NOT NULL
                AND loc.ConsignmentWarehouseId<>'00000000-0000-0000-0000-000000000000' THEN loc.ConsignmentWarehouseId ELSE loc.WarehouseId END=v.WarehouseId AND loc.IsDeleted=0
            WHERE c.Id IS NULL OR m.Id IS NULL OR loc.Id IS NULL OR v.IsDeleted=1 OR v.ValueChange=0 OR v.AttributedReceiptBaseQuantity<=0
              OR m.MovementType<>17 OR m.Quantity<>0 OR m.TotalValue<>v.ValueChange OR m.WarehouseId<>v.WarehouseId
              OR ISNULL(m.LocationId,'00000000-0000-0000-0000-000000000000')<>v.LocationId OR m.IsPosted<>1
              OR ISNULL(m.ReferenceId,'00000000-0000-0000-0000-000000000000')<>c.VendorInvoiceId OR m.IsReversal<>v.IsReversal
              OR (v.IsReversal=0 AND (SIGN(v.ValueChange)<>SIGN(c.InventoryAdjustmentAmount) OR c.ReversalJournalEntryId IS NOT NULL
                OR EXISTS(SELECT 1 FROM dbo.VendorInvoice i WHERE i.Id=c.VendorInvoiceId AND i.TenantId=c.TenantId AND i.JournalEntryId IS NOT NULL)))
              OR (v.IsReversal=1 AND (c.ReversalJournalEntryId IS NOT NULL OR SIGN(v.ValueChange)=SIGN(c.InventoryAdjustmentAmount) OR NOT EXISTS(SELECT 1 FROM dbo.JournalEntries j
                WHERE j.Id=c.JournalEntryId AND j.TenantId=c.TenantId AND j.IsReversed=1 AND j.ReversalJournalEntryId IS NOT NULL))))
            THROW 51939, 'Invoice value-only movement requires exact same-tenant receipt cost authority and storage-bin evidence.', 1;
        END
        """;

    internal const string InvoiceSeal = """
        CREATE OR ALTER TRIGGER dbo.TR_VendorInvoice_ReceiptCostSeal ON dbo.VendorInvoice
        AFTER UPDATE AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.VendorInvoiceReceiptCostAllocations c ON c.VendorInvoiceId=i.Id AND c.TenantId=i.TenantId
            WHERE i.JournalEntryId IS NOT NULL AND (i.IsDeleted=1 OR c.IsDeleted=1 OR c.JournalEntryId<>i.JournalEntryId
              OR (i.Status=7 AND c.ReversalJournalEntryId IS NULL)
              OR c.ReceiptFunctionalAmount<>ISNULL((SELECT SUM(l.FunctionalAmount) FROM dbo.VendorInvoiceReceiptCostPostingLines l WHERE l.CostAllocationId=c.Id AND l.TenantId=c.TenantId AND l.Purpose='Accrual'),0)
              OR c.InventoryAdjustmentAmount<>ISNULL((SELECT SUM(l.FunctionalAmount) FROM dbo.VendorInvoiceReceiptCostPostingLines l WHERE l.CostAllocationId=c.Id AND l.TenantId=c.TenantId AND l.Purpose='Inventory'),0)
              OR c.PurchasePriceVarianceAmount<>ISNULL((SELECT SUM(l.FunctionalAmount) FROM dbo.VendorInvoiceReceiptCostPostingLines l WHERE l.CostAllocationId=c.Id AND l.TenantId=c.TenantId AND l.Purpose='Ppv'),0)
              OR c.ExchangeDifferenceFunctionalAmount<>ISNULL((SELECT SUM(l.FunctionalAmount) FROM dbo.VendorInvoiceReceiptCostPostingLines l WHERE l.CostAllocationId=c.Id AND l.TenantId=c.TenantId AND l.Purpose='Fx'),0)
              OR c.InventoryAdjustmentAmount<>ISNULL((SELECT SUM(v.ValueChange) FROM dbo.VendorInvoiceReceiptCostValuations v WHERE v.CostAllocationId=c.Id AND v.TenantId=c.TenantId AND v.IsReversal=0),0)))
            THROW 51940, 'Invoice receipt cost posting is incomplete or does not conserve its immutable distributions and valuation movements.', 1;
          IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.VendorInvoiceLineItem l ON l.VendorInvoiceId=i.Id AND l.TenantId=i.TenantId AND l.IsDeleted=0
            WHERE i.JournalEntryId IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.VendorInvoiceReceiptCostAllocations c WHERE c.VendorInvoiceId=i.Id AND c.TenantId=i.TenantId)
              AND (l.Quantity<>ISNULL((SELECT SUM(c.PurchaseQuantity) FROM dbo.VendorInvoiceReceiptCostAllocations c WHERE c.VendorInvoiceId=i.Id AND c.TenantId=i.TenantId AND c.VendorInvoiceLineItemId=l.Id),0)
                OR ROUND(l.Quantity*l.UnitPrice-l.DiscountAmount,2)<>ISNULL((SELECT SUM(c.InvoiceNetForeignAmount) FROM dbo.VendorInvoiceReceiptCostAllocations c WHERE c.VendorInvoiceId=i.Id AND c.TenantId=i.TenantId AND c.VendorInvoiceLineItemId=l.Id),0)))
            THROW 51942, 'Every invoice line must retain its full quantity and commercial amount across exact receipt cost shares.', 1;
          IF EXISTS(SELECT c.VendorInvoiceId,c.VendorInvoiceLineItemId,l.AccountId,
              CASE l.Purpose WHEN 'Accrual' THEN 'AP-GRV' WHEN 'Inventory' THEN 'AP-INVENTORY-COST' WHEN 'Ppv' THEN 'AP-PRICE-VARIANCE' ELSE 'AP-RECEIPT-FX' END,
              SUM(l.FunctionalAmount)
            FROM inserted i JOIN dbo.VendorInvoiceReceiptCostAllocations c ON c.VendorInvoiceId=i.Id AND c.TenantId=i.TenantId
            JOIN dbo.VendorInvoiceReceiptCostPostingLines l ON l.CostAllocationId=c.Id AND l.TenantId=c.TenantId
            WHERE i.JournalEntryId IS NOT NULL
            GROUP BY c.VendorInvoiceId,c.VendorInvoiceLineItemId,l.AccountId,l.Purpose
            EXCEPT SELECT i.Id,t.SourceDocumentLineId,t.AccountId,t.TransactionTag,SUM(t.DebitAmount-t.CreditAmount)
            FROM inserted i JOIN dbo.AccountTransactions t ON t.JournalEntryId=i.JournalEntryId AND t.TenantId=i.TenantId AND t.IsDeleted=0
            WHERE t.TransactionTag IN('AP-GRV','AP-INVENTORY-COST','AP-PRICE-VARIANCE','AP-RECEIPT-FX')
            GROUP BY i.Id,t.SourceDocumentLineId,t.AccountId,t.TransactionTag)
            THROW 51943, 'Receipt cost account purposes must equal the actual immutable invoice journal distribution.', 1;
          IF EXISTS(SELECT i.Id,t.SourceDocumentLineId,t.AccountId,t.TransactionTag,SUM(t.DebitAmount-t.CreditAmount)
            FROM inserted i JOIN dbo.AccountTransactions t ON t.JournalEntryId=i.JournalEntryId AND t.TenantId=i.TenantId AND t.IsDeleted=0
            WHERE t.TransactionTag IN('AP-GRV','AP-INVENTORY-COST','AP-PRICE-VARIANCE','AP-RECEIPT-FX')
              AND EXISTS(SELECT 1 FROM dbo.VendorInvoiceReceiptCostAllocations c WHERE c.VendorInvoiceId=i.Id AND c.TenantId=i.TenantId)
            GROUP BY i.Id,t.SourceDocumentLineId,t.AccountId,t.TransactionTag
            EXCEPT SELECT c.VendorInvoiceId,c.VendorInvoiceLineItemId,l.AccountId,
              CASE l.Purpose WHEN 'Accrual' THEN 'AP-GRV' WHEN 'Inventory' THEN 'AP-INVENTORY-COST' WHEN 'Ppv' THEN 'AP-PRICE-VARIANCE' ELSE 'AP-RECEIPT-FX' END,
              SUM(l.FunctionalAmount)
            FROM inserted i JOIN dbo.VendorInvoiceReceiptCostAllocations c ON c.VendorInvoiceId=i.Id AND c.TenantId=i.TenantId
            JOIN dbo.VendorInvoiceReceiptCostPostingLines l ON l.CostAllocationId=c.Id AND l.TenantId=c.TenantId
            WHERE i.JournalEntryId IS NOT NULL GROUP BY c.VendorInvoiceId,c.VendorInvoiceLineItemId,l.AccountId,l.Purpose)
            THROW 51948, 'Invoice journal has receipt cost account amounts not represented by retained evidence.', 1;
          IF EXISTS(SELECT 1 FROM inserted i WHERE i.Status=7
            AND EXISTS(SELECT 1 FROM dbo.VendorInvoiceReceiptCostAllocations c WHERE c.TenantId=i.TenantId AND c.VendorInvoiceId=i.Id)
            AND (EXISTS(SELECT 1 FROM dbo.VendorInvoiceReceiptCostAllocations c WHERE c.TenantId=i.TenantId AND c.VendorInvoiceId=i.Id AND c.ReversalJournalEntryId IS NULL)
              OR ISNULL((SELECT SUM(c.InventoryAdjustmentAmount) FROM dbo.VendorInvoiceReceiptCostAllocations c WHERE c.TenantId=i.TenantId AND c.VendorInvoiceId=i.Id),0)
                + ISNULL((SELECT SUM(v.ValueChange) FROM dbo.VendorInvoiceReceiptCostValuations v
                  JOIN dbo.VendorInvoiceReceiptCostAllocations c ON c.Id=v.CostAllocationId AND c.TenantId=v.TenantId
                  WHERE c.TenantId=i.TenantId AND c.VendorInvoiceId=i.Id AND v.IsReversal=1),0)
                <> ISNULL((SELECT SUM(t.DebitAmount-t.CreditAmount) FROM dbo.AccountTransactions t
                  WHERE t.TenantId=i.TenantId AND t.IsDeleted=0 AND t.TransactionTag='AP-RECEIPT-COST-REVERSAL'
                    AND EXISTS(SELECT 1 FROM dbo.VendorInvoiceReceiptCostAllocations c
                      JOIN dbo.VendorInvoiceReceiptCostPostingLines l ON l.CostAllocationId=c.Id AND l.TenantId=c.TenantId AND l.Purpose='Inventory'
                      WHERE c.TenantId=i.TenantId AND c.VendorInvoiceId=i.Id AND c.ReversalReclassificationJournalEntryId=t.JournalEntryId
                        AND l.AccountId=t.AccountId AND c.VendorInvoiceLineItemId=t.SourceDocumentLineId)),0)))
            THROW 51949, 'Voided receipt invoice must conserve retained inventory reversal and consumed-cost reclassification.', 1;
        END
        """;

    // Preserve all earlier Auto Invoice lineage rules while allowing exact manual
    // receipt allocations only inside the approved invoice's successful posting.
    internal static readonly string ReceiptAllocation = ProcurementAutoInvoiceReceipts.AllocationTrigger
        .Replace("OR i.AutoInvoiceRequestId IS NULL OR i.Status<>1 OR i.AcceptedSupplyKind<>4 OR i.PurchaseOrderId IS NOT NULL",
            "OR (i.AutoInvoiceRequestId IS NOT NULL AND (i.Status<>1 OR ISNULL(i.AcceptedSupplyKind,0)<>4 OR i.PurchaseOrderId IS NOT NULL OR l.Quantity<>a.Quantity)) OR (i.AutoInvoiceRequestId IS NULL AND (i.Status<>3 OR ISNULL(i.PurchaseOrderId,'00000000-0000-0000-0000-000000000000')<>po.Id OR i.MatchingStatus<>2 OR i.MatchingControlEventId IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.FinancePostingEvents e WHERE e.TenantId=i.TenantId AND e.SourceDocumentId=i.Id AND e.SourceDocumentType='VendorInvoice' AND e.PostingAction='Post' AND e.PostingStatus='Posted' AND e.IsDeleted=0)))")
        .Replace("OR l.PurchaseOrderItemId<>pi.Id OR l.Quantity<>a.Quantity", "OR l.PurchaseOrderItemId<>pi.Id OR l.Quantity<(SELECT SUM(s.Quantity) FROM dbo.VendorInvoiceReceiptAllocations s WHERE s.VendorInvoiceLineItemId=l.Id AND s.TenantId=l.TenantId AND s.IsDeleted=0)");
    internal static readonly string ReceiptLine = ProcurementAutoInvoiceReceipts.LineTrigger
        .Replace("i.Quantity<>a.Quantity", "i.Quantity<>(SELECT SUM(s.Quantity) FROM dbo.VendorInvoiceReceiptAllocations s WHERE s.VendorInvoiceLineItemId=i.Id AND s.TenantId=i.TenantId AND s.IsDeleted=0)");
}
