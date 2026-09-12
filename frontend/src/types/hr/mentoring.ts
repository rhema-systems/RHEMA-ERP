// Types for HR Training & Learning — Slice 7 (Mentoring).
//
// A *programme* is the scheme; a *pair* is one mentor/mentee relationship inside it; a *session* is a
// logged meeting. Mirrors the same-named DTOs in TrainingDTOs.cs.
//
// ⚠ Four fields are author-only and the server decides, not the screen. `mentorNotes` returns only to
// the mentor and `menteeNotes` only to the mentee; `mentorRating` is the mentee's score OF the mentor
// so only the mentee sees it, and `mentorRating`'s counterpart likewise. Everyone else — including HR
// and the programme coordinator — receives null. Two consequences the UI has to respect:
//   1. A null note means "not yours to read", NOT "empty". Never render it as a blank editable field.
//   2. When saving, send back what you were given. The server preserves the absent party's value, but
//      a screen that treats null as "cleared" will still look like it deleted something.

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

export type MentoringStatus = 'Active' | 'Paused' | 'Completed' | 'Cancelled';

export const MENTORING_STATUS_OPTIONS = opts<MentoringStatus>([
  ['Active', 'Active'],
  ['Paused', 'Paused'],
  ['Completed', 'Completed'],
  ['Cancelled', 'Cancelled'],
]);

export type MentoringSessionFormat = 'InPerson' | 'Virtual' | 'Hybrid';

export const MENTORING_FORMAT_OPTIONS = opts<MentoringSessionFormat>([
  ['InPerson', 'In person'],
  ['Virtual', 'Virtual'],
  ['Hybrid', 'Hybrid'],
]);

// ── Programme ──────────────────────────────────────────────────────────────────────────────

export interface MentoringProgram {
  id: string;
  programName: string;
  description?: string | null;
  objectives?: string | null;
  startDate: string;
  endDate?: string | null;
  sessionsPerMonth?: number | null;
  minutesPerSession?: number | null;
  isActive: boolean;
  coordinatedById?: string | null;
  coordinatedByName?: string | null;
  totalPairsCount: number;
  activePairsCount: number;
}

export interface MentoringProgramSummary {
  id: string;
  programName: string;
  startDate: string;
  endDate?: string | null;
  isActive: boolean;
  coordinatedByName?: string | null;
  totalPairsCount: number;
  activePairsCount: number;
}

export interface MentoringProgramCreate {
  programName: string;
  description?: string | null;
  objectives?: string | null;
  startDate: string;
  endDate?: string | null;
  sessionsPerMonth?: number | null;
  minutesPerSession?: number | null;
  isActive: boolean;
  coordinatedById?: string | null;
}

export type MentoringProgramUpdate = MentoringProgramCreate;

// ── Pair ───────────────────────────────────────────────────────────────────────────────────

export interface MentoringPair {
  id: string;
  programId: string;
  programName: string;
  mentorId: string;
  mentorName: string;
  mentorNumber: string;
  mentorPosition?: string | null;
  menteeId: string;
  menteeName: string;
  menteeNumber: string;
  menteePosition?: string | null;
  startDate: string;
  endDate?: string | null;
  status: MentoringStatus;
  statusName: string;
  goals?: string | null;
  focusAreas?: string | null;
  closureNotes?: string | null;
  /** The mentee's score of the mentor — visible only to the mentee who gave it. */
  mentorRating?: number | null;
  /** The mentor's score of the mentee — visible only to the mentor who gave it. */
  menteeRating?: number | null;
  totalSessionsCount: number;
  totalMinutes: number;
  /**
   * Which side the caller is on — server-decided, and the only reliable way to tell "there is no
   * note" from "the note is not yours". Both arrive as null. A coordinator or HR viewer is neither.
   */
  viewerIsMentor: boolean;
  viewerIsMentee: boolean;
}

export interface MentoringPairSummary {
  id: string;
  mentorName: string;
  menteeName: string;
  programName: string;
  startDate: string;
  status: MentoringStatus;
  statusName: string;
  totalSessionsCount: number;
}

export interface MentoringPairCreate {
  programId: string;
  mentorId: string;
  menteeId: string;
  startDate: string;
  endDate?: string | null;
  goals?: string | null;
  focusAreas?: string | null;
}

export interface MentoringPairUpdate {
  endDate?: string | null;
  status: MentoringStatus;
  goals?: string | null;
  focusAreas?: string | null;
  closureNotes?: string | null;
  mentorRating?: number | null;
  menteeRating?: number | null;
}

// ── Session ────────────────────────────────────────────────────────────────────────────────

export interface MentoringSession {
  id: string;
  pairId: string;
  mentorName: string;
  menteeName: string;
  programName: string;
  sessionDate: string;
  durationMinutes: number;
  format?: MentoringSessionFormat | null;
  topicsDiscussed?: string | null;
  actionItems?: string | null;
  /** Author-only: null means "not yours to read", never "empty". */
  mentorNotes?: string | null;
  /** Author-only: null means "not yours to read", never "empty". */
  menteeNotes?: string | null;
  attendedByMentor: boolean;
  attendedByMentee: boolean;
}

export interface MentoringSessionCreate {
  pairId: string;
  sessionDate: string;
  durationMinutes: number;
  format?: MentoringSessionFormat | null;
  topicsDiscussed?: string | null;
  actionItems?: string | null;
  mentorNotes?: string | null;
  menteeNotes?: string | null;
  attendedByMentor: boolean;
  attendedByMentee: boolean;
}

export type MentoringSessionUpdate = Omit<MentoringSessionCreate, 'pairId'>;

export interface ClosePairRequest {
  closureNotes: string;
}
