import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type {
  ApplyCheckTemplate,
  CheckItemStatus,
  ConfirmHireStart,
  CreateJobHireRecord,
  CreateJobOffer,
  CreateJobOfferBenefit,
  CreatePreEmploymentCheck,
  CreatePreEmploymentCheckItem,
  CreatePreEmploymentCheckTemplate,
  CreatePreEmploymentCheckTemplateItem,
  CreateReferenceCheckResponse,
  IssueJobOffer,
  JobHireRecord,
  JobHireRecordSummary,
  JobHireStatus,
  JobOffer,
  JobOfferBenefit,
  JobOfferNote,
  JobOfferStatus,
  JobOfferSummary,
  OfferLetter,
  PreEmploymentCheck,
  PreEmploymentCheckDetail,
  PreEmploymentCheckItem,
  PreEmploymentCheckStatus,
  PreEmploymentCheckTemplate,
  PreEmploymentCheckTemplateDetail,
  PreEmploymentCheckTemplateItem,
  RecordOfferResponse,
  ReferenceCheckResponse,
  ReviseJobOffer,
  UpdateJobHireRecordStatus,
  UpdateJobOffer,
  UpdateJobOfferBenefit,
  UpdatePreEmploymentCheckItem,
  UpdatePreEmploymentCheckTemplate,
  UpdatePreEmploymentCheckTemplateItem,
  UpdateReferenceCheckResponse,
  PreEmploymentCheckProviderService,
  CreatePreEmploymentCheckProviderServices,
  PreEmploymentCheckType,
} from '@/types/hr/offers';
import type { HrPagedResult } from '@/types/hr/recruitment';

/**
 * api/job-offers — the terms, their approval, the issue to the candidate and the response.
 *
 * **HR-only, reads included** — an offer carries a new hire's salary, bonus and benefits.
 *
 * ⚠ **Approval runs on the generic workflow engine.** `submit` / `approve` / `reject` / `recall` go
 * through it and are **inoperable until a `JobOffer` definition is published** and
 * `POST api/Workflow/entity-types/seed` has been re-run — authority comes from the definition, not
 * from a role. Use `WorkflowApprovalActions` + `useWorkflowRecord`, never a bespoke approval UI.
 * Everything from `Sent` onwards (issue, response, negotiate, revise, revoke) is a direct action:
 * those have several writers, including the candidate through the token flow.
 */
class JobOfferService {
  private readonly baseUrl = '/job-offers';

  // ── queries ──────────────────────────────────────────────────────────────

  /**
   * ⚠ Unpaged and unfiltered — the whole tenant's offers.
   *
   * Prefer {@link getPaged} for anything a person looks at. This was the offers screen's **default**
   * view until 2026-09-15 (G-10.3): opening the screen fetched every offer in the tenant in one
   * response, with no pager, no total and no disclosure — the only recruitment list with no bound
   * at all. Kept for callers that genuinely need every row.
   */
  getAll(): Promise<JobOfferSummary[]> {
    return apiService.get<JobOfferSummary[]>(this.baseUrl);
  }

