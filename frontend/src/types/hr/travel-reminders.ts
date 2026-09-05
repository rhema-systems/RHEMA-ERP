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
 * One reminder the sweep would fire. `alreadySent` means the dedupe key has been seen, so a real
 * run would skip it.
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
}

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
}
