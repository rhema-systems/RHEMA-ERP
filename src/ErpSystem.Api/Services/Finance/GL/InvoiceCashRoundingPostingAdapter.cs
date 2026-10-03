using System.Globalization;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
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
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new InvalidOperationException("Invoice/cash rounding requires a posting idempotency key.");

        var evidence = await db.FinanceRoundingEvidence.SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && !x.IsDeleted &&
            x.PostingIdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (evidence is null)
        {
            var source = await ResolveSourceAsync(db, tenantId, request, anchor,
                functionalCurrency, cancellationToken);
            if (source is null) return;
            evidence = await CreateEvidenceAsync(db, tenantId, request, anchor, source.Value,
                functionalCurrency, functionalPlaces, cancellationToken);
            if (evidence is null) return;
            db.FinanceRoundingEvidence.Add(evidence);
        }
        else
        {
            EnsureEvidenceMatchesRequest(evidence, request);
        }

        request.FinanceRoundingEvidenceId = evidence.Id;
        await ApplySourceAsync(db, tenantId, request, evidence, cancellationToken);
        ApplyFrozenDecision(request, anchor, evidence, functionalCurrency);
    }

    private static async Task<FinanceRoundingEvidence?> CreateEvidenceAsync(
        ApplicationDbContext db, Guid tenantId, FinancePostingCommandDto request,
        FinancePostingLineDto anchor, RoundingSource source, string functionalCurrency,
        int functionalPlaces, CancellationToken cancellationToken)
    {
        var settings = await db.FinanceSettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken);
        if (settings is not { InvoiceRoundingEnabled: true }) return null;
        var increment = settings.InvoiceRoundingIncrement
            ?? throw new InvalidOperationException("Invoice/cash rounding is enabled without an increment.");
        var gainId = settings.InvoiceRoundingGainAccountId
            ?? throw new InvalidOperationException("Invoice/cash rounding is enabled without a gain account.");
        var lossId = settings.InvoiceRoundingLossAccountId
            ?? throw new InvalidOperationException("Invoice/cash rounding is enabled without a loss account.");
        await RequireAccountAsync(db, tenantId, gainId, AccountType.Revenue, "gain", cancellationToken);
        await RequireAccountAsync(db, tenantId, lossId, AccountType.Expense, "loss", cancellationToken);

        var currency = source.Currency;
        var places = await ResolvePlacesAsync(db, tenantId, currency, cancellationToken);
        var minorUnit = CurrencyMinorUnitPolicy.MinorUnit(places);
        if (increment < minorUnit || increment % minorUnit != 0m)
            throw new InvalidOperationException(
                $"Invoice/cash rounding increment {increment.ToString(CultureInfo.InvariantCulture)} must be a whole multiple of the {currency} minor unit {minorUnit.ToString(CultureInfo.InvariantCulture)}.");
        var original = source.OriginalAmount;
        var rounded = CurrencyMinorUnitPolicy.Round(
            PrecisionRoundingPolicy.RoundToIncrement(original, increment, settings.InvoiceRoundingMethod), places);
        var delta = CurrencyMinorUnitPolicy.Round(rounded - original, places);
        if (rounded <= 0m)
            throw new InvalidOperationException("Invoice/cash rounding cannot reduce the posting anchor to zero or below.");
        var rate = currency == functionalCurrency ? 1m : source.ExchangeRate
            ?? throw new InvalidOperationException("Foreign-currency rounding requires immutable exchange-rate evidence.");
        var originalFunctional = CurrencyMinorUnitPolicy.Round(original * rate, functionalPlaces);
        var roundedFunctional = CurrencyMinorUnitPolicy.Round(rounded * rate, functionalPlaces);
        var functionalDelta = roundedFunctional - originalFunctional;
        if (delta != 0m && functionalDelta == 0m)
            throw new InvalidOperationException("The rounding delta is below the functional-currency minor unit.");
        return new FinanceRoundingEvidence
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            PostingIdempotencyKey = request.IdempotencyKey!,
            SourceModule = request.SourceModule, SourceDocumentType = request.SourceDocumentType,
            SourceDocumentId = request.SourceDocumentId, PostingAction = request.PostingAction,
            Eligibility = source.Eligibility, CurrencyCode = currency, DecimalPlaces = places,
            OriginalAmount = original, RoundedAmount = rounded, DeltaAmount = delta,
            Increment = increment, Method = settings.InvoiceRoundingMethod,
            FunctionalCurrencyCode = functionalCurrency, FunctionalDecimalPlaces = functionalPlaces,
            OriginalFunctionalAmount = originalFunctional, RoundedFunctionalAmount = roundedFunctional,
            FunctionalDeltaAmount = functionalDelta, ExchangeRateId = source.ExchangeRateId,
            ExchangeRate = rate, ExchangeRateSource = source.ExchangeRateSource,
            ExchangeRateDate = source.ExchangeRateDate, GainAccountId = gainId, LossAccountId = lossId
        };
    }

    private static void ApplyFrozenDecision(FinancePostingCommandDto request,
        FinancePostingLineDto anchor, FinanceRoundingEvidence evidence, string functionalCurrency)
    {
        if (evidence.DeltaAmount == 0m) return;
        var anchorDebit = IsDebit(anchor);
        ApplyAnchor(anchor, anchorDebit, evidence.DeltaAmount,
            decimal.Abs(evidence.FunctionalDeltaAmount), evidence.RoundedAmount,
            evidence.CurrencyCode == functionalCurrency);
        var gain = anchorDebit ? evidence.DeltaAmount > 0m : evidence.DeltaAmount < 0m;
        request.Lines = request.Lines.Concat(new[] { BuildLine(request, anchor,
            gain ? evidence.GainAccountId : evidence.LossAccountId, gain, anchorDebit,
            evidence.DeltaAmount, decimal.Abs(evidence.FunctionalDeltaAmount), evidence.OriginalAmount,
            evidence.RoundedAmount, evidence.Increment, evidence.Method, evidence.CurrencyCode,
            functionalCurrency, evidence.ExchangeRate, evidence.Id) }).ToArray();
    }

    private static async Task<RoundingSource?> ResolveSourceAsync(
        ApplicationDbContext db, Guid tenantId, FinancePostingCommandDto request,
        FinancePostingLineDto anchor, string functionalCurrency, CancellationToken cancellationToken)
    {
        var key = (request.SourceModule.ToUpperInvariant(), request.SourceDocumentType);
        if (key == ("AR", "CustomerInvoice"))
        {
            var source = await db.Invoices.AsNoTracking().SingleOrDefaultAsync(x =>
                x.TenantId == tenantId && x.Id == request.SourceDocumentId && !x.IsDeleted, cancellationToken)
                ?? throw new InvalidOperationException("Customer invoice rounding source was not found.");
            return Source(FinanceRoundingEligibility.Invoice, source.CurrencyCode, source.TotalAmount,
                source.ExchangeRateId, source.ExchangeRate, anchor);
        }
        if (key == ("AP", "VendorInvoice"))
        {
            var source = await db.VendorInvoices.AsNoTracking().SingleOrDefaultAsync(x =>
                x.TenantId == tenantId && x.Id == request.SourceDocumentId && !x.IsDeleted, cancellationToken)
                ?? throw new InvalidOperationException("Vendor invoice rounding source was not found.");
            return Source(FinanceRoundingEligibility.Invoice, source.CurrencyCode, source.TotalAmount,
                source.ExchangeRateId, source.ExchangeRate, anchor);
        }
        if (key == ("AR", "CustomerPayment"))
        {
            var source = await db.Set<CustomerPayment>().AsNoTracking()
                .Include(x => x.ConfiguredPaymentMethod).Include(x => x.LiquidityAccount)
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.SourceDocumentId && !x.IsDeleted,
                    cancellationToken)
                ?? throw new InvalidOperationException("Customer receipt rounding source was not found.");
            if (source.ConfiguredPaymentMethod?.Type != PaymentMethodType.Cash
                || source.LiquidityAccount?.AccountType != LiquidityAccountType.CashTill)
                return null;
            return Source(FinanceRoundingEligibility.CashTillTender, source.CurrencyCode,
                source.TotalAmount, source.ExchangeRateId, source.ExchangeRate, anchor);
        }
        if (key == ("AP", "VendorPayment"))
        {
            var source = await db.Set<VendorPayment>().AsNoTracking().SingleOrDefaultAsync(x =>
                x.TenantId == tenantId && x.Id == request.SourceDocumentId && !x.IsDeleted, cancellationToken)
                ?? throw new InvalidOperationException("Vendor payment rounding source was not found.");
            if (source.PaymentMethod != VendorPaymentMethod.Cash) return null;
            return Source(FinanceRoundingEligibility.CashTillTender, source.CurrencyCode,
                source.TotalAmount, source.ExchangeRateId, source.ExchangeRate, anchor);
        }
        if (key.Item1 == "CASHBANK")
        {
            var source = await db.Set<CashTransaction>().AsNoTracking().Include(x => x.PaymentMethod)
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.SourceDocumentId && !x.IsDeleted,
                    cancellationToken)
                ?? throw new InvalidOperationException("Cash/bank rounding source was not found.");
            if (source.TransactionType == CashTransactionType.Transfer
                || source.PaymentMethod?.Type != PaymentMethodType.Cash)
                return null;
            return Source(FinanceRoundingEligibility.CashTillTender, source.Currency,
                source.Amount, source.ExchangeRateId, source.ExchangeRate, anchor);
        }
        return null;

        RoundingSource Source(FinanceRoundingEligibility eligibility, string? currency,
            decimal amount, Guid? exchangeRateId, decimal? exchangeRate, FinancePostingLineDto line) =>
            new(eligibility, NormalizeCurrency(currency, functionalCurrency), amount,
                exchangeRateId ?? line.ExchangeRateId, exchangeRate ?? line.ExchangeRate,
                line.ExchangeRateSource, line.ExchangeRateDate);
    }

    private static async Task ApplySourceAsync(ApplicationDbContext db, Guid tenantId,
        FinancePostingCommandDto request, FinanceRoundingEvidence evidence,
        CancellationToken cancellationToken)
    {
        var key = (request.SourceModule.ToUpperInvariant(), request.SourceDocumentType);
        if (key == ("AR", "CustomerInvoice"))
        {
            var source = await db.Invoices.SingleAsync(x => x.TenantId == tenantId
                && x.Id == request.SourceDocumentId && !x.IsDeleted, cancellationToken);
            var changed = Apply(source.TotalAmount, evidence, out var rounded);
            source.TotalAmount = rounded;
            source.BaseCurrencyAmount = evidence.RoundedFunctionalAmount;
            source.RoundingAdjustmentAmount = evidence.DeltaAmount;
            source.FinanceRoundingEvidenceId = evidence.Id;
            var partner = await db.Set<ErpSystem.Core.Entities.Procurement.BusinessPartner>()
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == source.BusinessPartnerId && !x.IsDeleted,
                    cancellationToken);
            if (changed && partner is not null)
                partner.OutstandingBalance = (partner.OutstandingBalance ?? 0m) + evidence.FunctionalDeltaAmount;
            return;
        }
        if (key == ("AP", "VendorInvoice"))
        {
            var source = await db.VendorInvoices.SingleAsync(x => x.TenantId == tenantId
                && x.Id == request.SourceDocumentId && !x.IsDeleted, cancellationToken);
            Apply(source.TotalAmount, evidence, out var rounded);
            source.TotalAmount = rounded;
            source.BaseCurrencyAmount = evidence.RoundedFunctionalAmount;
            source.RoundingAdjustmentAmount = evidence.DeltaAmount;
            source.FinanceRoundingEvidenceId = evidence.Id;
            return;
        }
        if (key == ("AR", "CustomerPayment"))
        {
            var source = await db.Set<CustomerPayment>().SingleAsync(x => x.TenantId == tenantId
                && x.Id == request.SourceDocumentId && !x.IsDeleted, cancellationToken);
            if (Apply(source.TotalAmount, evidence, out var rounded)) source.TotalAmount = rounded;
            source.RoundingAdjustmentAmount = evidence.DeltaAmount;
            source.FinanceRoundingEvidenceId = evidence.Id;
            return;
        }
        if (key == ("AP", "VendorPayment"))
        {
            var source = await db.Set<VendorPayment>().SingleAsync(x => x.TenantId == tenantId
                && x.Id == request.SourceDocumentId && !x.IsDeleted, cancellationToken);
            if (Apply(source.TotalAmount, evidence, out var rounded)) source.TotalAmount = rounded;
            source.RoundingAdjustmentAmount = evidence.DeltaAmount;
            source.FinanceRoundingEvidenceId = evidence.Id;
            return;
        }
        if (key.Item1 == "CASHBANK")
        {
            var source = await db.Set<CashTransaction>().SingleAsync(x => x.TenantId == tenantId
                && x.Id == request.SourceDocumentId && !x.IsDeleted, cancellationToken);
            if (Apply(source.Amount, evidence, out var rounded))
            {
                source.Amount = rounded;
                source.BaseAmount = evidence.RoundedFunctionalAmount;
            }
            source.RoundingAdjustmentAmount = evidence.DeltaAmount;
            source.FinanceRoundingEvidenceId = evidence.Id;
        }

        static bool Apply(decimal sourceAmount, FinanceRoundingEvidence frozen, out decimal rounded)
        {
            rounded = sourceAmount;
            if (sourceAmount == frozen.RoundedAmount) return false;
            if (sourceAmount != frozen.OriginalAmount)
                throw new InvalidOperationException("The source amount no longer matches its frozen rounding evidence.");
            rounded = frozen.RoundedAmount;
            return true;
        }
    }

    private static void EnsureEvidenceMatchesRequest(FinanceRoundingEvidence evidence,
        FinancePostingCommandDto request)
    {
        if (!string.Equals(evidence.SourceModule, request.SourceModule, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(evidence.SourceDocumentType, request.SourceDocumentType, StringComparison.Ordinal)
            || evidence.SourceDocumentId != request.SourceDocumentId
            || !string.Equals(evidence.PostingAction, request.PostingAction, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The posting idempotency key belongs to different rounding evidence.");
    }

    private readonly record struct RoundingSource(
        FinanceRoundingEligibility Eligibility,
        string Currency,
        decimal OriginalAmount,
        Guid? ExchangeRateId,
        decimal? ExchangeRate,
        string? ExchangeRateSource,
        DateTime? ExchangeRateDate);

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
        string functionalCurrency, decimal rate, Guid evidenceId)
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
                $"FinanceRounding:v2;Evidence={evidenceId:N};Original={original};Rounded={rounded};Delta={delta};Increment={increment};Method={method};Currency={currency};Anchor={anchor.TransactionTag}"),
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
