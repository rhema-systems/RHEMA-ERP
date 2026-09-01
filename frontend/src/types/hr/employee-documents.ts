/**
 * The employee document file — lane 3c.
 *
 * ⚠ Every shape here was PROBED against the running API on 2026-09-01, not written from the
 * endpoint names. Four TS unions in this module have shipped as fiction that type-checked because
 * nobody called the endpoint first.
 */

/** A kind of document. Tenant reference data, so this is a lookup and never a union of literals. */
export interface EmployeeDocumentType {
  id: string;
  name: string;
  code: string | null;
  description: string | null;
  /** Whether documents of this kind carry an expiry date the form must ask for. */
  hasExpiry: boolean;
  /** ⚠ Stored, read by nothing yet — no expiry sweep exists. Do not present it as an active setting. */
  expiryReminderLeadDays: number | null;
  isActive: boolean;
  /** How many documents reference it — what makes the delete affordance honest. */
  documentCount: number;
}

export interface EmployeeDocument {
  id: string;
  employeeId: string;
  employeeName: string | null;
  employeeNumber: string | null;
  documentTypeId: string;
  documentTypeName: string | null;
  title: string | null;
  description: string | null;
  issuedOn: string | null;
  expiresOn: string | null;
  /** True once the expiry is past. A document expiring today is still valid. */
  isExpired: boolean;
  /** Negative once expired; null where the document does not expire. */
  daysUntilExpiry: number | null;
  fileName: string | null;
  mimeType: string | null;
  fileSizeBytes: number | null;
  documentRecordId: string | null;
  uploadedById: string | null;
  uploadedByName: string | null;
  createdAt: string;
}

export interface PositionDocumentRequirement {
  id: string;
  positionId: string;
  positionTitle: string | null;
  documentTypeId: string;
  documentTypeName: string | null;
  isMandatory: boolean;
  notes: string | null;
}

export interface EmployeeDocumentComplianceLine {
  documentTypeId: string;
  documentTypeName: string;
  isMandatory: boolean;
  isSatisfied: boolean;
  documentId: string | null;
  expiresOn: string | null;
  daysUntilExpiry: number | null;
  /**
   * Held once, now expired — as opposed to never held at all.
   *
   * ⚠ Two different conversations. "Your licence lapsed" and "we never had your licence" must not
   * render the same, which is the whole reason this flag exists rather than a single "missing".
   */
  isExpiredOnly: boolean;
}

export interface EmployeeDocumentCompliance {
  employeeId: string;
  employeeName: string | null;
  /**
   * ⚠ Null where the employee holds no position — the API translates `Guid.Empty` for us, so a
   * client never sees an all-zeros id. An employee with no position has no requirements and is
   * reported compliant.
   */
  positionId: string | null;
  positionTitle: string | null;
  /** ⚠ Mandatory DOCUMENTS only — it kept its original meaning when the guarantor block arrived. */
  isCompliant: boolean;
  mandatoryCount: number;
  mandatorySatisfiedCount: number;
  lines: EmployeeDocumentComplianceLine[];

  // ── The guarantor the post requires ────────────────────────────────────────
  requiresGuarantor: boolean;
  /** Null means "a guarantor, amount unspecified" — having one at all is then the whole test. */
  requiredGuarantorAmount: number | null;
  requiredGuarantorCurrencyCode: string | null;
  guarantorCount: number;
  /** Counts only sureties stated in the requirement's currency. */
  guaranteedTotal: number;
  /**
   * Sureties in another currency, counted and shown but NEVER converted — a converted verdict
   * would move with the exchange rate, and which date's rate to use is a policy nobody has chosen.
   */
  guarantorsInOtherCurrencies: number;
  isGuarantorSatisfied: boolean;
  guarantorShortfall: number | null;
  /** Documents AND guarantor. */
  isFullyCompliant: boolean;
}
