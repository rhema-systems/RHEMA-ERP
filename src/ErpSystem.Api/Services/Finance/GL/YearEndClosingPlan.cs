using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Server-derived close plan. Nominal accounts close by their original coding, and retained
/// earnings receives each coding bucket separately. No current dimension default is introduced.
/// Both the orchestrator and the guarded posting leaf derive this plan inside the close transaction.
/// </summary>
internal sealed record YearEndClosingPlan(
    IReadOnlyList<FinancePostingLineDto> Lines,
    IReadOnlyDictionary<Guid, AccountTransaction> Sources,
    decimal NetIncome)
{
    public static async Task<YearEndClosingPlan> BuildAsync(
        ApplicationDbContext db, YearEndBookCloseCycle cycle, CancellationToken cancellationToken = default)
    {
        var periodIds = await db.FiscalPeriods.Where(period => period.TenantId == cycle.TenantId
            && period.FiscalYearId == cycle.FiscalYearId && !period.IsDeleted)
            .Select(period => period.Id).ToListAsync(cancellationToken);
        var activity = await db.AccountTransactions.AsNoTracking()
            .Include(item => item.Account)
            .Include(item => item.FinanceDimensionSnapshot)!.ThenInclude(snapshot => snapshot!.Items)
            .Include(item => item.FinanceDimensionSet)!.ThenInclude(set => set!.Items)
            .Where(item => item.TenantId == cycle.TenantId && item.AccountingBookId == cycle.AccountingBookId
                && periodIds.Contains(item.FiscalPeriodId) && !item.IsDeleted
                && (item.PostingStatus == "Posted" || item.PostingStatus == "Reversed")
                && (item.Account.AccountType == AccountType.Revenue || item.Account.AccountType == AccountType.Expense))
            .ToListAsync(cancellationToken);
        if (activity.Any(item => item.FunctionalCurrencyCode != cycle.FunctionalCurrencyCode
            || item.Account.TenantId != cycle.TenantId || item.Account.IsDeleted))
            throw new InvalidOperationException("Nominal-account activity does not match the selected book's functional-currency authority.");
        foreach (var source in activity)
        {
            if (source.FinanceDimensionSnapshotId.HasValue &&
                (source.FinanceDimensionSnapshot == null || source.FinanceDimensionSnapshot.TenantId != cycle.TenantId
                    || source.FinanceDimensionSnapshot.FinanceDimensionSetId != source.FinanceDimensionSetId))
                throw new InvalidOperationException("Nominal-account frozen dimension snapshot authority is inconsistent.");
            if (source.FinanceDimensionSetId.HasValue && source.FinanceDimensionSnapshot == null &&
                (source.FinanceDimensionSet == null || source.FinanceDimensionSet.TenantId != cycle.TenantId
                    || source.FinanceDimensionSet.IsDeleted))
                throw new InvalidOperationException("Nominal-account historical dimension authority is missing.");
        }

        var balances = activity.GroupBy(item => new { item.AccountId, item.FinanceDimensionSetId, item.SegmentString })
            .Select(group => new
            {
                group.Key.AccountId, group.Key.FinanceDimensionSetId, group.Key.SegmentString,
                Balance = group.Sum(item => item.CreditAmount - item.DebitAmount),
                Source = group.OrderBy(item => item.Id).First()
            })
            .Where(item => item.Balance != 0m)
            .OrderBy(item => item.AccountId).ThenBy(item => item.FinanceDimensionSetId)
            .ThenBy(item => item.SegmentString, StringComparer.Ordinal).ToList();
        var lines = new List<FinancePostingLineDto>();
        var sources = new Dictionary<Guid, AccountTransaction>();
        foreach (var balance in balances)
            AddLine(balance.Source, balance.AccountId, balance.Balance, "YEAR-END-NOMINAL", "Year-end close - nominal account");

        // Deliberate accounting policy: retain each source coding bucket in equity. Collapsing
        // these amounts into one uncoded line would destroy dimension-level balancing.
        foreach (var bucket in balances.GroupBy(item => new { item.FinanceDimensionSetId, item.SegmentString })
            .OrderBy(group => group.Key.FinanceDimensionSetId).ThenBy(group => group.Key.SegmentString, StringComparer.Ordinal))
        {
            var income = bucket.Sum(item => item.Balance);
            if (income != 0m)
                AddLine(bucket.First().Source, cycle.RetainedEarningsAccountId, -income,
                    "YEAR-END-RETAINED-EARNINGS", "Year-end close - retained earnings by original coding");
        }
        return new YearEndClosingPlan(lines, sources, balances.Sum(item => item.Balance));

        void AddLine(AccountTransaction source, Guid accountId, decimal debitBalance, string component, string description)
        {
            var lineId = FinanceSourceLineIdentity.Create(cycle.Id, component, source.Id);
            sources.Add(lineId, source);
            lines.Add(new FinancePostingLineDto
            {
                AccountId = accountId, SourceDocumentLineId = lineId, Description = description,
                DebitAmount = debitBalance > 0m ? debitBalance : 0m,
                CreditAmount = debitBalance < 0m ? -debitBalance : 0m,
                TransactionCurrency = cycle.FunctionalCurrencyCode,
                FinanceDimensionSetId = source.FinanceDimensionSetId, SegmentString = source.SegmentString,
                LineNumber = lines.Count + 1, TransactionTag = "YearEndClose"
            });
        }
    }

    public void RequireExactLines(IReadOnlyList<FinancePostingLineDto> requested)
    {
        if (requested.Count != Lines.Count)
            throw new InvalidOperationException("Year-end lines do not match the exact frozen nominal-balance plan.");
        for (var index = 0; index < Lines.Count; index++)
        {
            var expected = Lines[index];
            var actual = requested[index];
            if (actual.SourceDocumentLineId != expected.SourceDocumentLineId || actual.AccountId != expected.AccountId
                || actual.DebitAmount != expected.DebitAmount || actual.CreditAmount != expected.CreditAmount
                || actual.FinanceDimensionSetId != expected.FinanceDimensionSetId
                || actual.SegmentString != expected.SegmentString || actual.TransactionCurrency != expected.TransactionCurrency
                || actual.LineNumber != expected.LineNumber || actual.Dimensions?.Count > 0)
                throw new InvalidOperationException("Year-end lines do not match the exact frozen nominal-balance plan.");
        }
    }
}

