using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.JobAnalysis;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR.Finance;

/// <summary>
/// Finance's actuals against an HR budget (lane 8, slice 6). A READ, never a write: the manpower
/// budget's <c>ActualSpent</c>/<c>Variance</c> columns stay untouched (the backlog's decision — HR
/// does not invent a number), and the training budget's own <c>SpentAmount</c> stays its ledger of
/// memos. What Finance says was spent is read from its book balances: the net movement, period by
/// period, on the account the budget's unit is charged to (<c>OrganizationUnit.FinanceAccountId</c>,
/// round 2 lane B2) or the account the training budget names by code, over the budget's period.
/// </summary>
/// <remarks>
/// <para>This is the shape of every HR budget conversation with Finance until the budget-commitment
/// contract (FIN-INT-015's reserve → consume → release) is opened for HR: HR plans, Finance posts,
/// HR reads the difference. No dimension is sent or read — a unit is one account, which is what
/// the org-unit link says today.</para>
/// </remarks>
public sealed class HrFinanceActualsService : IHrFinanceActualsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHrFinancePostingStore _store;
    private readonly IBookBalanceReadModelService _balances;
    private readonly IFiscalPeriodService _periods;
    private readonly IAccountService _accounts;
    private readonly ICurrentUserProvider _currentUser;

    public HrFinanceActualsService(
        IUnitOfWork unitOfWork,
        IHrFinancePostingStore store,
        IBookBalanceReadModelService balances,
        IFiscalPeriodService periods,
        IAccountService accounts,
        ICurrentUserProvider currentUser)
    {
        _unitOfWork = unitOfWork;
        _store = store;
        _balances = balances;
        _periods = periods;
        _accounts = accounts;
        _currentUser = currentUser;
    }

    public async Task<HrBudgetFinanceActualsDto> GetManpowerBudgetActualsAsync(Guid budgetId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var budget = await _unitOfWork.Repository<ManpowerBudget>().GetQueryable().AsNoTracking()
            .Include(b => b.OrganizationUnit)
            .FirstOrDefaultAsync(b => b.Id == budgetId && b.TenantId == tenantId && !b.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Manpower budget '{budgetId}' was not found.");

        var result = new HrBudgetFinanceActualsDto
        {
            BudgetId = budget.Id,
            BudgetNumber = budget.BudgetNumber,
            BudgetKind = "Manpower",
            PeriodStart = budget.PeriodStartDate.Date,
            PeriodEnd = budget.PeriodEndDate.Date,
            Budget = budget.TotalBudget,
            UnitName = budget.OrganizationUnit?.Name
        };

        if (budget.OrganizationUnitId is null)
        {
            result.Problem = "This budget covers an organisation level, not a unit, so there is no single account to read Finance's actuals from.";
            return result;
        }
        if (budget.OrganizationUnit?.FinanceAccountId is not { } accountId)
        {
            result.Problem = $"{budget.OrganizationUnit?.Name ?? "The unit"} is not charged to a Finance account yet. Set it on the unit (Organisation structure → the unit → Finance account) and the actuals appear here.";
            return result;
        }

        await FillAsync(result, tenantId, accountId, cancellationToken);
        return result;
    }

    public async Task<HrBudgetFinanceActualsDto> GetTrainingBudgetActualsAsync(Guid budgetId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var budget = await _unitOfWork.Repository<TrainingBudget>().GetQueryable().AsNoTracking()
            .Include(b => b.OrganizationUnit)
            .FirstOrDefaultAsync(b => b.Id == budgetId && b.TenantId == tenantId && !b.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Training budget '{budgetId}' was not found.");

        var (start, end) = budget.Quarter is { } q and >= 1 and <= 4
            ? (new DateTime(budget.Year, (q - 1) * 3 + 1, 1), new DateTime(budget.Year, (q - 1) * 3 + 3, 1).AddMonths(1).AddDays(-1))
            : (new DateTime(budget.Year, 1, 1), new DateTime(budget.Year, 12, 31));
        var result = new HrBudgetFinanceActualsDto
        {
            BudgetId = budget.Id,
            BudgetNumber = budget.BudgetCode,
            BudgetKind = "Training",
            PeriodStart = start,
            PeriodEnd = end,
            Budget = budget.AllocatedAmount,
            UnitName = budget.OrganizationUnit?.Name
        };

        // The training budget names its account by CODE (free text, area 7); the unit's Finance
        // account is the fallback so a budget typed without a code still reads something.
        Guid? accountId = null;
        var code = budget.GLAccountCode?.Trim();
        if (!string.IsNullOrWhiteSpace(code))
        {
            var match = (await _accounts.GetAllAsync(accountType: null, status: null, isMultiCurrency: null, coaType: null, search: code, take: 50, cancellationToken: cancellationToken))
                .FirstOrDefault(a => string.Equals(a.AccountCode, code, StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(a.AccountNumber, code, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                result.Problem = $"The budget names GL account '{code}', which is not on Finance's chart. Correct the code on the budget.";
                return result;
            }
            accountId = match.Id;
        }
        accountId ??= budget.OrganizationUnit?.FinanceAccountId;
        if (accountId is null)
        {
            result.Problem = "The budget names no GL account and its unit is not charged to a Finance account, so there is nothing to read Finance's actuals from.";
            return result;
        }

        await FillAsync(result, tenantId, accountId.Value, cancellationToken);
        return result;
    }

    private async Task FillAsync(HrBudgetFinanceActualsDto result, Guid tenantId, Guid accountId, CancellationToken cancellationToken)
    {
        var context = await _store.GetTenantContextAsync(tenantId, cancellationToken);
        result.FunctionalCurrencyCode = context.FunctionalCurrencyCode;
        result.AccountingBookCode = context.AccountingBookCode;
        if (context.AccountingBookCode is null)
        {
            result.Problem = $"Finance's accounting book is not configured: {context.AccountingBookProblem}";
            return;
        }

        var account = await _accounts.GetByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            result.Problem = "The Finance account this budget is charged to no longer exists.";
            return;
        }
        result.AccountId = account.Id;
        result.AccountCode = account.AccountCode;
        result.AccountName = account.AccountName;

        // Every fiscal period that touches the budget's window. Finance's periods, not HR's
        // months: a budget that starts mid-period reads the whole period, which is said in the row.
        var periods = (await _periods.GetFiscalPeriodsAsync(null, null, cancellationToken))
            .Where(p => p.StartDate.Date <= result.PeriodEnd && p.EndDate.Date >= result.PeriodStart)
            .OrderBy(p => p.StartDate)
            .ToList();
        if (periods.Count == 0)
        {
            result.Problem = "Finance has no fiscal periods covering this budget's dates yet.";
            return;
        }

        var inquiry = await _balances.GetAsync(tenantId, accountId, context.AccountingBookCode, null, cancellationToken);
        var byPeriod = inquiry.Balances.ToDictionary(b => b.FiscalPeriodId);
        foreach (var period in periods)
        {
            byPeriod.TryGetValue(period.Id, out var balance);
            result.Periods.Add(new HrBudgetFinanceActualsPeriodDto
            {
                FiscalPeriodId = period.Id,
                PeriodName = period.PeriodName,
                StartDate = period.StartDate.Date,
                EndDate = period.EndDate.Date,
                PartlyOutsideBudget = period.StartDate.Date < result.PeriodStart || period.EndDate.Date > result.PeriodEnd,
                Debits = balance?.PeriodDebits ?? 0m,
                Credits = balance?.PeriodCredits ?? 0m,
                NetMovement = balance?.PeriodNetMovement ?? 0m,
                TransactionCount = balance?.TransactionCount ?? 0
            });
        }
        result.Actual = result.Periods.Sum(p => p.NetMovement);
        result.Linked = true;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == Guid.Empty) throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }
}
