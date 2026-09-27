using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Restores Finance classification authority tables omitted when an existing development
/// database was stamped with the disposable current-model baseline.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260923223000_ReconcileFinanceClassificationTablesForExistingDatabases")]
public sealed class ReconcileFinanceClassificationTablesForExistingDatabases : Migration
{
    public const string TableSql = """
        IF OBJECT_ID(N'dbo.AccountClassifications', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.AccountClassifications
            (
                Id uniqueidentifier NOT NULL,
                AccountingBookId uniqueidentifier NOT NULL,
                ParentClassificationId uniqueidentifier NULL,
                Code nvarchar(50) NOT NULL,
                Name nvarchar(200) NOT NULL,
                Description nvarchar(1000) NULL,
                CoreAccountType int NOT NULL,
                DefaultRevaluationTreatment int NOT NULL,
                SystemRole int NULL,
                IsPostingClassification bit NOT NULL,
                Status int NOT NULL,
                DisplayOrder int NOT NULL,
                RowVersion rowversion NOT NULL,
                RetirementReason nvarchar(500) NULL,
                RetiredByUserId uniqueidentifier NULL,
                RetiredAtUtc datetime2 NULL,
                CreatedAt datetime2 NOT NULL,
                UpdatedAt datetime2 NULL,
                CreatedBy nvarchar(max) NULL,
                UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL,
                LastModifiedById uniqueidentifier NULL,
                IsDeleted bit NOT NULL,
                DeletedAt datetime2 NULL,
                DeletedBy nvarchar(max) NULL,
                TenantId uniqueidentifier NOT NULL,
                CONSTRAINT PK_AccountClassifications PRIMARY KEY (Id),
                CONSTRAINT FK_AccountClassifications_AccountingBooks_AccountingBookId
                    FOREIGN KEY (AccountingBookId) REFERENCES dbo.AccountingBooks(Id),
                CONSTRAINT FK_AccountClassifications_Tenants_TenantId
                    FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                CONSTRAINT FK_AccountClassifications_AccountClassifications_ParentClassificationId
                    FOREIGN KEY (ParentClassificationId) REFERENCES dbo.AccountClassifications(Id)
            );
        END;

        IF OBJECT_ID(N'dbo.AccountAccountingBooks', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.AccountAccountingBooks
            (
                Id uniqueidentifier NOT NULL,
                AccountId uniqueidentifier NOT NULL,
                AccountingBookId uniqueidentifier NOT NULL,
                AccountClassificationId uniqueidentifier NULL,
                IsEnabled bit NOT NULL,
                FinancialStatementLineItem nvarchar(100) NULL,
                RowVersion rowversion NOT NULL,
                CreatedAt datetime2 NOT NULL,
                UpdatedAt datetime2 NULL,
                CreatedBy nvarchar(max) NULL,
                UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL,
                LastModifiedById uniqueidentifier NULL,
                IsDeleted bit NOT NULL,
                DeletedAt datetime2 NULL,
                DeletedBy nvarchar(max) NULL,
                TenantId uniqueidentifier NOT NULL,
                CONSTRAINT PK_AccountAccountingBooks PRIMARY KEY (Id),
                CONSTRAINT FK_AccountAccountingBooks_Accounts_AccountId
                    FOREIGN KEY (AccountId) REFERENCES dbo.Accounts(Id),
                CONSTRAINT FK_AccountAccountingBooks_AccountingBooks_AccountingBookId
                    FOREIGN KEY (AccountingBookId) REFERENCES dbo.AccountingBooks(Id),
                CONSTRAINT FK_AccountAccountingBooks_AccountClassifications_AccountClassificationId
                    FOREIGN KEY (AccountClassificationId) REFERENCES dbo.AccountClassifications(Id),
                CONSTRAINT FK_AccountAccountingBooks_Tenants_TenantId
                    FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
            );
        END
        ELSE
        BEGIN
            IF COL_LENGTH(N'dbo.AccountAccountingBooks', N'AccountClassificationId') IS NULL
                ALTER TABLE dbo.AccountAccountingBooks ADD AccountClassificationId uniqueidentifier NULL;
            IF COL_LENGTH(N'dbo.AccountAccountingBooks', N'RowVersion') IS NULL
                ALTER TABLE dbo.AccountAccountingBooks ADD RowVersion rowversion NOT NULL;
        END;
        """;

