using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ErpSystem.Data.ApplicationDbContext))]
[Migration("20260730150000_TDC0403OrdinarySourceTriggerCorrections")]
public partial class TDC0403OrdinarySourceTriggerCorrections : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @definition nvarchar(max) =
                OBJECT_DEFINITION(
                    OBJECT_ID(N'[TR_PurchaseOrders_ApprovedSourceProtected]'));
            IF @definition IS NULL
                THROW 51218, 'The approved-source protection trigger is unavailable.', 1;

            DECLARE @changed bit = 0;
            IF CHARINDEX(
                    N'readiness.SourceId <> exceptional.TenderId',
                    @definition) = 0
            BEGIN
                IF CHARINDEX(
                        N'readiness.SourceId <> exceptional.Id',
                        @definition) = 0
                    THROW 51218, 'The exceptional-source readiness guard could not be upgraded safely.', 1;
                SET @definition = REPLACE(
                    @definition,
                    N'readiness.SourceId <> exceptional.Id',
                    N'readiness.SourceId <> exceptional.TenderId');
                SET @changed = 1;
            END

            IF CHARINDEX(
                    N'contract.StartDate > SYSUTCDATETIME()',
                    @definition) = 0
            BEGIN
                DECLARE @contractNeedle nvarchar(max) =
                    N'OR contract.Status <> ''Active''';
                IF CHARINDEX(@contractNeedle, @definition) = 0
                    THROW 51218, 'The contract effective-period guard could not be upgraded safely.', 1;
                DECLARE @contractReplacement nvarchar(max) =
                    N'OR contract.Status <> ''Active''
                        OR (contract.StartDate IS NOT NULL
                            AND contract.StartDate > SYSUTCDATETIME())
                        OR (contract.EndDate IS NOT NULL
                            AND contract.EndDate < SYSUTCDATETIME())';
                SET @definition = REPLACE(
                    @definition,
                    @contractNeedle,
                    @contractReplacement);
                SET @changed = 1;
            END

            IF @changed = 1
            BEGIN
                DECLARE @triggerKeywordPosition int =
                    CHARINDEX(N'TRIGGER', UPPER(@definition));
                IF @triggerKeywordPosition = 0
                    THROW 51218, 'The approved-source trigger declaration could not be altered safely.', 1;
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
        // Retain the fail-closed effective-period and exceptional-readiness
        // guards so a downgrade cannot reopen direct-SQL source bypasses.
    }
}
