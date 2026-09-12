/**
 * The performance journal — the running notebook of evidence that feeds the year-end appraisal.
 * An employee keeps one for themselves; a manager keeps notes about each report.
 *
 * **Privacy is the whole feature.** An entry marked private belongs to its author and to nobody
 * else — not the line manager, not HR. That is deliberate and stricter than the rest of the module:
 * the cycle setting that switches journalling on is called `enablePrivateJournal`, and a private
 * note HR can read is not private. A shared entry (`isPrivate: false`) is readable by its author,
 * the author's line manager, and HR. Editing, deleting and re-classifying belong to the author
 * alone.
 *
 * **Nothing here takes an actor id.** Every route derives the caller from the token — `mine`,
 * `about/{subjectEmployeeId}` (the manager is the caller) and `team` (likewise). The id-bearing
 * routes that used to identify the caller were removed rather than guarded, because a client that
 * has to know its own employee id can pass someone else's, which is exactly what went wrong here.
 *
 * Route: `api/PerformanceJournal`.
 */
import type { AuditFields } from './common';

export interface PerformanceJournalEntry extends AuditFields {
  tenantId: string;
  appraisalCycleId: string;
  cycleCode?: string | null;
  ownerId: string;
  ownerName: string;
  /** Set when the entry is a note *about* someone — a manager writing about a report. */
  subjectEmployeeId?: string | null;
  subjectEmployeeName?: string | null;
  relatedGoalId?: string | null;
  relatedGoalTitle?: string | null;
  title: string;
  body: string;
  entryDate: string;
  isPrivate: boolean;
}

export interface CreatePerformanceJournalEntry {
  appraisalCycleId: string;
  /** Only for a note about a direct report; 403 if they are not one of yours. */
  subjectEmployeeId?: string | null;
  relatedGoalId?: string | null;
  title: string;
  body: string;
  /**
   * ⚠ Creating a private entry is refused with 422 when the cycle's settings profile has
   * `enablePrivateJournal` switched off. That is a policy decision on the cycle, not a permission
   * problem, and the message says so.
   */
  isPrivate: boolean;
}

export interface UpdatePerformanceJournalEntry extends CreatePerformanceJournalEntry {
  id: string;
}

/**
 * A row in the manager's team journal — a different shape from the entry itself, and deliberately
 * so: it carries a `previewText` rather than the body, because the list spans several people's
 * notebooks and is meant to be scanned, not read.
 *
 * Only ever contains what the manager may see: their own notes about a report (private or not,
 * since they wrote them) plus whatever those reports have chosen to share.
 */
export interface TeamJournalEntry {
  id: string;
  title: string;
  entryDate: string;
  writtenById: string;
  writtenByName: string;
  /** True when the manager wrote it about the subject; false when the subject wrote it. */
  isWrittenByManager: boolean;
  subjectEmployeeId: string;
  subjectEmployeeName: string;
  isPrivate: boolean;
  relatedGoalTitle?: string | null;
  previewText: string;
}

/**
 * A `type` rather than an `interface` on purpose: only object *type aliases* get an implicit index
 * signature, so this is assignable to the `Record<string, unknown>` that `apiService.get` takes for
 * query params. An interface here fails to compile at the call site.
 */
export type TeamJournalFilters = {
  /** Must be a direct report — the server answers 400 otherwise. */
  employeeId?: string;
  appraisalCycleId?: string;
  fromDate?: string;
  toDate?: string;
};
