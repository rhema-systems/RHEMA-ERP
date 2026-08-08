/**
 * Calibration — area 5's fourth slice.
 *
 * A calibration session is the panel that reconciles managers' ratings across a unit before HR
 * signs the appraisals off. When a cycle's settings profile has `requireCalibration` on,
 * `isCalibrated` on the appraisal is a hard gate: an appraisal cannot reach HR review until a
 * session has been committed over it.
 *
 * Route: `api/CalibrationSessions` (per-controller, as always in HR).
 *
 * **Scope.** A session is a cycle plus, optionally, an organization unit (which includes every
 * unit beneath it) or an organization level. Everything in that scope is on the grid, adjusted
 * or not — a panel has to see who it has *not* moved. A session with neither set covers the
 * whole cycle.
 *
 * **Two kinds of adjustment.** Omit `templateItemId` to restate the overall score directly;
 * supply one to move a single criterion, which is written onto the manager's evaluation and the
 * overall score recomputed from it. Committing applies item-level adjustments first, then any
 * overall adjustment, which wins.
 *
 * **Lifecycle.** Pending → open → InProgress → start → complete → Completed → commit. Closing
 * the room and writing the ratings onto the appraisals are two decisions, and the second is
 * irreversible.
 *
 * The actor is never sent: the facilitator, the adjuster and the committer all come from the
 * token. Reads are open to any authenticated user (managers sit on the panel); writes are HR.
 *
 * Enums serialize as strings.
 */
import type { AuditFields } from './common';
import type { AppraisalStatus } from './appraisal-run';

export type CalibrationStatus = 'Pending' | 'InProgress' | 'Completed' | 'Cancelled';

export interface CalibrationSession extends AuditFields {
  tenantId: string;
  appraisalCycleId: string;
  cycleCode?: string | null;
  sessionName: string;
  organizationLevelId?: string | null;
  organizationLevelName?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  status: CalibrationStatus;
  scheduledDate?: string | null;
  startedDate?: string | null;
  completedDate?: string | null;
  facilitatedById?: string | null;
  facilitatedByName?: string | null;
  completedById?: string | null;
  completedByName?: string | null;
  agenda?: string | null;
  meetingNotes?: string | null;
}

export interface CreateCalibrationSession {
  appraisalCycleId: string;
  sessionName: string;
  organizationLevelId?: string | null;
  organizationUnitId?: string | null;
  scheduledDate?: string | null;
  facilitatedById?: string | null;
  agenda?: string | null;
}

/**
 * Particulars only. Status and the started/completed stamps belong to the lifecycle endpoints —
 * sending them here does nothing.
 */
export interface UpdateCalibrationSession extends CreateCalibrationSession {
  id: string;
  meetingNotes?: string | null;
}

export interface CompleteCalibrationSession {
  meetingNotes?: string | null;
}

// ── Participants ─────────────────────────────────────────────────────────────────

export interface CalibrationParticipant extends AuditFields {
  tenantId: string;
  calibrationSessionId: string;
  sessionName: string;
  employeeId: string;
  employeeName: string;
  /** Free text — "Manager", "HR", "Head of Department". Defaults to Manager. */
  role: string;
  attended: boolean;
}

export interface CreateCalibrationParticipant {
  calibrationSessionId: string;
  employeeId: string;
  role: string;
  attended: boolean;
}

// ── Rating adjustments ───────────────────────────────────────────────────────────

export interface CalibrationRatingAdjustment extends AuditFields {
  tenantId: string;
  calibrationSessionId: string;
  sessionName: string;
  performanceAppraisalId: string;
  appraisalNumber?: string | null;
  employeeName?: string | null;
  /** Null for an overall-score adjustment. */
  templateItemId?: string | null;
  templateItemName?: string | null;
  originalScore?: number | null;
  adjustedScore?: number | null;
  adjustedById: string;
  adjustedByName: string;
  adjustmentDate: string;
  rationale?: string | null;
}

export interface CreateCalibrationRatingAdjustment {
  performanceAppraisalId: string;
  templateItemId?: string | null;
  originalScore?: number | null;
  adjustedScore?: number | null;
  rationale?: string | null;
}

export interface UpdateCalibrationRatingAdjustment extends CreateCalibrationRatingAdjustment {
  id: string;
}

// ── The grid ─────────────────────────────────────────────────────────────────────

export interface CalibrationMatrixRow {
  appraisalId: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  positionName?: string | null;
  departmentName?: string | null;
  appraisalStatus: AppraisalStatus;
  /** The manager's own evaluation total, before any calibration. */
  managerProposedScore?: number | null;
  preCalibrationScore?: number | null;
  /** From the latest *overall* adjustment; item-level ones show in `adjustments`. */
  calibratedScore?: number | null;
  scoreAdjustment?: number | null;
  adjustmentRationale?: string | null;
  isCalibrated: boolean;
  managerName?: string | null;
  adjustments: CalibrationRatingAdjustment[];
}

export interface CalibrationMatrix {
  sessionId: string;
  sessionName: string;
  sessionStatus: CalibrationStatus;
  organizationUnitName?: string | null;
  organizationLevelName?: string | null;
  totalEmployees: number;
  /** Rows carrying at least one recorded adjustment. */
  adjustedCount: number;
  /** Rows already through the calibration gate. */
  calibratedCount: number;
  averageScore?: number | null;
  rows: CalibrationMatrixRow[];
}

/**
 * What committing did. `appraisalsCalibrated` counts everyone in scope, not only the adjusted —
 * an employee the panel discussed and left alone is still calibrated.
 */
export interface CalibrationApplyResult {
  adjustmentsApplied: number;
  scoresChanged: number;
  appraisalsCalibrated: number;
}

// ── Attachments ──────────────────────────────────────────────────────────────────

export interface AppraisalAttachment extends AuditFields {
  tenantId: string;
  appraisalId: string;
  fileName: string;
  filePath: string;
  description?: string | null;
  uploadDate: string;
}

/**
 * ⚠ `appraisalId` is required by the DTO but ignored for a calibration attachment — the service
 * overwrites the link with the session id and stamps the entity type. Send an empty GUID.
 */
export interface CreateAppraisalAttachment {
  appraisalId: string;
  fileName: string;
  filePath: string;
  description?: string | null;
}
