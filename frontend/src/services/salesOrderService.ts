// Sales Order & Delivery Note API Service

// ==================== INTERFACES ====================

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface SalesLinkedProjectUnitContextDto {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  projectUnitId: string;
  projectUnitCode?: string;
  projectUnitName: string;
  projectUnitType: string;
  projectUnitStatus: string;
  projectUnitCommercialStatus: string;
  projectUnitHandoverStatus: string;
  isReleasedForMarket: boolean;
  handoverDate?: string;
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
  projectUnitContext?: SalesLinkedProjectUnitContextDto;
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
  paymentTermId?: string;
  paymentTermsDays?: number;
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
  projectUnitContext?: SalesLinkedProjectUnitContextDto;
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
  paymentTermId?: string;
  currency?: string;
  salesRepId?: string;
  propertyReference?: string;
  propertyType?: string;
  shippingAddress?: string;
  billingAddress?: string;
  customerPoNumber?: string;
  notes?: string;
  internalNotes?: string;
  // Finance posting consumes these optional document-level currency and tax fields when supplied by Sales UI flows.
  exchangeRate?: number;
  taxGroupId?: string;
  quoteId?: string;
  opportunityId?: string;
  lines: CreateSalesOrderLineDto[];
}

export interface CreateSalesOrderLineDto {
  itemId?: string;
  productId?: string;
  inventoryItemId?: string;
  itemCode?: string;
  productCode?: string;
  itemName: string;
  description?: string;
  quantity: number;
  unitOfMeasure?: string;
  unit?: string;
  unitPrice: number;
  discountPercent?: number;
  discountPercentage?: number;
  taxPercent?: number;
  taxRate?: number;
  taxGroupId?: string;
  warehouseId?: string;
  locationId?: string;
}

