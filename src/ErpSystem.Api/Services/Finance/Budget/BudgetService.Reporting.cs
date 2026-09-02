using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Budget;

public partial class BudgetService
{
    private const string PostedStatus = "Posted";
    private const string ReservedStatus = "Reserved";
    private const string ReportingBook = "IFRS";

    public async Task<BudgetScenarioDto> AdoptScenarioAsync(
        Guid id,
        AdoptBudgetScenarioDto dto)
    {
        var tenantId = TenantId;
        var reason = dto.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("An adoption reason is required.");
        if (dto.EffectiveDate == default)
            throw new InvalidOperationException("An adoption effective date is required.");

        var scenario = await _context.BudgetScenarios
            .Include(candidate => candidate.FiscalYear)
            .Include(candidate => candidate.BudgetReturns)
            .FirstOrDefaultAsync(candidate =>
                candidate.TenantId == tenantId
                && candidate.Id == id
                && !candidate.IsDeleted);
        if (scenario == null)
            throw new KeyNotFoundException("Budget scenario not found.");
        if (scenario.Status is not (ApprovedStatus or SupersededStatus))
            throw new InvalidOperationException(
                "Only an approved or previously superseded scenario can be adopted as the official budget.");
        if (scenario.IsActive)
            throw new InvalidOperationException("This scenario is already the official budget.");

        ApplyRowVersion(scenario, dto.RowVersion);
        var effectiveDate = dto.EffectiveDate.Date;
        if (scenario.FiscalYear != null
            && scenario.FiscalYear.StartDate != default
            && scenario.FiscalYear.EndDate != default
            && (effectiveDate < scenario.FiscalYear.StartDate.Date
                || effectiveDate > scenario.FiscalYear.EndDate.Date))
        {
            throw new InvalidOperationException(
                "The adoption effective date must fall within the scenario fiscal year.");
        }

        var now = DateTime.UtcNow;
        var userId = CurrentUserId;
        var previousOfficial = await _context.BudgetScenarios
            .FirstOrDefaultAsync(candidate =>
                candidate.TenantId == tenantId
                && candidate.FiscalYearId == scenario.FiscalYearId
                && candidate.Id != scenario.Id
                && candidate.IsActive
                && !candidate.IsDeleted);

        if (previousOfficial != null)
        {
            previousOfficial.IsActive = false;
            previousOfficial.Status = SupersededStatus;
            previousOfficial.SupersededAt = now;
            previousOfficial.SupersededByUserId = userId;
            previousOfficial.SupersessionReason = reason;
            previousOfficial.UpdatedAt = now;
            previousOfficial.LastModifiedById = userId;
        }

        var before = new
        {
            scenario.Status,
            scenario.IsActive,
            scenario.AdoptedAt,
            scenario.AdoptionEffectiveDate,
            scenario.AdoptionReason
        };
        scenario.Status = ApprovedStatus;
        scenario.IsActive = true;
        scenario.AdoptedAt = now;
        scenario.AdoptionEffectiveDate = effectiveDate;
        scenario.AdoptedByUserId = userId;
        scenario.AdoptionReason = reason;
        scenario.SupersededAt = null;
        scenario.SupersededByUserId = null;
        scenario.SupersessionReason = null;
        scenario.UpdatedAt = now;
        scenario.LastModifiedById = userId;

        await _context.SaveChangesAsync();

        if (previousOfficial != null)
        {
            await RecordAuditAsync(
                FinanceAuditEvents.BudgetScenarioSuperseded,
                "BudgetScenario",
                previousOfficial.Id,
                new { Status = ApprovedStatus, IsActive = true },
                new
                {
                    previousOfficial.Status,
                    previousOfficial.IsActive,
                    ReplacementScenarioId = scenario.Id,
                    previousOfficial.SupersededAt
                },
                reason);
        }

        await RecordAuditAsync(
            FinanceAuditEvents.BudgetScenarioAdopted,
            "BudgetScenario",
            scenario.Id,
            before,
            new
            {
                scenario.Status,
                scenario.IsActive,
                scenario.AdoptedAt,
                scenario.AdoptionEffectiveDate,
                scenario.AdoptedByUserId,
                ReplacedScenarioId = previousOfficial?.Id
            },
            reason);

        return await MapToDtoAsync(scenario);
    }

