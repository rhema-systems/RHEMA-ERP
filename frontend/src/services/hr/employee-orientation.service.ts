import { apiService } from '../api.service';
import type {
  EmployeeOrientation,
  EmployeeOrientationSummary,
  EmployeeOrientationCreateRequest,
  EmployeeOrientationUpdateRequest,
  BulkEnrollOrientationRequest,
  WithdrawOrientationRequest,
  ConfirmOrientationAttendanceRequest,
  OrientationSessionCompletionPreview,
  OrientationCompletionStatus,
  OrientationModule,
  OrientationContentProgress,
  TrackOrientationContentProgressRequest,
  OrientationAssessmentQuestion,
  SubmitOrientationAssessmentRequest,
  OrientationAssessmentResult,
  OrientationAssessmentResponse,
  OrientationAcknowledgement,
  OrientationAcknowledgementCreateRequest,
  SignOrientationAcknowledgementRequest,
  OrientationFeedback,
  OrientationFeedbackCreateRequest,
  OrientationCertificate,
  IssueOrientationCertificateRequest,
  RevokeOrientationCertificateRequest,
} from '@/types/hr/orientation';
import type { PagedResult } from '@/types/hr/common';

/**
 * Enrollment and participation. Backend route: api/employee-orientations.
 *
 * Two audiences share this controller. Cohort-wide reads and administrative writes (enrol, amend,
 * withdraw, delete, certify) are HR-only. The participant surface below is open to any authenticated
 * user, but the server enforces record-level entitlement: a non-HR caller can only reach their own
 * enrollment, so calling these with someone else's id returns 403, not their data.
 */
class EmployeeOrientationService {
  private readonly baseUrl = '/employee-orientations';

  // ── Reads ─────────────────────────────────────────────────────────────────

  getById(id: string): Promise<EmployeeOrientation> {
    return apiService.get<EmployeeOrientation>(`${this.baseUrl}/${id}`);
  }

