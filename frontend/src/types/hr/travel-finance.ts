/**
 * Staff travel — the money: budgets, expense claims and their lines, advances, and per-diem rates.
 * Backend route: `api/staff-travel/finance`.
 *
 * ⚠ **Almost every figure on these records is server-derived. Do not bind a form control to one.**
 * The area's whole finance layer used to accept the client's arithmetic, and it was wrong in ways
 * that cost money — an advance that was never recovered, a claim valued as though a foreign
 * currency were local. What the client supplies now is the *facts* (what was spent, in what
 * currency, on what date); the server supplies every consequence of them.
 *
 * | field | who decides |
 * |---|---|
 * | claim `totalClaimed` / `totalApproved` / `totalRejected` | the sum of its lines |
 * | claim `advanceDeducted` / `netPayable` | the linked advance and the approved total |
 * | line `exchangeRate` / `amountBaseCurrency` | Finance's rate for the day |
 * | advance `settledAmount` / `unsettledAmount` | claims settled against it |
 * | advance `approvedAmount` | the approve endpoint, never a plain update |
 * | advance `disbursedAt`, claim `paidAt` | the clock, on the action that caused them |
 * | budget `totalCommitted` / `totalActual` / `variance` | the request's bookings and paid claims |
 *
 * ⚠ **There is no GL posting anywhere in here.** Travel disburses advances and pays claims with no
 * accounting artifact at all; an unsettled advance is an employee receivable that appears in no
 * trial balance. That is a known, deliberate deferral (decision D-4) to the Finance sweep after the
 * HR module is complete, registered in `docs/HR/integration/HR-FINANCE-INTEGRATION-BACKLOG.md`. Do not invent an
 * HR-side posting mechanism to fill the gap.
 */

import type { AuditFields } from './common';

export type TravelClaimType = 'PostTravel' | 'AdvanceSettlement' | 'PartialClaim' | 'Amendment';

export type TravelClaimStatus =
  | 'Draft'
  | 'Submitted'
  | 'UnderReview'
  | 'Approved'
  | 'PartiallyApproved'
  | 'Rejected'
  | 'Paid'
  | 'Returned';

export type TravelExpenseCategory =
  | 'Airfare'
  | 'Accommodation'
  | 'Meals'
  | 'LocalTransport'
  | 'TaxiRideshare'
  | 'CarRental'
  | 'Fuel'
  | 'VisaFees'
  | 'Insurance'
  | 'Communication'
  | 'ConferenceFees'
  | 'GiftsEntertainment'
  | 'TipsGratuity'
  | 'Laundry'
  | 'Medical'
  | 'BaggageFees'
  | 'Miscellaneous';

export type TravelExpenseLineStatus = 'Pending' | 'Approved' | 'Rejected' | 'Queried' | 'Resolved';

export type TravelPaymentMethod =
  | 'BankTransfer'
  | 'PayrollOffset'
  | 'Cash'
  | 'Cheque'
  | 'CorporateCard';

export type TravelAdvanceType = 'Cash' | 'CorporateCardLoad' | 'PettyCash' | 'WireTransfer';

export type TravelAdvanceStatus =
  | 'Requested'
  | 'Approved'
  | 'Disbursed'
  | 'PartiallySettled'
  | 'FullySettled'
  | 'Overdue'
  | 'WrittenOff';

// ── Budget ───────────────────────────────────────────────────────────────────

export interface StaffTravelBudget extends AuditFields {
  staffTravelRequestId: string;
  budgetYear: number;
  approvedTotal: number;
  currencyCode: string;
  flightBudget: number;
  accommodationBudget: number;
  perDiemBudget: number;
  transportBudget: number;
  miscellaneousBudget: number;
  /**
   * Server-derived: the value of non-cancelled bookings on this request. Money the organisation is
   * on the hook for, whether or not it has left yet.
   */
  totalCommitted: number;
  /**
   * Server-derived: expense claims that have been **paid**. Cash actually gone out through the
   * claim route.
   *
   * ⚠ Committed and actual measure **different routes** and neither contains the other — a booking
   * paid direct to a vendor is committed but never becomes a claim. They must not be added
   * together, and a screen showing both should say what each one counts.
   */
  totalActual: number;
  /** Server-derived: `approvedTotal − totalActual`. */
  variance: number;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvedAt?: string | null;
}