    public async Task<ConsolidatedBudgetViewDto> GetActiveBudgetVsActualAsync(Guid fiscalYearId)
    {
        var tenantId = TenantId;
        var scenarioId = await _context.BudgetScenarios
            .AsNoTracking()
            .Where(scenario =>
                scenario.TenantId == tenantId
                && scenario.FiscalYearId == fiscalYearId
                && scenario.IsActive
                && !scenario.IsDeleted)
            .Select(scenario => (Guid?)scenario.Id)
            .SingleOrDefaultAsync();
        if (!scenarioId.HasValue)
            throw new KeyNotFoundException("No official budget has been adopted for this fiscal year.");

        return await GetConsolidatedViewAsync(scenarioId.Value, approvedOnly: true);
    }

    public async Task<ConsolidatedBudgetViewDto> GetConsolidatedViewAsync(
        Guid scenarioId,
        bool approvedOnly)
    {
        var tenantId = TenantId;
        var scenario = await _context.BudgetScenarios
            .AsNoTracking()
            .Include(candidate => candidate.FiscalYear)
            .FirstOrDefaultAsync(candidate =>
                candidate.TenantId == tenantId
                && candidate.Id == scenarioId
                && !candidate.IsDeleted);
        if (scenario == null)
            throw new KeyNotFoundException("Budget scenario not found.");

        var returns = await _context.BudgetReturns
            .AsNoTracking()
            .Include(budgetReturn => budgetReturn.SegmentValue)
            .Include(budgetReturn => budgetReturn.AssignedToUser)
            .Where(budgetReturn =>
                budgetReturn.TenantId == tenantId
                && budgetReturn.BudgetScenarioId == scenarioId
                && !budgetReturn.IsDeleted)
            .OrderBy(budgetReturn => budgetReturn.SegmentValue!.SegmentValue)
            .ToListAsync();

        var includedReturnIds = returns
            .Where(budgetReturn => !approvedOnly || budgetReturn.Status == ApprovedStatus)
            .Select(budgetReturn => budgetReturn.Id)
            .ToArray();
        var allReturnIds = returns
            .Select(budgetReturn => budgetReturn.Id)
            .ToArray();

        var allBudgetRows = await _context.BudgetEntries
            .AsNoTracking()
            .Where(entry =>
                entry.TenantId == tenantId
                && !entry.IsDeleted
                && allReturnIds.Contains(entry.BudgetReturnId))
            .Select(entry => new
            {
                BudgetEntryId = entry.Id,
                entry.BudgetReturnId,
                entry.AccountId,
                AccountCode = entry.Account!.AccountCode,
                AccountName = entry.Account.AccountName,
                AccountType = entry.Account.AccountType,
                entry.FiscalPeriodId,
                entry.FinanceDimensionSetId,
                PeriodCode = entry.FiscalPeriod!.PeriodCode,
                PeriodName = entry.FiscalPeriod.PeriodName,
                entry.FiscalPeriod.PeriodNumber,
                entry.AmountBase
            })
            .ToListAsync();
        var includedReturnIdSet = includedReturnIds.ToHashSet();
        var budgetRows = allBudgetRows
            .Where(row => includedReturnIdSet.Contains(row.BudgetReturnId))
            .ToList();

        var includedBudgetEntryIds = budgetRows
            .Select(row => row.BudgetEntryId)
            .ToArray();
        var activeReservations = await _context.FinanceBudgetReservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.TenantId == tenantId
                && !reservation.IsDeleted
                && reservation.Status == ReservedStatus
                && includedBudgetEntryIds.Contains(reservation.BudgetEntryId))
            .Select(reservation => new
            {
                reservation.BudgetEntryId,
                reservation.ReservedAmount
            })
            .ToListAsync();
        var reservedAmountByEntryId = activeReservations
            .GroupBy(reservation => reservation.BudgetEntryId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(reservation => reservation.ReservedAmount));

