using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The currencies HR may name, read from Finance's master.
/// </summary>
/// <remarks>
/// <para><b>Why HR has its own read.</b> <c>api/finance/currencies</c> is mapped to
/// <c>FinancePermissions.ViewFinance</c> by <c>FinancePermissionPolicyMap</c> — a convention map,
/// so nothing on <c>CurrenciesController</c> shows it. An HR user therefore gets a 403, which means
/// every HR currency picker fed from that endpoint renders EMPTY for exactly the people who use it.
/// Measured 2026-09-01: the guarantor surety picker and the position's guarantor requirement both
/// did, and <c>DevelopmentPanel</c> (area 13, succession development costs) has since it shipped.</para>
///
/// <para><b>The alternative was worse.</b> Granting HR <c>ViewFinance</c> to populate a dropdown
/// would open every Finance read — ledgers, balances, payments — to the HR desk, to solve a list of
/// three-letter codes. This keeps the division <c>HrCurrencyBridge</c> already states: <b>Finance
/// owns what a currency IS; HR owns which one it uses.</b></para>
///
/// <para><b>Read-only, and deliberately not permission-gated beyond being internal.</b> A currency
/// code and its name are public facts about money, not tenant secrets, and the write paths that
/// matter are all still Finance's. What this cannot do is create, amend or deactivate a currency —
/// for that the answer remains "do it in Finance".</para>
/// </remarks>
[ApiController]
[Route("api/hr/currencies")]
[Authorize(Policy = "InternalOnly")]
public class HrCurrenciesController : ControllerBase
{
    private readonly ICurrencyService _currencies;

    public HrCurrenciesController(ICurrencyService currencies) => _currencies = currencies;

    /// <summary>The active currencies, as {code, name} — what a picker needs and nothing more.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<HrCurrencyOption>>> GetActive(
        CancellationToken cancellationToken = default)
    {
        var currencies = await _currencies.GetActiveAsync(cancellationToken);

        // A deliberately narrow projection. Passing Finance's DTO through would hand HR screens
        // decimal places, rounding methods and symbol positions they have no use for, and would
        // make this endpoint a second, accidental Finance read surface.
        return Ok(currencies
            .Select(c => new HrCurrencyOption(c.CurrencyCode, c.CurrencyName, c.CurrencySymbol))
            .OrderBy(c => c.Code)
            .ToList());
    }
}

/// <param name="Code">ISO 4217, e.g. GHS. ⚠ `Code`, not `CurrencyCode` — see the remarks.</param>
/// <param name="Name">Display name, e.g. Ghanaian Cedi.</param>
/// <param name="Symbol">Optional symbol, where Finance holds one.</param>
/// <remarks>
/// ⚠ The names here are <c>Code</c>/<c>Name</c> rather than Finance's <c>CurrencyCode</c>/
/// <c>CurrencyName</c>, because an HR picker asks for a code and a label. That difference is the
/// reason this record exists rather than a type alias: <c>DevelopmentPanel</c> read <c>c.code</c>
/// off Finance's DTO behind an <c>any</c> cast and rendered "undefined — undefined" for months.
/// </remarks>
public record HrCurrencyOption(string Code, string Name, string? Symbol);
