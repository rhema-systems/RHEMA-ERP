/**
 * RFQ Service
 * Request For Quotation (RFQ) is intentionally separate from Tender.
 */

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

const getAuthHeaders = () => {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token && { Authorization: `Bearer ${token}` }),
  };
};

const readApiError = async (response: Response): Promise<string> => {
  const contentType = response.headers.get('content-type') || '';

  if (contentType.includes('application/json')) {
    try {
      const data: any = await response.json();
      return data?.detail || data?.title || data?.message || JSON.stringify(data);
    } catch {
      // fall through
    }
  }

  try {
    const text = await response.text();
    return text || response.statusText || 'Request failed';
  } catch {
    return response.statusText || 'Request failed';
  }
};

export interface RfqDto {
  id: string;
  rfqNumber: string;
  title: string;
  status: string; // Draft, Sent, Closed, Awarded, Cancelled
  submissionDeadline?: string;
  currency: string;
  estimatedValue: number;
  sourcePurchaseRequisitionId?: string;
  createdAt: string;
  sentAt?: string;
  supplierCount: number;
  quoteCount: number;
}

export interface RfqItemDto {
  id: string;
  lineNumber: number;
  inventoryItemId?: string;
  itemCode?: string;
  description: string;
  quantity: number;
  unitOfMeasure: string;
  specifications?: string;
  requiredDeliveryDate?: string;
}

export interface RfqInvitationDto {
  id: string;
  businessPartnerId: string;
  partnerCode: string;
  partnerName: string;
  primaryEmail?: string;
  status: string; // Selected, Invited, Responded, Declined
  invitedAt: string;
}

export interface RfqQuoteItemDto {
  rfqItemId: string;
  lineNumber: number;
  description: string;
  quantity: number;
  unitOfMeasure: string;
  unitPrice: number;
  lineTotal: number;
  isAwarded?: boolean;
}

export interface RfqQuoteDto {
  id: string;
  businessPartnerId: string;
  partnerCode: string;
  partnerName: string;
  status: string;
  submittedAt?: string;
  notes?: string;
  totalAmount: number;
  items: RfqQuoteItemDto[];
}

export interface RfqDetailDto extends RfqDto {
  description?: string;
  externalRecipientEmails?: string;
  items: RfqItemDto[];
  suppliers: RfqInvitationDto[];
  quotes: RfqQuoteDto[];
}

export interface CreatePurchaseOrdersFromRfqDto {
  mode: 'WinnerTakesAll' | 'SplitAward';
  quoteId?: string;
  lines?: { rfqItemId: string; quoteId: string; awardReason?: string }[];
}

export interface CreatePurchaseOrdersFromRfqResponseDto {
  purchaseOrders: Array<{
    purchaseOrderId: string;
    orderNumber: string;
    businessPartnerId: string;
    businessPartnerName: string;
    totalAmount: number;
  }>;
}

export interface UpdateRfqDto {
  title?: string;
  description?: string;
  submissionDeadline?: string;
  currency?: string;
  estimatedValue?: number;
  supplierIds?: string[];
  externalRecipientEmails?: string;
}

export interface SendRfqDto {
  supplierIds: string[];
  externalRecipientEmails?: string;
}

export const rfqService = {
  async getRfqs(params?: { page?: number; pageSize?: number; search?: string; status?: string }) {
    const query = new URLSearchParams();
    if (params?.page) query.append('page', params.page.toString());
    if (params?.pageSize) query.append('pageSize', params.pageSize.toString());
    if (params?.search) query.append('search', params.search);
    if (params?.status) query.append('status', params.status);

    const response = await fetch(`${API_BASE_URL}/procurement/rfqs?${query.toString()}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readApiError(response));
    return response.json();
  },

  async getRfqById(id: string): Promise<RfqDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/rfqs/${id}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readApiError(response));
    return response.json();
  },

  async getRfqPdf(id: string): Promise<Blob> {
    const response = await fetch(`${API_BASE_URL}/procurement/rfqs/${id}/pdf`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readApiError(response));
    return response.blob();
  },

  async updateRfq(id: string, dto: UpdateRfqDto): Promise<RfqDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/rfqs/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) {
      throw new Error(await readApiError(response));
    }
    return response.json();
  },

  async sendRfq(id: string, dto: SendRfqDto): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/rfqs/${id}/send`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) {
      throw new Error(await readApiError(response));
    }
  },

  // Supplier portal endpoints
  async getMyRfqs(): Promise<RfqDto[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/rfqs/my-rfqs`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readApiError(response));
    return response.json();
  },

  async getMyRfqDetail(id: string): Promise<RfqDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/rfqs/${id}/my-view`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readApiError(response));
    return response.json();
  },

  async submitQuote(id: string, dto: { notes?: string; items: { rfqItemId: string; unitPrice: number }[] }): Promise<RfqQuoteDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/rfqs/${id}/quote`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) {
      throw new Error(await readApiError(response));
    }
    return response.json();
  },

  async awardAndCreatePurchaseOrders(id: string, dto: CreatePurchaseOrdersFromRfqDto): Promise<CreatePurchaseOrdersFromRfqResponseDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/rfqs/${id}/award`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) {
      throw new Error(await readApiError(response));
    }
    return response.json();
  },
};