  /** One page of the tenant's offers, newest first. */
  getPaged(pageNumber = 1, pageSize = 20): Promise<HrPagedResult<JobOfferSummary>> {
    return apiService.get<HrPagedResult<JobOfferSummary>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getById(id: string): Promise<JobOffer> {
    return apiService.get<JobOffer>(`${this.baseUrl}/${id}`);
  }

  /** Adds the benefit lines and the candidate chain; what the detail screen reads. */
  getDetails(id: string): Promise<JobOffer> {
    return apiService.get<JobOffer>(`${this.baseUrl}/${id}/details`);
  }

  getByNumber(offerNumber: string): Promise<JobOffer | null> {
    return apiService.get<JobOffer | null>(`${this.baseUrl}/number/${offerNumber}`);
  }

  getByApplication(applicationId: string): Promise<JobOffer | null> {
    return apiService.get<JobOffer | null>(`${this.baseUrl}/application/${applicationId}`);
  }

  getByStatus(status: JobOfferStatus): Promise<JobOfferSummary[]> {
    return apiService.get<JobOfferSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  /** Offers whose expiry falls inside the window — the chase list. */
  getExpiring(daysAhead = 7): Promise<JobOfferSummary[]> {
    return apiService.get<JobOfferSummary[]>(`${this.baseUrl}/expiring`, { daysAhead });
  }

  getByPreparedBy(employeeId: string): Promise<JobOfferSummary[]> {
    return apiService.get<JobOfferSummary[]>(`${this.baseUrl}/prepared-by/${employeeId}`);
  }

  // ── CRUD ─────────────────────────────────────────────────────────────────

  /**
   * ⚠ Send the negotiated terms **only**. The role snapshot (position, reporting line, grade,
   * employment type, work mode) is taken from the application's vacancy and position, and the DTO
   * refuses any unmapped member with a 400 rather than dropping it silently.
   */
  create(payload: CreateJobOffer): Promise<JobOffer> {
    return apiService.post<JobOffer>(this.baseUrl, payload);
  }

  /** Draft and PendingApproval only. Same refusal on unmapped members as create. */
  update(id: string, payload: UpdateJobOffer): Promise<JobOffer> {
    return apiService.put<JobOffer>(`${this.baseUrl}/${id}`, { ...payload, id });
  }

  /** Draft only. */
  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── approval (workflow engine) ───────────────────────────────────────────

  submit(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/submit-for-approval`, {});
  }

  /** ⚠ The approver and the date come from the token and the server clock — only the comment is ours. */
  approve(id: string, comments: string | null): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/approve`, { offerId: id, comments });
  }

  reject(id: string, rejectionReason: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/reject-approval`, { offerId: id, rejectionReason });
  }

  /** Pulls an offer back out of approval, returning it to Draft. */
  recall(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/recall`, {});
  }

  // ── issue and response ───────────────────────────────────────────────────

