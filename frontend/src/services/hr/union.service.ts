import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type {
  CollectiveBargainingAgreement,
  CreateAgreementRequest,
  CreateUnionContactRequest,
  CreateUnionRequest,
  Union,
  UnionContact,
  UnionDocument,
  UnionDocumentKind,
  UpdateAgreementRequest,
  UpdateUnionContactRequest,
  UpdateUnionRequest,
} from '@/types/hr/union';

/**
 * The trade-union register and its collective bargaining agreements. Backend: `api/hr/unions`.
 *
 * ⚠ Reads are open to any authenticated user; **writes are SuperAdmin / TenantAdmin / HR**. The
 * union a role falls under is printed on the job description every employee can read, and a
 * collective agreement is published to the members it binds.
 *
 * ⚠ `remove` refuses a union that still has agreements, with a message naming how many. The delete
 * is a soft delete and the configured cascade only fires on a hard one, so deleting a union with
 * agreements would leave them alive and unreachable — every read of them goes through the union.
 */
class UnionService {
  private readonly baseUrl = '/hr/unions';

  /** Every union, each carrying its agreements. */
  getAll(): Promise<Union[]> {
    return apiService.get<Union[]>(this.baseUrl);
  }

  /** Active unions only — the picker source for the job-description form. */
  getActive(): Promise<Union[]> {
    return apiService.get<Union[]>(`${this.baseUrl}/active`);
  }

  getById(id: string): Promise<Union> {
    return apiService.get<Union>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateUnionRequest): Promise<Union> {
    return apiService.post<Union>(this.baseUrl, data);
  }

  /** The response carries the agreements, so a screen may re-render straight from it. */
  update(id: string, data: UpdateUnionRequest): Promise<Union> {
    return apiService.put<Union>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  getAgreements(unionId: string): Promise<CollectiveBargainingAgreement[]> {
    return apiService.get<CollectiveBargainingAgreement[]>(`${this.baseUrl}/${unionId}/agreements`);
  }

  addAgreement(unionId: string, data: CreateAgreementRequest): Promise<CollectiveBargainingAgreement> {
    return apiService.post<CollectiveBargainingAgreement>(`${this.baseUrl}/${unionId}/agreements`, data);
  }

  /** ⚠ Route is `/agreements/{id}`, NOT nested under the union. An agreement cannot change union. */
  updateAgreement(id: string, data: UpdateAgreementRequest): Promise<CollectiveBargainingAgreement> {
    return apiService.put<CollectiveBargainingAgreement>(`${this.baseUrl}/agreements/${id}`, data);
  }

  removeAgreement(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/agreements/${id}`);
  }

  // ── Contacts (round 3, lane U; decision D-8) ─────────────────────────────
  // The first contact becomes primary whatever the box said; a new primary demotes the old one;
  // the only primary cannot be demoted (400 in words); deleting the primary promotes the oldest.
  // The union's contactPerson/Email/Phone trio is rewritten from the primary on every save.

  getContacts(unionId: string): Promise<UnionContact[]> {
    return apiService.get<UnionContact[]>(`${this.baseUrl}/${unionId}/contacts`);
  }

  addContact(unionId: string, data: CreateUnionContactRequest): Promise<UnionContact> {
    return apiService.post<UnionContact>(`${this.baseUrl}/${unionId}/contacts`, data);
  }

  /** ⚠ Route is `/contacts/{id}`, not nested under the union. */
  updateContact(id: string, data: UpdateUnionContactRequest): Promise<UnionContact> {
    return apiService.put<UnionContact>(`${this.baseUrl}/contacts/${id}`, data);
  }

  removeContact(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/contacts/${id}`);
  }

  // ── Documents (round 3, lane U) ──────────────────────────────────────────
  // Multipart through the controlled-upload gate (scan-mandatory: a missing scanner is a 422 with
  // a reason, not a stored file). Served only by the gated download — never link to the route.

  getDocuments(unionId: string): Promise<UnionDocument[]> {
    return apiService.get<UnionDocument[]>(`${this.baseUrl}/${unionId}/documents`);
  }

  /**
   * Naming an `agreementId` makes the file that agreement's signed copy (kind forced to
   * CollectiveAgreement); kind CollectiveAgreement WITHOUT an agreement is refused.
   */
  uploadDocument(
    unionId: string,
    file: File,
    fields: { kind: UnionDocumentKind; agreementId?: string | null; description?: string | null },
  ): Promise<UnionDocument> {
    return hrDocumentService.upload<UnionDocument>(`${this.baseUrl}/${unionId}/documents`, file, {
      kind: fields.kind,
      agreementId: fields.agreementId ?? undefined,
      description: fields.description ?? undefined,
    });
  }

  downloadDocument(unionId: string, document: UnionDocument): Promise<void> {
    return hrDocumentService.download(
      `${this.baseUrl}/${unionId}/documents/${document.id}/download`,
      document.fileName,
    );
  }

  /** HR admin only (`HR.Employee.Admin`); the HR actor is refused. */
  removeDocument(documentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/documents/${documentId}`);
  }

  // ── Logo (round 3, lane U) ───────────────────────────────────────────────

  /** The gated GET that streams the logo inline — feed it to `GatedPhoto`, never to an `<img src>`. */
  logoEndpoint(unionId: string): string {
    return `${this.baseUrl}/${unionId}/logo`;
  }

  /** Answers the union with `hasLogo: true`. */
  uploadLogo(unionId: string, file: File): Promise<Union> {
    return hrDocumentService.upload<Union>(`${this.baseUrl}/${unionId}/logo`, file);
  }
}

export const unionService = new UnionService();
