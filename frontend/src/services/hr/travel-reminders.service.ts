import { apiService } from '../api.service';
import type {
  StaffTravelReminderRunResult,
  StaffTravelReminderRun,
  StaffTravelReminderPreviewItem,
  StaffTravelReminderLogEntry,
} from '@/types/hr/travel-reminders';

/**
 * The travel reminder sweep.
 *
 * ⚠ Every endpoint here is `HR.Travel.Admin` — including the reads, because the log lists named
 * employees against their document expiry dates.
 *
 * The sweep is normally scheduled; `run` exists so an administrator can force one and see the
 * result immediately. It is safe to repeat: the engine dedupes on a unique key, and `alreadySent`
 * counts what a repeat declined to send again.
 */
class TravelRemindersService {
  private readonly baseUrl = '/staff-travel/reminders';

  /** Runs a sweep now for the caller's tenant. Repeating it is harmless. */
  run() {
    return apiService.post<StaffTravelReminderRunResult>(`${this.baseUrl}/run`, {});
  }

  /**
   * What a sweep would fire, without firing it.
   *
   * `asOf` evaluates as if it were that instant. Every date in the engine is server-stamped, so
   * without it a test — or an administrator asking "what happens next month?" — can only see what
   * happens to be true today.
   */
  preview(asOf?: string) {
    return apiService.get<StaffTravelReminderPreviewItem[]>(
      `${this.baseUrl}/preview`, asOf ? { asOf } : {});
  }

  getRuns(count = 20) {
    return apiService.get<StaffTravelReminderRun[]>(`${this.baseUrl}/runs`, { count });
  }

  getLog(days = 14) {
    return apiService.get<StaffTravelReminderLogEntry[]>(`${this.baseUrl}/log`, { days });
  }
}

export const travelRemindersService = new TravelRemindersService();