  /**
   * Approved offers only. Mints the candidate's single-use response token, emails them the link and
   * advances the application to the Offer pipeline stage.
   */
  issue(id: string, payload: IssueJobOffer): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/issue`, { ...payload, offerId: id });
  }

  /**
   * Records what the candidate said when they said it off-system (a phone call, a letter).
   * ⚠ Sent offers only, and only Accepted / Negotiating / Declined — anything else is a 422.
   */
  recordResponse(id: string, payload: RecordOfferResponse): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/record-response`, { ...payload, offerId: id });
  }

  /**
   * Sent or Negotiating only. Marks the offer conditional and parks it at ConditionallyAccepted
   * until the pre-employment checks clear, which is what moves it to ChecksCleared.
   */
  acceptConditionally(id: string, candidateResponseNotes: string | null): Promise<JobOffer> {
    return apiService.post<JobOffer>(`${this.baseUrl}/${id}/accept-conditionally`, candidateResponseNotes);
  }

  /** Refused once an offer is Withdrawn, Declined or Expired — there is nothing left to withdraw. */
  revoke(id: string, revocationReason: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/revoke`, { offerId: id, revocationReason });
  }

  /**
   * Answers a counter-offer: supersedes the original and returns a **new Draft offer** at
   * `version + 1`, carrying the benefits forward. Sent or Negotiating only, and the revised salary
   * is re-checked against the grade band — this is the one path whose whole purpose is changing the
   * money, and it was the one path that never checked.
   */
  revise(id: string, payload: ReviseJobOffer): Promise<JobOffer> {
    return apiService.post<JobOffer>(`${this.baseUrl}/${id}/revise`, { ...payload, originalOfferId: id });
  }

  // ── benefits ─────────────────────────────────────────────────────────────
  //
  // ⚠ These four routes did not exist before this slice. The service methods behind them did, fully
  // implemented and tenant-scoped, with nothing routed to them — so benefits could be seeded from a
  // position grade and then never corrected or removed. Draft/PendingApproval only, all four.

  getBenefits(id: string): Promise<JobOfferBenefit[]> {
    return apiService.get<JobOfferBenefit[]>(`${this.baseUrl}/${id}/benefits`);
  }

  addBenefit(id: string, payload: CreateJobOfferBenefit): Promise<JobOfferBenefit> {
    return apiService.post<JobOfferBenefit>(`${this.baseUrl}/${id}/benefits`, { ...payload, jobOfferId: id });
  }

  updateBenefit(benefitId: string, payload: UpdateJobOfferBenefit): Promise<JobOfferBenefit> {
    return apiService.put<JobOfferBenefit>(`${this.baseUrl}/benefits/${benefitId}`, { ...payload, id: benefitId });
  }

  removeBenefit(benefitId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/benefits/${benefitId}`);
  }

  /** What the position grade would contribute, without writing anything. */
  suggestBenefits(id: string): Promise<JobOfferBenefit[]> {
    return apiService.get<JobOfferBenefit[]>(`${this.baseUrl}/${id}/suggest-benefits`);
  }

  /** Persists the suggestion, skipping names already on the offer. Idempotent. */
  importBenefits(id: string): Promise<JobOfferBenefit[]> {
    return apiService.post<JobOfferBenefit[]>(`${this.baseUrl}/${id}/import-benefits`, {});
  }

  // ── notes ────────────────────────────────────────────────────────────────

  getNotes(id: string): Promise<JobOfferNote[]> {
    return apiService.get<JobOfferNote[]>(`${this.baseUrl}/${id}/notes`);
  }

  addNote(id: string, body: string): Promise<JobOfferNote> {
    return apiService.post<JobOfferNote>(`${this.baseUrl}/${id}/notes`, { jobOfferId: id, body });
  }

  // ── the letter ───────────────────────────────────────────────────────────
  //
  // ⚠ **`letter-preview` and `letter` are two different resources — do not re-merge them.** The
  // preview is rendered on demand from the HR-editable template plus the offer's terms; the download
  // streams the file that was actually uploaded and issued. They were declared on one route and
  // template, which raised AmbiguousMatchException, so BOTH were dead until this slice.

  /** Renders the letter for on-screen preview and print-to-PDF. Nothing is stored. */
  getLetterPreview(id: string): Promise<OfferLetter> {
    return apiService.get<OfferLetter>(`${this.baseUrl}/${id}/letter-preview`);
  }

  uploadLetter(id: string, file: File): Promise<{ downloadUrl: string }> {
    return hrDocumentService.upload<{ downloadUrl: string }>(`${this.baseUrl}/${id}/upload-letter`, file);
  }

  uploadSignedLetter(id: string, file: File): Promise<{ downloadUrl: string }> {
    return hrDocumentService.upload<{ downloadUrl: string }>(`${this.baseUrl}/${id}/upload-signed-letter`, file);
  }

  /** Streams the stored issued letter. 404s until one has been uploaded. */
  downloadLetter(id: string, offerNumber: string): Promise<void> {
    return hrDocumentService.download(`${this.baseUrl}/${id}/letter`, `offer-${offerNumber}.pdf`);
  }

  downloadSignedLetter(id: string, offerNumber: string): Promise<void> {
    return hrDocumentService.download(`${this.baseUrl}/${id}/signed-letter`, `signed-offer-${offerNumber}.pdf`);
  }
}

/**
 * api/job-hires — the handover from recruitment to employment. **HR-only, reads included.**
 *
 * ⚠ **There is no list-all endpoint.** Only by id / number / application / employee / status, plus
 * `start-approaching`. A register has to be assembled from those, which is why the list screen asks
 * which view you want rather than showing everything.
 *
 * ⚠ `confirmStart` is the consequential one — see its own note.
 */
class JobHireService {
  private readonly baseUrl = '/job-hires';

  getById(id: string): Promise<JobHireRecord> {
    return apiService.get<JobHireRecord>(`${this.baseUrl}/${id}`);
  }

  getByNumber(hireNumber: string): Promise<JobHireRecord | null> {
    return apiService.get<JobHireRecord | null>(`${this.baseUrl}/number/${hireNumber}`);
  }

