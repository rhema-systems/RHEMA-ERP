using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ErpSystem.Data.ApplicationDbContext))]
[Migration("20260801060000_TDC0505TerminalReceiptEligibility")]
public sealed class TDC0505TerminalReceiptEligibility : Migration
{
    private const string OriginalGuard =
        "latestInspection.ApEligibleQuantity <= 0";

    private const string TerminalReceiptGuard =
        "(latestInspection.ApEligibleQuantity <= 0 AND NOT (latestInspection.Status = 8 AND latestInspection.ApEligibleQuantity = 0))";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(PatchPaymentReadinessTriggers(
            OriginalGuard,
            TerminalReceiptGuard,
            51625,
            "AP_PAYMENT_READINESS_TRIGGER_PATCH_FAILED"));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(PatchPaymentReadinessTriggers(
            TerminalReceiptGuard,
            OriginalGuard,
            51626,
            "AP_PAYMENT_READINESS_TRIGGER_RESTORE_FAILED"));
    }

    private static string PatchPaymentReadinessTriggers(
        string expectedFragment,
        string replacementFragment,
        int errorNumber,
        string errorCode) =>
        $$"""
        DECLARE @triggerName sysname;
        DECLARE @definition nvarchar(max);
        DECLARE @triggerKeywordIndex int;
        DECLARE @createKeywordIndex int;
        DECLARE @alterKeywordIndex int;
        DECLARE @expected nvarchar(max) = N'{{expectedFragment}}';
        DECLARE @replacement nvarchar(max) = N'{{replacementFragment}}';

        DECLARE payment_readiness_trigger_cursor CURSOR LOCAL FAST_FORWARD FOR
            SELECT triggerName
            FROM (VALUES
                (N'TR_VendorPaymentAllocation_TDC0505PaymentReadiness'),
                (N'TR_PaymentBatchInvoice_TDC0505PaymentReadiness')) triggers(triggerName);

        OPEN payment_readiness_trigger_cursor;
        FETCH NEXT FROM payment_readiness_trigger_cursor INTO @triggerName;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @definition = OBJECT_DEFINITION(OBJECT_ID(N'dbo.' + @triggerName));
            IF @definition IS NULL
            BEGIN
                CLOSE payment_readiness_trigger_cursor;
                DEALLOCATE payment_readiness_trigger_cursor;
                THROW {{errorNumber}}, '{{errorCode}}: required TDC-0505 trigger is missing.', 1;
            END;

            IF CHARINDEX(@replacement, @definition) = 0
            BEGIN
                IF CHARINDEX(@expected, @definition) = 0
                BEGIN
                    CLOSE payment_readiness_trigger_cursor;
                    DEALLOCATE payment_readiness_trigger_cursor;
                    THROW {{errorNumber}}, '{{errorCode}}: required inspection guard was not found.', 1;
                END;

                SET @definition = REPLACE(@definition, @expected, @replacement);
                SET @triggerKeywordIndex = CHARINDEX(N'TRIGGER', UPPER(@definition));
                SET @createKeywordIndex = CHARINDEX(N'CREATE', UPPER(@definition));
                SET @alterKeywordIndex = CHARINDEX(N'ALTER', UPPER(@definition));
                IF @createKeywordIndex > 0 AND @createKeywordIndex < @triggerKeywordIndex
                BEGIN
                    IF @alterKeywordIndex > @createKeywordIndex AND @alterKeywordIndex < @triggerKeywordIndex
                        SET @definition = STUFF(
                            @definition,
                            @createKeywordIndex,
                            @alterKeywordIndex - @createKeywordIndex,
                            N'');
                    ELSE
                        SET @definition = STUFF(
                            @definition,
                            @createKeywordIndex,
                            LEN(N'CREATE'),
                            N'ALTER');
                END
                ELSE IF @alterKeywordIndex = 0 OR @alterKeywordIndex > @triggerKeywordIndex
                BEGIN
                    CLOSE payment_readiness_trigger_cursor;
                    DEALLOCATE payment_readiness_trigger_cursor;
                    THROW {{errorNumber}}, '{{errorCode}}: stored trigger definition cannot be altered safely.', 1;
                END;
                EXEC sys.sp_executesql @definition;
            END;

            FETCH NEXT FROM payment_readiness_trigger_cursor INTO @triggerName;
        END;
        CLOSE payment_readiness_trigger_cursor;
        DEALLOCATE payment_readiness_trigger_cursor;
        """;
}