export interface UpdateSalesOrderDto {
  expectedDeliveryDate?: string;
  priority?: string;
  paymentTerms?: string;
  paymentTermId?: string;
  salesRepId?: string;
  propertyReference?: string;
  propertyType?: string;
  shippingAddress?: string;
  billingAddress?: string;
  customerPoNumber?: string;
  notes?: string;
  internalNotes?: string;
  currency?: string;
  exchangeRate?: number;
  taxGroupId?: string;
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

function normalizeProjectUnitContext(raw: any): SalesLinkedProjectUnitContextDto | undefined {
  if (!raw) {
    return undefined;
  }

  return {
    projectId: raw.projectId,
    projectCode: raw.projectCode ?? '',
    projectTitle: raw.projectTitle ?? '',
    projectUnitId: raw.projectUnitId,
    projectUnitCode: raw.projectUnitCode,
    projectUnitName: raw.projectUnitName ?? '',
    projectUnitType: raw.projectUnitType ?? '',
    projectUnitStatus: raw.projectUnitStatus ?? '',
    projectUnitCommercialStatus: raw.projectUnitCommercialStatus ?? '',
    projectUnitHandoverStatus: raw.projectUnitHandoverStatus ?? '',
    isReleasedForMarket: Boolean(raw.isReleasedForMarket),
    handoverDate: raw.handoverDate,
  };
}

function normalizeSalesOrderSummary(raw: any): SalesOrderSummaryDto {
  return {
    id: raw.id,
    orderNumber: raw.orderNumber ?? raw.documentNumber ?? '',
    orderDate: raw.orderDate ?? raw.documentDate ?? '',
    businessPartnerId: raw.businessPartnerId,
    customerName: raw.customerName ?? '',
    orderType: raw.orderType != null ? String(raw.orderType) : '',
    status: raw.status ?? (raw.orderStatus != null ? String(raw.orderStatus) : ''),
    priority: raw.priority ?? raw.orderPriority ?? 'Normal',
    totalAmount: raw.totalAmount ?? 0,
    currency: raw.currency ?? 'GHS',
    salesRepName: raw.salesRepName,
    propertyReference: raw.propertyReference,
    expectedDeliveryDate: raw.expectedDeliveryDate ?? raw.requestedDeliveryDate ?? raw.promisedDeliveryDate,
    deliveryProgress: raw.deliveryProgress ?? 0,
    createdAt: raw.createdAt ?? raw.documentDate ?? '',
    projectUnitContext: normalizeProjectUnitContext(raw.projectUnitContext),
  };
}

function normalizeSalesOrderDetail(raw: any): SalesOrderDetailDto {
  return {
    id: raw.id,
    orderNumber: raw.orderNumber ?? raw.documentNumber ?? '',
    orderDate: raw.orderDate ?? raw.documentDate ?? '',
    businessPartnerId: raw.businessPartnerId,
    customerName: raw.customerName ?? '',
    orderType: raw.orderType != null ? String(raw.orderType) : '',
    status: raw.status ?? (raw.orderStatus != null ? String(raw.orderStatus) : ''),
    priority: raw.priority ?? raw.orderPriority ?? 'Normal',
    subtotalAmount: raw.subtotalAmount ?? raw.subTotal ?? 0,
    discountAmount: raw.discountAmount ?? 0,
    taxAmount: raw.taxAmount ?? 0,
    totalAmount: raw.totalAmount ?? 0,
    currency: raw.currency ?? 'GHS',
    paymentTerms: raw.paymentTerms ?? (raw.paymentTermsDays ? `Net ${raw.paymentTermsDays} days` : undefined),
    salesRepId: raw.salesRepId,
    salesRepName: raw.salesRepName,
    propertyReference: raw.propertyReference,
    propertyType: raw.propertyType != null ? String(raw.propertyType) : undefined,
    shippingAddress: raw.shippingAddress,
    billingAddress: raw.billingAddress,
    customerPoNumber: raw.customerPoNumber ?? raw.referenceNumber,
    expectedDeliveryDate: raw.expectedDeliveryDate ?? raw.requestedDeliveryDate ?? raw.promisedDeliveryDate,
    notes: raw.notes ?? raw.externalNotes,
    internalNotes: raw.internalNotes,
    rejectionReason: raw.rejectionReason,
    cancellationReason: raw.cancellationReason,
    holdReason: raw.holdReason,
    sourceQuoteId: raw.sourceQuoteId ?? raw.quoteId,
    sourceQuoteNumber: raw.sourceQuoteNumber ?? raw.quoteNumber,
    approvedById: raw.approvedById,
    approvedByName: raw.approvedByName,
    approvedDate: raw.approvedDate,
    confirmedDate: raw.confirmedDate,
    deliveryProgress: raw.deliveryProgress ?? 0,
    lines: Array.isArray(raw.lines)
      ? raw.lines.map((line: any) => ({
          id: line.id,
          lineNumber: line.lineNumber ?? 0,
          itemId: line.itemId ?? line.inventoryItemId ?? line.productId,
          itemCode: line.itemCode ?? line.productCode,
          itemName: line.itemName ?? line.description ?? '',
          description: line.description,
          quantity: line.quantity ?? 0,
          unitOfMeasure: line.unitOfMeasure ?? line.unit ?? '',
          unitPrice: line.unitPrice ?? 0,
          discountPercent: line.discountPercent ?? line.discountPercentage ?? 0,
          taxPercent: line.taxPercent ?? line.taxRate ?? 0,
          lineTotal: line.lineTotal ?? 0,
          deliveredQuantity: line.deliveredQuantity ?? 0,
          remainingQuantity: line.remainingQuantity ?? 0,
        }))
      : [],
    statusHistory: Array.isArray(raw.statusHistory)
      ? raw.statusHistory.map((entry: any) => ({
          id: entry.id,
          fromStatus: entry.fromStatus != null ? String(entry.fromStatus) : '',
          toStatus: entry.toStatus != null ? String(entry.toStatus) : '',
          changedByName: entry.changedByName ?? '',
          changedDate: entry.changedDate ?? entry.changedAt ?? '',
          reason: entry.reason ?? entry.notes,
        }))
      : [],
    createdAt: raw.createdAt ?? raw.documentDate ?? '',
    createdByName: raw.createdByName,
    modifiedAt: raw.modifiedAt ?? raw.updatedAt,
    projectUnitContext: normalizeProjectUnitContext(raw.projectUnitContext),
  };
}

function toBackendCreateSalesOrderDto(data: CreateSalesOrderDto) {
  return {
    ...data,
    orderPriority: data.priority,
    requestedDeliveryDate: data.expectedDeliveryDate,
    promisedDeliveryDate: data.expectedDeliveryDate,
    quoteId: data.quoteId,
    opportunityId: data.opportunityId,
    terms: data.paymentTerms,
    externalNotes: data.notes,
    referenceNumber: data.customerPoNumber,
    lines: data.lines.map((line) => ({
      productId: line.productId,
      inventoryItemId: line.inventoryItemId,
      description: line.description || line.itemName,
      productCode: line.productCode || line.itemCode,
      quantity: line.quantity,
      unitPrice: line.unitPrice,
      discountPercentage: line.discountPercentage ?? line.discountPercent,
      taxRate: line.taxRate ?? line.taxPercent,
      unit: line.unit || line.unitOfMeasure,
      warehouseId: line.warehouseId,
      locationId: line.locationId,
    })),
  };
}

// ==================== API FUNCTIONS ====================

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
    projectLinkedOnly?: boolean,
    releasedUnitsOnly?: boolean,
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
    if (projectLinkedOnly) params.append('projectLinkedOnly', 'true');
    if (releasedUnitsOnly) params.append('releasedUnitsOnly', 'true');

    const response = await fetch(`${API_BASE_URL}/sales/orders?${params.toString()}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch sales orders');
    const result = await response.json();
    return {
      items: Array.isArray(result.items) ? result.items.map(normalizeSalesOrderSummary) : [],
      totalCount: result.totalCount ?? 0,
      page: result.page ?? page,
      pageSize: result.pageSize ?? pageSize,
      totalPages: result.totalPages ?? Math.max(1, Math.ceil((result.totalCount ?? 0) / ((result.pageSize ?? pageSize) || 1))),
    };
  },

  async getSalesOrderById(id: string): Promise<SalesOrderDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/orders/${id}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch sales order');
    return normalizeSalesOrderDetail(await response.json());
  },

  async createSalesOrder(data: CreateSalesOrderDto): Promise<SalesOrderDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/orders`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(toBackendCreateSalesOrderDto(data)),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create sales order');
    }
    return normalizeSalesOrderDetail(await response.json());
  },

  async convertQuoteToSalesOrder(quoteId: string): Promise<SalesOrderDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/orders/convert-from-quote/${quoteId}`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to convert quote to sales order');
    }
    return normalizeSalesOrderDetail(await response.json());
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
    return normalizeSalesOrderDetail(await response.json());
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
    return normalizeSalesOrderDetail(await response.json());
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
    return normalizeSalesOrderDetail(await response.json());
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
    return normalizeSalesOrderDetail(await response.json());
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
    return normalizeSalesOrderDetail(await response.json());
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
    return normalizeSalesOrderDetail(await response.json());
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
    return normalizeSalesOrderDetail(await response.json());
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
    return normalizeSalesOrderDetail(await response.json());
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
