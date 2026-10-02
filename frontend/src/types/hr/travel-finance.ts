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
 * | budget `totalCommitted` / `totalActual` / `variance` | the request's bookings, paid claims and advances paid out |
 * | budget `currencyCode`, `approvedById` / `approvedAt` | the trip's currency; the approve endpoint |
 *
 * Finance posting (since 2026-09-20, the HR finance posting sweep): `TravelAdvanceDisbursed`,
 * `TravelClaimApproved` and `TravelClaimPaid` post journals through HR's one posting adapter when a
 * rule for the event is enabled under HR Settings → Finance posting; without one the record is kept
 * Unposted. Nothing here posts on its own — read a record's posting through `FinancePostingCard`.
 * (This header said "there is no GL posting anywhere in here" until the travel final closure.)
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

// Rejected and Cancelled: travel final closure, migration batch 1 — written from lane 3. (A comment INSIDE the
// union hid it from travel-enums.test.ts's regex, so it was never compared with the C# enum — lane 3, N9.)
export type TravelAdvanceStatus =
  | 'Requested'
  | 'Approved'
  | 'Disbursed'
  | 'PartiallySettled'
  | 'FullySettled'
  | 'Overdue'
  | 'WrittenOff'
  | 'Rejected'
  | 'Cancelled';

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
   * Server-derived: the value of bookings on this request that are not cancelled, refunded or a no-show,
   * plus the cancellation fees of those cancelled or refunded. Money the organisation is on the hook for,
   * whether or not it has left yet.
   */
  totalCommitted: number;
  /**
   * Server-derived: cash actually gone out — `actualClaimsPaid` + `actualAdvancesPaidOut` (lane 3).
   *
   * ⚠ Committed and actual measure **different routes** and neither contains the other — a booking
   * paid direct to a vendor is committed but never becomes a claim. They must not be added
   * together, and a screen showing both should say what each one counts.
   */
  totalActual: number;
  /** Claims paid, net of the advance each recovered. */
  actualClaimsPaid: number;
  /** Advance cash paid out, less cash handed back. */
  actualAdvancesPaidOut: number;
  /** Server-derived: `approvedTotal − totalActual`. */
  variance: number;
  /** The trip's approved budget (its estimate on a trip approved before lane 2) — the cap on `approvedTotal`. */
  tripApprovedBudget?: number | null;
  /** Committed spend is above `approvedTotal`. It warns; whether it refuses is a TDC question. */
  committedOverrun: boolean;
  /** Actual spend is above `approvedTotal`. */
  actualOverrun: boolean;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvedAt?: string | null;
}

/**
 * Lane 3: only once the trip is approved; in the trip's currency (set by the server — there is no currency
 * field); `approvedTotal` 0 takes the trip's approved budget, and may not exceed it; the five parts are all 0 or
 * add up to the total exactly. Changing an approved budget withdraws its approval.
 */
