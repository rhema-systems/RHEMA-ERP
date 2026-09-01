import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type {
  EmployeeDocument,
  EmployeeDocumentCompliance,
  EmployeeDocumentType,
  PositionDocumentRequirement,
} from '@/types/hr/employee-documents';

/**
 * The employee document file. Backend route: `api/hr/employee-documents`.
 *
 * ⚠ **There is no create method, and there must never be one.** A document exists only as the
 * result of {@link EmployeeDocumentService.upload}, which goes through the virus-scanning gate and
 * the central DMS. A JSON create taking `fileName`/`filePath` is the sink that D-10, D-14 and D-39
 * each had to remove after it shipped; here the route returns 404 and the harness asserts it.
 *
 * ⚠ **Tiers.** Reading and uploading need `HR.Policy.EmployeeRead`/`EmployeeWrite`; deleting a
 * document, administering the vocabulary and requiring a document of a position are all
 * `EmployeeAdmin`. The desk that files a passport scan cannot silently remove it.
 */
class EmployeeDocumentService {
  private readonly baseUrl = '/hr/employee-documents';

  // ── the vocabulary ─────────────────────────────────────────────────────────

  getTypes(includeInactive = false) {
    return apiService.get<EmployeeDocumentType[]>(`${this.baseUrl}/types`, { includeInactive });
  }

  /** Seeds a starting list. Safe to run twice — existing names are skipped. */
  seedDefaultTypes() {
    return apiService.post<{ added: number }>(`${this.baseUrl}/types/seed-defaults`, {});
  }

  createType(payload: {
    name: string; code?: string | null; description?: string | null;
    hasExpiry: boolean; expiryReminderLeadDays?: number | null; isActive: boolean;
  }) {
    return apiService.post<EmployeeDocumentType>(`${this.baseUrl}/types`, payload);
  }

  updateType(id: string, payload: {
    name: string; code?: string | null; description?: string | null;
    hasExpiry: boolean; expiryReminderLeadDays?: number | null; isActive: boolean;
  }) {
    return apiService.put<EmployeeDocumentType>(`${this.baseUrl}/types/${id}`, { id, ...payload });
  }

  /** ⚠ Refused while any document or requirement references it — deactivate instead. */
  deleteType(id: string) {
    return apiService.delete(`${this.baseUrl}/types/${id}`);
  }

  // ── documents ──────────────────────────────────────────────────────────────

  getForEmployee(employeeId: string) {
    return apiService.get<EmployeeDocument[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  /**
   * Files a document against an employee, through the gate.
   *
   * ⚠ The uploader is taken from the TOKEN, not from these fields — there is no way to say who
   * filed it. ⚠ The account must be employee-linked: the gate refuses an unlinked one with 401
   * before anything is stored, which is why a bare `admin` cannot upload.
   */
  upload(employeeId: string, file: File, fields: {
    documentTypeId: string;
    title?: string | null;
    description?: string | null;
    issuedOn?: string | null;
    expiresOn?: string | null;
  }) {
    return hrDocumentService.upload<EmployeeDocument>(
      `${this.baseUrl}/employee/${employeeId}/upload`, file, fields);
  }

  /** Corrects the metadata. The file itself is replaced by uploading again. */
  update(id: string, payload: {
    documentTypeId: string;
    title?: string | null;
    description?: string | null;
    issuedOn?: string | null;
    expiresOn?: string | null;
  }) {
    return apiService.put<EmployeeDocument>(`${this.baseUrl}/${id}`, { id, ...payload });
  }

  /** ⚠ Admin tier — a higher bar than uploading. */
  delete(id: string) {
    return apiService.delete(`${this.baseUrl}/${id}`);
  }

  /**
   * ⚠ The file lives outside the web root and the download needs the bearer token, so an
   * `<a href>` cannot work. Always through the document service.
   */
  downloadUrl(id: string) {
    return `${this.baseUrl}/${id}/download`;
  }

  // ── requirements and compliance ────────────────────────────────────────────

  getRequirements(positionId: string) {
    return apiService.get<PositionDocumentRequirement[]>(
      `${this.baseUrl}/positions/${positionId}/requirements`);
  }

  addRequirement(payload: {
    positionId: string; documentTypeId: string; isMandatory: boolean; notes?: string | null;
  }) {
    return apiService.post<PositionDocumentRequirement>(`${this.baseUrl}/requirements`, payload);
  }

  updateRequirement(id: string, payload: { isMandatory: boolean; notes?: string | null }) {
    return apiService.put<PositionDocumentRequirement>(
      `${this.baseUrl}/requirements/${id}`, { id, ...payload });
  }

  deleteRequirement(id: string) {
    return apiService.delete(`${this.baseUrl}/requirements/${id}`);
  }

  /**
   * What the employee's position requires and whether they hold it.
   *
   * ⚠ Read this rather than deriving compliance in the browser, and note it is also the only read
   * that resolves an employee's `positionId` — the PAGED EMPLOYEE LIST DOES NOT RETURN ONE
   * (probed 2026-09-01, it comes back undefined). A requirements editor keyed off a list row's
   * position would be keyed off nothing.
   */
  getCompliance(employeeId: string) {
    return apiService.get<EmployeeDocumentCompliance>(
      `${this.baseUrl}/employee/${employeeId}/compliance`);
  }
}

export const employeeDocumentService = new EmployeeDocumentService();
