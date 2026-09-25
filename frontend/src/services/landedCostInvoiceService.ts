import { apiService } from './api.service';
import type { VendorInvoice } from '@/types/ap';

export interface LandedCostInvoiceRequest {
  invoiceDate: string;
  charges: Array<{ costItemId: string; businessPartnerId: string; supplierInvoiceNumber: string;
    taxTreatment?: number; taxGroupId?: string }>;
}
export interface LandedCostPostResult {
  inventoryPosted: boolean; invoicesPending: boolean; message?: string; invoices: VendorInvoice[];
}

export const landedCostInvoiceService = {
  post(voucherId: string, data: LandedCostInvoiceRequest) {
    return apiService.post<LandedCostPostResult>(`/inventory/landed-costs/${voucherId}/post`, data);
  },
  create(voucherId: string, data: LandedCostInvoiceRequest) {
    return apiService.post<VendorInvoice[]>(`/ap/invoices/from-landed-cost/${voucherId}`, data);
  },
};
