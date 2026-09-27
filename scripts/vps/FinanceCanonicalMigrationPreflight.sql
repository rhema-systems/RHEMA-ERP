-- Read-only data guards for the Finance canonical cutover migrations.
-- Result contract: CheckName (nvarchar), AffectedRows (bigint); blockers only.
-- No application data, migration history, or schema is changed by this batch.
--
-- These mirror executable Up guards, not Down guards or installed trigger bodies.
-- All inspected tables (and AccountingBooks.IsDeleted) are present in
-- 20260916132000_DisposableDevelopmentCurrentModelBaseline. None depends on a
-- column added or renamed by another pending canonical migration. Canonical
-- transaction guards include soft-deleted rows, exactly as the migrations do.
-- An absent baseline table/history is reported, never interpreted as empty.
SET NOCOUNT ON;

DECLARE @Blockers TABLE
(
    CheckName nvarchar(300) NOT NULL,
    AffectedRows bigint NOT NULL
);

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    SELECT CAST(N'FinanceCanonical.MigrationHistoryMissing' AS nvarchar(300)) AS CheckName,
           CAST(1 AS bigint) AS AffectedRows;
    RETURN;
END;

DECLARE @Guards TABLE
(
    MigrationId nvarchar(150) NOT NULL,
    TableName sysname NOT NULL,
    ActiveOnly bit NOT NULL
);

INSERT INTO @Guards (MigrationId, TableName, ActiveOnly)
VALUES
    (N'20260921154920_AccountingBookModelV2', N'AccountingBooks', 1),
    (N'20260924045510_CanonicalVendorInvoiceBusinessPartnerIdentity', N'VendorInvoice', 0),
    (N'20260924095137_CanonicalApSettlementBusinessPartnerIdentity', N'VendorPayment', 0),
    (N'20260924095137_CanonicalApSettlementBusinessPartnerIdentity', N'SupplierDebitNotes', 0),
    (N'20260924104115_CanonicalWhtBusinessPartnerIdentity', N'WithholdingTaxCertificates', 0),
    (N'20260924104115_CanonicalWhtBusinessPartnerIdentity', N'WithholdingTaxRemittanceLines', 0),
    (N'20260924152654_CanonicalSubledgerAdjustmentBusinessPartnerIdentity', N'SubledgerAdjustmentJournals', 0),
    (N'20260924175355_CanonicalCustomerPaymentBusinessPartnerIdentity', N'CustomerPayment', 0),
    (N'20260924190500_CanonicalInvoiceBusinessPartnerEvidence', N'Invoices', 0),
    (N'20260925051637_CanonicalCollectionBusinessPartnerIdentity', N'CollectionActivities', 0),
    (N'20260925051637_CanonicalCollectionBusinessPartnerIdentity', N'PaymentPlans', 0);

DECLARE @MigrationId nvarchar(150), @TableName sysname, @ActiveOnly bit,
        @CheckName nvarchar(300), @Sql nvarchar(max), @AffectedRows bigint,
        @Applied bit;

DECLARE FinanceCanonicalGuards CURSOR LOCAL FAST_FORWARD FOR
    SELECT MigrationId, TableName, ActiveOnly
    FROM @Guards
    ORDER BY MigrationId, TableName;

OPEN FinanceCanonicalGuards;
FETCH NEXT FROM FinanceCanonicalGuards INTO @MigrationId, @TableName, @ActiveOnly;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Applied = 0;
    -- Dynamic SQL also keeps a missing history table from causing compile-time
    -- name resolution before the explicit fail-closed check above can run.
    EXEC sys.sp_executesql
        N'SELECT @Applied = CASE WHEN EXISTS
          (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = @MigrationId)
          THEN 1 ELSE 0 END;',
        N'@MigrationId nvarchar(150), @Applied bit OUTPUT',
        @MigrationId = @MigrationId, @Applied = @Applied OUTPUT;

    IF @Applied = 0
    BEGIN
        SET @CheckName = N'FinanceCanonical.' + @MigrationId + N'.' + @TableName;
        IF OBJECT_ID(N'dbo.' + @TableName, N'U') IS NULL
            INSERT INTO @Blockers VALUES (@CheckName + N'.RequiredTableMissing', 1);
        ELSE IF @ActiveOnly = 1 AND COL_LENGTH(N'dbo.' + @TableName, N'IsDeleted') IS NULL
            INSERT INTO @Blockers VALUES (@CheckName + N'.RequiredIsDeletedColumnMissing', 1);
        ELSE
        BEGIN
            SET @AffectedRows = 0;
            SET @Sql = N'SELECT @AffectedRows = COUNT_BIG(*) FROM dbo.'
                + QUOTENAME(@TableName)
                + CASE WHEN @ActiveOnly = 1 THEN N' WHERE [IsDeleted] = 0;' ELSE N';' END;
            EXEC sys.sp_executesql @Sql, N'@AffectedRows bigint OUTPUT',
                @AffectedRows = @AffectedRows OUTPUT;
            IF @AffectedRows > 0
                INSERT INTO @Blockers VALUES (@CheckName, @AffectedRows);
        END;
    END;
    FETCH NEXT FROM FinanceCanonicalGuards INTO @MigrationId, @TableName, @ActiveOnly;
END;
CLOSE FinanceCanonicalGuards;
DEALLOCATE FinanceCanonicalGuards;

SELECT CheckName, AffectedRows FROM @Blockers ORDER BY CheckName;
