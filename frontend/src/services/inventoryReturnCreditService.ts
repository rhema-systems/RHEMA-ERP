import { apiService } from './api.service';

export interface InventoryReturnCreditSource {
  invoiceId: string;
  invoiceNumber: string;
  supplierInvoiceNumber?: string;
  currencyCode: string;
  outstandingAmount: number;
}

export interface InventoryReturnCreditNote {
  id: string;
  debitNoteNumber: string;
  inventoryPurchaseReturnId: string;
  status: number | string;
  statusName?: string;
  currencyCode: string;
  totalAmount: number;
  journalEntryId?: string | null;
  postingEventId?: string | null;
  returnDispatchPostingEventId?: string | null;
  returnDispatchJournalEntryId?: string | null;
  directInvoiceAppliedAmount?: number;
  directInvoiceAppliedAt?: string | null;
}

export interface InventoryReturnCreditRequest {
  originalVendorInvoiceId: string;
  supplierCreditNoteReference: string;
  creditDate: string;
  reason?: string;
}

const base = '/ap/supplier-debit-notes';
export const inventoryReturnCreditService = {
  getNotes: (returnId: string) => apiService.get<InventoryReturnCreditNote[]>(base, { inventoryPurchaseReturnId: returnId }),
  getSources: (returnId: string) => apiService.get<InventoryReturnCreditSource[]>(`${base}/inventory-returns/${encodeURIComponent(returnId)}/source-invoices`),
  create: (returnId: string, request: InventoryReturnCreditRequest) =>
    apiService.post<InventoryReturnCreditNote>(`${base}/inventory-returns/${encodeURIComponent(returnId)}/credit`, request),
};
