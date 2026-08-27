import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type {
  MyJobApplication,
  AggregatedReviewScore,
  ApplicantCommunication,
  ApplicantTestResult,
  ApplicationAutoScore,
  ApplicationStageHistory,
  ApplicationStatus,
  BlindApplicationSummary,
  BulkOperationResult,
  CandidateComparison,
  CandidateDocument,
  CandidateDocumentType,
  CandidateInterest,
  CandidateNote,
  CandidateNoteForm,
  CandidateQualification,
  CandidateQualificationForm,
  CandidateReferee,
  CandidateRefereeForm,
  CandidateSkill,
  CandidateSkillForm,
  CandidateWorkHistory,
  CandidateWorkHistoryForm,
  CreateApplicantCommunication,
  CreateApplicantTestResult,
  CreateJobApplication,
  CreateJobCandidate,
  CreateRecruitmentPipeline,
  CreateRecruitmentPipelineStage,
  EeoReport,
  HrPagedResult,
  InternalApplyForVacancy,
  InternalSaveDraft,
  InternalSubmitDraft,
  JobApplication,
  JobApplicationDetail,
  JobApplicationSummary,
  JobCandidate,
  JobCandidateDetail,
  JobCandidateSummary,
  PipelineApplicationListItem,
  PipelineColumn,
  PipelineOverview,
  RecruitmentPipeline,
  RecruitmentPipelineStage,
  RecruitmentPipelineSummary,
  ScoringRunResult,
  ShortlistDecisionLogEntry,
  ShortlistReview,
  ShortlistSlaStatus,
  ShortlistSummary,
  StageApplicationsQuery,
  UpdateApplicantTestResult,
  UpdateJobCandidate,
  UpdateRecruitmentPipeline,
  UpdateRecruitmentPipelineStage,
} from '@/types/hr/recruitment-pipeline';

/**
 * api/recruitment-pipelines — the stage definitions applications progress through.
 *
 * Reads are open to the tenant (every board column and stage-history row resolves a stage name);
 * every write is HR's, because a stage's `order`, `canRepeat` and `maxAttempts` are the transition
 * rules the server enforces on every move.
 */
class RecruitmentPipelineService {
  private readonly baseUrl = '/recruitment-pipelines';

  getAll(): Promise<RecruitmentPipelineSummary[]> {
    return apiService.get<RecruitmentPipelineSummary[]>(`${this.baseUrl}/all`);
  }

  getById(id: string): Promise<RecruitmentPipeline> {
    return apiService.get<RecruitmentPipeline>(`${this.baseUrl}/${id}`);
  }

  getDefault(): Promise<RecruitmentPipeline | null> {
    return apiService.get<RecruitmentPipeline | null>(`${this.baseUrl}/default`);
  }

  create(payload: CreateRecruitmentPipeline): Promise<RecruitmentPipeline> {
    return apiService.post<RecruitmentPipeline>(this.baseUrl, payload);
  }

  /** Deep-copies the pipeline and all its stages under a new name — the safe way to edit a live one. */
  clone(id: string, newName: string): Promise<RecruitmentPipeline> {
    return apiService.post<RecruitmentPipeline>(`${this.baseUrl}/${id}/clone`, { newName });
  }

  update(id: string, payload: UpdateRecruitmentPipeline): Promise<RecruitmentPipeline> {
    return apiService.put<RecruitmentPipeline>(`${this.baseUrl}/${id}`, { ...payload, id });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── stages ───────────────────────────────────────────────────────────────

  getStages(pipelineId: string): Promise<RecruitmentPipelineStage[]> {
    return apiService.get<RecruitmentPipelineStage[]>(`${this.baseUrl}/${pipelineId}/stages`);
  }

  addStage(pipelineId: string, payload: CreateRecruitmentPipelineStage): Promise<RecruitmentPipelineStage> {
    return apiService.post<RecruitmentPipelineStage>(`${this.baseUrl}/${pipelineId}/stages`, {
      ...payload,
      recruitmentPipelineId: pipelineId,
    });
  }

  updateStage(stageId: string, payload: UpdateRecruitmentPipelineStage): Promise<RecruitmentPipelineStage> {
    return apiService.put<RecruitmentPipelineStage>(`${this.baseUrl}/stages/${stageId}`, { ...payload, id: stageId });
  }

  deleteStage(stageId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/stages/${stageId}`);
  }

  reorderStages(pipelineId: string, orders: { stageId: string; newOrder: number }[]): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${pipelineId}/stages/reorder`, orders);
  }
}

