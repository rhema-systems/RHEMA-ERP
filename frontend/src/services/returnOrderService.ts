// Return Order, Credit Note & Refund API Service

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

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

// ── Return Order DTOs ──
export interface ReturnOrderSummaryDto {
  id: string;
  documentNumber: string;
  returnStatus: string;
  reasonCode: string;
  customerName?: string;
  salesOrderNumber?: string;
  totalAmount: number;
  lineCount: number;
  receivedDate?: string;
  createdAt: string;
}

export interface ReturnOrderDetailDto extends ReturnOrderSummaryDto {
  salesOrderId: string;
  deliveryNoteId?: string;
  customerId: string;
  reasonDescription?: string;
  inspectedDate?: string;
  inspectedByName?: string;
  inspectionNotes?: string;
  creditNoteId?: string;
  creditNoteNumber?: string;
  refundId?: string;
  lines: ReturnOrderLineDto[];
}

export interface ReturnOrderLineDto {
  id: string;
  description: string;
  productCode?: string;
  quantityReturned: number;
  unitPrice: number;
  lineTotal: number;
  reasonCode: string;
  condition?: string;
  isRestockable: boolean;
}

export interface CreateReturnOrderDto {
  salesOrderId: string;
  deliveryNoteId?: string;
  customerId: string;
  reasonCode: string;
  reasonDescription?: string;
  lines: CreateReturnOrderLineDto[];
}

export interface CreateReturnOrderLineDto {
  salesOrderLineId?: string;
  description: string;
  productCode?: string;
  quantityReturned: number;
  unitPrice: number;
  reasonCode: string;
  condition?: string;
  isRestockable?: boolean;
}

// ── Credit Note DTOs ──
export interface CreditNoteSummaryDto {
  id: string;
  documentNumber: string;
  creditNoteStatus: string;
  customerName?: string;
  totalAmount: number;
  reason?: string;
  appliedDate?: string;
  lineCount: number;
  createdAt: string;
}

export interface CreditNoteDetailDto extends CreditNoteSummaryDto {
  customerId: string;
  returnOrderId?: string;
  returnOrderNumber?: string;
  originalInvoiceId?: string;
  appliedToInvoiceId?: string;
  taxAmount: number;
  lines: CreditNoteLineDto[];
}

export interface CreditNoteLineDto {
  id: string;
  description: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  taxAmount: number;
  taxCode?: string;
}

// ── Refund DTOs ──
export interface RefundSummaryDto {
  id: string;
  documentNumber: string;
  refundStatus: string;
  customerName?: string;
  refundAmount: number;
  refundMethod: string;
  processedDate?: string;
  createdAt: string;
}

export interface RefundDetailDto extends RefundSummaryDto {
  customerId: string;
  creditNoteId?: string;
  creditNoteNumber?: string;
  returnOrderId?: string;
  reason?: string;
  processedByName?: string;
  paymentReference?: string;
}

export interface CreateRefundDto {
  customerId: string;
  creditNoteId?: string;
  returnOrderId?: string;
  refundAmount: number;
  refundMethod?: string;
  reason?: string;
}

export interface CreditNoteApprovalDto {
  isApproved: boolean;
  comments?: string;
  rejectionReason?: string;
}

export interface RefundApprovalDto {
  isApproved: boolean;
  comments?: string;
  rejectionReason?: string;
}

