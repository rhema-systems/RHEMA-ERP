using ErpSystem.Core.Interfaces.Finance;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// HR's read-only window onto Finance's currency and exchange-rate masters.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> Travel shipped its own <c>StaffTravelCurrencyExchangeRate</c> —
/// six fields, no rate type, no quote side, no approval, no provenance — beside Finance's
/// <c>ExchangeRate</c>, which has all of those and is sourced "Bank of Ghana" on the reference
/// database. Two rate tables mean travel spend and Finance reporting can silently disagree about
/// what a trip cost. This is the read side of retiring the duplicate.</para>
///
/// <para><b>Read-only, deliberately.</b> Nothing here creates a currency or a rate. Finance owns
/// both masters; travel consumes them. If a rate is missing the answer is to add it in Finance, not
/// to let travel invent one — which is precisely what the retired table allowed.</para>
///
/// <para>Not an interface of its own: it is a thin composition over two Finance services that
/// several HR services need, and giving it a bespoke abstraction would only obscure where the data
/// actually comes from.</para>
///
/// <para><b>⚠ Renamed from travel-specific to HR-wide on 2026-09-01, because this file said to.</b>
/// Its own note read: <i>"Same division StaffTravelCurrencyBridge settled for travel; when a third
/// area needs this the two should become one HR-wide bridge."</i> Guarantor sureties (lane 3a) are
/// that third area, so the implementation moved here. Travel kept a thin alias,
/// <c>StaffTravelCurrencyBridge</c>, until the travel final closure retired it (2026-10-01, lane 0):
/// the travel services now take this class directly.</para>
///
/// <para><b>One caller still keeps its own copy.</b> <c>SeparationService.ResolveCurrencyAsync</c>
/// has its own settings-then-base fallback; moving it is a change worth making with the separation
/// suites green, not as a side effect of another area's slice.</para>
/// </remarks>
public class HrCurrencyBridge
{
    private readonly ICurrencyService _currencies;
    private readonly IExchangeRateService _rates;

    public HrCurrencyBridge(ICurrencyService currencies, IExchangeRateService rates)
    {
        _currencies = currencies;
        _rates = rates;
    }

