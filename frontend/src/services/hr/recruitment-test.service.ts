// api/hr/recruitment/tests — HR's side of the recruitment test engine (round 4, lane E).
//
// ⚠ Everything here shows the answers, which is why it is a separate service from the candidate's
// calls in careers.service.ts. Those go to api/candidate and are served by DTOs that do not carry
// isCorrect at all.

import { apiService } from '@/services/api.service';
import type {
  CandidateSitting,
  CreateRecruitmentTestAssignmentPayload,
  CreateRecruitmentTestPayload,
  CreateRecruitmentTestQuestionPayload,
  CreateRecruitmentTestSectionPayload,
  FinaliseSittingPayload,
  GrantExtraAttemptPayload,
  MarkFreeTextAnswerPayload,
  RecordPaperSittingPayload,
  RecruitmentTest,
  RecruitmentTestAssignment,
  RecruitmentTestAssignmentCandidate,
  RecruitmentTestPaper,
  RecruitmentTestPaperRequest,
  RecruitmentTestQuestion,
  RecruitmentTestSection,
  RecruitmentTestSitting,
  SittingFilter,
  UpdateRecruitmentTestPayload,
  UpdateRecruitmentTestQuestionPayload,
  UpdateRecruitmentTestSectionPayload,
} from '@/types/hr/recruitment-tests';

class RecruitmentTestService {
  private readonly baseUrl = '/hr/recruitment/tests';

  // ── papers ───────────────────────────────────────────────────────────────

  getTests(activeOnly?: boolean): Promise<RecruitmentTest[]> {
    return apiService.get<RecruitmentTest[]>(
      this.baseUrl,
      activeOnly === undefined ? undefined : { activeOnly },
    );
  }

  getTest(id: string): Promise<RecruitmentTest> {
    return apiService.get<RecruitmentTest>(`${this.baseUrl}/${id}`);
  }

  /** The paper exactly as a candidate is served it — proof that the answers are not in the payload. */
  preview(id: string): Promise<CandidateSitting> {
    return apiService.get<CandidateSitting>(`${this.baseUrl}/${id}/preview`);
  }

  /**
   * The printed paper — the question paper (blank, or one named paper per candidate an assignment
   * reaches) or the marking key. ⚠ The key reveals every answer.
   */
  getPaper(testId: string, request: RecruitmentTestPaperRequest = {}): Promise<RecruitmentTestPaper> {
    return apiService.get<RecruitmentTestPaper>(`${this.baseUrl}/${testId}/paper`, {
      variant: request.variant ?? 'QuestionPaper',
      ...(request.assignmentId ? { assignmentId: request.assignmentId } : {}),
      // Repeated keys — the shape the API binds a Guid[] from.
      ...(request.applicationIds?.length ? { applicationIds: request.applicationIds } : {}),
    } as Record<string, unknown>);
  }

  createTest(payload: CreateRecruitmentTestPayload): Promise<RecruitmentTest> {
    return apiService.post<RecruitmentTest>(this.baseUrl, payload);
  }

  updateTest(payload: UpdateRecruitmentTestPayload): Promise<RecruitmentTest> {
    return apiService.put<RecruitmentTest>(`${this.baseUrl}/${payload.id}`, payload);
  }

  /** ⚠ 422 with the reason when the paper is not markable — show the message, it names the question. */
  activate(id: string): Promise<RecruitmentTest> {
    return apiService.post<RecruitmentTest>(`${this.baseUrl}/${id}/activate`, {});
  }

  retire(id: string): Promise<RecruitmentTest> {
    return apiService.post<RecruitmentTest>(`${this.baseUrl}/${id}/retire`, {});
  }

