/*
    Prepare-TwoWarehouseInventoryOpeningStock.sql

    Purpose
      Prepare governed INITIAL_STOCK drafts for every eligible active stock item
      at two explicitly selected warehouse locations on a test/UAT database.

    Safety boundary
      This script DOES NOT update InventoryItems, InventoryLocations,
      WarehouseQuantities, InventoryBalances, InventoryLayers or StockMovements.
      Those balances and ledgers are written only when an authorized user submits,
      independently approves when configured, and posts each generated adjustment
      through the ERP Stock Adjustment workflow. Posting also creates the required
      Finance journal (Inventory Control debit / Migration Clearing credit).

      Directly updating quantity caches would bypass valuation, tracking, Finance,
      physical-count and immutable movement controls and is intentionally prohibited.

    Usage
      1. Take a verified SQL backup.
      2. Edit only the CONFIGURATION block below.
      3. Run with @ApplyChanges = 0 and review every result set.
      4. Set @ApplyChanges = 1 and run again to create the two immutable Drafts.
      5. In the ERP, submit/approve/post both adjustment numbers returned by this
         script. Actual quantities do not change until posting succeeds.
      6. Run the read-only verification queries printed at the end after posting.

    Idempotency
      @SeedVersion, tenant, warehouse, location, book and opening date form each
      schedule identity. Re-running before posting reuses the identical Draft. After
      posting, the empty-location guard prevents another opening balance. A conflicting
      replay fails. Increase @SeedVersion only for a deliberately new schedule.

    Tracking
      Serial-, lot-, batch-, manufacture-date- or expiry-tracked items (including
      inherited category policy) are excluded and reported. They require explicit
      per-unit/lot evidence through the normal UI/API.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

/* ============================== CONFIGURATION ============================== */
DECLARE @ApplyChanges bit = 0;                 -- 0 = preview/rollback, 1 = create Draft schedules
DECLARE @ExpectedDatabase sysname = N'';       -- REQUIRED: exact VPS application database name
DECLARE @TenantCode nvarchar(50) = N'DEFAULT';
DECLARE @RequestedByLogin nvarchar(256) = N'storesmanager';
DECLARE @BookCode nvarchar(20) = N'IFRS';
DECLARE @OpeningDate date = CONVERT(date, SYSUTCDATETIME());
DECLARE @SeedVersion nvarchar(20) = N'V1';

DECLARE @WarehouseCode1 nvarchar(50) = N'DEMO-PM';
DECLARE @LocationCode1 nvarchar(100) = N'DEFAULT';
DECLARE @Quantity1 decimal(18,4) = 100.0000;

DECLARE @WarehouseCode2 nvarchar(50) = N'WH-02';
DECLARE @LocationCode2 nvarchar(100) = N'DEFAULT';
DECLARE @Quantity2 decimal(18,4) = 50.0000;

-- Used only when AverageCost, StandardCost and LastPurchaseCost are all non-positive.
-- Zero means "no approved fallback" and makes the script fail with an item report.
-- Supply a positive value only when Finance has approved one for this UAT load.
DECLARE @FallbackUnitCost decimal(18,4) = 0.0000;
/* ========================================================================== */

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
    THROW 51000, 'Refusing to run against a SQL Server system database.', 1;
IF NULLIF(LTRIM(RTRIM(@ExpectedDatabase)), N'') IS NULL
    THROW 51000, 'Set @ExpectedDatabase to the exact VPS application database name before running.', 1;
IF DB_NAME() <> @ExpectedDatabase
BEGIN
    DECLARE @WrongDatabaseError nvarchar(2048) =
        N'Refusing to run against ' + QUOTENAME(DB_NAME()) + N'; expected ' + QUOTENAME(@ExpectedDatabase) + N'.';
    THROW 51000, @WrongDatabaseError, 1;
END;

IF @ApplyChanges NOT IN (0, 1)
    THROW 51000, '@ApplyChanges must be 0 or 1.', 1;
IF NULLIF(LTRIM(RTRIM(@TenantCode)), N'') IS NULL
    THROW 51000, '@TenantCode is required.', 1;
IF NULLIF(LTRIM(RTRIM(@RequestedByLogin)), N'') IS NULL
    THROW 51000, '@RequestedByLogin is required.', 1;
IF NULLIF(LTRIM(RTRIM(@BookCode)), N'') IS NULL OR UPPER(LTRIM(RTRIM(@BookCode))) = N'ALL_ACTIVE_BOOKS'
    THROW 51000, '@BookCode must identify one explicit accounting book.', 1;
IF @OpeningDate IS NULL OR @OpeningDate > CONVERT(date, SYSUTCDATETIME())
    THROW 51000, '@OpeningDate is required and cannot be in the future.', 1;
IF NULLIF(LTRIM(RTRIM(@SeedVersion)), N'') IS NULL OR LEN(@SeedVersion) > 20
    THROW 51000, '@SeedVersion is required and cannot exceed 20 characters.', 1;
IF @Quantity1 <= 0 OR @Quantity2 <= 0
    THROW 51000, 'Both target quantities must be positive.', 1;
IF @FallbackUnitCost < 0
    THROW 51000, '@FallbackUnitCost cannot be negative. Leave it at zero to fail on missing valuation.', 1;

DECLARE @RequiredObjects table (ObjectName sysname NOT NULL PRIMARY KEY);
INSERT @RequiredObjects (ObjectName) VALUES
    (N'dbo.Tenants'), (N'dbo.Users'), (N'dbo.UserTenants'),
    (N'dbo.Warehouses'), (N'dbo.WarehouseLocations'),
    (N'dbo.InventoryItems'), (N'dbo.InventoryCategories'),
    (N'dbo.InventoryLocations'), (N'dbo.InventoryBalances'),
    (N'dbo.StockMovements'), (N'dbo.InventoryMovements'),
    (N'dbo.StockAdjustments'), (N'dbo.StockAdjustmentItems'),
    (N'dbo.FinanceSettings'), (N'dbo.Accounts'),
    (N'dbo.AccountingBooks'), (N'dbo.FiscalPeriods'),
    (N'dbo.AccountingBookPeriods');

