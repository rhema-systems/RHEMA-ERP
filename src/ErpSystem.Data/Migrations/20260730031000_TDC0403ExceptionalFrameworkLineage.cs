using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ErpSystem.Data.ApplicationDbContext))]
[Migration("20260730031000_TDC0403ExceptionalFrameworkLineage")]
public partial class TDC0403ExceptionalFrameworkLineage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @definition nvarchar(max) =
                OBJECT_DEFINITION(
                    OBJECT_ID(N'[TR_ProcurementFrameworkCallOffs_PurchaseOrderSource]'));
            IF @definition IS NULL
                THROW 51217, 'The framework source protection trigger is unavailable.', 1;

            DECLARE @changed bit = 0;
            IF CHARINDEX(
                    N'exceptional.TenderId = readiness.SourceId',
                    @definition) = 0
            BEGIN
                IF CHARINDEX(
                        N'exceptional.Id = readiness.SourceId',
                        @definition) = 0
                    THROW 51217, 'The exceptional framework source join could not be upgraded safely.', 1;
                SET @definition = REPLACE(
                    @definition,
                    N'exceptional.Id = readiness.SourceId',
                    N'exceptional.TenderId = readiness.SourceId');
                SET @changed = 1;
            END

            IF CHARINDEX(
                    N'newer.DecisionSequence >',
                    @definition) = 0
            BEGIN
                DECLARE @latestNeedle nvarchar(max) =
                    N'OR sourcing.Id IS NULL';
                IF CHARINDEX(@latestNeedle, @definition) = 0
                    THROW 51217, 'The framework readiness guard could not be upgraded safely.', 1;
                DECLARE @latestReplacement nvarchar(max) =
                    N'OR EXISTS (
                              SELECT 1
                              FROM ProcurementAwardReadinessDecisions newer
                              WHERE newer.TenantId = callOff.TenantId
                                AND newer.SourceType = readiness.SourceType
                                AND newer.SourceId = readiness.SourceId
                                AND newer.IsDeleted = 0
                                AND newer.DecisionSequence >
                                    readiness.DecisionSequence
                         )
                         OR sourcing.Id IS NULL';
                SET @definition = REPLACE(
                    @definition,
                    @latestNeedle,
                    @latestReplacement);
                SET @changed = 1;
            END

            IF @changed = 1
            BEGIN
                DECLARE @triggerKeywordPosition int =
                    CHARINDEX(N'TRIGGER', UPPER(@definition));
                IF @triggerKeywordPosition = 0
                    THROW 51217, 'The framework source trigger declaration could not be altered safely.', 1;
                SET @definition =
                    STUFF(
                        @definition,
                        1,
                        @triggerKeywordPosition - 1,
                        N'CREATE OR ALTER ');
                EXEC sys.sp_executesql @definition;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Retain both fail-closed lineage protections so a downgrade cannot
        // re-enable stale-readiness or exceptional-source bypasses.
    }
}
