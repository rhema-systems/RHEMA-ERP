import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  MedicalExpenseClaim,
  MedicalExpenseClaimSummary,
  MedicalExpenseClaimCreateRequest,
  MedicalExpenseClaimUpdateRequest,
  MedicalExpenseItemUpdateRequest,
  MedicalExpenseItem,
  MedicalExpenseItemCreateRequest,
  MedicalExpenseDocument,
  MedicalExpenseClaimNote,
  MedicalExpenseClaimNoteType,
  MedicalDocumentType,
  ProcessClaimRequest,
  ProcessClaimPaymentRequest,
  ClaimStatus,
  OwnMedicalClaim,
  OwnMedicalClaimSummary,
  NHISClaim,
  NHISClaimSummary,
  NHISClaimCreateRequest,
  NHISClaimUpdateRequest,
  NHISClaimStatus,
  NHISClaimDocument,
  MedicalDashboard,
} from '@/types/hr/medical';

/**
 * The HR medical-claims caseload. Backend route: api/medical-expense-claims.
 *
 * ⚠ This is the ADJUDICATION surface — reading it needs HR.Medical.Read and it lists every
 * employee's claims. Employees file and follow their own claims through
 * {@link medicalSelfServiceClaimService} instead; the two are deliberately separate so that the
 * function raising a claim is not the function approving it.
 */
class MedicalClaimService {
  private readonly baseUrl = '/medical-expense-claims';

  getPaged(
    pageNumber = 1,
    pageSize = 20,
    status?: ClaimStatus,
  ): Promise<PagedResult<MedicalExpenseClaimSummary>> {
    return apiService.get<PagedResult<MedicalExpenseClaimSummary>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
      ...(status ? { status } : {}),
    });
  }

  getPending(): Promise<MedicalExpenseClaimSummary[]> {
    return apiService.get<MedicalExpenseClaimSummary[]>(`${this.baseUrl}/pending`);
  }

  /** Claims someone has marked for a closer look — the fraud/irregularity queue. */
  getFlagged(): Promise<MedicalExpenseClaimSummary[]> {
    return apiService.get<MedicalExpenseClaimSummary[]>(`${this.baseUrl}/flagged`);
  }

  getByEmployee(employeeId: string): Promise<MedicalExpenseClaimSummary[]> {
    return apiService.get<MedicalExpenseClaimSummary[]>(`${this.baseUrl}/employees/${employeeId}`);
  }

  getClaim(id: string): Promise<MedicalExpenseClaim> {
    return apiService.get<MedicalExpenseClaim>(`${this.baseUrl}/${id}`);
  }

  /** HR files on an employee's behalf — employeeId is required here. */
  createClaim(payload: MedicalExpenseClaimCreateRequest): Promise<MedicalExpenseClaim> {
    return apiService.post<MedicalExpenseClaim>(this.baseUrl, payload);
  }

  /**
   * Corrects a claim.
   *
   * ⚠ Not a patch — every field is written, so seed the payload from {@link getClaim} rather than
   * from a list row or the omitted ones are blanked.
   */
  updateClaim(id: string, payload: MedicalExpenseClaimUpdateRequest): Promise<MedicalExpenseClaim> {
    return apiService.put<MedicalExpenseClaim>(`${this.baseUrl}/${id}`, { ...payload, id });
  }

  /** ⚠ Admin. */
  deleteClaim(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /**
   * Records the adjudication decision.
   *
   * Adjudicated exactly once — a second call is refused with 422. The approver is taken from the
   * token, so the caller's account must be linked to an employee record; an unlinked account gets
   * a 400 naming that as the reason.
   */
  processApproval(id: string, payload: ProcessClaimRequest): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/approval`, payload);
  }

  processPayment(id: string, payload: ProcessClaimPaymentRequest): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/payment`, payload);
  }

  flag(id: string, flagReason: string): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/flag`, {
      claimId: id,
      flagReason,
    });
  }

  unflag(id: string, notes?: string | null): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/unflag`, {
      claimId: id,
      notes: notes ?? null,
    });
  }

  // ── Lines ──────────────────────────────────────────────────────────────────

  getItems(claimId: string): Promise<MedicalExpenseItem[]> {
    return apiService.get<MedicalExpenseItem[]>(`${this.baseUrl}/${claimId}/items`);
  }

  addItem(
    claimId: string,
    payload: MedicalExpenseItemCreateRequest,
  ): Promise<MedicalExpenseItem> {
    return apiService.post<MedicalExpenseItem>(`${this.baseUrl}/${claimId}/items`, {
      ...payload,
      claimId,
    });
  }

  /** Note the flat route: an item is keyed by its own id once created, not by claim. */
  updateItem(id: string, payload: MedicalExpenseItemUpdateRequest): Promise<MedicalExpenseItem> {
    return apiService.put<MedicalExpenseItem>(`${this.baseUrl}/items/${id}`, { ...payload, id });
  }

  /** ⚠ Admin. */
  deleteItem(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/items/${id}`);
  }

  // ── Documents ──────────────────────────────────────────────────────────────

  getDocuments(claimId: string): Promise<MedicalExpenseDocument[]> {
    return apiService.get<MedicalExpenseDocument[]>(`${this.baseUrl}/${claimId}/documents`);
  }

  /** ⚠ Admin. Removes a receipt attached in error; the upload and download were already wired. */
  deleteDocument(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/documents/${id}`);
  }

  /** Multipart through the controlled gate — scanned and DMS-registered since slice 3a. */
  uploadDocument(
    claimId: string,
    file: File,
    type: MedicalDocumentType,
    description?: string | null,
  ): Promise<MedicalExpenseDocument> {
    return hrDocumentService.upload<MedicalExpenseDocument>(
      `${this.baseUrl}/${claimId}/documents`,
      file,
      { type, description },
    );
  }

  downloadDocument(document: MedicalExpenseDocument): Promise<void> {
    return hrDocumentService.download(
      `${this.baseUrl}/documents/${document.id}/download`,
      document.fileName,
    );
  }

  // ── Notes ──────────────────────────────────────────────────────────────────

  /**
   * @param internalOnly returns ONLY adjudicator-internal notes. Never expose this to a claimant —
   * the self-service surface has no note route at all for that reason.
   */
  getNotes(claimId: string, internalOnly = false): Promise<MedicalExpenseClaimNote[]> {
    return apiService.get<MedicalExpenseClaimNote[]>(
      `${this.baseUrl}/${claimId}/notes`,
      internalOnly ? { internalOnly: true } : undefined,
    );
  }

  addNote(
    claimId: string,
    content: string,
    noteType: MedicalExpenseClaimNoteType,
    isInternal: boolean,
  ): Promise<MedicalExpenseClaimNote> {
    return apiService.post<MedicalExpenseClaimNote>(`${this.baseUrl}/${claimId}/notes`, {
      claimId,
      content,
      noteType,
      isInternal,
    });
  }
}

