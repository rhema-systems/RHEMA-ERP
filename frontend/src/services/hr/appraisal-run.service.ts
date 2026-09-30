import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  ApprovePeerNominations,
  ApproveAppraisal,
  AppraisalEditRole,
  AppraisalEditableResponse,
  AppraisalEmployeeResponse,
  AppraisalPhaseResponse,
  BatchCreatePeerNominations,
  CheckIn,
  CheckInGoalUpdate,
  CompleteCheckIn,
  CreateCheckIn,
  CreateCheckInGoalUpdate,
  HRReview,
  HRReviewListItem,
  ManagerEvaluationContext,
  ManagerEvaluationResult,
  ManagerPeerEvaluationReview,
  MyAppraisal,
  PeerEvaluationAssignment,
  PeerEvaluationDetail,
  PeerNomination,
  PeerNominationSummary,
  PerformanceAppraisal,
  RejectPeerNominations,
  ReturnAppraisal,
  SaveManagerEvaluation,
  SavePeerEvaluation,
  SaveSelfEvaluation,
  SelfEvaluationContext,
  SelfEvaluationResult,
  TeamAppraisalCycleSummary,
  TeamMemberAppraisal,
  UpdateCheckIn,
  UpdateCheckInGoalUpdate,
  ViewSubmittedEvaluation,
} from '@/types/hr/appraisal-run';

/**
 * The appraisal run — the evaluation legs on an appraisal record that a cycle has generated.
 *
 * **The actor is never sent.** Every action here is taken as an employee (the appraisee, their
 * manager, a nominated peer, the HR reviewer) and the server takes that identity from the
 * token. Where a DTO still carries an id field it is overwritten server-side; the `/me` routes
 * do not carry one at all. Do not add "act as" parameters to these calls.
 *
 * **Business rules answer 422** with `{ message }` — goal caps, unscored criteria, "self
 * evaluation must be completed before finalizing", nomination limits. `apiService` surfaces
 * `.message`, so those are safe to show verbatim. 404 means not found *or* not yours.
 */

// ── The appraisal record and its evaluation legs ───────────────────────────────────

class PerformanceAppraisalService {
  private readonly baseUrl = '/PerformanceAppraisals';

