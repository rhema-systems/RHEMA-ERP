import { AccountsPayableService } from './accountsPayableService';
import { apiService } from './api.service';
import type { VendorInvoiceDistribution } from '@/types/ap';
export type * from './accountsPayableService';

// Shared AP contracts and engine; Procurement owns its routes and page implementations.
export const accountsPayableService = new AccountsPayableService(
  '/procurement/supplier-invoices'
);

export interface SupplierInvoiceDistributionInput {
  version: string;
  basisVersion: string;
  lines: Array<{
    lineId: string;
    groupId: string;
    accountId: string;
    debit: number;
    credit: number;
  }>;
}
export interface SupplierDistributionAccount {
  id: string;
  accountCode: string;
  accountNumber: string;
  accountName: string;
}
export const supplierInvoiceDistributionService = {
  accounts: (id: string) =>
    apiService.get<SupplierDistributionAccount[]>(
      `/procurement/supplier-invoices/${id}/distribution/accounts`
    ),
  save: (id: string, input: SupplierInvoiceDistributionInput) =>
    apiService.put<VendorInvoiceDistribution>(
      `/procurement/supplier-invoices/${id}/distribution`,
      input
    ),
  reset: (id: string, input: SupplierInvoiceDistributionInput) =>
    apiService.post<VendorInvoiceDistribution>(
      `/procurement/supplier-invoices/${id}/distribution/reset`,
      input
    ),
};
