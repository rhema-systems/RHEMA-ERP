-- Read-only Up preconditions for Inventory migrations 61 and 62.
-- Only table variables are written. No history, business data or trigger is changed.
-- 61: nullable/default additions and new empty tables preserve existing rows. The
-- existing-row risks are unique Sales invoice links, receipt allocation uniqueness,
-- nonnegative counted quantity, the widened action constraint, and the exact legacy
-- trigger predicates patched by transfer, committee, recount and return installers.
-- Other THROWs in those installers govern future writes or Down, not existing data.
-- 62: nullable approver needs no data conversion; its Up patch requires each known
-- predicate exactly once. Direct-issue rollback guards apply only to Down.
SET NOCOUNT ON;
DECLARE @Checks TABLE (CheckName nvarchar(240) NOT NULL, AffectedRows bigint NOT NULL);
DECLARE @Applied TABLE (MigrationId nvarchar(150) NOT NULL PRIMARY KEY);
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    SELECT N'InventoryMigrationHistoryMissing' AS CheckName, CAST(1 AS bigint) AS AffectedRows;
    RETURN;
END;
INSERT @Applied EXEC sys.sp_executesql N'SELECT MigrationId FROM dbo.__EFMigrationsHistory;';
DECLARE @Pending61 bit=CASE WHEN EXISTS(SELECT 1 FROM @Applied WHERE MigrationId=N'20260927141036_InventoryControlledWorkflowsAndAccounting') THEN 0 ELSE 1 END;
DECLARE @Pending62 bit=CASE WHEN EXISTS(SELECT 1 FROM @Applied WHERE MigrationId=N'20260927211546_InventoryIssueOptionalWorkflowApproval') THEN 0 ELSE 1 END;

