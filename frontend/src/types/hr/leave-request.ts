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
  | 'Completed'
  /** The approver sent it back with dates of their own; it is with the employee to answer. */
  | 'ChangesSuggested';

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
  { value: 'ChangesSuggested', label: 'Changes suggested' },
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
  /** The plan this came from, named rather than shown as a Guid. Single-request read only. */
  leavePlanReference?: string | null;

  /**
   * Attendance days recorded as OnLeave against this request. Single-request read only. It should
   * equal `totalDays` once approved; fewer means some days already had attendance recorded and were
   * left alone, and those will not reach the payroll export as leave (closure plan L-27).
   */
  attendanceDaysRecorded?: number;

  /** Dates the approver sent back instead. Set while the status is ChangesSuggested. */
  suggestedStartDate?: string | null;
  suggestedEndDate?: string | null;
  managerSuggestionNotes?: string | null;

  /** Set once an approved request has been moved: what it used to say, who moved it, and why. */
  originalStartDate?: string | null;
  originalEndDate?: string | null;
  rescheduledDate?: string | null;
  rescheduledById?: string | null;
  rescheduledByName?: string | null;
  rescheduleReason?: string | null;
  rescheduleCount: number;

  /** Set when somebody answered "yes, this is still going ahead". */
  observanceConfirmedDate?: string | null;
  observanceConfirmedById?: string | null;
  observanceConfirmedByName?: string | null;

  /**
   * Set when the employee was called back before their end date (R-14). The request keeps its
   * number, its status and its approval — `endDate` is simply earlier than it was, and
   * `preRecallEndDate` is what it used to be.
   *
   * ⚠ A screen showing a recalled request must show both dates, or the leave reads as though it
   * was always this short and the recall becomes invisible.
   */
  /**
   * The medical board that ruled on this absence, when one sat (G4).
   *
   * ⚠ A bare id across a module boundary. Leave READS the board — it never writes one — and a
   * board only satisfies the evidence rule once it has CONCLUDED.
   */
  medicalBoardId?: string | null;

  recallEffectiveDate?: string | null;
  preRecallEndDate?: string | null;
  recalledDate?: string | null;
  recalledById?: string | null;
  recalledByName?: string | null;
  recallReason?: string | null;
  daysRestored?: number | null;

  closureDate?: string | null;
  closureNotes?: string | null;
  cancellationDate?: string | null;
  cancellationReason?: string | null;
  createdAt: string;
}

/** An approver sending a request back with dates of their own. */
export interface SuggestLeaveRequestChanges {
  suggestedStartDate: string;
  suggestedEndDate: string;
  notes?: string | null;
}

/**
 * Moving an approved request. A reason is required: the point of this path over cancel-and-re-key
 * is that the record says why it moved.
 */
export interface RescheduleLeaveRequest {
  startDate: string;
  endDate: string;
  reason: string;
}

/**
 * Calling an employee back before their leave ends.
 *
 * ⚠ `effectiveDate` is **the first day the employee is back at work**, not the last day of their
 * leave — that is what a recall notice states, and the server takes the day before it as the new
 * end date. Reading it the other way round is a one-day error no screen would catch.
 */
export interface RecallLeaveRequest {
  effectiveDate: string;
  reason: string;
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
  /** Policy figure: entitled + carried + adjustments − used − pending − encashed. */
  availableDays: number;
  /** What the server's create check actually enforces — accrued replaces entitled for accruing types. */
  accruedAvailableDays: number;
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

// ── Attachments (through the controlled upload gate) ──────────────────────────

/**
 * What a document attached to a leave request actually is (R-15a).
 *
 * ⚠ `ExcuseDuty` and "medical certificate" are the same document under two names — the local
 * term and the generic one. The leave type's setting says `requiresMedicalCertificate`; the
 * thing an employee is holding is excuse duty.
 *
 * ⚠ Typing them is what makes the evidence gate possible at all. A rule saying "a certificate
 * must be attached" cannot be checked against file names: `scan.pdf` is a medical certificate
 * or a holiday photograph with equal probability.
 */
export type LeaveEvidenceKind = 'Other' | 'ExcuseDuty' | 'MedicalBoardRecommendation';

export const LEAVE_EVIDENCE_KIND_LABEL: Record<LeaveEvidenceKind, string> = {
  Other: 'Supporting document',
  ExcuseDuty: 'Excuse duty (medical certificate)',
  MedicalBoardRecommendation: 'Medical board recommendation',
};

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

  /** What the document is — read by the evidence gate, not inferred from the file name. */
  evidenceKind: LeaveEvidenceKind;
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
   * The live leave request already raised from this plan, if any. A cancelled or rejected request
   * does not count — the plan becomes raiseable again. (Closure plan L-9.)
   */
  raisedLeaveRequestId?: string | null;
  raisedLeaveRequestNumber?: string | null;
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
  //
  // `year` is likewise NOT sent: the server derives it from startDate. The desk screen used to send
  // the list filter's year, so a January plan raised from the December list was filed under the
  // wrong one and shown by neither (closure plan L-17).
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

  /**
   * How the amount was arrived at, in words — the monthly figure, the divisor, the resulting
   * daily rate, and where that divisor came from.
   *
   * ⚠ **Recorded at payout time, not recomputed.** The divisor behind it is a setting, so a
   * figure that cannot name its own basis stops reconciling the moment somebody edits it. This
   * is what finding L-20 was missing: the row showed an amount and nothing to check it against.
   */
  rateBasis?: string | null;

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

/** The processor is stamped server-side from the caller's employee id, never sent. */
export interface ProcessLeaveEncashmentRequest {
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

// ── Leave calendar (closure plan wave E, slice E1) ──────────────────────────────

export type LeaveCalendarScope = 'Mine' | 'Team' | 'Organisation';

/**
 * One person's leave, as a band on a calendar.
 *
 * ⚠ It carries no reason and no balance — a calendar is read by colleagues, and "Ama is on annual
 * leave" is what a calendar is for.
 */
export interface LeaveCalendarEntry {
  id: string;
  requestNumber: string;
  employeeId: string;
  employeeName: string;
  organizationUnitName?: string | null;
  leaveTypeId: string;
  leaveTypeName: string;
  /** The leave type's own colour; null when the tenant never set one. */
  calendarColor?: string | null;
  /** DateOnly */
  startDate: string;
  endDate: string;
  totalDays: number;
  status: LeaveStatus;
  /** Approved (or beyond). A pending band is drawn as an outline. */
  isConfirmed: boolean;
}

export interface LeaveCalendarHoliday {
  date: string;
  name: string;
}

export interface LeaveCalendarData {
  from: string;
  to: string;
  scope: LeaveCalendarScope;
  entries: LeaveCalendarEntry[];
  holidays: LeaveCalendarHoliday[];
}

// ── Leave register and bulk decisions (closure plan wave E, slices E3/E4) ───────

export interface LeaveRegisterFilter {
  from?: string;
  to?: string;
  status?: LeaveStatus;
  leaveTypeId?: string;
  employeeId?: string;
  organizationUnitId?: string;
  search?: string;
}

export interface BulkLeaveDecision {
  leaveRequestIds: string[];
  comments?: string | null;
  /** Required when rejecting. */
  rejectionReason?: string | null;
}

/** The house bulk-result shape: a per-item reason, so the few that failed say why. */
export interface HrBulkActionResult {
  requestedCount: number;
  succeededCount: number;
  results: { id: string; success: boolean; reason?: string | null }[];
}
