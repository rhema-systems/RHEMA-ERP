import type { PayBasis } from '@/types/hr/employee';

/** `SalaryChangeKind` on the server (round 3, lane S). */
export const SALARY_CHANGE_KINDS = ['Placement', 'NegotiatedAmount', 'PayBasisSwitch'] as const;
export type SalaryChangeKind = (typeof SALARY_CHANGE_KINDS)[number];

/** `SalaryChangeRequestStatus` on the server. */
export const SALARY_CHANGE_REQUEST_STATUSES = [
  'Draft',
  'PendingApproval',
  'Approved',
  'Rejected',
  'Applied',
  'AwaitingPayrollEntry',
] as const;
export type SalaryChangeRequestStatus = (typeof SALARY_CHANGE_REQUEST_STATUSES)[number];

export interface SalaryChangeRequest {
  id: string;
  employeeId: string;
  employeeName?: string | null;
  employeeNumber?: string | null;
  kind: SalaryChangeKind;
  status: SalaryChangeRequestStatus;

  currentPayBasis: PayBasis;
  currentGradeId?: string | null;
  currentGradeCode?: string | null;
  currentLevelId?: string | null;
  currentNotchId?: string | null;
  currentNotchNumber?: string | null;
  currentAmount?: number | null;

  proposedPayBasis?: PayBasis | null;
  proposedGradeId?: string | null;
  proposedGradeCode?: string | null;
  proposedGradeName?: string | null;
  proposedLevelId?: string | null;
  proposedLevelCode?: string | null;
  proposedNotchId?: string | null;
  proposedNotchNumber?: string | null;
  proposedNotchAmount?: number | null;
  proposedAmount?: number | null;
  proposedCurrencyCode?: string | null;
  resultingMonthlyBasicPay?: number | null;

  effectiveDate: string;
  reason: string;
  requestedById: string;
  requestedByName?: string | null;
  sourceProposalId?: string | null;

  rejectionReason?: string | null;
  hrAppliedOn?: string | null;
  appliedOn?: string | null;
  appliedPlacementId?: string | null;
  applyFailure?: string | null;

  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateSalaryChangeRequest {
  employeeId: string;
  kind: SalaryChangeKind;
  proposedPayBasis?: PayBasis | null;
  proposedGradeId?: string | null;
  proposedLevelId?: string | null;
  proposedNotchId?: string | null;
  proposedAmount?: number | null;
  proposedCurrencyCode?: string | null;
  effectiveDate: string;
  reason: string;
  sourceProposalId?: string | null;
}

export type UpdateSalaryChangeRequest = Omit<CreateSalaryChangeRequest, 'employeeId' | 'sourceProposalId'>;

export const SALARY_CHANGE_STATUS_LABELS: Record<SalaryChangeRequestStatus, string> = {
  Draft: 'Draft',
  PendingApproval: 'Awaiting approval',
  Approved: 'Approved — applying',
  Rejected: 'Rejected',
  Applied: 'Applied',
  AwaitingPayrollEntry: 'Applied in HR — awaiting payroll',
};
