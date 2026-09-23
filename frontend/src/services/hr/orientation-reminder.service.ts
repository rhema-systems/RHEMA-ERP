import { apiService } from '../api.service';
import type {
  OrientationReminderRunResult,
  OrientationReminderPreviewItem,
  OrientationReminderRun,
  OrientationReminderLogEntry,
  OrientationNoticeLogEntry,
  OrientationNoticeDispatchResult,
} from '@/types/hr/orientation';

/**
 * The orientation & onboarding reminder sweep (round 4, lane K). Backend route:
 * api/orientation-reminders. Run-now is HR.Orientation.Write; the reads are HR.Orientation.Read.
 */
class OrientationReminderService {
  private readonly baseUrl = '/orientation-reminders';

  /** Runs a sweep now — claims, notifies and emails. Safe to repeat: each item goes once. */
  run(): Promise<OrientationReminderRunResult> {
    return apiService.post<OrientationReminderRunResult>(`${this.baseUrl}/run`, {});
  }

  /** What a sweep would remind, as at a date. Claims and sends nothing. */
  preview(asOf?: string): Promise<OrientationReminderPreviewItem[]> {
    return apiService.get<OrientationReminderPreviewItem[]>(
      `${this.baseUrl}/preview`,
      asOf ? { asOf } : undefined,
    );
  }

  getRuns(count = 20): Promise<OrientationReminderRun[]> {
    return apiService.get<OrientationReminderRun[]>(`${this.baseUrl}/runs`, { count });
  }

  getLog(days = 14): Promise<OrientationReminderLogEntry[]> {
    return apiService.get<OrientationReminderLogEntry[]>(`${this.baseUrl}/log`, { days });
  }

  // ── Lifecycle notices (round 4, lane K-b) — api/orientation-notifications ──

  /** Every notice sent lately — who was told what, and what its email did. HR.Orientation.Read. */
  getRecentNotices(days = 14, kind?: string, emailStatus?: string): Promise<OrientationNoticeLogEntry[]> {
    const params: Record<string, string | number> = { days };
    if (kind) params.kind = kind;
    if (emailStatus) params.emailStatus = emailStatus;
    return apiService.get<OrientationNoticeLogEntry[]>('/orientation-notifications/recent', params);
  }

  /** Sends the queued notice emails now instead of on the dispatcher's next minute. HR.Orientation.Write. */
  sendQueuedNotices(): Promise<OrientationNoticeDispatchResult> {
    return apiService.post<OrientationNoticeDispatchResult>('/orientation-notifications/send-queued', {});
  }
}

export const orientationReminderService = new OrientationReminderService();
