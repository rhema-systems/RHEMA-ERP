using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

/// <summary>Additive exact receipt/invoice claims. Historical whole-return guards remain active.</summary>
internal static class InventorySupplierReturnAllocationGuards
{
    internal static void Install(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(GroupGuard);
        migrationBuilder.Sql(AllocationGuard);
        migrationBuilder.Sql(AccrualGuard);
        migrationBuilder.Sql(SealGuard);
        migrationBuilder.Sql(CreditGuard);
        migrationBuilder.Sql(CreditLineGuard);
        migrationBuilder.Sql(LegacyGuardPatch);
    }

    internal static void Remove(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        IF EXISTS(SELECT 1 FROM dbo.InventorySupplierReturnAccountingGroups)
           OR EXISTS(SELECT 1 FROM dbo.InventorySupplierReturnAllocations)
           OR EXISTS(SELECT 1 FROM dbo.InventorySupplierReturnAccrualShares)
           OR EXISTS(SELECT 1 FROM dbo.PurchaseReturns WHERE AccountingAllocationVersion<>0)
            THROW 52860,'RTV_ALLOCATION_ROLLBACK_RETAINS_HISTORY',1;
        DECLARE @sql nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_SupplierDebitNotes_InventoryReturnCreditGuard'));
        DECLARE @old1 nvarchar(400)=N'WHERE i.InventorySupplierReturnAccountingGroupId IS NULL AND i.InventoryPurchaseReturnId IS NOT NULL AND (r.Id IS NULL';
        DECLARE @old2 nvarchar(400)=N'WHERE i.InventorySupplierReturnAccountingGroupId IS NULL AND i.DirectInvoiceAppliedAmount>0 AND (p.Id IS NULL';
        IF @sql IS NULL OR (DATALENGTH(@sql)-DATALENGTH(REPLACE(@sql,@old1,N'')))/DATALENGTH(@old1)<>1
           OR (DATALENGTH(@sql)-DATALENGTH(REPLACE(@sql,@old2,N'')))/DATALENGTH(@old2)<>1
            THROW 52859,'RTV_LEGACY_GUARD_REVIEW_REQUIRED',1;
        SET @sql=REPLACE(@sql,@old1,N'WHERE i.InventoryPurchaseReturnId IS NOT NULL AND (r.Id IS NULL');
        SET @sql=REPLACE(@sql,@old2,N'WHERE i.DirectInvoiceAppliedAmount>0 AND (p.Id IS NULL');
        SET @sql=STUFF(@sql,1,CHARINDEX(N'TRIGGER',@sql)+LEN(N'TRIGGER')-1,N'CREATE OR ALTER TRIGGER');
        EXEC sys.sp_executesql @sql;
        DROP TRIGGER IF EXISTS dbo.TR_InventorySupplierReturnAccountingGroups_Authority;
        DROP TRIGGER IF EXISTS dbo.TR_InventorySupplierReturnAllocations_Authority;
        DROP TRIGGER IF EXISTS dbo.TR_InventorySupplierReturnAccrualShares_Authority;
        DROP TRIGGER IF EXISTS dbo.TR_PurchaseReturns_AccountingAllocationSeal;
        DROP TRIGGER IF EXISTS dbo.TR_SupplierDebitNotes_ReturnAllocationGuard;
        DROP TRIGGER IF EXISTS dbo.TR_SupplierDebitNoteLines_ReturnAllocationGuard;
        """);

    internal const string GroupGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_InventorySupplierReturnAccountingGroups_Authority
        ON dbo.InventorySupplierReturnAccountingGroups AFTER INSERT, UPDATE, DELETE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS(SELECT 1 FROM deleted) THROW 52840,'RTV_ACCOUNTING_GROUP_IMMUTABLE',1;
            IF EXISTS(SELECT 1 FROM inserted i
              LEFT JOIN dbo.PurchaseReturns r WITH(UPDLOCK,HOLDLOCK) ON r.Id=i.InventoryPurchaseReturnId AND r.TenantId=i.TenantId
              LEFT JOIN dbo.VendorInvoice v ON v.Id=i.OriginalVendorInvoiceId AND v.TenantId=i.TenantId
              LEFT JOIN dbo.JournalEntries vj ON vj.Id=v.JournalEntryId AND vj.TenantId=i.TenantId
              LEFT JOIN dbo.FinancePostingEvents e ON e.Id=i.DispatchPostingEventId AND e.TenantId=i.TenantId
              LEFT JOIN dbo.JournalEntries j ON j.Id=i.DispatchJournalEntryId AND j.TenantId=i.TenantId
              WHERE i.IsDeleted=1 OR r.Id IS NULL OR r.IsDeleted=1 OR r.Status<>'Shipped' OR r.AccountingAllocationVersion<>0
                OR i.CarryingAmount<0 OR i.OriginalAccrualAmount<0 OR i.CapturedAtUtc IS NULL
                OR e.Id IS NULL OR e.IsDeleted=1 OR e.PostingStatus<>'Posted' OR e.SourceDocumentType<>'SupplierReturnDispatch'
                OR e.SourceDocumentId<>r.Id OR e.JournalEntryId<>j.Id OR j.Id IS NULL OR j.IsDeleted=1 OR j.IsReversed=1
                OR j.PostingStatus<>'Posted' OR j.SourceDocumentId<>r.Id OR j.SourceDocumentType<>'SupplierReturnDispatch'
                OR (i.OriginalVendorInvoiceId IS NOT NULL AND (v.Id IS NULL OR v.IsDeleted=1 OR v.BusinessPartnerId<>r.SupplierId
                    OR v.Status=7 OR vj.Id IS NULL OR vj.IsDeleted=1 OR vj.IsReversed=1 OR vj.PostingStatus<>'Posted'
                    OR vj.SourceDocumentType<>'VendorInvoice' OR vj.SourceDocumentId<>v.Id
                    OR vj.AccountingBookId IS NULL OR vj.AccountingBookId<>j.AccountingBookId
                    OR i.ClearingAccountId IS NULL OR i.OriginalAccrualAmount<>0))
                OR (i.OriginalVendorInvoiceId IS NULL AND i.ClearingAccountId IS NOT NULL))
              THROW 52841,'RTV_ACCOUNTING_GROUP_SOURCE_INVALID',1;
        END;
        """;

    internal const string AllocationGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_InventorySupplierReturnAllocations_Authority
        ON dbo.InventorySupplierReturnAllocations AFTER INSERT, UPDATE, DELETE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS(SELECT 1 FROM deleted) THROW 52842,'RTV_RECEIPT_ALLOCATION_IMMUTABLE',1;
            DECLARE @locked uniqueidentifier;
            SELECT @locked=g.Id FROM dbo.GoodsReceiptNoteItems g WITH(UPDLOCK,HOLDLOCK)
              JOIN inserted i ON i.GoodsReceiptNoteItemId=g.Id AND i.TenantId=g.TenantId ORDER BY g.Id;
            IF EXISTS(SELECT 1 FROM inserted i
              LEFT JOIN dbo.PurchaseReturns r ON r.Id=i.InventoryPurchaseReturnId AND r.TenantId=i.TenantId
              LEFT JOIN dbo.PurchaseReturnItems l ON l.Id=i.InventoryPurchaseReturnItemId AND l.TenantId=i.TenantId
              LEFT JOIN dbo.GoodsReceiptNoteItems g ON g.Id=i.GoodsReceiptNoteItemId AND g.TenantId=i.TenantId
              LEFT JOIN dbo.GoodsReceiptNotes h ON h.Id=g.GoodsReceiptNoteId AND h.TenantId=i.TenantId
              LEFT JOIN dbo.PurchaseOrderReceiptItems p ON p.Id=i.PurchaseOrderReceiptItemId AND p.TenantId=i.TenantId
              LEFT JOIN dbo.InventorySupplierReturnAccountingGroups a ON a.Id=i.AccountingGroupId AND a.TenantId=i.TenantId
              LEFT JOIN dbo.VendorInvoiceReceiptAllocations v ON v.Id=i.VendorInvoiceReceiptAllocationId AND v.TenantId=i.TenantId
              LEFT JOIN dbo.JournalEntries j ON j.Id=i.OriginalReceiptJournalEntryId AND j.TenantId=i.TenantId
              LEFT JOIN dbo.ProcurementReceiptCostBases b ON b.Id=i.ProcurementReceiptCostBasisId AND b.TenantId=i.TenantId
              WHERE i.IsDeleted=1 OR r.Id IS NULL OR r.IsDeleted=1 OR r.Status<>'Shipped' OR r.AccountingAllocationVersion<>0
                OR l.Id IS NULL OR l.IsDeleted=1 OR l.PurchaseReturnId<>r.Id OR l.GoodsReceiptNoteItemId<>g.Id OR l.StockReversed=0
                OR g.Id IS NULL OR g.IsDeleted=1 OR g.GoodsReceiptNoteId<>r.GoodsReceiptNoteId OR g.InventoryItemId<>l.InventoryItemId
                OR a.Id IS NULL OR a.IsDeleted=1 OR a.InventoryPurchaseReturnId<>r.Id
                OR ISNULL(a.OriginalVendorInvoiceId,'00000000-0000-0000-0000-000000000000')<>ISNULL(i.OriginalVendorInvoiceId,'00000000-0000-0000-0000-000000000000')
                OR i.BaseQuantity<=0 OR i.PurchaseQuantity<=0 OR i.ConversionToBase<=0 OR i.PurchaseQuantity*i.ConversionToBase<>i.BaseQuantity
                OR h.Id IS NULL OR h.IsDeleted=1 OR h.StockUpdated=0 OR p.Id IS NULL OR p.IsDeleted=1
                OR p.ReceiptId<>h.PurchaseOrderReceiptId OR p.PurchaseOrderItemId<>g.PurchaseOrderItemId OR p.AcceptedQuantity<=0
                OR i.BaseQuantity*p.AcceptedQuantity<>i.PurchaseQuantity*g.AcceptedQuantity
                OR i.CarryingAmount<0 OR i.OriginalAccrualAmount<0 OR i.OriginalAccrualForeignAmount<0
                OR (i.ProcurementReceiptCostBasisId IS NOT NULL AND (b.Id IS NULL OR b.IsDeleted=1
                    OR b.PurchaseOrderReceiptItemId<>p.Id OR b.PurchaseOrderReceiptId<>p.ReceiptId OR b.InventoryItemId<>l.InventoryItemId
                    OR b.PurchaseCurrency<>i.PurchaseCurrency OR b.FunctionalCurrency<>i.FunctionalCurrency OR b.ConversionToBase<>i.ConversionToBase))
                OR (i.ProcurementReceiptCostBasisId IS NULL AND (i.PurchaseCurrency<>i.FunctionalCurrency OR i.OriginalAccrualForeignAmount<>i.OriginalAccrualAmount))
                OR (i.ProcurementReceiptCostBasisId IS NULL AND EXISTS(SELECT 1 FROM dbo.ProcurementReceiptCostBases retained
                    WHERE retained.TenantId=i.TenantId AND retained.PurchaseOrderReceiptItemId=p.Id AND retained.IsDeleted=0))
                OR j.Id IS NULL OR j.IsDeleted=1 OR j.IsReversed=1 OR j.PostingStatus<>'Posted'
                OR j.SourceDocumentType<>'ProcurementPurchaseOrderReceipt' OR j.SourceDocumentId<>p.ReceiptId
                OR (i.OriginalVendorInvoiceId IS NOT NULL AND (v.Id IS NULL OR v.IsDeleted=1 OR v.GoodsReceiptNoteItemId<>g.Id
                    OR v.VendorInvoiceId<>i.OriginalVendorInvoiceId OR v.VendorInvoiceLineItemId<>i.OriginalVendorInvoiceLineItemId
                    OR v.PurchaseOrderReceiptItemId<>i.PurchaseOrderReceiptItemId OR i.OriginalAccrualAmount<>0)))
              THROW 52843,'RTV_RECEIPT_ALLOCATION_SOURCE_INVALID',1;
            IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.PurchaseReturnItems l ON l.Id=i.InventoryPurchaseReturnItemId
              WHERE (SELECT SUM(a.BaseQuantity) FROM dbo.InventorySupplierReturnAllocations a WITH(UPDLOCK,HOLDLOCK)
                WHERE a.TenantId=i.TenantId AND a.InventoryPurchaseReturnItemId=l.Id)>l.ReturnQuantity)
              THROW 52844,'RTV_RETURN_LINE_CAPACITY_EXCEEDED',1;
            IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.GoodsReceiptNoteItems g ON g.Id=i.GoodsReceiptNoteItemId
              WHERE (SELECT SUM(a.BaseQuantity) FROM dbo.InventorySupplierReturnAllocations a WITH(UPDLOCK,HOLDLOCK)
                WHERE a.TenantId=i.TenantId AND a.GoodsReceiptNoteItemId=g.Id)>g.AcceptedQuantity)
              THROW 52845,'RTV_RECEIPT_CAPACITY_EXCEEDED',1;
            IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.VendorInvoiceReceiptAllocations v ON v.Id=i.VendorInvoiceReceiptAllocationId
              WHERE (SELECT SUM(a.PurchaseQuantity) FROM dbo.InventorySupplierReturnAllocations a WITH(UPDLOCK,HOLDLOCK)
                WHERE a.TenantId=i.TenantId AND a.VendorInvoiceReceiptAllocationId=v.Id)>v.Quantity)
              THROW 52846,'RTV_INVOICE_SHARE_CAPACITY_EXCEEDED',1;
            IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.PurchaseOrderReceiptItems p ON p.Id=i.PurchaseOrderReceiptItemId
              WHERE i.OriginalVendorInvoiceId IS NULL AND
                COALESCE((SELECT SUM(a.PurchaseQuantity) FROM dbo.InventorySupplierReturnAllocations a WITH(UPDLOCK,HOLDLOCK)
                    WHERE a.TenantId=i.TenantId AND a.GoodsReceiptNoteItemId=i.GoodsReceiptNoteItemId AND a.OriginalVendorInvoiceId IS NULL),0)
                +COALESCE((SELECT SUM(a.Quantity) FROM dbo.VendorInvoiceReceiptAllocations a WITH(UPDLOCK,HOLDLOCK)
                    JOIN dbo.VendorInvoice v ON v.Id=a.VendorInvoiceId AND v.TenantId=a.TenantId
                    WHERE a.TenantId=i.TenantId AND a.IsDeleted=0 AND a.GoodsReceiptNoteItemId=i.GoodsReceiptNoteItemId
                      AND v.IsDeleted=0 AND v.Status<>7),0)>p.AcceptedQuantity)
              THROW 52861,'RTV_UNINVOICED_CAPACITY_EXCEEDED',1;
            IF EXISTS(SELECT 1 FROM inserted i
              JOIN dbo.ProcurementReceiptCostBases b ON b.Id=i.ProcurementReceiptCostBasisId AND b.TenantId=i.TenantId
              WHERE COALESCE((SELECT SUM(a.OriginalAccrualForeignAmount) FROM dbo.InventorySupplierReturnAllocations a WITH(UPDLOCK,HOLDLOCK)
                    WHERE a.TenantId=i.TenantId AND a.GoodsReceiptNoteItemId=i.GoodsReceiptNoteItemId AND a.OriginalVendorInvoiceId IS NULL),0)
                +COALESCE((SELECT SUM(c.ReceiptForeignAmount) FROM dbo.VendorInvoiceReceiptCostAllocations c WITH(UPDLOCK,HOLDLOCK)
                    WHERE c.TenantId=i.TenantId AND c.GoodsReceiptNoteItemId=i.GoodsReceiptNoteItemId AND c.IsDeleted=0 AND c.ReversalJournalEntryId IS NULL),0)>b.PurchaseAmount)
              THROW 52864,'RTV_ORIGINAL_FOREIGN_ACCRUAL_CAPACITY_EXCEEDED',1;
        END;
        """;

    internal const string AccrualGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_InventorySupplierReturnAccrualShares_Authority
        ON dbo.InventorySupplierReturnAccrualShares AFTER INSERT, UPDATE, DELETE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS(SELECT 1 FROM deleted) THROW 52847,'RTV_ACCRUAL_SHARE_IMMUTABLE',1;
            IF EXISTS(SELECT 1 FROM inserted i
              LEFT JOIN dbo.InventorySupplierReturnAllocations a ON a.Id=i.InventorySupplierReturnAllocationId AND a.TenantId=i.TenantId
              LEFT JOIN dbo.PurchaseReturns r ON r.Id=a.InventoryPurchaseReturnId AND r.TenantId=i.TenantId
              LEFT JOIN dbo.PurchaseReturnItems l ON l.Id=a.InventoryPurchaseReturnItemId AND l.TenantId=i.TenantId
              LEFT JOIN dbo.AccountTransactions t WITH(UPDLOCK,HOLDLOCK) ON t.Id=i.OriginalReceiptAccountTransactionId AND t.TenantId=i.TenantId
              WHERE i.IsDeleted=1 OR a.Id IS NULL OR a.IsDeleted=1 OR a.OriginalVendorInvoiceId IS NOT NULL OR r.AccountingAllocationVersion<>0
                OR t.Id IS NULL OR t.IsDeleted=1 OR t.PostingStatus<>'Posted' OR t.JournalEntryId<>a.OriginalReceiptJournalEntryId
                OR l.Id IS NULL OR l.IsDeleted=1 OR t.SourceDocumentLineId IS NULL OR t.SourceDocumentLineId<>l.InventoryItemId
                OR t.AccountId<>i.AccountId OR t.TransactionTag<>'INV-RECEIPT-GRV-ACCRUAL' OR t.CreditAmount<=t.DebitAmount OR i.Amount<0)
              THROW 52848,'RTV_ACCRUAL_SHARE_SOURCE_INVALID',1;
            IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.InventorySupplierReturnAllocations a ON a.Id=i.InventorySupplierReturnAllocationId
              WHERE (SELECT SUM(s.Amount) FROM dbo.InventorySupplierReturnAccrualShares s WITH(UPDLOCK,HOLDLOCK)
                WHERE s.TenantId=i.TenantId AND s.InventorySupplierReturnAllocationId=a.Id)>a.OriginalAccrualAmount)
              THROW 52849,'RTV_ACCRUAL_ALLOCATION_CAPACITY_EXCEEDED',1;
            IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.AccountTransactions t ON t.Id=i.OriginalReceiptAccountTransactionId
              WHERE COALESCE((SELECT SUM(s.Amount) FROM dbo.InventorySupplierReturnAccrualShares s WITH(UPDLOCK,HOLDLOCK)
                    WHERE s.TenantId=i.TenantId AND s.OriginalReceiptAccountTransactionId=t.Id),0)
                + COALESCE((SELECT SUM(c.FunctionalAmount) FROM dbo.VendorInvoiceReceiptCostPostingLines c
                    JOIN dbo.VendorInvoiceReceiptCostAllocations h ON h.Id=c.CostAllocationId AND h.TenantId=c.TenantId
                    WHERE c.TenantId=i.TenantId AND c.IsDeleted=0 AND h.IsDeleted=0 AND h.ReversalJournalEntryId IS NULL
                      AND c.Purpose='Accrual' AND c.OriginalReceiptAccountTransactionId=t.Id),0)>t.CreditAmount-t.DebitAmount)
              THROW 52850,'RTV_ORIGINAL_ACCRUAL_CAPACITY_EXCEEDED',1;
        END;
        """;

    internal const string SealGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_PurchaseReturns_AccountingAllocationSeal
        ON dbo.PurchaseReturns AFTER INSERT, UPDATE, DELETE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id
              WHERE d.AccountingAllocationVersion=1 AND (i.Id IS NULL OR i.AccountingAllocationVersion<>1 OR i.IsDeleted<>d.IsDeleted
                OR i.TenantId<>d.TenantId OR i.GoodsReceiptNoteId<>d.GoodsReceiptNoteId OR i.SupplierId<>d.SupplierId OR i.WarehouseId<>d.WarehouseId))
              THROW 52851,'RTV_ACCOUNTING_SEAL_IMMUTABLE',1;
            IF EXISTS(SELECT 1 FROM inserted WHERE AccountingAllocationVersion NOT IN(0,1))
              THROW 52852,'RTV_ACCOUNTING_VERSION_INVALID',1;
            IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id WHERE i.AccountingAllocationVersion=1 AND ISNULL(d.AccountingAllocationVersion,0)=0 AND
              (i.Status<>'Shipped' OR i.IsDeleted=1 OR NOT EXISTS(SELECT 1 FROM dbo.InventorySupplierReturnAccountingGroups g WHERE g.TenantId=i.TenantId AND g.InventoryPurchaseReturnId=i.Id)
               OR EXISTS(SELECT 1 FROM dbo.PurchaseReturnItems l WHERE l.TenantId=i.TenantId AND l.PurchaseReturnId=i.Id AND l.IsDeleted=0 AND
                 COALESCE((SELECT SUM(a.BaseQuantity) FROM dbo.InventorySupplierReturnAllocations a WHERE a.TenantId=i.TenantId AND a.InventoryPurchaseReturnItemId=l.Id),0)<>l.ReturnQuantity)
               OR EXISTS(SELECT 1 FROM dbo.InventorySupplierReturnAccountingGroups g WHERE g.TenantId=i.TenantId AND g.InventoryPurchaseReturnId=i.Id AND
                 (COALESCE((SELECT SUM(a.CarryingAmount) FROM dbo.InventorySupplierReturnAllocations a WHERE a.TenantId=i.TenantId AND a.AccountingGroupId=g.Id),0)<>g.CarryingAmount
                  OR COALESCE((SELECT SUM(a.OriginalAccrualAmount) FROM dbo.InventorySupplierReturnAllocations a WHERE a.TenantId=i.TenantId AND a.AccountingGroupId=g.Id),0)<>g.OriginalAccrualAmount))
               OR EXISTS(SELECT 1 FROM dbo.InventorySupplierReturnAllocations a WHERE a.TenantId=i.TenantId AND a.InventoryPurchaseReturnId=i.Id AND
                 COALESCE((SELECT SUM(s.Amount) FROM dbo.InventorySupplierReturnAccrualShares s WHERE s.TenantId=i.TenantId AND s.InventorySupplierReturnAllocationId=a.Id),0)<>a.OriginalAccrualAmount)))
              THROW 52853,'RTV_ACCOUNTING_SEAL_INCOMPLETE',1;
            IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id WHERE i.AccountingAllocationVersion=1 AND ISNULL(d.AccountingAllocationVersion,0)=0 AND
              ((SELECT COUNT(DISTINCT g.DispatchJournalEntryId) FROM dbo.InventorySupplierReturnAccountingGroups g WHERE g.TenantId=i.TenantId AND g.InventoryPurchaseReturnId=i.Id)<>1
               OR COALESCE((SELECT SUM(g.CarryingAmount) FROM dbo.InventorySupplierReturnAccountingGroups g WHERE g.TenantId=i.TenantId AND g.InventoryPurchaseReturnId=i.Id),0)
                  <>COALESCE((SELECT SUM(m.TotalValue) FROM dbo.InventoryMovements m WHERE m.TenantId=i.TenantId AND m.ReferenceId=i.Id AND m.ReferenceType=7
                     AND m.MovementType=10 AND m.Direction=2 AND m.IsPosted=1 AND m.IsDeleted=0 AND m.IsReversal=0),0)
               OR EXISTS(SELECT 1 FROM dbo.InventorySupplierReturnAccountingGroups g WHERE g.TenantId=i.TenantId AND g.InventoryPurchaseReturnId=i.Id AND
                 (SELECT SUM(a.CarryingAmount) FROM dbo.InventorySupplierReturnAccountingGroups a WHERE a.TenantId=i.TenantId AND a.InventoryPurchaseReturnId=i.Id)
                   <>COALESCE((SELECT SUM(t.CreditAmount-t.DebitAmount) FROM dbo.AccountTransactions t WHERE t.TenantId=i.TenantId AND t.JournalEntryId=g.DispatchJournalEntryId
                       AND t.IsDeleted=0 AND t.PostingStatus='Posted' AND t.TransactionTag='RTV-Dispatch-Inventory'),0))
               OR EXISTS(SELECT 1 FROM dbo.InventorySupplierReturnAccountingGroups g WHERE g.TenantId=i.TenantId AND g.InventoryPurchaseReturnId=i.Id AND g.OriginalVendorInvoiceId IS NOT NULL AND
                 (SELECT SUM(a.CarryingAmount) FROM dbo.InventorySupplierReturnAccountingGroups a WHERE a.TenantId=i.TenantId AND a.InventoryPurchaseReturnId=i.Id AND a.ClearingAccountId=g.ClearingAccountId)
                   <>COALESCE((SELECT SUM(t.DebitAmount-t.CreditAmount) FROM dbo.AccountTransactions t WHERE t.TenantId=i.TenantId AND t.JournalEntryId=g.DispatchJournalEntryId
                       AND t.AccountId=g.ClearingAccountId AND t.IsDeleted=0 AND t.PostingStatus='Posted' AND t.TransactionTag='RTV-Dispatch-Clearing'),0))
               OR EXISTS(SELECT 1 FROM dbo.InventorySupplierReturnAccountingGroups g WHERE g.TenantId=i.TenantId AND g.InventoryPurchaseReturnId=i.Id AND g.OriginalVendorInvoiceId IS NULL AND
                 g.OriginalAccrualAmount<>COALESCE((SELECT SUM(t.DebitAmount-t.CreditAmount) FROM dbo.AccountTransactions t WHERE t.TenantId=i.TenantId
                    AND t.JournalEntryId=g.DispatchJournalEntryId AND t.IsDeleted=0 AND t.PostingStatus='Posted' AND t.TransactionTag='RTV-Uninvoiced-Accrual'),0))))
              THROW 52862,'RTV_ACCOUNTING_SEAL_LEDGER_MISMATCH',1;
        END;
        """;

    internal const string CreditGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_SupplierDebitNotes_ReturnAllocationGuard
        ON dbo.SupplierDebitNotes AFTER INSERT, UPDATE, DELETE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.PurchaseReturns r ON r.Id=i.InventoryPurchaseReturnId AND r.TenantId=i.TenantId
              WHERE i.InventorySupplierReturnAccountingGroupId IS NULL AND (r.AccountingAllocationVersion=1 OR
                EXISTS(SELECT 1 FROM dbo.InventorySupplierReturnAccountingGroups g WHERE g.TenantId=i.TenantId AND g.InventoryPurchaseReturnId=r.Id)))
              THROW 52863,'RTV_ALLOCATED_RETURN_REQUIRES_GROUP_CREDIT',1;
            IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE d.InventorySupplierReturnAccountingGroupId IS NOT NULL AND
              (i.Id IS NULL OR i.InventorySupplierReturnAccountingGroupId IS NULL OR i.InventorySupplierReturnAccountingGroupId<>d.InventorySupplierReturnAccountingGroupId OR i.IsDeleted<>d.IsDeleted))
              THROW 52854,'RTV_CREDIT_GROUP_IMMUTABLE',1;
            IF EXISTS(SELECT 1 FROM inserted i
              LEFT JOIN dbo.InventorySupplierReturnAccountingGroups g ON g.Id=i.InventorySupplierReturnAccountingGroupId AND g.TenantId=i.TenantId
              LEFT JOIN dbo.PurchaseReturns r ON r.Id=g.InventoryPurchaseReturnId AND r.TenantId=i.TenantId
              WHERE i.InventorySupplierReturnAccountingGroupId IS NOT NULL AND (i.IsDeleted=1 OR g.Id IS NULL OR g.IsDeleted=1
                OR r.Id IS NULL OR r.IsDeleted=1 OR r.AccountingAllocationVersion<>1 OR r.Status NOT IN('Shipped','Acknowledged')
                OR i.InventoryPurchaseReturnId<>r.Id OR i.InventoryPurchaseReturnId IS NULL OR i.OriginalVendorInvoiceId IS NULL
                OR i.OriginalVendorInvoiceId<>g.OriginalVendorInvoiceId OR g.OriginalVendorInvoiceId IS NULL OR i.VendorId<>r.SupplierId
                OR i.SupplierReturnId IS NOT NULL))
              THROW 52855,'RTV_CREDIT_GROUP_SOURCE_INVALID',1;
            IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.InventorySupplierReturnAccountingGroups g ON g.Id=i.InventorySupplierReturnAccountingGroupId
              LEFT JOIN dbo.FinancePostingEvents e ON e.Id=i.PostingEventId AND e.TenantId=i.TenantId
              LEFT JOIN dbo.JournalEntries j ON j.Id=i.JournalEntryId AND j.TenantId=i.TenantId
              WHERE i.DirectInvoiceAppliedAmount>0 AND (e.Id IS NULL OR e.IsDeleted=1 OR e.PostingStatus<>'Posted'
                OR e.SourceDocumentType<>'SupplierDebitNote' OR e.SourceDocumentId<>i.Id OR e.JournalEntryId<>j.Id
                OR j.Id IS NULL OR j.IsDeleted=1 OR j.IsReversed=1 OR j.PostingStatus<>'Posted'
                OR i.ReturnDispatchPostingEventId<>g.DispatchPostingEventId OR i.ReturnDispatchJournalEntryId<>g.DispatchJournalEntryId
                OR i.TotalAmount<>COALESCE((SELECT SUM(l.LineTotal) FROM dbo.SupplierDebitNoteLineItems l WHERE l.TenantId=i.TenantId AND l.SupplierDebitNoteId=i.Id AND l.IsDeleted=0),0)
                OR EXISTS(SELECT a.OriginalVendorInvoiceLineItemId FROM dbo.InventorySupplierReturnAllocations a
                    WHERE a.TenantId=i.TenantId AND a.AccountingGroupId=g.Id GROUP BY a.OriginalVendorInvoiceLineItemId
                    HAVING SUM(a.PurchaseQuantity)<>COALESCE((SELECT SUM(l.Quantity) FROM dbo.SupplierDebitNoteLineItems l WHERE l.TenantId=i.TenantId
                        AND l.SupplierDebitNoteId=i.Id AND l.IsDeleted=0 AND l.OriginalVendorInvoiceLineItemId=a.OriginalVendorInvoiceLineItemId),0))))
              THROW 52856,'RTV_CREDIT_GROUP_POSTING_INVALID',1;
        END;
        """;

    internal const string CreditLineGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_SupplierDebitNoteLines_ReturnAllocationGuard
        ON dbo.SupplierDebitNoteLineItems AFTER INSERT, UPDATE, DELETE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS(SELECT 1 FROM deleted d JOIN dbo.SupplierDebitNotes n ON n.Id=d.SupplierDebitNoteId AND n.TenantId=d.TenantId
              LEFT JOIN inserted i ON i.Id=d.Id WHERE n.InventorySupplierReturnAccountingGroupId IS NOT NULL AND
               (i.Id IS NULL OR i.IsDeleted<>d.IsDeleted OR i.SupplierDebitNoteId<>d.SupplierDebitNoteId OR i.TenantId<>d.TenantId
                OR i.Quantity<>d.Quantity OR i.UnitPrice<>d.UnitPrice OR i.LineTotal<>d.LineTotal OR i.TaxAmount<>d.TaxAmount
                OR i.DiscountAmount<>d.DiscountAmount OR i.DiscountPercentage<>d.DiscountPercentage OR i.TaxRate<>d.TaxRate OR i.Description<>d.Description
                OR ISNULL(i.OriginalAccountTransactionId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.OriginalAccountTransactionId,'00000000-0000-0000-0000-000000000000')
                OR ISNULL(i.ResolvedCreditAccountId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.ResolvedCreditAccountId,'00000000-0000-0000-0000-000000000000')
                OR ISNULL(i.GLAccountId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.GLAccountId,'00000000-0000-0000-0000-000000000000')
                OR i.OriginalVendorInvoiceLineItemId IS NULL OR i.OriginalVendorInvoiceLineItemId<>d.OriginalVendorInvoiceLineItemId))
              THROW 52857,'RTV_CREDIT_GROUP_LINES_IMMUTABLE',1;
            IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.SupplierDebitNotes n ON n.Id=i.SupplierDebitNoteId AND n.TenantId=i.TenantId
              WHERE n.InventorySupplierReturnAccountingGroupId IS NOT NULL AND (i.IsDeleted=1 OR i.OriginalVendorInvoiceLineItemId IS NULL OR
                i.Quantity<>COALESCE((SELECT SUM(a.PurchaseQuantity) FROM dbo.InventorySupplierReturnAllocations a WHERE a.TenantId=i.TenantId
                    AND a.AccountingGroupId=n.InventorySupplierReturnAccountingGroupId AND a.OriginalVendorInvoiceLineItemId=i.OriginalVendorInvoiceLineItemId),0)))
              THROW 52858,'RTV_CREDIT_GROUP_LINE_SOURCE_INVALID',1;
        END;
        """;

    internal const string LegacyGuardPatch = """
        DECLARE @sql nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_SupplierDebitNotes_InventoryReturnCreditGuard'));
        DECLARE @old1 nvarchar(400)=N'WHERE i.InventoryPurchaseReturnId IS NOT NULL AND (r.Id IS NULL';
        DECLARE @old2 nvarchar(400)=N'WHERE i.DirectInvoiceAppliedAmount>0 AND (p.Id IS NULL';
        IF @sql IS NULL OR (DATALENGTH(@sql)-DATALENGTH(REPLACE(@sql,@old1,N'')))/DATALENGTH(@old1)<>1
           OR (DATALENGTH(@sql)-DATALENGTH(REPLACE(@sql,@old2,N'')))/DATALENGTH(@old2)<>1
            THROW 52859,'RTV_LEGACY_GUARD_REVIEW_REQUIRED',1;
        SET @sql=REPLACE(@sql,@old1,N'WHERE i.InventorySupplierReturnAccountingGroupId IS NULL AND i.InventoryPurchaseReturnId IS NOT NULL AND (r.Id IS NULL');
        SET @sql=REPLACE(@sql,@old2,N'WHERE i.InventorySupplierReturnAccountingGroupId IS NULL AND i.DirectInvoiceAppliedAmount>0 AND (p.Id IS NULL');
        SET @sql=STUFF(@sql,1,CHARINDEX(N'TRIGGER',@sql)+LEN(N'TRIGGER')-1,N'CREATE OR ALTER TRIGGER');
        EXEC sys.sp_executesql @sql;
        """;
}
