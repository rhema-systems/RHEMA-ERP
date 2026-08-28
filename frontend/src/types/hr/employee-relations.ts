/**
 * Employee relations — HR area 9c. Backend route: `api/hr/employee-relations`.
 *
 * This supersedes `types/hr/grievance.ts`, which now re-exports from here. The register holds
 * mediations, welfare matters and union consultations as well as FR-HR-181 grievances, so
 * "grievance" was the wrong name for the module the moment slice 1 landed.
 *
 * ⚠ **The READ shapes were written from PROBED RESPONSES, not from the C# DTOs read by eye.**
 * `probe-slice10.mjs` and `probe-slice10b.mjs` in `dev-harness/hr-employee-relations` walk the
 * whole register, find a non-empty example of every collection and print it. That mattered: the
 * payloads carry computed fields — `displayName`, `isActive`, `isComplete`, `isOverdue`,
 * `outcomeIsMissing`, `agreementAccepted`, `notesRedacted` — which are C# expression-bodied
 * properties and are easy to miss transcribing a class. Area 12 shipped a type written from an
 * endpoint's name; it type-checked perfectly and described a response the server never sent.
 *
 * ⚠ **The WRITE shapes come from the DTOs, because no probe can show them.** That distinction is
 * not academic: two of them were wrong when first written from memory — `HoldConferenceRequest`
 * had an optional outcome and a `heldDate` that does not exist, and `UpdateConferenceRequest` was
 * missing the chair.
 *
 * ⚠ **The unions come from `HREnums.cs`** and list EVERY member — not just the ones the probe
 * happened to see. A union built from observed values silently excludes whatever the seed data
 * lacks.
 *
 * ⚠ **One exception, and it is flagged where it sits: `EmployeeRelationsResponder`.** The matrix
 * holds zero rows on this tenant, so there was nothing to probe and it came from the DTO alone.
 */

// ─────────────────────────────────────────────────────────────────────────────
//  Unions — from HREnums.cs, complete
// ─────────────────────────────────────────────────────────────────────────────

/** Which kind of employee-relations case. Grievance is 1, so every pre-9c row is one. */
export type EmployeeRelationsCaseType =
  | 'Grievance'
  | 'ConflictMediation'
  | 'WelfareCounselling'
  | 'UnionConsultation'
  | 'Other';

/**
 * FR-HR-181's ladder. The employee is the origin, not a rung — these are the levels a case is
 * ANSWERED at, in the order the requirement names them.
 *
 * ⚠ Since slice 5 a rung CAN resolve to a person, but only where the responder matrix has been
 * filled in for that org unit. It resolves to nobody far more often than not, and a case with
 * nobody named is normal rather than broken. Do not build UI that implies the system always knows
 * who someone's HOD is.
 */
export type GrievanceEscalationLevel =
  | 'Supervisor'
  | 'HeadOfDepartment'
  | 'HumanResources'
  | 'GeneralManagerFinanceAdmin'
  | 'ManagingDirector'
  | 'Board';

export type GrievanceStatus =
  | 'Filed' | 'UnderReview' | 'Escalated' | 'Resolved' | 'Withdrawn' | 'Closed';

export type GrievanceStepOutcome = 'AwaitingResponse' | 'Resolved' | 'Escalated';

/** Everybody on a case other than the primary party, who is never repeated here. */
export type GrievancePartyRole =
  | 'AffectedEmployee'
  | 'Respondent'
  | 'Representative'
  | 'UnionRepresentative'
  | 'Witness'
  | 'Mediator';

/** Where on the case file a document belongs. */
export type GrievanceDocumentScope =
  | 'Case' | 'Step' | 'Investigation' | 'Agreement' | 'Conference';

export type GrievanceConferenceType = 'CaseConference' | 'Mediation' | 'UnionConsultation';

export type GrievanceConferenceStatus = 'Scheduled' | 'Held' | 'Cancelled';

