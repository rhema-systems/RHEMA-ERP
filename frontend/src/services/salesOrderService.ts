// Sales Order & Delivery Note API Service

// ==================== INTERFACES ====================

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface SalesOrderSummaryDto {
  id: string;
  orderNumber: string;
  orderDate: string;
  businessPartnerId: string;
  customerName: string;
  orderType: string;
  status: string;
  priority: string;
  totalAmount: number;
  currency: string;
  salesRepName?: string;
  propertyReference?: string;
  expectedDeliveryDate?: string;
  deliveryProgress: number;
  createdAt: string;
}

export interface SalesOrderDetailDto {
  id: string;
  orderNumber: string;
  orderDate: string;
  businessPartnerId: string;
  customerName: string;
  orderType: string;
  status: string;
  priority: string;
  subtotalAmount: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  currency: string;
  paymentTerms?: string;
  salesRepId?: string;
  salesRepName?: string;
  propertyReference?: string;
  propertyType?: string;
  shippingAddress?: string;
  billingAddress?: string;
  customerPoNumber?: string;
  expectedDeliveryDate?: string;
  notes?: string;
  internalNotes?: string;
  rejectionReason?: string;
  cancellationReason?: string;
  holdReason?: string;
  sourceQuoteId?: string;
  sourceQuoteNumber?: string;
  approvedById?: string;
  approvedByName?: string;
  approvedDate?: string;
  confirmedDate?: string;
  deliveryProgress: number;
  lines: SalesOrderLineDto[];
  statusHistory: SalesOrderStatusHistoryDto[];
  createdAt: string;
  createdByName?: string;
  modifiedAt?: string;
}

export interface SalesOrderLineDto {
  id: string;
  lineNumber: number;
  itemId?: string;
  itemCode?: string;
  itemName: string;
  description?: string;
  quantity: number;
  unitOfMeasure: string;
  unitPrice: number;
  discountPercent: number;
  taxPercent: number;
  lineTotal: number;
  deliveredQuantity: number;
  remainingQuantity: number;
}

export interface SalesOrderStatusHistoryDto {
  id: string;
  fromStatus: string;
  toStatus: string;
  changedByName: string;
  changedDate: string;
  reason?: string;
}

export interface CreateSalesOrderDto {
  businessPartnerId: string;
  orderType?: string;
  priority?: string;
  expectedDeliveryDate?: string;
  paymentTerms?: string;
  salesRepId?: string;
  propertyReference?: string;
  propertyType?: string;
  shippingAddress?: string;
  billingAddress?: string;
  customerPoNumber?: string;
  notes?: string;
  internalNotes?: string;
  lines: CreateSalesOrderLineDto[];
}

export interface CreateSalesOrderLineDto {
  itemId?: string;
  itemCode?: string;
  itemName: string;
  description?: string;
  quantity: number;
  unitOfMeasure?: string;
  unitPrice: number;
  discountPercent?: number;
  taxPercent?: number;
}

export interface UpdateSalesOrderDto {
  expectedDeliveryDate?: string;
  priority?: string;
  paymentTerms?: string;
  salesRepId?: string;
  propertyReference?: string;
  propertyType?: string;
  shippingAddress?: string;
  billingAddress?: string;
  customerPoNumber?: string;
  notes?: string;
  internalNotes?: string;
  lines?: CreateSalesOrderLineDto[];
}

export interface SalesOrderApprovalDto {
  isApproved: boolean;
  comments?: string;
}

export interface CancelSalesOrderDto {
  reason: string;
}

// Delivery Note interfaces
export interface DeliveryNoteSummaryDto {
  id: string;
  deliveryNumber: string;
  salesOrderId: string;
  salesOrderNumber: string;
  customerName: string;
  status: string;
  deliveryDate?: string;
  shippingAddress?: string;
  carrierName?: string;
  trackingNumber?: string;
  totalItems: number;
  createdAt: string;
}

export interface DeliveryNoteDetailDto {
  id: string;
  deliveryNumber: string;
  salesOrderId: string;
  salesOrderNumber: string;
  businessPartnerId: string;
  customerName: string;
  status: string;
  deliveryDate?: string;
  shippingAddress?: string;
  carrierName?: string;
  trackingNumber?: string;
  receivedByName?: string;
  notes?: string;
  lines: DeliveryNoteLineDto[];
  createdAt: string;
  createdByName?: string;
  packedDate?: string;
  shippedDate?: string;
  deliveredDate?: string;
}

export interface DeliveryNoteLineDto {
  id: string;
  salesOrderLineId: string;
  itemName: string;
  itemCode?: string;
  quantity: number;
  unitOfMeasure: string;
  deliveredQuantity: number;
}

export interface CreateDeliveryNoteDto {
  salesOrderId: string;
  shippingAddress?: string;
  notes?: string;
  lines: CreateDeliveryNoteLineDto[];
}

export interface CreateDeliveryNoteLineDto {
  salesOrderLineId: string;
  quantity: number;
}

export interface ConfirmDeliveryDto {
  receivedByName?: string;
  notes?: string;
  lines?: ConfirmDeliveryLineDto[];
}

export interface ConfirmDeliveryLineDto {
  deliveryNoteLineId: string;
  deliveredQuantity: number;
}

// ==================== API FUNCTIONS ====================

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

