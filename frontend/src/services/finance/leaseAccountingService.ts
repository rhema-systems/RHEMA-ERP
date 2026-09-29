/**
 * Lease Accounting Data Service
 */

import { apiService } from '@/services/api.service';
import type { FinanceSourceDocumentDimension, FinanceSourceDocumentDimensionInput } from '@/types/finance';

// ── Types ──────────────────────────────────────────────────────────

export type LeaseStatus = 'Draft' | 'Active' | 'Terminated' | 'Completed';
export type PaymentFrequency = 'Monthly' | 'Quarterly' | 'SemiAnnually' | 'Annually';
export type VendorInvoiceStatus = 'Draft' | 'PendingApproval' | 'Approved' | 'PartiallyPaid' | 'Paid' | 'Overdue' | 'Voided' | 'Rejected' | 'OnHold';

export interface LeaseContractList {
  id: string;
  contractNumber: string;
  description: string;
  lessorName?: string;
  startDate: string;
  endDate: string;
  monthlyPaymentAmount: number;
  presentValue: number;
  status: LeaseStatus;
  rouAssetId?: string;
  createdAt: string;
}

export interface LeaseScheduleLine {
  id: string;
  periodNumber: number;
  periodDate: string;
  paymentAmount: number;
  interestExpense: number;
  principalReduction: number;
  remainingLiability: number;
  isPosted: boolean;
  vendorInvoiceId?: string;
  vendorInvoiceNumber?: string;
  vendorInvoiceStatus?: VendorInvoiceStatus;
  vendorInvoiceApprovalStatus?: string;
  vendorInvoiceJournalEntryId?: string;
  vendorInvoicePaidAmount?: number;
  vendorInvoiceBalanceAmount?: number;
  canPreparePayable: boolean;
  financeDimensions?: FinanceSourceDocumentDimension;
}

export interface LeaseContractDetail extends LeaseContractList {
  lessorId: string;
  paymentFrequency: PaymentFrequency;
  annualDiscountRate: number;
  totalPeriods: number;
  createdBy?: string;
  updatedAt?: string;
  updatedBy?: string;
  scheduleLines: LeaseScheduleLine[];
  recognitionFinanceDimensions?: FinanceSourceDocumentDimension;
}

export interface CreateLeaseContractDto {
  contractNumber: string;
  description: string;
  lessorId: string;
  startDate: string;
  endDate: string;
  monthlyPaymentAmount: number;
  paymentFrequency: PaymentFrequency;
  annualDiscountRate: number;
}

// ── Service ──────────────────────────────────────────────────────

class LeaseAccountingService {
  async getAll(): Promise<LeaseContractList[]> {
    return apiService.get<LeaseContractList[]>('/finance/leases');
  }

  async getById(id: string): Promise<LeaseContractDetail> {
    return apiService.get<LeaseContractDetail>(`/finance/leases/${id}`);
  }

  async previewSchedule(dto: CreateLeaseContractDto): Promise<LeaseScheduleLine[]> {
    return apiService.post<LeaseScheduleLine[]>('/finance/leases/preview', dto);
  }

  async create(dto: CreateLeaseContractDto): Promise<LeaseContractDetail> {
    return apiService.post<LeaseContractDetail>('/finance/leases', dto);
  }

  async activate(id: string, financeDimensions?: FinanceSourceDocumentDimensionInput): Promise<LeaseContractDetail> {
    return apiService.post<LeaseContractDetail>(`/finance/leases/${id}/activate`, { financeDimensions });
  }

  async preparePeriodPayable(id: string, lineId: string): Promise<LeaseContractDetail> {
    return apiService.post<LeaseContractDetail>(`/finance/leases/${id}/schedule-lines/${lineId}/prepare-payable`, {});
  }
}

export const leaseAccountingService = new LeaseAccountingService();
