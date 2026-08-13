// Types for HR Area 10 (SHE) slice 6: PPE — the type catalogue, stock inventory with restock
// and reorder levels, issuance to employees (and return), and the job-role requirement matrix.
// Mirrors ErpSystem.Core.DTOs.HR.SafetyPermitPpeEquipmentDTOs (region F).
// Backend route: api/safety/ppe.
//
// ⚠ Nothing here is automatic yet: no reorder or expiry ALERTS fire (that is the slice-13 job
// engine) — `below-reorder` is a query the screens poll, not a notification. The PPE-compliance %
// on performance snapshots is hand-reported, not computed from this data (slice 14).

import type { AuditFields } from './common';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

// ── Enums (serialize as strings) ──────────────────────────────────────────────

export type ShePpeCategory =
  | 'HeadProtection'
  | 'EyeFaceProtection'
  | 'HearingProtection'
  | 'RespiratoryProtection'
  | 'HandProtection'
  | 'FootProtection'
  | 'BodyProtection'
  | 'FallProtection'
  | 'HighVisibility'
  | 'Combination';

export const SHE_PPE_CATEGORY_OPTIONS = opts<ShePpeCategory>([
  ['HeadProtection', 'Head Protection'],
  ['EyeFaceProtection', 'Eye / Face Protection'],
  ['HearingProtection', 'Hearing Protection'],
  ['RespiratoryProtection', 'Respiratory Protection'],
  ['HandProtection', 'Hand Protection'],
  ['FootProtection', 'Foot Protection'],
  ['BodyProtection', 'Body Protection'],
  ['FallProtection', 'Fall Protection'],
  ['HighVisibility', 'High Visibility'],
  ['Combination', 'Combination'],
]);

export type ShePpeCondition = 'New' | 'Good' | 'Fair' | 'Worn' | 'Damaged' | 'Condemned';

export const SHE_PPE_CONDITION_OPTIONS = opts<ShePpeCondition>([
  ['New', 'New'],
  ['Good', 'Good'],
  ['Fair', 'Fair'],
  ['Worn', 'Worn'],
  ['Damaged', 'Damaged'],
  ['Condemned', 'Condemned'],
]);

// ── PPE types (the catalogue) ─────────────────────────────────────────────────

export interface PpeType extends AuditFields {
  tenantId: string;
  code: string;
  name: string;
  description?: string | null;
  category: ShePpeCategory;
  categoryName: string;
  /** Conformance standard, e.g. "EN 397", "ANSI Z87.1". */
  standard?: string | null;
  lifespanMonths?: number | null;
  requiresSerialNumber: boolean;
  hasExpiryDate: boolean;
  isActive: boolean;
}

export interface PpeTypeCreateRequest {
  code: string;
  name: string;
  description?: string | null;
  category: ShePpeCategory;
  standard?: string | null;
  lifespanMonths?: number | null;
  requiresSerialNumber: boolean;
  hasExpiryDate: boolean;
  isActive: boolean;
}

export interface PpeTypeUpdateRequest {
  id: string;
  name: string;
  description?: string | null;
  category: ShePpeCategory;
  standard?: string | null;
  lifespanMonths?: number | null;
  requiresSerialNumber: boolean;
  hasExpiryDate: boolean;
  isActive: boolean;
}

// ── Inventory ─────────────────────────────────────────────────────────────────

export interface PpeInventory extends AuditFields {
  tenantId: string;
  ppeTypeId: string;
  ppeTypeName: string;
  itemCode: string;
  brand: string;
  model: string;
  size?: string | null;
  quantityInStock: number;
  minimumStockLevel: number;
  reorderLevel: number;
  isBelowReorderLevel: boolean;
  storageLocation?: string | null;
  unitCost?: number | null;
  supplier?: string | null;
  lastRestockDate?: string | null;
  lastRestockedById?: string | null;
  lastRestockedByName?: string | null;
}

export interface PpeInventoryCreateRequest {
  ppeTypeId: string;
  itemCode: string;
  brand: string;
  model: string;
  size?: string | null;
  quantityInStock: number;
  minimumStockLevel: number;
  reorderLevel: number;
  storageLocation?: string | null;
  unitCost?: number | null;
  supplier?: string | null;
}

export interface PpeInventoryUpdateRequest {
  id: string;
  brand: string;
  model: string;
  size?: string | null;
  quantityInStock: number;
  minimumStockLevel: number;
  reorderLevel: number;
  storageLocation?: string | null;
  unitCost?: number | null;
  supplier?: string | null;
}

export interface PpeInventoryRestockRequest {
  ppeInventoryId: string;
  quantity: number;
  restockedById: string;
  restockDate?: string;
}

// ── Issuance ──────────────────────────────────────────────────────────────────

export interface PpeIssuance extends AuditFields {
  tenantId: string;
  employeeId: string;
  employeeName: string;
  employeeNumber?: string | null;
  ppeTypeId: string;
  ppeTypeName: string;
  issueDate: string;
  quantity: number;
  size?: string | null;
  serialNumber?: string | null;
  expiryDate?: string | null;
  expectedReturnDate?: string | null;
  actualReturnDate?: string | null;
  conditionWhenIssued?: ShePpeCondition | null;
  conditionWhenIssuedName?: string | null;
  conditionWhenReturned?: ShePpeCondition | null;
  conditionWhenReturnedName?: string | null;
  issuedById: string;
  issuedByName: string;
  isReturned: boolean;
  returnedToId?: string | null;
  returnedToName?: string | null;
  notes?: string | null;
}

export interface PpeIssuanceCreateRequest {
  employeeId: string;
  ppeTypeId: string;
  issueDate?: string;
  quantity: number;
  size?: string | null;
  serialNumber?: string | null;
  expiryDate?: string | null;
  expectedReturnDate?: string | null;
  conditionWhenIssued?: ShePpeCondition | null;
  issuedById: string;
  notes?: string | null;
}

export interface PpeIssuanceReturnRequest {
  issuanceId: string;
  actualReturnDate?: string;
  conditionWhenReturned?: ShePpeCondition | null;
  returnedToId: string;
  notes?: string | null;
}

// ── Job-role requirement matrix ───────────────────────────────────────────────

export interface JobRolePpeRequirement extends AuditFields {
  tenantId: string;
  jobRoleCode: string;
  jobRoleName: string;
  ppeTypeId: string;
  ppeTypeName: string;
  quantity: number;
  replacementFrequencyMonths: number;
  isMandatory: boolean;
}

export interface JobRolePpeRequirementCreateRequest {
  jobRoleCode: string;
  jobRoleName: string;
  ppeTypeId: string;
  quantity: number;
  replacementFrequencyMonths: number;
  isMandatory: boolean;
}

export interface JobRolePpeRequirementUpdateRequest {
  id: string;
  jobRoleName: string;
  quantity: number;
  replacementFrequencyMonths: number;
  isMandatory: boolean;
}
