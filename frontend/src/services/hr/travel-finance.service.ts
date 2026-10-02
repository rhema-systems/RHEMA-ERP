import { apiService } from '../api.service';
import type {
  StaffTravelBudget,
  CreateStaffTravelBudget,
  UpdateStaffTravelBudget,
  StaffTravelExpenseClaim,
  StaffTravelExpenseClaimSummary,
  StaffTravelExpenseClaimLine,
  CreateStaffTravelExpenseClaim,
  UpdateStaffTravelExpenseClaim,
  CreateStaffTravelExpenseClaimLine,
  ReviewStaffTravelExpenseClaim,
  ReviewStaffTravelExpenseClaimLine,
  PayStaffTravelExpenseClaim,
  TravelClaimStatus,
  StaffTravelAdvance,
  StaffTravelAdvanceSummary,
  CreateStaffTravelAdvance,
  UpdateStaffTravelAdvance,
  ApproveStaffTravelAdvance,
  RefundStaffTravelAdvance,
  TravelAdvanceStatus,
  StaffTravelPerDiemRate,
  CreateStaffTravelPerDiemRate,
  UpdateStaffTravelPerDiemRate,
} from '@/types/hr/travel-finance';

/**
 * Staff travel money: budgets, expense claims, advances and per-diem rates.
 * Backend route: `api/staff-travel/finance`.
 *
 * <b>The rule for every screen over this service: send facts, never consequences.</b> A claim line
 * carries what was spent and in what currency; the exchange rate, the base-currency amount and
 * every total are the server's. An advance carries what was asked for; what was approved comes from
 * the approve endpoint, and what is outstanding from the claims settled against it. Screens that
 * "help" by computing a total and sending it are how this area came to have two formulas for
 * `NetPayable` and an advance that was never recovered.
 *
 * ⚠ <b>Reviewing is not updating.</b> `reviewClaim`, `reviewClaimLine` and `approveAdvance` stamp
 * the reviewer from the token and check the record's status; the ordinary `update` calls
 * deliberately cannot set an outcome. Do not reach for `update` because it is one call fewer.
 */
class TravelFinanceService {
  private readonly baseUrl = '/staff-travel/finance';

  // ── Budget ─────────────────────────────────────────────────────────────────

  /**
   * A request's budget, or null when none has been set. `totalCommitted` / `totalActual` /
   * `variance` are recomputed as this is read, so they are never stale against the bookings.
   */
  getBudget(requestId: string) {
    return apiService.get<StaffTravelBudget | null>(`${this.baseUrl}/budgets/request/${requestId}`);
  }

  createBudget(payload: CreateStaffTravelBudget) {
    return apiService.post<StaffTravelBudget>(`${this.baseUrl}/budgets`, payload);
  }

  updateBudget(payload: UpdateStaffTravelBudget) {
    return apiService.put<StaffTravelBudget>(`${this.baseUrl}/budgets/${payload.id}`, payload);
  }

  // ── Expense claims ─────────────────────────────────────────────────────────

  getClaims() {
    return apiService.get<StaffTravelExpenseClaimSummary[]>(`${this.baseUrl}/claims`);
  }

  getClaim(id: string) {
    return apiService.get<StaffTravelExpenseClaim>(`${this.baseUrl}/claims/${id}`);
  }

  getClaimsByRequest(requestId: string) {
    return apiService.get<StaffTravelExpenseClaimSummary[]>(
      `${this.baseUrl}/claims/request/${requestId}`);
  }

  getClaimsByEmployee(employeeId: string) {
    return apiService.get<StaffTravelExpenseClaimSummary[]>(
      `${this.baseUrl}/claims/employee/${employeeId}`);
  }

  getClaimsByStatus(status: TravelClaimStatus) {
    return apiService.get<StaffTravelExpenseClaimSummary[]>(
      `${this.baseUrl}/claims/status/${status}`);
  }

