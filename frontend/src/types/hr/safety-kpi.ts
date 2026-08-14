// Types for HR Area 10 slice 14: the unified corrective-action tracker (FR-SHE-245)
// and computed KPIs (FR-SHE-248/230/232, FR-CON-001).
// Mirrors ErpSystem.Core.DTOs.HR.SafetyKpiCorrectiveActionDTOs. Backend routes:
// api/safety/corrective-actions and api/safety/performance/{compute,kpis/*}.

import type { SheCorrectiveActionPriority } from './safety-incidents';
import type { SheSnapshotPeriodType } from './safety';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

// ── Unified corrective-action tracker ────────────────────────────────────────

export type SheCorrectiveActionSource = 'Incident' | 'Inspection' | 'Equipment' | 'Committee';

export const SHE_CORRECTIVE_ACTION_SOURCE_OPTIONS = opts<SheCorrectiveActionSource>([
  ['Incident', 'Incident investigation'],
  ['Inspection', 'Workplace inspection'],
  ['Equipment', 'Safety equipment'],
  ['Committee', 'Committee meeting'],
]);

/** Lifecycle stage normalised across the silos' own status enums. A stored "Overdue"
 * status maps to Open — overdue-ness is recomputed from the due date on every read. */
export type SheUnifiedActionStatus = 'Open' | 'InProgress' | 'Completed' | 'Cancelled';

export const SHE_UNIFIED_ACTION_STATUS_OPTIONS = opts<SheUnifiedActionStatus>([
  ['Open', 'Open'],
  ['InProgress', 'In Progress'],
  ['Completed', 'Completed'],
  ['Cancelled', 'Cancelled'],
]);

export interface SheUnifiedCorrectiveAction {
  /** Id of the row in its own silo table. */
  id: string;
  source: SheCorrectiveActionSource;
  sourceName: string;
  parentId: string;
  /** Incident number, inspection number, equipment number + name, or committee + meeting number. */
  parentReference: string;
  /** Route of the parent's detail screen. */
  parentPath: string;
  description: string;
  /** Null for silos that carry no priority (inspection and equipment actions). */
  priority?: SheCorrectiveActionPriority | null;
  priorityName?: string | null;
  status: SheUnifiedActionStatus;
  statusName: string;
  /** The silo's own status value, verbatim. */
  rawStatus: string;
  assignedToId?: string | null;
  assignedToName?: string | null;
  dueDate?: string | null;
  completionDate?: string | null;
  isOverdue: boolean;
  daysOverdue?: number | null;
  /** 1 (≤7 days), 2 (≤30), 3 (beyond); 0 when not overdue. */
  escalationTier: number;
  /** Incident CAs only. */
  effectivenessVerified?: boolean | null;
  createdAt: string;
}

export interface SheUnifiedCorrectiveActionSourceSummary {
  total: number;
  open: number;
  overdue: number;
  completed: number;
}

export interface SheUnifiedCorrectiveActionSummary {
  total: number;
  open: number;
  inProgress: number;
  completed: number;
  cancelled: number;
  overdue: number;
  overdueTier1: number;
  overdueTier2: number;
  overdueTier3: number;
  dueWithin7Days: number;
  withoutDueDate: number;
  bySource: Record<string, SheUnifiedCorrectiveActionSourceSummary>;
}

export interface SheCorrectiveActionFilters {
  source?: SheCorrectiveActionSource;
  status?: SheUnifiedActionStatus;
  assignedToId?: string;
  overdueOnly?: boolean;
  dueFrom?: string;
  dueTo?: string;
}

// ── Computed KPIs ────────────────────────────────────────────────────────────

/** Every figure the KPI engine computes for one period + optional location. Frequency
 * rates are null without man-hours; averages are null with nothing to average; PPE
 * compliance is null until the PPE matrix's job-role codes match position codes. */
export interface SheComputedKpis {
  periodType: SheSnapshotPeriodType;
  periodTypeName: string;
  year: number;
  periodNumber?: number | null;
  locationId?: string | null;
  periodStart: string;
  periodEnd: string;
  manHoursUsed: number;

  totalAccidents: number;
  totalIncidents: number;
  totalNearMisses: number;
  totalDangerousOccurrences: number;
  totalFatalities: number;
  totalLostTimeInjuries: number;
  totalLostDays: number;

  lostTimeInjuryFrequencyRate?: number | null;
  totalRecordableIncidentRate?: number | null;
  nearMissFrequencyRate?: number | null;

  inspectionsConducted: number;
  inspectionsOverdue: number;
  averageInspectionComplianceScore?: number | null;
  housekeepingComplianceRating?: number | null;

  correctiveActionsIssued: number;
  correctiveActionsCompleted: number;
  correctiveActionsOverdue: number;
  correctiveActionClosureRate?: number | null;

  trainingProgramsPlanned: number;
  trainingProgramsConducted: number;
  totalTrainingHours: number;
  trainingCompletionRate?: number | null;

  contractorsOnSite: number;
  contractorInspectionsConducted: number;
  contractorNonComplianceNoticesIssued: number;
  contractorComplianceRate?: number | null;

  environmentalIncidents: number;
  environmentalIncidentsReportedToEpa: number;
  wasteRecyclingRate?: number | null;

  emergencyDrillsConducted: number;
  fireDrillObjectivesMetRate?: number | null;

  ppeComplianceRate?: number | null;
  ppeEmployeesAssessed: number;

  regulatoryObligationsTotal: number;
  regulatoryObligationsCompliant: number;
  regulatoryObligationsNonCompliant: number;
  regulatoryObligationsExpiringSoon: number;
}

/** Per-organization-unit compliance picture for a period (FR-SHE-230). */
export interface SheDepartmentalCompliance {
  organizationUnitId?: string | null;
  organizationUnitName: string;
  inspectionsConducted: number;
  inspectionsScored: number;
  averageComplianceScore?: number | null;
  incidents: number;
  lostTimeInjuries: number;
  openInspectionFindings: number;
}

/** Contractor SHE ranking for a period (FR-CON-001), best average score first. */
export interface SheContractorRanking {
  contractorId: string;
  contractorCode: string;
  companyName: string;
  preQualificationScore?: number | null;
  inspectionsConducted: number;
  inspectionsScored: number;
  averageComplianceScore?: number | null;
  nonComplianceNoticesIssued: number;
  openNonCompliances: number;
  rank: number;
}

/** 5×5 likelihood × severity counts over the active hazard register (FR-SHE-232).
 * Grids are [likelihood-1][severity-1] → count. */
export interface SheHazardHeatmap {
  inherent: number[][];
  residual: number[][];
  totalHazards: number;
  excludedOutOfRange: number;
}
