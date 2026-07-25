export interface FinanceDashboardKpis {
    revenue: number;
    expenses: number;
    netProfit: number;
    cashOnHand: number;
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
    kpis: FinanceDashboardKpis;
    monthly: FinanceDashboardMonthlyPoint[];
    expenseChart: FinanceDashboardBreakdownPoint[];
}
