/**
 * Medical boards — a panel that hears employees' cases and rules on each one's fitness (residue
 * plan G4; round 5, lanes K and K-II-a).
 *
 * Backend route: `api/hr/medical-boards`.
 *
 * ⚠ **The board is the panel; each employee before it is a case** (lane K-II-a). One sitting can
 * decide several cases; each finding records the sitting it was decided at, and that sitting's
 * attendance is who decided. Before K-II-a a board WAS one employee's case.
 *
 * ⚠ **The board is a Medical-module record, and the bridge to it is one way.** Leave reads the case
 * about its employee to satisfy its evidence rule; separation reads one to justify a medical
 * retirement. Neither writes to it — what anybody does about a recommendation belongs to the module
 * that acts.
 *
 * ⚠ **"Gated on HR.Medical.*" does not mean HR cannot see it.** `HrStaffGrants` gives the HR role
 * ViewMedicalRecords and MaintainMedicalRecords deliberately, because HR administers the process
 * even though clinicians decide it. An ordinary employee and a line manager hold neither.
 */

/**
 * ⚠ A ratchet: Requested → Convened → Concluded, with Cancelled available until it reports. A board
 * reports BY ITSELF when its last open case closes with at least one decided (lane K-II-a) — there is
 * no "conclude the board" action.
 */
export type MedicalBoardStatus = 'Requested' | 'Convened' | 'Concluded' | 'Cancelled';

/** Who convened it (lane K-II-a): the employer, or one of the Workmen's Compensation Act's boards. */
export type MedicalBoardKind = 'Employer' | 'StatutoryDisfigurement' | 'StatutoryInternalOrgan';

export const MEDICAL_BOARD_KIND_LABEL: Record<MedicalBoardKind, string> = {
  Employer: "The employer's own board",
  StatutoryDisfigurement: 'Statutory board — disfigurement (chief labour officer)',
  StatutoryInternalOrgan: 'Statutory board — internal organ (Minister)',
};

/** A case: Listed until decided at a sitting, or withdrawn without a finding. */
export type MedicalBoardCaseStatus = 'Listed' | 'Concluded' | 'Withdrawn';

export const MEDICAL_BOARD_CASE_STATUS_LABEL: Record<MedicalBoardCaseStatus, string> = {
  Listed: 'Listed',
  Concluded: 'Decided',
  Withdrawn: 'Withdrawn',
};

/** ⚠ Chair and Member decide — the quorum counts them. Secretary and Observer attend without deciding. */
export type MedicalBoardMemberRole = 'Chair' | 'Member' | 'Secretary' | 'Observer';

/**
 * The question a board is asked about one employee (round 5, lane K1) — per case since K-II-a.
 *
 * ⚠ Leave reads it: only a case about an absence can stand as the board a leave type's threshold
 * asks for. The server says which through `coversAbsence` — read that, do not keep a copy of the list.
 */
export type MedicalBoardPurpose =
  | 'ExtendedSickLeave'
  | 'InjuryOnDuty'
  | 'FitnessForDuty'
  | 'MedicalRetirement'
  | 'Other';

export const MEDICAL_BOARD_PURPOSE_LABEL: Record<MedicalBoardPurpose, string> = {
  ExtendedSickLeave: 'Extended sick leave',
  InjuryOnDuty: 'Injury on duty',
  FitnessForDuty: 'Fitness for duty',
  MedicalRetirement: 'Medical retirement',
  Other: 'Other',
};

/**
 * The finding. ⚠ Reuses `MedicalExamResult`, which the module already used on examinations —
 * a parallel enum would have let the two vocabularies drift.
 */
export type MedicalBoardOutcome =
  | 'Fit'
  | 'FitWithRestrictions'
  | 'TemporarilyUnfit'
  | 'Unfit'
  | 'RequiresFurtherInvestigation';