export interface CreateStaffTravelBudget {
  staffTravelRequestId: string;
  budgetYear: number;
  approvedTotal: number;
  currencyCode: string;
  flightBudget: number;
  accommodationBudget: number;
  perDiemBudget: number;
  transportBudget: number;
  miscellaneousBudget: number;
}

export type UpdateStaffTravelBudget =
  Omit<CreateStaffTravelBudget, 'staffTravelRequestId'> & { id: string };

// ── Expense claims ───────────────────────────────────────────────────────────

export interface StaffTravelExpenseClaimLine extends AuditFields {
  staffTravelExpenseClaimId: string;
  expenseCategory: TravelExpenseCategory;
  expenseCategoryName: string;
  expenseDate: string;
  description?: string | null;
  merchantName?: string | null;
  /** What was actually spent, in the currency it was spent in. The one figure the client supplies. */
  amountOriginal: number;
  currencyOriginal: string;
  /** Server-derived from Finance's published rate for the expense date. */
  exchangeRate: number;
  /** Server-derived: `amountOriginal × exchangeRate`. */
  amountBaseCurrency: number;
  policyLimit?: number | null;
  amountApproved?: number | null;
  amountRejected?: number | null;
  rejectionReason?: string | null;
  receiptAttachmentId?: string | null;
  isPerDiem: boolean;
  perDiemRateId?: string | null;
  status: TravelExpenseLineStatus;
  statusName: string;
  reviewedById?: string | null;
  reviewedByName?: string | null;
  reviewedAt?: string | null;
}

export interface StaffTravelExpenseClaimSummary {
  id: string;
  claimNumber: string;
  employeeId: string;
  employeeName: string;
  claimType: TravelClaimType;
  claimTypeName: string;
  status: TravelClaimStatus;
  statusName: string;
  totalClaimed: number;
  netPayable: number;
  currencyCode: string;
  submittedAt?: string | null;
}

export interface StaffTravelExpenseClaim extends AuditFields {
  claimNumber: string;
  staffTravelRequestId: string;
  requestNumber?: string | null;
  employeeId: string;
  employeeName: string;
  claimType: TravelClaimType;
  claimTypeName: string;
  status: TravelClaimStatus;
  statusName: string;
  travelAdvanceId?: string | null;
  travelAdvanceNumber?: string | null;
  totalClaimed: number;
  totalApproved: number;
  totalRejected: number;
  /** Server-derived: recovered from the linked advance when the claim is approved. */
  advanceDeducted: number;
  /** Server-derived: what actually leaves the organisation, net of any advance recovered. */
  netPayable: number;
  currencyCode: string;
  paymentMethod?: TravelPaymentMethod | null;
  paymentMethodName?: string | null;
  paymentReference?: string | null;
  paidAt?: string | null;
  financeReviewedById?: string | null;
  financeReviewedByName?: string | null;
  financeReviewedAt?: string | null;
  submittedAt?: string | null;
  lines: StaffTravelExpenseClaimLine[];
}

export interface CreateStaffTravelExpenseClaimLine {
  staffTravelExpenseClaimId?: string;
  expenseCategory: TravelExpenseCategory;
  expenseDate: string;
  description?: string | null;
  merchantName?: string | null;
  amountOriginal: number;
  currencyOriginal: string;
  policyLimit?: number | null;
  receiptAttachmentId?: string | null;
  isPerDiem: boolean;
  perDiemRateId?: string | null;
}

export interface CreateStaffTravelExpenseClaim {
  staffTravelRequestId: string;
  employeeId: string;
  claimType: TravelClaimType;
  travelAdvanceId?: string | null;
  currencyCode: string;
  lines: CreateStaffTravelExpenseClaimLine[];
}

export interface UpdateStaffTravelExpenseClaim {
  id: string;
  claimType: TravelClaimType;
  travelAdvanceId?: string | null;
  currencyCode: string;
}

