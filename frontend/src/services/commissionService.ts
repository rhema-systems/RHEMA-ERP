const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';
function getAuthHeaders(): Record<string, string> {
  const token = typeof window !== 'undefined' ? (localStorage.getItem('token') || localStorage.getItem('authToken')) : null;
  return { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) };
}

export interface CommissionRuleSummary {
  id: string; name: string; commissionType: string; rate: number;
  salesRepId?: string; minimumSaleAmount: number; maximumCommission: number;
  isActive: boolean; effectiveFrom: string; effectiveTo?: string; createdAt: string;
}
export interface CommissionRuleDetail extends CommissionRuleSummary {
  description?: string; tierDefinitions?: string; productId?: string; customerId?: string;
}
export interface CreateCommissionRule {
  name: string; description?: string; commissionType: string; rate: number;
  tierDefinitions?: string; salesRepId?: string; productId?: string; customerId?: string;
  minimumSaleAmount: number; maximumCommission: number; effectiveFrom?: string; effectiveTo?: string;
}
export interface CommissionStatementSummary {
  id: string; statementNumber: string; salesRepName: string;
  periodStart: string; periodEnd: string; totalSales: number;
  totalCommission: number; adjustments: number; netCommission: number;
  status: string; lineCount: number; createdAt: string;
}
export interface CommissionStatementDetail extends CommissionStatementSummary {
  salesRepId: string; approvedDate?: string; approvedBy?: string;
  paidDate?: string; paymentReference?: string; notes?: string;
  lines: CommissionStatementLine[];
}
export interface CommissionStatementLine {
  id: string; salesOrderNumber?: string; customerName?: string;
  saleAmount: number; commissionRate: number; commissionAmount: number;
  description?: string; transactionDate: string;
}
export interface GenerateStatement { salesRepId: string; periodStart: string; periodEnd: string; }

export const commissionService = {
  async getRules(page = 1, pageSize = 20, search?: string, isActive?: boolean) {
    const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() });
    if (search) params.append('search', search);
    if (isActive !== undefined) params.append('isActive', isActive.toString());
    const res = await fetch(`${API_BASE_URL}/sales/commissions/rules?${params}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch rules');
    return { data: await res.json() };
  },
  async getRuleById(id: string) {
    const res = await fetch(`${API_BASE_URL}/sales/commissions/rules/${id}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch rule');
    return { data: await res.json() };
  },
  async createRule(data: CreateCommissionRule) {
    const res = await fetch(`${API_BASE_URL}/sales/commissions/rules`, { method: 'POST', headers: getAuthHeaders(), body: JSON.stringify(data) });
    if (!res.ok) throw new Error(await res.text() || 'Failed to create rule');
    return { data: await res.json() };
  },
  async updateRule(id: string, data: CreateCommissionRule) {
    const res = await fetch(`${API_BASE_URL}/sales/commissions/rules/${id}`, { method: 'PUT', headers: getAuthHeaders(), body: JSON.stringify(data) });
    if (!res.ok) throw new Error(await res.text() || 'Failed to update rule');
    return { data: await res.json() };
  },
  async activateRule(id: string) {
    const res = await fetch(`${API_BASE_URL}/sales/commissions/rules/${id}/activate`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to activate rule');
    return true;
  },
  async deactivateRule(id: string) {
    const res = await fetch(`${API_BASE_URL}/sales/commissions/rules/${id}/deactivate`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to deactivate rule');
    return true;
  },
  async getStatements(page = 1, pageSize = 20, search?: string, status?: string) {
    const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() });
    if (search) params.append('search', search);
    if (status) params.append('status', status);
    const res = await fetch(`${API_BASE_URL}/sales/commissions/statements?${params}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch statements');
    return { data: await res.json() };
  },
  async generateStatement(data: GenerateStatement) {
    const res = await fetch(`${API_BASE_URL}/sales/commissions/statements/generate`, { method: 'POST', headers: getAuthHeaders(), body: JSON.stringify(data) });
    if (!res.ok) throw new Error(await res.text() || 'Failed to generate statement');
    return { data: await res.json() };
  },
  async approveStatement(id: string) {
    const res = await fetch(`${API_BASE_URL}/sales/commissions/statements/${id}/approve`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to approve statement');
    return { data: await res.json() };
  },
  async markAsPaid(id: string, paymentReference?: string) {
    const params = paymentReference ? `?paymentReference=${encodeURIComponent(paymentReference)}` : '';
    const res = await fetch(`${API_BASE_URL}/sales/commissions/statements/${id}/pay${params}`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to mark as paid');
    return { data: await res.json() };
  },
  async disputeStatement(id: string, reason?: string) {
    const params = reason ? `?reason=${encodeURIComponent(reason)}` : '';
    const res = await fetch(`${API_BASE_URL}/sales/commissions/statements/${id}/dispute${params}`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to dispute statement');
    return { data: await res.json() };
  },
};
