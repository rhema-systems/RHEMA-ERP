import { apiService } from './api.service';

type ApiEnvelope<T> = { success: boolean; data: T; message?: string };

export interface DataRetentionPolicy {
  id: string;
  tenantId: string;
  enabled: boolean;
  auditLogRetentionDays: number;
  securityLogRetentionDays: number;
  notificationRetentionDays: number;
  ehcAuditEventRetentionDays: number;
  workflowAuditRetentionDays: number;
}

export interface UpdateDataRetentionPolicyRequest {
  enabled: boolean;
  auditLogRetentionDays: number;
  securityLogRetentionDays: number;
  notificationRetentionDays: number;
  ehcAuditEventRetentionDays: number;
  workflowAuditRetentionDays: number;
}

export interface DataRetentionJobRun {
  id: string;
  tenantId: string;
  jobName: string;
  startedAtUtc: string;
  completedAtUtc?: string | null;
  success: boolean;
  countsJson?: string | null;
  error?: string | null;
}

export const dataRetentionService = {
  async getPolicy(): Promise<DataRetentionPolicy | null> {
    const res = await apiService.request<
      ApiEnvelope<DataRetentionPolicy | null>
    >('/admin/retention/policy', { method: 'GET' });
    return res.data ?? null;
  },

  async updatePolicy(payload: UpdateDataRetentionPolicyRequest): Promise<void> {
    await apiService.request<ApiEnvelope<any>>('/admin/retention/policy', {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
  },

  async listRuns(take = 50): Promise<DataRetentionJobRun[]> {
    const qs = new URLSearchParams({ take: String(take) });
    const res = await apiService.request<ApiEnvelope<DataRetentionJobRun[]>>(
      `/admin/retention/runs?${qs.toString()}`,
      { method: 'GET' }
    );
    return res.data ?? [];
  },
};

export default dataRetentionService;
