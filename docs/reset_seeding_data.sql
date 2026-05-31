-- =========================================================================================
-- TDC ERP Data Reset Script
-- Purpose: Safely clear targeted data from the database to allow clean re-seeding
--          of Finance, Sales, AP/AR, and Unit Account structures without dropping tables.
-- Note: This script disables foreign key constraints temporarily to allow deletion.
-- =========================================================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

PRINT 'Disabling referential integrity constraints...'
EXEC sp_MSforeachtable "ALTER TABLE ? NOCHECK CONSTRAINT all"

BEGIN TRANSACTION;
BEGIN TRY

    PRINT 'Clearing Transactional Data...'
    IF OBJECT_ID('SubledgerJournalGlLinks', 'U') IS NOT NULL DELETE FROM [SubledgerJournalGlLinks];
    IF OBJECT_ID('SubledgerJournalLines', 'U') IS NOT NULL DELETE FROM [SubledgerJournalLines];
    IF OBJECT_ID('SubledgerJournalEntries', 'U') IS NOT NULL DELETE FROM [SubledgerJournalEntries];
    IF OBJECT_ID('AccountTransactions', 'U') IS NOT NULL DELETE FROM [AccountTransactions];
    IF OBJECT_ID('JournalEntryAttachments', 'U') IS NOT NULL DELETE FROM [JournalEntryAttachments];
    IF OBJECT_ID('JournalEntryLines', 'U') IS NOT NULL DELETE FROM [JournalEntryLines];
    IF OBJECT_ID('JournalEntries', 'U') IS NOT NULL DELETE FROM [JournalEntries];
    IF OBJECT_ID('Payments', 'U') IS NOT NULL DELETE FROM [Payments];
    IF OBJECT_ID('InvoiceLineItems', 'U') IS NOT NULL DELETE FROM [InvoiceLineItems];
    IF OBJECT_ID('Invoices', 'U') IS NOT NULL DELETE FROM [Invoices];

    -- AR Returns, Credit Notes & Refunds
    IF OBJECT_ID('Refunds', 'U') IS NOT NULL DELETE FROM [Refunds];
    IF OBJECT_ID('CreditNoteApplications', 'U') IS NOT NULL DELETE FROM [CreditNoteApplications];
    IF OBJECT_ID('CreditNoteLines', 'U') IS NOT NULL DELETE FROM [CreditNoteLines];
    IF OBJECT_ID('CreditNotes', 'U') IS NOT NULL DELETE FROM [CreditNotes];
    IF OBJECT_ID('ReturnOrderLines', 'U') IS NOT NULL DELETE FROM [ReturnOrderLines];
    IF OBJECT_ID('ReturnOrders', 'U') IS NOT NULL DELETE FROM [ReturnOrders];

    -- GL Budgets, Bank Accounts & Assets/Contracts
    IF OBJECT_ID('BudgetReturns', 'U') IS NOT NULL DELETE FROM [BudgetReturns];
    IF OBJECT_ID('BudgetEntries', 'U') IS NOT NULL DELETE FROM [BudgetEntries];
    IF OBJECT_ID('BudgetScenarios', 'U') IS NOT NULL DELETE FROM [BudgetScenarios];
    IF OBJECT_ID('BankAccounts', 'U') IS NOT NULL DELETE FROM [BankAccounts];
    IF OBJECT_ID('LeaseScheduleLines', 'U') IS NOT NULL DELETE FROM [LeaseScheduleLines];
    IF OBJECT_ID('LeaseContracts', 'U') IS NOT NULL DELETE FROM [LeaseContracts];
    IF OBJECT_ID('ProjectSettlementRules', 'U') IS NOT NULL DELETE FROM [ProjectSettlementRules];
    IF OBJECT_ID('ProjectCostLines', 'U') IS NOT NULL DELETE FROM [ProjectCostLines];
    IF OBJECT_ID('CapitalProjects', 'U') IS NOT NULL DELETE FROM [CapitalProjects];

    IF OBJECT_ID('VendorPayments', 'U') IS NOT NULL DELETE FROM [VendorPayments];
    IF OBJECT_ID('VendorInvoiceLineItem', 'U') IS NOT NULL DELETE FROM [VendorInvoiceLineItem];
    IF OBJECT_ID('VendorInvoice', 'U') IS NOT NULL DELETE FROM [VendorInvoice];

    -- AP Supplier Returns, Debit Notes & Finance Purchase Orders
    IF OBJECT_ID('SupplierDebitNoteLineItems', 'U') IS NOT NULL DELETE FROM [SupplierDebitNoteLineItems];
    IF OBJECT_ID('SupplierDebitNotes', 'U') IS NOT NULL DELETE FROM [SupplierDebitNotes];
    IF OBJECT_ID('SupplierReturnLineItems', 'U') IS NOT NULL DELETE FROM [SupplierReturnLineItems];
    IF OBJECT_ID('SupplierReturns', 'U') IS NOT NULL DELETE FROM [SupplierReturns];
    IF OBJECT_ID('FinancePurchaseOrderReceiptItems', 'U') IS NOT NULL DELETE FROM [FinancePurchaseOrderReceiptItems];
    IF OBJECT_ID('FinancePurchaseOrderReceipts', 'U') IS NOT NULL DELETE FROM [FinancePurchaseOrderReceipts];
    IF OBJECT_ID('FinancePurchaseOrderItems', 'U') IS NOT NULL DELETE FROM [FinancePurchaseOrderItems];
    IF OBJECT_ID('FinancePurchaseOrders', 'U') IS NOT NULL DELETE FROM [FinancePurchaseOrders];

    IF OBJECT_ID('GoodsReceivedVoucherLineItems', 'U') IS NOT NULL DELETE FROM [GoodsReceivedVoucherLineItems];
    IF OBJECT_ID('GoodsReceivedVouchers', 'U') IS NOT NULL DELETE FROM [GoodsReceivedVouchers];
    IF OBJECT_ID('PurchaseOrderReceiptItems', 'U') IS NOT NULL DELETE FROM [PurchaseOrderReceiptItems];
    IF OBJECT_ID('PurchaseOrderReceipts', 'U') IS NOT NULL DELETE FROM [PurchaseOrderReceipts];
    IF OBJECT_ID('PurchaseOrderItems', 'U') IS NOT NULL DELETE FROM [PurchaseOrderItems];
    IF OBJECT_ID('PurchaseOrders', 'U') IS NOT NULL DELETE FROM [PurchaseOrders];
    IF OBJECT_ID('DeliveryNoteLines', 'U') IS NOT NULL DELETE FROM [DeliveryNoteLines];
    IF OBJECT_ID('DeliveryNotes', 'U') IS NOT NULL DELETE FROM [DeliveryNotes];
    IF OBJECT_ID('SalesOrderLines', 'U') IS NOT NULL DELETE FROM [SalesOrderLines];
    IF OBJECT_ID('SalesOrders', 'U') IS NOT NULL DELETE FROM [SalesOrders];
    IF OBJECT_ID('QuoteLineItems', 'U') IS NOT NULL DELETE FROM [QuoteLineItems];
    IF OBJECT_ID('Quotes', 'U') IS NOT NULL DELETE FROM [Quotes];
    IF OBJECT_ID('Opportunities', 'U') IS NOT NULL DELETE FROM [Opportunities];
    IF OBJECT_ID('Leads', 'U') IS NOT NULL DELETE FROM [Leads];

    PRINT 'Clearing Sales and Product Catalog Data...'
    IF OBJECT_ID('SalesProducts', 'U') IS NOT NULL DELETE FROM [SalesProducts];

    PRINT 'Clearing Accounts Payable and Accounts Receivable Data...'
    IF OBJECT_ID('BusinessPartners', 'U') IS NOT NULL DELETE FROM [BusinessPartners];

    PRINT 'Clearing Fixed Assets Data...'
    IF OBJECT_ID('AssetDepreciationSchedules', 'U') IS NOT NULL DELETE FROM [AssetDepreciationSchedules];
    IF OBJECT_ID('AssetVerificationItems', 'U') IS NOT NULL DELETE FROM [AssetVerificationItems];
    IF OBJECT_ID('AssetVerificationSessions', 'U') IS NOT NULL DELETE FROM [AssetVerificationSessions];
    IF OBJECT_ID('AssetValuations', 'U') IS NOT NULL DELETE FROM [AssetValuations];
    IF OBJECT_ID('AssetTransfers', 'U') IS NOT NULL DELETE FROM [AssetTransfers];
    IF OBJECT_ID('AssetTransactions', 'U') IS NOT NULL DELETE FROM [AssetTransactions];
    IF OBJECT_ID('AssetDisposals', 'U') IS NOT NULL DELETE FROM [AssetDisposals];
    IF OBJECT_ID('FixedAssetBooks', 'U') IS NOT NULL DELETE FROM [FixedAssetBooks];
    IF OBJECT_ID('FixedAssets', 'U') IS NOT NULL DELETE FROM [FixedAssets];
    IF OBJECT_ID('FixedAssetCategories', 'U') IS NOT NULL DELETE FROM [FixedAssetCategories];

    PRINT 'Clearing Unit / Statistical Accounts Data...'
    IF OBJECT_ID('UnitJournalEntryLine', 'U') IS NOT NULL DELETE FROM [UnitJournalEntryLine];
    IF OBJECT_ID('UnitJournalEntry', 'U') IS NOT NULL DELETE FROM [UnitJournalEntry];
    IF OBJECT_ID('UnitAccountBudgets', 'U') IS NOT NULL DELETE FROM [UnitAccountBudgets];
    IF OBJECT_ID('UnitAccountBalances', 'U') IS NOT NULL DELETE FROM [UnitAccountBalances];
    IF OBJECT_ID('UnitAccounts', 'U') IS NOT NULL DELETE FROM [UnitAccounts];
    IF OBJECT_ID('UnitTypes', 'U') IS NOT NULL DELETE FROM [UnitTypes];

    PRINT 'Clearing Tax Configurations...'
    IF OBJECT_ID('TaxRules', 'U') IS NOT NULL DELETE FROM [TaxRules];
    IF OBJECT_ID('TaxGroupComponents', 'U') IS NOT NULL DELETE FROM [TaxGroupComponents];
    IF OBJECT_ID('TaxRates', 'U') IS NOT NULL DELETE FROM [TaxRates];
    IF OBJECT_ID('Taxes', 'U') IS NOT NULL DELETE FROM [Taxes];
    IF OBJECT_ID('TaxGroups', 'U') IS NOT NULL DELETE FROM [TaxGroups];

    PRINT 'Clearing General Ledger and Segment Data...'
    IF OBJECT_ID('UserGLSegmentAccesses', 'U') IS NOT NULL DELETE FROM [UserGLSegmentAccesses];
    IF OBJECT_ID('AccountSegmentValues', 'U') IS NOT NULL DELETE FROM [AccountSegmentValues];
    IF OBJECT_ID('AccountCurrencyLinks', 'U') IS NOT NULL DELETE FROM [AccountCurrencyLinks];
    IF OBJECT_ID('Accounts', 'U') IS NOT NULL DELETE FROM [Accounts];
    IF OBJECT_ID('AccountSegments', 'U') IS NOT NULL DELETE FROM [AccountSegments];
    IF OBJECT_ID('SegmentLookupValues', 'U') IS NOT NULL DELETE FROM [SegmentLookupValues];
    IF OBJECT_ID('AccountSegmentStructures', 'U') IS NOT NULL DELETE FROM [AccountSegmentStructures];

    PRINT 'Clearing Fiscal Calendar Data...'
    IF OBJECT_ID('FiscalYearBookStatuses', 'U') IS NOT NULL DELETE FROM [FiscalYearBookStatuses];
    IF OBJECT_ID('FiscalPeriods', 'U') IS NOT NULL DELETE FROM [FiscalPeriods];
    IF OBJECT_ID('FiscalYears', 'U') IS NOT NULL DELETE FROM [FiscalYears];

    PRINT 'Clearing Multi-Currency and Settings Data...'
    IF OBJECT_ID('ExchangeRates', 'U') IS NOT NULL DELETE FROM [ExchangeRates];
    IF OBJECT_ID('Currencies', 'U') IS NOT NULL DELETE FROM [Currencies];
    IF OBJECT_ID('TransactionDocumentModuleMappings', 'U') IS NOT NULL DELETE FROM [TransactionDocumentModuleMappings];
    IF OBJECT_ID('FinanceSettings', 'U') IS NOT NULL DELETE FROM [FinanceSettings];

    COMMIT TRANSACTION;
    PRINT 'Targeted tables successfully cleared.'

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'An error occurred during deletion. Transaction rolled back.'
    PRINT ERROR_MESSAGE();
END CATCH

PRINT 'Re-enabling referential integrity constraints...'
EXEC sp_MSforeachtable "ALTER TABLE ? WITH CHECK CHECK CONSTRAINT all"

PRINT '========================================================================================='
PRINT 'RESET COMPLETE: You can now run the API to trigger the Seeders for the TDC Configuration.'
PRINT '========================================================================================='
