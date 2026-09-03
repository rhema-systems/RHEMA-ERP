import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type {
  EmployeeImportColumnGuide,
  EmployeeImportCommitPolicy,
  EmployeeImportFileRejection,
  EmployeeImportFollowUp,
  EmployeeImportMode,
  EmployeeImportProgress,
  EmployeeImportRow,
  EmployeeImportRowOutcome,
  EmployeeImportRowPage,
  EmployeeImportSessionSummary,
} from '@/types/hr/employee-import';

/**
 * Employee bulk import. Backend route: `api/hr/employees/import-sessions` (EmployeeAdmin tier).
 *
 * The shape is template → upload (checked, nothing written) → review → commit (background) →
 * progress → follow-up. Every download is streamed with the bearer token: the template, the
 * checked copy and the follow-up list are generated on request, not stored at a URL.
 *
 * ⚠ Upload failures come in two shapes. A workbook the checker cannot use at all answers 400 with
 * `{ message, findings }` and no session; a file the upload gate refuses (type, size, malware)
 * answers the gate's own status with `{ code, message }`. {@link toFileRejection} normalises both.
 */
class EmployeeImportService {
  private readonly baseUrl = '/hr/employees/import-sessions';

  downloadTemplate() {
    const stamp = new Date().toISOString().slice(0, 10);
    return hrDocumentService.download(`${this.baseUrl}/template`, `Employee Import Template ${stamp}.xlsx`);
  }

  getColumnGuide() {
    return apiService.get<EmployeeImportColumnGuide[]>(`${this.baseUrl}/columns`);
  }

  /**
   * Uploads a filled template. Checks every row; writes nothing to the register.
   * `mode` decides what a staff number already in the register means: an error (CreateOnly),
   * an update of that employee (UpdateOnly), or either (CreateOrUpdate).
   */
  async upload(file: File, mode: EmployeeImportMode = 'CreateOnly'): Promise<EmployeeImportSessionSummary> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('mode', mode);
    try {
      return await apiService.post<EmployeeImportSessionSummary>(this.baseUrl, formData);
    } catch (error) {
      throw toFileRejection(error);
    }
  }

  list() {
    return apiService.get<EmployeeImportSessionSummary[]>(this.baseUrl);
  }

  get(id: string) {
    return apiService.get<EmployeeImportSessionSummary>(`${this.baseUrl}/${id}`);
  }

  getRows(id: string, options: { outcome?: EmployeeImportRowOutcome | null; search?: string; page?: number; pageSize?: number } = {}) {
    return apiService.get<EmployeeImportRowPage>(`${this.baseUrl}/${id}/rows`, {
      outcome: options.outcome ?? undefined,
      search: options.search || undefined,
      page: options.page ?? 1,
      pageSize: options.pageSize ?? 50,
    });
  }

  setSkip(id: string, rowId: string, skip: boolean) {
    return apiService.patch<EmployeeImportRow>(`${this.baseUrl}/${id}/rows/${rowId}/skip`, { skip });
  }

  /** The uploaded workbook with a Result column and a comment on every problem cell. */
  downloadCheckedCopy(id: string, reference: string) {
    return hrDocumentService.download(`${this.baseUrl}/${id}/report`, `${reference} checked.xlsx`);
  }

  commit(id: string, policy: EmployeeImportCommitPolicy) {
    return apiService.post<EmployeeImportSessionSummary>(`${this.baseUrl}/${id}/commit`, { policy });
  }

  progress(id: string) {
    return apiService.silentGet<EmployeeImportProgress>(`${this.baseUrl}/${id}/progress`);
  }

  cancel(id: string) {
    return apiService.post<EmployeeImportSessionSummary>(`${this.baseUrl}/${id}/cancel`, {});
  }

  followUp(id: string) {
    return apiService.get<EmployeeImportFollowUp[]>(`${this.baseUrl}/${id}/follow-up`);
  }

  downloadFollowUp(id: string, reference: string) {
    return hrDocumentService.download(`${this.baseUrl}/${id}/follow-up?format=xlsx`, `${reference} profiles to complete.xlsx`);
  }
}

export class EmployeeImportUploadError extends Error {
  readonly status: number;
  readonly findings: EmployeeImportFileRejection['findings'];
  readonly code?: string;

  constructor(message: string, status: number, findings: EmployeeImportFileRejection['findings'], code?: string) {
    super(message);
    this.name = 'EmployeeImportUploadError';
    this.status = status;
    this.findings = findings;
    this.code = code;
  }
}

function toFileRejection(error: any): EmployeeImportUploadError {
  const status: number = error?.status ?? error?.response?.status ?? 0;
  const body = error?.data ?? error?.response?.data ?? error?.body;
  const message: string =
    body?.message ?? error?.message ?? 'The file could not be uploaded. Please try again.';
  const findings = Array.isArray(body?.findings) ? body.findings : [];
  return new EmployeeImportUploadError(message, status, findings, body?.code);
}

export const employeeImportService = new EmployeeImportService();