        var actualRows = await _context.AccountTransactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.TenantId == tenantId
                && !transaction.IsDeleted
                && transaction.PostingStatus == PostedStatus
                && transaction.JournalEntry.PostingStatus == PostedStatus
                && !transaction.JournalEntry.IsDeleted
                && transaction.BookClassification == ReportingBook
                && transaction.FiscalPeriod.FiscalYearId == scenario.FiscalYearId
                && (transaction.Account.AccountType == AccountType.Revenue
                    || transaction.Account.AccountType == AccountType.Expense))
            .Select(transaction => new
            {
                transaction.AccountId,
                transaction.FiscalPeriodId,
                transaction.Account.AccountType,
                transaction.FinanceDimensionSetId,
                transaction.DebitAmount,
                transaction.CreditAmount
            })
            .ToListAsync();

        var dimensionSetIds = budgetRows.Select(row => row.FinanceDimensionSetId)
            .Concat(actualRows.Select(row => row.FinanceDimensionSetId))
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        var dimensionItems = await _context.FinanceDimensionSetItems.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted
                && dimensionSetIds.Contains(item.FinanceDimensionSetId))
            .Select(item => new
            {
                item.FinanceDimensionSetId,
                item.FinanceDimensionDefinitionId,
                item.FinanceDimensionValueId,
                item.FinanceDimensionSet.CombinationHash,
                item.FinanceDimensionSet.DisplayValue,
                DimensionCode = item.DimensionCodeSnapshot,
                DimensionName = item.FinanceDimensionDefinition.Name,
                ValueCode = item.DimensionValueCodeSnapshot,
                ValueName = item.DimensionValueNameSnapshot
            })
            .ToListAsync();
        var assignmentsBySet = dimensionItems.GroupBy(item => item.FinanceDimensionSetId)
            .ToDictionary(group => group.Key, group => group
                .Select(item => (item.FinanceDimensionDefinitionId, item.FinanceDimensionValueId))
                .ToHashSet());
        var dimensionMetadataBySet = dimensionItems
            .GroupBy(item => item.FinanceDimensionSetId)
            .ToDictionary(group => group.Key, group => new
            {
                group.First().CombinationHash,
                group.First().DisplayValue,
                Assignments = (IReadOnlyList<BudgetDimensionAssignmentDto>)group
                    .OrderBy(item => item.DimensionCode)
                    .Select(item => new BudgetDimensionAssignmentDto
                    {
                        FinanceDimensionDefinitionId = item.FinanceDimensionDefinitionId,
                        FinanceDimensionValueId = item.FinanceDimensionValueId,
                        DimensionCode = item.DimensionCode,
                        DimensionName = item.DimensionName,
                        ValueCode = item.ValueCode,
                        ValueName = item.ValueName
                    })
                    .ToList()
            });

        var returnById = returns.ToDictionary(budgetReturn => budgetReturn.Id);
        var lines = budgetRows
            .GroupBy(row => new
            {
                row.AccountId,
                row.AccountCode,
                row.AccountName,
                row.AccountType,
                row.FiscalPeriodId,
                row.PeriodCode,
                row.PeriodName,
                row.PeriodNumber
            })
            .Select(group =>
            {
                var budgetAmount = group.Sum(row => row.AmountBase);
                var budgetSetIds = group.Select(row => row.FinanceDimensionSetId).Distinct().ToList();
                if (budgetSetIds.Any(id => id.HasValue) && budgetSetIds.Any(id => !id.HasValue))
                    throw new InvalidOperationException(
                        "A budget account and period cannot mix legacy and dimension-grained cells.");
                var matchingActuals = actualRows.Where(row => row.AccountId == group.Key.AccountId
                    && row.FiscalPeriodId == group.Key.FiscalPeriodId).ToList();
                if (budgetSetIds.All(id => id.HasValue))
                {
                    matchingActuals = matchingActuals.Where(actual =>
                    {
                        if (!actual.FinanceDimensionSetId.HasValue
                            || !assignmentsBySet.TryGetValue(actual.FinanceDimensionSetId.Value, out var actualAssignments))
                            return false;
                        var matches = budgetSetIds.Count(budgetSetId =>
                            assignmentsBySet.TryGetValue(budgetSetId!.Value, out var budgetAssignments)
                            && budgetAssignments.IsSubsetOf(actualAssignments));
                        if (matches > 1)
                            throw new InvalidOperationException(
                                "Overlapping budget dimension cells match the same posted transaction.");
                        return matches == 1;
                    }).ToList();
                }
                var actualAmount = matchingActuals.Sum(row =>
                    row.AccountType == AccountType.Revenue
                        ? row.CreditAmount - row.DebitAmount
                        : row.DebitAmount - row.CreditAmount);
                var dimensionCells = group
                    .GroupBy(row => row.FinanceDimensionSetId)
                    .Select(cellGroup =>
                    {
                        var cellActuals = cellGroup.Key.HasValue
                            ? matchingActuals.Where(actual =>
                                actual.FinanceDimensionSetId.HasValue
                                && assignmentsBySet.TryGetValue(cellGroup.Key.Value, out var budgetAssignments)
                                && assignmentsBySet.TryGetValue(actual.FinanceDimensionSetId.Value, out var actualAssignments)
                                && budgetAssignments.IsSubsetOf(actualAssignments))
                            : matchingActuals;
                        var cellActualAmount = cellActuals.Sum(row =>
                            row.AccountType == AccountType.Revenue
                                ? row.CreditAmount - row.DebitAmount
                                : row.DebitAmount - row.CreditAmount);
                        var cellBudgetAmount = cellGroup.Sum(row => row.AmountBase);
                        var cellReservedAmount = cellGroup.Sum(row =>
                            reservedAmountByEntryId.GetValueOrDefault(row.BudgetEntryId));
                        var metadata = cellGroup.Key.HasValue
                            && dimensionMetadataBySet.TryGetValue(cellGroup.Key.Value, out var found)
                                ? found
                                : null;

                        return new BudgetDimensionCellPositionDto
                        {
                            FinanceDimensionSetId = cellGroup.Key,
                            DimensionDisplayValue = metadata?.DisplayValue ?? "Legacy / no dimensions",
                            DimensionCombinationHash = metadata?.CombinationHash,
                            DimensionAssignments = metadata?.Assignments
                                ?? Array.Empty<BudgetDimensionAssignmentDto>(),
                            BudgetAmount = cellBudgetAmount,
                            ActualAmount = cellActualAmount,
                            ReservedAmount = cellReservedAmount,
                            AvailableAmount = cellBudgetAmount - cellActualAmount - cellReservedAmount
                        };
                    })
                    .OrderBy(cell => cell.DimensionDisplayValue)
                    .ToList();
                var variance = actualAmount - budgetAmount;
                return new BudgetReportLineDto
                {
                    AccountId = group.Key.AccountId,
                    AccountCode = group.Key.AccountCode,
                    AccountName = group.Key.AccountName,
                    AccountType = group.Key.AccountType.ToString(),
                    FiscalPeriodId = group.Key.FiscalPeriodId,
                    PeriodCode = group.Key.PeriodCode,
                    PeriodName = group.Key.PeriodName,
                    PeriodNumber = group.Key.PeriodNumber,
                    BudgetAmount = budgetAmount,
                    ActualAmount = actualAmount,
                    VarianceAmount = variance,
                    VariancePercent = budgetAmount == 0m
                        ? null
                        : decimal.Round(variance / Math.Abs(budgetAmount) * 100m, 2),
                    Favorability = ResolveFavorability(
                        group.Key.AccountType,
                        variance),
                    DimensionCells = dimensionCells,
                    Contributions = group
                        .GroupBy(row => row.BudgetReturnId)
                        .Select(contribution =>
                        {
                            var budgetReturn = returnById[contribution.Key];
                            return new BudgetReportContributionDto
                            {
                                BudgetReturnId = budgetReturn.Id,
                                SegmentValueId = budgetReturn.SegmentValueId,
                                SegmentCode = budgetReturn.SegmentValue?.SegmentValue ?? string.Empty,
                                SegmentName = budgetReturn.SegmentValue?.Description
                                    ?? budgetReturn.SegmentValue?.SegmentValue
                                    ?? "General",
                                ReturnStatus = budgetReturn.Status,
                                BudgetAmount = contribution.Sum(row => row.AmountBase)
                            };
                        })
                        .OrderBy(contribution => contribution.SegmentCode)
                        .ToList()
                };
            })
            .OrderBy(line => line.AccountCode)
            .ThenBy(line => line.PeriodNumber)
            .ToList();

        var periods = await _context.FiscalPeriods
            .AsNoTracking()
            .Where(period =>
                period.TenantId == tenantId
                && period.FiscalYearId == scenario.FiscalYearId
                && !period.IsDeleted)
            .OrderBy(period => period.PeriodNumber)
            .Select(period => new BudgetReportPeriodDto
            {
                FiscalPeriodId = period.Id,
                PeriodCode = period.PeriodCode,
                PeriodName = period.PeriodName,
                PeriodNumber = period.PeriodNumber
            })
            .ToListAsync();

        var units = returns.Select(budgetReturn => new BudgetUnitSummaryDto
            {
                BudgetReturnId = budgetReturn.Id,
                SegmentValueId = budgetReturn.SegmentValueId,
                SegmentCode = budgetReturn.SegmentValue?.SegmentValue ?? string.Empty,
                SegmentName = budgetReturn.SegmentValue?.Description
                    ?? budgetReturn.SegmentValue?.SegmentValue
                    ?? "General",
                ReturnStatus = budgetReturn.Status,
                AssignedToUserName = ResolveUserName(budgetReturn.AssignedToUser),
                BudgetAmount = budgetRows
                    .Where(row => row.BudgetReturnId == budgetReturn.Id)
                    .Sum(row => row.AmountBase)
            })
            .OrderBy(unit => unit.SegmentCode)
            .ToList();

        var returnIdsWithEntries = allBudgetRows
            .Select(row => row.BudgetReturnId)
            .Distinct()
            .ToHashSet();
        var issues = BuildValidationIssues(returns, returnIdsWithEntries);
        var totalRevenueBudget = lines
            .Where(line => line.AccountType == AccountType.Revenue.ToString())
            .Sum(line => line.BudgetAmount);
        var totalExpenseBudget = lines
            .Where(line => line.AccountType == AccountType.Expense.ToString())
            .Sum(line => line.BudgetAmount);
        var totalRevenueActual = lines
            .Where(line => line.AccountType == AccountType.Revenue.ToString())
            .Sum(line => line.ActualAmount);
        var totalExpenseActual = lines
            .Where(line => line.AccountType == AccountType.Expense.ToString())
            .Sum(line => line.ActualAmount);

        return new ConsolidatedBudgetViewDto
        {
            ScenarioId = scenario.Id,
            ScenarioName = scenario.Name,
            FiscalYearId = scenario.FiscalYearId,
            FiscalYearName = scenario.FiscalYear?.FiscalYearName ?? string.Empty,
            ScenarioStatus = scenario.Status,
            IsOfficial = scenario.IsActive,
            ApprovedOnly = approvedOnly,
            CurrencyCode = scenario.BaseCurrencyCode,
            BookClassification = ReportingBook,
            TotalReturnCount = returns.Count,
            IncludedReturnCount = includedReturnIds.Length,
            ApprovedReturnCount = returns.Count(item => item.Status == ApprovedStatus),
            DraftReturnCount = returns.Count(item => item.Status == DraftStatus),
            SubmittedReturnCount = returns.Count(item => item.Status == SubmittedStatus),
            RejectedReturnCount = returns.Count(item => item.Status == RejectedStatus),
            ReadyForSubmission = returns.Count > 0
                && returns.All(item => item.Status == ApprovedStatus),
            TotalRevenueBudget = totalRevenueBudget,
            TotalExpenseBudget = totalExpenseBudget,
            NetBudget = totalRevenueBudget - totalExpenseBudget,
            TotalRevenueActual = totalRevenueActual,
            TotalExpenseActual = totalExpenseActual,
            NetActual = totalRevenueActual - totalExpenseActual,
            Periods = periods,
            Lines = lines,
            Units = units,
            ValidationIssues = issues
        };
    }

    public async Task<BudgetScenarioComparisonDto> CompareScenariosAsync(
        Guid baseScenarioId,
        Guid comparisonScenarioId)
    {
        if (baseScenarioId == comparisonScenarioId)
            throw new InvalidOperationException("Select two different scenarios to compare.");

        var tenantId = TenantId;
        var scenarios = await _context.BudgetScenarios
            .AsNoTracking()
            .Where(scenario =>
                scenario.TenantId == tenantId
                && (scenario.Id == baseScenarioId || scenario.Id == comparisonScenarioId)
                && !scenario.IsDeleted)
            .ToListAsync();
        var baseScenario = scenarios.FirstOrDefault(scenario => scenario.Id == baseScenarioId);
        var comparisonScenario = scenarios.FirstOrDefault(scenario => scenario.Id == comparisonScenarioId);
        if (baseScenario == null || comparisonScenario == null)
            throw new KeyNotFoundException("One or both budget scenarios were not found.");
        if (baseScenario.FiscalYearId != comparisonScenario.FiscalYearId)
            throw new InvalidOperationException("Only scenarios from the same fiscal year can be compared.");
        if (!IsComparableScenario(baseScenario) || !IsComparableScenario(comparisonScenario))
            throw new InvalidOperationException(
                "Scenario comparison is available for approved, official, or superseded scenarios.");

        var baseView = await GetConsolidatedViewAsync(baseScenarioId, approvedOnly: true);
        var comparisonView = await GetConsolidatedViewAsync(comparisonScenarioId, approvedOnly: true);
        var baseLines = baseView.Lines.ToDictionary(
            line => (line.AccountId, line.FiscalPeriodId));
        var comparisonLines = comparisonView.Lines.ToDictionary(
            line => (line.AccountId, line.FiscalPeriodId));
        var keys = baseLines.Keys.Union(comparisonLines.Keys).ToList();

        var lines = keys.Select(key =>
            {
                baseLines.TryGetValue(key, out var baseLine);
                comparisonLines.TryGetValue(key, out var comparisonLine);
                var baseAmount = baseLine?.BudgetAmount ?? 0m;
                var comparisonAmount = comparisonLine?.BudgetAmount ?? 0m;
                var difference = comparisonAmount - baseAmount;
                var source = comparisonLine ?? baseLine!;
                return new BudgetScenarioComparisonLineDto
                {
                    AccountId = source.AccountId,
                    AccountCode = source.AccountCode,
                    AccountName = source.AccountName,
                    AccountType = source.AccountType,
                    FiscalPeriodId = source.FiscalPeriodId,
                    PeriodCode = source.PeriodCode,
                    PeriodName = source.PeriodName,
                    PeriodNumber = source.PeriodNumber,
                    BaseAmount = baseAmount,
                    ComparisonAmount = comparisonAmount,
                    DifferenceAmount = difference,
                    DifferencePercent = baseAmount == 0m
                        ? null
                        : decimal.Round(difference / Math.Abs(baseAmount) * 100m, 2)
                };
            })
            .OrderBy(line => line.AccountCode)
            .ThenBy(line => line.PeriodNumber)
            .ToList();

        return new BudgetScenarioComparisonDto
        {
            BaseScenarioId = baseScenario.Id,
            BaseScenarioName = baseScenario.Name,
            ComparisonScenarioId = comparisonScenario.Id,
            ComparisonScenarioName = comparisonScenario.Name,
            FiscalYearId = baseScenario.FiscalYearId,
            CurrencyCode = baseScenario.BaseCurrencyCode,
            BaseTotal = lines.Sum(line => line.BaseAmount),
            ComparisonTotal = lines.Sum(line => line.ComparisonAmount),
            DifferenceTotal = lines.Sum(line => line.DifferenceAmount),
            Lines = lines
        };
    }

    private static IReadOnlyList<BudgetValidationIssueDto> BuildValidationIssues(
        IReadOnlyCollection<ErpSystem.Core.Entities.Finance.BudgetReturn> returns,
        IReadOnlySet<Guid> returnIdsWithEntries)
    {
        var issues = new List<BudgetValidationIssueDto>();
        if (returns.Count == 0)
        {
            issues.Add(new BudgetValidationIssueDto
            {
                Severity = "Error",
                Code = "NO_RETURNS",
                Message = "Create at least one departmental budget return before submitting the scenario."
            });
            return issues;
        }

        foreach (var budgetReturn in returns)
        {
            var label = budgetReturn.SegmentValue?.Description
                ?? budgetReturn.SegmentValue?.SegmentValue
                ?? "General return";
            if (!returnIdsWithEntries.Contains(budgetReturn.Id))
            {
                issues.Add(new BudgetValidationIssueDto
                {
                    Severity = "Error",
                    Code = "EMPTY_RETURN",
                    Message = $"{label} has no budget entries.",
                    ReturnId = budgetReturn.Id
                });
            }
            if (!budgetReturn.AssignedToUserId.HasValue)
            {
                issues.Add(new BudgetValidationIssueDto
                {
                    Severity = "Warning",
                    Code = "UNASSIGNED_RETURN",
                    Message = $"{label} is not assigned to a preparer.",
                    ReturnId = budgetReturn.Id
                });
            }
            if (budgetReturn.Status != ApprovedStatus)
            {
                issues.Add(new BudgetValidationIssueDto
                {
                    Severity = "Warning",
                    Code = "RETURN_NOT_APPROVED",
                    Message = $"{label} is {budgetReturn.Status} and is not part of the approved reporting baseline.",
                    ReturnId = budgetReturn.Id
                });
            }
        }

        return issues;
    }

    private static string ResolveFavorability(AccountType accountType, decimal variance)
    {
        if (variance == 0m)
            return "OnBudget";
        var favorable = accountType == AccountType.Revenue
            ? variance > 0m
            : variance < 0m;
        return favorable ? "Favorable" : "Unfavorable";
    }

    private static string ResolveUserName(ErpSystem.Core.Entities.ApplicationUser? user)
    {
        if (user == null)
            return string.Empty;
        var name = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? user.UserName ?? string.Empty : name;
    }

    private static bool IsComparableScenario(
        ErpSystem.Core.Entities.Finance.BudgetScenario scenario) =>
        scenario.IsActive || scenario.Status is ApprovedStatus or SupersededStatus;
}
