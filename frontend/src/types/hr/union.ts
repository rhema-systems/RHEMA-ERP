import type { AuditFields } from './common';

/**
 * Where a collective agreement stands today — derived server-side, never stored.
 *
 * ⚠ Do NOT re-derive this from `isActive` plus the two dates. `isActive` is a flag somebody sets and
 * nothing ever clears: slice 6's probe found an agreement running 2019-2021 still reading
 * `isActive: true` five years after it lapsed. The server classifies it once
 * (`CollectiveBargainingAgreementStatuses`) so every screen gives the same answer.
 */
export type CollectiveBargainingAgreementStatus = 'Inactive' | 'Pending' | 'Active' | 'Expired';

export const CBA_STATUSES: CollectiveBargainingAgreementStatus[] = [
  'Active',
  'Pending',
  'Expired',
  'Inactive',
];

// Mirrors CollectiveBargainingAgreementDto.
export interface CollectiveBargainingAgreement extends AuditFields {
  tenantId: string;
  unionId: string;
  unionName?: string | null;
  referenceNumber?: string | null;
  title: string;
  /** ISO date-time. The day the agreement takes effect. */
  effectiveDate: string;
  /** ISO date-time, or null for an open-ended agreement. */
  expiryDate?: string | null;
  summary?: string | null;
  documentReference?: string | null;
  isActive: boolean;
  status: CollectiveBargainingAgreementStatus;
  /** Switched on, already started, not yet expired. */
  isInForce: boolean;
  /** Files attached to this agreement — the signed copy among them (round 3, lane U). */
  documentCount: number;
}

/**
 * A person the company deals with at the union (round 3, lane U; decision D-8): one of our
 * employees who holds a union office, or an external person at the union's own office.
 * Mirrors UnionContactDto.
 */
export interface UnionContact extends AuditFields {
  tenantId: string;
  unionId: string;
  /** Set when the contact is one of our employees; then `displayName` is theirs. */
  employeeId?: string | null;
  employeeNumber?: string | null;
  /** The person's name when they are not an employee. */
  externalName?: string | null;
  displayName: string;
  isInternal: boolean;
  email?: string | null;
  phone?: string | null;
  /** Their role at the union — Shop Steward, Branch Chair, General Secretary. */
  role: string;
  /** Exactly one contact is primary while any exist; the union's contact trio mirrors that row. */
  isPrimary: boolean;
  notes?: string | null;
}

// Mirrors CreateUnionContactDto: an employee (by id) OR an external person (by name).
export interface CreateUnionContactRequest {
  employeeId?: string | null;
  externalName?: string | null;
  email?: string | null;
  phone?: string | null;
  role: string;
  isPrimary: boolean;
  notes?: string | null;
}

export interface UpdateUnionContactRequest extends CreateUnionContactRequest {
  id: string;
}

/** Mirrors UnionDocumentKind. A CollectiveAgreement row always names the agreement it is the copy of. */
export type UnionDocumentKind =
  | 'CollectiveAgreement'
  | 'Constitution'
  | 'Correspondence'
  | 'MembershipList'
  | 'Other';

export const UNION_DOCUMENT_KINDS: { value: UnionDocumentKind; label: string }[] = [
  { value: 'CollectiveAgreement', label: 'Collective agreement (signed copy)' },
  { value: 'Constitution', label: 'Constitution' },
  { value: 'Correspondence', label: 'Correspondence' },
  { value: 'MembershipList', label: 'Membership list' },
  { value: 'Other', label: 'Other' },
];

/** Mirrors UnionDocumentDto. Download by id through the gated route; there is no path. */
export interface UnionDocument extends AuditFields {
  tenantId: string;
  unionId: string;
  agreementId?: string | null;
  agreementTitle?: string | null;
  kind: UnionDocumentKind;
  kindName: string;
  fileName: string;
  fileSize?: number | null;
  description?: string | null;
  uploadDate: string;
  uploadedById: string;
  uploadedByName: string;
}

// Mirrors UnionDto.
export interface Union extends AuditFields {
  tenantId: string;
  code: string;
  name: string;
  description?: string | null;
  contactPerson?: string | null;
  contactEmail?: string | null;
  contactPhone?: string | null;
  isActive: boolean;
  /** Every agreement on file, whatever its state. */
  agreementCount: number;
  /** How many of those are in force today — the number the register actually cares about. */
  inForceAgreementCount: number;
  /** Populated by the detail read and the update response; empty on the list reads' rows. */
  agreements: CollectiveBargainingAgreement[];
  /**
   * Round 3, lane U. The contact trio above is a MIRROR of `primaryContact` once any contact row
   * exists; edit the rows, not the trio. Carried by every read (list rows included).
   */
  contacts: UnionContact[];
  primaryContact?: UnionContact | null;
  /** Populated by the detail read; the list rows carry an empty array. */
  documents: UnionDocument[];
  /** Whether a logo is on file — the image itself comes from the gated `{id}/logo` route. */
  hasLogo: boolean;
  logoFileName?: string | null;
}

// Mirrors CreateUnionDto.
export interface CreateUnionRequest {
  code: string;
  name: string;
  description?: string | null;
  contactPerson?: string | null;
  contactEmail?: string | null;
  contactPhone?: string | null;
  isActive: boolean;
}

// Mirrors UpdateUnionDto (adds Id; the route id must match).
export interface UpdateUnionRequest extends CreateUnionRequest {
  id: string;
}

// Mirrors CreateCollectiveBargainingAgreementDto. `unionId` comes from the route, so the server
// overwrites whatever is sent here — it is included only because the DTO declares it required.
export interface CreateAgreementRequest {
  unionId: string;
  referenceNumber?: string | null;
  title: string;
  effectiveDate: string;
  expiryDate?: string | null;
  summary?: string | null;
  documentReference?: string | null;
  isActive: boolean;
}

// Mirrors UpdateCollectiveBargainingAgreementDto. Note it carries NO unionId: an agreement cannot
// be moved between unions, and sending one would be sending a key the endpoint does not model.
export interface UpdateAgreementRequest {
  id: string;
  referenceNumber?: string | null;
  title: string;
  effectiveDate: string;
  expiryDate?: string | null;
  summary?: string | null;
  documentReference?: string | null;
  isActive: boolean;
}