  /** HR, or the employee themselves. Prefer `getMine()` for self-service screens. */
  getByEmployee(employeeId: string): Promise<EmployeeOrientationSummary[]> {
    return apiService.get<EmployeeOrientationSummary[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  /** Enrollments for the signed-in employee — no id to pass and nothing to leak. */
  getMine(): Promise<EmployeeOrientationSummary[]> {
    return apiService.get<EmployeeOrientationSummary[]>(`${this.baseUrl}/mine`);
  }

  getByProgram(programId: string): Promise<EmployeeOrientationSummary[]> {
    return apiService.get<EmployeeOrientationSummary[]>(`${this.baseUrl}/program/${programId}`);
  }

  getPagedByProgram(
    programId: string,
    pageNumber = 1,
    pageSize = 20,
  ): Promise<PagedResult<EmployeeOrientationSummary>> {
    return apiService.get<PagedResult<EmployeeOrientationSummary>>(
      `${this.baseUrl}/program/${programId}/paged`,
      { pageNumber, pageSize },
    );
  }

  getBySession(sessionId: string): Promise<EmployeeOrientationSummary[]> {
    return apiService.get<EmployeeOrientationSummary[]>(`${this.baseUrl}/session/${sessionId}`);
  }

  getByCompletionStatus(status: OrientationCompletionStatus): Promise<EmployeeOrientationSummary[]> {
    return apiService.get<EmployeeOrientationSummary[]>(`${this.baseUrl}/completion-status/${status}`);
  }

  getOverdue(): Promise<EmployeeOrientationSummary[]> {
    return apiService.get<EmployeeOrientationSummary[]>(`${this.baseUrl}/overdue`);
  }

  getDueSoon(daysAhead = 7): Promise<EmployeeOrientationSummary[]> {
    return apiService.get<EmployeeOrientationSummary[]>(`${this.baseUrl}/due-soon`, { daysAhead });
  }

  // ── Enrollment (HR) ───────────────────────────────────────────────────────

  /**
   * Refused with 422 if the employee already has a non-deleted enrollment on this program, or if the
   * chosen session is full and does not allow a waitlist. A full session that does allow one returns
   * the enrollment as `Waitlisted` with a `waitlistPosition` — check the status, do not assume
   * `Confirmed`.
   */
  enroll(data: EmployeeOrientationCreateRequest): Promise<EmployeeOrientation> {
    return apiService.post<EmployeeOrientation>(this.baseUrl, data);
  }

  /** Silently skips employees already enrolled — compare the returned length against the input. */
  bulkEnroll(data: BulkEnrollOrientationRequest): Promise<EmployeeOrientation[]> {
    return apiService.post<EmployeeOrientation[]>(`${this.baseUrl}/bulk-enroll`, data);
  }

  update(id: string, data: EmployeeOrientationUpdateRequest): Promise<EmployeeOrientation> {
    return apiService.put<EmployeeOrientation>(`${this.baseUrl}/${id}`, data);
  }

  withdraw(id: string, data: WithdrawOrientationRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/withdraw`, data);
  }

  /**
   * Round 4, lane R — HR's "Mark completed" on an enrolment whose programme is only its live
   * session. Refused (422) on any other programme, on an enrolment HR ended, and without a note.
   * The completion rule still runs: a declaration the programme requires is still to be signed.
   */
  confirmAttendance(id: string, data: ConfirmOrientationAttendanceRequest): Promise<EmployeeOrientation> {
    return apiService.post<EmployeeOrientation>(`${this.baseUrl}/${id}/confirm-attendance`, data);
  }

  /** Round 4, lane R — what marking the session Completed would do, for its confirmation to say. */
  getSessionCompletionPreview(sessionId: string): Promise<OrientationSessionCompletionPreview> {
    return apiService.get<OrientationSessionCompletionPreview>(`${this.baseUrl}/session/${sessionId}/completion-preview`);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Content progress (participant) ────────────────────────────────────────

  /**
   * The programme's live modules and content items, as the participant may see them.
   *
   * This is the participant's only route to the catalogue: `orientationProgramService` is HR-only, and
   * a progress row does not exist until its item has been tracked — so the player builds its list from
   * here and layers `getContentProgress` on top, rather than the other way round.
   */
  getProgramContent(enrollmentId: string): Promise<OrientationModule[]> {
    return apiService.get<OrientationModule[]>(`${this.baseUrl}/${enrollmentId}/content`);
  }

  getContentProgress(enrollmentId: string): Promise<OrientationContentProgress[]> {
    return apiService.get<OrientationContentProgress[]>(
      `${this.baseUrl}/${enrollmentId}/content-progress`,
    );
  }

  /**
   * Records time on a content item and optionally completes it. Completing the last required item is
   * what finishes a program with no assessment, so the enrollment's completionStatus can change as a
   * result of this call — refetch the enrollment rather than assuming it is still InProgress.
   */
  trackContentProgress(
    enrollmentId: string,
    data: TrackOrientationContentProgressRequest,
  ): Promise<OrientationContentProgress> {
    return apiService.post<OrientationContentProgress>(
      `${this.baseUrl}/${enrollmentId}/content-progress`,
      data,
    );
  }

  // ── Assessment (participant) ──────────────────────────────────────────────

  /**
   * The paper as the participant sees it: `isCorrect` is forced false on every option and
   * `explanation` withheld until the attempt has been graded. Use this — never the authoring read
   * on `orientationProgramService.getQuestions`, which carries the answer key.
   */
  getAssessment(enrollmentId: string): Promise<OrientationAssessmentQuestion[]> {
    return apiService.get<OrientationAssessmentQuestion[]>(
      `${this.baseUrl}/${enrollmentId}/assessment`,
    );
  }

  /**
   * Scored against every gradable question on the paper, not just the ones answered — omitting a
   * question costs its marks. Replaces any previous attempt's responses.
   */
  submitAssessment(
    enrollmentId: string,
    data: SubmitOrientationAssessmentRequest,
  ): Promise<OrientationAssessmentResult> {
    return apiService.post<OrientationAssessmentResult>(
      `${this.baseUrl}/${enrollmentId}/assessment/submit`,
      data,
    );
  }

  getAssessmentResponses(enrollmentId: string): Promise<OrientationAssessmentResponse[]> {
    return apiService.get<OrientationAssessmentResponse[]>(
      `${this.baseUrl}/${enrollmentId}/assessment/responses`,
    );
  }

  // ── Acknowledgements ──────────────────────────────────────────────────────

  getAcknowledgements(enrollmentId: string): Promise<OrientationAcknowledgement[]> {
    return apiService.get<OrientationAcknowledgement[]>(
      `${this.baseUrl}/${enrollmentId}/acknowledgements`,
    );
  }

  /** HR authors the declaration; the participant signs it. */
  addAcknowledgement(
    enrollmentId: string,
    data: OrientationAcknowledgementCreateRequest,
  ): Promise<OrientationAcknowledgement> {
    return apiService.post<OrientationAcknowledgement>(
      `${this.baseUrl}/${enrollmentId}/acknowledgements`,
      data,
    );
  }

  /**
   * Signing is personal — the server refuses an acknowledgement belonging to anyone but the caller
   * (or HR), and records the signer's IP and a tamper hash. Signing the last outstanding requirement
   * can complete the enrollment.
   */
  signAcknowledgement(
    data: SignOrientationAcknowledgementRequest,
  ): Promise<OrientationAcknowledgement> {
    return apiService.post<OrientationAcknowledgement>(`${this.baseUrl}/acknowledgements/sign`, data);
  }

  // ── Feedback ──────────────────────────────────────────────────────────────

  getFeedback(enrollmentId: string): Promise<OrientationFeedback[]> {
    return apiService.get<OrientationFeedback[]>(`${this.baseUrl}/${enrollmentId}/feedback`);
  }

  submitFeedback(
    enrollmentId: string,
    data: OrientationFeedbackCreateRequest,
  ): Promise<OrientationFeedback> {
    return apiService.post<OrientationFeedback>(`${this.baseUrl}/${enrollmentId}/feedback`, data);
  }

  /**
   * The caller's own feedback, across every orientation they have had.
   *
   * Lane 6: the read the portal form needed. Without it a submission was write-only and the page
   * could only say "sending again adds another response" -- which the server now refuses anyway.
   * Own rows are never withheld, anonymous or not: the author always sees their own name.
   */
  getMyFeedback(): Promise<OrientationFeedback[]> {
    return apiService.get<OrientationFeedback[]>(`${this.baseUrl}/feedback/mine`);
  }

  // ── Certificates ──────────────────────────────────────────────────────────

  getCertificatesForEnrollment(enrollmentId: string): Promise<OrientationCertificate[]> {
    return apiService.get<OrientationCertificate[]>(`${this.baseUrl}/${enrollmentId}/certificates`);
  }

  /** HR, or the holder themselves. */
  getCertificatesForEmployee(employeeId: string): Promise<OrientationCertificate[]> {
    return apiService.get<OrientationCertificate[]>(
      `${this.baseUrl}/employee/${employeeId}/certificates`,
    );
  }

  /** HR-only — serials are sequential, so this lookup is not a public verification endpoint. */
  getCertificateByNumber(certificateNumber: string): Promise<OrientationCertificate | null> {
    return apiService.get<OrientationCertificate | null>(
      `${this.baseUrl}/certificates/number/${encodeURIComponent(certificateNumber)}`,
    );
  }

  getExpiringCertificates(daysAhead = 30): Promise<OrientationCertificate[]> {
    return apiService.get<OrientationCertificate[]>(`${this.baseUrl}/certificates/expiring`, {
      daysAhead,
    });
  }

  /**
   * Omit `certificateNumber` to have the server issue the next serial. Expiry defaults from the
   * program's certificateValidityMonths when not supplied.
   */
  issueCertificate(
    enrollmentId: string,
    data: IssueOrientationCertificateRequest,
  ): Promise<OrientationCertificate> {
    return apiService.post<OrientationCertificate>(
      `${this.baseUrl}/${enrollmentId}/certificates`,
      data,
    );
  }

  /** Falls the enrollment back to any other active certificate rather than clearing the flag blindly. */
  revokeCertificate(data: RevokeOrientationCertificateRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/certificates/revoke`, data);
  }
}

export const employeeOrientationService = new EmployeeOrientationService();
export default employeeOrientationService;
