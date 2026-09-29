using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Reconciles AccountingBooks databases that retained the pre-governance table while the
/// disposable current-model baseline was stamped as applied. Fresh databases already have this
/// shape, so every operation is conditional and safe to repeat.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260923213000_ReconcileAccountingBookGovernanceForExistingDatabases")]
public sealed class ReconcileAccountingBookGovernanceForExistingDatabases : Migration
{
    public const string ReconciliationSql = """
        IF OBJECT_ID(N'dbo.AccountingBooks', N'U') IS NOT NULL
        BEGIN
            IF COL_LENGTH(N'dbo.AccountingBooks', N'BookType') IS NULL
                ALTER TABLE dbo.AccountingBooks ADD BookType int NOT NULL
                    CONSTRAINT DF_AccountingBooks_BookType_Reconcile DEFAULT (1) WITH VALUES;
            IF COL_LENGTH(N'dbo.AccountingBooks', N'LifecycleStatus') IS NULL
                ALTER TABLE dbo.AccountingBooks ADD LifecycleStatus int NOT NULL
                    CONSTRAINT DF_AccountingBooks_LifecycleStatus_Reconcile DEFAULT (2) WITH VALUES;
            IF COL_LENGTH(N'dbo.AccountingBooks', N'FunctionalCurrencyCode') IS NULL
                ALTER TABLE dbo.AccountingBooks ADD FunctionalCurrencyCode nvarchar(3) NULL;
            IF COL_LENGTH(N'dbo.AccountingBooks', N'EffectiveFromUtc') IS NULL
                ALTER TABLE dbo.AccountingBooks ADD EffectiveFromUtc datetime2 NULL;
            IF COL_LENGTH(N'dbo.AccountingBooks', N'EffectiveToUtc') IS NULL
                ALTER TABLE dbo.AccountingBooks ADD EffectiveToUtc datetime2 NULL;
            IF COL_LENGTH(N'dbo.AccountingBooks', N'BaseAccountingBookId') IS NULL
                ALTER TABLE dbo.AccountingBooks ADD BaseAccountingBookId uniqueidentifier NULL;
            IF COL_LENGTH(N'dbo.AccountingBooks', N'InitializationStartedAtUtc') IS NULL
                ALTER TABLE dbo.AccountingBooks ADD InitializationStartedAtUtc datetime2 NULL;
            IF COL_LENGTH(N'dbo.AccountingBooks', N'PendingLifecycleStatus') IS NULL
                ALTER TABLE dbo.AccountingBooks ADD PendingLifecycleStatus int NULL;
            IF COL_LENGTH(N'dbo.AccountingBooks', N'PendingTransitionReason') IS NULL
                ALTER TABLE dbo.AccountingBooks ADD PendingTransitionReason nvarchar(500) NULL;
            IF COL_LENGTH(N'dbo.AccountingBooks', N'TransitionRequestedByUserId') IS NULL
                ALTER TABLE dbo.AccountingBooks ADD TransitionRequestedByUserId uniqueidentifier NULL;
            IF COL_LENGTH(N'dbo.AccountingBooks', N'TransitionRequestedAtUtc') IS NULL
                ALTER TABLE dbo.AccountingBooks ADD TransitionRequestedAtUtc datetime2 NULL;
            IF COL_LENGTH(N'dbo.AccountingBooks', N'TransitionWorkflowInstanceId') IS NULL
                ALTER TABLE dbo.AccountingBooks ADD TransitionWorkflowInstanceId uniqueidentifier NULL;
            IF COL_LENGTH(N'dbo.AccountingBooks', N'TransitionDecidedByUserId') IS NULL
                ALTER TABLE dbo.AccountingBooks ADD TransitionDecidedByUserId uniqueidentifier NULL;
            IF COL_LENGTH(N'dbo.AccountingBooks', N'TransitionDecidedAtUtc') IS NULL
                ALTER TABLE dbo.AccountingBooks ADD TransitionDecidedAtUtc datetime2 NULL;
            IF COL_LENGTH(N'dbo.AccountingBooks', N'TransitionDecisionReason') IS NULL
                ALTER TABLE dbo.AccountingBooks ADD TransitionDecisionReason nvarchar(500) NULL;
            IF COL_LENGTH(N'dbo.AccountingBooks', N'RowVersion') IS NULL
                ALTER TABLE dbo.AccountingBooks ADD RowVersion rowversion NOT NULL;
        END

        -- RHEMA_BATCH_BREAK

        IF OBJECT_ID(N'dbo.AccountingBooks', N'U') IS NOT NULL
        BEGIN
            -- A database that already has the governed base-shape constraint contains classified
            -- books. Re-running the legacy conversion would incorrectly turn every non-default
            -- governed book into a parallel book without its required replication/opening fields.
            IF OBJECT_ID(N'dbo.CK_AccountingBooks_BaseShape', N'C') IS NULL
                EXEC(N'
                    UPDATE b
                       SET BookType = CASE WHEN b.IsDefault = 1 THEN 1 ELSE 2 END,
                           LifecycleStatus = CASE WHEN b.IsActive = 1 AND b.AllowsPosting = 1 THEN 4 ELSE 2 END,
                           IsActive = CASE WHEN b.IsActive = 1 AND b.AllowsPosting = 1 THEN 1 ELSE 0 END,
                           AllowsPosting = CASE WHEN b.IsActive = 1 AND b.AllowsPosting = 1 THEN 1 ELSE 0 END,
                           FunctionalCurrencyCode = COALESCE(
                               CASE WHEN LEN(LTRIM(RTRIM(t.BaseCurrency))) = 3
                                    THEN UPPER(LTRIM(RTRIM(t.BaseCurrency))) END,
                               N''GHS'')
                      FROM dbo.AccountingBooks b
                      JOIN dbo.Tenants t ON t.Id = b.TenantId
                     WHERE b.IsDeleted = 0;');

            IF NOT EXISTS (SELECT 1 FROM sys.key_constraints
                WHERE parent_object_id = OBJECT_ID(N'dbo.AccountingBooks')
                  AND name = N'AK_AccountingBooks_TenantId_Id')
                ALTER TABLE dbo.AccountingBooks ADD CONSTRAINT AK_AccountingBooks_TenantId_Id
                    UNIQUE (TenantId, Id);
            IF NOT EXISTS (SELECT 1 FROM sys.key_constraints
                WHERE parent_object_id = OBJECT_ID(N'dbo.AccountingBooks')
                  AND name = N'AK_AccountingBooks_TenantId_Id_Code')
                ALTER TABLE dbo.AccountingBooks ADD CONSTRAINT AK_AccountingBooks_TenantId_Id_Code
                    UNIQUE (TenantId, Id, Code);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.AccountingBooks')
                  AND name = N'IX_AccountingBooks_TenantId_Code')
                CREATE UNIQUE INDEX IX_AccountingBooks_TenantId_Code
                    ON dbo.AccountingBooks(TenantId, Code);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.AccountingBooks')
                  AND name = N'IX_AccountingBooks_TenantId_BaseAccountingBookId')
                CREATE INDEX IX_AccountingBooks_TenantId_BaseAccountingBookId
                    ON dbo.AccountingBooks(TenantId, BaseAccountingBookId);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.AccountingBooks')
                  AND name = N'IX_AccountingBooks_TenantId_IsDefault')
                CREATE UNIQUE INDEX IX_AccountingBooks_TenantId_IsDefault
                    ON dbo.AccountingBooks(TenantId, IsDefault)
                    WHERE IsDeleted = 0 AND IsDefault = 1;

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
                WHERE parent_object_id = OBJECT_ID(N'dbo.AccountingBooks')
                  AND name = N'FK_AccountingBooks_AccountingBooks_TenantId_BaseAccountingBookId')
                ALTER TABLE dbo.AccountingBooks WITH CHECK ADD CONSTRAINT
                    FK_AccountingBooks_AccountingBooks_TenantId_BaseAccountingBookId
                    FOREIGN KEY (TenantId, BaseAccountingBookId)
                    REFERENCES dbo.AccountingBooks(TenantId, Id);

            IF OBJECT_ID(N'dbo.CK_AccountingBooks_BookType', N'C') IS NULL
                ALTER TABLE dbo.AccountingBooks WITH CHECK ADD CONSTRAINT CK_AccountingBooks_BookType
                    CHECK (IsDeleted = 1 OR BookType IN (1, 2, 3));
            IF OBJECT_ID(N'dbo.CK_AccountingBooks_LifecycleStatus', N'C') IS NULL
                ALTER TABLE dbo.AccountingBooks WITH CHECK ADD CONSTRAINT CK_AccountingBooks_LifecycleStatus
                    CHECK (IsDeleted = 1 OR LifecycleStatus IN (1, 2, 3, 4, 5, 6));
            IF OBJECT_ID(N'dbo.CK_AccountingBooks_EffectiveDates', N'C') IS NULL
                ALTER TABLE dbo.AccountingBooks WITH CHECK ADD CONSTRAINT CK_AccountingBooks_EffectiveDates
                    CHECK (IsDeleted = 1 OR EffectiveToUtc IS NULL OR EffectiveFromUtc IS NULL OR EffectiveToUtc > EffectiveFromUtc);
            IF OBJECT_ID(N'dbo.CK_AccountingBooks_BaseShape', N'C') IS NULL
                ALTER TABLE dbo.AccountingBooks WITH CHECK ADD CONSTRAINT CK_AccountingBooks_BaseShape
                    CHECK (IsDeleted = 1 OR (BookType = 3 AND BaseAccountingBookId IS NOT NULL AND FunctionalCurrencyCode IS NULL)
                        OR (BookType IN (1, 2) AND BaseAccountingBookId IS NULL AND FunctionalCurrencyCode IS NOT NULL));
            IF OBJECT_ID(N'dbo.CK_AccountingBooks_DefaultType', N'C') IS NULL
                ALTER TABLE dbo.AccountingBooks WITH CHECK ADD CONSTRAINT CK_AccountingBooks_DefaultType
                    CHECK (IsDeleted = 1 OR (BookType = 1 AND IsDefault = 1) OR (BookType <> 1 AND IsDefault = 0));
            IF OBJECT_ID(N'dbo.CK_AccountingBooks_NoSelfBase', N'C') IS NULL
                ALTER TABLE dbo.AccountingBooks WITH CHECK ADD CONSTRAINT CK_AccountingBooks_NoSelfBase
                    CHECK (IsDeleted = 1 OR BaseAccountingBookId IS NULL OR BaseAccountingBookId <> Id);
            IF OBJECT_ID(N'dbo.CK_AccountingBooks_PostingLifecycle', N'C') IS NULL
                ALTER TABLE dbo.AccountingBooks WITH CHECK ADD CONSTRAINT CK_AccountingBooks_PostingLifecycle
                    CHECK (IsDeleted = 1 OR (LifecycleStatus = 4 AND IsActive = 1 AND AllowsPosting = 1)
                        OR (LifecycleStatus <> 4 AND IsActive = 0 AND AllowsPosting = 0));
        END
        """;

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var batch in ReconciliationSql.Split(
                     "-- RHEMA_BATCH_BREAK", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            migrationBuilder.Sql(batch);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Existing deployments may already depend on this governed shape. Preserve it on rollback.
    }
}