    public const string AuthoritySql = """
        IF OBJECT_ID(N'dbo.AccountClassifications', N'U') IS NOT NULL
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AccountClassifications')
                AND name = N'IX_AccountClassifications_AccountingBookId')
                CREATE INDEX IX_AccountClassifications_AccountingBookId ON dbo.AccountClassifications(AccountingBookId);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AccountClassifications')
                AND name = N'IX_AccountClassifications_ParentClassificationId')
                CREATE INDEX IX_AccountClassifications_ParentClassificationId ON dbo.AccountClassifications(ParentClassificationId);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AccountClassifications')
                AND name = N'IX_AccountClassifications_TenantId_AccountingBookId_Code')
                CREATE UNIQUE INDEX IX_AccountClassifications_TenantId_AccountingBookId_Code
                    ON dbo.AccountClassifications(TenantId, AccountingBookId, Code) WHERE IsDeleted = 0;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AccountClassifications')
                AND name = N'IX_AccountClassifications_TenantId_AccountingBookId_ParentClassificationId_DisplayOrder')
                CREATE INDEX IX_AccountClassifications_TenantId_AccountingBookId_ParentClassificationId_DisplayOrder
                    ON dbo.AccountClassifications(TenantId, AccountingBookId, ParentClassificationId, DisplayOrder);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AccountClassifications')
                AND name = N'IX_AccountClassifications_TenantId_AccountingBookId_SystemRole')
                CREATE UNIQUE INDEX IX_AccountClassifications_TenantId_AccountingBookId_SystemRole
                    ON dbo.AccountClassifications(TenantId, AccountingBookId, SystemRole)
                    WHERE IsDeleted = 0 AND SystemRole IS NOT NULL AND SystemRole <> 1 AND SystemRole <> 2;
        END;

        IF OBJECT_ID(N'dbo.AccountAccountingBooks', N'U') IS NOT NULL
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AccountAccountingBooks')
                AND name = N'IX_AccountAccountingBooks_AccountClassificationId')
                CREATE INDEX IX_AccountAccountingBooks_AccountClassificationId ON dbo.AccountAccountingBooks(AccountClassificationId);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AccountAccountingBooks')
                AND name = N'IX_AccountAccountingBooks_AccountId')
                CREATE INDEX IX_AccountAccountingBooks_AccountId ON dbo.AccountAccountingBooks(AccountId);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AccountAccountingBooks')
                AND name = N'IX_AccountAccountingBooks_AccountingBookId')
                CREATE INDEX IX_AccountAccountingBooks_AccountingBookId ON dbo.AccountAccountingBooks(AccountingBookId);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AccountAccountingBooks')
                AND name = N'IX_AccountAccountingBooks_TenantId_AccountId_AccountingBookId')
                CREATE UNIQUE INDEX IX_AccountAccountingBooks_TenantId_AccountId_AccountingBookId
                    ON dbo.AccountAccountingBooks(TenantId, AccountId, AccountingBookId);
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.AccountAccountingBooks')
                AND name = N'FK_AccountAccountingBooks_AccountClassifications_AccountClassificationId')
                ALTER TABLE dbo.AccountAccountingBooks WITH CHECK ADD CONSTRAINT
                    FK_AccountAccountingBooks_AccountClassifications_AccountClassificationId
                    FOREIGN KEY (AccountClassificationId) REFERENCES dbo.AccountClassifications(Id);
        END;
        """;

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(TableSql);
        migrationBuilder.Sql(AuthoritySql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // These tables can contain governed Finance mappings and must be retained on rollback.
    }
}
