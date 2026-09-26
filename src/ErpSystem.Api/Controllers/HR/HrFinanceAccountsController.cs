using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The chart-of-accounts codes HR may name on an organisation unit or a team, read from Finance's
/// master (demo feedback round 2, lane B2; plan § 1.1 and § 6.2).
/// </summary>
/// <remarks>
/// <para><b>Why HR has its own read — the same reason as <c>HrCurrenciesController</c>.</b>
/// <c>api/finance/accounts</c> is mapped to <c>FinancePermissions.ViewFinance</c> by
/// <c>FinancePermissionPolicyMap</c>, a convention map that nothing on the Finance controller
/// shows. An HR user therefore gets a 403, and any HR account picker fed from that endpoint would
/// render EMPTY for exactly the people who use it.</para>
///
/// <para><b>The alternative was worse.</b> Granting HR <c>ViewFinance</c> to fill a dropdown would
/// open every Finance read — ledgers, balances, payments — to the HR desk, to solve a list of
/// account codes. This keeps the division the currency bridge already states: <b>Finance owns what
/// an account IS; HR owns which one a unit is charged to.</b></para>
///
/// <para><b>What it deliberately does not carry.</b> Code, number, name, type and whether the
/// account is active. No balances, no posting rules, no segment structure, no currency links. An
/// account's code and name are not tenant secrets; its balance is, and none is here. Passing
/// Finance's own <c>AccountDto</c> through would have made this a second, accidental Finance read
/// surface — which is exactly what the currency projection exists to avoid.</para>
///
/// <para><b>Which accounts.</b> Active accounts of any type. Finance's chart decides what a
/// departmental code is, and HR does not second-guess it by filtering on <c>AccountType</c>
/// (plan Q-1). If Finance later marks cost-centre accounts distinctly, this gains a filter.</para>
///
/// <para><b>Read-only.</b> There is no create, amend or deactivate here. For those the answer
/// remains "do it in Finance".</para>
/// </remarks>
[ApiController]
[Route("api/hr/finance-accounts")]
[Authorize(Policy = "InternalOnly")]
public class HrFinanceAccountsController : ControllerBase
{
    private readonly IAccountService _accounts;

    public HrFinanceAccountsController(IAccountService accounts) => _accounts = accounts;

    /// <summary>
    /// Accounts a picker can offer, optionally searched by code, number or name.
    /// </summary>
    /// <param name="search">Matches account code, number or name. Omit for the first <paramref name="take"/>.</param>
    /// <param name="take">Cap on rows returned. Defaults to 50, hard-capped at 200 — a picker is not a report.</param>
    /// <param name="includeInactive">
    /// Off by default. On, so that a unit already charged to a since-deactivated account can still
    /// show what it is charged to rather than rendering an empty box over a real value.
    /// </param>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<HrFinanceAccountOption>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<HrFinanceAccountOption>>> Search(
        [FromQuery] string? search = null,
        [FromQuery] int take = 50,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var capped = take < 1 ? 50 : Math.Min(take, 200);

        var accounts = await _accounts.GetAllAsync(
            accountType: null,
            status: includeInactive ? null : "Active",
            isMultiCurrency: null,
            coaType: null,
            search: string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            take: capped,
            cancellationToken: cancellationToken);

        return Ok(accounts.Select(Project).OrderBy(a => a.AccountCode).ToList());
    }

    /// <summary>
    /// One account, by id — what a picker needs to render the account a unit is ALREADY charged to.
    /// </summary>
    /// <remarks>
    /// ⚠ Deliberately answers for an INACTIVE account too. A unit charged to an account Finance has
    /// since deactivated must still show what it is charged to; the picker refusing to name it, or
    /// rendering an empty box over a real value, would be worse than saying "(inactive)".
    /// </remarks>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(HrFinanceAccountOption), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HrFinanceAccountOption>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetByIdAsync(id, cancellationToken);
        return account is null
            ? NotFound(new { message = $"No chart-of-accounts row was found with ID '{id}'." })
            : Ok(Project(account));
    }

    private static HrFinanceAccountOption Project(ErpSystem.Core.DTOs.Finance.AccountDto a) =>
        new(a.Id,
            a.AccountCode,
            a.AccountNumber,
            a.AccountName,
            a.AccountType,
            string.Equals(a.Status, "Active", StringComparison.OrdinalIgnoreCase));
}

/// <param name="Id">What HR stores. The code below is only ever a snapshot for display.</param>
/// <param name="AccountCode">Finance's own code, e.g. <c>ADM-1200</c>.</param>
/// <param name="AccountNumber">The ledger number, where the chart uses one separately.</param>
/// <param name="AccountName">What a picker shows beside the code.</param>
/// <param name="AccountType">Asset, Liability, Expense … as Finance classifies it.</param>
/// <param name="IsActive">
/// Derived from Finance's <c>Status</c> string. ⚠ HR refuses to newly assign an inactive account,
/// but shows one that is already assigned — see <c>includeInactive</c>.
/// </param>
public record HrFinanceAccountOption(
    Guid Id,
    string AccountCode,
    string AccountNumber,
    string AccountName,
    string AccountType,
    bool IsActive);
