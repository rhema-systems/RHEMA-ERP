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
}