export const salesOrderService = {
  // ── Sales Orders ──────────────────────────────────────────────────

  async getSalesOrders(
    page: number = 1,
    pageSize: number = 20,
    search?: string,
    status?: string,
    customerId?: string,
    orderType?: string,
    startDate?: string,
    endDate?: string,
    priority?: string,
  ): Promise<PagedResult<SalesOrderSummaryDto>> {
    const params = new URLSearchParams();
    params.append('page', page.toString());
    params.append('pageSize', pageSize.toString());
    if (search) params.append('search', search);
    if (status) params.append('status', status);
    if (customerId) params.append('customerId', customerId);
    if (orderType) params.append('orderType', orderType);
    if (startDate) params.append('startDate', startDate);
    if (endDate) params.append('endDate', endDate);
    if (priority) params.append('priority', priority);

    const response = await fetch(`${API_BASE_URL}/sales/orders?${params.toString()}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch sales orders');
    return response.json();
  },

  async getSalesOrderById(id: string): Promise<SalesOrderDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/orders/${id}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch sales order');
    return response.json();
  },

  async createSalesOrder(data: CreateSalesOrderDto): Promise<SalesOrderDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/orders`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create sales order');
    }
    return response.json();
  },

  async updateSalesOrder(id: string, data: UpdateSalesOrderDto): Promise<SalesOrderDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/orders/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update sales order');
    }
    return response.json();
  },

  async submitForApproval(id: string): Promise<SalesOrderDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/orders/${id}/submit`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to submit sales order');
    }
    return response.json();
  },

  async processApproval(id: string, data: SalesOrderApprovalDto): Promise<SalesOrderDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/orders/${id}/approve`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to process approval');
    }
    return response.json();
  },

  async confirmSalesOrder(id: string): Promise<SalesOrderDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/orders/${id}/confirm`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to confirm sales order');
    }
    return response.json();
  },

  async cancelSalesOrder(id: string, data: CancelSalesOrderDto): Promise<SalesOrderDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/orders/${id}/cancel`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to cancel sales order');
    }
    return response.json();
  },

  async putOnHold(id: string, reason?: string): Promise<SalesOrderDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/orders/${id}/hold`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(reason || ''),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to put order on hold');
    }
    return response.json();
  },

  async releaseFromHold(id: string): Promise<SalesOrderDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/orders/${id}/release`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to release order');
    }
    return response.json();
  },

  async closeSalesOrder(id: string): Promise<SalesOrderDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/orders/${id}/close`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to close sales order');
    }
    return response.json();
  },

  async validateCredit(businessPartnerId: string, amount: number): Promise<{ isValid: boolean; outstandingBalance: number; requestedAmount: number }> {
    const response = await fetch(`${API_BASE_URL}/sales/orders/validate-credit/${businessPartnerId}?amount=${amount}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to validate credit');
    return response.json();
  },

  // ── Delivery Notes ────────────────────────────────────────────────

  async getDeliveryNotes(
    page: number = 1,
    pageSize: number = 20,
    search?: string,
    status?: string,
    salesOrderId?: string,
    customerId?: string,
    startDate?: string,
    endDate?: string,
  ): Promise<PagedResult<DeliveryNoteSummaryDto>> {
    const params = new URLSearchParams();
    params.append('page', page.toString());
    params.append('pageSize', pageSize.toString());
    if (search) params.append('search', search);
    if (status) params.append('status', status);
    if (salesOrderId) params.append('salesOrderId', salesOrderId);
    if (customerId) params.append('customerId', customerId);
    if (startDate) params.append('startDate', startDate);
    if (endDate) params.append('endDate', endDate);

    const response = await fetch(`${API_BASE_URL}/sales/deliveries?${params.toString()}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch delivery notes');
    return response.json();
  },

  async getDeliveryNoteById(id: string): Promise<DeliveryNoteDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/deliveries/${id}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch delivery note');
    return response.json();
  },

  async createDeliveryNote(data: CreateDeliveryNoteDto): Promise<DeliveryNoteDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/deliveries`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create delivery note');
    }
    return response.json();
  },

  async markAsPacked(id: string): Promise<DeliveryNoteDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/deliveries/${id}/pack`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to mark as packed');
    }
    return response.json();
  },

  async markAsShipped(id: string, carrierName?: string, trackingNumber?: string): Promise<DeliveryNoteDetailDto> {
    const params = new URLSearchParams();
    if (carrierName) params.append('carrierName', carrierName);
    if (trackingNumber) params.append('trackingNumber', trackingNumber);

    const response = await fetch(`${API_BASE_URL}/sales/deliveries/${id}/ship?${params.toString()}`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to mark as shipped');
    }
    return response.json();
  },

  async confirmDelivery(id: string, data: ConfirmDeliveryDto): Promise<DeliveryNoteDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/deliveries/${id}/confirm`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to confirm delivery');
    }
    return response.json();
  },

  async cancelDeliveryNote(id: string, reason?: string): Promise<DeliveryNoteDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/deliveries/${id}/cancel`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(reason || ''),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to cancel delivery note');
    }
    return response.json();
  },

  async getDeliverableLines(salesOrderId: string): Promise<SalesOrderLineDto[]> {
    const response = await fetch(`${API_BASE_URL}/sales/deliveries/deliverable-lines/${salesOrderId}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch deliverable lines');
    return response.json();
  },
};
