import { apiService } from '../api.service';
import type {
  AppraisalOutcomeRecommendation,
  CreateAppraisalOutcomeRecommendation,
  AppraisalTransitionReport,
  DeadlineEnforcementResult,
  EmploymentActionProposal,
  EmploymentActionProposalStatus,
  ManualAdvanceRequest,
  ManualAdvanceResult,
  ProposalNotes,
  RecommendationStatus,
  ResolveRecommendation,
  SalaryReviewProposal,
  SalaryReviewProposalStatus,
  TrainingBondStatus,
  TrainingServiceBond,
  UpdateSalaryReviewProposal,
} from '@/types/hr/outcomes';

/**
 * api/AppraisalOutcomeRecommendations — what an appraisal says should happen next.
 *
 * Proposing is limited to the appraisee's own manager, or HR (403 otherwise); approving,
 * rejecting and dismissing are HR. Approving *dispatches* the recommendation to the module that
 * owns the outcome, which creates the real downstream record.
 *
 * ⚠ Approve returns 200 whether or not the dispatch succeeded — check the returned `status`.
 * `Actioned` means the downstream record exists and `targetEntityId` points at it; `Approved`
 * means the handler failed and the item is a work list entry, not a finished one. Use
 * `retryDispatch` for those.
 */
class AppraisalOutcomeRecommendationService {
  private readonly baseUrl = '/AppraisalOutcomeRecommendations';

  getByAppraisal(appraisalId: string): Promise<AppraisalOutcomeRecommendation[]> {
    return apiService.get<AppraisalOutcomeRecommendation[]>(
      `${this.baseUrl}/by-appraisal/${appraisalId}`,
    );
  }

  /** HR's cross-organisation queue. Capped at 500 rows, newest first. */
  getWorklist(status?: RecommendationStatus): Promise<AppraisalOutcomeRecommendation[]> {
    return apiService.get<AppraisalOutcomeRecommendation[]>(
      `${this.baseUrl}/worklist`,
      status ? { status } : undefined,
    );
  }

  propose(
    data: CreateAppraisalOutcomeRecommendation,
  ): Promise<AppraisalOutcomeRecommendation> {
    return apiService.post<AppraisalOutcomeRecommendation>(this.baseUrl, data);
  }

  /** Idempotent: an already-actioned recommendation is returned unchanged. */
  approve(id: string): Promise<AppraisalOutcomeRecommendation> {
    return apiService.post<AppraisalOutcomeRecommendation>(`${this.baseUrl}/${id}/approve`);
  }

  /** Re-runs a failed dispatch. 422 unless the recommendation is Approved-but-not-Actioned. */
  retryDispatch(id: string): Promise<AppraisalOutcomeRecommendation> {
    return apiService.post<AppraisalOutcomeRecommendation>(`${this.baseUrl}/${id}/retry-dispatch`);
  }

  /** 422 once the recommendation has been actioned — the downstream record already exists. */
  reject(id: string, data: ResolveRecommendation = {}): Promise<AppraisalOutcomeRecommendation> {
    return apiService.post<AppraisalOutcomeRecommendation>(`${this.baseUrl}/${id}/reject`, data);
  }

  dismiss(id: string, data: ResolveRecommendation = {}): Promise<AppraisalOutcomeRecommendation> {
    return apiService.post<AppraisalOutcomeRecommendation>(`${this.baseUrl}/${id}/dismiss`, data);
  }
}

/**
 * api/SalaryReviewProposals — the pay-for-performance intake record.
 *
 * The recommendation handler raises these with **no figure on them** — it has an appraisal, not
 * a pay decision — so `update` is where the number is actually set, and `submit` refuses until
 * it has been.
 *
 * Approval runs on the generic workflow engine: reuse `WorkflowApprovalActions` /
 * `WorkflowRecordTab` on the detail screen rather than driving approve/reject from here, and
 * remember that nothing can be submitted until a `SalaryReviewProposal` workflow definition has
 * been published.
 */
class SalaryReviewProposalService {
  private readonly baseUrl = '/SalaryReviewProposals';

  getAll(status?: SalaryReviewProposalStatus): Promise<SalaryReviewProposal[]> {
    return apiService.get<SalaryReviewProposal[]>(
      this.baseUrl,
      status ? { status } : undefined,
    );
  }

  getById(id: string): Promise<SalaryReviewProposal> {
    return apiService.get<SalaryReviewProposal>(`${this.baseUrl}/${id}`);
  }

  /** 422 once the proposal has left Proposed — an approved figure is what payroll acts on. */
  update(id: string, data: UpdateSalaryReviewProposal): Promise<SalaryReviewProposal> {
    return apiService.put<SalaryReviewProposal>(`${this.baseUrl}/${id}`, data);
  }