/**
 * An employee's own claims. Backend route: api/medical/me.
 *
 * No route here carries an employee id — the subject is always the token's employee, and a claim
 * belonging to somebody else is a 404 rather than a 403 so the surface cannot be used to discover
 * which claims exist. Approval, payment, flagging and notes are absent by design.
 */
class MedicalSelfServiceClaimService {
  private readonly baseUrl = '/medical/me/expense-claims';

  getMine(): Promise<OwnMedicalClaimSummary[]> {
    return apiService.get<OwnMedicalClaimSummary[]>(this.baseUrl);
  }

  getMineById(id: string): Promise<OwnMedicalClaim> {
    return apiService.get<OwnMedicalClaim>(`${this.baseUrl}/${id}`);
  }

  /**
   * Files a claim as a DRAFT (2026-10-07) — the desk sees nothing until `submit`. A supplied
   * employeeId is ignored by the API — the token decides the subject.
   */
  file(payload: MedicalExpenseClaimCreateRequest): Promise<OwnMedicalClaim> {
    return apiService.post<OwnMedicalClaim>(this.baseUrl, payload);
  }

  /** Replaces a draft's details, the same shape as filing. A submitted claim answers 422. */
  updateDraft(id: string, payload: MedicalExpenseClaimCreateRequest): Promise<OwnMedicalClaim> {
    return apiService.put<OwnMedicalClaim>(`${this.baseUrl}/${id}`, payload);
  }

  /** Draft → Pending. Refused (422) until a receipt is attached. */
  submit(id: string): Promise<OwnMedicalClaim> {
    return apiService.post<OwnMedicalClaim>(`${this.baseUrl}/${id}/submit`, {});
  }

