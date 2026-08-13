// Types for HR Training & Learning — Slice 4 (Certificates & Service Bonds).
//
// Three distinct things that all read as "a certificate" in conversation but are not the same:
//   • TrainingCertificate — issued BY us off the back of a completed nomination, with a public
//     verification code a third party can check.
//   • EmployeeCertificate — held BY the employee from an outside body (PMP, CISA, a trade licence).
//     We record and verify it; we did not issue it.
//   • TrainingServiceBond — the service obligation attached to expensive training.
//
// Mirrors the same-named DTOs in ErpSystem.Core.DTOs.HR.TrainingDTOs.

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

// ── Enums ──────────────────────────────────────────────────────────────────────────────────

export type CertificateStatus = 'Active' | 'Expired' | 'Revoked' | 'Pending';

export const CERTIFICATE_STATUS_OPTIONS = opts<CertificateStatus>([
  ['Active', 'Active'],
  ['Expired', 'Expired'],
  ['Revoked', 'Revoked'],
  ['Pending', 'Pending'],
]);

// ── Training Certificate (issued by us) ────────────────────────────────────────────────────

export interface TrainingCertificate {
  id: string;
  certificateNumber: string;
  /** The code a third party enters on the public verification page. */
  verificationCode?: string | null;
  nominationId: string;
  nominationNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  programId: string;
  programName: string;
  certificateName: string;
  issuedDate: string;
  expiryDate?: string | null;
  validityMonths?: number | null;
  isExpired: boolean;
  daysUntilExpiry?: number | null;
  status: CertificateStatus;
  revokedDate?: string | null;
  revokedReason?: string | null;
  filePath?: string | null;
  externalUrl?: string | null;
  isRenewal: boolean;
  previousCertificateId?: string | null;
  previousCertificateNumber?: string | null;
  issuedById?: string | null;
  issuedByName?: string | null;
}

export interface TrainingCertificateSummary {
  id: string;
  certificateNumber: string;
  verificationCode?: string | null;
  /** Ids added so a list row can link through to the holder and the programme. */
  employeeId: string;
  programId: string;
  employeeName: string;
  programName: string;
  certificateName: string;
  issuedDate: string;
  expiryDate?: string | null;
  isExpired: boolean;
  status: CertificateStatus;
}

export interface IssueCertificateRequest {
  nominationId: string;
  employeeId: string;
  programId: string;
  certificateName: string;
  issuedDate: string;
  expiryDate?: string | null;
  validityMonths?: number | null;
  filePath?: string | null;
  externalUrl?: string | null;
  isRenewal?: boolean;
  previousCertificateId?: string | null;
}

export interface RevokeCertificateRequest {
  certificateId: string;
  revokedReason: string;
}

/**
 * The public verification answer. Deliberately narrow — no ids, no tenant, no contact details —
 * because the endpoint is anonymous and rate-limited.
 */
export interface CertificateVerificationResult {
  found: boolean;
  isValid: boolean;
  status: 'Active' | 'Revoked' | 'Expired' | 'NotFound';
  certificateNumber?: string | null;
  verificationCode?: string | null;
  certificateName?: string | null;
  employeeName?: string | null;
  programName?: string | null;
  issuingOrganization?: string | null;
  issuedDate?: string | null;
  expiryDate?: string | null;
  isExpired: boolean;
  isRevoked: boolean;
  revokedReason?: string | null;
}

// ── Employee Certificate (held by the employee, issued elsewhere) ───────────────────────────

export interface EmployeeCertificate {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  certificateName: string;
  issuingBody: string;
  certificateNumber?: string | null;
  issuedDate: string;
  expiryDate?: string | null;
  isExpired: boolean;
  status: CertificateStatus;
  category?: string | null;
  categoryName?: string | null;
  description?: string | null;
  filePath?: string | null;
  isVerified: boolean;
  verifiedById?: string | null;
  verifiedByName?: string | null;
  verifiedDate?: string | null;
}

export interface EmployeeCertificateSummary {
  id: string;
  /** Added: the unverified queue is org-wide, so a row has to say whose certificate it is. */
  employeeId: string;
  employeeName: string;
  certificateName: string;
  issuingBody: string;
  issuedDate: string;
  expiryDate?: string | null;
  isExpired: boolean;
  status: CertificateStatus;
  isVerified: boolean;
}

export interface EmployeeCertificateCreate {
  employeeId: string;
  certificateName: string;
  issuingBody: string;
  certificateNumber?: string | null;
  issuedDate: string;
  expiryDate?: string | null;
  category?: string | null;
  description?: string | null;
  filePath?: string | null;
}

export type EmployeeCertificateUpdate = Omit<EmployeeCertificateCreate, 'employeeId'>;

// ── Training Service Bond ──────────────────────────────────────────────────────────────────
//
// NOT redefined here. `TrainingServiceBond`, `TrainingBondStatus` and the lifecycle payloads already
// live in `types/hr/outcomes.ts`, with `trainingServiceBondService` in `services/hr/outcomes.service.ts`
// and a full screen at `/hr/service-bonds` — all built during the area-5 outcomes slice. Bonds are
// raised automatically when a nomination is approved (TrainingServiceBondService
// .EnsureBondForNominationAsync), which is why there is no "new bond" form anywhere.
export type {
  TrainingServiceBond,
  TrainingBondStatus,
} from './outcomes';
