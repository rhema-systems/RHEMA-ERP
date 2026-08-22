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
  parentUnitId?: string | null;
  parentUnitName?: string | null;
  isActive: boolean;
}

// Mirrors CreateOrganizationUnitDto.
export interface CreateOrganizationUnitRequest {
  name: string;
  code: string;
  accountCode?: string | null;
  description?: string | null;
  organizationLevelId: string;
  parentUnitId?: string | null;
  headEmployeeId?: string | null;
  sequence: number;
  isActive: boolean;
}

// Mirrors UpdateOrganizationUnitDto (adds Id; level is immutable server-side).
export interface UpdateOrganizationUnitRequest extends CreateOrganizationUnitRequest {
  id: string;
  /**
   * Why the unit was reparented or given a different head, recorded on the change log.
   *
   * ⚠ The server has accepted this since slice 3 and this form never sent it, so every row the
   * only working write path produced carried `changeReason: null` — an audit trail able to record
   * what changed and when, but never why. It is ignored on a plain rename, because the server does
   * not write a history row for one.
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
  changeType: OrganizationUnitChangeType;
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