/**
 * api/job-candidates — the person behind an application.
 *
 * ⚠ **HR-only, in full, including reads.** These records hold date of birth, contact details, CVs,
 * referees and recruiters' private notes.
 *
 * ⚠ The professional-profile fields on `JobCandidate` (headline, current role, expected salary,
 * notice period, work authorization) are **not** on the create/update payloads — they come from the
 * candidate portal. Saving from HR leaves them untouched; do not build inputs for them.
 */
class JobCandidateService {
  private readonly baseUrl = '/job-candidates';

  getPaged(pageNumber = 1, pageSize = 20): Promise<HrPagedResult<JobCandidateSummary>> {
    return apiService.get<HrPagedResult<JobCandidateSummary>>(this.baseUrl, { pageNumber, pageSize });
  }

  getAll(): Promise<JobCandidateSummary[]> {
    return apiService.get<JobCandidateSummary[]>(`${this.baseUrl}/all`);
  }

  getById(id: string): Promise<JobCandidate> {
    return apiService.get<JobCandidate>(`${this.baseUrl}/${id}`);
  }

  getDetail(id: string): Promise<JobCandidateDetail> {
    return apiService.get<JobCandidateDetail>(`${this.baseUrl}/${id}/details`);
  }

  getTalentPool(): Promise<JobCandidateSummary[]> {
    return apiService.get<JobCandidateSummary[]>(`${this.baseUrl}/talent-pool`);
  }

  getByVacancy(vacancyId: string): Promise<JobCandidateSummary[]> {
    return apiService.get<JobCandidateSummary[]>(`${this.baseUrl}/vacancy/${vacancyId}`);
  }

  /** Returns null rather than 404 when nobody matches — used to spot a duplicate before creating. */
  getByEmail(email: string): Promise<JobCandidate | null> {
    return apiService.get<JobCandidate | null>(`${this.baseUrl}/email/${encodeURIComponent(email)}`);
  }

  create(payload: CreateJobCandidate): Promise<JobCandidate> {
    return apiService.post<JobCandidate>(this.baseUrl, payload);
  }

  update(id: string, payload: UpdateJobCandidate): Promise<JobCandidate> {
    return apiService.put<JobCandidate>(`${this.baseUrl}/${id}`, { ...payload, id });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  addToTalentPool(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/add-to-talent-pool`, {});
  }

  removeFromTalentPool(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/remove-from-talent-pool`, {});
  }

  // ── files ────────────────────────────────────────────────────────────────
  // Candidate files live in private storage with no public URL — always fetched as a blob.

  downloadCv(id: string): Promise<Blob> {
    return apiService.downloadBlob(`${this.baseUrl}/${id}/cv`);
  }

  downloadPhoto(id: string): Promise<Blob> {
    return apiService.downloadBlob(`${this.baseUrl}/${id}/photo`);
  }

  // ── sub-resources ────────────────────────────────────────────────────────
  // Each create posts to the candidate-scoped route; the server takes the candidate from the route
  // and ignores any id in the body, so there is no need to send one.

  getQualifications(candidateId: string): Promise<CandidateQualification[]> {
    return apiService.get<CandidateQualification[]>(`${this.baseUrl}/${candidateId}/qualifications`);
  }

  addQualification(candidateId: string, payload: CandidateQualificationForm): Promise<CandidateQualification> {
    return apiService.post<CandidateQualification>(`${this.baseUrl}/${candidateId}/qualifications`, payload);
  }

  updateQualification(
    candidateId: string,
    qualificationId: string,
    payload: CandidateQualificationForm,
  ): Promise<CandidateQualification> {
    return apiService.put<CandidateQualification>(
      `${this.baseUrl}/${candidateId}/qualifications/${qualificationId}`,
      { ...payload, id: qualificationId },
    );
  }

