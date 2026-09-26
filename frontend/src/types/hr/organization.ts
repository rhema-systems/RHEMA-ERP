import type { AuditFields } from './common';

// Mirrors OrganizationStructureDto (ErpSystem.Core.DTOs.HR.OrganizationStructureDTOs).
export interface OrganizationStructure extends AuditFields {
  tenantId: string;
  name: string;
  code: string;
  isDefault: boolean;
  description?: string | null;
  isActive: boolean;
}

// Mirrors OrganizationStructureSummaryDto.
export interface OrganizationStructureSummary {
  id: string;
  name: string;
  code: string;
  isDefault: boolean;
  isActive: boolean;
}

// Mirrors CreateOrganizationStructureDto.
export interface CreateOrganizationStructureRequest {
  name: string;
  code: string;
  isDefault: boolean;
  description?: string | null;
  isActive: boolean;
}

// Mirrors UpdateOrganizationStructureDto (adds Id).
export interface UpdateOrganizationStructureRequest extends CreateOrganizationStructureRequest {
  id: string;
}

// Mirrors OrganizationLevelDto.
export interface OrganizationLevel extends AuditFields {
  tenantId: string;
  name: string;
  code: string;
  description?: string | null;
  levelNumber: number;
  isRootLevel: boolean;
  requiresHead: boolean;
  allowsDirectEmployees: boolean;
  isLocked: boolean;
  isActive: boolean;
  structureId: string;
  structureName?: string | null;
}

// Mirrors CreateOrganizationLevelDto.
export interface CreateOrganizationLevelRequest {
  name: string;
  code: string;
  description?: string | null;
  levelNumber: number;
  requiresHead: boolean;
  allowsDirectEmployees: boolean;
  isActive: boolean;
  structureId: string;
}

// Mirrors UpdateOrganizationLevelDto (adds Id; PUT route id must match).
export interface UpdateOrganizationLevelRequest extends CreateOrganizationLevelRequest {
  id: string;
}

// Mirrors OrganizationUnitDto.
export interface OrganizationUnit extends AuditFields {
  tenantId: string;
  name: string;
  code: string;
  accountCode?: string | null;
  /** Round 2, lane B2: the chart-of-accounts row; accountCode above is its snapshot. */
  financeAccountId?: string | null;
  description?: string | null;
  organizationLevelId: string;
  levelName?: string | null;
  parentUnitId?: string | null;
  parentUnitName?: string | null;
  headEmployeeId?: string | null;
  headEmployeeName?: string | null;
  sequence: number;
  path: string;
  isActive: boolean;
}

// Mirrors OrganizationUnitSummaryDto.
export interface OrganizationUnitSummary {
  id: string;
  name: string;
  code: string;
  organizationLevelId: string;
  levelName?: string | null;
  /**
   * The level's tier number and structure, and the unit's `/ancestor/…/self` path — what the
   * cascading picker needs to rank units and exclude a subtree without a second read (round 2, X-10).
   */
  levelNumber: number;
  structureId: string;
  parentUnitId?: string | null;
  parentUnitName?: string | null;
  path: string;
  isActive: boolean;
}

// Mirrors CreateOrganizationUnitDto.
export interface CreateOrganizationUnitRequest {
  name: string;
  code: string;
  accountCode?: string | null;
  /** Round 2, lane B2: the chart-of-accounts row; accountCode above is its snapshot. */
  financeAccountId?: string | null;
  description?: string | null;
  organizationLevelId: string;
  parentUnitId?: string | null;
  headEmployeeId?: string | null;
  sequence: number;
  isActive: boolean;
  /**
   * The initial placement's history row (round 2, O-3a/O-3b). Creating a unit now writes one —
   * two, when a head is named — and these are the user's dates, reason and notes for it.
   * `effectiveFrom` defaults to today on the server.
   */
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  changeReason?: string | null;
  notes?: string | null;
}

