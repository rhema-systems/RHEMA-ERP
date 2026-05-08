/**
 * Lease Accounting Data Service
 */

import { apiService } from '@/services/api.service';

// ── Types ──────────────────────────────────────────────────────────

export type LeaseStatus = 'Draft' | 'Active' | 'Terminated' | 'Completed';
export type PaymentFrequency = 'Monthly' | 'Quarterly' | 'SemiAnnually' | 'Annually';

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

  async activate(id: string): Promise<LeaseContractDetail> {
    return apiService.post<LeaseContractDetail>(`/finance/leases/${id}/activate`);
  }

  async postPeriodJournal(id: string, lineId: string): Promise<LeaseContractDetail> {
    return apiService.post<LeaseContractDetail>(`/finance/leases/${id}/schedule-lines/${lineId}/post`);
  }
}

export const leaseAccountingService = new LeaseAccountingService();
