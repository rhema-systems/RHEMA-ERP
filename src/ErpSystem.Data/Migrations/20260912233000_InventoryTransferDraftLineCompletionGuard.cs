using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912233000_InventoryTransferDraftLineCompletionGuard")]
public sealed class InventoryTransferDraftLineCompletionGuard : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Removing a draft line retains its audit row. Only the remaining active
        // lines belong to receipt reconciliation; deleted rows are not outstanding stock.
        Patch(migrationBuilder,
            "AND (line.IsDeleted=1 OR line.RequestedQuantity<=0",
            "AND line.IsDeleted=0 AND (line.RequestedQuantity<=0", 2);
        Patch(migrationBuilder,
            "line.TenantId = i.TenantId AND (line.ShippedQuantity <> line.RequestedQuantity",
            "line.TenantId = i.TenantId AND line.IsDeleted = 0 AND (line.ShippedQuantity <> line.RequestedQuantity", 1);
    }

    private static void Patch(MigrationBuilder builder, string before, string after, int expectedMatches)
    {
        static string Literal(string value) => value.Replace("'", "''", StringComparison.Ordinal);
        builder.Sql($$"""
            DECLARE @definition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_InventoryTransfers_ControlledLifecycle',N'TR'));
            DECLARE @before nvarchar(max)=N'{{Literal(before)}}';
            DECLARE @expectedMatches int={{expectedMatches}};
            IF @definition IS NULL OR (LEN(@definition)-LEN(REPLACE(@definition,@before,N'')))/LEN(@before)<>@expectedMatches
                THROW 51998, 'INV_TRANSFER_DRAFT_LINE_GUARD_DRIFT: expected exact lifecycle definition.', 1;
            SET @definition=REPLACE(@definition,@before,N'{{Literal(after)}}');
            SET @definition=STUFF(@definition,1,CHARINDEX(N'TRIGGER',UPPER(@definition))-1,N'CREATE OR ALTER ');
            EXEC sys.sp_executesql @definition;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        THROW 51999, 'Completed transfer and removed draft-line history are retained. Restore a verified backup and matching application for rollback.', 1;
        """);
}
