using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260919210000_ReconcileAuditLogIdempotencyForExistingDatabases")]
public sealed class ReconcileAuditLogIdempotencyForExistingDatabases : Migration
{
    // Also safe to apply as a scoped repair to databases predating the disposable baseline.
    // Existing audit rows and the append-only trigger remain untouched.
    public const string ReconciliationSql = """
        IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NULL
            THROW 52032, 'AuditLogs must already exist before its idempotency schema is reconciled.', 1;

        IF COL_LENGTH(N'dbo.AuditLogs', N'IdempotencyKey') IS NULL
            ALTER TABLE dbo.AuditLogs ADD IdempotencyKey nvarchar(450) NULL;

        IF NOT EXISTS (SELECT 1 FROM sys.indexes
                       WHERE object_id = OBJECT_ID(N'dbo.AuditLogs')
                         AND name = N'IX_AuditLogs_TenantId_IdempotencyKey')
            EXEC(N'CREATE UNIQUE INDEX IX_AuditLogs_TenantId_IdempotencyKey
                   ON dbo.AuditLogs (TenantId, IdempotencyKey)
                   WHERE IdempotencyKey IS NOT NULL');
        """;

    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(ReconciliationSql);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Never erase audit idempotency history or drop an index that may predate this repair.
    }
}
