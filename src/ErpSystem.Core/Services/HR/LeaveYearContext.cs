using ErpSystem.Core.Interfaces.HR.Services;

namespace ErpSystem.Core.Services.HR;

/// <inheritdoc cref="ILeaveYearContext"/>
public sealed class LeaveYearContext : ILeaveYearContext
{
    private readonly ICompanyHrPolicyProvider _policyProvider;
    private int? _startMonth;

    public LeaveYearContext(ICompanyHrPolicyProvider policyProvider) => _policyProvider = policyProvider;

    /// <inheritdoc />
    public async Task<int> StartMonthAsync(CancellationToken cancellationToken = default)
    {
        // ⚠ Scoped, so this caches for one request. See the interface for why that is the contract
        // rather than an optimisation.
        _startMonth ??= (await _policyProvider.GetAsync(cancellationToken)).LeaveYearStartMonth;

        // ⚠ A row edited to something outside 1–12 degrades to January rather than throwing. Every
        // read path in the leave module goes through here, and a bad settings value should not be
        // able to take the module down — the same defence LeaveYear itself makes, repeated at the
        // door so the stored value is never propagated.
        return _startMonth is >= 1 and <= 12 ? _startMonth.Value : LeaveYear.CalendarStartMonth;
    }
}
