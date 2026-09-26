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
  reason: string;

  requestedById?: string | null;
  requestedByName?: string | null;
  /** DateOnly */
  requestedOn: string;
  convenedOn?: string | null;

  healthProfileId?: string | null;
  basedOnExamId?: string | null;
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

  members: MedicalBoardMember[];
  sittings: MedicalBoardSitting[];
}

export interface RequestMedicalBoardRequest {
  employeeId: string;
  reason: string;
  healthProfileId?: string | null;
  basedOnExamId?: string | null;
  facilityId?: string | null;
}

/**
 * ⚠ Supply **one** of `physicianId`, `employeeId` or `memberName`. A board is not only doctors —
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
