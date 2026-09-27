-- Read-only forward-migration preflight. Only table variables are written.
-- Result contract: CheckName nvarchar, AffectedRows bigint; positive rows block deployment.
-- Applied migrations are skipped. Unexpected partial schema fails closed rather than
-- being treated as an applied migration. This file does not repair data or history.
-- Reviewed coverage (Up versus Down/runtime THROW):
-- 20260924190000_ReceiptItemWeightSnapshots: Up widens decimal and adds nullable/default
--   columns. 51710 is Down-only; historical weight precision is NOT an upgrade blocker.
-- 20260924210000_LandedCostReceiptWeights: new empty table; 51711/51712 are runtime triggers.
-- 20260924220000_LandedCostSupplierDocuments: new empty table; 51713/51714 are runtime triggers.
-- 20260924230000_ProcurementAutoInvoiceReceipts: new nullable source fields/new empty table;
--   existing accepted-supply constraints are widened. Check existing rows below. 51720
--   is Down-only and 51721..51725 are runtime triggers, not existing-row Up guards.
-- 20260924233000_BusinessPartnerRoleCompatibility: 51727 is a real Up precondition.
--   Mirror installed trigger existence, exact predicates and DDL-header recognition.
--   51726 is Down-only. Governance triggers originate in the current-model baseline.
-- 20260925110645_CustomerPostingAccounts: nullable FK additions; 51728 is Down-only.
-- 20260925180442_SupplierInvoiceTaxFallback: nullable FK addition; 51729 is Down-only.
-- 20260925220000_CustomerAdjustmentPostingAccounts: nullable FK additions; 51730 Down-only.
-- 20260925230000_ProcurementInvoiceDistributionDraft: nullable addition; 51731 Down-only.
-- 20260926210000_EstateSupplierInvoiceLineage: new nullable fields make the new check/FK/
--   filtered unique index valid for existing invoices; source trigger 51732 is runtime,
--   51733 is Down-only. Check acquisition alternate-key uniqueness and schema below.
SET NOCOUNT ON;
DECLARE @Checks TABLE (CheckName nvarchar(240) NOT NULL, AffectedRows bigint NOT NULL);
DECLARE @Applied TABLE (MigrationId nvarchar(150) NOT NULL PRIMARY KEY);
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    SELECT N'ProcurementMigrationHistoryMissing' AS CheckName, CAST(1 AS bigint) AS AffectedRows;
    RETURN;
END;
INSERT @Applied EXEC sys.sp_executesql N'SELECT MigrationId FROM dbo.__EFMigrationsHistory;';