/**
 * The decision recorded on a resolved case.
 *
 * ⚠ `NotRecorded` **cannot be chosen**. It is what a case gets when it was resolved through the
 * older `respond(resolvesGrievance: true)` path, which had no outcome to record. Never offer it in
 * a picker; do surface it, because a resolved case with no recorded outcome is a real gap.
 */
export type GrievanceResolutionOutcome =
  | 'NotRecorded'
  | 'UpheldInFull'
  | 'UpheldInPart'
  | 'NotUpheld'
  | 'SettledByAgreement'
  | 'NoFurtherAction';

/** The kinds of record a case can be cross-referenced to — slice 9. */
export type EmployeeRelationsLinkSource =
  | 'SafetyIncident'
  | 'PerformanceImprovementPlan'
  | 'DisciplinaryCase';

// ─────────────────────────────────────────────────────────────────────────────
//  Option lists — the labels the UI shows
// ─────────────────────────────────────────────────────────────────────────────

export const ER_CASE_TYPE_OPTIONS: { value: EmployeeRelationsCaseType; label: string }[] = [
  { value: 'Grievance', label: 'Grievance' },
  { value: 'ConflictMediation', label: 'Conflict / mediation' },
  { value: 'WelfareCounselling', label: 'Welfare / counselling' },
  { value: 'UnionConsultation', label: 'Union consultation' },
  { value: 'Other', label: 'Other' },
];

/**
 * The case types the desk may OPEN. Grievance is deliberately absent: a grievance is the
 * employee's own complaint and the server refuses to open one here, because that would be the
 * raise-on-behalf-of that `file` exists to prevent.
 */
export const ER_OPENABLE_CASE_TYPES = ER_CASE_TYPE_OPTIONS.filter((o) => o.value !== 'Grievance');

export const GRIEVANCE_LADDER: { value: GrievanceEscalationLevel; label: string }[] = [
  { value: 'Supervisor', label: 'Supervisor' },
  { value: 'HeadOfDepartment', label: 'Head of department' },
  { value: 'HumanResources', label: 'Human Resources' },
  { value: 'GeneralManagerFinanceAdmin', label: 'GM Finance & Administration' },
  { value: 'ManagingDirector', label: 'Managing Director' },
  { value: 'Board', label: 'Board' },
];

export const GRIEVANCE_STATUS_OPTIONS: { value: GrievanceStatus; label: string }[] = [
  { value: 'Filed', label: 'Filed' },
  { value: 'UnderReview', label: 'Under review' },
  { value: 'Escalated', label: 'Escalated' },
  { value: 'Resolved', label: 'Resolved' },
  { value: 'Withdrawn', label: 'Withdrawn' },
  { value: 'Closed', label: 'Closed unresolved' },
];

export const GRIEVANCE_PARTY_ROLE_OPTIONS: { value: GrievancePartyRole; label: string }[] = [
  { value: 'AffectedEmployee', label: 'Affected employee' },
  { value: 'Respondent', label: 'Respondent' },
  { value: 'Representative', label: 'Representative' },
  { value: 'UnionRepresentative', label: 'Union representative' },
  { value: 'Witness', label: 'Witness' },
  { value: 'Mediator', label: 'Mediator' },
];

export const CONFERENCE_TYPE_OPTIONS: { value: GrievanceConferenceType; label: string }[] = [
  { value: 'CaseConference', label: 'Case conference' },
  { value: 'Mediation', label: 'Mediation' },
  { value: 'UnionConsultation', label: 'Union consultation' },
];

/** Outcomes the desk may choose. `NotRecorded` is excluded — see the union's note. */
export const RESOLUTION_OUTCOME_OPTIONS: { value: GrievanceResolutionOutcome; label: string }[] = [
  { value: 'UpheldInFull', label: 'Upheld in full' },
  { value: 'UpheldInPart', label: 'Upheld in part' },
  { value: 'NotUpheld', label: 'Not upheld' },
  { value: 'SettledByAgreement', label: 'Settled by agreement' },
  { value: 'NoFurtherAction', label: 'No further action' },
];

