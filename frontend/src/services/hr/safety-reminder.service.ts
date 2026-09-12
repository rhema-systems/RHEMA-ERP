import { apiService } from '../api.service';
import type {
  SheReminderRun,
  SheReminderRunResult,
  SheReminderLogEntry,
} from '@/types/hr/safety-reminders';

/**
 * SHE reminder engine (slice 13, FRD §17): operates the sweep and reads its history.
 * The hourly background job and runNow() execute the same sweep — running it twice
 * is safe (dispatch is deduped per item + due date + ladder rung).
 * Backend route: api/safety/reminders (HR-gated).
 */
class SafetyReminderService {
  private readonly baseUrl = '/safety/reminders';

  /** Forces a sweep for the tenant now (e.g. after bulk date edits). Dedupe-safe. */
  runNow(): Promise<SheReminderRunResult> {
    return apiService.post<SheReminderRunResult>(`${this.baseUrl}/run`, {});
  }

  getRecentRuns(count = 20): Promise<SheReminderRun[]> {
    return apiService.get<SheReminderRun[]>(`${this.baseUrl}/runs`, { count });
  }

  getRecentLog(days = 14): Promise<SheReminderLogEntry[]> {
    return apiService.get<SheReminderLogEntry[]>(`${this.baseUrl}/log`, { days });
  }
}

export const safetyReminderService = new SafetyReminderService();
export default safetyReminderService;