-- Each owned addition must still be absent when its migration is pending. All parent
-- tables below exist in the canonical baseline, so no pending predecessor is guessed.
DECLARE @Additions TABLE (MigrationId nvarchar(150), TableName sysname, ColumnName sysname NULL);
INSERT @Additions VALUES
(N'20260924190000_ReceiptItemWeightSnapshots', N'InventoryItems', N'WeightUnit'),
(N'20260924190000_ReceiptItemWeightSnapshots', N'PurchaseOrderReceiptItems', N'UnitWeightKg'),
(N'20260924190000_ReceiptItemWeightSnapshots', N'PurchaseOrderReceiptItems', N'WeightStockUom'),
(N'20260924190000_ReceiptItemWeightSnapshots', N'PurchaseOrderReceiptItems', N'WeightOverridden'),
(N'20260924190000_ReceiptItemWeightSnapshots', N'GoodsReceiptNoteItems', N'UnitWeightKg'),
(N'20260924190000_ReceiptItemWeightSnapshots', N'GoodsReceiptNoteItems', N'WeightStockUom'),
(N'20260924190000_ReceiptItemWeightSnapshots', N'GoodsReceiptNoteItems', N'WeightOverridden'),
(N'20260924210000_LandedCostReceiptWeights', N'LandedCostReceiptWeights', NULL),
(N'20260924220000_LandedCostSupplierDocuments', N'LandedCostSupplierDocuments', NULL),
(N'20260924230000_ProcurementAutoInvoiceReceipts', N'VendorInvoice', N'AutoInvoiceRequestId'),
(N'20260924230000_ProcurementAutoInvoiceReceipts', N'VendorInvoice', N'AutoInvoiceRequestHash'),
(N'20260924230000_ProcurementAutoInvoiceReceipts', N'VendorInvoiceReceiptAllocations', NULL),
(N'20260925110645_CustomerPostingAccounts', N'BusinessPartners', N'CustomerCostOfSalesAccountId'),
(N'20260925110645_CustomerPostingAccounts', N'BusinessPartners', N'CustomerInventoryAccountId'),
(N'20260925110645_CustomerPostingAccounts', N'BusinessPartners', N'CustomerSalesAccountId'),
(N'20260925110645_CustomerPostingAccounts', N'BusinessPartners', N'CustomerSalesReturnsAccountId'),
(N'20260925110645_CustomerPostingAccounts', N'BusinessPartners', N'CustomerTermsDiscountsTakenAccountId'),
(N'20260925180442_SupplierInvoiceTaxFallback', N'VendorInvoice', N'SupplierTaxFallbackAccountId'),
(N'20260925220000_CustomerAdjustmentPostingAccounts', N'BusinessPartners', N'CustomerFinanceChargesAccountId'),
(N'20260925220000_CustomerAdjustmentPostingAccounts', N'BusinessPartners', N'CustomerWriteoffAccountId'),
(N'20260925220000_CustomerAdjustmentPostingAccounts', N'BusinessPartners', N'CustomerOverpaymentWriteoffAccountId'),
(N'20260925220000_CustomerAdjustmentPostingAccounts', N'SubledgerAdjustmentJournals', N'ControlAccountId'),
(N'20260925230000_ProcurementInvoiceDistributionDraft', N'VendorInvoice', N'DistributionDraftJson'),
(N'20260926210000_EstateSupplierInvoiceLineage', N'VendorInvoice', N'EstateAcquisitionId'),
(N'20260926210000_EstateSupplierInvoiceLineage', N'VendorInvoice', N'EstatePayableKind');
INSERT @Checks
SELECT CONCAT(N'UnexpectedPendingSchema:', a.MigrationId, N':', a.TableName, N'.', COALESCE(a.ColumnName, N'<table>')), 1
FROM @Additions a WHERE NOT EXISTS (SELECT 1 FROM @Applied m WHERE m.MigrationId=a.MigrationId)
AND ((a.ColumnName IS NULL AND OBJECT_ID(N'dbo.'+QUOTENAME(a.TableName), N'U') IS NOT NULL)
 OR (a.ColumnName IS NOT NULL AND (OBJECT_ID(N'dbo.'+QUOTENAME(a.TableName), N'U') IS NULL
 OR COL_LENGTH(N'dbo.'+QUOTENAME(a.TableName), a.ColumnName) IS NOT NULL)));

DECLARE @Definition nvarchar(max), @Trigger sysname, @Before nvarchar(200),
    @After nvarchar(200)=N'bp.PartnerType NOT IN (''Supplier'',''Vendor'',''Manufacturer'',''Contractor'',''Both'',''CustomerAndSupplier'')';
IF NOT EXISTS (SELECT 1 FROM @Applied WHERE MigrationId=N'20260924233000_BusinessPartnerRoleCompatibility')
BEGIN
    DECLARE @Triggers TABLE (TriggerName sysname, LegacyPredicate nvarchar(200));
    INSERT @Triggers VALUES
    (N'TR_ProcurementTenderDocumentIssuances_Immutable', N'bp.PartnerType NOT IN (''Supplier'', ''Contractor'', ''Both'')'),
    (N'TR_QS0521_Subcontracts_Governance', N'bp.PartnerType NOT IN (''Supplier'',''Contractor'',''Both'')');
    WHILE EXISTS (SELECT 1 FROM @Triggers)
    BEGIN
        SELECT TOP (1) @Trigger=TriggerName, @Before=LegacyPredicate FROM @Triggers ORDER BY TriggerName;
        SET @Definition=OBJECT_DEFINITION(OBJECT_ID(N'dbo.'+QUOTENAME(@Trigger), N'TR'));
        IF @Definition IS NULL
            INSERT @Checks VALUES (N'PartnerGovernanceTriggerMissing:'+@Trigger,1);
        ELSE IF CHARINDEX(@Before,@Definition)=0 AND CHARINDEX(@After,@Definition)=0
            INSERT @Checks VALUES (N'PartnerGovernancePredicateUnrecognized:'+@Trigger,1);
        ELSE IF CHARINDEX(@Before,@Definition)>0
        BEGIN
            SET @Definition=LTRIM(@Definition);
            IF CHARINDEX(N'TRIGGER',UPPER(@Definition))=0 OR LEFT(UPPER(@Definition),6) NOT IN (N'CREATE',N'ALTER ')
                INSERT @Checks VALUES (N'PartnerGovernanceHeaderUnrecognized:'+@Trigger,1);
        END;
        DELETE @Triggers WHERE TriggerName=@Trigger;
    END;
END;

