-- =========================================================================================
-- Finance Transaction Reset - Default Tenant Only
-- Purpose:
--   Delete finance/AP/AR source documents, GL postings, cash/bank transaction rows,
--   budget rows, runtime notifications, fixed-asset transaction rows, lease/AUC finance
--   documents, and reset finance document numbering for the default tenant.
--
-- Preserves master/config data:
--   Accounts, account segments, fiscal years/periods, finance settings, currencies,
--   tax setup, business partners, customers, bank accounts, fixed assets, fixed asset
--   categories, payment methods, notification topics/templates/recipients, unit accounts,
--   and unit types.
--   Transaction-derived cached balances on preserved master rows are reset.
--
-- Safety:
--   @DryRun = 1 previews and rolls back.
--   Set @DryRun = 0 only after reviewing the preview counts.
--
-- Caveat:
--   Cash/bank statement/reconciliation tables are not fully tenant-scoped in the model.
--   This script resets those rows only when their bank account is linked to a default
--   tenant GL account. Set @DeleteUnscopedCashBankRows = 1 only in a single-tenant DB.
-- =========================================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000001';
DECLARE @DryRun bit = 1;
DECLARE @DeleteUnscopedCashBankRows bit = 0;

DECLARE @HadError bit = 0;
DECLARE @ErrorMessage nvarchar(4000) = NULL;

IF OBJECT_ID('dbo.Tenants', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.Tenants WHERE Id = @TenantId)
BEGIN
    THROW 51000, 'Default tenant was not found. Check @TenantId before running reset.', 1;
END;