  /** Deletes a draft. A submitted claim answers 422. */
  discardDraft(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Removes a receipt from a draft; once submitted, receipts stay (422). */
  removeDraftDocument(claimId: string, documentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${claimId}/documents/${documentId}`);
  }

  getItems(claimId: string): Promise<MedicalExpenseItem[]> {
    return apiService.get<MedicalExpenseItem[]>(`${this.baseUrl}/${claimId}/items`);
  }

  addItem(
    claimId: string,
    payload: MedicalExpenseItemCreateRequest,
  ): Promise<MedicalExpenseItem> {
    return apiService.post<MedicalExpenseItem>(`${this.baseUrl}/${claimId}/items`, payload);
  }

  getDocuments(claimId: string): Promise<MedicalExpenseDocument[]> {
    return apiService.get<MedicalExpenseDocument[]>(`${this.baseUrl}/${claimId}/documents`);
  }

  uploadDocument(
    claimId: string,
    file: File,
    type: MedicalDocumentType,
    description?: string | null,
  ): Promise<MedicalExpenseDocument> {
    return hrDocumentService.upload<MedicalExpenseDocument>(
      `${this.baseUrl}/${claimId}/documents`,
      file,
      { type, description },
    );
  }

  /** The claim id in the path is load-bearing — it is what scopes the document to its owner. */
  downloadDocument(claimId: string, document: MedicalExpenseDocument): Promise<void> {
    return hrDocumentService.download(
      `${this.baseUrl}/${claimId}/documents/${document.id}/download`,
      document.fileName,
    );
  }
}

/** NHIS claims against the national scheme. Backend route: api/nhis-claims. */
class NhisClaimService {
  private readonly baseUrl = '/nhis-claims';

  getAll(): Promise<NHISClaimSummary[]> {
    return apiService.get<NHISClaimSummary[]>(this.baseUrl);
  }

  getPending(): Promise<NHISClaimSummary[]> {
    return apiService.get<NHISClaimSummary[]>(`${this.baseUrl}/pending`);
  }

  getClaim(id: string): Promise<NHISClaim> {
    return apiService.get<NHISClaim>(`${this.baseUrl}/${id}`);
  }

  create(payload: NHISClaimCreateRequest): Promise<NHISClaim> {
    return apiService.post<NHISClaim>(this.baseUrl, payload);
  }

  update(id: string, payload: NHISClaimUpdateRequest): Promise<NHISClaim> {
    return apiService.put<NHISClaim>(`${this.baseUrl}/${id}`, payload);
  }

  submit(id: string, batchNumber?: string | null): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/submit`, {
      claimId: id,
      batchNumber: batchNumber ?? null,
      submissionDate: new Date().toISOString(),
    });
  }

  updateStatus(
    id: string,
    status: NHISClaimStatus,
    approvedAmount?: number | null,
    rejectionReason?: string | null,
  ): Promise<{ message: string }> {
    return apiService.put<{ message: string }>(`${this.baseUrl}/${id}/status`, {
      claimId: id,
      status,
      approvedAmount: approvedAmount ?? null,
      rejectionReason: rejectionReason ?? null,
    });
  }

  recordPayment(
    id: string,
    paymentReference: string,
    paidAmount: number,
  ): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/payment`, {
      claimId: id,
      paymentReference,
      paidAmount,
      paymentDate: new Date().toISOString(),
    });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Documents ──────────────────────────────────────────────────────────────
  //
  // The metadata-only `POST nhis-claims/documents` beside these is deliberately not wired: it
  // refuses `filePath` and the three DMS ids outright, so through the API it can only mint a row
  // naming a file the server never received. It survives for the legacy migration utility.

  getDocuments(claimId: string): Promise<NHISClaimDocument[]> {
    return apiService.get<NHISClaimDocument[]>(`${this.baseUrl}/${claimId}/documents`);
  }

  /** Through the scanning + DMS gate. NHIS shares the medical-claim document category. */
  uploadDocument(claimId: string, file: File, description?: string | null) {
    return hrDocumentService.upload<NHISClaimDocument>(`${this.baseUrl}/documents/upload`, file, {
      nhisClaimId: claimId,
      description,
    });
  }

  downloadDocument(documentId: string, fileName: string) {
    return hrDocumentService.download(`${this.baseUrl}/documents/${documentId}/download`, fileName);
  }

  /** Admin-tier. */
  removeDocument(documentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/documents/${documentId}`);
  }
}

/** The medical dashboard. Tenant-scoped and computed in SQL. */
class MedicalDashboardService {
  get(upcomingDays = 30): Promise<MedicalDashboard> {
    return apiService.get<MedicalDashboard>('/medical/dashboard', { upcomingDays });
  }
}

export const medicalClaimService = new MedicalClaimService();
export const medicalSelfServiceClaimService = new MedicalSelfServiceClaimService();
export const nhisClaimService = new NhisClaimService();
export const medicalDashboardService = new MedicalDashboardService();