export const DOCUMENT_SCOPE_OPTIONS: { value: GrievanceDocumentScope; label: string }[] = [
  { value: 'Case', label: 'Case file' },
  { value: 'Step', label: 'Ladder step' },
  { value: 'Investigation', label: 'Investigation' },
  { value: 'Agreement', label: 'Signed agreement' },
  { value: 'Conference', label: 'Conference' },
];

export const LINK_SOURCE_OPTIONS: { value: EmployeeRelationsLinkSource; label: string }[] = [
  { value: 'SafetyIncident', label: 'Safety incident' },
  { value: 'PerformanceImprovementPlan', label: 'Performance improvement plan' },
  { value: 'DisciplinaryCase', label: 'Disciplinary case' },
];

/** Statuses that mean the case is finished; the ladder can do nothing more to it. */
export const SETTLED_GRIEVANCE_STATUSES: GrievanceStatus[] = ['Resolved', 'Withdrawn', 'Closed'];

// ─────────────────────────────────────────────────────────────────────────────
//  Read shapes — probed
// ─────────────────────────────────────────────────────────────────────────────

export interface GrievanceStep {
  id: string;
  grievanceId: string;
  level: GrievanceEscalationLevel;
  levelName: string;
  sequence: number;
  reachedDate: string;
  assignedToId?: string | null;
  assignedToName?: string | null;
  response?: string | null;
  respondedDate?: string | null;
  respondedById?: string | null;
  respondedByName?: string | null;
  outcome: GrievanceStepOutcome;
  outcomeName: string;
}

export interface GrievanceParty {
  id: string;
  grievanceId: string;
  role: GrievancePartyRole;
  roleName: string;
  /** Null for an external party — a union official or a lawyer who is not on the payroll. */
  employeeId?: string | null;
  employeeName?: string | null;
  externalName?: string | null;
  externalOrganisation?: string | null;
  /** Server-computed: the employee's name or the external name, whichever this party has. */
  displayName: string;
  representsEmployeeId?: string | null;
  representsEmployeeName?: string | null;
  unionId?: string | null;
  unionName?: string | null;
  addedDate: string;
  addedById?: string | null;
  addedByName?: string | null;
  notes?: string | null;
  /** Standing down is not a delete: the row stays on the file with a date and a reason. */
  removedDate?: string | null;
  removalReason?: string | null;
  /** Server-computed: `removedDate == null`. */
  isActive: boolean;
}

export interface GrievanceInvestigation {
  id: string;
  grievanceId: string;
  /** Null when the investigator is external — which is exactly the senior-management case. */
  investigatorId?: string | null;
  investigatorName?: string | null;
  externalInvestigatorName?: string | null;
  externalInvestigatorOrganisation?: string | null;
  investigatorDisplayName: string;
  startedDate: string;
  targetDate?: string | null;
  completedDate?: string | null;
  findings?: string | null;
  evidenceCollected?: string | null;
  recommendation?: string | null;
  openedById?: string | null;
  openedByName?: string | null;
  /** Server-computed: completion is a gate, not a flag — it cannot be set without findings. */
  isComplete: boolean;
  isOverdue: boolean;
}

export interface GrievanceResolution {
  id: string;
  grievanceId: string;
  outcome: GrievanceResolutionOutcome;
  outcomeName: string;
  /** Server-computed: the outcome is `NotRecorded`, i.e. this case predates the resolve path. */
  outcomeIsMissing: boolean;
  decision: string;
  remedyOrUndertakings?: string | null;
  decidedById?: string | null;
  decidedByName?: string | null;
  decidedDate: string;
  decidedAtLevel: GrievanceEscalationLevel;
  decidedAtLevelName: string;
  outcomeRecordedDate?: string | null;
  outcomeRecordedByName?: string | null;
  agreementSignedDate?: string | null;
  agreementAcceptedDate?: string | null;
  agreementAcceptedById?: string | null;
  agreementAcceptedByName?: string | null;
  agreementAcceptanceComment?: string | null;
  /** Server-computed. The employee's acceptance, which only the employee can give. */
  agreementAccepted: boolean;
}

