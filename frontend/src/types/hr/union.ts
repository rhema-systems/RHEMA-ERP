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
