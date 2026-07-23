import { apiService } from '@/services/api.service';
import type {
  AcknowledgeProcurementApp,
  ProcurementAppSubmission,
  ProcurementAppSubmissionPage,
  ProcurementAppSubmissionPlanOption,
  ProcurementAppSubmissionSearch,
  ProcurementAppSubmissionSummary,
  RecordProcurementAppExport,
  RejectProcurementApp,
  ResubmitProcurementApp,
  SubmitProcurementApp,
} from '@/types/procurement-app-submission';

const root = '/procurement/app-submissions';

export const procurementAppSubmissionService = {
  summary: () =>
    apiService.get<ProcurementAppSubmissionSummary>(`${root}/summary`),
  publishedPlans: () =>
    apiService.get<ProcurementAppSubmissionPlanOption[]>(
      `${root}/published-plans`
    ),
  search: (request: ProcurementAppSubmissionSearch) =>
    apiService.get<ProcurementAppSubmissionPage>(
      root,
      request as Record<string, unknown>
    ),
  get: (id: string) =>
    apiService.get<ProcurementAppSubmission>(`${root}/${id}`),
  recordExport: (request: RecordProcurementAppExport) =>
    apiService.post<ProcurementAppSubmission>(`${root}/exports`, request),
  submit: (id: string, request: SubmitProcurementApp) =>
    apiService.post<ProcurementAppSubmission>(`${root}/${id}/submit`, request),
  acknowledge: (id: string, request: AcknowledgeProcurementApp) =>
    apiService.post<ProcurementAppSubmission>(
      `${root}/${id}/acknowledge`,
      request
    ),
  reject: (id: string, request: RejectProcurementApp) =>
    apiService.post<ProcurementAppSubmission>(`${root}/${id}/reject`, request),
  resubmit: (id: string, request: ResubmitProcurementApp) =>
    apiService.post<ProcurementAppSubmission>(
      `${root}/${id}/resubmit`,
      request
    ),
};
