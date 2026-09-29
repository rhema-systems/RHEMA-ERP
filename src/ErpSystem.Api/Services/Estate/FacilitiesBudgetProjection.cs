using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Api.Services.Estate;

public static class FacilitiesBudgetProjection
{
    public static FacilitiesBudgetReport FromOfficialBudget(ConsolidatedBudgetViewDto budget)
    {
        var lines = budget.Lines
            .Where(line => string.Equals(line.AccountType, "Expense", StringComparison.OrdinalIgnoreCase))
            .SelectMany(line => line.DimensionCells
                .Where(cell => cell.DimensionAssignments.Any(IsFacilitiesDepartment))
                .Select(cell => new FacilitiesBudgetLine(
                    line.AccountCode,
                    line.AccountName,
                    line.PeriodCode,
                    line.PeriodNumber,
                    cell.BudgetAmount,
                    cell.ActualAmount,
                    cell.ActualAmount - cell.BudgetAmount)))
            .OrderBy(line => line.PeriodNumber)
            .ThenBy(line => line.AccountCode)
            .ToList();

        return new FacilitiesBudgetReport(
            budget.FiscalYearId,
            budget.FiscalYearName,
            budget.CurrencyCode,
            budget.ScenarioName,
            lines.Sum(line => line.PlannedAmount),
            lines.Sum(line => line.ActualExpense),
            lines.Sum(line => line.Variance),
            lines);
    }

    private static bool IsFacilitiesDepartment(BudgetDimensionAssignmentDto assignment)
    {
        var dimension = $"{assignment.DimensionCode} {assignment.DimensionName}";
        if (!dimension.Contains("department", StringComparison.OrdinalIgnoreCase)
            && !dimension.Contains("dept", StringComparison.OrdinalIgnoreCase))
            return false;

        return assignment.ValueName.Contains("facilit", StringComparison.OrdinalIgnoreCase)
            || assignment.ValueCode.Contains("FACILIT", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record FacilitiesBudgetLine(
    string AccountCode,
    string AccountName,
    string PeriodCode,
    int PeriodNumber,
    decimal PlannedAmount,
    decimal ActualExpense,
    decimal Variance);

public sealed record FacilitiesBudgetReport(
    Guid FiscalYearId,
    string FiscalYearName,
    string CurrencyCode,
    string ScenarioName,
    decimal PlannedAmount,
    decimal ActualExpense,
    decimal Variance,
    IReadOnlyList<FacilitiesBudgetLine> Lines);