export interface GrievanceDocument {
  id: string;
  grievanceId: string;
  scope: GrievanceDocumentScope;
  scopeName: string;
  stepId?: string | null;
  conferenceId?: string | null;
  fileName: string;
  filePath: string;
  fileSize: number;
  description?: string | null;
  uploadDate: string;
  uploadedById?: string | null;
  uploadedByName?: string | null;
  /** The controlled-upload trail. Present because the file went through the central DMS. */
  fileUploadRecordId?: string | null;
  documentRecordId?: string | null;
  documentVersionId?: string | null;
}

export interface GrievanceConferenceAttendee {
  id: string;
  conferenceId: string;
  employeeId?: string | null;
  employeeName?: string | null;
  externalName?: string | null;
  externalOrganisation?: string | null;
  displayName: string;
  /** Free text, not an enum — "Complainant", "Union official", whatever the desk types. */
  capacity?: string | null;
  didAttend: boolean;
  apologyReason?: string | null;
}

export interface GrievanceConference {
  id: string;
  grievanceId: string;
  conferenceType: GrievanceConferenceType;
  conferenceTypeName: string;
  status: GrievanceConferenceStatus;
  statusName: string;
  scheduledFor: string;
  venue?: string | null;
  chairId?: string | null;
  chairName?: string | null;
  externalChairName?: string | null;
  externalChairOrganisation?: string | null;
  chairDisplayName: string;
  unionId?: string | null;
  unionName?: string | null;
  purpose?: string | null;
  /**
   * ⚠ Redacted per reader. `notes` arrives NULL for anyone but HR and the chair, and
   * `notesRedacted` says which of the two you are looking at — an absent note and a withheld one
   * are different facts, and a screen that renders both as blank is lying about one of them.
   */
  notes?: string | null;
  notesRedacted: boolean;
  outcome?: string | null;
  heldDate?: string | null;
  cancelledDate?: string | null;
  cancellationReason?: string | null;
  convenedById?: string | null;
  convenedByName?: string | null;
  attendees: GrievanceConferenceAttendee[];
}

/**
 * A cross-reference to the record a case arose from — slice 9.
 *
 * ⚠ **HR ONLY.** `Grievance.links` arrives as an EMPTY ARRAY for every other reader, the griever
 * included. Do not build a portal screen around it.
 */
export interface ErCaseSourceLink {
  source: EmployeeRelationsLinkSource;
  sourceLabel: string;
  recordId: string;
  /** The source's own reference — incident number, PIP number, disciplinary case number. */
  number: string;
  date: string;
  status: string;
  /** Null for a safety incident: an incident has involved persons, not one subject. */
  subjectEmployeeId?: string | null;
  subjectEmployeeName?: string | null;
  /** False once the owning module has retired the record. Render it dead, do not hide it. */
  available: boolean;
}

/** A case that points at a given source record — the reverse read. */
export interface ErLinkedCase {
  id: string;
  grievanceNumber: string;
  caseType: EmployeeRelationsCaseType;
  caseTypeName: string;
  subject: string;
  status: GrievanceStatus;
  statusName: string;
  filedDate: string;
  employeeId: string;
  employeeName: string;
}

export interface GrievanceSummary {
  id: string;
  grievanceNumber: string;
  caseType: EmployeeRelationsCaseType;
  caseTypeName: string;
  employeeId: string;
  employeeName: string;
  subject: string;
  filedDate: string;
  status: GrievanceStatus;
  statusName: string;
  currentLevel: GrievanceEscalationLevel;
  currentLevelName: string;
  /**
   * True while the rung it sits at owes an answer.
   *
   * ⚠ This is open AND unanswered. A withdrawn case's last step still reads `AwaitingResponse`,
   * and treating that as "somebody owes an answer" is the slice-8 defect: 227 cases instead of 174.
   */
  awaitingResponse: boolean;
  /** Parties besides the primary one. A count, never the rows — a register must not carry them. */
  activePartyCount: number;
}