PRINT 'Disabling referential integrity constraints...';
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT 'Collecting default-tenant bank accounts tied to default-tenant GL accounts...';
    IF OBJECT_ID('tempdb..#TenantBankAccounts') IS NOT NULL DROP TABLE #TenantBankAccounts;
    CREATE TABLE #TenantBankAccounts (Id uniqueidentifier NOT NULL PRIMARY KEY);

    IF OBJECT_ID('dbo.BankAccounts', 'U') IS NOT NULL
       AND OBJECT_ID('dbo.Accounts', 'U') IS NOT NULL
       AND COL_LENGTH('dbo.BankAccounts', 'GLAccountId') IS NOT NULL
       AND COL_LENGTH('dbo.Accounts', 'TenantId') IS NOT NULL
    BEGIN
        INSERT INTO #TenantBankAccounts (Id)
        SELECT DISTINCT ba.Id
        FROM dbo.BankAccounts ba
        JOIN dbo.Accounts a ON a.Id = ba.GLAccountId
        WHERE a.TenantId = @TenantId;
    END;

    IF @DeleteUnscopedCashBankRows = 1
       AND OBJECT_ID('dbo.BankAccounts', 'U') IS NOT NULL
    BEGIN
        INSERT INTO #TenantBankAccounts (Id)
        SELECT ba.Id
        FROM dbo.BankAccounts ba
        WHERE NOT EXISTS (SELECT 1 FROM #TenantBankAccounts tba WHERE tba.Id = ba.Id);
    END;

    PRINT 'Preview counts before reset...';
    IF OBJECT_ID('tempdb..#PreviewCounts') IS NOT NULL DROP TABLE #PreviewCounts;
    CREATE TABLE #PreviewCounts (TableName nvarchar(200) NOT NULL, RowsToDelete int NOT NULL);

    DECLARE @PreviewTable sysname;
    DECLARE @PreviewSql nvarchar(max);
    DECLARE @PreviewTenantTables TABLE (TableName sysname NOT NULL);
    INSERT INTO @PreviewTenantTables (TableName) VALUES
        ('JournalEntries'),
        ('AccountTransactions'),
        ('FinancePurchaseOrders'),
        ('VendorInvoice'),
        ('Invoices'),
        ('CustomerPayment'),
        ('CustomerPayments'),
        ('VendorPayment'),
        ('SupplierReturns'),
        ('SupplierDebitNotes'),
        ('BudgetEntries'),
        ('BudgetScenarios'),
        ('TaxCalculations'),
        ('AccountBalances'),
        ('Notifications');

    DECLARE preview_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT TableName FROM @PreviewTenantTables;

    OPEN preview_cursor;
    FETCH NEXT FROM preview_cursor INTO @PreviewTable;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF OBJECT_ID(N'dbo.' + @PreviewTable, 'U') IS NOT NULL
           AND COL_LENGTH(N'dbo.' + @PreviewTable, 'TenantId') IS NOT NULL
        BEGIN
            SET @PreviewSql = N'
                INSERT INTO #PreviewCounts (TableName, RowsToDelete)
                SELECT @TableName, COUNT(*)
                FROM dbo.' + QUOTENAME(@PreviewTable) + N'
                WHERE TenantId = @TenantId;';
            EXEC sp_executesql @PreviewSql,
                N'@TenantId uniqueidentifier, @TableName nvarchar(200)',
                @TenantId,
                @PreviewTable;
        END;

        FETCH NEXT FROM preview_cursor INTO @PreviewTable;
    END;

    CLOSE preview_cursor;
    DEALLOCATE preview_cursor;

    IF OBJECT_ID('dbo.CashTransaction', 'U') IS NOT NULL
       AND COL_LENGTH('dbo.CashTransaction', 'BankAccountId') IS NOT NULL
    BEGIN
        INSERT INTO #PreviewCounts (TableName, RowsToDelete)
        SELECT 'CashTransaction linked to tenant bank accounts', COUNT(*)
        FROM dbo.CashTransaction ct
        WHERE EXISTS (SELECT 1 FROM #TenantBankAccounts tba WHERE tba.Id IN (ct.BankAccountId, ct.ToBankAccountId));
    END;

    SELECT TableName, RowsToDelete
    FROM #PreviewCounts
    ORDER BY TableName;

    PRINT 'Resetting document numbering reservations and sequence counters...';
    IF OBJECT_ID('dbo.DocumentNumberReservations', 'U') IS NOT NULL
       AND OBJECT_ID('dbo.DocumentSequenceDefinitions', 'U') IS NOT NULL
    BEGIN
        DELETE r
        FROM dbo.DocumentNumberReservations r
        JOIN dbo.DocumentSequenceDefinitions d ON d.Id = r.DocumentSequenceDefinitionId
        WHERE r.TenantId = @TenantId
          AND (
                d.Module = 'Finance'
                OR (d.Module = 'Sales' AND d.DocumentType IN ('ReturnOrder', 'CreditNote', 'Refund'))
              );

        UPDATE d
        SET NextNumber = StartNumber,
            LastResetPeriodKey = NULL,
            UpdatedAt = SYSUTCDATETIME()
        FROM dbo.DocumentSequenceDefinitions d
        WHERE d.TenantId = @TenantId
          AND (
                d.Module = 'Finance'
                OR (d.Module = 'Sales' AND d.DocumentType IN ('ReturnOrder', 'CreditNote', 'Refund'))
              );
    END;

    PRINT 'Clearing links from preserved non-finance records to finance transactions...';
    IF OBJECT_ID('dbo.Invoices', 'U') IS NOT NULL
    BEGIN
        IF OBJECT_ID('dbo.Quotes', 'U') IS NOT NULL
           AND COL_LENGTH('dbo.Quotes', 'ConvertedInvoiceId') IS NOT NULL
            UPDATE q
            SET ConvertedInvoiceId = NULL
            FROM dbo.Quotes q
            WHERE EXISTS (SELECT 1 FROM dbo.Invoices i WHERE i.Id = q.ConvertedInvoiceId AND i.TenantId = @TenantId);

        IF OBJECT_ID('dbo.SalesOrders', 'U') IS NOT NULL
           AND COL_LENGTH('dbo.SalesOrders', 'InvoiceId') IS NOT NULL
            UPDATE so
            SET InvoiceId = NULL
            FROM dbo.SalesOrders so
            WHERE EXISTS (SELECT 1 FROM dbo.Invoices i WHERE i.Id = so.InvoiceId AND i.TenantId = @TenantId);

        IF OBJECT_ID('dbo.SalesAgreementMilestones', 'U') IS NOT NULL
           AND COL_LENGTH('dbo.SalesAgreementMilestones', 'InvoiceId') IS NOT NULL
            UPDATE sam
            SET InvoiceId = NULL,
                InvoiceNumber = NULL
            FROM dbo.SalesAgreementMilestones sam
            WHERE EXISTS (SELECT 1 FROM dbo.Invoices i WHERE i.Id = sam.InvoiceId AND i.TenantId = @TenantId);

        IF OBJECT_ID('dbo.CollectionActivities', 'U') IS NOT NULL
           AND COL_LENGTH('dbo.CollectionActivities', 'InvoiceId') IS NOT NULL
            UPDATE ca
            SET InvoiceId = NULL
            FROM dbo.CollectionActivities ca
            WHERE EXISTS (SELECT 1 FROM dbo.Invoices i WHERE i.Id = ca.InvoiceId AND i.TenantId = @TenantId);
    END;

    IF OBJECT_ID('dbo.JournalEntries', 'U') IS NOT NULL
    BEGIN
        IF OBJECT_ID('dbo.PayrollJournalLines', 'U') IS NOT NULL
           AND COL_LENGTH('dbo.PayrollJournalLines', 'JournalEntryId') IS NOT NULL
            UPDATE pjl
            SET JournalEntryId = NULL,
                Posted = 0
            FROM dbo.PayrollJournalLines pjl
            WHERE EXISTS (SELECT 1 FROM dbo.JournalEntries je WHERE je.Id = pjl.JournalEntryId AND je.TenantId = @TenantId);
    END;

    PRINT 'Clearing legacy child rows without direct tenant scoping...';
    IF OBJECT_ID('dbo.Payments', 'U') IS NOT NULL
       AND OBJECT_ID('dbo.Invoices', 'U') IS NOT NULL
       AND COL_LENGTH('dbo.Payments', 'InvoiceId') IS NOT NULL
    BEGIN
        DELETE p
        FROM dbo.Payments p
        WHERE EXISTS (SELECT 1 FROM dbo.Invoices i WHERE i.Id = p.InvoiceId AND i.TenantId = @TenantId);
    END;

    PRINT 'Clearing tenant-scoped finance transaction tables...';

    DECLARE @DirectTenantTables TABLE (Ordinal int NOT NULL, TableName sysname NOT NULL);
    INSERT INTO @DirectTenantTables (Ordinal, TableName) VALUES
        (1, 'Notifications'),
        (5, 'TaxCalculations'),
        (6, 'TaxThresholds'),
        (10, 'CreditNoteApplications'),
        (20, 'PaymentAllocation'),
        (30, 'CustomerPayment'),
        (31, 'CustomerPayments'),
        (40, 'InvoiceLineItem'),
        (50, 'InvoiceLineItems'),
        (60, 'Invoices'),
        (70, 'Refunds'),
        (80, 'CreditNoteLines'),
        (90, 'CreditNotes'),
        (100, 'ReturnOrderLines'),
        (110, 'ReturnOrders'),
        (115, 'CollectionActivities'),
        (116, 'PaymentPlanInstallments'),
        (117, 'PaymentPlans'),
        (120, 'PaymentBatchItem'),
        (130, 'VendorPaymentAllocation'),
        (140, 'VendorPayment'),
        (150, 'PaymentBatch'),
        (160, 'VendorInvoiceLineItem'),
        (170, 'VendorInvoice'),
        (180, 'SupplierDebitNoteLineItems'),
        (190, 'SupplierDebitNotes'),
        (200, 'SupplierReturnLineItems'),
        (210, 'SupplierReturns'),
        (220, 'FinancePurchaseOrderReceiptItems'),
        (230, 'FinancePurchaseOrderReceipts'),
        (240, 'FinancePurchaseOrderItems'),
        (250, 'FinancePurchaseOrders'),
        (260, 'Payments'),
        (300, 'BudgetEntries'),
        (310, 'BudgetReturns'),
        (311, 'BudgetScenarios'),
        (320, 'UnitJournalEntryLine'),
        (330, 'UnitJournalEntry'),
        (340, 'UnitAccountBudgets'),
        (350, 'UnitAccountBalances'),
        (400, 'AssetVerificationItems'),
        (410, 'AssetVerificationSessions'),
        (420, 'AssetDepreciationSchedules'),
        (430, 'AssetValuations'),
        (440, 'AssetTransfers'),
        (450, 'AssetTransactions'),
        (460, 'AssetDisposals'),
        (600, 'SubledgerJournalGlLinks'),
        (610, 'SubledgerJournalLines'),
        (620, 'SubledgerJournalEntries'),
        (690, 'AccountBalances'),
        (700, 'AccountTransactions'),
        (710, 'JournalEntryAttachments'),
        (720, 'JournalEntryLines'),
        (730, 'JournalEntries');

    DECLARE @TableName sysname;
    DECLARE @Sql nvarchar(max);

    DECLARE table_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT TableName
        FROM @DirectTenantTables
        ORDER BY Ordinal;

    OPEN table_cursor;
    FETCH NEXT FROM table_cursor INTO @TableName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF OBJECT_ID(N'dbo.' + @TableName, 'U') IS NOT NULL
           AND COL_LENGTH(N'dbo.' + @TableName, 'TenantId') IS NOT NULL
        BEGIN
            SET @Sql = N'DELETE FROM dbo.' + QUOTENAME(@TableName) + N' WHERE TenantId = @TenantId;';
            EXEC sp_executesql @Sql, N'@TenantId uniqueidentifier', @TenantId;
            PRINT 'Deleted tenant rows from ' + @TableName;
        END
        ELSE IF OBJECT_ID(N'dbo.' + @TableName, 'U') IS NOT NULL
        BEGIN
            PRINT 'Skipped ' + @TableName + ' because it has no TenantId column and no custom tenant route in this script.';
        END;

        FETCH NEXT FROM table_cursor INTO @TableName;
    END;

    CLOSE table_cursor;
    DEALLOCATE table_cursor;

    PRINT 'Clearing fixed-asset transaction children that are scoped through FixedAssets...';
    IF OBJECT_ID('dbo.FixedAssets', 'U') IS NOT NULL
    BEGIN
        IF OBJECT_ID('dbo.AssetVerificationItems', 'U') IS NOT NULL
            DELETE avi FROM dbo.AssetVerificationItems avi WHERE EXISTS (SELECT 1 FROM dbo.FixedAssets fa WHERE fa.Id = avi.FixedAssetId AND fa.TenantId = @TenantId);
        IF OBJECT_ID('dbo.AssetDepreciationSchedules', 'U') IS NOT NULL
            DELETE ads FROM dbo.AssetDepreciationSchedules ads WHERE EXISTS (SELECT 1 FROM dbo.FixedAssets fa WHERE fa.Id = ads.FixedAssetId AND fa.TenantId = @TenantId);
        IF OBJECT_ID('dbo.AssetValuations', 'U') IS NOT NULL
            DELETE av FROM dbo.AssetValuations av WHERE EXISTS (SELECT 1 FROM dbo.FixedAssets fa WHERE fa.Id = av.FixedAssetId AND fa.TenantId = @TenantId);
        IF OBJECT_ID('dbo.AssetTransfers', 'U') IS NOT NULL
            DELETE atf FROM dbo.AssetTransfers atf WHERE EXISTS (SELECT 1 FROM dbo.FixedAssets fa WHERE fa.Id = atf.FixedAssetId AND fa.TenantId = @TenantId);
        IF OBJECT_ID('dbo.AssetTransactions', 'U') IS NOT NULL
            DELETE atr FROM dbo.AssetTransactions atr WHERE EXISTS (SELECT 1 FROM dbo.FixedAssets fa WHERE fa.Id = atr.FixedAssetId AND fa.TenantId = @TenantId);
        IF OBJECT_ID('dbo.AssetDisposals', 'U') IS NOT NULL
            DELETE ad FROM dbo.AssetDisposals ad WHERE EXISTS (SELECT 1 FROM dbo.FixedAssets fa WHERE fa.Id = ad.FixedAssetId AND fa.TenantId = @TenantId);

        UPDATE dbo.FixedAssets
        SET NetBookValue = AcquisitionCost,
            DisposalDate = NULL,
            Status = CASE WHEN Status IN (3, 4, 6) THEN 2 ELSE Status END
        WHERE TenantId = @TenantId;
    END;

    PRINT 'Clearing lease and AUC transaction children scoped through their finance parent documents...';
    IF OBJECT_ID('dbo.LeaseScheduleLines', 'U') IS NOT NULL
       AND OBJECT_ID('dbo.LeaseContracts', 'U') IS NOT NULL
        DELETE lsl FROM dbo.LeaseScheduleLines lsl WHERE EXISTS (SELECT 1 FROM dbo.LeaseContracts lc WHERE lc.Id = lsl.LeaseContractId AND lc.TenantId = @TenantId);

    IF OBJECT_ID('dbo.LeaseContracts', 'U') IS NOT NULL
        DELETE FROM dbo.LeaseContracts WHERE TenantId = @TenantId;

    IF OBJECT_ID('dbo.ProjectSettlementRules', 'U') IS NOT NULL
       AND OBJECT_ID('dbo.CapitalProjects', 'U') IS NOT NULL
        DELETE psr FROM dbo.ProjectSettlementRules psr WHERE EXISTS (SELECT 1 FROM dbo.CapitalProjects cp WHERE cp.Id = psr.CapitalProjectId AND cp.TenantId = @TenantId);

    IF OBJECT_ID('dbo.ProjectCostLines', 'U') IS NOT NULL
       AND OBJECT_ID('dbo.CapitalProjects', 'U') IS NOT NULL
        DELETE pcl FROM dbo.ProjectCostLines pcl WHERE EXISTS (SELECT 1 FROM dbo.CapitalProjects cp WHERE cp.Id = pcl.CapitalProjectId AND cp.TenantId = @TenantId);

    IF OBJECT_ID('dbo.CapitalProjects', 'U') IS NOT NULL
        DELETE FROM dbo.CapitalProjects WHERE TenantId = @TenantId;

    PRINT 'Clearing cash, cheque, bank statement, and reconciliation rows for tenant-linked bank accounts...';
    IF EXISTS (SELECT 1 FROM #TenantBankAccounts)
    BEGIN
        IF OBJECT_ID('dbo.BankStatementLine', 'U') IS NOT NULL
            UPDATE bsl
            SET MatchedTransactionId = NULL,
                ReconciliationMatchId = NULL
            FROM dbo.BankStatementLine bsl
            WHERE EXISTS (
                SELECT 1
                FROM dbo.BankStatement bs
                JOIN #TenantBankAccounts tba ON tba.Id = bs.BankAccountId
                WHERE bs.Id = bsl.BankStatementId
            );

        IF OBJECT_ID('dbo.CashTransaction', 'U') IS NOT NULL
            UPDATE ct
            SET ChequeId = NULL,
                ReconciliationId = NULL
            FROM dbo.CashTransaction ct
            WHERE EXISTS (SELECT 1 FROM #TenantBankAccounts tba WHERE tba.Id IN (ct.BankAccountId, ct.ToBankAccountId));

        IF OBJECT_ID('dbo.Cheque', 'U') IS NOT NULL
            UPDATE ch
            SET CashTransactionId = NULL
            FROM dbo.Cheque ch
            WHERE EXISTS (SELECT 1 FROM #TenantBankAccounts tba WHERE tba.Id = ch.BankAccountId);

        IF OBJECT_ID('dbo.ReconciliationMatch', 'U') IS NOT NULL
            DELETE rm
            FROM dbo.ReconciliationMatch rm
            WHERE EXISTS (
                SELECT 1
                FROM dbo.CashTransaction ct
                JOIN #TenantBankAccounts tba ON tba.Id IN (ct.BankAccountId, ct.ToBankAccountId)
                WHERE ct.Id = rm.CashTransactionId
            )
            OR EXISTS (
                SELECT 1
                FROM dbo.BankReconciliation br
                JOIN #TenantBankAccounts tba ON tba.Id = br.BankAccountId
                WHERE br.Id = rm.ReconciliationId
            );

        IF OBJECT_ID('dbo.BankStatementLine', 'U') IS NOT NULL
            DELETE bsl
            FROM dbo.BankStatementLine bsl
            WHERE EXISTS (
                SELECT 1
                FROM dbo.BankStatement bs
                JOIN #TenantBankAccounts tba ON tba.Id = bs.BankAccountId
                WHERE bs.Id = bsl.BankStatementId
            );

        IF OBJECT_ID('dbo.CashTransaction', 'U') IS NOT NULL
            DELETE ct
            FROM dbo.CashTransaction ct
            WHERE EXISTS (SELECT 1 FROM #TenantBankAccounts tba WHERE tba.Id IN (ct.BankAccountId, ct.ToBankAccountId));

        IF OBJECT_ID('dbo.Cheque', 'U') IS NOT NULL
            DELETE ch
            FROM dbo.Cheque ch
            WHERE EXISTS (SELECT 1 FROM #TenantBankAccounts tba WHERE tba.Id = ch.BankAccountId);

        IF OBJECT_ID('dbo.BankReconciliation', 'U') IS NOT NULL
            DELETE br
            FROM dbo.BankReconciliation br
            WHERE EXISTS (SELECT 1 FROM #TenantBankAccounts tba WHERE tba.Id = br.BankAccountId);

        IF OBJECT_ID('dbo.BankStatement', 'U') IS NOT NULL
            DELETE bs
            FROM dbo.BankStatement bs
            WHERE EXISTS (SELECT 1 FROM #TenantBankAccounts tba WHERE tba.Id = bs.BankAccountId);
    END;

    PRINT 'Clearing fiscal year links to deleted opening/closing journals...';
    IF OBJECT_ID('dbo.FiscalYears', 'U') IS NOT NULL
    BEGIN
        UPDATE dbo.FiscalYears
        SET IsCloseInitiated = 0,
            CloseInitiatedDate = NULL,
            CloseInitiatedByUserId = NULL,
            IsClosed = 0,
            ClosedDate = NULL,
            ClosedByUserId = NULL,
            AllPeriodsClosedValidated = 0,
            AllPeriodsClosedValidatedDate = NULL,
            FinalDepreciationComplete = 0,
            FinalDepreciationDate = NULL,
            YearEndRevaluationComplete = 0,
            YearEndRevaluationDate = NULL,
            YearEndInventoryComplete = 0,
            YearEndInventoryDate = NULL,
            YearEndAccrualsComplete = 0,
            YearEndAccrualsDate = NULL,
            YearEndTrialBalanceValidated = 0,
            YearEndTrialBalanceDate = NULL,
            RetainedEarningsTransferComplete = 0,
            RetainedEarningsTransferDate = NULL,
            ClosingJournalEntryId = NULL,
            NetIncomeTransferred = NULL,
            OpeningBalancesGenerated = 0,
            OpeningBalancesGeneratedDate = NULL,
            OpeningBalanceJournalEntryId = NULL,
            TotalJournalEntries = 0,
            TotalTransactionLines = 0,
            TotalDebits = 0,
            TotalCredits = 0,
            BalanceDifference = 0,
            TotalRevenue = 0,
            TotalExpenses = 0,
            NetIncome = 0,
            YearEndClosingNotes = NULL
        WHERE TenantId = @TenantId;
    END;

    PRINT 'Resetting derived cached balances and finance status fields on preserved master records...';
    IF OBJECT_ID('dbo.Accounts', 'U') IS NOT NULL
    BEGIN
        UPDATE dbo.Accounts
        SET Balance = 0,
            DebitBalance = 0,
            CreditBalance = 0,
            OpeningBalance = 0,
            LastTransactionDate = NULL
        WHERE TenantId = @TenantId;
    END;

    IF OBJECT_ID('dbo.AccountCurrencyLinks', 'U') IS NOT NULL
    BEGIN
        UPDATE dbo.AccountCurrencyLinks
        SET HasTransactionHistory = 0,
            TransactionCount = 0,
            FirstTransactionDate = NULL,
            LastTransactionDate = NULL,
            ForeignCurrencyBalance = 0,
            BaseCurrencyEquivalent = 0,
            LastRevaluationDate = NULL,
            LastRevaluationAdjustment = 0,
            CumulativeRevaluationAdjustment = 0
        WHERE TenantId = @TenantId;
    END;

    IF OBJECT_ID('dbo.BankAccounts', 'U') IS NOT NULL
       AND EXISTS (SELECT 1 FROM #TenantBankAccounts)
    BEGIN
        UPDATE ba
        SET CurrentBalance = OpeningBalance,
            AvailableBalance = OpeningBalance
        FROM dbo.BankAccounts ba
        WHERE EXISTS (SELECT 1 FROM #TenantBankAccounts tba WHERE tba.Id = ba.Id);
    END;

    IF OBJECT_ID('dbo.BusinessPartners', 'U') IS NOT NULL
       AND COL_LENGTH('dbo.BusinessPartners', 'OutstandingBalance') IS NOT NULL
    BEGIN
        UPDATE dbo.BusinessPartners
        SET OutstandingBalance = 0
        WHERE TenantId = @TenantId;
    END;

    IF OBJECT_ID('dbo.Customers', 'U') IS NOT NULL
       AND COL_LENGTH('dbo.Customers', 'OutstandingBalance') IS NOT NULL
    BEGIN
        UPDATE dbo.Customers
        SET OutstandingBalance = 0
        WHERE TenantId = @TenantId;
    END;

    IF OBJECT_ID('dbo.UnitAccounts', 'U') IS NOT NULL
       AND COL_LENGTH('dbo.UnitAccounts', 'CurrentBalance') IS NOT NULL
    BEGIN
        UPDATE dbo.UnitAccounts
        SET CurrentBalance = 0
        WHERE TenantId = @TenantId;
    END;

    IF OBJECT_ID('dbo.FiscalPeriods', 'U') IS NOT NULL
    BEGIN
        UPDATE dbo.FiscalPeriods
        SET IsCloseInitiated = 0,
            CloseInitiatedDate = NULL,
            CloseInitiatedByUserId = NULL,
            IsClosed = 0,
            ClosedDate = NULL,
            ClosedByUserId = NULL,
            TrialBalanceValidated = 0,
            TrialBalanceValidatedDate = NULL,
            BankReconciliationComplete = 0,
            BankReconciliationCompletedDate = NULL,
            CurrencyRevaluationComplete = 0,
            CurrencyRevaluationDate = NULL,
            DepreciationComplete = 0,
            DepreciationCompletedDate = NULL,
            InventoryValuationComplete = 0,
            InventoryValuationDate = NULL,
            AccrualsComplete = 0,
            AccrualsCompletedDate = NULL,
            YearEndCloseComplete = 0,
            YearEndCloseDate = NULL,
            YearEndClosedByUserId = NULL,
            TotalJournalEntries = 0,
            TotalTransactionLines = 0,
            TotalDebits = 0,
            TotalCredits = 0,
            BalanceDifference = 0,
            ClosingNotes = NULL
        WHERE TenantId = @TenantId;
    END;

    IF @DryRun = 1
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT 'DRY RUN COMPLETE: changes were rolled back. Set @DryRun = 0 to commit.';
    END
    ELSE
    BEGIN
        COMMIT TRANSACTION;
        PRINT 'RESET COMPLETE: changes committed.';
    END;
END TRY
BEGIN CATCH
    SET @HadError = 1;
    SET @ErrorMessage = ERROR_MESSAGE();
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT 'Reset failed. Transaction rolled back.';
    PRINT @ErrorMessage;
END CATCH;

PRINT 'Re-enabling referential integrity constraints...';
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';

IF @HadError = 1
BEGIN
    THROW 51001, @ErrorMessage, 1;
END;
