import { apiService } from '@/services/api.service';
import type { CivilEngineeringComplaintResolution, CivilEngineeringComplaintResolutionTimelineEntry } from '@/types/civil-engineering-complaint-resolution';

const root = '/projects/civil-engineering/complaint-resolutions';

export const civilEngineeringComplaintResolutionService = {
  list: () => apiService.get<CivilEngineeringComplaintResolution[]>(root),
  timeline: (helpdeskTicketId: string) => apiService.get<CivilEngineeringComplaintResolutionTimelineEntry[]>(`${root}/${helpdeskTicketId}/timeline`),
};
