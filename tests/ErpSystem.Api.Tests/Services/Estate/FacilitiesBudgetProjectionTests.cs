using ErpSystem.Api.Services.Estate;
using ErpSystem.Core.DTOs.Finance;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Estate;

public sealed class FacilitiesBudgetProjectionTests
{
    [Fact]
    public void IncludesOnlyFacilitiesDepartmentExpenseCells()
    {
        var report = new ConsolidatedBudgetViewDto
        {
            FiscalYearId = Guid.NewGuid(), FiscalYearName = "FY2026", ScenarioName = "Approved",
            Lines = new[]
            {
                Line("Expense", "5000", "Maintenance", 100m, 130m, "Facilities"),
                Line("Expense", "5000", "Maintenance", 900m, 950m, "Legal"),
                Line("Revenue", "4000", "Rent", 200m, 210m, "Facilities")
            }
        };

        var result = FacilitiesBudgetProjection.FromOfficialBudget(report);

        result.Lines.Should().ContainSingle();
        result.PlannedAmount.Should().Be(100m);
        result.ActualExpense.Should().Be(130m);
        result.Variance.Should().Be(30m);
    }

    [Fact]
    public void SeparatesDepartmentsWithinTheSameAccountAndPeriod()
    {
        var facilities = Line("Expense", "5000", "Maintenance", 100m, 130m, "Facilities");
        var legal = Line("Expense", "5000", "Maintenance", 900m, 950m, "Legal");
        facilities.DimensionCells = facilities.DimensionCells.Concat(legal.DimensionCells).ToList();
        var report = new ConsolidatedBudgetViewDto { Lines = new[] { facilities } };

        var result = FacilitiesBudgetProjection.FromOfficialBudget(report);

        result.Lines.Should().ContainSingle();
        result.PlannedAmount.Should().Be(100m);
        result.ActualExpense.Should().Be(130m);
    }

    private static BudgetReportLineDto Line(
        string type, string code, string name, decimal planned, decimal actual, string department) => new()
    {
        AccountType = type, AccountCode = code, AccountName = name,
        PeriodCode = "2026-09", PeriodNumber = 9,
        DimensionCells = new[]
        {
            new BudgetDimensionCellPositionDto
            {
                BudgetAmount = planned, ActualAmount = actual,
                DimensionAssignments = new[]
                {
                    new BudgetDimensionAssignmentDto
                    {
                        DimensionCode = "DEPT", DimensionName = "Department",
                        ValueCode = department.ToUpperInvariant(), ValueName = department
                    }
                }
            }
        }
    };
}
