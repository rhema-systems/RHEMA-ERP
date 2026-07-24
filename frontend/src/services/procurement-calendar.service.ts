import { apiService } from '@/services/api.service';
import type {
  ProcurementCalendarOccurrence,
  ProcurementCalendarOccurrencePage,
  ProcurementCalendarProfile,
  ProcurementCalendarRun,
  ProcurementCalendarSummary,
  SaveProcurementCalendarProfile,
} from '@/types/procurement-calendar';

const root = '/procurement/calendar';

export const procurementCalendarService = {
  summary: () => apiService.get<ProcurementCalendarSummary>(`${root}/summary`),
  profiles: () =>
    apiService.get<ProcurementCalendarProfile[]>(`${root}/profiles`),
  profile: (id: string) =>
    apiService.get<ProcurementCalendarProfile>(`${root}/profiles/${id}`),
  timeZones: () => apiService.get<string[]>(`${root}/time-zones`),
  createProfile: (request: SaveProcurementCalendarProfile) =>
    apiService.post<ProcurementCalendarProfile>(`${root}/profiles`, request),
  updateProfile: (id: string, request: SaveProcurementCalendarProfile) =>
    apiService.put<ProcurementCalendarProfile>(
      `${root}/profiles/${id}`,
      request
    ),
  cloneProfile: (
    id: string,
    request: {
      changeSummary: string;
      effectiveFromUtc: string;
      effectiveToUtc?: string;
    }
  ) =>
    apiService.post<ProcurementCalendarProfile>(
      `${root}/profiles/${id}/clone`,
      request
    ),
  publishProfile: (
    id: string,
    request: { rowVersion: string; reason: string; approvalReference: string }
  ) =>
    apiService.post<ProcurementCalendarProfile>(
      `${root}/profiles/${id}/publish`,
      request
    ),
  retireProfile: (
    id: string,
    request: { rowVersion: string; reason: string }
  ) =>
    apiService.post<ProcurementCalendarProfile>(
      `${root}/profiles/${id}/retire`,
      request
    ),
  deleteDraft: (id: string, request: { rowVersion: string; reason: string }) =>
    apiService.delete<void>(`${root}/profiles/${id}`, request),
  occurrences: (query?: Record<string, unknown>) =>
    apiService.get<ProcurementCalendarOccurrencePage>(
      `${root}/occurrences`,
      query
    ),
  acknowledge: (id: string, rowVersion: string, reason: string) =>
    apiService.post<ProcurementCalendarOccurrence>(
      `${root}/occurrences/${id}/acknowledge`,
      { rowVersion, reason }
    ),
  complete: (id: string, rowVersion: string, reason: string) =>
    apiService.post<ProcurementCalendarOccurrence>(
      `${root}/occurrences/${id}/complete`,
      { rowVersion, reason }
    ),
  cancel: (id: string, rowVersion: string, reason: string) =>
    apiService.post<ProcurementCalendarOccurrence>(
      `${root}/occurrences/${id}/cancel`,
      { rowVersion, reason }
    ),
  runs: () => apiService.get<ProcurementCalendarRun[]>(`${root}/runs`),
  run: (reason: string, horizonDays?: number) =>
    apiService.post<ProcurementCalendarRun>(`${root}/runs`, {
      reason,
      horizonDays,
    }),
};
