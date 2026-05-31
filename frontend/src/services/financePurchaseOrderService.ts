import apiService from './api.service';

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
    lineTotal?: number;
    taxCode?: string;
    currencyCode?: string;
}

export interface FinancePurchaseOrder {
    id: string;
    tenantId: string;
    orderNumber: string;
    vendorId: string;
    orderDate: string;
    expectedDeliveryDate?: string;
    status: number; // 1 = Draft, 2 = Approved, 3 = PartiallyReceived, 4 = Received, 5 = PartiallyInvoiced, 6 = Invoiced
    currencyCode: string;
    exchangeRate: number;
    totalAmount: number;
    remarks?: string;
    items: FinancePurchaseOrderItem[];
    vendorName?: string;
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
    financePurchaseOrderId: string;
    receiptNumber: string;
    receiptDate: string;
    remarks?: string;
    items: FinancePurchaseOrderReceiptItem[];
    
    // UI helpers
    orderNumber?: string;
    vendorName?: string;
}

export const financePurchaseOrderService = {
    // PO Endpoints
    getPurchaseOrders: async (params?: any) => {
        return await apiService.get('/finance/ap/purchase-orders', params);
    },
    
    getPurchaseOrderById: async (id: string) => {
        return await apiService.get(`/finance/ap/purchase-orders/${id}`);
    },
    
    createPurchaseOrder: async (data: Partial<FinancePurchaseOrder>) => {
        return await apiService.post('/finance/ap/purchase-orders', data);
    },
    
    approvePurchaseOrder: async (id: string) => {
        return await apiService.post(`/finance/ap/purchase-orders/${id}/approve`);
    },

    // Receipt Endpoints
    getReceipts: async (params?: any) => {
        // We'll just fetch all or filter by PO ID
        return await apiService.get('/finance/ap/purchase-receipts', params);
    },
    
    getReceiptsByPo: async (poId: string) => {
        return await apiService.get(`/finance/ap/purchase-orders/${poId}/receipts`);
    },
    
    getReceiptById: async (id: string) => {
        return await apiService.get(`/finance/ap/purchase-receipts/${id}`);
    },
    
    createReceipt: async (data: Partial<FinancePurchaseOrderReceipt>) => {
        return await apiService.post('/finance/ap/purchase-receipts', data);
    },
    
    convertToVendorInvoice: async (receiptId: string) => {
        return await apiService.post(`/finance/ap/invoices/from-receipt/${receiptId}`);
    }
};