/**
 * Reviewing a claim sets its status outright — there is no boolean verdict. Both the reviewer and
 * the moment are the server's.
 *
 * ⚠ **The claim's status is not derived from its lines.** Approving every line does not approve the
 * claim; a reviewer says what the claim now is. So a screen must offer the real statuses rather
 * than an approve/reject pair, or it will leave claims stuck in `UnderReview` and unpayable —
 * `pay` refuses anything that is not `Approved`.
 */
export interface ReviewStaffTravelExpenseClaim {
  claimId: string;
  newStatus: TravelClaimStatus;
  notes?: string | null;
}

/** Omitting `amountApproved` on an approval leaves it null — set it explicitly. */
export interface ReviewStaffTravelExpenseClaimLine {
  lineId: string;
  status: TravelExpenseLineStatus;
  amountApproved?: number | null;
  amountRejected?: number | null;
  rejectionReason?: string | null;
}

export interface PayStaffTravelExpenseClaim {
  claimId: string;
  paymentMethod: TravelPaymentMethod;
  paymentReference?: string | null;
}

// ── Advances ─────────────────────────────────────────────────────────────────

export interface StaffTravelAdvanceSummary {
  id: string;
  advanceNumber: string;
  employeeId: string;
  employeeName: string;
  requestedAmount: number;
  approvedAmount?: number | null;
  currencyCode: string;
  advanceType: TravelAdvanceType;
  advanceTypeName: string;
  status: TravelAdvanceStatus;
  statusName: string;
  unsettledAmount: number;
  settlementDeadline?: string | null;
}

export interface StaffTravelAdvance extends AuditFields {
  advanceNumber: string;
  staffTravelRequestId: string;
  requestNumber?: string | null;
  employeeId: string;
  employeeName: string;
  requestedAmount: number;
  /** Server-assigned by the approve endpoint. A plain update cannot set it. */
  approvedAmount?: number | null;
  currencyCode: string;
  advanceType: TravelAdvanceType;
  advanceTypeName: string;
  status: TravelAdvanceStatus;
  statusName: string;
  /** Server-stamped when the advance is disbursed. */
  disbursedAt?: string | null;
  settlementDeadline?: string | null;
  /** Server-derived: recovered from expense claims filed against this advance. */
  settledAmount: number;
  unsettledAmount: number;
  approvedById?: string | null;
  approvedByName?: string | null;
  disbursedById?: string | null;
  disbursedByName?: string | null;
}

export interface CreateStaffTravelAdvance {
  staffTravelRequestId: string;
  employeeId: string;
  requestedAmount: number;
  currencyCode: string;
  advanceType: TravelAdvanceType;
  settlementDeadline?: string | null;
}

export type UpdateStaffTravelAdvance =
  Omit<CreateStaffTravelAdvance, 'staffTravelRequestId' | 'employeeId'> & { id: string };

/** The approver is the token's. */
export interface ApproveStaffTravelAdvance {
  advanceId: string;
  approvedAmount: number;
}

/** The disburser and the moment are both the server's. */
export interface DisburseStaffTravelAdvance {
  advanceId: string;
}

// ── Per-diem rates ───────────────────────────────────────────────────────────

export interface StaffTravelPerDiemRate extends AuditFields {
  countryId: string;
  countryName?: string | null;
  city?: string | null;
  staffLevelId?: string | null;
  staffLevelName?: string | null;
  effectiveFrom: string;
  effectiveTo?: string | null;
  dailyAllowance: number;
  accommodationLimit: number;
  mealAllowance: number;
  incidentalAllowance: number;
  currencyCode: string;
  mealBreakdownBreakfast: number;
  mealBreakdownLunch: number;
  mealBreakdownDinner: number;
  isActive: boolean;
}

export interface CreateStaffTravelPerDiemRate {
  countryId: string;
  city?: string | null;
  staffLevelId?: string | null;
  effectiveFrom: string;
  effectiveTo?: string | null;
  dailyAllowance: number;
  accommodationLimit: number;
  mealAllowance: number;
  incidentalAllowance: number;
  currencyCode: string;
  mealBreakdownBreakfast: number;
  mealBreakdownLunch: number;
  mealBreakdownDinner: number;
  isActive: boolean;
}

export type UpdateStaffTravelPerDiemRate = CreateStaffTravelPerDiemRate & { id: string };
