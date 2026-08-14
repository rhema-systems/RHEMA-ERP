using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// A fully returned requisition legitimately moves back to Approved. The immutable Store Return
/// Voucher must remain reversible in that state, while a new PendingApproval voucher must still
/// require an actually issued source requisition.
/// </summary>
public partial class INVREQFU003AllowPostedFullReturnReversal : Migration
{
    private const string StrictIssuedGate = "r.Status NOT IN (5,6,7)";
    private const string LifecycleAwareGate =
        "(r.Status NOT IN (5,6,7) AND NOT (r.Status = 3 AND i.Status IN (4,5)))";

    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(AlterGateSql(StrictIssuedGate, LifecycleAwareGate));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(AlterGateSql(LifecycleAwareGate, StrictIssuedGate));

    private static string AlterGateSql(string from, string to) => $$"""
        DECLARE @definition nvarchar(max) = OBJECT_DEFINITION(
            OBJECT_ID(N'[dbo].[TR_InventoryReturnVouchers_ControlledLifecycle]', N'TR'));

        IF @definition IS NULL
            THROW 51851, 'INV_RETURN_TRIGGER_MISSING: the controlled Store Return Voucher lifecycle trigger is required.', 1;

        IF CHARINDEX(N'{{to}}', @definition) = 0
        BEGIN
            IF CHARINDEX(N'{{from}}', @definition) = 0
                THROW 51852, 'INV_RETURN_TRIGGER_DRIFT: the Store Return Voucher source gate does not match the governed definition.', 1;

            SET @definition = REPLACE(@definition, N'{{from}}', N'{{to}}');
            DECLARE @triggerKeyword int = CHARINDEX(N'TRIGGER', UPPER(@definition));
            IF @triggerKeyword = 0
                THROW 51852, 'INV_RETURN_TRIGGER_DRIFT: the Store Return Voucher trigger definition is invalid.', 1;
            SET @definition = N'CREATE OR ALTER ' + SUBSTRING(@definition, @triggerKeyword, LEN(@definition));
            EXEC sys.sp_executesql @definition;
        END;
        """;
}