IF @Pending61=1
BEGIN
    DECLARE @Additions TABLE (TableName sysname, ColumnName sysname NULL);
    INSERT @Additions VALUES
    (N'SupplierDebitNotes',N'InventorySupplierReturnAccountingGroupId'),
    (N'StockMovements',N'TransferDispatchAllocationId'),
    (N'StockMovements',N'TransferLeg'),
    (N'StockMovements',N'TransferReceiptAllocationId'),
    (N'SalesOrders',N'InvoiceEconomicsJson'),
    (N'SalesOrders',N'InvoiceGeneratedById'),
    (N'SalesOrders',N'InvoiceGenerationHash'),
    (N'SalesOrders',N'InvoiceGenerationKey'),
    (N'SalesOrders',N'InvoiceSourceJson'),
    (N'SalesOrders',N'RowVersion'),
    (N'PurchaseReturns',N'AccountingAllocationVersion'),
    (N'ProcurementSettings',N'PurchasePriceDifferenceHandling'),
    (N'PhysicalCounts',N'ObservationSubmittedAtUtc'),
    (N'PhysicalCounts',N'ParentPhysicalCountId'),
    (N'PhysicalCounts',N'RecountAttempt'),
    (N'PhysicalCounts',N'RecountRequestHash'),
    (N'PhysicalCounts',N'RecountRequestKey'),
    (N'PhysicalCounts',N'RootPhysicalCountId'),
    (N'PhysicalCountItems',N'DefectiveNotes'),
    (N'PhysicalCountItems',N'DefectiveQuantity'),
    (N'PhysicalCountItems',N'PredecessorPhysicalCountItemId'),
    (N'PhysicalCountItems',N'RecountReason'),
    (N'PhysicalCountItems',N'RootPhysicalCountItemId'),
    (N'PhysicalCountItems',N'SupersededByPhysicalCountId'),
    (N'InventoryTransfers',N'CarrierBusinessPartnerId'),
    (N'InventoryMovements',N'TransferDispatchAllocationId'),
    (N'InventoryMovements',N'TransferLeg'),
    (N'InventoryMovements',N'TransferReceiptAllocationId'),
    (N'InventoryItems',N'InventoryDisposalAccountId'),
    (N'InventoryDisposalCases',N'AccountingVersion'),
    (N'InventoryDisposalCases',N'PreparedStockAdjustmentId'),
    (N'InventoryDisposalAuctionInvoices',NULL),
    (N'InventorySupplierReturnAccountingGroups',NULL),
    (N'InventoryTransferDispatchAllocations',NULL),
    (N'PhysicalCountAdjustmentClaims',NULL),
    (N'PhysicalCountCounters',NULL),
    (N'ProcurementReceiptCostBases',NULL),
    (N'InventoryTransferReceiptAllocations',NULL),
    (N'InventorySupplierReturnAllocations',NULL),
    (N'VendorInvoiceReceiptCostAllocations',NULL),
    (N'InventorySupplierReturnAccrualShares',NULL),
    (N'VendorInvoiceReceiptCostPostingLines',NULL),
    (N'VendorInvoiceReceiptCostValuations',NULL);
    INSERT @Checks
    SELECT N'Inventory61:UnexpectedPendingSchema:'+a.TableName+N'.'+COALESCE(a.ColumnName,N'<table>'),1
    FROM @Additions a
    WHERE (a.ColumnName IS NULL AND OBJECT_ID(N'dbo.'+QUOTENAME(a.TableName),N'U') IS NOT NULL)
       OR (a.ColumnName IS NOT NULL AND (OBJECT_ID(N'dbo.'+QUOTENAME(a.TableName),N'U') IS NULL
           OR COL_LENGTH(N'dbo.'+QUOTENAME(a.TableName),a.ColumnName) IS NOT NULL));

    IF COL_LENGTH(N'dbo.SalesOrders',N'InvoiceId') IS NULL OR COL_LENGTH(N'dbo.SalesOrders',N'TenantId') IS NULL
        INSERT @Checks VALUES(N'Inventory61:SalesOrderSchemaMissing',1);
    ELSE
        INSERT @Checks EXEC sys.sp_executesql N'SELECT N''Inventory61:DuplicateSalesInvoiceLinks'',COUNT_BIG(*)
            FROM (SELECT TenantId,InvoiceId FROM dbo.SalesOrders WHERE InvoiceId IS NOT NULL
                GROUP BY TenantId,InvoiceId HAVING COUNT_BIG(*)>1) duplicates;';
    IF OBJECT_ID(N'dbo.VendorInvoiceReceiptAllocations',N'U') IS NULL
        INSERT @Checks VALUES(N'Inventory61:ReceiptAllocationSchemaMissing',1);
    ELSE
        INSERT @Checks EXEC sys.sp_executesql N'SELECT N''Inventory61:DuplicateReceiptAllocations'',COUNT_BIG(*)
            FROM (SELECT TenantId,VendorInvoiceLineItemId,GoodsReceiptNoteItemId FROM dbo.VendorInvoiceReceiptAllocations
                GROUP BY TenantId,VendorInvoiceLineItemId,GoodsReceiptNoteItemId HAVING COUNT_BIG(*)>1) duplicates;';
    IF OBJECT_ID(N'dbo.PhysicalCountItems',N'U') IS NULL OR OBJECT_ID(N'dbo.PhysicalCountActions',N'U') IS NULL
        INSERT @Checks VALUES(N'Inventory61:CountSchemaMissing',1);
    ELSE
    BEGIN
        INSERT @Checks EXEC sys.sp_executesql N'SELECT N''Inventory61:NegativeCountedQuantity'',COUNT_BIG(*)
            FROM dbo.PhysicalCountItems WHERE CountedQuantity<0;
            SELECT N''Inventory61:InvalidCountActionType'',COUNT_BIG(*) FROM dbo.PhysicalCountActions WHERE ActionType NOT BETWEEN 1 AND 17;';
    END;
END;

