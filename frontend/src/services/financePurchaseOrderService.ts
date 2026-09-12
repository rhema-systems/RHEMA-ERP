import apiService from './api.service';

type PagedResult<T> = {
    items?: T[];
    Items?: T[];
    data?: T[];
    Data?: T[];
};

export interface FinancePurchaseOrderItem {
    id?: string;
    financePurchaseOrderId?: string;
    lineType: number; // 1 = Inventory, 2 = GLAccount
    inventoryItemId?: string;
    warehouseId?: string;
    glAccountId?: string;
    description: string;
    orderedQuantity: number;
    receivedQuantity?: number;
    invoicedQuantity?: number;
    unitPrice: number;
    taxRate?: number;
    taxAmount?: number;
    taxGroupId?: string | null;
    lineTotal?: number;
    taxCode?: string;
    currencyCode?: string;
}

export interface FinancePurchaseOrder {
    id: string;
    approvalRequired?: boolean;
    tenantId?: string;
    orderNumber?: string;
    vendorId: string;
    supplierId?: string;
    orderDate: string;
    expectedDeliveryDate?: string;
    status: number | string; // 1 = Draft, 2 = Approved, 3 = PartiallyReceived, 4 = Received, 5 = PartiallyInvoiced, 6 = Invoiced, 9 = PendingApproval, 10 = Rejected
    currencyCode: string;
    exchangeRate: number;
    totalAmount: number;
    discountAmount?: number;
    paymentTermId?: string | null;
    paymentTermsDays?: number | null;
    earlyPaymentDiscountPercentage?: number | null;
    earlyPaymentDiscountDueDate?: string | null;
    taxGroupId?: string | null;
    remarks?: string;
    items: FinancePurchaseOrderItem[];
    vendorName?: string;
    supplierName?: string;
}

export interface FinancePurchaseOrderReceiptItem {
    id?: string;
    financePurchaseOrderReceiptId?: string;
    financePurchaseOrderItemId: string;
    quantityReceived: number;
    invoicedQuantity?: number;
    remainingToInvoice?: number;

    // UI helpers
    description?: string;
    orderedQuantity?: number;
    previouslyReceived?: number;
}

export interface FinancePurchaseOrderReceipt {
    id: string;
    approvalRequired?: boolean;
    financePurchaseOrderId: string;
    vendorInvoiceId?: string | null;
    receiptNumber?: string;
    receiptDate: string;
    remarks?: string;
    status?: number | string;
    statusName?: string;
    workflowInstanceId?: string | null;
    submittedAt?: string | null;
    approvedAt?: string | null;
    rejectedAt?: string | null;
    rejectionReason?: string | null;
    items: FinancePurchaseOrderReceiptItem[];

    // UI helpers
    orderNumber?: string;
    vendorName?: string;
}

const STATUS_TO_FINANCE_CODE: Record<string, number> = {
    Draft: 1,
    Approved: 2,
    PartiallyReceived: 3,
    'Partially Received': 3,
    Received: 4,
    PartiallyInvoiced: 5,
    'Partially Invoiced': 5,
    Invoiced: 6,
    Closed: 7,
    Cancelled: 8,
    PendingApproval: 9,
    'Pending Approval': 9,
    Rejected: 10
};

const unwrapList = <T>(response: T[] | PagedResult<T>): T[] => {
    if (Array.isArray(response)) return response;
    return response.items || response.Items || response.data || response.Data || [];
};

const normalizeStatus = (status: unknown): number | string => {
    if (typeof status === 'number') return status;
    if (typeof status !== 'string') return 'Unknown';

    return STATUS_TO_FINANCE_CODE[status] ?? status;
};

const normalizePurchaseOrderItem = (item: any, currencyCode: string): FinancePurchaseOrderItem => ({
    id: item.id,
    financePurchaseOrderId: item.purchaseOrderId || item.financePurchaseOrderId,
    lineType: item.lineType ?? 1,
    inventoryItemId: item.inventoryItemId,
    warehouseId: item.warehouseId,
    glAccountId: item.glAccountId,
    description: item.description || item.itemDescription || item.itemName || item.itemCode || 'Purchase order line',
    orderedQuantity: Number(item.orderedQuantity) || 0,
    receivedQuantity: Number(item.receivedQuantity) || 0,
    invoicedQuantity: Number(item.invoicedQuantity) || 0,
    unitPrice: Number(item.unitPrice) || 0,
    taxRate: Number(item.taxRate) || 0,
    taxAmount: Number(item.taxAmount) || 0,
    taxGroupId: item.taxGroupId || null,
    lineTotal: Number(item.lineTotal) || ((Number(item.orderedQuantity) || 0) * (Number(item.unitPrice) || 0)),
    taxCode: item.taxCode,
    currencyCode
});

