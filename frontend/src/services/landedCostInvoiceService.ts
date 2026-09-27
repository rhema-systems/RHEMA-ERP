import { apiService } from './api.service';
import type { VendorInvoice } from '@/types/ap';

export interface LandedCostInvoiceRequest {
  invoiceDate: string;
  charges: Array<{ costItemId: string; businessPartnerId: string; businessPartnerRoleId?: string; supplierInvoiceNumber: string;
    taxTreatment?: number; taxGroupId?: string }>;
}
export interface LandedCostPostResult {
  inventoryPosted: boolean; invoicesPending: boolean; message?: string; invoices: VendorInvoice[];
}

export const landedCostInvoiceService = {
  prepare(voucherId: string, data: LandedCostInvoiceRequest) {
    return apiService.post<LandedCostPostResult>(`/inventory/landed-costs/${voucherId}/prepare-invoices`, data);
  },
  create(voucherId: string, data: LandedCostInvoiceRequest) {
    return apiService.post<VendorInvoice[]>(`/ap/invoices/from-landed-cost/${voucherId}`, data);
  },
};
