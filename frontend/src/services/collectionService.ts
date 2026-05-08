// Collections & Debt Management API Service

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

function getAuthHeaders(): Record<string, string> {
  const token = typeof window !== 'undefined'
    ? (localStorage.getItem('token') || localStorage.getItem('authToken'))
    : null;
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// ── Collection Activity DTOs ──
export interface CollectionActivitySummaryDto {
  id: string;
  subject: string;
  activityType: string;
  collectionStatus: string;
  customerName?: string;
  outstandingAmount: number;
  promisedAmount: number;
  promisedPayDate?: string;
  outcome?: string;
  activityDate: string;
  followUpDate?: string;
  assignedToName?: string;
  createdAt: string;
}

export interface CreateCollectionActivityDto {
  customerId: string;
  invoiceId?: string;
  subject: string;
  activityType?: string;
  description?: string;
  activityDate?: string;
  followUpDate?: string;
  outstandingAmount: number;
  assignedToId?: string;
  notes?: string;
}

export interface UpdateCollectionActivityDto {
  collectionStatus?: string;
  outcome?: string;
  promisedAmount?: number;
  promisedPayDate?: string;
  followUpDate?: string;
  notes?: string;
}

// ── Payment Plan DTOs ──
export interface PaymentPlanSummaryDto {
  id: string;
  planName: string;
  planStatus: string;
  customerName?: string;
  totalDebt: number;
  totalPaid: number;
  remainingBalance: number;
  numberOfInstallments: number;
  installmentsPaid: number;
  frequency: string;
  startDate: string;
  endDate?: string;
  createdAt: string;
}

export interface PaymentPlanDetailDto extends PaymentPlanSummaryDto {
  customerId: string;
  approvedById?: string;
  approvedByName?: string;
  approvedDate?: string;
  terms?: string;
  notes?: string;
  installments: PaymentPlanInstallmentDto[];
}

export interface PaymentPlanInstallmentDto {
  id: string;
  installmentNumber: number;
  amountDue: number;
  amountPaid: number;
  balance: number;
  dueDate: string;
  paidDate?: string;
  installmentStatus: string;
  paymentReference?: string;
  notes?: string;
}

export interface CreatePaymentPlanDto {
  customerId: string;
  planName: string;
  totalDebt: number;
  numberOfInstallments: number;
  frequency?: string;
  startDate: string;
  terms?: string;
  notes?: string;
}

export interface RecordInstallmentPaymentDto {
  amountPaid: number;
  paymentReference?: string;
  notes?: string;
}

export const collectionService = {
  // ── Collection Activities ──
  async getActivities(page = 1, pageSize = 20, search?: string, status?: string, activityType?: string): Promise<PagedResult<CollectionActivitySummaryDto>> {
    const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() });
    if (search) params.append('search', search);
    if (status) params.append('status', status);
    if (activityType) params.append('activityType', activityType);
    const res = await fetch(`${API_BASE_URL}/sales/collections/activities?${params}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch collection activities');
    return res.json();
  },

  async createActivity(data: CreateCollectionActivityDto): Promise<CollectionActivitySummaryDto> {
    const res = await fetch(`${API_BASE_URL}/sales/collections/activities`, { method: 'POST', headers: getAuthHeaders(), body: JSON.stringify(data) });
    if (!res.ok) throw new Error(await res.text() || 'Failed to create activity');
    return res.json();
  },

  async updateActivity(id: string, data: UpdateCollectionActivityDto): Promise<CollectionActivitySummaryDto> {
    const res = await fetch(`${API_BASE_URL}/sales/collections/activities/${id}`, { method: 'PUT', headers: getAuthHeaders(), body: JSON.stringify(data) });
    if (!res.ok) throw new Error('Failed to update activity');
    return res.json();
  },

  async getOverdueFollowUps(daysOverdue = 0): Promise<CollectionActivitySummaryDto[]> {
    const res = await fetch(`${API_BASE_URL}/sales/collections/activities/overdue?daysOverdue=${daysOverdue}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch overdue follow-ups');
    return res.json();
  },

  // ── Payment Plans ──
  async getPlans(page = 1, pageSize = 20, search?: string, status?: string): Promise<PagedResult<PaymentPlanSummaryDto>> {
    const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() });
    if (search) params.append('search', search);
    if (status) params.append('status', status);
    const res = await fetch(`${API_BASE_URL}/sales/collections/plans?${params}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch payment plans');
    return res.json();
  },

  async getPlanById(id: string): Promise<PaymentPlanDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/collections/plans/${id}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch payment plan');
    return res.json();
  },

  async createPlan(data: CreatePaymentPlanDto): Promise<PaymentPlanDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/collections/plans`, { method: 'POST', headers: getAuthHeaders(), body: JSON.stringify(data) });
    if (!res.ok) throw new Error(await res.text() || 'Failed to create plan');
    return res.json();
  },

  async approvePlan(id: string): Promise<PaymentPlanDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/collections/plans/${id}/approve`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to approve plan');
    return res.json();
  },

  async cancelPlan(id: string, reason?: string): Promise<PaymentPlanDetailDto> {
    const params = reason ? `?reason=${encodeURIComponent(reason)}` : '';
    const res = await fetch(`${API_BASE_URL}/sales/collections/plans/${id}/cancel${params}`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to cancel plan');
    return res.json();
  },

  async recordPayment(planId: string, installmentId: string, data: RecordInstallmentPaymentDto): Promise<PaymentPlanDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/collections/plans/${planId}/installments/${installmentId}/pay`, {
      method: 'POST', headers: getAuthHeaders(), body: JSON.stringify(data)
    });
    if (!res.ok) throw new Error('Failed to record payment');
    return res.json();
  },

  async getOverdueInstallments(customerId?: string): Promise<PaymentPlanInstallmentDto[]> {
    const params = customerId ? `?customerId=${customerId}` : '';
    const res = await fetch(`${API_BASE_URL}/sales/collections/installments/overdue${params}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch overdue installments');
    return res.json();
  },
};
