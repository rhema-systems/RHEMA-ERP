const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';
function getAuthHeaders(): Record<string, string> {
  const token = typeof window !== 'undefined' ? (localStorage.getItem('token') || localStorage.getItem('authToken')) : null;
  return { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) };
}

export interface SalesReportSummary {
  totalRevenue: number; totalOrders: number; completedOrders: number;
  averageOrderValue: number; totalLeads: number; convertedLeads: number;
  conversionRate: number; openOpportunities: number;
  pipelineValue: number; weightedPipelineValue: number;
  activeCampaigns: number; campaignSpend: number;
  topProducts: TopProduct[]; topSalesReps: TopSalesRep[];
  monthlySales: MonthlySales[];
}
export interface TopProduct { productName: string; orderCount: number; totalRevenue: number; }
export interface TopSalesRep { salesRepName: string; orderCount: number; totalRevenue: number; commissionEarned: number; }
export interface MonthlySales { year: number; month: number; monthName: string; revenue: number; orderCount: number; }

export const salesReportingService = {
  async getSummary(from?: string, to?: string) {
    const params = new URLSearchParams();
    if (from) params.append('from', from);
    if (to) params.append('to', to);
    const queryStr = params.toString() ? `?${params}` : '';
    const res = await fetch(`${API_BASE_URL}/sales/reports/summary${queryStr}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch sales summary');
    return { data: await res.json() };
  },
};