export interface Grievance extends GrievanceSummary {
  tenantId: string;
  employeeNumber?: string | null;
  /** The employee's own words. Never amended after filing — FR-HR-181 requires it retained. */
  statement: string;
  resolvedDate?: string | null;
  resolutionSummary?: string | null;
  withdrawnDate?: string | null;
  withdrawalReason?: string | null;

  /** HR's reading of the merits — distinct from HR's step response. Frozen once terminal. */
  hrInterpretation?: string | null;
  hrInterpretationById?: string | null;
  hrInterpretationByName?: string | null;
  hrInterpretationDate?: string | null;

  /** Closure without resolution: the ladder exhausted at Board level. */
  closedDate?: string | null;
  closureReason?: string | null;
  closedById?: string | null;
  closedByName?: string | null;

  /** The full ladder in order: every rung reached, answered or not. */
  steps: GrievanceStep[];
  parties: GrievanceParty[];
  investigation?: GrievanceInvestigation | null;
  resolution?: GrievanceResolution | null;
  documents: GrievanceDocument[];
  conferences: GrievanceConference[];
  /** ⚠ HR only — empty for every other reader. See `ErCaseSourceLink`. */
  links: ErCaseSourceLink[];
}

// ─────────────────────────────────────────────────────────────────────────────
//  Write shapes
// ─────────────────────────────────────────────────────────────────────────────

/** No employee id: the griever is the caller. HR cannot raise one for somebody else. */
export interface FileGrievanceRequest {
  subject: string;
  statement: string;
}

/**
 * Opens a case that is NOT a grievance. Takes an explicit employee, which is the exact opposite of
 * filing and deliberately so — the server refuses `caseType: 'Grievance'` here.
 */
export interface OpenErCaseRequest {
  caseType: Exclude<EmployeeRelationsCaseType, 'Grievance'>;
  employeeId: string;
  subject: string;
  /** At least 20 characters — the server enforces it. */
  statement: string;
  /** Optional: open the case straight from a source record. Both fields or neither. */
  source?: EmployeeRelationsLinkSource;
  sourceRecordId?: string;
}

export interface RespondToGrievanceRequest {
  /** At least 10 characters. */
  response: string;
  /**
   * True when this answer settles it; false leaves it open for the employee to judge.
   *
   * ⚠ Resolving this way records an outcome of `NotRecorded`. Prefer the resolve path, which
   * demands a real outcome; this flag exists because it predates it.
   */
  resolvesGrievance: boolean;
}

export interface AssignGrievanceStepRequest {
  assignedToId: string;
}

export interface EscalateGrievanceRequest {
  reason?: string | null;
}

export interface WithdrawGrievanceRequest {
  reason: string;
}

export interface AddGrievancePartyRequest {
  role: GrievancePartyRole;
  /** Exactly one of `employeeId` and `externalName`. */
  employeeId?: string;
  externalName?: string;
  externalOrganisation?: string;
  /** Whom they act for. Only meaningful for a representative, and they must already be a party. */
  representsEmployeeId?: string;
  unionId?: string;
  notes?: string;
}

export interface RemoveGrievancePartyRequest {
  reason: string;
}

export interface RecordHrInterpretationRequest {
  interpretation: string;
}

export interface OpenInvestigationRequest {
  /** Exactly one of `investigatorId` and `externalInvestigatorName`. */
  investigatorId?: string;
  externalInvestigatorName?: string;
  externalInvestigatorOrganisation?: string;
  targetDate?: string | null;
}

export interface UpdateInvestigationRequest {
  targetDate?: string | null;
  findings?: string | null;
  evidenceCollected?: string | null;
  recommendation?: string | null;
}

/** ⚠ The server refuses this without findings: "complete" with nothing in it is worse than open. */
export interface CompleteInvestigationRequest {
  findings: string;
  evidenceCollected?: string | null;
  recommendation?: string | null;
}

export interface ResolveGrievanceRequest {
  outcome: Exclude<GrievanceResolutionOutcome, 'NotRecorded'>;
  /** At least 20 characters. */
  decision: string;
  remedyOrUndertakings?: string | null;
}

