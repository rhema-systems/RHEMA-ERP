using System;
using System.Collections.Generic;

namespace ErpSystem.Core.DTOs.Finance;

/// <summary>
/// Read-side finance dashboard aggregates for the current fiscal year, computed from posted
/// GL activity. Shape mirrors the frontend FinanceDashboardData contract.
/// </summary>
public class FinanceDashboardDto
{
    public string CurrencyCode { get; set; } = string.Empty;
    public string CurrencySymbol { get; set; } = string.Empty;
    public int CurrencyDecimalPlaces { get; set; }
    public DateTime RangeStartDate { get; set; }
    public DateTime RangeEndDate { get; set; }
    public DateTime ComparisonStartDate { get; set; }
    public DateTime ComparisonEndDate { get; set; }
    public FinanceDashboardKpisDto Kpis { get; set; } = new();
    public List<FinanceDashboardMonthlyPointDto> Monthly { get; set; } = new();
    public List<FinanceDashboardBreakdownPointDto> ExpenseChart { get; set; } = new();
}

public class FinanceDashboardKpisDto
{
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetProfit { get; set; }
    public decimal CashOnHand { get; set; }
    public decimal PreviousRevenue { get; set; }
    public decimal PreviousExpenses { get; set; }
    public decimal PreviousNetProfit { get; set; }
    public decimal? RevenueChangePercent { get; set; }
    public decimal? ExpensesChangePercent { get; set; }
    public decimal? NetProfitChangePercent { get; set; }
}

public class FinanceDashboardMonthlyPointDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
}

public class FinanceDashboardBreakdownPointDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
}
