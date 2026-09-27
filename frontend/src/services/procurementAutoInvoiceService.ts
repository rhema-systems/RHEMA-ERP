import { apiService } from './api.service';
import type { VendorInvoice } from '@/types/ap';

export interface AutoInvoiceReceiptLine {
  goodsReceiptNoteItemId: string; purchaseOrderItemId: string; description: string; unit: string;
  acceptedQuantity: number; returnedQuantity: number; invoicedQuantity: number; availableQuantity: number; unitPrice: number;
}
export interface AutoInvoiceReceipt {
  goodsReceiptNoteId: string; receiptNumber: string; receiptDate: string; purchaseOrderId: string;
  orderNumber: string; currencyCode: string; warehouseId: string; lines: AutoInvoiceReceiptLine[];
}
export interface AutoInvoiceRequest {
  requestId: string; businessPartnerId: string; businessPartnerRoleId?: string; supplierInvoiceNumber: string; invoiceDate: string;
  exchangeRateId?: string; exchangeRate: number; lines: Array<{ goodsReceiptNoteItemId: string; quantity: number }>;
}
export interface InvoiceReceiptLink {
  invoiceLineId: string; goodsReceiptNoteId: string; receiptNumber: string; purchaseOrderId: string;
  orderNumber: string; inspectionCaseId: string; quantity: number;
}
export const procurementAutoInvoiceService = {
  receipts: (businessPartnerId: string) => apiService.get<AutoInvoiceReceipt[]>(`/procurement/supplier-invoices/auto-invoice/receipts?businessPartnerId=${encodeURIComponent(businessPartnerId)}`),
  create: (request: AutoInvoiceRequest) => apiService.post<VendorInvoice>('/procurement/supplier-invoices/auto-invoice', request),
  links: (invoiceId: string) => apiService.get<InvoiceReceiptLink[]>(`/procurement/supplier-invoices/${invoiceId}/receipt-links`),
};