DECLARE @MissingObjects nvarchar(max) =
(
    SELECT STRING_AGG(ObjectName, N', ')
    FROM @RequiredObjects
    WHERE OBJECT_ID(ObjectName, N'U') IS NULL
);
IF @MissingObjects IS NOT NULL
BEGIN
    DECLARE @MissingObjectError nvarchar(2048) = N'Required current-schema tables are missing: ' + @MissingObjects;
    THROW 51000, @MissingObjectError, 1;
END;

IF OBJECT_ID(N'dbo.TR_StockAdjustments_ControlledLifecycle', N'TR') IS NULL OR
   OBJECT_ID(N'dbo.TR_StockAdjustmentItems_ControlledMutation', N'TR') IS NULL
    THROW 51000, 'Governed Stock Adjustment SQL triggers are missing. Apply current migrations before running this script.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @TenantId uniqueidentifier;
    IF (SELECT COUNT(*) FROM dbo.Tenants WHERE UPPER(LTRIM(RTRIM(Code))) = UPPER(LTRIM(RTRIM(@TenantCode))) AND IsDeleted = 0) <> 1
        THROW 51000, '@TenantCode must resolve to exactly one active, non-deleted tenant.', 1;
    SELECT @TenantId = Id
    FROM dbo.Tenants
    WHERE UPPER(LTRIM(RTRIM(Code))) = UPPER(LTRIM(RTRIM(@TenantCode))) AND IsDeleted = 0;

    DECLARE @ApplicationLockResult int;
    DECLARE @ApplicationLockResource nvarchar(255) = N'vps:inventory-opening-stock:' + CONVERT(nvarchar(36), @TenantId);
    EXEC @ApplicationLockResult = sys.sp_getapplock
        @Resource = @ApplicationLockResource,
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;
    IF @ApplicationLockResult < 0
        THROW 51000, 'Could not acquire the tenant opening-stock preparation lock.', 1;

    DECLARE @RequestedById uniqueidentifier;
    IF
    (
        SELECT COUNT(*)
        FROM dbo.Users u
        JOIN dbo.UserTenants ut ON ut.UserId = u.Id
        WHERE ut.TenantId = @TenantId AND ut.IsDeleted = 0 AND ut.Status = 0
          AND (ut.ExpiresAt IS NULL OR ut.ExpiresAt > SYSUTCDATETIME())
          AND u.IsActive = 1
          AND (UPPER(u.UserName) = UPPER(@RequestedByLogin) OR UPPER(u.Email) = UPPER(@RequestedByLogin))
    ) <> 1
        THROW 51000, '@RequestedByLogin must resolve to exactly one active user in the selected tenant.', 1;

    SELECT @RequestedById = u.Id
    FROM dbo.Users u
    JOIN dbo.UserTenants ut ON ut.UserId = u.Id
    WHERE ut.TenantId = @TenantId AND ut.IsDeleted = 0 AND ut.Status = 0
      AND (ut.ExpiresAt IS NULL OR ut.ExpiresAt > SYSUTCDATETIME())
      AND u.IsActive = 1
      AND (UPPER(u.UserName) = UPPER(@RequestedByLogin) OR UPPER(u.Email) = UPPER(@RequestedByLogin));

    CREATE TABLE #Targets
    (
        TargetNo tinyint NOT NULL PRIMARY KEY,
        WarehouseCode nvarchar(50) NOT NULL,
        LocationCode nvarchar(100) NOT NULL,
        SeedQuantity decimal(18,4) NOT NULL,
        WarehouseId uniqueidentifier NULL,
        LocationId uniqueidentifier NULL,
        ScheduleReference nvarchar(50) NULL,
        AdjustmentId uniqueidentifier NULL,
        AdjustmentNumber nvarchar(50) NULL,
        IdempotencyKey nvarchar(100) NULL
    );

    INSERT #Targets (TargetNo, WarehouseCode, LocationCode, SeedQuantity)
    VALUES (1, LTRIM(RTRIM(@WarehouseCode1)), LTRIM(RTRIM(@LocationCode1)), @Quantity1),
           (2, LTRIM(RTRIM(@WarehouseCode2)), LTRIM(RTRIM(@LocationCode2)), @Quantity2);

    IF EXISTS (SELECT 1 FROM #Targets GROUP BY UPPER(WarehouseCode), UPPER(LocationCode) HAVING COUNT(*) > 1)
        THROW 51000, 'The two configured warehouse/location pairs must be distinct.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM #Targets t
        WHERE
        (
            SELECT COUNT(*)
            FROM dbo.Warehouses w
            JOIN dbo.WarehouseLocations l
              ON l.TenantId = w.TenantId AND l.WarehouseId = w.Id
            WHERE w.TenantId = @TenantId AND w.IsDeleted = 0 AND w.IsActive = 1
              AND w.IsConsignmentWarehouse = 0
              AND UPPER(LTRIM(RTRIM(w.Code))) = UPPER(t.WarehouseCode)
              AND l.IsDeleted = 0 AND l.IsActive = 1 AND l.IsConsignmentBin = 0
              AND l.IsInTransitLocation = 0
              AND ISNULL(l.LocationHierarchyType, 0) <> 7
              AND UPPER(LTRIM(RTRIM(l.LocationType))) NOT IN (N'TRANSIT', N'INTRANSIT')
              AND UPPER(LTRIM(RTRIM(l.LocationCode))) = UPPER(t.LocationCode)
              AND UPPER(LTRIM(RTRIM(w.WarehouseType))) NOT IN (N'TRANSIT', N'INTRANSIT')
        ) <> 1
    )
        THROW 51000, 'Each warehouse/location pair must resolve exactly once to an active, owned, non-transit scope in the tenant.', 1;

    UPDATE t
    SET WarehouseId = resolved.WarehouseId,
        LocationId = resolved.LocationId
    FROM #Targets t
    CROSS APPLY
    (
        SELECT w.Id WarehouseId, l.Id LocationId
        FROM dbo.Warehouses w
        JOIN dbo.WarehouseLocations l
          ON l.TenantId = w.TenantId AND l.WarehouseId = w.Id
        WHERE w.TenantId = @TenantId AND w.IsDeleted = 0 AND w.IsActive = 1
          AND w.IsConsignmentWarehouse = 0
          AND UPPER(LTRIM(RTRIM(w.Code))) = UPPER(t.WarehouseCode)
          AND l.IsDeleted = 0 AND l.IsActive = 1 AND l.IsConsignmentBin = 0
          AND l.IsInTransitLocation = 0
          AND ISNULL(l.LocationHierarchyType, 0) <> 7
          AND UPPER(LTRIM(RTRIM(l.LocationType))) NOT IN (N'TRANSIT', N'INTRANSIT')
          AND UPPER(LTRIM(RTRIM(l.LocationCode))) = UPPER(t.LocationCode)
          AND UPPER(LTRIM(RTRIM(w.WarehouseType))) NOT IN (N'TRANSIT', N'INTRANSIT')
    ) resolved;

    DECLARE @BookId uniqueidentifier;
    SET @BookCode = UPPER(LTRIM(RTRIM(@BookCode)));
    IF
    (
        SELECT COUNT(*) FROM dbo.AccountingBooks
        WHERE TenantId = @TenantId AND IsDeleted = 0 AND Code = @BookCode
          AND LifecycleStatus = 4 AND IsActive = 1 AND AllowsPosting = 1
    ) <> 1
        THROW 51000, '@BookCode must resolve to exactly one active accounting book that allows posting.', 1;
    SELECT @BookId = Id FROM dbo.AccountingBooks
    WHERE TenantId = @TenantId AND IsDeleted = 0 AND Code = @BookCode
      AND LifecycleStatus = 4 AND IsActive = 1 AND AllowsPosting = 1;

    DECLARE @FiscalPeriodId uniqueidentifier;
    IF
    (
        SELECT COUNT(*) FROM dbo.FiscalPeriods
        WHERE TenantId = @TenantId AND IsDeleted = 0
          AND @OpeningDate >= CONVERT(date, StartDate) AND @OpeningDate <= CONVERT(date, EndDate)
          AND IsOpen = 1 AND IsClosed = 0 AND IsLocked = 0 AND PeriodStatus = N'Open'
    ) <> 1
        THROW 51000, '@OpeningDate must resolve to exactly one open, unlocked outer fiscal period.', 1;
    SELECT @FiscalPeriodId = Id FROM dbo.FiscalPeriods
    WHERE TenantId = @TenantId AND IsDeleted = 0
      AND @OpeningDate >= CONVERT(date, StartDate) AND @OpeningDate <= CONVERT(date, EndDate)
      AND IsOpen = 1 AND IsClosed = 0 AND IsLocked = 0 AND PeriodStatus = N'Open';

    IF
    (
        SELECT COUNT(*) FROM dbo.AccountingBookPeriods
        WHERE TenantId = @TenantId AND IsDeleted = 0
          AND AccountingBookId = @BookId AND FiscalPeriodId = @FiscalPeriodId
          AND PeriodStatus = 2 AND ISNULL(PendingStatus, 0) NOT IN (3, 4)
    ) <> 1
        THROW 51000, 'The selected accounting book must have one open, unlocked authority row for the opening fiscal period.', 1;

    DECLARE @InventoryFallbackAccountId uniqueidentifier;
    DECLARE @MigrationClearingAccountId uniqueidentifier;
    IF (SELECT COUNT(*) FROM dbo.FinanceSettings WHERE TenantId = @TenantId AND IsDeleted = 0) <> 1
        THROW 51000, 'Exactly one active FinanceSettings row is required for the selected tenant.', 1;
    SELECT @InventoryFallbackAccountId = ControlAccountInventoryId,
           @MigrationClearingAccountId = MigrationClearingAccountId
    FROM dbo.FinanceSettings
    WHERE TenantId = @TenantId AND IsDeleted = 0;
    IF @InventoryFallbackAccountId IS NULL OR @MigrationClearingAccountId IS NULL
        THROW 51000, 'FinanceSettings must configure Inventory Control and Migration Clearing accounts.', 1;
    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.Accounts
        WHERE TenantId = @TenantId AND Id = @InventoryFallbackAccountId AND IsDeleted = 0
          AND Status = 1 AND AccountType = 1 AND (AllowDirectPosting = 1 OR IsControlAccount = 1)
    )
        THROW 51000, 'The configured Inventory Control account is not an active tenant Asset posting/control account.', 1;
    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.Accounts
        WHERE TenantId = @TenantId AND Id = @MigrationClearingAccountId AND IsDeleted = 0
          AND Status = 1 AND (AllowDirectPosting = 1 OR IsControlAccount = 1)
    )
        THROW 51000, 'The configured Migration Clearing account is not an active tenant posting/control account.', 1;

    /* Reproduce the effective tracking policy (item flags plus category ancestry). */
    CREATE TABLE #TrackingExcluded
    (
        InventoryItemId uniqueidentifier NOT NULL PRIMARY KEY,
        Reason nvarchar(500) NOT NULL
    );

    ;WITH CategoryLineage AS
    (
        SELECT i.Id InventoryItemId, c.Id CategoryId, c.ParentCategoryId,
               c.DefaultSerialTracking, c.DefaultLotTracking, c.DefaultBatchTracking,
               c.DefaultManufactureDateTracking, c.DefaultExpirationTracking, 0 Depth
        FROM dbo.InventoryItems i
        LEFT JOIN dbo.InventoryCategories c
          ON c.Id = i.CategoryId AND c.TenantId = i.TenantId AND c.IsDeleted = 0
        WHERE i.TenantId = @TenantId AND i.IsDeleted = 0 AND i.Status = 1 AND i.ItemType = 1

        UNION ALL

        SELECT lineage.InventoryItemId, parent.Id, parent.ParentCategoryId,
               parent.DefaultSerialTracking, parent.DefaultLotTracking, parent.DefaultBatchTracking,
               parent.DefaultManufactureDateTracking, parent.DefaultExpirationTracking,
               lineage.Depth + 1
        FROM CategoryLineage lineage
        JOIN dbo.InventoryCategories parent
          ON parent.Id = lineage.ParentCategoryId AND parent.TenantId = @TenantId AND parent.IsDeleted = 0
        WHERE lineage.Depth < 32
    ), EffectiveCategoryPolicy AS
    (
        SELECT InventoryItemId,
               MAX(CASE WHEN CategoryId IS NULL THEN 1 ELSE 0 END) MissingCategory,
               MAX(CONVERT(int, ISNULL(DefaultSerialTracking, 0))) CategorySerial,
               MAX(CONVERT(int, ISNULL(DefaultLotTracking, 0))) CategoryLot,
               MAX(CONVERT(int, ISNULL(DefaultBatchTracking, 0))) CategoryBatch,
               MAX(CONVERT(int, ISNULL(DefaultManufactureDateTracking, 0))) CategoryManufacture,
               MAX(CONVERT(int, ISNULL(DefaultExpirationTracking, 0))) CategoryExpiry
        FROM CategoryLineage
        GROUP BY InventoryItemId
    )
    INSERT #TrackingExcluded (InventoryItemId, Reason)
    SELECT i.Id,
           CASE
             WHEN p.MissingCategory = 1 THEN N'Inventory category is missing.'
             ELSE N'Item or inherited category requires serial/lot/batch/manufacture/expiry tracking.'
           END
    FROM dbo.InventoryItems i
    JOIN EffectiveCategoryPolicy p ON p.InventoryItemId = i.Id
    WHERE i.TenantId = @TenantId AND i.IsDeleted = 0 AND i.Status = 1 AND i.ItemType = 1
      AND (p.MissingCategory = 1 OR i.IsSerialTracked = 1 OR i.IsLotTracked = 1 OR i.IsBatchTracked = 1
           OR i.IsManufactureDateTracked = 1 OR i.IsExpirationTracked = 1
           OR p.CategorySerial = 1 OR p.CategoryLot = 1 OR p.CategoryBatch = 1
           OR p.CategoryManufacture = 1 OR p.CategoryExpiry = 1)
    OPTION (MAXRECURSION 100);

    CREATE TABLE #Plan
    (
        TargetNo tinyint NOT NULL,
        InventoryItemId uniqueidentifier NOT NULL,
        ItemCode nvarchar(100) NOT NULL,
        ItemName nvarchar(200) NOT NULL,
        WarehouseId uniqueidentifier NOT NULL,
        LocationId uniqueidentifier NOT NULL,
        Quantity decimal(18,4) NOT NULL,
        UnitCost decimal(18,4) NOT NULL,
        UsedFallbackCost bit NOT NULL,
        PRIMARY KEY (TargetNo, InventoryItemId)
    );

    INSERT #Plan
        (TargetNo, InventoryItemId, ItemCode, ItemName, WarehouseId, LocationId,
         Quantity, UnitCost, UsedFallbackCost)
    SELECT t.TargetNo, i.Id, i.ItemCode, i.Name, t.WarehouseId, t.LocationId,
           t.SeedQuantity,
           CONVERT(decimal(18,4), CASE WHEN i.AverageCost > 0 THEN i.AverageCost
                                      WHEN i.StandardCost > 0 THEN i.StandardCost
                                      WHEN i.LastPurchaseCost > 0 THEN i.LastPurchaseCost
                                      ELSE @FallbackUnitCost END),
           CONVERT(bit, CASE WHEN i.AverageCost <= 0 AND i.StandardCost <= 0 AND i.LastPurchaseCost <= 0 THEN 1 ELSE 0 END)
    FROM dbo.InventoryItems i
    CROSS JOIN #Targets t
    WHERE i.TenantId = @TenantId AND i.IsDeleted = 0 AND i.Status = 1 AND i.ItemType = 1
      AND NOT EXISTS (SELECT 1 FROM #TrackingExcluded e WHERE e.InventoryItemId = i.Id)
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.InventoryLocations il
          WHERE il.TenantId = @TenantId AND il.InventoryItemId = i.Id AND il.LocationId = t.LocationId
            AND il.IsDeleted = 0
            AND (il.Quantity <> 0 OR il.AllocatedQuantity <> 0 OR il.AvailableQuantity <> 0)
      )
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.InventoryBalances b
          WHERE b.TenantId = @TenantId AND b.InventoryItemId = i.Id AND b.WarehouseId = t.WarehouseId
            AND b.LocationId = t.LocationId AND b.IsDeleted = 0
            AND (b.QuantityOnHand <> 0 OR b.QuantityAllocated <> 0 OR b.QuantityAvailable <> 0 OR b.TotalValue <> 0)
      )
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.StockMovements m
          WHERE m.TenantId = @TenantId AND m.InventoryItemId = i.Id AND m.WarehouseId = t.WarehouseId
            AND m.LocationId = t.LocationId AND m.IsDeleted = 0
      )
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.InventoryMovements m
          WHERE m.TenantId = @TenantId AND m.InventoryItemId = i.Id AND m.WarehouseId = t.WarehouseId
            AND m.LocationId = t.LocationId AND m.IsDeleted = 0
      );

    IF EXISTS (SELECT 1 FROM #Plan WHERE UnitCost <= 0)
    BEGIN
        SELECT N'MISSING_APPROVED_UNIT_COST' ResultSet,
               p.TargetNo, t.WarehouseCode, t.LocationCode, p.ItemCode, p.ItemName,
               i.AverageCost, i.StandardCost, i.LastPurchaseCost, @FallbackUnitCost ConfiguredFallbackUnitCost
        FROM #Plan p
        JOIN #Targets t ON t.TargetNo = p.TargetNo
        JOIN dbo.InventoryItems i ON i.TenantId = @TenantId AND i.Id = p.InventoryItemId
        WHERE p.UnitCost <= 0
        ORDER BY p.TargetNo, p.ItemCode;

        THROW 51000, 'Eligible items lack an approved positive unit cost. Review the report and set @FallbackUnitCost only with Finance approval.', 1;
    END;

    IF NOT EXISTS (SELECT 1 FROM #Plan)
        THROW 51000, 'No eligible empty-location stock item remains for either configured target.', 1;
    IF EXISTS (SELECT 1 FROM #Targets t WHERE NOT EXISTS (SELECT 1 FROM #Plan p WHERE p.TargetNo = t.TargetNo))
        THROW 51000, 'At least one configured target has no eligible empty-location stock items.', 1;

    /* A pre-existing nonterminal opening schedule for a planned bin must be resolved first. */
    IF EXISTS
    (
        SELECT 1
        FROM #Plan p
        JOIN dbo.StockAdjustmentItems line
          ON line.TenantId = @TenantId AND line.InventoryItemId = p.InventoryItemId
         AND line.LocationId = p.LocationId AND line.IsDeleted = 0
        JOIN dbo.StockAdjustments adjustment
          ON adjustment.Id = line.AdjustmentId AND adjustment.TenantId = line.TenantId
         AND adjustment.IsDeleted = 0 AND adjustment.ReasonCode = N'INITIAL_STOCK'
        JOIN #Targets target ON target.TargetNo = p.TargetNo
        WHERE adjustment.Status NOT IN (N'Posted', N'Reversed', N'Cancelled', N'Rejected')
          AND NOT
          (
              adjustment.WarehouseId = target.WarehouseId
              AND adjustment.AdjustmentDate = @OpeningDate
              AND UPPER(adjustment.BookClassification) = @BookCode
              AND UPPER(adjustment.Reference) = UPPER(LEFT(N'VPS-2WH-' + UPPER(@SeedVersion) + N'-' +
                  CONVERT(nvarchar(8), @OpeningDate, 112) + N'-T' + CONVERT(nvarchar(1), target.TargetNo), 50))
          )
    )
        THROW 51000, 'Another nonterminal INITIAL_STOCK schedule already targets at least one planned item/location.', 1;

    /* Do not prepare stock while a controlled count has frozen a planned item. */
    IF OBJECT_ID(N'dbo.PhysicalCounts', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.PhysicalCountItems', N'U') IS NOT NULL
       AND EXISTS
       (
           SELECT 1
           FROM #Plan p
           JOIN dbo.PhysicalCounts c
             ON c.TenantId = @TenantId AND c.WarehouseId = p.WarehouseId AND c.IsDeleted = 0
            AND c.FreezeInventory = 1 AND c.FreezeStartedAtUtc IS NOT NULL AND c.FreezeReleasedAtUtc IS NULL
           JOIN dbo.PhysicalCountItems ci
             ON ci.TenantId = c.TenantId AND ci.PhysicalCountId = c.Id
            AND ci.InventoryItemId = p.InventoryItemId AND ci.IsDeleted = 0
       )
        THROW 51000, 'A planned item is currently frozen by a physical count. Complete or cancel the count first.', 1;

    /* Every item must resolve to a valid Inventory asset account for eventual posting. */
    IF EXISTS
    (
        SELECT 1
        FROM #Plan p
        JOIN dbo.InventoryItems i ON i.Id = p.InventoryItemId AND i.TenantId = @TenantId
        LEFT JOIN dbo.Accounts a
          ON a.Id = COALESCE(i.InventoryAccountId, @InventoryFallbackAccountId)
         AND a.TenantId = @TenantId AND a.IsDeleted = 0 AND a.Status = 1
         AND a.AccountType = 1 AND (a.AllowDirectPosting = 1 OR a.IsControlAccount = 1)
        WHERE a.Id IS NULL
    )
        THROW 51000, 'At least one planned item lacks an active tenant Inventory asset posting/control account.', 1;

    UPDATE t
    SET ScheduleReference = LEFT(N'VPS-2WH-' + UPPER(@SeedVersion) + N'-' + CONVERT(nvarchar(8), @OpeningDate, 112) + N'-T' + CONVERT(nvarchar(1), t.TargetNo), 50)
    FROM #Targets t;

    UPDATE t
    SET IdempotencyKey = N'OPENING-STOCK:' + CONVERT(char(64), HASHBYTES(N'SHA2_256',
          CONVERT(varbinary(max), LOWER(CONVERT(nvarchar(36), @TenantId)) + N'|' +
          LOWER(CONVERT(nvarchar(36), t.WarehouseId)) + N'|' + CONVERT(nvarchar(10), @OpeningDate, 23) + N'|' +
          @BookCode + N'|' + UPPER(t.ScheduleReference))), 2),
        AdjustmentId = CONVERT(uniqueidentifier, SUBSTRING(HASHBYTES(N'SHA2_256',
          CONVERT(varbinary(max), N'VPS-2WH-ADJUSTMENT|' + CONVERT(nvarchar(36), @TenantId) + N'|' + t.ScheduleReference)), 1, 16)),
        AdjustmentNumber = LEFT(N'VPSOS-' + CONVERT(char(32), HASHBYTES(N'MD5',
          CONVERT(varbinary(max), CONVERT(nvarchar(36), @TenantId) + N'|' + t.ScheduleReference)), 2), 50)
    FROM #Targets t;

    CREATE TABLE #ExistingSchedule
    (
        TargetNo tinyint NOT NULL PRIMARY KEY,
        AdjustmentId uniqueidentifier NOT NULL,
        Status nvarchar(20) NOT NULL,
        LineCount int NOT NULL,
        MatchingLineCount int NOT NULL
    );

    INSERT #ExistingSchedule (TargetNo, AdjustmentId, Status, LineCount, MatchingLineCount)
    SELECT t.TargetNo, a.Id, a.Status,
           (SELECT COUNT(*) FROM dbo.StockAdjustmentItems x WHERE x.AdjustmentId = a.Id AND x.TenantId = @TenantId AND x.IsDeleted = 0),
           (SELECT COUNT(*)
            FROM dbo.StockAdjustmentItems x
            JOIN #Plan p ON p.TargetNo = t.TargetNo AND p.InventoryItemId = x.InventoryItemId
                         AND p.LocationId = x.LocationId AND p.Quantity = x.AdjustmentQuantity
                         AND p.UnitCost = x.UnitCost
            WHERE x.AdjustmentId = a.Id AND x.TenantId = @TenantId AND x.IsDeleted = 0)
    FROM #Targets t
    JOIN dbo.StockAdjustments a
      ON a.TenantId = @TenantId AND a.IsDeleted = 0
     AND (a.IdempotencyKey = t.IdempotencyKey OR
          (a.WarehouseId = t.WarehouseId AND a.AdjustmentDate = @OpeningDate
           AND a.BookClassification = @BookCode AND UPPER(a.Reference) = UPPER(t.ScheduleReference)));

    IF EXISTS
    (
        SELECT 1
        FROM #ExistingSchedule e
        WHERE e.LineCount <> e.MatchingLineCount
           OR e.LineCount <> (SELECT COUNT(*) FROM #Plan p WHERE p.TargetNo = e.TargetNo)
    )
        THROW 51000, 'An existing schedule identity has a different item, quantity or valuation payload. Change nothing until the conflict is reviewed.', 1;

    /* Insert one complete immutable Draft per missing target. */
    DECLARE @TargetNo tinyint = 1;
    WHILE @TargetNo <= 2
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM #ExistingSchedule WHERE TargetNo = @TargetNo)
        BEGIN
            DECLARE @AdjustmentId uniqueidentifier;
            DECLARE @AdjustmentNumber nvarchar(50);
            DECLARE @WarehouseId uniqueidentifier;
            DECLARE @LocationId uniqueidentifier;
            DECLARE @ScheduleReference nvarchar(50);
            DECLARE @IdempotencyKey nvarchar(100);
            DECLARE @PayloadHash char(64);
            DECLARE @IntegrityHash char(64);
            DECLARE @TotalValue decimal(18,2);

            SELECT @AdjustmentId = AdjustmentId, @AdjustmentNumber = AdjustmentNumber,
                   @WarehouseId = WarehouseId, @LocationId = LocationId,
                   @ScheduleReference = ScheduleReference, @IdempotencyKey = IdempotencyKey
            FROM #Targets WHERE TargetNo = @TargetNo;

            SELECT @TotalValue = SUM(ROUND(Quantity * UnitCost, 2))
            FROM #Plan WHERE TargetNo = @TargetNo;

            DECLARE @CanonicalPayload nvarchar(max) =
                N'TENANT=' + CONVERT(nvarchar(36), @TenantId) + N'|WAREHOUSE=' + CONVERT(nvarchar(36), @WarehouseId) +
                N'|LOCATION=' + CONVERT(nvarchar(36), @LocationId) + N'|DATE=' + CONVERT(nvarchar(10), @OpeningDate, 23) +
                N'|BOOK=' + @BookCode + N'|SOURCE=' + @ScheduleReference + N'|VERSION=' + @SeedVersion + N'|' +
                (SELECT STRING_AGG(CONVERT(nvarchar(max),
                    CONVERT(nvarchar(36), InventoryItemId) + N':' +
                    CONVERT(nvarchar(40), Quantity) + N':' + CONVERT(nvarchar(40), UnitCost)), N'|')
                 WITHIN GROUP (ORDER BY InventoryItemId)
                 FROM #Plan WHERE TargetNo = @TargetNo);
            SET @PayloadHash = CONVERT(char(64), HASHBYTES(N'SHA2_256', CONVERT(varbinary(max), @CanonicalPayload)), 2);
            SET @IntegrityHash = CONVERT(char(64), HASHBYTES(N'SHA2_256', CONVERT(varbinary(max),
                CONVERT(nvarchar(36), @AdjustmentId) + N'|' + @AdjustmentNumber + N'|' + @PayloadHash)), 2);

            INSERT dbo.StockAdjustments
            (
                Id, AdjustmentNumber, WarehouseId, Reference, AdjustmentDate, BookClassification,
                ReasonCode, Description, Status, ApprovalRequired, RequestedById,
                IdempotencyKey, PayloadHash, CorrelationId, IntegrityHash, TotalAdjustmentValue,
                CreatedAt, CreatedBy, CreatedById, IsDeleted, TenantId
            )
            VALUES
            (
                @AdjustmentId, @AdjustmentNumber, @WarehouseId, @ScheduleReference, @OpeningDate, @BookCode,
                N'INITIAL_STOCK', N'VPS UAT two-warehouse governed opening-stock schedule ' + @SeedVersion,
                N'Draft', 1, @RequestedById,
                @IdempotencyKey, @PayloadHash, N'opening-stock:' + RIGHT(@IdempotencyKey, 32), @IntegrityHash, @TotalValue,
                SYSUTCDATETIME(), N'Prepare-TwoWarehouseInventoryOpeningStock.sql', @RequestedById, 0, @TenantId
            );

            INSERT dbo.StockAdjustmentItems
            (
                Id, AdjustmentId, InventoryItemId, LocationId,
                SystemQuantity, PhysicalQuantity, AdjustmentQuantity,
                UnitCost, AdjustmentValue, Notes, Reason,
                CreatedAt, CreatedBy, CreatedById, IsDeleted, TenantId
            )
            SELECT NEWID(), @AdjustmentId, p.InventoryItemId, p.LocationId,
                   0, p.Quantity, p.Quantity,
                   p.UnitCost, ROUND(p.Quantity * p.UnitCost, 2),
                   N'Prepared for VPS UAT; post through governed Inventory workflow.',
                   N'INITIAL_STOCK',
                   DATEADD(millisecond, CONVERT(int, ROW_NUMBER() OVER (ORDER BY p.InventoryItemId)), SYSUTCDATETIME()),
                   N'Prepare-TwoWarehouseInventoryOpeningStock.sql', @RequestedById, 0, @TenantId
            FROM #Plan p
            WHERE p.TargetNo = @TargetNo;
        END;

        SET @TargetNo += 1;
    END;

    /* In-transaction verification: schedules agree exactly with the intended plan. */
    IF EXISTS
    (
        SELECT 1
        FROM #Targets t
        LEFT JOIN dbo.StockAdjustments a
          ON a.TenantId = @TenantId AND a.IsDeleted = 0 AND a.IdempotencyKey = t.IdempotencyKey
        OUTER APPLY
        (
            SELECT COUNT(*) LineCount,
                   SUM(CASE WHEN p.InventoryItemId IS NOT NULL AND p.LocationId = line.LocationId
                                  AND p.Quantity = line.AdjustmentQuantity AND p.UnitCost = line.UnitCost
                            THEN 1 ELSE 0 END) MatchingCount
            FROM dbo.StockAdjustmentItems line
            LEFT JOIN #Plan p ON p.TargetNo = t.TargetNo AND p.InventoryItemId = line.InventoryItemId
            WHERE line.AdjustmentId = a.Id AND line.TenantId = @TenantId AND line.IsDeleted = 0
        ) checked
        WHERE a.Id IS NULL OR a.Status NOT IN (N'Draft', N'PendingApproval', N'Approved', N'ReadyToPost', N'Posted')
           OR checked.LineCount <> (SELECT COUNT(*) FROM #Plan p WHERE p.TargetNo = t.TargetNo)
           OR checked.MatchingCount <> checked.LineCount
    )
        THROW 51000, 'Post-insert verification failed; the transaction will be rolled back.', 1;

    SELECT N'ELIGIBLE_PLAN' ResultSet, p.TargetNo, t.WarehouseCode, t.LocationCode,
           p.ItemCode, p.ItemName, p.Quantity, p.UnitCost, p.UsedFallbackCost,
           ROUND(p.Quantity * p.UnitCost, 2) ExtendedValue
    FROM #Plan p
    JOIN #Targets t ON t.TargetNo = p.TargetNo
    ORDER BY p.TargetNo, p.ItemCode;

    SELECT N'TRACKED_ITEMS_EXCLUDED' ResultSet, i.ItemCode, i.Name, e.Reason
    FROM #TrackingExcluded e
    JOIN dbo.InventoryItems i ON i.Id = e.InventoryItemId
    ORDER BY i.ItemCode;

    SELECT N'EXCLUDED_OR_SKIPPED_BY_TARGET' ResultSet,
           t.TargetNo, t.WarehouseCode, t.LocationCode, i.ItemCode, i.Name ItemName,
           CASE
             WHEN tracked.InventoryItemId IS NOT NULL THEN tracked.Reason
             WHEN EXISTS
             (
                 SELECT 1 FROM dbo.InventoryLocations il
                 WHERE il.TenantId = @TenantId AND il.InventoryItemId = i.Id
                   AND il.LocationId = t.LocationId AND il.IsDeleted = 0
                   AND (il.Quantity <> 0 OR il.AllocatedQuantity <> 0 OR il.AvailableQuantity <> 0)
             ) THEN N'Exact location already has a non-zero quantity or allocation.'
             WHEN EXISTS
             (
                 SELECT 1 FROM dbo.InventoryBalances b
                 WHERE b.TenantId = @TenantId AND b.InventoryItemId = i.Id
                   AND b.WarehouseId = t.WarehouseId AND b.LocationId = t.LocationId AND b.IsDeleted = 0
                   AND (b.QuantityOnHand <> 0 OR b.QuantityAllocated <> 0 OR b.QuantityAvailable <> 0 OR b.TotalValue <> 0)
             ) THEN N'Valuation balance already exists at the exact warehouse/location.'
             WHEN EXISTS
             (
                 SELECT 1 FROM dbo.StockMovements movement
                 WHERE movement.TenantId = @TenantId AND movement.InventoryItemId = i.Id
                   AND movement.WarehouseId = t.WarehouseId AND movement.LocationId = t.LocationId
                   AND movement.IsDeleted = 0
             ) THEN N'Immutable stock-movement history already exists at the exact warehouse/location.'
             WHEN EXISTS
             (
                 SELECT 1 FROM dbo.InventoryMovements movement
                 WHERE movement.TenantId = @TenantId AND movement.InventoryItemId = i.Id
                   AND movement.WarehouseId = t.WarehouseId AND movement.LocationId = t.LocationId
                   AND movement.IsDeleted = 0
             ) THEN N'Inventory valuation-movement history already exists at the exact warehouse/location.'
             ELSE N'Excluded by a current opening-stock eligibility control; review before changing the script.'
           END SkipReason
    FROM dbo.InventoryItems i
    CROSS JOIN #Targets t
    LEFT JOIN #TrackingExcluded tracked ON tracked.InventoryItemId = i.Id
    WHERE i.TenantId = @TenantId AND i.IsDeleted = 0 AND i.Status = 1 AND i.ItemType = 1
      AND NOT EXISTS
      (
          SELECT 1 FROM #Plan p
          WHERE p.TargetNo = t.TargetNo AND p.InventoryItemId = i.Id
      )
    ORDER BY t.TargetNo, i.ItemCode;

    SELECT N'PLANNED_TOTALS_BY_TARGET' ResultSet,
           t.TargetNo, t.WarehouseCode, t.LocationCode,
           COUNT_BIG(p.InventoryItemId) PlannedItemCount,
           SUM(p.Quantity) PlannedQuantity,
           SUM(ROUND(p.Quantity * p.UnitCost, 2)) PlannedValue,
           SUM(CONVERT(bigint, p.UsedFallbackCost)) FallbackCostItemCount
    FROM #Targets t
    JOIN #Plan p ON p.TargetNo = t.TargetNo
    GROUP BY t.TargetNo, t.WarehouseCode, t.LocationCode
    ORDER BY t.TargetNo;

    SELECT N'SCHEDULES' ResultSet, t.TargetNo, t.WarehouseCode, t.LocationCode,
           a.Id AdjustmentId, a.AdjustmentNumber, a.Reference, a.Status,
           COUNT(line.Id) LineCount, a.TotalAdjustmentValue,
           CASE WHEN @ApplyChanges = 1 THEN N'Draft retained; submit/approve/post in ERP.'
                ELSE N'PREVIEW ONLY; transaction rolled back.' END NextAction
    FROM #Targets t
    JOIN dbo.StockAdjustments a
      ON a.TenantId = @TenantId AND a.IsDeleted = 0 AND a.IdempotencyKey = t.IdempotencyKey
    LEFT JOIN dbo.StockAdjustmentItems line
      ON line.AdjustmentId = a.Id AND line.TenantId = a.TenantId AND line.IsDeleted = 0
    GROUP BY t.TargetNo, t.WarehouseCode, t.LocationCode, a.Id, a.AdjustmentNumber,
             a.Reference, a.Status, a.TotalAdjustmentValue
    ORDER BY t.TargetNo;

    SELECT N'POSTING_PREREQUISITES' ResultSet,
           @TenantCode TenantCode, @BookCode BookCode, @OpeningDate OpeningDate,
           @RequestedByLogin RequestedByLogin,
           @InventoryFallbackAccountId InventoryControlAccountId,
           @MigrationClearingAccountId MigrationClearingAccountId,
           @FiscalPeriodId FiscalPeriodId, @BookId AccountingBookId;

    IF @ApplyChanges = 1
    BEGIN
        COMMIT TRANSACTION;
        PRINT 'DRAFT_PREPARATION|COMMITTED';
        PRINT 'NEXT_ACTION|Submit, independently approve when configured, and post both returned adjustment numbers in the ERP.';
        PRINT 'IMPORTANT|Actual inventory quantities remain unchanged until both governed postings succeed.';
    END
    ELSE
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT 'DRAFT_PREPARATION|PREVIEW_ROLLED_BACK';
        PRINT 'NEXT_ACTION|Review results, set @ApplyChanges = 1, and run again.';
    END;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

/*
    AFTER POSTING: read-only reconciliation query.
    Run this section again after both adjustments show Posted in the ERP.

    SELECT w.Code WarehouseCode, l.LocationCode, i.ItemCode,
           il.Quantity ExactBinQuantity, il.AvailableQuantity ExactBinAvailable,
           wq.CurrentStock WarehouseQuantity, wq.AvailableStock WarehouseAvailable,
           b.QuantityOnHand ValuationQuantity, b.TotalValue ValuationValue,
           movement.MovementCount, movement.MovementQuantity
    FROM dbo.InventoryItems i
    CROSS JOIN (VALUES
        (N'DEMO-PM', N'DEFAULT'),
        (N'WH-02', N'DEFAULT')
    ) configured(WarehouseCode, LocationCode)
    JOIN dbo.Warehouses w ON w.TenantId = i.TenantId AND w.Code = configured.WarehouseCode AND w.IsDeleted = 0
    JOIN dbo.WarehouseLocations l ON l.TenantId = w.TenantId AND l.WarehouseId = w.Id
                                  AND l.LocationCode = configured.LocationCode AND l.IsDeleted = 0
    LEFT JOIN dbo.InventoryLocations il ON il.TenantId = i.TenantId AND il.InventoryItemId = i.Id
                                        AND il.LocationId = l.Id AND il.IsDeleted = 0
    LEFT JOIN dbo.WarehouseQuantities wq ON wq.TenantId = i.TenantId AND wq.InventoryItemId = i.Id
                                         AND wq.WarehouseId = w.Id AND wq.IsDeleted = 0
    LEFT JOIN dbo.InventoryBalances b ON b.TenantId = i.TenantId AND b.InventoryItemId = i.Id
                                      AND b.WarehouseId = w.Id AND b.LocationId = l.Id AND b.IsDeleted = 0
    OUTER APPLY
    (
        SELECT COUNT(*) MovementCount, SUM(sm.Quantity) MovementQuantity
        FROM dbo.StockMovements sm
        WHERE sm.TenantId = i.TenantId AND sm.InventoryItemId = i.Id
          AND sm.WarehouseId = w.Id AND sm.LocationId = l.Id AND sm.IsDeleted = 0
          AND sm.ReferenceType = 5 -- ReferenceType.Adjustment
    ) movement
    WHERE i.TenantId = (SELECT Id FROM dbo.Tenants WHERE Code = N'DEFAULT' AND IsDeleted = 0)
      AND i.IsDeleted = 0 AND i.Status = 1 AND i.ItemType = 1
    ORDER BY w.Code, l.LocationCode, i.ItemCode;
*/
