import { apiService } from '../api.service';
import type {
  CreatePerformanceJournalEntry,
  PerformanceJournalEntry,
  TeamJournalEntry,
  TeamJournalFilters,
  UpdatePerformanceJournalEntry,
} from '@/types/hr/journal';

/**
 * api/PerformanceJournal — the evidence notebook behind the appraisal.
 *
 * Every actor is taken from the token, so nothing here accepts an employee id for the caller.
 * `getMine` returns the caller's own entries including private ones; `getByOwner` is the
 * line-manager/HR view of someone else and never includes their private entries; `getAbout` is
 * the caller's own notes about one of their reports, private ones included, because they wrote
 * them.
 *
 * ⚠ Creating a **private** entry is 422 when the cycle has `enablePrivateJournal` off.
 * ⚠ Writing a note about someone who does not report to you is 403.
 */
class PerformanceJournalService {
  private readonly baseUrl = '/PerformanceJournal';

  getById(id: string): Promise<PerformanceJournalEntry> {
    return apiService.get<PerformanceJournalEntry>(`${this.baseUrl}/${id}`);
  }

  /** The caller's own journal, private entries included. */
  getMine(cycleId?: string): Promise<PerformanceJournalEntry[]> {
    return apiService.get<PerformanceJournalEntry[]>(
      `${this.baseUrl}/mine`,
      cycleId ? { cycleId } : undefined,
    );
  }

  /**
   * Someone else's journal, as their line manager or HR. Private entries are never included —
   * only the owner's own `getMine` returns those.
   */
  getByOwner(ownerId: string, cycleId?: string): Promise<PerformanceJournalEntry[]> {
    return apiService.get<PerformanceJournalEntry[]>(
      `${this.baseUrl}/by-owner/${ownerId}`,
      cycleId ? { cycleId } : undefined,
    );
  }

  /** The caller's own notes about one of their direct reports. */
  getAbout(subjectEmployeeId: string, cycleId?: string): Promise<PerformanceJournalEntry[]> {
    return apiService.get<PerformanceJournalEntry[]>(
      `${this.baseUrl}/about/${subjectEmployeeId}`,
      cycleId ? { cycleId } : undefined,
    );
  }

  /** The manager's cross-team view: own notes about reports + reports' shared entries. */
  getTeam(filters: TeamJournalFilters = {}): Promise<TeamJournalEntry[]> {
    return apiService.get<TeamJournalEntry[]>(`${this.baseUrl}/team`, filters);
  }

  /** The author is the caller — an `ownerId` in the payload is ignored. */
  create(data: CreatePerformanceJournalEntry): Promise<PerformanceJournalEntry> {
    return apiService.post<PerformanceJournalEntry>(this.baseUrl, data);
  }

  /** Author only; 403 for anyone else, including HR. */
  update(id: string, data: UpdatePerformanceJournalEntry): Promise<PerformanceJournalEntry> {
    return apiService.put<PerformanceJournalEntry>(`${this.baseUrl}/${id}`, data);
  }

  /** Author only. */
  delete(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /**
   * Share an entry with the line manager, or take it back.
   *
   * Author only — otherwise this would be a read hole by another route: anyone able to un-private
   * someone's entry could then read it through the shared views.
   */
  setPrivacy(id: string, isPrivate: boolean): Promise<void> {
    return apiService.patch<void>(`${this.baseUrl}/${id}/privacy`, isPrivate);
  }
}

export const performanceJournalService = new PerformanceJournalService();
