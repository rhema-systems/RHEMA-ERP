using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ErpSystem.Data.ApplicationDbContext))]
[Migration("20260730030000_TDC0403RfqAwardSupplierHardStop")]
public partial class TDC0403RfqAwardSupplierHardStop : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @definition nvarchar(max) =
                OBJECT_DEFINITION(OBJECT_ID(N'[TR_PurchaseOrders_ApprovedSourceProtected]'));
            IF @definition IS NULL
                THROW 51216, 'The approved-source protection trigger is unavailable.', 1;

            IF CHARINDEX(
                    N'awardLine.BusinessPartnerId = i.BusinessPartnerId',
                    @definition) = 0
            BEGIN
                DECLARE @needle nvarchar(max) =
                    N'OR readiness.SourceId <> rfq.Id))';
                IF CHARINDEX(@needle, @definition) = 0
                    THROW 51216, 'The RFQ source guard could not be upgraded safely.', 1;

                DECLARE @replacement nvarchar(max) =
                    N'OR readiness.SourceId <> rfq.Id
                        OR NOT EXISTS (
                            SELECT 1
                            FROM RequestForQuotationAwardLines awardLine
                            WHERE awardLine.TenantId = i.TenantId
                              AND awardLine.RfqId = rfq.Id
                              AND awardLine.BusinessPartnerId = i.BusinessPartnerId
                              AND awardLine.IsDeleted = 0)))';
                SET @definition = REPLACE(@definition, @needle, @replacement);
                EXEC sys.sp_executesql @definition;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Retain the supplier-award hard stop when rolling back this corrective
        // migration so a downgrade cannot reopen the direct-SQL bypass.
    }
}