export interface CloseGrievanceRequest {
  reason: string;
}

export interface ScheduleConferenceRequest {
  conferenceType: GrievanceConferenceType;
  scheduledFor: string;
  venue?: string | null;
  /** Exactly one of `chairId` and `externalChairName`. */
  chairId?: string;
  externalChairName?: string;
  externalChairOrganisation?: string;
  unionId?: string;
  purpose?: string | null;
}

export interface UpdateConferenceRequest {
  scheduledFor?: string | null;
  venue?: string | null;
  /** The chair CAN be changed while the conference is still scheduled. */
  chairId?: string | null;
  externalChairName?: string | null;
  purpose?: string | null;
}

export interface ConferenceAttendanceRequest {
  attendeeId: string;
  didAttend: boolean;
  apologyReason?: string | null;
}

/**
 * Records that a conference happened.
 *
 * ⚠ `outcome` is REQUIRED and at least 20 characters, and there is no `heldDate` — the server
 * stamps that itself. Both of those were wrong in the first draft of this file, written from the
 * C# by memory rather than read: a screen built on it would have offered a date the server ignores
 * and allowed a submit the server refuses.
 */
export interface HoldConferenceRequest {
  outcome: string;
  /** Redacted on read to HR and the chair. Whoever holds the meeting may write them. */
  notes?: string | null;
  attendance?: ConferenceAttendanceRequest[];
}

export interface CancelConferenceRequest {
  reason: string;
}

export interface AddConferenceAttendeeRequest {
  /** Exactly one of `employeeId` and `externalName`. */
  employeeId?: string;
  externalName?: string;
  externalOrganisation?: string;
  capacity?: string;
}

export interface AcceptAgreementRequest {
  comment?: string | null;
}

/** Cross-references a case to a source record. HR only. */
export interface LinkErCaseSourceRequest {
  source: EmployeeRelationsLinkSource;
  recordId: string;
}

/** The register's filters. All optional; the server clamps page size to 200. */
export interface ErRegisterQuery {
  page?: number;
  pageSize?: number;
  caseType?: EmployeeRelationsCaseType;
  status?: GrievanceStatus;
  level?: GrievanceEscalationLevel;
  organizationUnitId?: string;
  awaitingResponseOnly?: boolean;
  search?: string;
}

// ═════════════════════════════════════════════════════════════════════════════
//  The responder matrix — area 9c slice 5. Backend: `api/hr/employee-relations/responders`
// ═════════════════════════════════════════════════════════════════════════════

/**
 * One row of the matrix: who answers a given rung, for a given scope.
 *
 * ⚠ **Written from the C# DTO and NOT from a probe, because there was nothing to probe: the matrix
 * holds ZERO rows on this tenant** — 0 of 48 org units covered. That is the normal state rather
 * than a broken one; the matrix is opt-in, and a case in an uncovered unit simply arrives
 * unassigned for HR to route by hand. It does mean this one interface carries exactly the risk the
 * probe exists to remove, so re-check it against a live row the first time one exists.
 */
export interface EmployeeRelationsResponder {
  id: string;
  /** Null means the tenant-wide default — the fallback when no unit row matches. */
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  /** Server-computed: the unit's name, or "All units (default)". */
  scopeName: string;
  level: GrievanceEscalationLevel;
  levelName: string;
  responderEmployeeId: string;
  responderName?: string | null;
  responderEmployeeNumber?: string | null;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  notes?: string | null;
  /** Server-computed: in force today, by the effective dates. */
  isCurrent: boolean;
}

export interface UpsertResponderRequest {
  /** Omit for the tenant-wide default. */
  organizationUnitId?: string | null;
  level: GrievanceEscalationLevel;
  responderEmployeeId: string;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  notes?: string | null;
}

/**
 * What the matrix answers for one unit and one rung.
 *
 * ⚠ A miss is a **200 with `resolved: false`**, never a 404 — showing HR where the matrix answers
 * nobody is the admin screen's whole job, and a lookup that errored could not be rendered as a gap.
 * `resolvedBy` reads `"None"` for a miss, and otherwise says which row won.
 */
