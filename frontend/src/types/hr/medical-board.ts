/**
 * Medical boards — a panel convened to rule on an employee's fitness for duty (residue plan G4).
 *
 * Backend route: `api/hr/medical-boards`.
 *
 * ⚠ **The board is a Medical-module record, and the bridge to it is one way.** Leave reads a board
 * to satisfy its evidence rule; separation reads one to justify a medical retirement. Neither
 * writes to it, and there is no endpoint here for acting on a board — what anybody does about a
 * recommendation belongs to the module that acts.
 *
 * ⚠ **"Gated on HR.Medical.*" does not mean HR cannot see it.** `HrStaffGrants` gives the HR role
 * ViewMedicalRecords and MaintainMedicalRecords deliberately, because HR administers the process
 * even though clinicians decide it. An ordinary employee and a line manager hold neither.
 */

/**
 * ⚠ A ratchet: Requested → Convened → Concluded, with Cancelled available until it reports.
 * There is no un-conclude — a finding that needs revisiting is a NEW board, which is also how it
 * works on paper.
 */
export type MedicalBoardStatus = 'Requested' | 'Convened' | 'Concluded' | 'Cancelled';

export type MedicalBoardMemberRole = 'Chair' | 'Member' | 'Secretary' | 'Observer';

/**
 * The question a board is asked (round 5, lane K1). Required when a board is requested.
 *
 * ⚠ Leave reads it: only a board about an absence can stand as the board a leave type's threshold
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
}

export interface MedicalBoardSitting {
  id: string;
  boardId: string;
  /** DateOnly */
  sittingDate: string;
  venue?: string | null;
  /** ⚠ Medical-grade content — gated with the rest. */
  notes?: string | null;
}

export interface MedicalBoard {
  id: string;
  /** ⚠ Timestamped, not a countable sequence: a sequential board number would be enumerable. */
  boardNumber: string;

  employeeId: string;
  employeeName: string;
  employeeNumber: string;

  status: MedicalBoardStatus;
  purpose: MedicalBoardPurpose;
  /** ⚠ Whether this purpose can satisfy a leave type's board rule (lane K6) — the server's answer. */
  coversAbsence: boolean;
  reason: string;

  requestedById?: string | null;
  requestedByName?: string | null;
  /** DateOnly */
  requestedOn: string;
  convenedOn?: string | null;

  healthProfileId?: string | null;
  basedOnExamId?: string | null;
  /** DateOnly — the examination the board was based on. */
  basedOnExamDate?: string | null;
  basedOnExamResult?: MedicalBoardOutcome | null;
  facilityId?: string | null;
  facilityName?: string | null;

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
  cancellationReason?: string | null;
  /** DateOnly. When it was stopped, and by whom (lane K5); null on boards stopped before that. */
  cancelledOn?: string | null;
  cancelledById?: string | null;
  cancelledByName?: string | null;
  /** ⚠ Stopped after it was convened: dissolved. Stopped before: a cancelled request. */
  wasDissolved: boolean;

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

/** A paper on a board (lane K4). Served only by the board's gated download — never link to a path. */
export interface MedicalBoardDocument {
  id: string;
  boardId: string;
  fileName: string;
  fileSize?: number | null;
  description?: string | null;
  uploadDate: string;
  uploadedById: string;
  uploadedByName?: string | null;
}

/**
 * ⚠ The facility and the examination are checked by the server (lane K3): each must be this
 * tenant's, and the examination must be the subject's own. The health profile follows from the
 * examination, or is the subject's own when neither is named — there is no need to send it.
 */
export interface RequestMedicalBoardRequest {
  employeeId: string;
  purpose: MedicalBoardPurpose;
  reason: string;
  healthProfileId?: string | null;
  basedOnExamId?: string | null;
  facilityId?: string | null;
}

/**
 * ⚠ Supply **exactly one** of `physicianId`, `employeeId` or `memberName` — two is refused. A board is not only doctors —
 * it carries HR as secretary, a staff or union representative, and often a clinician from outside
 * who is in nobody's register.
 *
 * ⚠ The subject of the board cannot be seated on it.
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
}

/** ⚠ Cannot be undone. The only status leave and separation act on. */
export interface ConcludeMedicalBoardRequest {
  outcome: MedicalBoardOutcome;
  findings?: string | null;
  recommendation: string;
  restrictions?: string | null;
  reviewDueDate?: string | null;
  recommendsMedicalRetirement: boolean;
}
