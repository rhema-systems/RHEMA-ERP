import { apiService } from './api.service';

type ApiEnvelope<T> = { success: boolean; data: T; message?: string };

export type EhcServiceRequestStatus =
  | 'Draft'
  | 'Submitted'
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected'
  | 'Fulfilled'
  | 'Closed'
  | 'Cancelled';

export interface EhcServiceRequestType {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  workflowName?: string | null;
  formDefinitionJson: string;
}

export interface EhcServiceRequestSummary {
  id: string;
  requestNumber: string;
  requestTypeName: string;
  status: EhcServiceRequestStatus;
  title?: string | null;
  submittedAtUtc?: string | null;
}

export interface EhcServiceRequestDetail {
  id: string;
  requestNumber: string;
  requestTypeId: string;
  requestTypeName: string;
  status: EhcServiceRequestStatus;
  title?: string | null;
  formDefinitionJson: string;
  formDataJson: string;
  workflowInstanceId?: string | null;
  submittedAtUtc?: string | null;
  approvedAtUtc?: string | null;
  rejectedAtUtc?: string | null;
  rejectionReason?: string | null;
  attachments?: EhcServiceRequestAttachment[];
}

export interface EhcServiceRequestAttachment {
  id: string;
  filePath: string;
  publicUrl?: string | null;
  fileName: string;
  contentType?: string | null;
  fileSize: number;
  isInternal: boolean;
  createdAtUtc: string;
}

export interface WorkflowApprovalItem {
  entityId: string;
  entityType: string;
  entityTitle: string;
  entityDescription: string;
  currentStep: string;
  submittedAt: string;
  submittedBy: string;
  priority: string;
  daysPending: number;
}

export interface CreateEhcServiceRequest {
  requestTypeId: string;
  title?: string | null;
  formDataJson: string;
  captchaToken?: string | null;
}

export const ehcServiceCatalogService = {
  async listRequestTypesExternal(): Promise<EhcServiceRequestType[]> {
    const res = await apiService.request<ApiEnvelope<EhcServiceRequestType[]>>('/ehc/external/service-catalog/request-types', { method: 'GET' });
    return res.data ?? [];
  },

  async listRequestTypesInternal(): Promise<EhcServiceRequestType[]> {
    const res = await apiService.request<ApiEnvelope<EhcServiceRequestType[]>>('/ehc/internal/service-catalog/request-types', { method: 'GET' });
    return res.data ?? [];
  },

  async listMyRequestsExternal(): Promise<EhcServiceRequestSummary[]> {
    const res = await apiService.request<ApiEnvelope<EhcServiceRequestSummary[]>>('/ehc/external/service-catalog/requests', { method: 'GET' });
    return res.data ?? [];
  },

  async getMyRequestExternal(id: string): Promise<EhcServiceRequestDetail> {
    const res = await apiService.request<ApiEnvelope<EhcServiceRequestDetail>>(`/ehc/external/service-catalog/requests/${id}`, { method: 'GET' });
    return res.data;
  },

  async createRequestExternal(payload: CreateEhcServiceRequest): Promise<EhcServiceRequestDetail> {
    const res = await apiService.request<ApiEnvelope<EhcServiceRequestDetail>>('/ehc/external/service-catalog/requests', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return res.data;
  },

  async createRequestInternal(payload: Omit<CreateEhcServiceRequest, 'captchaToken'> & { captchaToken?: never }): Promise<EhcServiceRequestDetail> {
    const res = await apiService.request<ApiEnvelope<EhcServiceRequestDetail>>('/ehc/internal/service-catalog/requests', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return res.data;
  },

  async listRequestsInternal(take = 200): Promise<EhcServiceRequestSummary[]> {
    const res = await apiService.request<ApiEnvelope<EhcServiceRequestSummary[]>>(`/ehc/internal/service-catalog/requests?take=${take}`, { method: 'GET' });
    return res.data ?? [];
  },

  async getRequestInternal(id: string): Promise<EhcServiceRequestDetail> {
    const res = await apiService.request<ApiEnvelope<EhcServiceRequestDetail>>(`/ehc/internal/service-catalog/requests/${id}`, { method: 'GET' });
    return res.data;
  },

  async listPendingApprovalsInternal(): Promise<WorkflowApprovalItem[]> {
    const res = await apiService.request<ApiEnvelope<WorkflowApprovalItem[]>>('/ehc/internal/service-catalog/approvals/pending', { method: 'GET' });
    return res.data ?? [];
  },

  async addAttachmentExternal(requestId: string, payload: { filePath: string; fileName: string; contentType?: string | null; fileSize: number }): Promise<any> {
    const res = await apiService.request<ApiEnvelope<any>>(`/ehc/external/service-catalog/requests/${requestId}/attachments`, {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return res.data;
  },

  async addAttachmentInternal(
    requestId: string,
    payload: { filePath: string; fileName: string; contentType?: string | null; fileSize: number; isInternal?: boolean },
  ): Promise<any> {
    const res = await apiService.request<ApiEnvelope<any>>(`/ehc/internal/service-catalog/requests/${requestId}/attachments`, {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return res.data;
  },

  async approveInternal(id: string, comments?: string | null): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/internal/service-catalog/requests/${id}/approve`, {
      method: 'POST',
      body: JSON.stringify({ comments: comments || null }),
    });
  },

  async rejectInternal(id: string, reason: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/internal/service-catalog/requests/${id}/reject`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    });
  },

  async fulfillInternal(id: string, notes?: string | null): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/internal/service-catalog/requests/${id}/fulfill`, {
      method: 'POST',
      body: JSON.stringify({ notes: notes || null }),
    });
  },

  async closeInternal(id: string, notes?: string | null): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/internal/service-catalog/requests/${id}/close`, {
      method: 'POST',
      body: JSON.stringify({ notes: notes || null }),
    });
  },
};