const normalizePurchaseOrder = (po: any): FinancePurchaseOrder => {
    const currencyCode = po.currencyCode || 'GHS';

    return {
        id: po.id,
        tenantId: po.tenantId,
        orderNumber: po.orderNumber,
        vendorId: po.vendorId || po.supplierId || po.businessPartnerId,
        supplierId: po.supplierId || po.vendorId || po.businessPartnerId,
        orderDate: po.orderDate,
        expectedDeliveryDate: po.expectedDeliveryDate || po.promisedDate || po.requiredDate,
        status: normalizeStatus(po.status),
        currencyCode,
        exchangeRate: Number(po.exchangeRate) || 1,
        totalAmount: Number(po.totalAmount) || 0,
        discountAmount: Number(po.discountAmount) || 0,
        paymentTermId: po.paymentTermId || null,
        paymentTermsDays: po.paymentTermsDays ?? null,
        earlyPaymentDiscountPercentage: po.earlyPaymentDiscountPercentage ?? null,
        earlyPaymentDiscountDueDate: po.earlyPaymentDiscountDueDate || null,
        taxGroupId: po.taxGroupId || null,
        remarks: po.remarks || po.notes,
        items: Array.isArray(po.items)
            ? po.items.map((item: any) => normalizePurchaseOrderItem(item, currencyCode))
            : [],
        vendorName: po.vendorName || po.supplierName || po.businessPartnerName,
        supplierName: po.supplierName || po.vendorName || po.businessPartnerName
    };
};

const normalizeReceipt = (receipt: any): FinancePurchaseOrderReceipt => ({
    id: receipt.id,
    financePurchaseOrderId: receipt.financePurchaseOrderId || receipt.purchaseOrderId,
    vendorInvoiceId: receipt.vendorInvoiceId || null,
    receiptNumber: receipt.receiptNumber,
    receiptDate: receipt.receiptDate,
    remarks: receipt.remarks || receipt.notes,
    status: receipt.status ?? receipt.statusName,
    statusName: receipt.statusName,
    workflowInstanceId: receipt.workflowInstanceId || null,
    submittedAt: receipt.submittedAt || null,
    approvedAt: receipt.approvedAt || null,
    rejectedAt: receipt.rejectedAt || null,
    rejectionReason: receipt.rejectionReason || null,
    items: Array.isArray(receipt.items)
        ? receipt.items.map((item: any) => ({
            id: item.id,
            financePurchaseOrderReceiptId: item.receiptId || item.financePurchaseOrderReceiptId,
            financePurchaseOrderItemId: item.financePurchaseOrderItemId || item.purchaseOrderItemId,
            quantityReceived: Number(item.quantityReceived ?? item.receivedQuantity) || 0,
            invoicedQuantity: Number(item.invoicedQuantity) || 0,
            remainingToInvoice: Number(item.remainingToInvoice) || 0,
            description: item.description || item.itemName || item.itemCode,
            orderedQuantity: Number(item.orderedQuantity) || 0,
            previouslyReceived: Number(item.previouslyReceived) || 0
        }))
        : [],
    orderNumber: receipt.orderNumber || receipt.purchaseOrderNumber,
    vendorName: receipt.vendorName || receipt.supplierName
});

