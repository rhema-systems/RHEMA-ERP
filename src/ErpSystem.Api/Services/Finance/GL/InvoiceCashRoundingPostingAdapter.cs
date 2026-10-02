using System.Globalization;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

internal static class InvoiceCashRoundingPostingAdapter
{
    internal const string GainTag = "Finance-Rounding-Gain";
    internal const string LossTag = "Finance-Rounding-Loss";

    internal static async Task ApplyAsync(ApplicationDbContext db, Guid tenantId,
        FinancePostingCommandDto request, string functionalCurrency, int functionalPlaces,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.PostingAction, "Post", StringComparison.OrdinalIgnoreCase)
            || request.ReversalOfJournalEntryId.HasValue || request.Lines.Any(IsRoundingLine))
            return;
        var anchor = FindAnchor(request);
        if (anchor is null) return;
        var settings = await db.FinanceSettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken);
        if (settings is not { InvoiceRoundingEnabled: true }) return;
        var increment = settings.InvoiceRoundingIncrement
            ?? throw new InvalidOperationException("Invoice/cash rounding is enabled without an increment.");
        var gainId = settings.InvoiceRoundingGainAccountId
            ?? throw new InvalidOperationException("Invoice/cash rounding is enabled without a gain account.");
        var lossId = settings.InvoiceRoundingLossAccountId
            ?? throw new InvalidOperationException("Invoice/cash rounding is enabled without a loss account.");
        await RequireAccountAsync(db, tenantId, gainId, AccountType.Revenue, "gain", cancellationToken);
        await RequireAccountAsync(db, tenantId, lossId, AccountType.Expense, "loss", cancellationToken);

        var currency = NormalizeCurrency(anchor.TransactionCurrency, functionalCurrency);
        var places = await ResolvePlacesAsync(db, tenantId, currency, cancellationToken);
        var minorUnit = CurrencyMinorUnitPolicy.MinorUnit(places);
        if (increment < minorUnit || increment % minorUnit != 0m)
            throw new InvalidOperationException(
                $"Invoice/cash rounding increment {increment.ToString(CultureInfo.InvariantCulture)} must be a whole multiple of the {currency} minor unit {minorUnit.ToString(CultureInfo.InvariantCulture)}.");
        var anchorDebit = IsDebit(anchor);
        var original = TransactionAmount(anchor, anchorDebit, currency, functionalCurrency);
        var rounded = CurrencyMinorUnitPolicy.Round(
            PrecisionRoundingPolicy.RoundToIncrement(original, increment, settings.InvoiceRoundingMethod), places);
        var delta = CurrencyMinorUnitPolicy.Round(rounded - original, places);
        if (delta == 0m) return;
        if (rounded <= 0m)
            throw new InvalidOperationException("Invoice/cash rounding cannot reduce the posting anchor to zero or below.");
        var rate = currency == functionalCurrency ? 1m : anchor.ExchangeRate
            ?? throw new InvalidOperationException("Foreign-currency rounding requires immutable exchange-rate evidence.");
        var functionalDelta = CurrencyMinorUnitPolicy.Round(decimal.Abs(delta) * rate, functionalPlaces);
        if (functionalDelta == 0m)
            throw new InvalidOperationException("The rounding delta is below the functional-currency minor unit.");

        ApplyAnchor(anchor, anchorDebit, delta, functionalDelta, rounded, currency == functionalCurrency);
        var gain = anchorDebit ? delta > 0m : delta < 0m;
        request.Lines = request.Lines.Concat(new[] { BuildLine(request, anchor,
            gain ? gainId : lossId, gain, anchorDebit, delta, functionalDelta, original,
            rounded, increment, settings.InvoiceRoundingMethod, currency, functionalCurrency, rate) }).ToArray();
    }

    private static FinancePostingLineDto? FindAnchor(FinancePostingCommandDto request)
    {
        var tags = (request.SourceModule.ToUpperInvariant(), request.SourceDocumentType) switch
        {
            ("AR", "CustomerInvoice") => new[] { "AR-Control" },
            ("AP", "VendorInvoice") => new[] { "AP-Control" },
            ("AR", "CustomerPayment") => new[] { "AR-Bank", "AR-Liquidity" },
            ("AP", "VendorPayment") => new[] { "AP-Bank" },
            ("CASHBANK", _) => new[] { "CashBankReceipt.Bank", "CashBankPayment.Bank" },
            _ => []
        };
        var matches = request.Lines.Where(x => tags.Contains(x.TransactionTag, StringComparer.Ordinal)).ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException("Invoice/cash rounding requires exactly one canonical anchor line.");
        return matches.SingleOrDefault();
    }

    private static bool IsDebit(FinancePostingLineDto line)
    {
        if (line.DebitAmount > 0m && line.CreditAmount == 0m) return true;
        if (line.CreditAmount > 0m && line.DebitAmount == 0m) return false;
        throw new InvalidOperationException("Invoice/cash rounding anchor must contain one debit or credit.");
    }

    private static decimal TransactionAmount(FinancePostingLineDto line, bool debit,
        string currency, string functionalCurrency)
    {
        var amount = debit ? line.TransactionDebitAmount : line.TransactionCreditAmount;
        amount ??= currency != functionalCurrency ? line.ForeignCurrencyAmount
            : debit ? line.DebitAmount : line.CreditAmount;
        return amount is > 0m ? amount.Value
            : throw new InvalidOperationException("Invoice/cash rounding anchor has no positive amount.");
    }

    private static void ApplyAnchor(FinancePostingLineDto line, bool debit, decimal delta,
        decimal functionalDelta, decimal rounded, bool functional)
    {
        var signed = delta > 0m ? functionalDelta : -functionalDelta;
        if (debit)
        {
            line.DebitAmount += signed;
            line.TransactionDebitAmount = rounded;
        }
        else
        {
            line.CreditAmount += signed;
            line.TransactionCreditAmount = rounded;
        }
        if (!functional) line.ForeignCurrencyAmount = rounded;
    }

    private static FinancePostingLineDto BuildLine(FinancePostingCommandDto request,
        FinancePostingLineDto anchor, Guid accountId, bool gain, bool anchorDebit,
        decimal delta, decimal functionalDelta, decimal original, decimal rounded,
        decimal increment, GovernedRoundingMethod method, string currency,
        string functionalCurrency, decimal rate)
    {
        var debit = anchorDebit ? delta < 0m : delta > 0m;
        var amount = decimal.Abs(delta);
        var foreign = currency != functionalCurrency;
        return new FinancePostingLineDto
        {
            AccountId = accountId,
            Description = $"Invoice/cash rounding {(gain ? "gain" : "loss")} - {request.SourceDocumentReference ?? request.SourceDocumentId.ToString("N")}",
            DebitAmount = debit ? functionalDelta : 0m,
            CreditAmount = debit ? 0m : functionalDelta,
            TransactionCurrency = currency,
            TransactionDebitAmount = debit ? amount : 0m,
            TransactionCreditAmount = debit ? 0m : amount,
            ForeignCurrencyAmount = foreign ? amount : null,
            ExchangeRateId = foreign ? anchor.ExchangeRateId : null,
            ExchangeRate = foreign ? rate : null,
            ExchangeRateSource = foreign ? anchor.ExchangeRateSource : null,
            ExchangeRateDate = foreign ? anchor.ExchangeRateDate : null,
            SourceReferenceNumber = anchor.SourceReferenceNumber ?? request.SourceDocumentReference,
            LineNumber = request.Lines.Select(x => x.LineNumber ?? 0).DefaultIfEmpty().Max() + 1,
            Dimensions = anchor.Dimensions,
            Notes = FormattableString.Invariant(
                $"FinanceRounding:v1;Original={original};Rounded={rounded};Delta={delta};Increment={increment};Method={method};Currency={currency};Anchor={anchor.TransactionTag}"),
            TransactionTag = gain ? GainTag : LossTag
        };
    }

    private static bool IsRoundingLine(FinancePostingLineDto line) =>
        string.Equals(line.TransactionTag, GainTag, StringComparison.Ordinal)
        || string.Equals(line.TransactionTag, LossTag, StringComparison.Ordinal);

    private static async Task RequireAccountAsync(ApplicationDbContext db, Guid tenantId,
        Guid accountId, AccountType type, string label, CancellationToken cancellationToken)
    {
        var valid = accountId != Guid.Empty && await db.Accounts.AsNoTracking().AnyAsync(x =>
            x.Id == accountId && x.TenantId == tenantId && !x.IsDeleted
            && x.Status == AccountStatus.Active && x.AllowDirectPosting && !x.IsControlAccount
            && x.AccountType == type, cancellationToken);
        if (!valid)
            throw new InvalidOperationException(
                $"Invoice rounding {label} account must be an active {type} direct-posting account belonging to the tenant.");
    }

    private static async Task<int> ResolvePlacesAsync(ApplicationDbContext db, Guid tenantId,
        string currency, CancellationToken cancellationToken)
    {
        var configured = await db.Currencies.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.CurrencyCode == currency)
            .Select(x => (int?)x.DecimalPlaces).SingleOrDefaultAsync(cancellationToken);
        if (configured.HasValue)
        {
            CurrencyMinorUnitPolicy.Validate(currency, configured.Value);
            return configured.Value;
        }
        return CurrencyMinorUnitPolicy.ExpectedDecimalPlaces(currency)
            ?? throw new InvalidOperationException($"Currency '{currency}' is not configured for invoice/cash rounding.");
    }

    private static string NormalizeCurrency(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToUpperInvariant();
}
