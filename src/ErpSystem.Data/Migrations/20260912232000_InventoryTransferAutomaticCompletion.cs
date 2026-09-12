using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912232000_InventoryTransferAutomaticCompletion")]
public sealed class InventoryTransferAutomaticCompletion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Preserve historic/manual closure proofs. New receipts may complete with
        // their actual receiver only when every unit has been received in good condition.
        const string automaticCompletion = """
            (i.HasOpenDiscrepancy=0
             AND EXISTS (SELECT 1 FROM InventoryTransferItems line WHERE line.InventoryTransferId=i.Id AND line.TenantId=i.TenantId AND line.IsDeleted=0)
             AND NOT EXISTS (SELECT 1 FROM InventoryTransferItems line WHERE line.InventoryTransferId=i.Id AND line.TenantId=i.TenantId
                 AND (line.IsDeleted=1 OR line.RequestedQuantity<=0 OR line.ShippedQuantity<>line.RequestedQuantity
                     OR line.ReceivedQuantity<>line.ShippedQuantity OR line.DamagedQuantity<>0 OR line.ShortageQuantity<>0))
             AND EXISTS (SELECT 1 FROM InventoryTransferActions completed
                 INNER JOIN InventoryTransferActions receipt ON receipt.Id=TRY_CONVERT(uniqueidentifier,JSON_VALUE(completed.SnapshotJson,'$.Metadata.ReceiptActionId'))
                     AND receipt.InventoryTransferId=completed.InventoryTransferId AND receipt.TenantId=completed.TenantId
                     AND receipt.ActorUserId=completed.ActorUserId AND receipt.ActionType IN (6,7) AND receipt.IsDeleted=0
                     AND completed.Sequence=receipt.Sequence+1
                 WHERE completed.InventoryTransferId=i.Id AND completed.TenantId=i.TenantId AND completed.IsDeleted=0
                     AND completed.ActionType=8 AND completed.ActorUserId=i.ClosedById
                     AND JSON_VALUE(completed.SnapshotJson,'$.Metadata.AutomaticCompletion')=N'true'))
            """;
        const string independentCloser = "(i.ApprovalRequired=0 OR (i.ClosedById <> i.RequestedById AND i.ClosedById <> i.ShippedById AND i.ClosedById <> i.ReceivedById AND i.ClosedById <> i.ApprovedById))";
        Patch(migrationBuilder, independentCloser, $"({independentCloser} OR {automaticCompletion})");
        const string participatingCloser = "(i.ApprovalRequired=1 AND i.ClosedById IN (i.RequestedById, i.ApprovedById, i.ShippedById, i.ReceivedById))";
        Patch(migrationBuilder, participatingCloser, $"({participatingCloser} AND NOT {automaticCompletion})");
    }

    private static void Patch(MigrationBuilder builder, string before, string after)
    {
        static string Literal(string value) => value.Replace("'", "''", StringComparison.Ordinal);
        builder.Sql($$"""
            DECLARE @definition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_InventoryTransfers_ControlledLifecycle',N'TR'));
            DECLARE @before nvarchar(max)=N'{{Literal(before)}}';
            IF @definition IS NULL OR (LEN(@definition)-LEN(REPLACE(@definition,@before,N'')))/LEN(@before)<>1
                THROW 51998, 'INV_TRANSFER_AUTOCOMPLETE_GUARD_DRIFT: expected exact lifecycle definition.', 1;
            SET @definition=REPLACE(@definition,@before,N'{{Literal(after)}}');
            SET @definition=STUFF(@definition,1,CHARINDEX(N'TRIGGER',UPPER(@definition))-1,N'CREATE OR ALTER ');
            EXEC sys.sp_executesql @definition;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        THROW 51999, 'Automatic transfer completion history is retained. Restore a verified backup and matching application for rollback.', 1;
        """);
}