  getAll(): Promise<PerformanceAppraisal[]> {
    return apiService.get<PerformanceAppraisal[]>(this.baseUrl);
  }

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<PerformanceAppraisal>> {
    return apiService.get<PagedResult<PerformanceAppraisal>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getById(id: string): Promise<PerformanceAppraisal> {
    return apiService.get<PerformanceAppraisal>(`${this.baseUrl}/${id}`);
  }

  getByEmployee(employeeId: string): Promise<PerformanceAppraisal[]> {
    return apiService.get<PerformanceAppraisal[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  // ── The employee's own appraisals ────────────────────────────────────────────────

  /**
   * The signed-in employee's appraisals. `cycleFilter` accepts `open` or `completed`; anything
   * else is ignored and returns everything.
   *
   * Peer work owed on *other people's* appraisals is deliberately not here — see
   * `peerEvaluationService.getMyAssignments`.
   */
  getMine(cycleFilter?: 'open' | 'completed'): Promise<MyAppraisal[]> {
    return apiService.get<MyAppraisal[]>(
      `${this.baseUrl}/my-appraisals/me`,
      cycleFilter ? { cycleFilter } : undefined,
    );
  }

  /** Someone else's list — for HR. Prefer `getMine` for the signed-in user. */
  getForEmployee(employeeId: string, cycleFilter?: 'open' | 'completed'): Promise<MyAppraisal[]> {
    return apiService.get<MyAppraisal[]>(
      `${this.baseUrl}/my-appraisals/${employeeId}`,
      cycleFilter ? { cycleFilter } : undefined,
    );
  }

  // ── Self-evaluation ──────────────────────────────────────────────────────────────

  getSelfEvaluationContext(appraisalId: string): Promise<SelfEvaluationContext> {
    return apiService.get<SelfEvaluationContext>(
      `${this.baseUrl}/${appraisalId}/self-evaluation-context`,
    );
  }

  /**
   * Draft save and submit are the same call — `isDraft` decides. A submit runs the completeness
   * rules (every scored item carries a value, peer nominations within range when the cycle
   * requires employee-driven nomination, all competencies rated when soft-skill rating is on).
   *
   * ⚠ Check `result.success`: a rejected save answers 200 with `success: false` as well as 400.
   */
  saveSelfEvaluation(appraisalId: string, data: SaveSelfEvaluation): Promise<SelfEvaluationResult> {
    return apiService.post<SelfEvaluationResult>(
      `${this.baseUrl}/${appraisalId}/self-evaluation`,
      data,
    );
  }

  /** Read-only view of what was submitted. 404 until the self-evaluation is in. */
  getSubmittedEvaluation(appraisalId: string): Promise<ViewSubmittedEvaluation> {
    return apiService.get<ViewSubmittedEvaluation>(
      `${this.baseUrl}/${appraisalId}/view-submitted-evaluation`,
    );
  }

  // ── Manager evaluation ───────────────────────────────────────────────────────────

  /**
   * Cycles in which the signed-in manager has anyone to evaluate.
   *
   * The `/me` form is what the UI uses: the client has no employee id of its own — it only
   * ever exists in the token — so there is nothing to pass.
   */
  getMyTeamCycles(): Promise<TeamAppraisalCycleSummary[]> {
    return apiService.get<TeamAppraisalCycleSummary[]>(`${this.baseUrl}/manager/me/team-cycles`);
  }

  getMyTeamMembers(cycleId: string): Promise<TeamMemberAppraisal[]> {
    return apiService.get<TeamMemberAppraisal[]>(
      `${this.baseUrl}/manager/me/cycle/${cycleId}/team-members`,
    );
  }

  /** Another manager's team — for HR looking across the organisation. */
  getTeamCycles(managerId: string): Promise<TeamAppraisalCycleSummary[]> {
    return apiService.get<TeamAppraisalCycleSummary[]>(
      `${this.baseUrl}/manager/${managerId}/team-cycles`,
    );
  }

  getTeamMembers(managerId: string, cycleId: string): Promise<TeamMemberAppraisal[]> {
    return apiService.get<TeamMemberAppraisal[]>(
      `${this.baseUrl}/manager/${managerId}/cycle/${cycleId}/team-members`,
    );
  }

  /** The manager is resolved from the token; 403 when the caller is not this employee's manager. */
  getManagerEvaluationContext(appraisalId: string): Promise<ManagerEvaluationContext> {
    return apiService.get<ManagerEvaluationContext>(
      `${this.baseUrl}/${appraisalId}/manager-evaluation-context`,
    );
  }

  /**
   * Draft save and submit, as with the self-evaluation. Submitting is what hands the appraisal
   * to HR: it assigns the HR reviewer and moves the record to Governance.
   *
   * ⚠ Blocked once submitted — a remand is the only way back, and it clears the submission first.
   */
  saveManagerEvaluation(
    appraisalId: string,
    data: SaveManagerEvaluation,
  ): Promise<ManagerEvaluationResult> {
    return apiService.post<ManagerEvaluationResult>(
      `${this.baseUrl}/${appraisalId}/manager-evaluation`,
      data,
    );
  }

  /**
   * Peer feedback in full, for the manager: every criterion each peer scored — competency, and KPI
   * or goal rows where the cycle lets peers score them. Evaluator identities are always shown here.
   */
  getPeerEvaluationReview(appraisalId: string): Promise<ManagerPeerEvaluationReview> {
    return apiService.get<ManagerPeerEvaluationReview>(
      `${this.baseUrl}/${appraisalId}/manager-peer-evaluations`,
    );
  }

  // ── Peer nominations ─────────────────────────────────────────────────────────────

  /** The appraisee in Manager mode with anonymous reviews gets the counts only (`peersWithheld`). */
  getPeerNominationSummary(appraisalId: string): Promise<PeerNominationSummary> {
    return apiService.get<PeerNominationSummary>(
      `${this.baseUrl}/${appraisalId}/peer-nominations/summary`,
    );
  }

  /**
   * In Employee mode nominations land Pending and the manager approves them. In Manager mode the
   * manager chooses the peers, so they are approved as they are made and the peers asked at once
   * (performance closure D4).
   */
  nominatePeers(
    appraisalId: string,
    data: BatchCreatePeerNominations,
  ): Promise<PeerNomination[]> {
    return apiService.post<PeerNomination[]>(
      `${this.baseUrl}/${appraisalId}/peer-nominations/batch`,
      data,
    );
  }

  /** Creates each peer's evaluation record and notifies them. 422 if any is not Pending. */
  approvePeerNominations(
    appraisalId: string,
    data: ApprovePeerNominations,
  ): Promise<PeerNomination[]> {
    return apiService.post<PeerNomination[]>(
      `${this.baseUrl}/${appraisalId}/peer-nominations/approve`,
      data,
    );
  }

  rejectPeerNominations(
    appraisalId: string,
    data: RejectPeerNominations,
  ): Promise<PeerNomination[]> {
    return apiService.post<PeerNomination[]>(
      `${this.baseUrl}/${appraisalId}/peer-nominations/reject`,
      data,
    );
  }

  // ── HR review ────────────────────────────────────────────────────────────────────

  /**
   * The HR sign-off context. The requester is resolved from the token, and that is what hides
   * peer detail from the appraisee when the cycle runs anonymous peer reviews.
   */
  getHRReview(appraisalId: string): Promise<HRReview> {
    return apiService.get<HRReview>(`${this.baseUrl}/${appraisalId}/hr-review`);
  }

  /** The HR queue. `status` accepts the `hrReviewStatus` values. */
  getHRReviewList(cycleId?: string, status?: string): Promise<HRReviewListItem[]> {
    const params: Record<string, string> = {};
    if (cycleId) params.cycleId = cycleId;
    if (status) params.status = status;
    return apiService.get<HRReviewListItem[]>(
      `${this.baseUrl}/hr-review-list`,
      Object.keys(params).length ? params : undefined,
    );
  }

  /**
   * Assigns an HR reviewer and opens the HR evaluation record. Submitting the manager
   * evaluation does this automatically; this is the repair route for when no HR employee could
   * be resolved at the time. Returns false when HR review is not required, or already assigned.
   */
  progressToHRReview(appraisalId: string): Promise<boolean> {
    return apiService.post<boolean>(`${this.baseUrl}/${appraisalId}/progress-to-hr-review`);
  }

  /**
   * Signs the appraisal off: recalculates the weighted final score, records the sign-off, and
   * pushes the score onto the employee's talent records.
   *
   * Lands on **Governance** (awaiting the employee's acknowledgment) when the cycle requires
   * one, otherwise straight to **Completed**. 422 while self, manager or the minimum peer
   * count is outstanding.
   */
  approve(appraisalId: string, data: ApproveAppraisal): Promise<HRReview> {
    return apiService.post<HRReview>(`${this.baseUrl}/${appraisalId}/approve`, data);
  }

  /**
   * Reopens the manager's evaluation and puts the appraisal back to Active. Remarks required. Only
   * in governance, before the sign-off and while no calibration panel is sitting on it (422
   * otherwise); the panel's restatement goes, so the appraisal is calibrated again on the manager's
   * new evaluation. 403 on your own appraisal (performance closure E-a).
   */
  returnToManager(appraisalId: string, data: ReturnAppraisal): Promise<HRReview> {
    return apiService.post<HRReview>(`${this.baseUrl}/${appraisalId}/return-to-manager`, data);
  }

  // ── Employee acknowledgment ──────────────────────────────────────────────────────

  /**
   * Corrects a generated appraisal's window — its year and dates — and nothing else (performance
   * closure E-a). 422 once the appraisal is final (Completed, Closed, Appealed, Withdrawn) or when
   * the end is not after the start; 403 on your own appraisal.
   *
   * The route used to take the whole record — the employee, the cycle, the status, the manager's
   * narrative and recommendations, the peer count — and this sent none of the manager's fields, so
   * every correction blanked them. Regenerating the cycle is not an alternative: it does not touch
   * an appraisal that already exists.
   */
  updateHeader(appraisal: PerformanceAppraisal, patch: {
    year: number; startDate: string; endDate: string;
  }): Promise<void> {
    return apiService.put<void>(`${this.baseUrl}/${appraisal.id}`, { id: appraisal.id, ...patch });
  }

  /**
   * Removes an appraisal generated against somebody who should not have been in scope — only
   * before anything in it counts: Draft, or Active with no evaluation submitted (422 otherwise;
   * performance closure E-a).
   *
   * ⚠ Admin-tier — an HR-role caller is refused with a 403, established by
   * `hr-performance/probe-lane3-appraisals.mjs` rather than assumed, which is why the control is
   * behind a permission gate rather than shown to everyone who can open the screen. No other route
   * removes one.
   */
  deleteAppraisal(appraisalId: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/${appraisalId}`);
  }

  /** What has been written in answer to this appraisal. Readable by anyone who may read it. */
  getResponses(appraisalId: string): Promise<AppraisalEmployeeResponse[]> {
    return apiService.get<AppraisalEmployeeResponse[]>(`${this.baseUrl}/${appraisalId}/responses`);
  }

  /**
   * The appraisee's own written answer to their appraisal.
   *
   * ⚠ Deliberately the `/me` route, not the desk one. The desk route sits on the HR write policy
   * and the response row has no author column of any kind — so through it, "the employee's
   * response" was whatever HR typed, and nothing on the record contradicted that. This route
   * refuses anyone but the appraisal's own employee (404, never 403), which is the only thing
   * making the record mean what it says.
   */
  addMyResponse(appraisalId: string, responseText: string): Promise<AppraisalEmployeeResponse> {
    return apiService.post<AppraisalEmployeeResponse>(
      `/performance-appraisals/me/${appraisalId}/responses`,
      { appraisalId, responseText },
    );
  }

  /**
   * Closes out the appraisal. Only from Governance, only by the appraisee, and only once —
   * all three answer 422/403 rather than failing silently. The employee id in the body is
   * ignored; the token decides. An optional comment is kept on the appraisal (E-a).
   */
  acknowledge(appraisalId: string, comments?: string | null): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${appraisalId}/acknowledge`, {
      employeeId: '00000000-0000-0000-0000-000000000000',
      comments: comments ?? null,
    });
  }
}

// ── Peer evaluation ────────────────────────────────────────────────────────────────

/**
 * api/PeerEvaluations — a peer's own queue and scoring form. Every route resolves the
 * evaluator from the token, so there is no id to pass and no way to open someone else's form.
 */
class PeerEvaluationService {
  private readonly baseUrl = '/PeerEvaluations';

  /** Appears only once the nomination has been approved. */
  getMyAssignments(): Promise<PeerEvaluationAssignment[]> {
    return apiService.get<PeerEvaluationAssignment[]>(`${this.baseUrl}/me`);
  }

  getDetail(evaluationId: string): Promise<PeerEvaluationDetail> {
    return apiService.get<PeerEvaluationDetail>(`${this.baseUrl}/${evaluationId}`);
  }

  /** Refused with 422 once submitted. */
  saveDraft(evaluationId: string, data: SavePeerEvaluation): Promise<PeerEvaluationDetail> {
    return apiService.post<PeerEvaluationDetail>(`${this.baseUrl}/${evaluationId}/draft`, data);
  }

  /**
   * Submits. Everything required must be scored first — competencies always, KPI items only
   * when the cycle lets peers score them — and 422 names how many are outstanding.
   */
  submit(evaluationId: string): Promise<PeerEvaluationDetail> {
    return apiService.post<PeerEvaluationDetail>(`${this.baseUrl}/${evaluationId}/submit`);
  }
}

// ── Individual peer nominations ────────────────────────────────────────────────────

/** api/PeerNomination — single-row CRUD. Batch nominate/approve/reject live on the appraisal. */
class PeerNominationService {
  private readonly baseUrl = '/PeerNomination';

  getByAppraisal(appraisalId: string): Promise<PeerNomination[]> {
    return apiService.get<PeerNomination[]>(`${this.baseUrl}/appraisal/${appraisalId}`);
  }

  /**
   * The nominations this employee has been asked to act on as the peer: approved ones only, with no
   * rejection reason (performance closure D-41).
   */
  getForPeer(peerEmployeeId: string): Promise<PeerNomination[]> {
    return apiService.get<PeerNomination[]>(`${this.baseUrl}/peer/${peerEmployeeId}`);
  }

  /**
   * Withdraws a pending nomination. 422 once it is approved — the peer has been asked for their
   * feedback — or rejected. (`send-invitation` went in D3: approval asks the peer.)
   */
  remove(id: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/${id}`);
  }
}

// ── Check-ins ──────────────────────────────────────────────────────────────────────

/**
 * api/CheckIns — one-to-ones held during a cycle.
 *
 * ⚠ `privateNotes` come back on every read. They are the conductor's own record; never render
 * them on a screen the employee sees.
 */
class CheckInService {
  private readonly baseUrl = '/CheckIns';

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<CheckIn>> {
    return apiService.get<PagedResult<CheckIn>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getById(id: string): Promise<CheckIn> {
    return apiService.get<CheckIn>(`${this.baseUrl}/${id}`);
  }

  /** Check-ins the signed-in employee is the subject of. */
  getMine(cycleId?: string): Promise<CheckIn[]> {
    return apiService.get<CheckIn[]>(`${this.baseUrl}/me`, cycleId ? { cycleId } : undefined);
  }

  /** Check-ins the signed-in employee is running — the manager's list. */
  getMineAsConductor(cycleId?: string): Promise<CheckIn[]> {
    return apiService.get<CheckIn[]>(
      `${this.baseUrl}/me/conducting`,
      cycleId ? { cycleId } : undefined,
    );
  }

  getForEmployee(employeeId: string, cycleId?: string): Promise<CheckIn[]> {
    return apiService.get<CheckIn[]>(
      `${this.baseUrl}/by-employee/${employeeId}`,
      cycleId ? { cycleId } : undefined,
    );
  }

  getUpcoming(employeeId: string, daysAhead = 30): Promise<CheckIn[]> {
    return apiService.get<CheckIn[]>(`${this.baseUrl}/upcoming/${employeeId}`, { daysAhead });
  }

  /** 422 when the cycle's settings profile has check-ins switched off. */
  create(data: CreateCheckIn): Promise<CheckIn> {
    return apiService.post<CheckIn>(this.baseUrl, data);
  }

  update(id: string, data: UpdateCheckIn): Promise<CheckIn> {
    return apiService.put<CheckIn>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Stamps it held, now. The three note fields are replaced wholesale, not merged. */
  complete(id: string, data: CompleteCheckIn): Promise<CheckIn> {
    return apiService.post<CheckIn>(`${this.baseUrl}/${id}/complete`, data);
  }

  // ── Goal updates ─────────────────────────────────────────────────────────────────

  getGoalUpdates(checkInId: string): Promise<CheckInGoalUpdate[]> {
    return apiService.get<CheckInGoalUpdate[]>(`${this.baseUrl}/${checkInId}/goal-updates`);
  }

  /**
   * Records the goal's state as discussed **and applies it to the goal** — the percent, the
   * reported status and `flaggedAtRisk` all land on the `EmployeeGoal`. A goal that is not
   * live (draft, awaiting approval, locked, already complete) keeps its status and only the
   * note is recorded.
   */
  addGoalUpdate(checkInId: string, data: CreateCheckInGoalUpdate): Promise<CheckInGoalUpdate> {
    return apiService.post<CheckInGoalUpdate>(`${this.baseUrl}/${checkInId}/goal-updates`, data);
  }

  updateGoalUpdate(
    checkInId: string,
    updateId: string,
    data: UpdateCheckInGoalUpdate,
  ): Promise<CheckInGoalUpdate> {
    return apiService.put<CheckInGoalUpdate>(
      `${this.baseUrl}/${checkInId}/goal-updates/${updateId}`,
      data,
    );
  }

  removeGoalUpdate(checkInId: string, updateId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${checkInId}/goal-updates/${updateId}`);
  }
}

// ── Computed phase ─────────────────────────────────────────────────────────────────

/**
 * api/AppraisalWorkflow — where an appraisal actually is, and who may edit it.
 *
 * The phase is computed from live state each time, never stored: it reflects what has been
 * submitted plus which steps the cycle's settings require, so a cycle with no peer reviews
 * never reports `PeerEvaluation`. `AppraisalStatus` is the coarse record, this is the step.
 */
class AppraisalWorkflowService {
  private readonly baseUrl = '/AppraisalWorkflow';

  getPhase(appraisalId: string): Promise<AppraisalPhaseResponse> {
    return apiService.get<AppraisalPhaseResponse>(`${this.baseUrl}/${appraisalId}/phase`);
  }

  /** Whether a role-holder may submit edits right now, given the status and the phase. */
  isEditableBy(appraisalId: string, role: AppraisalEditRole): Promise<AppraisalEditableResponse> {
    return apiService.get<AppraisalEditableResponse>(
      `${this.baseUrl}/${appraisalId}/editable/${role}`,
    );
  }
}

export const performanceAppraisalService = new PerformanceAppraisalService();
export const peerEvaluationService = new PeerEvaluationService();
export const peerNominationService = new PeerNominationService();
export const checkInService = new CheckInService();
export const appraisalWorkflowService = new AppraisalWorkflowService();