  submit(id: string): Promise<SalaryReviewProposal> {
    return apiService.post<SalaryReviewProposal>(`${this.baseUrl}/${id}/submit`);
  }

  approve(id: string): Promise<SalaryReviewProposal> {
    return apiService.post<SalaryReviewProposal>(`${this.baseUrl}/${id}/approve`);
  }

  reject(id: string, data: ProposalNotes = {}): Promise<SalaryReviewProposal> {
    return apiService.post<SalaryReviewProposal>(`${this.baseUrl}/${id}/reject`, data);
  }

  recall(id: string): Promise<SalaryReviewProposal> {
    return apiService.post<SalaryReviewProposal>(`${this.baseUrl}/${id}/recall`);
  }

  /** Records that payroll has made the change. Only from Approved. */
  markApplied(id: string, data: ProposalNotes = {}): Promise<SalaryReviewProposal> {
    return apiService.post<SalaryReviewProposal>(`${this.baseUrl}/${id}/mark-applied`, data);
  }
}

/**
 * api/EmploymentActionProposals — promotion, demotion, contract renewal, termination and
 * recognition intake. Same shape as the salary proposals, minus the figure: the proposal
 * captures the intent, and HR actions it in the module that owns the change.
 */
class EmploymentActionProposalService {
  private readonly baseUrl = '/EmploymentActionProposals';

  getAll(status?: EmploymentActionProposalStatus): Promise<EmploymentActionProposal[]> {
    return apiService.get<EmploymentActionProposal[]>(
      this.baseUrl,
      status ? { status } : undefined,
    );
  }

  getById(id: string): Promise<EmploymentActionProposal> {
    return apiService.get<EmploymentActionProposal>(`${this.baseUrl}/${id}`);
  }

  submit(id: string): Promise<EmploymentActionProposal> {
    return apiService.post<EmploymentActionProposal>(`${this.baseUrl}/${id}/submit`);
  }

  approve(id: string): Promise<EmploymentActionProposal> {
    return apiService.post<EmploymentActionProposal>(`${this.baseUrl}/${id}/approve`);
  }

  reject(id: string, data: ProposalNotes = {}): Promise<EmploymentActionProposal> {
    return apiService.post<EmploymentActionProposal>(`${this.baseUrl}/${id}/reject`, data);
  }

  recall(id: string): Promise<EmploymentActionProposal> {
    return apiService.post<EmploymentActionProposal>(`${this.baseUrl}/${id}/recall`);
  }

  /** Records that the owning module has created the real record. Only from Approved. */
  markActioned(id: string, data: ProposalNotes = {}): Promise<EmploymentActionProposal> {
    return apiService.post<EmploymentActionProposal>(`${this.baseUrl}/${id}/mark-actioned`, data);
  }
}

/**
 * api/DeadlineEnforcement — HR's manual override on a stalled pipeline. On demand only; there is
 * no background job, so nothing happens unless someone runs it.
 *
 * The advancing officer comes from the token and is written to the audit log.
 */
class DeadlineEnforcementService {
  private readonly baseUrl = '/DeadlineEnforcement';

  /**
   * Sweeps a cycle and advances every active appraisal whose current step is overdue.
   *
   * ⚠ Honours the cycle's `autoLockOnDeadline` setting: with it off, nothing is changed and
   * `advanced` is 0 however many are overdue. Check `autoLockEnabled` on the result and say so,
   * rather than reporting a successful no-op.
   */
  enforce(cycleId: string): Promise<DeadlineEnforcementResult> {
    return apiService.post<DeadlineEnforcementResult>(`${this.baseUrl}/enforce/${cycleId}`);
  }

  /**
   * Advances one appraisal past one stalled sub-step. `targetSubStatus` is optional — omit it to
   * advance past whatever is currently blocking.
   *
   * ⚠ Returns 200 with `success: false` and an `errorMessage` for a refused advance, as well as
   * 400 for a malformed one. Check the flag.
   */
  advance(appraisalId: string, data: ManualAdvanceRequest): Promise<ManualAdvanceResult> {
    return apiService.post<ManualAdvanceResult>(`${this.baseUrl}/advance/${appraisalId}`, data);
  }

  /**
   * The cycle's in-flight appraisals whose records run ahead of the gates (closure B8) — HR's list
   * to waive through `advance`. Read-only.
   */
  transitionReport(cycleId: string): Promise<AppraisalTransitionReport> {
    return apiService.get<AppraisalTransitionReport>(`${this.baseUrl}/transition-report/${cycleId}`);
  }
}