  deleteTest(id: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/${id}`);
  }

  // ── sections ─────────────────────────────────────────────────────────────

  addSection(payload: CreateRecruitmentTestSectionPayload): Promise<RecruitmentTestSection> {
    return apiService.post<RecruitmentTestSection>(`${this.baseUrl}/sections`, payload);
  }

  updateSection(payload: UpdateRecruitmentTestSectionPayload): Promise<RecruitmentTestSection> {
    return apiService.put<RecruitmentTestSection>(`${this.baseUrl}/sections/${payload.id}`, payload);
  }

  deleteSection(id: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/sections/${id}`);
  }

  // ── questions ────────────────────────────────────────────────────────────

  addQuestion(payload: CreateRecruitmentTestQuestionPayload): Promise<RecruitmentTestQuestion> {
    return apiService.post<RecruitmentTestQuestion>(`${this.baseUrl}/questions`, payload);
  }

  updateQuestion(payload: UpdateRecruitmentTestQuestionPayload): Promise<RecruitmentTestQuestion> {
    return apiService.put<RecruitmentTestQuestion>(`${this.baseUrl}/questions/${payload.id}`, payload);
  }

  deleteQuestion(id: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/questions/${id}`);
  }

  // ── assignment ───────────────────────────────────────────────────────────

  getAssignments(filter: {
    testId?: string;
    vacancyId?: string;
    applicationId?: string;
  } = {}): Promise<RecruitmentTestAssignment[]> {
    return apiService.get<RecruitmentTestAssignment[]>(
      `${this.baseUrl}/assignments`,
      filter as Record<string, unknown>,
    );
  }

  assign(payload: CreateRecruitmentTestAssignmentPayload): Promise<RecruitmentTestAssignment> {
    return apiService.post<RecruitmentTestAssignment>(`${this.baseUrl}/assignments`, payload);
  }

  deleteAssignment(id: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/assignments/${id}`);
  }

  /** Returns how many invitations went out — not how many candidates the assignment reaches. */
  invite(id: string): Promise<number> {
    return apiService.post<number>(`${this.baseUrl}/assignments/${id}/invite`, {});
  }

  /** Everybody an assignment reaches, and whether a paper sitting can be recorded for each. */
  getAssignmentCandidates(assignmentId: string): Promise<RecruitmentTestAssignmentCandidate[]> {
    return apiService.get<RecruitmentTestAssignmentCandidate[]>(
      `${this.baseUrl}/assignments/${assignmentId}/candidates`,
    );
  }

  grantExtraAttempt(payload: GrantExtraAttemptPayload): Promise<RecruitmentTestAssignment> {
    return apiService.post<RecruitmentTestAssignment>(
      `${this.baseUrl}/assignments/${payload.assignmentId}/extra-attempt`,
      payload,
    );
  }

  // ── sittings and marking ─────────────────────────────────────────────────

  getSittings(filter: SittingFilter = {}): Promise<RecruitmentTestSitting[]> {
    return apiService.get<RecruitmentTestSitting[]>(
      `${this.baseUrl}/sittings`,
      filter as Record<string, unknown>,
    );
  }

  getSitting(id: string): Promise<RecruitmentTestSitting> {
    return apiService.get<RecruitmentTestSitting>(`${this.baseUrl}/sittings/${id}`);
  }

  /**
   * Records a script sat on paper and finalises it in the same call. Closed questions carry what was
   * ticked; written answers carry their marks.
   */
  recordPaperSitting(payload: RecordPaperSittingPayload): Promise<RecruitmentTestSitting> {
    return apiService.post<RecruitmentTestSitting>(`${this.baseUrl}/sittings/paper`, payload);
  }

  markAnswer(payload: MarkFreeTextAnswerPayload): Promise<RecruitmentTestSitting> {
    return apiService.post<RecruitmentTestSitting>(
      `${this.baseUrl}/sittings/answers/${payload.answerId}/mark`,
      payload,
    );
  }

  /**
   * Closes the marking. This is the call that writes the result into the applicant's test ledger
   * and re-scores the application, so the vacancy's test weighting is applied.
   */
  finalise(payload: FinaliseSittingPayload): Promise<RecruitmentTestSitting> {
    return apiService.post<RecruitmentTestSitting>(
      `${this.baseUrl}/sittings/${payload.sittingId}/finalise`,
      payload,
    );
  }
}

export const recruitmentTestService = new RecruitmentTestService();
