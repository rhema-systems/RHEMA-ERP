using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Additive compatibility migration for databases that applied the supplier debit-note lifecycle
/// before its approved-rate snapshot was widened. The guard makes the operation safe whether the
/// preceding migration created the column at six decimals or inherited the legacy four decimals.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260818123000_WidenSupplierDebitNoteExchangeRatePrecision")]
public class WidenSupplierDebitNoteExchangeRatePrecision : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF EXISTS
(
    SELECT 1
    FROM sys.columns c
    JOIN sys.types t ON t.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID(N'[dbo].[SupplierDebitNotes]')
      AND c.[name] = N'ExchangeRate'
      AND t.[name] IN (N'decimal', N'numeric')
      AND c.[scale] < 6
)
    ALTER TABLE [dbo].[SupplierDebitNotes] ALTER COLUMN [ExchangeRate] decimal(18,6) NOT NULL;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException(
            "Supplier debit-note FX evidence cannot be narrowed after migration.");
    }
}
