import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type {
  SheControlledDocument,
  SheControlledDocumentSummary,
  SheControlledDocumentCategory,
  SheControlledDocumentStatus,
  SheControlledDocumentCreateRequest,
  SheControlledDocumentUpdateRequest,
} from '@/types/hr/safety-documents';

/**
 * SHE controlled document register (SRS §14): filing/retrieval plus version
 * control on the central DMS. Backend route: api/safety/documents. HR-only.
 *
 * The server refuses (422): a duplicate document number, editing or uploading
 * to an archived document, activating a document with no uploaded version or
 * one already active, reviewing a non-active document, re-archiving, and
 * deleting a document that has version history. Rejected files come back with
 * the upload gate's own {code, message} contract (type, size, quota, scan).
 */
class SafetyDocumentsService {
  private readonly baseUrl = '/safety/documents';

  getAll(
    category?: SheControlledDocumentCategory,
    status?: SheControlledDocumentStatus,
    search?: string,
    dueForReviewInDays?: number,
  ): Promise<SheControlledDocumentSummary[]> {
    const params = new URLSearchParams();
    if (category) params.set('category', category);
    if (status) params.set('status', status);
    if (search) params.set('search', search);
    if (dueForReviewInDays != null) params.set('dueForReviewInDays', String(dueForReviewInDays));
    const query = params.toString();
    return apiService.get<SheControlledDocumentSummary[]>(
      query ? `${this.baseUrl}?${query}` : this.baseUrl,
    );
  }

  getById(id: string): Promise<SheControlledDocument> {
    return apiService.get<SheControlledDocument>(`${this.baseUrl}/${id}`);
  }

  create(data: SheControlledDocumentCreateRequest): Promise<SheControlledDocument> {
    return apiService.post<SheControlledDocument>(this.baseUrl, data);
  }

  update(id: string, data: SheControlledDocumentUpdateRequest): Promise<SheControlledDocument> {
    return apiService.put<SheControlledDocument>(`${this.baseUrl}/${id}`, data);
  }

  /** Drafts without history only — anything with versions archives instead. */
  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Uploads the next revision through the controlled upload gate (multipart). */
  uploadVersion(id: string, file: File, changeSummary?: string): Promise<SheControlledDocument> {
    return hrDocumentService.upload<SheControlledDocument>(
      `${this.baseUrl}/${id}/versions`,
      file,
      { changeSummary },
    );
  }

  /** Streams a version's bytes as a browser download. */
  downloadVersion(id: string, versionId: string, fileName: string): Promise<void> {
    return hrDocumentService.download(
      `${this.baseUrl}/${id}/versions/${versionId}/download`,
      fileName,
    );
  }

  /** The approval step — refused while the document has no uploaded version. */
  activate(
    id: string,
    effectiveDate?: string | null,
    nextReviewDate?: string | null,
  ): Promise<SheControlledDocument> {
    return apiService.post<SheControlledDocument>(`${this.baseUrl}/${id}/activate`, {
      documentId: id,
      effectiveDate: effectiveDate ?? null,
      nextReviewDate: nextReviewDate ?? null,
    });
  }

  startReview(id: string): Promise<SheControlledDocument> {
    return apiService.post<SheControlledDocument>(`${this.baseUrl}/${id}/start-review`, {});
  }

  archive(id: string, reason?: string | null): Promise<SheControlledDocument> {
    return apiService.post<SheControlledDocument>(`${this.baseUrl}/${id}/archive`, {
      documentId: id,
      reason: reason ?? null,
    });
  }
}

export const safetyDocumentsService = new SafetyDocumentsService();