export const financePurchaseOrderService = {
    // Finance AP PO endpoints backed by the restored finance AP PO tables/API.
    getPurchaseOrders: async (params?: any): Promise<FinancePurchaseOrder[]> => {
        const response = await apiService.get<any>('/finance/ap/purchase-orders', params);
        return unwrapList<any>(response).map(normalizePurchaseOrder);
    },

    getPendingApprovals: async (): Promise<FinancePurchaseOrder[]> => {
        const response = await apiService.get<any>('/finance/ap/purchase-orders/pending-approvals');
        return unwrapList<any>(response).map(normalizePurchaseOrder);
    },

    getPurchaseOrderById: async (id: string): Promise<FinancePurchaseOrder> => {
        const response = await apiService.get<any>(`/finance/ap/purchase-orders/${id}`);
        return normalizePurchaseOrder(response);
    },

    createPurchaseOrder: async (data: Partial<FinancePurchaseOrder>): Promise<FinancePurchaseOrder> => {
        const payload = {
            vendorId: data.vendorId || data.supplierId,
            orderNumber: data.orderNumber,
            orderDate: data.orderDate,
            expectedDeliveryDate: data.expectedDeliveryDate,
            currencyCode: data.currencyCode,
            exchangeRate: data.exchangeRate,
            totalAmount: data.totalAmount,
            discountAmount: data.discountAmount,
            paymentTermId: data.paymentTermId || null,
            taxGroupId: data.taxGroupId || null,
            remarks: data.remarks,
            items: (data.items || []).map(item => ({
                lineType: item.lineType,
                inventoryItemId: item.inventoryItemId,
                warehouseId: item.warehouseId,
                glAccountId: item.glAccountId,
                description: item.description,
                orderedQuantity: item.orderedQuantity,
                unitPrice: item.unitPrice,
                taxRate: item.taxRate,
                taxAmount: item.taxAmount,
                taxGroupId: item.taxGroupId || null,
                lineTotal: item.lineTotal
            }))
        };

        const response = await apiService.post<any>('/finance/ap/purchase-orders', payload);
        return normalizePurchaseOrder(response);
    },

    submitPurchaseOrderForApproval: async (id: string): Promise<FinancePurchaseOrder> => {
        const response = await apiService.post<any>(`/finance/ap/purchase-orders/${id}/submit-for-approval`, {});
        return normalizePurchaseOrder(response);
    },

    approvePurchaseOrder: async (id: string) => {
        const response = await apiService.post<any>(`/finance/ap/purchase-orders/${id}/approve`, {
            approved: true,
            comments: 'Approved from finance AP purchase order screen'
        });
        return normalizePurchaseOrder(response);
    },

    rejectPurchaseOrder: async (id: string, reason: string): Promise<FinancePurchaseOrder> => {
        const response = await apiService.post<any>(`/finance/ap/purchase-orders/${id}/reject`, {
            reason
        });
        return normalizePurchaseOrder(response);
    },

    // Finance receipt endpoints backed by FinancePurchaseOrderReceipts.
    getReceipts: async (params?: any): Promise<FinancePurchaseOrderReceipt[]> => {
        const response = await apiService.get<any>('/finance/ap/purchase-receipts', params);
        return unwrapList<any>(response).map(normalizeReceipt);
    },

    getReceiptsByPo: async (poId: string): Promise<FinancePurchaseOrderReceipt[]> => {
        const response = await apiService.get<any[]>(`/finance/ap/purchase-receipts/by-purchase-order/${poId}`);
        return response.map(normalizeReceipt);
    },

    getReceiptById: async (id: string): Promise<FinancePurchaseOrderReceipt> => {
        const response = await apiService.get<any>(`/finance/ap/purchase-receipts/${id}`);
        return normalizeReceipt(response);
    },

    createReceipt: async (data: Partial<FinancePurchaseOrderReceipt> & { lines?: any[] }): Promise<FinancePurchaseOrderReceipt> => {
        const poId = data.financePurchaseOrderId;
        if (!poId) {
            throw new Error('Purchase order is required before creating a receipt.');
        }

        const lines = data.lines || data.items || [];
        const payload = {
            financePurchaseOrderId: poId,
            receiptNumber: data.receiptNumber,
            receiptDate: data.receiptDate,
            notes: data.remarks,
            remarks: data.remarks,
            items: lines.map((line: any) => ({
                financePurchaseOrderItemId: line.financePurchaseOrderItemId,
                quantityReceived: Number(line.quantityReceived) || 0
            }))
        };

        const response = await apiService.post<any>('/finance/ap/purchase-receipts', payload);
        return normalizeReceipt(response);
    },

    submitReceiptForApproval: async (id: string): Promise<FinancePurchaseOrderReceipt> => {
        const response = await apiService.post<any>(`/finance/ap/purchase-receipts/${id}/submit-for-approval`, {});
        return normalizeReceipt(response);
    },

    convertToVendorInvoice: async (receiptId?: string): Promise<{ id: string }> => {
        if (!receiptId) {
            throw new Error('Receipt is required before converting to vendor invoice.');
        }

        return await apiService.post<{ id: string }>(
            `/finance/ap/purchase-receipts/${receiptId}/convert-to-vendor-invoice`,
            {}
        );
    }
};
