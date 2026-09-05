import { apiService } from '../api.service';

/**
 * Area 25 slice 12b (decision D7) — letters an employee asks HR for.
 *
 * Shapes measured against the live API (`probe-slice12b.mjs`), not inferred. Enums arrive as
 * strings (`"EmploymentConfirmation"`, `"Pending"`) and are sent back the same way.
 *
 * HR fulfils a request one of two ways, and `fulfilment` says which: `Generated` means a letter
 * was rendered from the HR-editable template and FROZEN on the record — read it with
 * `getDocument` and print it; `Uploaded` means HR attached a signed scan — download it as a file.
 * The screens must branch on this, because only one of the two endpoints will answer.
 */

export type HrLetterType =
  | 'EmploymentConfirmation'
  | 'IntroductionLetter'
  | 'ServiceCertificate'
  | 'SalaryConfirmation';

export type HrLetterRequestStatus = 'Pending' | 'Issued' | 'Rejected' | 'Cancelled';

/** What each letter is for, in the employee's language — the request form's copy. */
export const LETTER_TYPES: { value: HrLetterType; label: string; hint: string }[] = [
  {
    value: 'EmploymentConfirmation',
    label: 'Confirmation of employment',
    hint: 'Confirms that you work here, since when, and in what role. The usual one.',
  },
  {
    value: 'IntroductionLetter',
    label: 'Letter of introduction',
    hint: 'Introduces you to a named organisation — say who it should be addressed to.',
  },
  {
    value: 'ServiceCertificate',
    label: 'Certificate of service',
    hint: 'States the period and capacity in which you have served.',
  },
  {
    value: 'SalaryConfirmation',
    label: 'Employment and salary confirmation',
    hint: 'Confirms your employment and states your salary — for a bank or a landlord.',
  },
];

export const LETTER_TYPE_LABEL: Record<HrLetterType, string> = LETTER_TYPES.reduce(
  (map, t) => ({ ...map, [t.value]: t.label }),
  {} as Record<HrLetterType, string>,
);

export interface HrLetterRequest {
  id: string;
  requestNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  letterType: HrLetterType;
  letterTypeName: string;
  purpose: string;
  addressedTo?: string | null;
  status: HrLetterRequestStatus;
  statusName: string;
  requestedAt: string;
  issuedById?: string | null;
  issuedByName?: string | null;
  issuedAt?: string | null;
  /** The reference printed on the letter — distinct from `requestNumber`, and only set once issued. */
  letterNumber?: string | null;
  decisionComments?: string | null;
  rejectedAt?: string | null;
  cancelledAt?: string | null;
  hasDocument: boolean;
  /** 'Generated' | 'Uploaded' | null while pending. Decides which endpoint answers. */
  fulfilment?: 'Generated' | 'Uploaded' | null;
  fileName?: string | null;
}

export interface HrLetterDocument {
  requestId: string;
  requestNumber: string;
  letterNumber?: string | null;
  letterType: HrLetterType;
  letterTypeName: string;
  subject: string;
  /** The letter as HTML. For an issued request this is the copy frozen at issue, not a re-render. */
  html: string;
  isIssued: boolean;
  issuedAt?: string | null;
  issuedByName?: string | null;
}

export interface CreateHrLetterRequest {
  letterType: HrLetterType;
  purpose: string;
  addressedTo?: string | null;
}

class MyLettersService {
  private readonly baseUrl = '/employee-portal/letters';

  getMine(): Promise<HrLetterRequest[]> {
    return apiService.get<HrLetterRequest[]>(this.baseUrl);
  }

  create(payload: CreateHrLetterRequest): Promise<HrLetterRequest> {
    return apiService.post<HrLetterRequest>(this.baseUrl, payload);
  }

  /** Somebody else's id is a 404 lookup miss — never a 403. */
  getById(id: string): Promise<HrLetterRequest> {
    return apiService.get<HrLetterRequest>(`${this.baseUrl}/${id}`);
  }

  cancel(id: string): Promise<HrLetterRequest> {
    return apiService.post<HrLetterRequest>(`${this.baseUrl}/${id}/cancel`, {});
  }

  /** The frozen letter. 404s while unissued, and for the uploaded route — use `fileEndpoint`. */
  getDocument(id: string): Promise<HrLetterDocument> {
    return apiService.get<HrLetterDocument>(`${this.baseUrl}/${id}/document`);
  }

  fileEndpoint(id: string): string {
    return `${this.baseUrl}/${id}/file`;
  }
}

/** The HR desk side. Same permissions that govern the employee record itself. */
class HrLetterQueueService {
  private readonly baseUrl = '/hr/letter-requests';

  getQueue(status?: HrLetterRequestStatus): Promise<HrLetterRequest[]> {
    return apiService.get<HrLetterRequest[]>(
      status ? `${this.baseUrl}?status=${status}` : this.baseUrl,
    );
  }

  /** Renders the letter WITHOUT issuing it — HR reads it before committing. */
  preview(id: string): Promise<HrLetterDocument> {
    return apiService.get<HrLetterDocument>(`${this.baseUrl}/${id}/preview`);
  }

  /** Issues the generated letter and freezes it. */
  issue(id: string): Promise<HrLetterRequest> {
    return apiService.post<HrLetterRequest>(`${this.baseUrl}/${id}/issue`, {});
  }

  getDocument(id: string): Promise<HrLetterDocument> {
    return apiService.get<HrLetterDocument>(`${this.baseUrl}/${id}/document`);
  }

  /** The comment is required — the employee reads it back. */
  reject(id: string, comments: string): Promise<HrLetterRequest> {
    return apiService.post<HrLetterRequest>(`${this.baseUrl}/${id}/reject`, { comments });
  }

  /** Multipart, through the controlled gate — for a letter that needs a wet signature. */
  uploadEndpoint(id: string): string {
    return `${this.baseUrl}/${id}/upload`;
  }
}

export const myLettersService = new MyLettersService();
export const hrLetterQueueService = new HrLetterQueueService();
