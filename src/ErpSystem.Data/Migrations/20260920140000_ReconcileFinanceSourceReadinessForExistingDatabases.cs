using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

/// <summary>Reconciles the archived Finance readiness migration without replaying the disposable baseline.</summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260920140000_ReconcileFinanceSourceReadinessForExistingDatabases")]
public sealed class ReconcileFinanceSourceReadinessForExistingDatabases : Migration
{
    public const string ReconciliationSql = """
        IF OBJECT_ID(N'dbo.FinanceSourceDimensionAssignments', N'U') IS NULL
            THROW 52038, 'Finance source dimension assignments must exist before readiness reconciliation.', 1;
        IF COL_LENGTH(N'dbo.FinanceSourceDimensionAssignments', N'ExpectedSourceLineCount') IS NULL
            ALTER TABLE dbo.FinanceSourceDimensionAssignments ADD ExpectedSourceLineCount int NULL;
        IF COL_LENGTH(N'dbo.FinanceSourceDimensionAssignments', N'ResolvedAccountId') IS NULL
            ALTER TABLE dbo.FinanceSourceDimensionAssignments ADD ResolvedAccountId uniqueidentifier NULL;
        IF COL_LENGTH(N'dbo.FinanceSourceDimensionAssignments', N'SourceDocumentDate') IS NULL
            ALTER TABLE dbo.FinanceSourceDimensionAssignments ADD SourceDocumentDate datetime2 NULL;
        IF COL_LENGTH(N'dbo.FinanceSourceDimensionAssignments', N'SourceLineManifestHash') IS NULL
            ALTER TABLE dbo.FinanceSourceDimensionAssignments ADD SourceLineManifestHash nvarchar(64) NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FinanceSourceDimensionAssignments')
            AND name = N'IX_FinanceSourceDimensionAssignments_ResolvedAccountId')
            EXEC(N'CREATE INDEX IX_FinanceSourceDimensionAssignments_ResolvedAccountId ON dbo.FinanceSourceDimensionAssignments(ResolvedAccountId)');
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FinanceSourceDimensionAssignments')
            AND name = N'IX_FinanceSourceDimensionAssignments_TenantId_ResolvedAccountId')
            EXEC(N'CREATE INDEX IX_FinanceSourceDimensionAssignments_TenantId_ResolvedAccountId ON dbo.FinanceSourceDimensionAssignments(TenantId, ResolvedAccountId)');
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.FinanceSourceDimensionAssignments')
            AND name = N'FK_FinanceSourceDimensionAssignments_Accounts_ResolvedAccountId')
            EXEC(N'ALTER TABLE dbo.FinanceSourceDimensionAssignments WITH CHECK ADD CONSTRAINT FK_FinanceSourceDimensionAssignments_Accounts_ResolvedAccountId FOREIGN KEY (ResolvedAccountId) REFERENCES dbo.Accounts(Id)');
        """;

    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(ReconciliationSql);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve evidence and columns also owned by the current baseline and archived migration.
    }
}