export const returnOrderService = {
  // ── Return Orders ──
  async getReturnOrders(page = 1, pageSize = 20, search?: string, status?: string): Promise<PagedResult<ReturnOrderSummaryDto>> {
    const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() });
    if (search) params.append('search', search);
    if (status) params.append('status', status);
    const res = await fetch(`${API_BASE_URL}/sales/return-orders?${params}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch return orders');
    return res.json();
  },

  async getReturnOrderById(id: string): Promise<ReturnOrderDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/return-orders/${id}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch return order');
    return res.json();
  },

  async createReturnOrder(data: CreateReturnOrderDto): Promise<ReturnOrderDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/return-orders`, { method: 'POST', headers: getAuthHeaders(), body: JSON.stringify(data) });
    if (!res.ok) throw new Error(await res.text() || 'Failed to create return order');
    return res.json();
  },

  async approveReturnOrder(id: string): Promise<ReturnOrderDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/return-orders/${id}/approve`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to approve');
    return res.json();
  },

  async receiveReturnOrder(id: string): Promise<ReturnOrderDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/return-orders/${id}/receive`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to receive');
    return res.json();
  },

  async inspectReturnOrder(id: string, notes?: string): Promise<ReturnOrderDetailDto> {
    const params = notes ? `?notes=${encodeURIComponent(notes)}` : '';
    const res = await fetch(`${API_BASE_URL}/sales/return-orders/${id}/inspect${params}`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to inspect');
    return res.json();
  },

  async issueCreditNote(id: string): Promise<CreditNoteDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/return-orders/${id}/issue-credit`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to issue credit note');
    return res.json();
  },

  async rejectReturnOrder(id: string, reason?: string): Promise<ReturnOrderDetailDto> {
    const params = reason ? `?reason=${encodeURIComponent(reason)}` : '';
    const res = await fetch(`${API_BASE_URL}/sales/return-orders/${id}/reject${params}`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to reject');
    return res.json();
  },

  // ── Credit Notes ──
  async getCreditNotes(page = 1, pageSize = 20, search?: string, status?: string): Promise<PagedResult<CreditNoteSummaryDto>> {
    const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() });
    if (search) params.append('search', search);
    if (status) params.append('status', status);
    const res = await fetch(`${API_BASE_URL}/sales/credit-notes?${params}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch credit notes');
    return res.json();
  },

  async getCreditNoteById(id: string): Promise<CreditNoteDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/credit-notes/${id}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch credit note');
    return res.json();
  },

  async approveCreditNote(id: string): Promise<CreditNoteDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/credit-notes/${id}/approve`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to approve credit note');
    return res.json();
  },

  async submitCreditNoteForApproval(id: string): Promise<CreditNoteDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/credit-notes/${id}/submit`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error(await res.text() || 'Failed to submit credit note');
    return res.json();
  },

  async processCreditNoteApproval(id: string, data: CreditNoteApprovalDto): Promise<CreditNoteDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/credit-notes/${id}/workflow-approval`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!res.ok) throw new Error(await res.text() || 'Failed to process credit note approval');
    return res.json();
  },

  async applyCreditNote(id: string, invoiceId?: string): Promise<CreditNoteDetailDto> {
    const params = invoiceId ? `?invoiceId=${invoiceId}` : '';
    const res = await fetch(`${API_BASE_URL}/sales/credit-notes/${id}/apply${params}`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to apply credit note');
    return res.json();
  },

  async voidCreditNote(id: string, reason?: string): Promise<CreditNoteDetailDto> {
    const params = reason ? `?reason=${encodeURIComponent(reason)}` : '';
    const res = await fetch(`${API_BASE_URL}/sales/credit-notes/${id}/void${params}`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to void credit note');
    return res.json();
  },

  // ── Refunds ──
  async getRefunds(page = 1, pageSize = 20, search?: string, status?: string): Promise<PagedResult<RefundSummaryDto>> {
    const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() });
    if (search) params.append('search', search);
    if (status) params.append('status', status);
    const res = await fetch(`${API_BASE_URL}/sales/refunds?${params}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch refunds');
    return res.json();
  },

  async getRefundById(id: string): Promise<RefundDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/refunds/${id}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch refund');
    return res.json();
  },

  async createRefund(data: CreateRefundDto): Promise<RefundDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/refunds`, { method: 'POST', headers: getAuthHeaders(), body: JSON.stringify(data) });
    if (!res.ok) throw new Error(await res.text() || 'Failed to create refund');
    return res.json();
  },

  async approveRefund(id: string): Promise<RefundDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/refunds/${id}/approve`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to approve refund');
    return res.json();
  },

  async submitRefundForApproval(id: string): Promise<RefundDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/refunds/${id}/submit`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error(await res.text() || 'Failed to submit refund');
    return res.json();
  },

  async processRefundApproval(id: string, data: RefundApprovalDto): Promise<RefundDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/refunds/${id}/workflow-approval`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!res.ok) throw new Error(await res.text() || 'Failed to process refund approval');
    return res.json();
  },

  async processRefund(id: string, paymentReference?: string): Promise<RefundDetailDto> {
    const params = paymentReference ? `?paymentReference=${encodeURIComponent(paymentReference)}` : '';
    const res = await fetch(`${API_BASE_URL}/sales/refunds/${id}/process${params}`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to process refund');
    return res.json();
  },

  async rejectRefund(id: string, reason?: string): Promise<RefundDetailDto> {
    const params = reason ? `?reason=${encodeURIComponent(reason)}` : '';
    const res = await fetch(`${API_BASE_URL}/sales/refunds/${id}/reject${params}`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to reject refund');
    return res.json();
  },
};
