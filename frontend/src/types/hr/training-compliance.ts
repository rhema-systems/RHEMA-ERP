// Types for HR Training & Learning — Slice 5 (Compliance).
//
// A *requirement* says a population must hold a given training (all Drivers, everyone in Operations,
// anyone at level 3). Assigning it to a person produces a *record*, which is the thing that goes
// overdue, gets fulfilled by a nomination, or gets exempted.
//
// Mirrors ComplianceTrainingRequirementDto / EmployeeComplianceRecordDto in TrainingDTOs.cs.

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

// ── Enums ──────────────────────────────────────────────────────────────────────────────────

export type ComplianceFrequency =
  | 'OneTime'
  | 'Annual'
  | 'BiAnnual'
  | 'Quarterly'
  | 'Monthly'
  | 'Custom';

export const COMPLIANCE_FREQUENCY_OPTIONS = opts<ComplianceFrequency>([
  ['OneTime', 'One-off'],
  ['Annual', 'Annually'],
  ['BiAnnual', 'Twice a year'],
  ['Quarterly', 'Quarterly'],
  ['Monthly', 'Monthly'],
  ['Custom', 'Custom interval'],
]);

export type ComplianceStatus =
  | 'Compliant'
  | 'NonCompliant'
  | 'PartiallyCompliant'
  | 'NotApplicable';

export const COMPLIANCE_STATUS_OPTIONS = opts<ComplianceStatus>([
  ['Compliant', 'Compliant'],
  ['NonCompliant', 'Non-Compliant'],
  ['PartiallyCompliant', 'Partially Compliant'],
  ['NotApplicable', 'Not Applicable'],
]);

// ── Requirement ────────────────────────────────────────────────────────────────────────────

export interface ComplianceTrainingRequirement {
  id: string;
  requirementCode: string;
  requirementName: string;
  description?: string | null;
  /** The regulation or policy this exists to satisfy — the reason it cannot simply be waived. */
  regulatoryReference?: string | null;
  programId: string;
  programCode: string;
  programName: string;
  /** Scope: any combination of level, unit and position. All null means organisation-wide. */
  organizationLevelId?: string | null;
  organizationLevelName?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  positionId?: string | null;
  positionTitle?: string | null;
  frequency: ComplianceFrequency;
  /** Only meaningful when frequency is Custom. */
  customFrequencyDays?: number | null;
  gracePeriodDays?: number | null;
  isActive: boolean;
  effectiveDate: string;
  expiryDate?: string | null;
  nonComplianceConsequences?: string | null;
  totalAssignedEmployees: number;
  compliantEmployeesCount: number;
  /** Server-derived percentage; 0 when nobody is assigned. */
  complianceRate: number;
}

export interface ComplianceTrainingRequirementSummary {
  id: string;
  requirementCode: string;
  requirementName: string;
  programId: string;
  programName: string;
  frequency: ComplianceFrequency;
  isActive: boolean;
  effectiveDate: string;
  totalAssignedEmployees: number;
  complianceRate: number;
}

export interface ComplianceRequirementCreate {
  requirementCode: string;
  requirementName: string;
  description?: string | null;
  regulatoryReference?: string | null;
  programId: string;
  organizationLevelId?: string | null;
  organizationUnitId?: string | null;
  positionId?: string | null;
  frequency: ComplianceFrequency;
  customFrequencyDays?: number | null;
  gracePeriodDays?: number | null;
  isActive: boolean;
  effectiveDate: string;
  expiryDate?: string | null;
  nonComplianceConsequences?: string | null;
}

export type ComplianceRequirementUpdate = ComplianceRequirementCreate;

// ── Employee record ────────────────────────────────────────────────────────────────────────

export interface EmployeeComplianceRecord {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  requirementId: string;
  requirementCode: string;
  requirementName: string;
  programName: string;
  status: ComplianceStatus;
  assignedDate: string;
  lastCompletedDate?: string | null;
  nextDueDate?: string | null;
  /** How long past the due date before it truly counts as a breach. */
  gracePeriodExpiry?: string | null;
  isOverdue: boolean;
  /** The nomination that satisfied it — fulfilment is evidenced, not asserted. */
  fulfillingNominationId?: string | null;
  fulfillingNominationNumber?: string | null;
  isExempt: boolean;
  exemptionReason?: string | null;
  exemptedById?: string | null;
  exemptedByName?: string | null;
  exemptionDate?: string | null;
  exemptionExpiryDate?: string | null;
  reminderSentDate?: string | null;
  notes?: string | null;
}

export interface EmployeeComplianceRecordSummary {
  id: string;
  /** Ids added so the org-wide queues can link to both sides of the row. */
  employeeId: string;
  requirementId: string;
  employeeName: string;
  requirementName: string;
  /** Added: the queues show which programme satisfies it, and who granted an exemption. */
  programName: string;
  exemptedByName?: string | null;
  status: ComplianceStatus;
  nextDueDate?: string | null;
  isOverdue: boolean;
  isExempt: boolean;
}

export interface AssignComplianceRequest {
  employeeId: string;
  requirementId: string;
}

export interface ExemptEmployeeComplianceRequest {
  recordId: string;
  exemptionReason: string;
  /** Null means the exemption does not lapse — worth being deliberate about. */
  exemptionExpiryDate?: string | null;
}

export interface FulfillComplianceRequest {
  nominationId: string;
}