export const MEDICAL_BOARD_STATUS_LABEL: Record<MedicalBoardStatus, string> = {
  Requested: 'Requested',
  Convened: 'Convened',
  Concluded: 'Concluded',
  Cancelled: 'Cancelled',
};

export const MEDICAL_BOARD_OUTCOME_LABEL: Record<MedicalBoardOutcome, string> = {
  Fit: 'Fit',
  FitWithRestrictions: 'Fit with restrictions',
  TemporarilyUnfit: 'Temporarily unfit',
  Unfit: 'Unfit',
  RequiresFurtherInvestigation: 'Requires further investigation',
};

export const MEDICAL_BOARD_MEMBER_ROLE_LABEL: Record<MedicalBoardMemberRole, string> = {
  Chair: 'Chair',
  Member: 'Member',
  Secretary: 'Secretary',
  Observer: 'Observer',
};

export interface MedicalBoardMember {
  id: string;
  boardId: string;
  physicianId?: string | null;
  /** Set when the member works here — HR, a staff or union representative, a nurse. */
  employeeId?: string | null;
  /** Registered identity wins over a typed name: if somebody is on file, the file is the answer. */
  displayName: string;
  /** `Physician` · `Employee` · `External`, so a screen shows which without guessing. */
  memberKind: string;
  institution?: string | null;
  role: MedicalBoardMemberRole;
  /** Chair or member: counted towards the quorum at a deciding sitting. */
  decides: boolean;
}

/** A member present at a sitting. Removed members keep their attendance — it records who sat. */
export interface MedicalBoardAttendee {
  memberId: string;
  displayName: string;
  role: MedicalBoardMemberRole;
  decides: boolean;
}

export interface MedicalBoardSitting {
  id: string;
  boardId: string;
  /** DateOnly */
  sittingDate: string;
  venue?: string | null;
  /** ⚠ Medical-grade content — gated with the rest. */
  notes?: string | null;
  attendees: MedicalBoardAttendee[];
  /** Cases decided at this sitting. ⚠ Above zero, its attendance is fixed: it is the panel that decided. */
  casesDecided: number;
}

/** One employee's case before a board, and its finding (lane K-II-a). */
export interface MedicalBoardCase {
  id: string;
  boardId: string;
  boardNumber: string;

  employeeId: string;
  employeeName: string;
  employeeNumber: string;

  purpose: MedicalBoardPurpose;
  /** ⚠ Whether this purpose can satisfy a leave type's board rule (lane K6) — the server's answer. */
  coversAbsence: boolean;
  reason: string;

  requestedById?: string | null;
  requestedByName?: string | null;
  /** DateOnly */
  requestedOn: string;

  healthProfileId?: string | null;
  basedOnExamId?: string | null;
  /** DateOnly — the examination the case was based on. */
  basedOnExamDate?: string | null;
  basedOnExamResult?: MedicalBoardOutcome | null;

  status: MedicalBoardCaseStatus;

  decidedAtSittingId?: string | null;
  /** DateOnly */
  decidedAtSittingDate?: string | null;
  /** ⚠ Who decided: that sitting's attendance. Empty for a case decided before attendance was recorded. */
  decidedBy: string[];

  outcome?: MedicalBoardOutcome | null;
  findings?: string | null;
  recommendation?: string | null;
  restrictions?: string | null;
  reviewDueDate?: string | null;
  /** ⚠ A recommendation, not an act — separation decides, this only records what was advised. */
  recommendsMedicalRetirement: boolean;

  concludedOn?: string | null;
  concludedById?: string | null;
  concludedByName?: string | null;

  withdrawnOn?: string | null;
  withdrawnById?: string | null;
  withdrawnByName?: string | null;
  withdrawalReason?: string | null;
}

export interface MedicalBoard {
  id: string;
  /** ⚠ Timestamped, not a countable sequence: a sequential board number would be enumerable. */
  boardNumber: string;
  kind: MedicalBoardKind;
  status: MedicalBoardStatus;

