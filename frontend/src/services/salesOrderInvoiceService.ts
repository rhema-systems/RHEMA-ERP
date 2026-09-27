import { apiService } from './api.service';
import type { FinanceSourceDocumentDimensionInput } from '@/types/finance';

export interface SalesInvoiceLineSelection {
  salesOrderLineId: string; glAccountId?: string; taxGroupId?: string;
  taxTreatment: 'Standard' | 'Exempt' | 'ZeroRated' | 'OutOfScope' | 'PendingReview';
}
export interface GenerateSalesInvoiceRequest {
  rowVersion: string; idempotencyKey: string; invoiceDate: string; dueDate?: string;
  exchangeRateId?: string; lines: SalesInvoiceLineSelection[];
  freightAccountId?: string; freightTaxGroupId?: string; freightTaxTreatment?: SalesInvoiceLineSelection['taxTreatment'];
  financeDimensions?: FinanceSourceDocumentDimensionInput;
}
export interface SalesInvoiceDetail {
  salesOrderId: string; salesOrderNumber: string; canSubmit: boolean; canPost: boolean;
  invoice: { id: string; invoiceNumber: string; status: string; currencyCode: string; totalAmount: number;
    invoiceDate: string; journalEntryId?: string; workflowInstanceId?: string;
    lineItems: { id: string; description: string; quantity: number; unitPrice: number; taxAmount: number; discountAmount: number }[] };
}
export interface SalesInvoiceDistribution {
  invoiceId: string; currencyCode: string; isEstimated: boolean; totalDebit: number; totalCredit: number; isBalanced: boolean;
  lines: { accountId: string; accountCode: string; accountName: string; description: string; debit: number; credit: number; sourceLineId?: string }[];
}
const route = (id: string) => `/sales/orders/${id}/invoice`;
export const salesOrderInvoiceService = {
  get: (id: string) => apiService.get<SalesInvoiceDetail>(route(id)),
  generate: (id: string, request: GenerateSalesInvoiceRequest) => apiService.post<SalesInvoiceDetail>(route(id), request),
  submit: (id: string) => apiService.post<SalesInvoiceDetail>(`${route(id)}/submit`, {}),
  post: (id: string) => apiService.post<SalesInvoiceDetail>(`${route(id)}/post`, {}),
  distribution: (id: string) => apiService.get<SalesInvoiceDistribution>(`${route(id)}/distribution`),
};