export interface ResponderResolution {
  organizationUnitId?: string | null;
  level: GrievanceEscalationLevel;
  levelName: string;
  responderEmployeeId?: string | null;
  responderName?: string | null;
  resolvedBy: string;
  resolved: boolean;
}

/** Every rung's answer for one scope — the coverage row the admin screen renders. */
export interface ResponderCoverage {
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  levels: ResponderResolution[];
  coveredLevels: number;
}

// ═════════════════════════════════════════════════════════════════════════════
//  Anonymous concerns — area 9c slice 6. Backend: `.../employee-relations/concerns`
// ═════════════════════════════════════════════════════════════════════════════

export type ConcernCategory =
  | 'Harassment' | 'Discrimination' | 'Bullying' | 'SafetyRisk'
  | 'FraudOrMalpractice' | 'Misconduct' | 'Other';

export type ConcernStatus = 'New' | 'UnderTriage' | 'UnderReview' | 'Closed' | 'ConvertedToCase';

export const CONCERN_CATEGORY_OPTIONS: { value: ConcernCategory; label: string }[] = [
  { value: 'Harassment', label: 'Harassment' },
  { value: 'Discrimination', label: 'Discrimination' },
  { value: 'Bullying', label: 'Bullying' },
  { value: 'SafetyRisk', label: 'Safety risk' },
  { value: 'FraudOrMalpractice', label: 'Fraud or malpractice' },
  { value: 'Misconduct', label: 'Misconduct' },
  { value: 'Other', label: 'Other' },
];

export const CONCERN_STATUS_OPTIONS: { value: ConcernStatus; label: string }[] = [
  { value: 'New', label: 'New' },
  { value: 'UnderTriage', label: 'Under triage' },
  { value: 'UnderReview', label: 'Under review' },
  { value: 'Closed', label: 'Closed' },
  { value: 'ConvertedToCase', label: 'Converted to a case' },
];

/**
 * The statuses triage may SET.
 *
 * ⚠ `New` and `ConvertedToCase` are absent, and the server refuses both. `New` is where a concern
 * starts, and moving one back to it would erase that anybody ever looked; `ConvertedToCase` is set
 * by the convert path, which also creates the case — setting it here would claim a case that does
 * not exist.
 */
export const CONCERN_TRIAGE_STATUS_OPTIONS = CONCERN_STATUS_OPTIONS.filter(
  (o) => o.value === 'UnderTriage' || o.value === 'UnderReview' || o.value === 'Closed',
);

/** One post on the thread. `authorName` is null for the reporter — that is the anonymity. */
export interface ConcernUpdate {
  id: string;
  concernId: string;
  isFromReporter: boolean;
  /** Null whenever `isFromReporter` is true. HR's replies are attributed; the reporter's are not. */
  authorName?: string | null;
  body: string;
  postedAt: string;
}

/**
 * An anonymously-reported concern.
 *
 * ⚠ **There is no reporter on this type, and that is the feature.** No employee id, no created-by,
 * nothing that could be joined back to a person. The guarantee is an ABSENCE, so it cannot be
 * demonstrated by reading this file — `verify-slice6-anonymity.sql` checks the stored rows, because
 * a column could hold the reporter, go unmapped, and every API assertion would still pass.
 */
export interface EmployeeRelationsConcern {
  id: string;
  concernNumber: string;
  category: ConcernCategory;
  categoryName: string;
  subject: string;
  statement: string;
  status: ConcernStatus;
  /** Duplicate of `status`; the server sends both. Prefer `status`. */
  statusValue: ConcernStatus;
  statusName: string;
  reportedAt: string;
  triageNotes?: string | null;
  triagedAt?: string | null;
  triagedByName?: string | null;
  closedAt?: string | null;
  closureReason?: string | null;
  closedByName?: string | null;
  convertedCaseId?: string | null;
  convertedCaseNumber?: string | null;
  updates: ConcernUpdate[];
}

