import type { AuditFields } from './common';

// ---- Location Structure (top of the location hierarchy) ----
export interface LocationStructure extends AuditFields {
  tenantId: string;
  name: string;
  code: string;
  isDefault: boolean;
  description?: string | null;
  isActive: boolean;
}

export interface LocationStructureSummary {
  id: string;
  name: string;
  code: string;
  isDefault: boolean;
  isActive: boolean;
}

export interface CreateLocationStructureRequest {
  name: string;
  code: string;
  isDefault: boolean;
  description?: string | null;
  isActive: boolean;
}

export interface UpdateLocationStructureRequest extends CreateLocationStructureRequest {
  id: string;
}

// ---- Location Level (tiers of the location hierarchy) ----
export interface LocationLevel extends AuditFields {
  tenantId: string;
  name: string;
  code: string;
  description?: string | null;
  levelNumber: number;
  requiresAddress: boolean;
  requiresContactInfo: boolean;
  allowsEmployeeAssignment: boolean;
  isRootLevel: boolean;
  isLocked: boolean;
  isActive: boolean;
  structureId: string;
  structureName?: string | null;
}

export interface CreateLocationLevelRequest {
  name: string;
  code: string;
  description?: string | null;
  levelNumber: number;
  requiresAddress: boolean;
  requiresContactInfo: boolean;
  allowsEmployeeAssignment: boolean;
  isActive: boolean;
  structureId: string;
}

export interface UpdateLocationLevelRequest extends CreateLocationLevelRequest {
  id: string;
}

// ---- Location (actual nodes) ----
export interface Location extends AuditFields {
  tenantId: string;
  name: string;
  code: string;
  description?: string | null;
  structureId: string;
  structureName?: string | null;
  locationLevelId: string;
  levelName?: string | null;
  parentLocationId?: string | null;
  parentLocationName?: string | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  postalCode?: string | null;
  countryId?: string | null;
  countryName?: string | null;
  digitalAddress?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  faxNumber?: string | null;
  sequence: number;
  path: string;
  isActive: boolean;
}

export interface LocationSummary {
  id: string;
  name: string;
  code: string;
  levelName?: string | null;
  city?: string | null;
  countryName?: string | null;
  isActive: boolean;
}

export interface CreateLocationRequest {
  name: string;
  code: string;
  description?: string | null;
  structureId: string;
  locationLevelId: string;
  parentLocationId?: string | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  postalCode?: string | null;
  countryId?: string | null;
  digitalAddress?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  faxNumber?: string | null;
  sequence: number;
  isActive: boolean;
}

export interface UpdateLocationRequest extends CreateLocationRequest {
  id: string;
}

/**
 * Who to ring at a location.
 *
 * ⚠ Four write endpoints with no caller anywhere, and the create had **never once succeeded**:
 * the service did not stamp TenantId, so every insert died on the tenant foreign key and the
 * controller returned a bare 500. Fixed 2026-08-31; a dead path cannot fail visibly, which is why
 * it survived the port.
 */
export interface LocationContact {
  id: string;
  tenantId: string;
  locationId: string;
  locationName?: string | null;
  employeeId?: string | null;
  employeeName?: string | null;
  contactName?: string | null;
  phone?: string | null;
  email?: string | null;
  isPrimary: boolean;
}

export interface UpsertLocationContact {
  locationId: string;
  employeeId?: string | null;
  contactName?: string | null;
  phone?: string | null;
  email?: string | null;
  isPrimary: boolean;
}
