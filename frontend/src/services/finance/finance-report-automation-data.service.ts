import { apiService } from '@/services/api.service';

export interface FinanceReportSchedule {
  id: string;
  reportTemplateId: string;
  reportId: string;
  name: string;
  reportName: string;
  templateName: string;
  templateVersion: number;
  frequency: string;
  timeOfDay: string;
  dayOfWeek?: number;
  dayOfMonth?: number;
  startDate: string;
  endDate?: string;
  nextExecutionDate?: string;
  lastExecutionDate?: string;
  exportFormat: string;
  recipientUserIds: string[];
  maximumRetryAttempts: number;
  consecutiveFailureCount: number;
  lastError?: string;
  status: string;
  rowVersion: string;
}

export interface FinanceReportExecution {
  id: string;
  scheduleId: string;
  scheduleName: string;
  reportName: string;
  templateVersion?: number;
  scheduledFor?: string;
  startedAt?: string;
  completedAt?: string;
  attemptNumber: number;
  trigger: string;
  status: string;
  errorMessage?: string;
  exportId?: string;
  fileName?: string;
  fileSize?: number;
}

export interface FinanceReportAutomationWorkspace {
  schedules: FinanceReportSchedule[];
  recentExecutions: FinanceReportExecution[];
  templates: Array<{ id: string; name: string; reportName: string; version: number; outputFormats: string[] }>;
  recipients: Array<{ id: string; name: string; email: string }>;
  activeSchedules: number;
  failedExecutions: number;
  artifactsReady: number;
}

export interface CreateFinanceReportSchedule {
  reportTemplateId: string;
  name: string;
  frequency: string;
  timeOfDay: string;
  dayOfWeek?: number;
  dayOfMonth?: number;
  startDate: string;
  endDate?: string;
  exportFormat: string;
  filterOverrides?: Record<string, unknown>;
  recipientUserIds: string[];
  maximumRetryAttempts: number;
}

export interface FinanceReportProcessResult {
  dueCount: number;
  succeededCount: number;
  failedCount: number;
  skippedCount: number;
}

class FinanceReportAutomationDataService {
  // Route literals stay explicit so repository route-contract tests can detect
  // drift between this shared Reports workspace and its Finance API controller.
  getWorkspace = () => apiService.get<FinanceReportAutomationWorkspace>('/finance/report-automation');
  create = (request: CreateFinanceReportSchedule) =>
    apiService.post<FinanceReportSchedule>('/finance/report-automation/schedules', request);
  pause = (item: FinanceReportSchedule, reason: string) =>
    apiService.post<FinanceReportSchedule>(`/finance/report-automation/schedules/${item.id}/pause`, { reason, rowVersion: item.rowVersion });
  resume = (item: FinanceReportSchedule, reason: string) =>
    apiService.post<FinanceReportSchedule>(`/finance/report-automation/schedules/${item.id}/resume`, { reason, rowVersion: item.rowVersion });
  runNow = (id: string) =>
    apiService.post<FinanceReportProcessResult>(`/finance/report-automation/schedules/${id}/run-now`, {});
  processDue = () => apiService.post<FinanceReportProcessResult>('/finance/report-automation/process-due', {});
  download = (exportId: string) => apiService.downloadBlob(`/finance/report-automation/artifacts/${exportId}`);
}

export const financeReportAutomationDataService = new FinanceReportAutomationDataService();