  getByApplication(applicationId: string): Promise<JobHireRecord | null> {
    return apiService.get<JobHireRecord | null>(`${this.baseUrl}/application/${applicationId}`);
  }

  getByEmployee(employeeId: string): Promise<JobHireRecord | null> {
    return apiService.get<JobHireRecord | null>(`${this.baseUrl}/employee/${employeeId}`);
  }

  getByStatus(status: JobHireStatus): Promise<JobHireRecordSummary[]> {
    return apiService.get<JobHireRecordSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  /** Hires whose expected start falls inside the window — the onboarding runway. */
  getStartApproaching(daysAhead = 14): Promise<JobHireRecordSummary[]> {
    return apiService.get<JobHireRecordSummary[]>(`${this.baseUrl}/start-approaching`, { daysAhead });
  }

  /**
   * One hire per application. Refused unless the offer has been accepted — and for a **conditional**
   * offer, unless the checks have cleared (`ChecksCleared`), not merely that the candidate said yes.
   */
  create(payload: CreateJobHireRecord): Promise<JobHireRecord> {
    return apiService.post<JobHireRecord>(this.baseUrl, payload);
  }

  /**
   * ⚠ **`Active` is refused here** — a hire becomes Active by confirming its start, which is what
   * creates the employee. Setting it directly used to be a one-way door that permanently blocked
   * confirm-start. Terminal states cannot be edited either.
   */
  updateStatus(id: string, payload: UpdateJobHireRecordStatus): Promise<JobHireRecord> {
    return apiService.put<JobHireRecord>(`${this.baseUrl}/${id}/status`, { ...payload, hireRecordId: id });
  }

  /**
   * ⚠ **The consequential one.** Creates the `Employee` plus their contract, probation period, salary
   * assignment, position history, qualifications, work history, referees and skills — and burns an
   * employee number. It is idempotent by design (refused once the hire is linked to an employee), so
   * a retry is safe, but there is no undo.
   *
   * Pass `linkedEmployeeId` for an internal hire or a manual link; omit it and the server creates
   * the employee record from the candidate's profile.
   */
  confirmStart(id: string, payload: ConfirmHireStart): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/confirm-start`, payload);
  }
}

/**
 * api/pre-employment-checks — medical, police clearance, background, academic and professional
 * verification, references, credit and drug testing against a conditional offer.
 *
 * **HR-only, reads included, and the most sensitive data in the module**: criminal-record results,
 * medical outcomes and referees' candid opinions about a named person.
 *
 * ⚠ Completing a check is gated on mandatory and blocking items having a recorded outcome. Waived
 * and not-applicable items count as **settled, not failed** — and a completion that finds a real
 * blocking failure lands on `Failed`, which is terminal with no reopen. A clean completion is what
 * advances a `ConditionallyAccepted` offer to `ChecksCleared`.
 */
class PreEmploymentCheckService {
  private readonly baseUrl = '/pre-employment-checks';

  // ── providers (round 3, lane G; D-14): which suppliers provide which checks ──

  getProviders(checkType?: PreEmploymentCheckType, includeInactive = false): Promise<PreEmploymentCheckProviderService[]> {
    return apiService.get<PreEmploymentCheckProviderService[]>(`${this.baseUrl}/providers`, {
      ...(checkType ? { checkType } : {}),
      ...(includeInactive ? { includeInactive: true } : {}),
    });
  }

  addProviders(payload: CreatePreEmploymentCheckProviderServices): Promise<PreEmploymentCheckProviderService[]> {
    return apiService.post<PreEmploymentCheckProviderService[]>(`${this.baseUrl}/providers`, payload);
  }

  removeProvider(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/providers/${id}`);
  }

  getById(id: string): Promise<PreEmploymentCheck> {
    return apiService.get<PreEmploymentCheck>(`${this.baseUrl}/${id}`);
  }

  /** The check raised against one offer, or null if none has been. */
  getByOffer(offerId: string): Promise<PreEmploymentCheck | null> {
    return apiService.get<PreEmploymentCheck | null>(`${this.baseUrl}/offer/${offerId}`);
  }

  getWithItems(id: string): Promise<PreEmploymentCheckDetail> {
    return apiService.get<PreEmploymentCheckDetail>(`${this.baseUrl}/${id}/with-items`);
  }

  /** Backs the cross-offer queue — everything outstanding, across every candidate. */
  getByStatus(status: PreEmploymentCheckStatus): Promise<PreEmploymentCheck[]> {
    return apiService.get<PreEmploymentCheck[]>(`${this.baseUrl}/status/${status}`);
  }

  create(payload: CreatePreEmploymentCheck): Promise<PreEmploymentCheck> {
    return apiService.post<PreEmploymentCheck>(this.baseUrl, payload);
  }

  /** ⚠ Refused while any mandatory or blocking item is still Pending or Requested. */
  complete(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/complete`, {});
  }

  /** Seeds items from a template. `overwriteExisting` replaces same-type items rather than skipping. */
  applyTemplate(id: string, payload: ApplyCheckTemplate): Promise<PreEmploymentCheckDetail> {
    return apiService.post<PreEmploymentCheckDetail>(`${this.baseUrl}/${id}/apply-template`, payload);
  }

  // ── items ────────────────────────────────────────────────────────────────

  getItems(checkId: string): Promise<PreEmploymentCheckItem[]> {
    return apiService.get<PreEmploymentCheckItem[]>(`${this.baseUrl}/${checkId}/items`);
  }

  getBlockingFailures(checkId: string): Promise<PreEmploymentCheckItem[]> {
    return apiService.get<PreEmploymentCheckItem[]>(`${this.baseUrl}/${checkId}/items/blocking-failures`);
  }

  getMandatoryItems(checkId: string): Promise<PreEmploymentCheckItem[]> {
    return apiService.get<PreEmploymentCheckItem[]>(`${this.baseUrl}/${checkId}/items/mandatory`);
  }

  /** Across every check when `checkId` is omitted. */
  getItemsByStatus(status: CheckItemStatus, checkId?: string): Promise<PreEmploymentCheckItem[]> {
    return apiService.get<PreEmploymentCheckItem[]>(
      `${this.baseUrl}/items/status/${status}`,
      checkId ? { checkId } : undefined,
    );
  }

  addItem(checkId: string, payload: CreatePreEmploymentCheckItem): Promise<PreEmploymentCheckItem> {
    return apiService.post<PreEmploymentCheckItem>(`${this.baseUrl}/${checkId}/items`, {
      ...payload,
      preEmploymentCheckId: checkId,
    });
  }

  updateItem(itemId: string, payload: UpdatePreEmploymentCheckItem): Promise<PreEmploymentCheckItem> {
    return apiService.put<PreEmploymentCheckItem>(`${this.baseUrl}/items/${itemId}`, { ...payload, id: itemId });
  }

  removeItem(itemId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/items/${itemId}`);
  }

  // ── reference responses ──────────────────────────────────────────────────

  getReferenceResponses(checkItemId: string): Promise<ReferenceCheckResponse[]> {
    return apiService.get<ReferenceCheckResponse[]>(`${this.baseUrl}/items/${checkItemId}/reference-responses`);
  }

  getReferenceResponsesByReferee(refereeId: string): Promise<ReferenceCheckResponse[]> {
    return apiService.get<ReferenceCheckResponse[]>(`${this.baseUrl}/reference-responses/referee/${refereeId}`);
  }

  addReferenceResponse(
    checkItemId: string,
    payload: CreateReferenceCheckResponse,
  ): Promise<ReferenceCheckResponse> {
    return apiService.post<ReferenceCheckResponse>(`${this.baseUrl}/items/${checkItemId}/reference-responses`, {
      ...payload,
      checkItemId,
    });
  }

  /** ⚠ The referee's identity and contact method are not editable — see the type's note. */
  updateReferenceResponse(id: string, payload: UpdateReferenceCheckResponse): Promise<ReferenceCheckResponse> {
    return apiService.put<ReferenceCheckResponse>(`${this.baseUrl}/reference-responses/${id}`, { ...payload, id });
  }

  removeReferenceResponse(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/reference-responses/${id}`);
  }

  // ── evidence ─────────────────────────────────────────────────────────────
  //
  // ⚠ Through the controlled-upload gate — scan, then DMS registration — into their own storage
  // category, not the one candidate documents use: these are third-party verification results about
  // a named person. It used to be a `documentPath` string on the payload, so a caller named any path
  // they liked and nothing was ever scanned or stored.

  uploadItemDocument(itemId: string, file: File): Promise<unknown> {
    return hrDocumentService.upload<unknown>(`${this.baseUrl}/items/${itemId}/document`, file);
  }

  downloadItemDocument(itemId: string, fileName: string): Promise<void> {
    return hrDocumentService.download(`${this.baseUrl}/items/${itemId}/document`, fileName);
  }

  uploadReferenceDocument(responseId: string, file: File): Promise<unknown> {
    return hrDocumentService.upload<unknown>(`${this.baseUrl}/reference-responses/${responseId}/document`, file);
  }

  downloadReferenceDocument(responseId: string, fileName: string): Promise<void> {
    return hrDocumentService.download(`${this.baseUrl}/reference-responses/${responseId}/document`, fileName);
  }
}

/**
 * api/pre-employment-check-templates — "which checks a hire of this kind needs". Setup data, HR-only.
 *
 * ⚠ Until this slice's backend commit the service behind this controller had **no tenancy at all**:
 * the list returned every tenant's templates and every by-id operation acted on whichever tenant
 * owned that id. Nothing about the client changes as a result, but it is why these reads can now be
 * trusted to be the caller's own.
 */
class PreEmploymentCheckTemplateService {
  private readonly baseUrl = '/pre-employment-check-templates';

  getAll(): Promise<PreEmploymentCheckTemplate[]> {
    return apiService.get<PreEmploymentCheckTemplate[]>(this.baseUrl);
  }

  getById(id: string): Promise<PreEmploymentCheckTemplateDetail> {
    return apiService.get<PreEmploymentCheckTemplateDetail>(`${this.baseUrl}/${id}`);
  }

  /** Items may be supplied inline here; afterwards they are managed one at a time. */
  create(payload: CreatePreEmploymentCheckTemplate): Promise<PreEmploymentCheckTemplateDetail> {
    return apiService.post<PreEmploymentCheckTemplateDetail>(this.baseUrl, payload);
  }

  update(id: string, payload: UpdatePreEmploymentCheckTemplate): Promise<PreEmploymentCheckTemplate> {
    return apiService.put<PreEmploymentCheckTemplate>(`${this.baseUrl}/${id}`, { ...payload, id });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** ⚠ One item per check type — a duplicate is refused with a 422. */
  addItem(
    templateId: string,
    payload: CreatePreEmploymentCheckTemplateItem,
  ): Promise<PreEmploymentCheckTemplateItem> {
    return apiService.post<PreEmploymentCheckTemplateItem>(`${this.baseUrl}/${templateId}/items`, {
      ...payload,
      templateId,
    });
  }

  updateItem(
    templateId: string,
    itemId: string,
    payload: UpdatePreEmploymentCheckTemplateItem,
  ): Promise<PreEmploymentCheckTemplateItem> {
    return apiService.put<PreEmploymentCheckTemplateItem>(`${this.baseUrl}/${templateId}/items/${itemId}`, {
      ...payload,
      id: itemId,
      templateId,
    });
  }

  removeItem(templateId: string, itemId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${templateId}/items/${itemId}`);
  }
}

export const jobOfferService = new JobOfferService();
export const jobHireService = new JobHireService();
export const preEmploymentCheckService = new PreEmploymentCheckService();
export const preEmploymentCheckTemplateService = new PreEmploymentCheckTemplateService();