/**
 * Returned ONCE, when the concern is reported.
 *
 * ⚠ The retrieval code is hashed on the way in and cannot be recovered or reissued — not by HR, not
 * by anyone. A screen that shows it must say so, and must never imply it can be looked up later.
 */
export interface ConcernReceipt {
  concernNumber: string;
  retrievalCode: string;
  reportedAt: string;
  notice: string;
}

export interface ReportConcernRequest {
  category: ConcernCategory;
  subject: string;
  /** At least 20 characters. */
  statement: string;
}

export interface TrackConcernRequest {
  concernNumber: string;
  retrievalCode: string;
}

export interface AddConcernUpdateRequest extends TrackConcernRequest {
  /** At least 5 characters. */
  body: string;
}

export interface ReplyToConcernRequest {
  /** At least 5 characters. */
  body: string;
}

export interface TriageConcernRequest {
  /** At least 10 characters. */
  notes: string;
  status: 'UnderTriage' | 'UnderReview' | 'Closed';
}

export interface CloseConcernRequest {
  /** At least 10 characters. */
  reason: string;
}

/** ⚠ Never a grievance — that would be HR raising one on somebody's behalf by a longer route. */
export interface ConvertConcernRequest {
  caseType: Exclude<EmployeeRelationsCaseType, 'Grievance'>;
  employeeId: string;
}

// ═════════════════════════════════════════════════════════════════════════════
//  Analytics — area 9c slice 8
// ═════════════════════════════════════════════════════════════════════════════

/** A count that always carries the total it is a share OF. */
export interface ErCountSlice {
  key: string;
  label: string;
  count: number;
  total: number;
  /** Null when `total` is 0. */
  percent?: number | null;
}

/**
 * A rate, carrying both its numbers.
 *
 * ⚠ `percent` is **null**, never 0, when the denominator is zero — "nobody complied" and "nobody was
 * asked" must not render alike, and `noData` says which one you have. Do not coalesce it to 0 on
 * the way into a chart: that is precisely the area-7 defect this type exists to make impossible.
 */
export interface ErRate {
  label: string;
  numerator: number;
  denominator: number;
  percent?: number | null;
  noData: boolean;
}

export interface ErResolutionTime {
  resolvedCount: number;
  /** Null, not zero: "nothing has been resolved" is not "everything resolves instantly". */
  averageDays?: number | null;
  medianDays?: number | null;
  longestDays?: number | null;
}

export interface ErStuckRung {
  level: GrievanceEscalationLevel;
  levelName: string;
  count: number;
  oldestWaitingDays: number;
  /** Cases at this rung nobody has been named to answer — the matrix gap, expressed in cases. */
  unassigned: number;
}

export interface EmployeeRelationsAnalytics {
  from?: string | null;
  to?: string | null;
  totalCases: number;
  openCases: number;
  resolvedCases: number;
  withdrawnCases: number;
  closedUnresolvedCases: number;
  byCaseType: ErCountSlice[];
  byStatus: ErCountSlice[];
  /** Open cases only — where a case SITS means nothing once it has ended. */
  byCurrentLevel: ErCountSlice[];
  /** Resolved cases only. Carries a "no resolution record" slice so the breakdown adds up. */
  byOutcome: ErCountSlice[];
  byOrganizationUnit: ErCountSlice[];
  escalationRate: ErRate;
  outcomeNotRecordedRate: ErRate;
  agreementMissingRate: ErRate;
  /** ⚠ Resolved only. A withdrawal is not a resolution, and counting one would shorten the average. */
  resolutionTime: ErResolutionTime;
  stuckAtRung: ErStuckRung[];
  concernsReported: number;
  concernsUntriaged: number;
  concernsConverted: number;
  /**
   * ⚠ Counted, never cross-tabbed. There is deliberately no breakdown by unit or reporter: in a
   * unit of four, "one fraud concern this quarter" is an identification, not a statistic.
   */
  concernsByCategory: ErCountSlice[];
}
