-- Shared fresh-install and populated-upgrade checks. No data is changed.
DECLARE @tables TABLE(Name sysname PRIMARY KEY);
INSERT @tables VALUES
('InventoryDisposalAuctionInvoices'),('InventorySupplierReturnAccrualShares'),
('InventoryTransferReceiptAllocations'),('PhysicalCountAdjustmentClaims'),
('PhysicalCountCounters'),('VendorInvoiceReceiptCostPostingLines'),
('VendorInvoiceReceiptCostValuations'),('InventorySupplierReturnAllocations'),
('InventoryTransferDispatchAllocations'),('VendorInvoiceReceiptCostAllocations'),
('InventorySupplierReturnAccountingGroups'),('ProcurementReceiptCostBases');
IF EXISTS(SELECT 1 FROM @tables t WHERE OBJECT_ID('dbo.'+t.Name,'U') IS NULL)
 THROW 51031,'Inventory workflow evidence table is missing.',1;
IF EXISTS(SELECT 1 FROM @tables t WHERE NOT EXISTS(SELECT 1 FROM sys.foreign_keys f
 WHERE f.parent_object_id=OBJECT_ID('dbo.'+t.Name) AND f.is_disabled=0 AND f.is_not_trusted=0))
 THROW 51032,'Inventory workflow evidence foreign keys are missing or untrusted.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys f JOIN @tables t ON f.parent_object_id=OBJECT_ID('dbo.'+t.Name)
 WHERE f.is_disabled=1 OR f.is_not_trusted=1)
 THROW 51033,'Inventory workflow evidence has disabled or untrusted foreign keys.',1;
DECLARE @triggers TABLE(Name sysname PRIMARY KEY);
INSERT @triggers VALUES
('TR_InventoryTransferDispatchAllocations_Guard'),('TR_InventoryTransferReceiptAllocations_Guard'),
('TR_InventoryMovements_TransferLegGuard'),('TR_StockMovements_TransferLegGuard'),
('TR_WarehouseLocations_TransitIdentity'),('TR_Warehouses_TransitIdentity'),
('TR_PhysicalCountCounters_ControlledMutation'),('TR_PhysicalCounts_CommitteeActors'),
('TR_PhysicalCountItems_DefectiveObservation'),('TR_PhysicalCountItems_RecountLineage'),
('TR_PhysicalCountAdjustmentClaims_Immutable'),('TR_PhysicalCounts_RecountLineage'),
('TR_StockAdjustments_CountResolutionClaims'),('TR_ProcurementReceiptCostBases_Guard'),
('TR_VendorInvoiceReceiptCostAllocations_Guard'),('TR_VendorInvoiceReceiptCostPostingLines_Guard'),
('TR_VendorInvoiceReceiptCostValuations_Guard'),('TR_VendorInvoice_ReceiptCostSeal'),
('TR_InventorySupplierReturnAccountingGroups_Authority'),('TR_InventorySupplierReturnAllocations_Authority'),
('TR_InventorySupplierReturnAccrualShares_Authority'),('TR_PurchaseReturns_AccountingAllocationSeal'),
('TR_SupplierDebitNotes_ReturnAllocationGuard'),('TR_SupplierDebitNoteLines_ReturnAllocationGuard'),
('TR_InventoryDisposalCases_Guard'),('TR_InventoryDisposalAuctionInvoices_Guard'),
('TR_Invoices_DisposalEconomics'),('TR_InvoiceLineItem_DisposalEconomics'),
('TR_SalesOrders_InvoiceSource'),('TR_SalesOrderLines_InvoiceSource'),
('TR_Invoices_SalesSource'),('TR_InvoiceLineItem_SalesSource');
IF EXISTS(SELECT 1 FROM @triggers t WHERE NOT EXISTS(SELECT 1 FROM sys.triggers st
 WHERE st.name=t.Name AND st.is_disabled=0 AND st.parent_class=1))
 THROW 51034,'Inventory workflow SQL authority trigger is missing or disabled.',1;
DECLARE @precision TABLE(TableName sysname,ColumnName sysname,ExpectedPrecision int,ExpectedScale int);
INSERT @precision VALUES
('ProcurementReceiptCostBases','ConversionToBase',18,8),
('ProcurementReceiptCostBases','ExchangeRateToFunctional',18,6),
('ProcurementReceiptCostBases','PurchaseUnitCost',18,6),
('InventorySupplierReturnAllocations','ConversionToBase',18,8),
('VendorInvoiceReceiptCostAllocations','InvoiceExchangeRateToFunctional',18,6),
('VendorInvoiceReceiptCostAllocations','RevaluedReceiptBaseQuantity',28,12),
('VendorInvoiceReceiptCostValuations','AttributedReceiptBaseQuantity',28,12),
('VendorInvoiceReceiptCostValuations','ValueChange',18,2);
IF EXISTS(SELECT 1 FROM @precision p WHERE NOT EXISTS(SELECT 1 FROM sys.columns c
 WHERE c.object_id=OBJECT_ID('dbo.'+p.TableName) AND c.name=p.ColumnName
 AND c.precision=p.ExpectedPrecision AND c.scale=p.ExpectedScale))
 THROW 51035,'Immutable Inventory cost evidence precision differs from its source contract.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.SalesOrders')
 AND is_unique=1 AND has_filter=1 AND is_disabled=0 AND name LIKE '%InvoiceId%')
 THROW 51036,'Sales source invoice uniqueness is missing.',1;