    /// <summary>
    /// Confirms a currency code is one Finance actually knows.
    /// </summary>
    /// <remarks>
    /// Every travel money field carried a bare <c>char(3)</c> that nothing validated, so a claim
    /// could be filed in "XYZ" and the total would happily add it up. The code stays a code — no
    /// FK column is added, because Finance's uniqueness is <c>(TenantId, Code)</c> and a composite
    /// FK across eleven travel tables would buy little over this check while making a currency
    /// re-code a schema problem.
    /// </remarks>
    /// <param name="optional">
    /// True where the field is genuinely nullable — a visa application may record no fee at all.
    /// An absent optional currency is fine; a PRESENT one still has to be real.
    /// </param>
    public async Task RequireKnownCurrencyAsync(
        string? currencyCode, CancellationToken cancellationToken = default, bool optional = false)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            if (optional) return;
            throw new InvalidOperationException("A currency code is required.");
        }

        var currency = await _currencies.GetByCodeAsync(currencyCode.Trim().ToUpperInvariant(), cancellationToken);
        if (currency is null)
            throw new InvalidOperationException(
                // ⚠ Area-neutral wording. This said "before using it on travel" until the bridge
                // stopped being travel's — and a guarantor's surety then refused with a sentence
                // about trips. A shared component's messages have to survive its callers.
                $"'{currencyCode}' is not a currency this organisation holds. Add it in Finance before using it.");
    }

    /// <summary>
    /// The rate to convert <paramref name="fromCurrency"/> into the organisation's base currency,
    /// obtained by asking Finance to convert one unit.
    /// </summary>
    /// <remarks>
    /// <para><b>Why it delegates instead of reading <c>ExchangeRate.Rate</c> itself.</b> Travel must
    /// not hold a second opinion about what a currency is worth — that is the whole point of
    /// retiring <c>StaffTravelCurrencyExchangeRate</c>. Converting one unit through Finance's own
    /// <c>ConvertAsync</c> means travel agrees with Finance by construction: if Finance's
    /// conversion changes, travel changes with it, and a trip can never be worth one thing on a
    /// travel screen and another on a financial report.</para>
    ///
    /// <para>⚠ <b>RESOLVED 2026-09-10.</b> Finance PR #99 (<c>finance-fx-seed-contract</c>) transposed
    /// the seed and documented the contract; <c>GET /api/finance/exchange-rates/current/USD</c> now
    /// reads <c>rate 12.5, inverseRate 0.08</c> and this bridge answers 12.5 GHS per USD (asserted
    /// by <c>hr-jobarch/run-r7.mjs</c>). The paragraphs below are kept as the record of what was
    /// wrong and why this class inherited it rather than working around it.</para>
    ///
    /// <para><b>Finance's conversion WAS inverted, and this deliberately inherited that.</b>
    /// Measured 2026-08-17: <c>GET /api/finance/currencies/convert</c> answers
    /// <c>1 USD = 0.08 GHS</c> and <c>1 GHS = 12.5 USD</c> — reciprocals of the truth. The cause is
    /// in Finance, not here: <c>FinanceDataSeeder</c> writes <c>Rate = 0.08</c> meaning "1 GHS =
    /// 0.08 USD" (base→target), while both the <c>ExchangeRate</c> documentation and
    /// <c>CurrencyService.ConvertAsync</c> read <c>Rate</c> as "1 Target = Rate Base". Rate and
    /// InverseRate are transposed relative to the code that consumes them.</para>
    ///
    /// <para>Reading <c>InverseRate</c> here would make travel numerically right today and put it
    /// in open disagreement with every other module — two truths about the same trip, which is
    /// worse than one shared, fixable error. Reported instead: see
    /// <c>docs/HR/integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md</c> §2. Fixing Finance fixes travel with no
    /// change here.</para>
    /// </remarks>
    public async Task<decimal> GetRateToBaseAsync(
        string fromCurrency, DateOnly asOf, CancellationToken cancellationToken = default)
    {
        await RequireKnownCurrencyAsync(fromCurrency, cancellationToken);
        var code = fromCurrency.Trim().ToUpperInvariant();

        var baseCurrency = await _currencies.GetBaseCurrencyAsync(cancellationToken);
        var baseCode = baseCurrency?.CurrencyCode;

        // Same currency needs no rate row at all — asking Finance would fail an ordinary
        // domestic claim.
        if (string.IsNullOrWhiteSpace(baseCode) ||
            string.Equals(baseCode, code, StringComparison.OrdinalIgnoreCase))
            return 1m;

        // Refuse before converting if Finance holds no rate for the pair: ConvertAsync returns the
        // amount UNCHANGED when it finds none, which would silently value a foreign claim as
        // though it were local — the same class of error slice 4 removed from the amount.
        var published = await _rates.GetCurrentRateAsync(
            targetCurrencyCode: code,
            baseCurrencyCode: baseCode,
            effectiveDate: asOf.ToDateTime(TimeOnly.MinValue),
            cancellationToken: cancellationToken);

        if (published is null || published.Rate <= 0m)
            throw new InvalidOperationException(
                $"Finance holds no exchange rate for {code} on {asOf:yyyy-MM-dd}. " +
                "Add the rate in Finance, then resubmit — travel does not keep its own rates.");

        var rate = await _currencies.ConvertAsync(1m, code, baseCode, cancellationToken);
        if (rate <= 0m)
            throw new InvalidOperationException(
                $"Finance could not convert {code} to {baseCode}. Check the rate in Finance and resubmit.");

        return rate;
    }

    /// <summary>The organisation's base currency code, or null when Finance marks none.</summary>
    public async Task<string?> GetBaseCurrencyCodeAsync(CancellationToken cancellationToken = default)
        => (await _currencies.GetBaseCurrencyAsync(cancellationToken))?.CurrencyCode;

    /// <summary>
    /// Expresses an amount in another currency, through the base currency, at the rates Finance
    /// holds — so a figure in one currency can be compared with a limit set in another.
    /// </summary>
    /// <remarks>
    /// Added for the travel policy's single-trip limit (travel final closure, lane 1): a trip costed
    /// in USD against a limit set in GHS. Each leg is <see cref="GetRateToBaseAsync"/>, so it refuses
    /// in the same words when Finance holds no rate, and it inherits that method's rate date (which
    /// travel's lane 3 corrects for back-dated expenses). The same currency needs no rate at all.
    /// </remarks>
    public async Task<decimal> ConvertBetweenAsync(
        decimal amount, string fromCurrency, string toCurrency, DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(fromCurrency?.Trim(), toCurrency?.Trim(), StringComparison.OrdinalIgnoreCase))
            return amount;

        var fromRate = await GetRateToBaseAsync(fromCurrency!, asOf, cancellationToken);
        var toRate = await GetRateToBaseAsync(toCurrency!, asOf, cancellationToken);
        return amount * fromRate / toRate;
    }
}
