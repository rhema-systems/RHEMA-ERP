using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261002073500_GovernedAccountingBookPeriodBackfill")]
public class GovernedAccountingBookPeriodBackfill : Migration
{
    private const string Marker = "Migration:20261002073500_GovernedAccountingBookPeriodBackfill";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            $"""
            INSERT INTO [AccountingBookPeriods]
                ([Id], [TenantId], [AccountingBookId], [FiscalPeriodId], [PeriodStatus],
                 [CreatedAt], [CreatedBy], [IsDeleted])
            SELECT NEWID(), book.[TenantId], book.[Id], period.[Id],
                   CASE
                       WHEN period.[IsLocked] = 1 THEN 4
                       WHEN period.[IsClosed] = 1 THEN 3
                       WHEN period.[IsOpen] = 1 THEN 2
                       ELSE 1
                   END,
                   SYSUTCDATETIME(), N'{Marker}', 0
            FROM [AccountingBooks] AS book
            INNER JOIN [FiscalPeriods] AS period
                ON period.[TenantId] = book.[TenantId]
               AND period.[IsDeleted] = 0
            WHERE book.[IsDeleted] = 0
              AND book.[LifecycleStatus] <> 6
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [AccountingBookPeriods] AS authority
                  WHERE authority.[TenantId] = book.[TenantId]
                    AND authority.[AccountingBookId] = book.[Id]
                    AND authority.[FiscalPeriodId] = period.[Id]
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            $"DELETE FROM [AccountingBookPeriods] WHERE [CreatedBy] = N'{Marker}';");
    }
}