// Mirrors UpdateOrganizationUnitDto (adds Id; level is immutable server-side).
/**
 * Reparents a unit. `newParentId: null` moves it to the root, which only a root-level unit may do
 * and only while no other root exists in its structure.
 *
 * ⚠ Distinct from {@link UpdateOrganizationUnitRequest} on purpose, even though the edit form can
 * perform the same move. The edit form carries ONE reason for a save that may change both the
 * parent and the head, and the change log records those as two independent series — so a save that
 * did both stamped the same sentence on two unrelated rows. These two commands each carry the
 * reason for the one act they perform.
 */
export interface MoveUnitRequest {
  newParentId?: string | null;
  changeReason: string;
  /** When the move took effect (today if omitted), when it ended if already known, and notes. */
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  notes?: string | null;
}

/** Appoints, replaces or (where the level permits) removes a unit's head. */
export interface ChangeUnitHeadRequest {
  newHeadEmployeeId?: string | null;
  changeReason: string;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  notes?: string | null;
}

export interface UpdateOrganizationUnitRequest extends CreateOrganizationUnitRequest {
  id: string;
  /**
   * Why the unit was reparented or given a different head, recorded on the change log.
   *
   * ⚠ The server has accepted this since slice 3 and this form never sent it, so every row the
   * only working write path produced carried `changeReason: null` — an audit trail able to record
   * what changed and when, but never why. It is ignored on a plain rename, because the server does
   * not write a history row for one.
   *
   * `effectiveFrom`, `effectiveTo` and `notes` are inherited from the create request and mean the
   * same here: the dates and notes for the history row(s) this update writes.
   */
  changeReason?: string | null;
}

// ── the organisation-unit change log ────────────────────────────────────────

/**
 * Derived server-side from the four nullable ids — see `OrganizationUnitChangeTypes`. A row is a
 * `Restructure` when the unit's parent moved and a `Leadership Change` when its head did; the two
 * are recorded as separate rows even when one edit caused both.
 */
export type OrganizationUnitChangeType = 'Restructure' | 'Leadership Change' | 'Other';

// Mirrors OrganizationUnitHistoryDto.
export interface OrganizationUnitHistoryEntry extends AuditFields {
  tenantId: string;
  organizationUnitId: string;
  organizationUnitName?: string | null;
  previousParentId?: string | null;
  previousParentName?: string | null;
  newParentId?: string | null;
  newParentName?: string | null;
  previousHeadEmployeeId?: string | null;
  previousHeadEmployeeName?: string | null;
  newHeadEmployeeId?: string | null;
  newHeadEmployeeName?: string | null;
  /** The day the arrangement this row describes took effect. */
  effectiveFrom: string;
  /** The day a later change of the SAME kind superseded it; null while it still stands. */
  effectiveTo?: string | null;
  changeReason?: string | null;
  /** Anything beyond the reason: the memo reference, the board minute, who approved it. */
  notes?: string | null;
  changeType: OrganizationUnitChangeType;
}

/**
 * An entry recorded by hand (round 2, O-3b). Moves no parent and appoints no head, so it
 * classifies as `Other`; the reason is required for exactly that reason. Admin-tier.
 */
export interface CreateOrganizationUnitHistoryRequest {
  organizationUnitId: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  changeReason: string;
  notes?: string | null;
}

/** Corrects a row's dates, reason and notes. What the row says changed is not editable. */
export interface UpdateOrganizationUnitHistoryRequest {
  id: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  changeReason?: string | null;
  notes?: string | null;
}

// Query for GET api/OrganizationUnitHistory/paged. An unrecognised changeType is refused with a
// 400 rather than ignored, so never widen this to `string`.
export interface OrganizationUnitHistoryQuery {
  pageNumber?: number;
  pageSize?: number;
  unitId?: string;
  startDate?: string;
  endDate?: string;
  changeType?: OrganizationUnitChangeType;
}