-- Match every Up patch against the current definitions without executing DDL.
-- Mode 1 = exact occurrence count; mode 0 = required substring (migration CHARINDEX).
DECLARE @Predicates TABLE (Scope nvarchar(20), TriggerName sysname, Predicate nvarchar(1000), ExactOnce bit);
IF @Pending61=1
BEGIN
    INSERT @Predicates VALUES
    (N'Inventory61',N'TR_StockMovements_GovernedInventoryTransfer',N'WHERE i.MovementType IN (N''TransferOut'', N''TransferIn'', N''TransferReversal'', N''TransferDiscrepancyReturn'', N''TransferReplacementIn'')',1),
    (N'Inventory61',N'TR_PhysicalCountActions_AppendOnly',N'i.ActionType NOT BETWEEN 1 AND 16',0),
    (N'Inventory61',N'TR_PhysicalCountItems_ControlledMutation',N'i.CountedById <> p.CountedById',0),
    (N'Inventory61',N'TR_PhysicalCountItems_ControlledMutation',N'i.RecountedById IN (p.InitiatedById,p.CountedById)',0),
    (N'Inventory61',N'TR_PhysicalCounts_ControlledLifecycle',N'a.ActionType=14 AND a.ActorUserId=i.CountedById',0),
    (N'Inventory61',N'TR_PhysicalCounts_ControlledLifecycle',N'a.ActionType=16 AND a.ActorUserId=i.CountedById',0),
    (N'Inventory61',N'TR_PhysicalCounts_ControlledLifecycle',N'a.ActionType IN (5,7) AND a.ActorUserId=i.CountedById',0),
    (N'Inventory61',N'TR_SupplierDebitNotes_InventoryReturnCreditGuard',N'WHERE i.InventoryPurchaseReturnId IS NOT NULL AND (r.Id IS NULL',1),
    (N'Inventory61',N'TR_SupplierDebitNotes_InventoryReturnCreditGuard',N'WHERE i.DirectInvoiceAppliedAmount>0 AND (p.Id IS NULL',1);
    DECLARE @Freeze nvarchar(1000)=N'p.Status IN (N''InProgress'',N''UnderReview'',N''UnderInvestigation'',N''RecountRequired'',N''PendingStoresApproval'',N''PendingFinanceApproval'',N''PendingAuditAttestation'',N''ReadyToPost'')';
    INSERT @Predicates VALUES
    (N'Inventory61',N'TR_WarehouseQuantities_PhysicalCountFreeze',@Freeze,0),
    (N'Inventory61',N'TR_InventoryItems_PhysicalCountFreeze',@Freeze,0),
    (N'Inventory61',N'TR_StockMovements_PhysicalCountFreeze',@Freeze,0);
END;
IF @Pending62=1
BEGIN
    IF COL_LENGTH(N'dbo.InventoryIssueVouchers',N'ApprovedById') IS NULL
        INSERT @Checks VALUES(N'Inventory62:ApproverColumnMissing',1);
    ELSE IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.InventoryIssueVouchers') AND name=N'ApprovedById' AND is_nullable=1)
        INSERT @Checks VALUES(N'Inventory62:UnexpectedNullableApprover',1);
    INSERT @Predicates VALUES
    (N'Inventory62',N'TR_InventoryIssueVouchers_ControlledLifecycle',N'i.ApprovedById <> d.ApprovedById',1),
    (N'Inventory62',N'TR_InventoryIssueVouchers_ControlledLifecycle',N'i.ApprovedById = d.ApprovedById',1),
    (N'Inventory62',N'TR_InventoryIssueVouchers_ControlledLifecycle',N'r.ApprovedById <> i.ApprovedById',1),
    (N'Inventory62',N'TR_InventoryIssueVouchers_ControlledLifecycle',N'VALUES (i.RequestedById),(i.ApprovedById),(i.IssuedById),(i.ReceiverUserId)',1),
    (N'Inventory62',N'TR_InventoryIssueVouchers_ControlledLifecycle',N'SET NOCOUNT ON;',1);
END;
INSERT @Checks
SELECT DISTINCT p.Scope+N':TriggerPredicate:'+p.TriggerName,1
FROM @Predicates p
CROSS APPLY(SELECT REPLACE(OBJECT_DEFINITION(OBJECT_ID(N'dbo.'+QUOTENAME(p.TriggerName),N'TR')),CHAR(13),N'') AS Definition) d
WHERE d.Definition IS NULL OR CHARINDEX(p.Predicate,d.Definition)=0
   OR (p.ExactOnce=1 AND (DATALENGTH(d.Definition)-DATALENGTH(REPLACE(d.Definition,p.Predicate,N'')))/DATALENGTH(p.Predicate)<>1)
   OR CHARINDEX(N'TRIGGER',UPPER(d.Definition))=0;

SELECT CheckName,AffectedRows FROM @Checks WHERE AffectedRows>0 ORDER BY CheckName;
