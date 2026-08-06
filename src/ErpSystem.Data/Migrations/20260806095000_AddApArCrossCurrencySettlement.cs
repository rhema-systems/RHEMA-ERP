using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds the immutable currency/rate evidence required to settle AP and AR invoices from a
/// different payment currency. This migration is intentionally hand-scoped because the merged
/// repository model currently contains unrelated snapshot drift; accepting EF's generated diff
/// would recreate unrelated module tables.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260806095000_AddApArCrossCurrencySettlement")]
public class AddApArCrossCurrencySettlement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The entity contract already uses six-decimal rates. Align both payment headers so the
        // bank leg cannot be rounded to four decimals while its allocation snapshot keeps six.
        migrationBuilder.AlterColumn<decimal>(
            "ExchangeRate", "VendorPayment", "decimal(18,6)", nullable: false,
            oldClrType: typeof(decimal), oldType: "decimal(18,4)");
        migrationBuilder.AlterColumn<decimal>(
            "ExchangeRate", "CustomerPayment", "decimal(18,6)", nullable: false,
            oldClrType: typeof(decimal), oldType: "decimal(18,4)");

        // Header rate ids identify the approved rate-master row; the existing decimal rate is
        // retained as the immutable value used for deterministic posting and reversal.
        migrationBuilder.AddColumn<Guid>("ExchangeRateId", "VendorPayment", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("ExchangeRateId", "CustomerPayment", "uniqueidentifier", nullable: true);

        AddAllocationCurrencyEvidence(migrationBuilder, "VendorPaymentAllocation");
        AddAllocationCurrencyEvidence(migrationBuilder, "PaymentAllocation");

        BackfillExistingAllocationCurrencyEvidence(migrationBuilder);

        // Realized-FX rows must be understandable without joining back to mutable operational
        // projections, so they retain the payment-side currency evidence as well.
        migrationBuilder.AddColumn<bool>("IsCrossCurrency", "FxRealizedSettlements", "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>("PaymentCurrencyCode", "FxRealizedSettlements", "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "GHS");
        migrationBuilder.AddColumn<decimal>("PaymentCurrencyAmount", "FxRealizedSettlements", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<Guid>("PaymentExchangeRateId", "FxRealizedSettlements", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<decimal>("PaymentExchangeRate", "FxRealizedSettlements", "decimal(18,6)", nullable: false, defaultValue: 1m);

        BackfillExistingRealizedFxPaymentEvidence(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("ExchangeRateId", "VendorPayment");
        migrationBuilder.DropColumn("ExchangeRateId", "CustomerPayment");

        DropAllocationCurrencyEvidence(migrationBuilder, "VendorPaymentAllocation");
        DropAllocationCurrencyEvidence(migrationBuilder, "PaymentAllocation");

        migrationBuilder.DropColumn("IsCrossCurrency", "FxRealizedSettlements");
        migrationBuilder.DropColumn("PaymentCurrencyCode", "FxRealizedSettlements");
        migrationBuilder.DropColumn("PaymentCurrencyAmount", "FxRealizedSettlements");
        migrationBuilder.DropColumn("PaymentExchangeRateId", "FxRealizedSettlements");
        migrationBuilder.DropColumn("PaymentExchangeRate", "FxRealizedSettlements");

        migrationBuilder.AlterColumn<decimal>(
            "ExchangeRate", "VendorPayment", "decimal(18,4)", nullable: false,
            oldClrType: typeof(decimal), oldType: "decimal(18,6)");
        migrationBuilder.AlterColumn<decimal>(
            "ExchangeRate", "CustomerPayment", "decimal(18,4)", nullable: false,
            oldClrType: typeof(decimal), oldType: "decimal(18,6)");
    }

    private static void AddAllocationCurrencyEvidence(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.AddColumn<decimal>("PaymentCurrencyAmount", table, "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<string>("InvoiceCurrencyCode", table, "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "GHS");
        migrationBuilder.AddColumn<string>("PaymentCurrencyCode", table, "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "GHS");
        migrationBuilder.AddColumn<bool>("IsCrossCurrency", table, "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<Guid>("InvoiceSettlementExchangeRateId", table, "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<decimal>("InvoiceSettlementExchangeRate", table, "decimal(18,6)", nullable: false, defaultValue: 1m);
        migrationBuilder.AddColumn<Guid>("PaymentExchangeRateId", table, "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<decimal>("PaymentExchangeRate", table, "decimal(18,6)", nullable: false, defaultValue: 1m);
        migrationBuilder.AddColumn<decimal>("PaymentFunctionalAmount", table, "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("SettlementFunctionalAmount", table, "decimal(18,2)", nullable: false, defaultValue: 0m);
    }

    private static void DropAllocationCurrencyEvidence(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.DropColumn("PaymentCurrencyAmount", table);
        migrationBuilder.DropColumn("InvoiceCurrencyCode", table);
        migrationBuilder.DropColumn("PaymentCurrencyCode", table);
        migrationBuilder.DropColumn("IsCrossCurrency", table);
        migrationBuilder.DropColumn("InvoiceSettlementExchangeRateId", table);
        migrationBuilder.DropColumn("InvoiceSettlementExchangeRate", table);
        migrationBuilder.DropColumn("PaymentExchangeRateId", table);
        migrationBuilder.DropColumn("PaymentExchangeRate", table);
        migrationBuilder.DropColumn("PaymentFunctionalAmount", table);
        migrationBuilder.DropColumn("SettlementFunctionalAmount", table);
    }

    private static void BackfillExistingAllocationCurrencyEvidence(MigrationBuilder migrationBuilder)
    {
        // Before this migration the application rejected cross-currency allocations, so an
        // existing legitimate row has one shared native currency and the payment header's frozen
        // rate is the only defensible settlement-rate snapshot. Fail loudly if hand-edited data
        // violates that invariant; silently inventing a cross-rate would corrupt AP/AR control.
        migrationBuilder.Sql(
            """
            IF EXISTS
            (
                SELECT 1
                FROM [VendorPaymentAllocation] AS [a]
                INNER JOIN [VendorPayment] AS [p] ON [p].[Id] = [a].[VendorPaymentId]
                INNER JOIN [VendorInvoice] AS [i] ON [i].[Id] = [a].[VendorInvoiceId]
                WHERE UPPER(LTRIM(RTRIM([p].[CurrencyCode]))) <> UPPER(LTRIM(RTRIM([i].[CurrencyCode])))
            )
                THROW 51000, 'Cannot safely backfill pre-migration AP cross-currency allocations. Recreate those development drafts through the controlled settlement workflow.', 1;

            IF EXISTS
            (
                SELECT 1
                FROM [PaymentAllocation] AS [a]
                INNER JOIN [CustomerPayment] AS [p] ON [p].[Id] = [a].[CustomerPaymentId]
                INNER JOIN [Invoices] AS [i] ON [i].[Id] = [a].[InvoiceId]
                WHERE UPPER(LTRIM(RTRIM([p].[CurrencyCode]))) <> UPPER(LTRIM(RTRIM([i].[CurrencyCode])))
            )
                THROW 51000, 'Cannot safely backfill pre-migration AR cross-currency allocations. Recreate those development drafts through the controlled settlement workflow.', 1;

            UPDATE [a]
            SET
                [PaymentCurrencyAmount] = [a].[AllocatedAmount],
                [InvoiceCurrencyCode] = UPPER(LTRIM(RTRIM([i].[CurrencyCode]))),
                [PaymentCurrencyCode] = UPPER(LTRIM(RTRIM([p].[CurrencyCode]))),
                [IsCrossCurrency] = 0,
                [InvoiceSettlementExchangeRate] = CASE WHEN [p].[ExchangeRate] > 0 THEN [p].[ExchangeRate] ELSE 1 END,
                [PaymentExchangeRate] = CASE WHEN [p].[ExchangeRate] > 0 THEN [p].[ExchangeRate] ELSE 1 END,
                [PaymentFunctionalAmount] = ROUND([a].[AllocatedAmount] * CASE WHEN [p].[ExchangeRate] > 0 THEN [p].[ExchangeRate] ELSE 1 END, 2),
                [SettlementFunctionalAmount] = ROUND(
                    ([a].[AllocatedAmount] + [a].[DiscountAmount] + [a].[WithholdingTaxAmount])
                    * CASE WHEN [p].[ExchangeRate] > 0 THEN [p].[ExchangeRate] ELSE 1 END,
                    2)
            FROM [VendorPaymentAllocation] AS [a]
            INNER JOIN [VendorPayment] AS [p] ON [p].[Id] = [a].[VendorPaymentId]
            INNER JOIN [VendorInvoice] AS [i] ON [i].[Id] = [a].[VendorInvoiceId];

            UPDATE [a]
            SET
                [PaymentCurrencyAmount] = [a].[AllocatedAmount],
                [InvoiceCurrencyCode] = UPPER(LTRIM(RTRIM([i].[CurrencyCode]))),
                [PaymentCurrencyCode] = UPPER(LTRIM(RTRIM([p].[CurrencyCode]))),
                [IsCrossCurrency] = 0,
                [InvoiceSettlementExchangeRate] = CASE WHEN [p].[ExchangeRate] > 0 THEN [p].[ExchangeRate] ELSE 1 END,
                [PaymentExchangeRate] = CASE WHEN [p].[ExchangeRate] > 0 THEN [p].[ExchangeRate] ELSE 1 END,
                [PaymentFunctionalAmount] = ROUND([a].[AllocatedAmount] * CASE WHEN [p].[ExchangeRate] > 0 THEN [p].[ExchangeRate] ELSE 1 END, 2),
                [SettlementFunctionalAmount] = ROUND(
                    ([a].[AllocatedAmount] + [a].[DiscountAmount])
                    * CASE WHEN [p].[ExchangeRate] > 0 THEN [p].[ExchangeRate] ELSE 1 END,
                    2)
            FROM [PaymentAllocation] AS [a]
            INNER JOIN [CustomerPayment] AS [p] ON [p].[Id] = [a].[CustomerPaymentId]
            INNER JOIN [Invoices] AS [i] ON [i].[Id] = [a].[InvoiceId];
            """);
    }

    private static void BackfillExistingRealizedFxPaymentEvidence(MigrationBuilder migrationBuilder)
    {
        // Realized-FX rows are immutable audit evidence. Copy the now-repaired allocation values
        // so reports do not label historical USD/EUR settlements as zero-value GHS payments.
        migrationBuilder.Sql(
            """
            UPDATE [fx]
            SET
                [PaymentCurrencyCode] = [a].[PaymentCurrencyCode],
                [PaymentCurrencyAmount] = [a].[PaymentCurrencyAmount],
                [PaymentExchangeRateId] = [a].[PaymentExchangeRateId],
                [PaymentExchangeRate] = [a].[PaymentExchangeRate],
                [IsCrossCurrency] = [a].[IsCrossCurrency]
            FROM [FxRealizedSettlements] AS [fx]
            INNER JOIN [VendorPaymentAllocation] AS [a] ON [a].[Id] = [fx].[SettlementAllocationId]
            WHERE [fx].[SettlementDocumentType] = N'VendorPayment';

            UPDATE [fx]
            SET
                [PaymentCurrencyCode] = [a].[PaymentCurrencyCode],
                [PaymentCurrencyAmount] = [a].[PaymentCurrencyAmount],
                [PaymentExchangeRateId] = [a].[PaymentExchangeRateId],
                [PaymentExchangeRate] = [a].[PaymentExchangeRate],
                [IsCrossCurrency] = [a].[IsCrossCurrency]
            FROM [FxRealizedSettlements] AS [fx]
            INNER JOIN [PaymentAllocation] AS [a] ON [a].[Id] = [fx].[SettlementAllocationId]
            WHERE [fx].[SettlementDocumentType] = N'CustomerPayment';
            """);
    }
}
