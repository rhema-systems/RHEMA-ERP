/**
 * Leave operations — requests, balances, adjustments, plans, encashments, year-end.
 *
 * Routes are split across four controllers:
 *   api/Leaves               requests, balances, adjustments, compliance, attachments
 *   api/hr/leave-plans       annual leave plans
 *   api/hr/leave-encashments encashment requests
 *   api/hr/leave-year-end    carry-over and forfeiture batches
 *
 * Approvals run through the generic workflow engine (entity types LeaveRequest,
 * LeavePlan, LeaveEncashment), so the screens embed the shared workflow components
 * rather than implementing their own approval UI.
 */
import type { PagedResult } from './common';

export type LeaveStatus =
  | 'Draft'
  | 'Pending'
  | 'Approved'
  | 'Rejected'
  | 'Cancelled'
  | 'InProgress'
  | 'Completed';

export type LeavePlanStatus =
  | 'Draft'
  | 'Submitted'
  | 'Approved'
  | 'Rejected'
  | 'Cancelled'
  | 'ChangesSuggested';

export type LeaveEncashmentStatus =
  | 'Draft'
  | 'Submitted'
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected'
  | 'Processed'
  | 'Cancelled';

export const LEAVE_STATUS_OPTIONS: { value: LeaveStatus; label: string }[] = [
  { value: 'Draft', label: 'Draft' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Approved', label: 'Approved' },
  { value: 'Rejected', label: 'Rejected' },
  { value: 'Cancelled', label: 'Cancelled' },
  { value: 'InProgress', label: 'In progress' },
  { value: 'Completed', label: 'Completed' },
];

// ── Leave requests ──────────────────────────────────────────────────────────────

export interface LeaveRequest {
  id: string;
  requestNumber: string;
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  leaveTypeId: string;
  leaveTypeName: string;
  isPaidLeave: boolean;
  leaveSubTypeId?: string | null;
  leaveSubTypeName?: string | null;
  /** DateOnly */
  startDate: string;
  endDate: string;
  totalDays: number;
  /** DateTime */
  requestDate: string;
  reason: string;
  handoverNotes?: string | null;
  status: LeaveStatus;
  relieverEmployeeId?: string | null;
  relieverEmployeeName?: string | null;
  secondRelieverEmployeeId?: string | null;
  secondRelieverEmployeeName?: string | null;
  relieverNotes?: string | null;
  leavePlanId?: string | null;
  closureDate?: string | null;
  closureNotes?: string | null;
  cancellationDate?: string | null;
  cancellationReason?: string | null;
  createdAt: string;
}

export interface CreateLeaveRequest {
  employeeId: string;
  leaveTypeId: string;
  leaveSubTypeId?: string | null;
  startDate: string;
  endDate: string;
  reason: string;
  relieverEmployeeId?: string | null;
  secondRelieverEmployeeId?: string | null;
  relieverNotes?: string | null;
  handoverNotes?: string | null;
  leavePlanId?: string | null;
  /** Keep as Draft instead of entering the approval workflow immediately. */
  saveAsDraft: boolean;
}

export interface ApproveLeaveRequest {
  approvedBy: string;
  approvalNotes?: string | null;
  comments?: string | null;
}

export interface RejectLeaveRequest {
  rejectionReason: string;
}

export interface CloseLeaveRequest {
  closureNotes?: string | null;
}

export type LeaveRequestPagedResult = PagedResult<LeaveRequest>;

// ── Balances ────────────────────────────────────────────────────────────────────

export interface LeaveBalance {
  id: string;
  employeeId: string;
  employeeName: string;
  organizationUnitName?: string | null;
  leaveTypeId: string;
  leaveTypeName: string;
  leaveSubTypeId?: string | null;
  leaveSubTypeName?: string | null;
  year: number;
  entitledDays: number;
  accruedToDateDays: number;
  usedDays: number;
  pendingDays: number;
  carriedOverDays: number;
  adjustmentDays: number;
  encashedDays: number;
  availableDays: number;
}

export interface LeaveBalanceDetail extends LeaveBalance {
  allowCashConversion: boolean;
  requests: LeaveRequest[];
  encashments: LeaveEncashment[];
  adjustments: LeaveAdjustment[];
}

export interface RecalculateLeaveBalanceRequest {
  employeeId: string;
  year: number;
  leaveTypeId?: string | null;
}

// ── Adjustments ─────────────────────────────────────────────────────────────────

export interface LeaveAdjustment {
  id: string;
  leaveBalanceId: string;
  employeeId: string;
  employeeName: string;
  leaveTypeId: string;
  leaveTypeName: string;
  leaveSubTypeId?: string | null;
  leaveSubTypeName?: string | null;
  year: number;
  /** Signed: negative values deduct from the balance. */
  days: number;
  reasonCodeId?: string | null;
  reasonCodeName?: string | null;
  reason: string;
  adjustmentDate: string;
  performedBy: string;
  performedByName: string;
}

export interface CreateLeaveAdjustmentRequest {
  employeeId: string;
  leaveTypeId: string;
  leaveSubTypeId?: string | null;
  year: number;
  days: number;
  reasonCodeId?: string | null;
  /** Free text — labelled "Remarks" on the screen, beside the reason code. */
  reason: string;
  // performedBy is stamped server-side from the token (finish-plan lane 4). The screen used to send
  // the login's user id, which is never an employee id, and the Employee foreign key refused it.
  adjustmentDate?: string | null;
}

export interface UpdateLeaveAdjustmentRequest {
  days: number;
  reasonCodeId?: string | null;
  reason: string;
  adjustmentDate?: string | null;
}

// ── Mandatory-leave compliance ──────────────────────────────────────────────────

export interface MandatoryLeaveCompliance {
  employeeId: string;
  employeeName: string;
  leaveTypeId: string;
  leaveTypeName: string;
  year: number;
  entitledDays: number;
  takenDays: number;
  scheduledDays: number;
  outstandingDays: number;
  status: string;
}

// ── Attachments (through the controlled upload gate) ────────────────────────────

export interface LeaveRequestAttachment {
  id: string;
  leaveRequestId: string;
  fileName: string;
  /** Stored path — NOT a URL. Download via the authorized endpoint. */
  filePath: string;
  contentType?: string | null;
  fileSizeBytes?: number | null;
  uploadedDate: string;
  uploadedBy: string;
  uploadedByName: string;
}

// ── Leave plans ─────────────────────────────────────────────────────────────────

export interface LeavePlan {
  id: string;
  employeeId: string;
  employeeName: string;
  organizationLevelId?: string | null;
  organizationLevelName?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  positionId?: string | null;
  positionName?: string | null;
  leaveTypeId: string;
  leaveTypeName: string;
  leaveSubTypeId?: string | null;
  leaveSubTypeName?: string | null;
  startDate: string;
  endDate: string;
  relieverId?: string | null;
  relieverName?: string | null;
  secondRelieverId?: string | null;
  secondRelieverName?: string | null;
  notes?: string | null;
  plannedBy: string;
  plannedByName: string;
  year: number;
  status: LeavePlanStatus;
  suggestedStartDate?: string | null;
  suggestedEndDate?: string | null;
  managerSuggestionNotes?: string | null;
  workflowInstanceId?: string | null;
  approvedById?: string | null;
  approvedDate?: string | null;
  rejectionReason?: string | null;
  /**
   * Why the named reliever(s) may not be free over the plan's dates — their own plans, their own
   * live leave requests, or another plan in the window that already names them. Empty when clear.
   * Advisory: the plan can still be saved and approved. (Finish-plan lane 4.)
   */
  relieverClashes: LeaveRelieverClash[];
}

/** One reason a reliever is not free over a leave plan's dates. Shape probed from GET /hr/leave-plans. */
export interface LeaveRelieverClash {
  relieverId: string;
  relieverName: string;
  /** 1 = reliever, 2 = second reliever. */
  slot: number;
  source: 'LeavePlan' | 'LeaveRequest' | 'RelieverOnAnotherPlan';
  description: string;
  fromDate: string;
  toDate: string;
}

export interface CreateLeavePlanRequest {
  employeeId: string;
  organizationLevelId?: string | null;
  organizationUnitId?: string | null;
  positionId?: string | null;
  leaveTypeId: string;
  leaveSubTypeId?: string | null;
  startDate: string;
  endDate: string;
  relieverId?: string | null;
  secondRelieverId?: string | null;
  notes?: string | null;
  // plannedBy is stamped server-side from the token (finish-plan lane 4); it is an Employee
  // foreign key and both screens used to send the login's user id.
  year: number;
}

export interface SuggestLeavePlanChangesRequest {
  suggestedStartDate: string;
  suggestedEndDate: string;
  notes?: string | null;
}

export interface RespondToLeaveSuggestionRequest {
  accept: boolean;
  startDate?: string | null;
  endDate?: string | null;
  notes?: string | null;
}

// ── Encashments ─────────────────────────────────────────────────────────────────

export interface LeaveEncashment {
  id: string;
  leaveRequestId: string;
  employeeId: string;
  employeeName: string;
  leaveTypeId: string;
  leaveTypeName: string;
  year: number;
  daysEncashed: number;
  amountPaid: number;
  status: LeaveEncashmentStatus;
  processedDate?: string | null;
  processedByEmployeeId?: string | null;
  processedByName?: string | null;
  paymentReference?: string | null;
  notes?: string | null;
  workflowInstanceId?: string | null;
  approvedById?: string | null;
  approvedDate?: string | null;
  rejectionReason?: string | null;
}

export interface CreateLeaveEncashmentRequest {
  leaveRequestId: string;
  employeeId: string;
  leaveTypeId: string;
  year: number;
  daysEncashed: number;
  amountPaid: number;
  notes?: string | null;
}

export interface ProcessLeaveEncashmentRequest {
  processedByEmployeeId: string;
  paymentReference: string;
}

// ── Year-end ────────────────────────────────────────────────────────────────────

export interface LeaveYearEndResult {
  balancesProcessed: number;
  balancesAffected: number;
  totalDaysCarriedOver: number;
  totalDaysForfeited: number;
  notes: string[];
}