  /** The finance desk's payment queue: approved and not yet paid. */
  getUnpaidApprovedClaims() {
    return apiService.get<StaffTravelExpenseClaimSummary[]>(
      `${this.baseUrl}/claims/unpaid-approved`);
  }

  createClaim(payload: CreateStaffTravelExpenseClaim) {
    return apiService.post<StaffTravelExpenseClaim>(`${this.baseUrl}/claims`, payload);
  }

  updateClaim(payload: UpdateStaffTravelExpenseClaim) {
    return apiService.put<StaffTravelExpenseClaim>(
      `${this.baseUrl}/claims/${payload.id}`, payload);
  }

  /** Admin-gated. */
  deleteClaim(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/claims/${id}`);
  }

  submitClaim(id: string) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/claims/${id}/submit`, {});
  }

  /**
   * Sets the claim's status outright. The reviewer is the token's employee record, so this endpoint
   * needs the caller linked to one.
   *
   * ⚠ Reviewing the lines does **not** review the claim — the claim's status is set here and
   * nowhere else, and `payClaim` refuses anything not `Approved`.
   */
  reviewClaim(id: string, payload: Omit<ReviewStaffTravelExpenseClaim, 'claimId'>) {
    return apiService.post<{ message: string }>(
      `${this.baseUrl}/claims/${id}/review`, { claimId: id, ...payload });
  }

  /**
   * Records that the money left. The advance recovery has already happened at approval, so
   * `netPayable` is what is actually paid — do not re-deduct anything on screen.
   */
  payClaim(id: string, payload: Omit<PayStaffTravelExpenseClaim, 'claimId'>) {
    return apiService.post<{ message: string }>(
      `${this.baseUrl}/claims/${id}/pay`, { claimId: id, ...payload });
  }

  // ── Claim lines ────────────────────────────────────────────────────────────

  getClaimLines(claimId: string) {
    return apiService.get<StaffTravelExpenseClaimLine[]>(`${this.baseUrl}/claims/${claimId}/lines`);
  }

  /**
   * ⚠ `currencyOriginal` must be one Finance holds, and Finance must have a rate for it on the
   * expense date — otherwise the line is refused with 422 saying so, rather than silently valued
   * as though it were already in the base currency.
   */
  addClaimLine(claimId: string, payload: CreateStaffTravelExpenseClaimLine) {
    return apiService.post<StaffTravelExpenseClaimLine>(
      `${this.baseUrl}/claims/${claimId}/lines`, { ...payload, staffTravelExpenseClaimId: claimId });
  }

  updateClaimLine(payload: CreateStaffTravelExpenseClaimLine & { id: string }) {
    return apiService.put<StaffTravelExpenseClaimLine>(
      `${this.baseUrl}/lines/${payload.id}`, payload);
  }

  /** Approving or rejecting one line. Omitting `amountApproved` approves the full amount. */
  reviewClaimLine(lineId: string, payload: Omit<ReviewStaffTravelExpenseClaimLine, 'lineId'>) {
    return apiService.post<{ message: string }>(
      `${this.baseUrl}/lines/${lineId}/review`, { lineId, ...payload });
  }

  /** Admin-gated. */
  deleteClaimLine(lineId: string) {
    return apiService.delete<void>(`${this.baseUrl}/lines/${lineId}`);
  }

  // ── Advances ───────────────────────────────────────────────────────────────

  getAdvances() {
    return apiService.get<StaffTravelAdvanceSummary[]>(`${this.baseUrl}/advances`);
  }

  getAdvance(id: string) {
    return apiService.get<StaffTravelAdvance>(`${this.baseUrl}/advances/${id}`);
  }

  getAdvancesByRequest(requestId: string) {
    return apiService.get<StaffTravelAdvanceSummary[]>(
      `${this.baseUrl}/advances/request/${requestId}`);
  }

  getAdvancesByStatus(status: TravelAdvanceStatus) {
    return apiService.get<StaffTravelAdvanceSummary[]>(`${this.baseUrl}/advances/status/${status}`);
  }

  getOutstandingAdvances(employeeId: string) {
    return apiService.get<StaffTravelAdvanceSummary[]>(
      `${this.baseUrl}/advances/employee/${employeeId}/outstanding`);
  }

  /** Advances with cash still out past their settlement deadline — the desk's chase list. */
  getOverdueSettlements() {
    return apiService.get<StaffTravelAdvanceSummary[]>(
      `${this.baseUrl}/advances/overdue-settlements`);
  }

  createAdvance(payload: CreateStaffTravelAdvance) {
    return apiService.post<StaffTravelAdvance>(`${this.baseUrl}/advances`, payload);
  }

  /** ⚠ Cannot set `approvedAmount` — that is `approveAdvance`, which records who approved it. */
  updateAdvance(payload: UpdateStaffTravelAdvance) {
    return apiService.put<StaffTravelAdvance>(`${this.baseUrl}/advances/${payload.id}`, payload);
  }

  /** Admin-gated. */
  deleteAdvance(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/advances/${id}`);
  }

  approveAdvance(id: string, approvedAmount: number) {
    return apiService.post<{ message: string }>(
      `${this.baseUrl}/advances/${id}/approve`, { advanceId: id, approvedAmount });
  }

  /** The disburser and the moment are both the server's; there is nothing else to send. Never the approver (D-2). */
  disburseAdvance(id: string) {
    return apiService.post<{ message: string }>(
      `${this.baseUrl}/advances/${id}/disburse`, { advanceId: id });
  }

  /** A requested advance, refused with a reason. */
  rejectAdvance(id: string, reason: string) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/advances/${id}/reject`, { reason });
  }

  /** A requested or approved advance withdrawn before any money goes out. */
  cancelAdvance(id: string, reason: string) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/advances/${id}/cancel`, { reason });
  }

  /** Unused cash handed back — at most what is outstanding, once per advance. */
  refundAdvance(id: string, payload: RefundStaffTravelAdvance) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/advances/${id}/refund`, payload);
  }

  /** Admin-gated: writes off what is left on an advance with cash out. */
  writeOffAdvance(id: string, reason: string) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/advances/${id}/write-off`, { reason });
  }

  // ── Per-diem rates ─────────────────────────────────────────────────────────

  getActivePerDiemRates() {
    return apiService.get<StaffTravelPerDiemRate[]>(`${this.baseUrl}/per-diem-rates/active`);
  }

  getPerDiemRate(id: string) {
    return apiService.get<StaffTravelPerDiemRate>(`${this.baseUrl}/per-diem-rates/${id}`);
  }

  getPerDiemRatesByCountry(countryId: string) {
    return apiService.get<StaffTravelPerDiemRate[]>(
      `${this.baseUrl}/per-diem-rates/country/${countryId}`);
  }

  /** The rate in force for a country, city and staff level on a date. */
  getEffectivePerDiemRate(params: {
    countryId: string;
    onDate: string;
    city?: string;
    staffLevelId?: string;
  }) {
    return apiService.get<StaffTravelPerDiemRate | null>(
      `${this.baseUrl}/per-diem-rates/effective`, params);
  }

  createPerDiemRate(payload: CreateStaffTravelPerDiemRate) {
    return apiService.post<StaffTravelPerDiemRate>(`${this.baseUrl}/per-diem-rates`, payload);
  }

  updatePerDiemRate(payload: UpdateStaffTravelPerDiemRate) {
    return apiService.put<StaffTravelPerDiemRate>(
      `${this.baseUrl}/per-diem-rates/${payload.id}`, payload);
  }

  /** Admin-gated. */
  deletePerDiemRate(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/per-diem-rates/${id}`);
  }
}

export const travelFinanceService = new TravelFinanceService();
