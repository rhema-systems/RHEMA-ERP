using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Keeps an awarded RFQ line's commercial identity stable when receiving staff
/// create and link the stock-master item that did not exist when the RFQ was
/// issued. Quantity, UOM, price, total and source-line checks remain unchanged.
/// </summary>
public partial class AlignRfqCommercialIdentityWithReceiptItemMaster : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CreateIdentityFunctionSql);

        migrationBuilder.Sql(BuildPatchSql(
            "TR_PurchaseOrders_ApprovedCommercialCapacity",
            expectedIdentityOccurrences: 4,
            OldIdentity,
            SourceLineIdentity));

        migrationBuilder.Sql(BuildPatchSql(
            "TR_PurchaseOrderItems_ApprovedCommercialCapacity",
            expectedIdentityOccurrences: 2,
            OldIdentity,
            SourceLineIdentity));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(BuildPatchSql(
            "TR_PurchaseOrders_ApprovedCommercialCapacity",
            expectedIdentityOccurrences: 4,
            SourceLineIdentity,
            OldIdentity));

        migrationBuilder.Sql(BuildPatchSql(
            "TR_PurchaseOrderItems_ApprovedCommercialCapacity",
            expectedIdentityOccurrences: 2,
            SourceLineIdentity,
            OldIdentity));

        migrationBuilder.Sql(
            "DROP FUNCTION IF EXISTS [dbo].[fn_ProcurementRfqSourceLineIdentity];");
    }

    private static string BuildPatchSql(
        string triggerName,
        int expectedIdentityOccurrences,
        string fromIdentity,
        string toIdentity)
    {
        var fromIdentitySql = EscapeSqlLiteral(fromIdentity);
        var toIdentitySql = EscapeSqlLiteral(toIdentity);
        return $$"""
            DECLARE @definition nvarchar(max) =
                OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[{{triggerName}}]', N'TR'));
            IF @definition IS NULL
                THROW 51980, 'The RFQ commercial-capacity trigger is required before receipt item-master alignment.', 1;

            DECLARE @fromIdentity nvarchar(max) = N'{{fromIdentitySql}}';
            DECLARE @toIdentity nvarchar(max) = N'{{toIdentitySql}}';
            DECLARE @identityCount int =
                (LEN(@definition) - LEN(REPLACE(@definition, @fromIdentity, N''))) /
                NULLIF(LEN(@fromIdentity), 0);
            IF @identityCount <> {{expectedIdentityOccurrences}}
                THROW 51981, 'The RFQ commercial-capacity trigger has drifted from the verified baseline.', 1;

            SET @definition = REPLACE(@definition, @fromIdentity, @toIdentity);

            DECLARE @triggerKeywordPosition int =
                CHARINDEX(N'TRIGGER', UPPER(@definition));
            IF @triggerKeywordPosition = 0
                THROW 51982, 'The RFQ commercial-capacity trigger declaration could not be altered safely.', 1;
            SET @definition =
                N'ALTER ' + SUBSTRING(
                    @definition,
                    @triggerKeywordPosition,
                    LEN(@definition));
            EXEC sys.sp_executesql @definition;
            """;
    }

    private static string EscapeSqlLiteral(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);

    private const string OldIdentity =
        "WHEN item.InventoryItemId IS NOT NULL";

    private const string SourceLineIdentity = """
        -- TDC0502_RFQ_ITEM_MASTER_LINEAGE
        WHEN item.SourceRfqItemId IS NOT NULL
                                            THEN dbo.fn_ProcurementRfqSourceLineIdentity(
                                                item.TenantId,
                                                item.SourceRfqItemId)
                                            WHEN item.InventoryItemId IS NOT NULL
        """;

    private const string CreateIdentityFunctionSql = """
        CREATE OR ALTER FUNCTION [dbo].[fn_ProcurementRfqSourceLineIdentity]
        (
            @TenantId uniqueidentifier,
            @SourceRfqItemId uniqueidentifier
        )
        RETURNS nvarchar(400)
        AS
        BEGIN
            DECLARE @identity nvarchar(400);

            SELECT @identity =
                CASE
                    WHEN sourceRfqItem.InventoryItemId IS NOT NULL
                        THEN CONCAT(
                            N'inventory:',
                            LOWER(CONVERT(
                                varchar(36),
                                sourceRfqItem.InventoryItemId)))
                    ELSE CONCAT(
                        N'description:',
                        LOWER(LTRIM(RTRIM(
                            ISNULL(sourceRfqItem.Description, N'')))))
                END
            FROM RequestForQuotationItems sourceRfqItem
            WHERE sourceRfqItem.Id = @SourceRfqItemId
              AND sourceRfqItem.TenantId = @TenantId
              AND sourceRfqItem.IsDeleted = 0;

            RETURN @identity;
        END;
        """;
}
