export interface FinanceDashboardKpis {
    revenue: number;
    expenses: number;
    netProfit: number;
    cashOnHand: number;
    previousRevenue: number;
    previousExpenses: number;
    previousNetProfit: number;
    revenueChangePercent: number | null;
    expensesChangePercent: number | null;
    netProfitChangePercent: number | null;
}

export interface FinanceDashboardMonthlyPoint {
    name: string;
    revenue: number;
    expenses: number;
}

export interface FinanceDashboardBreakdownPoint {
    name: string;
    value: number;
}

export interface FinanceDashboardData {
    currencyCode: string;
    currencySymbol: string;
    currencyDecimalPlaces: number;
    rangeStartDate: string;
    rangeEndDate: string;
    comparisonStartDate: string;
    comparisonEndDate: string;
    kpis: FinanceDashboardKpis;
    monthly: FinanceDashboardMonthlyPoint[];
    expenseChart: FinanceDashboardBreakdownPoint[];
}