  requestedById?: string | null;
  requestedByName?: string | null;
  /** DateOnly */
  requestedOn: string;
  convenedOn?: string | null;

  facilityId?: string | null;
  facilityName?: string | null;

  /** When the board reported — its last open case closed with at least one decided. */
  concludedOn?: string | null;
  cancellationReason?: string | null;
  /** DateOnly. When it was stopped, and by whom (lane K5); null on boards stopped before that. */
  cancelledOn?: string | null;
  cancelledById?: string | null;
  cancelledByName?: string | null;
  /** ⚠ Stopped after it was convened: dissolved. Stopped before: a cancelled request. */
  wasDissolved: boolean;

  cases: MedicalBoardCase[];
  members: MedicalBoardMember[];
  sittings: MedicalBoardSitting[];
}

/**
 * What to call a board's state. ⚠ `Cancelled` is one status with two meanings (lane K5): a request
 * cancelled before any panel existed, or a board dissolved after it was convened.
 */
export function boardStatusLabel(board: Pick<MedicalBoard, 'status' | 'wasDissolved'>): string {
  return board.status === 'Cancelled' && board.wasDissolved ? 'Dissolved' : board.status;
}

/** The case about one employee on a board, if they are before it. At most one (a unique index). */
export function caseFor(board: Pick<MedicalBoard, 'cases'>, employeeId: string): MedicalBoardCase | undefined {
  return board.cases.find((c) => c.employeeId === employeeId);
}

/** A paper on a board, or on one of its cases (lanes K4, K-II-a). Served only by the gated download. */
export interface MedicalBoardDocument {
  id: string;
  boardId: string;
  caseId?: string | null;
  caseEmployeeName?: string | null;
  fileName: string;
  fileSize?: number | null;
  description?: string | null;
  uploadDate: string;
  uploadedById: string;
  uploadedByName?: string | null;
}

/**
 * Asking for a board, with its first case.
 *
 * ⚠ The facility and the examination are checked by the server (lane K3): each must be this
 * tenant's, and the examination must be the subject's own. The health profile follows from the
 * examination, or is the subject's own when neither is named — there is no need to send it.
 */
export interface RequestMedicalBoardRequest {
  kind?: MedicalBoardKind | null;
  facilityId?: string | null;
  employeeId: string;
  purpose: MedicalBoardPurpose;
  reason: string;
  healthProfileId?: string | null;
  basedOnExamId?: string | null;
}

/** Another employee before the board. ⚠ Refused for somebody already before it, or sitting on it. */
export interface AddMedicalBoardCaseRequest {
  employeeId: string;
  purpose: MedicalBoardPurpose;
  reason: string;
  healthProfileId?: string | null;
  basedOnExamId?: string | null;
}

/**
 * ⚠ Supply **exactly one** of `physicianId`, `employeeId` or `memberName` — two is refused. A board is not only doctors —
 * it carries HR as secretary, a staff or union representative, and often a clinician from outside
 * who is in nobody's register.
 *
 * ⚠ Nobody whose case is before the board can be seated on it.
 */
export interface AddMedicalBoardMemberRequest {
  physicianId?: string | null;
  employeeId?: string | null;
  memberName?: string | null;
  institution?: string | null;
  role: MedicalBoardMemberRole;
}

export interface RecordMedicalBoardSittingRequest {
  sittingDate: string;
  venue?: string | null;
  notes?: string | null;
  /** The members present — each must be on the board. */
  attendeeMemberIds?: string[];
}

/**
 * ⚠ Cannot be undone. The only state leave and separation act on. `sittingId` is required: its
 * attendance must hold the quorum of deciding members (`MedicalBoardQuorum`, default 1).
 */
export interface ConcludeMedicalBoardCaseRequest {
  sittingId: string;
  outcome: MedicalBoardOutcome;
  findings?: string | null;
  recommendation: string;
  restrictions?: string | null;
  reviewDueDate?: string | null;
  recommendsMedicalRetirement: boolean;
}
