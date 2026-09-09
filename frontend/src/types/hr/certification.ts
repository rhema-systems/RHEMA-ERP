// Demo feedback round 2, lane C2 — the certification model (plan § 6.3).
// Mirrors CertificationDTOs.cs.

import type { AuditFields } from './common';

export type CertificationKind = 'Certification' | 'Licence' | 'Permit' | 'Registration';

export const CERTIFICATION_KIND_OPTIONS: { value: CertificationKind; label: string }[] = [
  { value: 'Certification', label: 'Certification' },
  { value: 'Licence', label: 'Licence' },
  { value: 'Permit', label: 'Permit' },
  { value: 'Registration', label: 'Registration' },
];

/** Computed server-side from the dates and lead days; only Revoked is stored. */
export type EmployeeCertificationStatus = 'Valid' | 'ExpiringSoon' | 'Expired' | 'Revoked';

// ── Catalogue ────────────────────────────────────────────────────────────────

export interface Certification extends AuditFields {
  tenantId: string;
  certifyingBodyId: string;
  certifyingBodyName: string;
  certifyingBodyAbbreviation?: string | null;
  name: string;
  code?: string | null;
  kind: CertificationKind;
  description?: string | null;
  /** Null = does not expire. */
  validityMonths?: number | null;
  renewalRequired: boolean;
  /** Null = the tenant's policy default. */
  expiryNotificationLeadDays?: number | null;
  isActive: boolean;
  skillCount: number;
  positionCount: number;
  holderCount: number;
}

export interface CertificationRequest {
  certifyingBodyId: string;
  name: string;
  code?: string | null;
  kind: CertificationKind;
  description?: string | null;
  validityMonths?: number | null;
  renewalRequired: boolean;
  expiryNotificationLeadDays?: number | null;
  isActive: boolean;
}

// ── Skill links ──────────────────────────────────────────────────────────────

export interface SkillCertification {
  id: string;
  certificationId: string;
  certificationName: string;
  kind: CertificationKind;
  certifyingBodyId: string;
  certifyingBodyName: string;
  isMandatory: boolean;
  notes?: string | null;
}

/** One row of a skill's accepted credentials; the skill save sends the whole set. */
export interface SkillCertificationInput {
  certificationId: string;
  isMandatory: boolean;
  notes?: string | null;
}

// ── Position requirements ────────────────────────────────────────────────────

export interface PositionCertificationRequirement {
  id: string;
  certificationId: string;
  certificationName: string;
  kind: CertificationKind;
  certifyingBodyId: string;
  certifyingBodyName: string;
  isMandatory: boolean;
  notes?: string | null;
}

/** One row of a position's required credentials; the position save sends the whole set. */
export interface PositionCertificationRequirementInput {
  certificationId: string;
  isMandatory: boolean;
  notes?: string | null;
}

// ── Employee credentials ─────────────────────────────────────────────────────

export interface EmployeeCertification extends AuditFields {
  employeeId: string;
  certificationId: string;
  certificationName: string;
  kind: CertificationKind;
  certifyingBodyId: string;
  certifyingBodyName: string;
  certificateNumber?: string | null;
  /** DateOnly */
  issuedOn?: string | null;
  expiresOn?: string | null;
  status: EmployeeCertificationStatus;
  /** Negative once expired; null when it does not expire. */
  daysUntilExpiry?: number | null;
  isRevoked: boolean;
  revokedOn?: string | null;
  revocationReason?: string | null;
  hasEvidence: boolean;
  evidenceFileName?: string | null;
  evidenceFileSizeBytes?: number | null;
  isVerified: boolean;
  verifiedById?: string | null;
  verifiedByName?: string | null;
  verifiedOn?: string | null;
  notes?: string | null;
}

export interface CreateEmployeeCertificationRequest {
  employeeId: string;
  certificationId: string;
  certificateNumber?: string | null;
  issuedOn?: string | null;
  /** Blank = computed from issuedOn and the catalogue's validity. */
  expiresOn?: string | null;
  notes?: string | null;
}

export interface UpdateEmployeeCertificationRequest {
  id: string;
  certificateNumber?: string | null;
  issuedOn?: string | null;
  expiresOn?: string | null;
  notes?: string | null;
}

export interface RevokeEmployeeCertificationRequest {
  reason: string;
  revokedOn?: string | null;
}

// ── Compliance ───────────────────────────────────────────────────────────────

export type ComplianceLineStatus = 'Held' | 'ExpiringSoon' | 'Expired' | 'Revoked' | 'Missing';

export interface EmployeeCertificationComplianceLine {
  certificationId: string;
  certificationName: string;
  certifyingBodyName: string;
  kind: CertificationKind;
  isMandatory: boolean;
  status: ComplianceLineStatus;
  isSatisfied: boolean;
  employeeCertificationId?: string | null;
  expiresOn?: string | null;
  daysUntilExpiry?: number | null;
}

export interface EmployeeCertificationCompliance {
  employeeId: string;
  employeeName?: string | null;
  positionId?: string | null;
  positionTitle?: string | null;
  requiresCertification: boolean;
  requiresLicense: boolean;
  isCompliant: boolean;
  mandatoryCount: number;
  mandatorySatisfiedCount: number;
  expiringSoonCount: number;
  expiredCount: number;
  lines: EmployeeCertificationComplianceLine[];
}

// ── The expiry sweep ─────────────────────────────────────────────────────────

export interface CertificationExpiryReminderItem {
  kind: string;
  employeeId: string;
  employeeName?: string | null;
  employeeNumber?: string | null;
  employeeCertificationId: string;
  certificationId: string;
  certificationName: string;
  certificateNumber?: string | null;
  reference?: string | null;
  dueDate?: string | null;
  daysRemaining: number;
  leadDays: number;
  escalationTier: number;
  routedToEmployeeId?: string | null;
  alreadyRaised: boolean;
}

export interface CertificationExpiryRunResult {
  runId: string;
  startedAt: string;
  completedAt?: string | null;
  trigger: string;
  credentialsConsidered: number;
  remindersQueued: number;
  alreadyRaised: number;
}

export interface CertificationExpiryLogEntry {
  id: string;
  runId: string;
  kind: string;
  employeeId: string;
  employeeName?: string | null;
  employeeNumber?: string | null;
  employeeCertificationId: string;
  certificationId: string;
  certificationName?: string | null;
  reference?: string | null;
  dueDate?: string | null;
  daysRemaining: number;
  escalationTier: number;
  routedToEmployeeId?: string | null;
  raisedAt: string;
}