-- Dynamic SQL avoids compile-time binding to fields from migrations not yet applied.
DECLARE @Count bigint;
IF NOT EXISTS (SELECT 1 FROM @Applied WHERE MigrationId=N'20260924230000_ProcurementAutoInvoiceReceipts')
BEGIN
    IF COL_LENGTH(N'dbo.VendorInvoice',N'AcceptedSupplyKind') IS NULL
       OR COL_LENGTH(N'dbo.VendorInvoice',N'AcceptedSupplySourceId') IS NULL
       OR COL_LENGTH(N'dbo.VendorInvoice',N'AcceptedSupplySourceReference') IS NULL
       OR COL_LENGTH(N'dbo.VendorInvoice',N'AcceptedSupplySnapshotHash') IS NULL
       OR COL_LENGTH(N'dbo.VendorInvoice',N'AcceptedSupplyValidatedAtUtc') IS NULL
       OR COL_LENGTH(N'dbo.VendorInvoice',N'PurchaseOrderId') IS NULL
        INSERT @Checks VALUES (N'AutoInvoiceAcceptedSupplyBaselineSchemaMissing',1);
    ELSE
    BEGIN
        EXEC sys.sp_executesql N'SELECT @n=COUNT_BIG(*) FROM dbo.VendorInvoice WHERE NOT (
          (AcceptedSupplyKind IS NULL AND AcceptedSupplySourceId IS NULL AND AcceptedSupplySourceReference IS NULL AND AcceptedSupplySnapshotHash IS NULL AND AcceptedSupplyValidatedAtUtc IS NULL)
          OR (AcceptedSupplyKind BETWEEN 1 AND 4 AND AcceptedSupplySourceId IS NOT NULL AND LEN(AcceptedSupplySourceReference) BETWEEN 1 AND 100 AND LEN(AcceptedSupplySnapshotHash)=64 AND AcceptedSupplyValidatedAtUtc IS NOT NULL))
          OR NOT (AcceptedSupplyKind IS NULL OR AcceptedSupplyKind=3 OR PurchaseOrderId IS NOT NULL);',
          N'@n bigint OUTPUT', @n=@Count OUTPUT;
        IF @Count>0 INSERT @Checks VALUES (N'AutoInvoiceAcceptedSupplyCheckWouldFail',@Count);
        INSERT @Checks
        SELECT N'AutoInvoiceRequiredConstraintMissing:'+v.ConstraintName,1
        FROM (VALUES (N'CK_VendorInvoice_AcceptedSupplyCoherent'),(N'CK_VendorInvoice_AcceptedSupplyPurchaseOrder')) v(ConstraintName)
        WHERE NOT EXISTS (SELECT 1 FROM sys.check_constraints c WHERE c.parent_object_id=OBJECT_ID(N'dbo.VendorInvoice') AND c.name=v.ConstraintName);
    END;
END;
IF NOT EXISTS (SELECT 1 FROM @Applied WHERE MigrationId=N'20260926210000_EstateSupplierInvoiceLineage')
BEGIN
    IF COL_LENGTH(N'dbo.LandAcquisitions',N'TenantId') IS NULL OR COL_LENGTH(N'dbo.LandAcquisitions',N'Id') IS NULL
        INSERT @Checks VALUES (N'EstateAcquisitionBaselineSchemaMissing',1);
    ELSE
    BEGIN
        EXEC sys.sp_executesql N'SELECT @n=COUNT_BIG(*) FROM (SELECT TenantId,Id FROM dbo.LandAcquisitions GROUP BY TenantId,Id HAVING COUNT_BIG(*)>1) d;',N'@n bigint OUTPUT',@n=@Count OUTPUT;
        IF @Count>0 INSERT @Checks VALUES (N'EstateAcquisitionAlternateKeyDuplicates',@Count);
    END;
    -- AutoInvoiceRequestId may legitimately be absent: its predecessor adds a NULL
    -- column before EstateSource is installed. If predecessor is already applied,
    -- absence is a history/schema mismatch and must block.
    IF EXISTS (SELECT 1 FROM @Applied WHERE MigrationId=N'20260924230000_ProcurementAutoInvoiceReceipts')
       AND COL_LENGTH(N'dbo.VendorInvoice',N'AutoInvoiceRequestId') IS NULL
        INSERT @Checks VALUES (N'EstateAutoInvoicePredecessorSchemaMissing',1);
    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.LandAcquisitions') AND name=N'AK_LandAcquisitions_TenantId_Id')
        INSERT @Checks VALUES (N'EstateAcquisitionAlternateKeyAlreadyPresentWithoutHistory',1);
END;
SELECT CheckName,AffectedRows FROM @Checks ORDER BY CheckName;