/**
 * api/training-service-bonds — the service obligation attached to a sponsored training
 * nomination. Listed under area 24 because it is enforcement, not training delivery.
 *
 * ⚠ Note the kebab-case route; HR routes are per-controller with no shared prefix.
 */
class TrainingServiceBondService {
  private readonly baseUrl = '/training-service-bonds';

  getAll(status?: TrainingBondStatus): Promise<TrainingServiceBond[]> {
    return apiService.get<TrainingServiceBond[]>(this.baseUrl, status ? { status } : undefined);
  }

  /**
   * Raise a bond by hand.
   *
   * ⚠ Bonds are normally MINTED SERVER-SIDE when a sponsored nomination is submitted, which is
   * why this had no caller. It is the exception: a sponsorship agreed outside the nomination flow,
   * or one the programme's own terms did not produce.
   */
  create(payload: {
    nominationId: string;
    bondDurationMonths: number;
    bondAmount: number;
    currency: string;
    termsText?: string | null;
    notes?: string | null;
  }): Promise<TrainingServiceBond> {
    return apiService.post<TrainingServiceBond>(this.baseUrl, payload);
  }

  /**
   * Correct one.
   *
   * ⚠ **This is money.** The amount and the duration are copied from the programme when the bond
   * is minted, so a programme priced wrongly mints every bond wrongly — and until this existed the
   * only correction was to delete the bond and raise another, which loses the acceptance the
   * employee has already given. The edit keeps the row and its signature.
   */
  update(id: string, payload: {
    id: string;
    bondDurationMonths: number;
    bondAmount: number;
    currency: string;
    termsText?: string | null;
    notes?: string | null;
  }): Promise<TrainingServiceBond> {
    return apiService.put<TrainingServiceBond>(`${this.baseUrl}/${id}`, payload);
  }

  /** For one raised in error. A bond that was accepted should be waived, not deleted. */
  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  getActive(): Promise<TrainingServiceBond[]> {
    return apiService.get<TrainingServiceBond[]>(`${this.baseUrl}/active`);
  }

  getById(id: string): Promise<TrainingServiceBond> {
    return apiService.get<TrainingServiceBond>(`${this.baseUrl}/${id}`);
  }

  /** The signed-in employee's own bonds. Returns `[]` when the account has no employee link. */
  getMine(): Promise<TrainingServiceBond[]> {
    return apiService.get<TrainingServiceBond[]>(`${this.baseUrl}/mine`);
  }

  getByEmployee(employeeId: string): Promise<TrainingServiceBond[]> {
    return apiService.get<TrainingServiceBond[]>(`${this.baseUrl}/by-employee/${employeeId}`);
  }

  /**
   * The employee accepting their own terms; the obligation period starts here. Omit
   * `bondStartDate` to let the server default it to the completion or acceptance date.
   */
  accept(bondId: string, notes?: string, bondStartDate?: string): Promise<TrainingServiceBond> {
    return apiService.post<TrainingServiceBond>(`${this.baseUrl}/accept`, {
      bondId,
      bondStartDate,
      acceptanceNotes: notes,
    });
  }

  /** HR recording acceptance on the employee's behalf — a form signed offline. */
  acceptOnBehalf(
    bondId: string,
    notes?: string,
    bondStartDate?: string,
  ): Promise<TrainingServiceBond> {
    return apiService.post<TrainingServiceBond>(`${this.baseUrl}/accept-on-behalf`, {
      bondId,
      bondStartDate,
      acceptanceNotes: notes,
    });
  }

  /** Records an early exit, which computes the pro-rated repayment and marks the bond Breached. */
  recordExit(bondId: string, exitDate: string, notes?: string): Promise<TrainingServiceBond> {
    return apiService.post<TrainingServiceBond>(`${this.baseUrl}/record-exit`, {
      bondId,
      exitDate,
      notes,
    });
  }

  waive(bondId: string, waiverReason: string): Promise<TrainingServiceBond> {
    return apiService.post<TrainingServiceBond>(`${this.baseUrl}/waive`, { bondId, waiverReason });
  }

  settle(bondId: string, settledDate?: string, notes?: string): Promise<TrainingServiceBond> {
    return apiService.post<TrainingServiceBond>(`${this.baseUrl}/settle`, {
      bondId,
      settledDate,
      notes,
    });
  }
}

export const appraisalOutcomeRecommendationService = new AppraisalOutcomeRecommendationService();
export const salaryReviewProposalService = new SalaryReviewProposalService();
export const employmentActionProposalService = new EmploymentActionProposalService();
export const deadlineEnforcementService = new DeadlineEnforcementService();
export const trainingServiceBondService = new TrainingServiceBondService();