export interface CreateStaffTravelBudget {
  staffTravelRequestId: string;
  budgetYear: number;
  approvedTotal: number;
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
  staffTravelRequestId: string;
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
  /** Server-derived: recovered from the linked advance when the claim is PAID (not approved). */
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
  // Lane 3 (slice 3b).
  /** The reviewer's words on the outcome — always there for a returned or rejected claim. */
  reviewNotes?: string | null;
  /** Who recorded the payment — never the claimant or a reviewer of the claim. */
  paidById?: string | null;
  paidByName?: string | null;
  /** Why the claim was paid in full past advance cash the traveller held that it did not name. */
  advanceWaiverReason?: string | null;
  // Lane 3 (slice 3c, T-39): the last payment voided, if one was. The claim went back to approved.
  paymentVoidedAt?: string | null;
  paymentVoidedByName?: string | null;
  paymentVoidReason?: string | null;
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

/**
 * The traveller and the currency are the server's (lane 3): the trip's traveller, and the base currency every
 * claim total is kept in. Only on a trip that is approved, under way or completed.
 */
export interface CreateStaffTravelExpenseClaim {
  staffTravelRequestId: string;
  claimType: TravelClaimType;
  /** An advance of this trip and traveller, recovered when the claim is paid. */
  travelAdvanceId?: string | null;
  lines: CreateStaffTravelExpenseClaimLine[];
}

/** While the claim is a draft or returned. */
export interface UpdateStaffTravelExpenseClaim {
  id: string;
  claimType: TravelClaimType;
  travelAdvanceId?: string | null;
}

/**
 * The review's outcome (lane 3). `UnderReview`, `Rejected` and `Returned` are recorded as sent — the last two
 * need `notes`, which the claimant sees. `Approved` asks the server to approve what the lines' reviews
 * approved: every expense must be decided first, and the claim becomes `Approved` when all of it was approved,
 * `PartiallyApproved` otherwise. Never the reviewer's own claim.
 */
export interface ReviewStaffTravelExpenseClaim {
  claimId: string;
  newStatus: TravelClaimStatus;
  notes?: string | null;
}

/**
 * One expense decided (lane 3). `Approved` takes `amountApproved` — the whole line when omitted, never more —
 * and the rest is rejected by the server; `Rejected` takes nothing. Any rejected part needs `rejectionReason`.
 */
export interface ReviewStaffTravelExpenseClaimLine {
  lineId: string;
  status: TravelExpenseLineStatus;
  amountApproved?: number | null;
  rejectionReason?: string | null;
}

/**
 * Not `PayrollOffset` (refused: payroll cannot receive travel claims yet). Never by the claimant or anyone who
 * reviewed the claim or one of its expenses. `advanceWaiverReason` is needed only when the traveller holds paid-out
 * advance cash on the trip that the claim does not name.
 */
export interface PayStaffTravelExpenseClaim {
  claimId: string;
  paymentMethod: TravelPaymentMethod;
  paymentReference?: string | null;
  advanceWaiverReason?: string | null;
}

/**
 * Lane 3, T-39: a travel administrator who is neither the claimant nor the payer, with a reason of at least five
 * characters. The payment's journal is reversed, its advance settlement undone, and the claim goes back to approved.
 */
export interface VoidStaffTravelClaimPayment {
  reason: string;
}

// ── Advances ─────────────────────────────────────────────────────────────────

export interface StaffTravelAdvanceSummary {
  id: string;
  advanceNumber: string;
  staffTravelRequestId: string;
  requestNumber?: string | null;
  employeeId: string;
  employeeName: string;
  requestedAmount: number;
  approvedAmount?: number | null;
  currencyCode: string;
  advanceType: TravelAdvanceType;
  advanceTypeName: string;
  status: TravelAdvanceStatus;
  statusName: string;
  /** Recovered by claims plus cash handed back. */
  settledAmount: number;
  /** What the traveller still holds: 0 until the advance is disbursed (lane 3). */
  unsettledAmount: number;
  refundedAmount: number;
  settlementDeadline?: string | null;
  disbursedAt?: string | null;
  /** Cash out past its deadline — true before the nightly sweep writes Overdue. */
  isOverdue: boolean;
  /** Why a rejected, cancelled or written-off advance ended as it did. */
  outcomeReason?: string | null;
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
  // Lane 3 — the verbs' records.
  rejectedAt?: string | null;
  rejectedByName?: string | null;
  rejectionReason?: string | null;
  cancelledAt?: string | null;
  cancelledByName?: string | null;
  cancellationReason?: string | null;
  writtenOffAt?: string | null;
  writtenOffByName?: string | null;
  writeOffReason?: string | null;
  writtenOffAmount?: number | null;
  refundedAmount: number;
  refundedAt?: string | null;
  refundedByName?: string | null;
  refundReference?: string | null;
  isOverdue: boolean;
}

/** The traveller is the trip's — the server sets it (lane 3, B3). */
export interface CreateStaffTravelAdvance {
  staffTravelRequestId: string;
  requestedAmount: number;
  currencyCode: string;
  advanceType: TravelAdvanceType;
  settlementDeadline?: string | null;
}

/** A requested advance only. */
export type UpdateStaffTravelAdvance =
  Omit<CreateStaffTravelAdvance, 'staffTravelRequestId'> & { id: string };

/** Reject, cancel or write off: the verb is the route; the reason is required. */
export interface DecideStaffTravelAdvance {
  reason: string;
}

/** Unused cash handed back — one refund per advance. */
export interface RefundStaffTravelAdvance {
  amount: number;
  reference: string;
}

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