  deleteQualification(qualificationId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/qualifications/${qualificationId}`);
  }

  getWorkHistory(candidateId: string): Promise<CandidateWorkHistory[]> {
    return apiService.get<CandidateWorkHistory[]>(`${this.baseUrl}/${candidateId}/work-history`);
  }

  addWorkHistory(candidateId: string, payload: CandidateWorkHistoryForm): Promise<CandidateWorkHistory> {
    return apiService.post<CandidateWorkHistory>(`${this.baseUrl}/${candidateId}/work-history`, payload);
  }

  updateWorkHistory(
    candidateId: string,
    workHistoryId: string,
    payload: CandidateWorkHistoryForm,
  ): Promise<CandidateWorkHistory> {
    return apiService.put<CandidateWorkHistory>(
      `${this.baseUrl}/${candidateId}/work-history/${workHistoryId}`,
      { ...payload, id: workHistoryId },
    );
  }

  deleteWorkHistory(workHistoryId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/work-history/${workHistoryId}`);
  }

  getReferees(candidateId: string): Promise<CandidateReferee[]> {
    return apiService.get<CandidateReferee[]>(`${this.baseUrl}/${candidateId}/referees`);
  }

  addReferee(candidateId: string, payload: CandidateRefereeForm): Promise<CandidateReferee> {
    return apiService.post<CandidateReferee>(`${this.baseUrl}/${candidateId}/referees`, payload);
  }

  updateReferee(candidateId: string, refereeId: string, payload: CandidateRefereeForm): Promise<CandidateReferee> {
    return apiService.put<CandidateReferee>(`${this.baseUrl}/${candidateId}/referees/${refereeId}`, {
      ...payload,
      id: refereeId,
    });
  }

  deleteReferee(refereeId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/referees/${refereeId}`);
  }

  getSkills(candidateId: string): Promise<CandidateSkill[]> {
    return apiService.get<CandidateSkill[]>(`${this.baseUrl}/${candidateId}/skills`);
  }

  addSkill(candidateId: string, payload: CandidateSkillForm): Promise<CandidateSkill> {
    return apiService.post<CandidateSkill>(`${this.baseUrl}/${candidateId}/skills`, payload);
  }

  updateSkill(candidateId: string, skillId: string, payload: CandidateSkillForm): Promise<CandidateSkill> {
    return apiService.put<CandidateSkill>(`${this.baseUrl}/${candidateId}/skills/${skillId}`, {
      ...payload,
      id: skillId,
    });
  }

  deleteSkill(skillId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/skills/${skillId}`);
  }

  getInterests(candidateId: string): Promise<CandidateInterest[]> {
    return apiService.get<CandidateInterest[]>(`${this.baseUrl}/${candidateId}/interests`);
  }

  addInterest(candidateId: string, detail: string): Promise<CandidateInterest> {
    return apiService.post<CandidateInterest>(`${this.baseUrl}/${candidateId}/interests`, { detail });
  }

  deleteInterest(interestId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/interests/${interestId}`);
  }

  // ── documents ────────────────────────────────────────────────────────────
  // ⚠ Multipart through the controlled-upload gate — never a JSON filePath. The old JSON endpoint
  // stored no file and wrote a row that could never be downloaded.

  getDocuments(candidateId: string): Promise<CandidateDocument[]> {
    return apiService.get<CandidateDocument[]>(`${this.baseUrl}/${candidateId}/documents`);
  }

  uploadDocument(
    candidateId: string,
    file: File,
    documentType: CandidateDocumentType,
    description: string | null,
  ): Promise<CandidateDocument> {
    return hrDocumentService.upload<CandidateDocument>(`${this.baseUrl}/${candidateId}/documents`, file, {
      documentType,
      description,
    });
  }

  downloadDocument(candidateId: string, documentId: string): Promise<Blob> {
    return apiService.downloadBlob(`${this.baseUrl}/${candidateId}/documents/${documentId}/download`);
  }

  deleteDocument(documentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/documents/${documentId}`);
  }

  // ── notes ────────────────────────────────────────────────────────────────

  /**
   * `includePrivate` is honoured because this whole controller is HR-only — a private note is hidden
   * from everyone outside HR by the role gate, not by the flag. It is not an owner-only note.
   */
  getNotes(candidateId: string, includePrivate = true): Promise<CandidateNote[]> {
    return apiService.get<CandidateNote[]>(`${this.baseUrl}/${candidateId}/notes`, { includePrivate });
  }

  addNote(candidateId: string, payload: CandidateNoteForm): Promise<CandidateNote> {
    return apiService.post<CandidateNote>(`${this.baseUrl}/${candidateId}/notes`, payload);
  }

  updateNote(candidateId: string, noteId: string, payload: CandidateNoteForm): Promise<CandidateNote> {
    return apiService.put<CandidateNote>(`${this.baseUrl}/${candidateId}/notes/${noteId}`, {
      ...payload,
      id: noteId,
    });
  }

  deleteNote(noteId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/notes/${noteId}`);
  }
}

/**
 * api/job-applications — a candidate against a vacancy, and everything a recruiter does to it.
 *
 * ⚠ **HR-only, including reads.** The one exception on the controller is the internal job board,
 * which is not part of this slice.
 *
 * ⚠ Every action route takes its subject from the URL and overwrites whatever id the body carries —
 * so these methods never need to send an `applicationId`.
 *
 * ⚠ Business rules answer **422 with the rule's own message** via `[RecruitmentBusinessRules]`; a
 * missing record answers 404. Surface `error.message` rather than a generic failure toast — the
 * messages explain a passed shortlisting deadline, an already-shortlisted application, or a stage
 * that has hit its attempt limit.
 */
class JobApplicationService {
  private readonly baseUrl = '/job-applications';

  // ── reads ────────────────────────────────────────────────────────────────

  getPaged(pageNumber = 1, pageSize = 20, vacancyId?: string): Promise<HrPagedResult<JobApplicationSummary>> {
    return apiService.get<HrPagedResult<JobApplicationSummary>>(this.baseUrl, {
      pageNumber,
      pageSize,
      vacancyId: vacancyId ?? undefined,
    });
  }

  getById(id: string): Promise<JobApplication> {
    return apiService.get<JobApplication>(`${this.baseUrl}/${id}`);
  }

  getDetail(id: string): Promise<JobApplicationDetail> {
    return apiService.get<JobApplicationDetail>(`${this.baseUrl}/${id}/details`);
  }

  getByVacancy(vacancyId: string): Promise<JobApplicationSummary[]> {
    return apiService.get<JobApplicationSummary[]>(`${this.baseUrl}/vacancy/${vacancyId}`);
  }

  getByCandidate(candidateId: string): Promise<JobApplicationSummary[]> {
    return apiService.get<JobApplicationSummary[]>(`${this.baseUrl}/candidate/${candidateId}`);
  }

  getByStatus(status: ApplicationStatus, vacancyId?: string): Promise<JobApplicationSummary[]> {
    return apiService.get<JobApplicationSummary[]>(`${this.baseUrl}/status/${status}`, {
      vacancyId: vacancyId ?? undefined,
    });
  }

  getShortlisted(vacancyId: string): Promise<JobApplicationSummary[]> {
    return apiService.get<JobApplicationSummary[]>(`${this.baseUrl}/vacancy/${vacancyId}/shortlisted`);
  }

  create(payload: CreateJobApplication): Promise<JobApplication> {
    return apiService.post<JobApplication>(this.baseUrl, payload);
  }

  /** ⚠ Only Draft and New applications can be deleted — anything further along answers 422. */
  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── decisions ────────────────────────────────────────────────────────────

  shortlist(id: string, shortlistingNotes?: string | null): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/shortlist`, {
      applicationId: id,
      shortlistingNotes: shortlistingNotes ?? null,
    });
  }

  unshortlist(id: string, reason?: string | null): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/unshortlist`, { applicationId: id, reason: reason ?? null });
  }

  waitlist(id: string, waitlistReason?: string | null): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/waitlist`, {
      applicationId: id,
      waitlistReason: waitlistReason ?? null,
    });
  }

  reject(id: string, rejectionReason: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/reject`, { applicationId: id, rejectionReason });
  }

  withdraw(id: string, withdrawalReason: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/withdraw`, { applicationId: id, withdrawalReason });
  }

  /**
   * Identical in effect to `applicationPipelineService.moveStage` — both run the same guarded
   * transition (terminal status, pipeline membership, stage order, CanRepeat, MaxAttempts). Use this
   * one when you have a note to record with the move.
   */
  moveToStage(id: string, pipelineStageId: string, notes?: string | null): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/move-to-stage`, {
      applicationId: id,
      pipelineStageId,
      notes: notes ?? null,
    });
  }

  markInternal(id: string, internalEmployeeId?: string | null): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/mark-internal`, {
      applicationId: id,
      internalEmployeeId: internalEmployeeId ?? null,
    });
  }

  unmarkInternal(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}/mark-internal`);
  }

  // ── history, tests, communications ───────────────────────────────────────

  getStageHistory(id: string): Promise<ApplicationStageHistory[]> {
    return apiService.get<ApplicationStageHistory[]>(`${this.baseUrl}/${id}/stage-history`);
  }

  getDecisionLog(id: string): Promise<ShortlistDecisionLogEntry[]> {
    return apiService.get<ShortlistDecisionLogEntry[]>(`${this.baseUrl}/${id}/decision-log`);
  }

  getTestResults(id: string): Promise<ApplicantTestResult[]> {
    return apiService.get<ApplicantTestResult[]>(`${this.baseUrl}/${id}/test-results`);
  }

  addTestResult(id: string, payload: CreateApplicantTestResult): Promise<ApplicantTestResult> {
    return apiService.post<ApplicantTestResult>(`${this.baseUrl}/${id}/test-results`, payload);
  }

  /** ⚠ Marking only — name, type, date and venue are fixed once the result exists. */
  updateTestResult(testResultId: string, payload: UpdateApplicantTestResult): Promise<ApplicantTestResult> {
    return apiService.put<ApplicantTestResult>(`${this.baseUrl}/test-results/${testResultId}`, {
      ...payload,
      id: testResultId,
    });
  }

  deleteTestResult(testResultId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/test-results/${testResultId}`);
  }

  getCommunications(id: string): Promise<ApplicantCommunication[]> {
    return apiService.get<ApplicantCommunication[]>(`${this.baseUrl}/${id}/communications`);
  }

  /**
   * Records a communication — it does **not** send one. The system writes its own rows when an
   * application is shortlisted or rejected; this is for logging a call or a letter sent by hand.
   */
  addCommunication(id: string, payload: CreateApplicantCommunication): Promise<ApplicantCommunication> {
    return apiService.post<ApplicantCommunication>(`${this.baseUrl}/${id}/communications`, payload);
  }

  // ── panel reviews ────────────────────────────────────────────────────────

  getAggregatedReview(id: string): Promise<AggregatedReviewScore> {
    return apiService.get<AggregatedReviewScore>(`${this.baseUrl}/${id}/reviews/aggregated`);
  }

  addReview(id: string, score: number, notes?: string | null): Promise<ShortlistReview> {
    return apiService.post<ShortlistReview>(`${this.baseUrl}/${id}/reviews`, {
      applicationId: id,
      score,
      notes: notes ?? null,
    });
  }

  /** ⚠ Only the review's own author may finalize it — anyone else gets 403. */
  finalizeReview(reviewId: string): Promise<ShortlistReview> {
    return apiService.post<ShortlistReview>(`${this.baseUrl}/reviews/${reviewId}/finalize`, {});
  }

  // ── scoring ──────────────────────────────────────────────────────────────

  score(id: string): Promise<ApplicationAutoScore> {
    return apiService.post<ApplicationAutoScore>(`${this.baseUrl}/${id}/score`, {});
  }

  scoreAll(vacancyId: string): Promise<ApplicationAutoScore[]> {
    return apiService.post<ApplicationAutoScore[]>(`${this.baseUrl}/vacancy/${vacancyId}/score-all`, {});
  }

  // ── shortlisting: bulk and automatic ─────────────────────────────────────
  // ⚠ Partial by design. Read `succeeded`/`skipped` and the per-item messages — an application that
  // is already shortlisted, past the deadline, or on another vacancy is skipped, not fatal.

  bulkShortlist(vacancyId: string, applicationIds: string[], shortlistingNotes?: string | null): Promise<BulkOperationResult> {
    return apiService.post<BulkOperationResult>(`${this.baseUrl}/vacancy/${vacancyId}/bulk-shortlist`, {
      applicationIds,
      shortlistingNotes: shortlistingNotes ?? null,
    });
  }

  bulkReject(vacancyId: string, applicationIds: string[], rejectionReason: string): Promise<BulkOperationResult> {
    return apiService.post<BulkOperationResult>(`${this.baseUrl}/vacancy/${vacancyId}/bulk-reject`, {
      applicationIds,
      rejectionReason,
    });
  }

  /**
   * Shortlists everything at or above `minScore`. Only counts applications with a **fresh** score —
   * a stale one is skipped, which is why the screen should offer a re-score first.
   */
  autoShortlist(
    vacancyId: string,
    payload: { minScore: number; requireAllMandatoryPassed: boolean; shortlistingNotes?: string | null },
  ): Promise<BulkOperationResult> {
    return apiService.post<BulkOperationResult>(`${this.baseUrl}/vacancy/${vacancyId}/auto-shortlist`, {
      ...payload,
      vacancyId,
      shortlistingNotes: payload.shortlistingNotes ?? null,
    });
  }

  /** Emails everyone shortlisted who has not already been told. Safe to re-run. */
  sendShortlistNotifications(vacancyId: string): Promise<BulkOperationResult> {
    return apiService.post<BulkOperationResult>(`${this.baseUrl}/vacancy/${vacancyId}/shortlist/send-notifications`, {});
  }

  sendRejectionNotifications(vacancyId: string): Promise<BulkOperationResult> {
    return apiService.post<BulkOperationResult>(`${this.baseUrl}/vacancy/${vacancyId}/rejections/send-notifications`, {});
  }

  // ── shortlist dashboard, comparison, screening ───────────────────────────

  getShortlistSummary(vacancyId: string): Promise<ShortlistSummary> {
    return apiService.get<ShortlistSummary>(`${this.baseUrl}/vacancy/${vacancyId}/shortlist/summary`);
  }

  /** With no ids, compares everyone currently shortlisted. */
  getComparison(vacancyId: string, applicationIds: string[] = []): Promise<CandidateComparison> {
    const query = applicationIds.map((id) => `applicationIds=${encodeURIComponent(id)}`).join('&');
    return apiService.get<CandidateComparison>(
      `${this.baseUrl}/vacancy/${vacancyId}/shortlist/comparison${query ? `?${query}` : ''}`,
    );
  }

  /** ⚠ 422 unless the vacancy has `isBlindScreeningEnabled` set. */
  getBlindApplications(vacancyId: string): Promise<BlindApplicationSummary[]> {
    return apiService.get<BlindApplicationSummary[]>(`${this.baseUrl}/vacancy/${vacancyId}/blind-applications`);
  }

  getSla(vacancyId: string): Promise<ShortlistSlaStatus> {
    return apiService.get<ShortlistSlaStatus>(`${this.baseUrl}/vacancy/${vacancyId}/shortlist/sla`);
  }

  getEeoReport(vacancyId: string): Promise<EeoReport> {
    return apiService.get<EeoReport>(`${this.baseUrl}/vacancy/${vacancyId}/eeo-report`);
  }

  exportShortlistCsv(vacancyId: string): Promise<Blob> {
    return apiService.downloadBlob(`${this.baseUrl}/vacancy/${vacancyId}/shortlist/export`);
  }

  // ── shortlist approval ───────────────────────────────────────────────────
  // Not on the workflow engine — a flag on the vacancy with several writers. Shortlisting,
  // un-shortlisting and auto-shortlisting all reset an in-flight decision back to NotSubmitted, so
  // refetch the summary after any of them.

  /** ⚠ 422 when nothing is shortlisted yet — there would be nothing to approve. */
  submitShortlistForApproval(vacancyId: string, notes?: string | null): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/vacancy/${vacancyId}/shortlist/submit-approval`, {
      vacancyId,
      notes: notes ?? null,
    });
  }

  /** ⚠ 422 for the person who submitted it — the submitter cannot approve their own shortlist. */
  reviewShortlistApproval(vacancyId: string, approved: boolean, notes?: string | null): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/vacancy/${vacancyId}/shortlist/review-approval`, {
      vacancyId,
      approved,
      notes: notes ?? null,
    });
  }

  recallShortlistApproval(vacancyId: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/vacancy/${vacancyId}/shortlist/recall-approval`, {});
  }

  // ── internal job board ────────────────────────────────────────────────────
  // Open to any authenticated employee — see the class doc comment on the controller side.

  applyInternal(payload: InternalApplyForVacancy): Promise<JobApplication> {
    return apiService.post<JobApplication>(`${this.baseUrl}/apply-internal`, payload);
  }

  /**
   * The caller's own applications.
   *
   * ⚠ Returns `MyJobApplication[]`, NOT `JobApplicationSummary[]`. The recruiter's summary carries
   * `autoScore`, `scoredAt`, `scoreIsStale`, `aggregatedReviewScore` (the panel's verdict on
   * them), `snapshotAvailable`, `jobCandidateId` and the pipeline stage — the assessment, not the
   * answer. Leaning only the single read and leaving this list alone would have moved the leak
   * rather than closed it.
   */
  getMyApplications(): Promise<MyJobApplication[]> {
    return apiService.get<MyJobApplication[]>(`${this.baseUrl}/my-applications`);
  }

  saveInternalDraft(payload: InternalSaveDraft): Promise<JobApplication> {
    return apiService.post<JobApplication>(`${this.baseUrl}/internal/draft`, payload);
  }

  submitInternalDraft(id: string, payload: InternalSubmitDraft): Promise<JobApplication> {
    return apiService.put<JobApplication>(`${this.baseUrl}/internal/${id}/submit`, payload);
  }

  /**
   * One of the caller's own applications.
   *
   * ⚠ Returns `MyJobApplication`, NOT `JobApplicationDetail`. The recruiter's detail carries the
   * auto-score and its criterion breakdown, the shortlisting notes, the names of whoever
   * shortlisted or rejected them, the communication log and the test results — the assessment,
   * not the answer. Somebody else's id is a 404, never a 403.
   */
  getMyApplication(id: string): Promise<MyJobApplication> {
    return apiService.get<MyJobApplication>(`${this.baseUrl}/my-applications/${id}`);
  }

  /**
   * Withdraws the caller's own application. The reason is optional — a candidate who no longer
   * wants the job owes no explanation, unlike a recruiter withdrawing on somebody's behalf.
   */
  withdrawMyApplication(id: string, withdrawalReason?: string | null): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(
      `${this.baseUrl}/my-applications/${id}/withdraw`,
      { withdrawalReason: withdrawalReason ?? null },
    );
  }
}

/**
 * api/applications — the pipeline board, its paginated stage lists and its bulk moves.
 *
 * A different controller from `api/job-applications` and a different shape: this one maps its own
 * exceptions, answering **404** for a missing record and **422** for a refused transition.
 */
class ApplicationPipelineService {
  private readonly baseUrl = '/applications';

  /** The full Kanban board. Returns `[]` when the vacancy has no pipeline assigned. */
  getBoard(vacancyId: string): Promise<PipelineColumn[]> {
    return apiService.get<PipelineColumn[]>(`${this.baseUrl}/pipeline/${vacancyId}`);
  }

  /** Counts only — cheap enough to poll for the header bar. Includes the synthetic inbox bucket. */
  getOverview(vacancyId: string): Promise<PipelineOverview> {
    return apiService.get<PipelineOverview>(`${this.baseUrl}/pipeline/${vacancyId}/overview`);
  }

  /** Pass `INBOX_STAGE_ID` for applications not yet placed in any stage. */
  getStageApplications(
    vacancyId: string,
    stageId: string,
    query: StageApplicationsQuery = {},
  ): Promise<HrPagedResult<PipelineApplicationListItem>> {
    return apiService.get<HrPagedResult<PipelineApplicationListItem>>(
      `${this.baseUrl}/pipeline/${vacancyId}/stages/${stageId}/applications`,
      query,
    );
  }

  moveStage(applicationId: string, targetStageId: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/move-stage`, { applicationId, targetStageId });
  }

  bulkMove(applicationIds: string[], targetStageId: string): Promise<BulkOperationResult> {
    return apiService.post<BulkOperationResult>(`${this.baseUrl}/bulk-move`, { applicationIds, targetStageId });
  }

  bulkPipelineReject(applicationIds: string[], rejectionReason: string): Promise<BulkOperationResult> {
    return apiService.post<BulkOperationResult>(`${this.baseUrl}/bulk-pipeline-reject`, {
      applicationIds,
      rejectionReason,
    });
  }

  /** Scores every active application on the vacancy; per-application failures land in `errors`. */
  runScoring(vacancyId: string): Promise<ScoringRunResult> {
    return apiService.post<ScoringRunResult>(`${this.baseUrl}/pipeline/${vacancyId}/run-scoring`, {});
  }
}

export const recruitmentPipelineService = new RecruitmentPipelineService();
export const jobCandidateService = new JobCandidateService();
export const jobApplicationService = new JobApplicationService();
export const applicationPipelineService = new ApplicationPipelineService();
