/**
 * Staff travel reminders — the sweep that chases expiring passports and visas, overdue advance
 * settlements and imminent departures. Backend route: `api/staff-travel/reminders`.
 *
 * ⚠ **The whole controller is `HR.Travel.Admin`.** Reading what the engine has done is as gated as
 * running it, because the log names people and their document expiry dates.
 *
 * The engine dedupes on `dedupeKey` behind a unique `(TenantId, DedupeKey)` index, so a sweep is
 * safe to repeat — `alreadySent` counts what a second run declined to send again rather than
 * anything that failed.
 */

/** What one sweep did. */
export interface StaffTravelReminderRunResult {
  runId: string;
  startedAt: string;
  completedAt?: string | null;
  /** "Manual" for a sweep someone triggered; the scheduler names itself otherwise. */
  trigger: string;
  remindersQueued: number;
  /** Suppressed as duplicates — not failures. */
  alreadySent: number;
  /** Claimed by an earlier sweep that stopped before sending them — sent by this one (lane 8, slice 8b). */
  retried: number;
  /** Advances this sweep marked overdue (lane 3). */
  advancesMarkedOverdue: number;
  /** Approved trips moved under way — the departure date, or Fleet's dispatch (lane 8, slice 8c). */
  tripsStarted: number;
  /** Trips under way marked completed, the day after they ended (D-47). */
  tripsCompleted: number;
  /** Completed trips closed — claim window passed, nothing left to settle (D-51). */
  tripsClosed: number;
  /** Group trips moved under way or completed. */
  groupsUpdated: number;
  /** Working days put on travellers' attendance as on duty (lane 9, D-54). */
  attendanceDaysAdded: number;
  /** Attendance days given up — trips cancelled, sent back, shortened or deleted. */
  attendanceDaysRemoved: number;
}

export interface StaffTravelReminderRun {
  id: string;
  startedAt: string;
  completedAt?: string | null;
  trigger: string;
  triggeredByUserId?: string | null;
  remindersQueued: number;
}

/**
 * One reminder the sweep would fire — or, since lane 8 slice 8c, one move it would make (`TripStarted`,
 * `TripCompleted`, `TripClosed`, `GroupStarted`, `GroupCompleted`; see `SWEEP_MOVES`). `alreadySent` means the dedupe
 * key has been seen, so a real run would skip it.
 */
export interface StaffTravelReminderPreviewItem {
  kind: string;
  itemType: string;
  entityId: string;
  reference: string;
  dueDate?: string | null;
  daysRemaining: number;
  escalationTier: number;
  dedupeKey: string;
  alreadySent: boolean;
  /** Whom it reaches: "Traveller", "Desk", "Approvers" (lane 8, slice 8b), "LineManager" (8c). Empty for a silent move. */
  sentTo: string[];
}

/** The kinds that are the sweep's own moves rather than reminders (lane 8, slice 8c). */
export const SWEEP_MOVES: ReadonlySet<string> = new Set([
  'TripStarted',
  'TripCompleted',
  'TripClosed',
  'GroupStarted',
  'GroupCompleted',
]);

export interface StaffTravelReminderLogEntry {
  id: string;
  runId: string;
  kind: string;
  itemType: string;
  entityId: string;
  reference: string;
  dueDate?: string | null;
  daysRemaining: number;
  escalationTier: number;
  createdAt: string;
  /** When the sweep sent it; null for one claimed and not yet sent (the next sweep sends it). */
  publishedAt?: string | null;
}
