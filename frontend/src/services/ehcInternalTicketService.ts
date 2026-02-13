import { apiService } from './api.service';
import type {
  CreateEhcTicketRequest,
  EhcTicketDetail,
  EhcTicketListItem,
  EhcTicketAttachment,
  EhcTicketMessage,
  EhcTicketPriority,
  EhcTicketSource,
  EhcTicketStatus,
  EhcTicketType,
} from './ehcTicketService';

type ApiEnvelope<T> = { success: boolean; data: T; message?: string };

export interface EhcLookupItem {
  id: string;
  name: string;
}

export interface EhcAdminCategory {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  appliesToType?: EhcTicketType | null;
  parentCategoryId?: string | null;
}

export interface EhcInternalTicketFilters {
  status?: EhcTicketStatus | null;
  ticketType?: EhcTicketType | null;
  priority?: EhcTicketPriority | null;
  source?: EhcTicketSource | null;
  categoryId?: string | null;
  assignedDepartmentId?: string | null;
  createdFrom?: string | null; // yyyy-mm-dd
  createdTo?: string | null; // yyyy-mm-dd
}

export interface EhcHelpdeskSummary {
  totals: {
    total: number;
    open: number;
    firstResponseBreaches: number;
    resolutionBreaches: number;
    avgFirstResponseMinutes?: number | null;
    avgResolutionMinutes?: number | null;
  };
  byStatus: Array<{ status: EhcTicketStatus; count: number }>;
  byPriority: Array<{ priority: EhcTicketPriority; count: number }>;
  byType: Array<{ ticketType: EhcTicketType; count: number }>;
  byDepartment: Array<{ departmentId?: string | null; departmentName: string; count: number }>;
  byCategory: Array<{ categoryId?: string | null; categoryName: string; count: number }>;
  byRootCause?: Array<{ rootCauseId?: string | null; rootCauseName: string; count: number }>;
}

export interface EhcSlaCompliancePoint {
  date: string; // yyyy-mm-dd
  totalTickets: number;
  firstResponseMet: number;
  firstResponseBreached: number;
  resolutionMet: number;
  resolutionBreached: number;
  firstResponseCompliancePercent?: number | null;
  resolutionCompliancePercent?: number | null;
}

export interface EhcAgentPerformanceRow {
  agentUserId?: string | null;
  agentName: string;
  totalAssigned: number;
  openAssigned: number;
  resolvedAssigned: number;
  firstResponseBreaches: number;
  resolutionBreaches: number;
  avgFirstResponseMinutes?: number | null;
  avgResolutionMinutes?: number | null;
}

export interface EhcEscalationReportRow {
  policyId: string;
  policyName: string;
  trigger: string;
  level: number;
  count: number;
}

export interface EhcRelatedEntityLookupItem {
  id: string;
  entityType: string;
  reference: string;
  label: string;
  openUrl?: string | null;
}

export interface EhcRelatedEntityResolve {
  exists: boolean;
  id?: string | null;
  entityType?: string;
  reference?: string;
  label?: string | null;
  openUrl?: string | null;
}

export interface EhcAllowedTicketTransition {
  transitionId?: string | null;
  transitionName?: string | null;
  transitionDescription?: string | null;
  targetStatus: EhcTicketStatus;
}

export interface EhcRootCauseCode {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface UpdateEhcTicketRcaRequest {
  rootCauseId?: string | null;
  rootCauseDetails?: string | null;
  resolutionSummary?: string | null;
}

export const ehcInternalTicketService = {
  async createTicket(request: CreateEhcTicketRequest): Promise<EhcTicketDetail> {
    const res = await apiService.request<ApiEnvelope<EhcTicketDetail>>('/ehc/internal/tickets', {
      method: 'POST',
      body: JSON.stringify(request),
    });
    return res.data;
  },

  async listTickets(
    page = 1,
    pageSize = 25,
    filters?: EhcInternalTicketFilters
  ): Promise<EhcTicketListItem[]> {
    const qs = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (filters?.status) qs.set('status', filters.status);
    if (filters?.ticketType) qs.set('ticketType', filters.ticketType);
    if (filters?.priority) qs.set('priority', filters.priority);
    if (filters?.source) qs.set('source', filters.source);
    if (filters?.categoryId) qs.set('categoryId', filters.categoryId);
    if (filters?.assignedDepartmentId) qs.set('assignedDepartmentId', filters.assignedDepartmentId);
    if (filters?.createdFrom) qs.set('createdFrom', filters.createdFrom);
    if (filters?.createdTo) qs.set('createdTo', filters.createdTo);
    const res = await apiService.request<ApiEnvelope<EhcTicketListItem[]>>(`/ehc/internal/tickets?${qs.toString()}`, {
      method: 'GET',
    });
    return res.data ?? [];
  },

  async getTicket(id: string): Promise<EhcTicketDetail> {
    const res = await apiService.request<ApiEnvelope<EhcTicketDetail>>(`/ehc/internal/tickets/${id}`, { method: 'GET' });
    return res.data;
  },

  async getAllowedTransitions(ticketId: string): Promise<EhcAllowedTicketTransition[]> {
    const res = await apiService.request<ApiEnvelope<EhcAllowedTicketTransition[]>>(`/ehc/internal/tickets/${ticketId}/allowed-transitions`, { method: 'GET' });
    return res.data ?? [];
  },

  async assignTicket(ticketId: string, assignedToUserId: string, assignedDepartmentId?: string | null): Promise<void> {
    const qs = new URLSearchParams({ assignedToUserId });
    if (assignedDepartmentId) qs.set('assignedDepartmentId', assignedDepartmentId);
    await apiService.request<ApiEnvelope<any>>(`/ehc/internal/tickets/${ticketId}/assign?${qs.toString()}`, {
      method: 'POST',
    });
  },

  async transitionTicket(ticketId: string, targetStatus: EhcTicketStatus, notes?: string | null, workflowTransitionId?: string | null, workflowTransitionName?: string | null): Promise<void> {
    const qs = new URLSearchParams({ targetStatus });
    if (workflowTransitionId) qs.set('workflowTransitionId', workflowTransitionId);
    if (workflowTransitionName) qs.set('workflowTransitionName', workflowTransitionName);
    await apiService.request<ApiEnvelope<any>>(`/ehc/internal/tickets/${ticketId}/transition?${qs.toString()}`, {
      method: 'POST',
      body: JSON.stringify(notes || null),
    });
  },

  async addInternalComment(ticketId: string, body: string): Promise<EhcTicketMessage> {
    const res = await apiService.request<ApiEnvelope<EhcTicketMessage>>(`/ehc/internal/tickets/${ticketId}/internal-comments`, {
      method: 'POST',
      body: JSON.stringify({ body }),
    });
    return res.data;
  },

  async addMessage(ticketId: string, body: string): Promise<EhcTicketMessage> {
    const res = await apiService.request<ApiEnvelope<EhcTicketMessage>>(`/ehc/internal/tickets/${ticketId}/messages`, {
      method: 'POST',
      body: JSON.stringify({ body }),
    });
    return res.data;
  },

  async addAttachment(
    ticketId: string,
    attachment: { filePath: string; fileName: string; contentType?: string | null; fileSize: number; messageId?: string | null; isInternal?: boolean }
  ): Promise<EhcTicketAttachment> {
    const res = await apiService.request<ApiEnvelope<EhcTicketAttachment>>(`/ehc/internal/tickets/${ticketId}/attachments`, {
      method: 'POST',
      body: JSON.stringify({
        messageId: attachment.messageId ?? null,
        filePath: attachment.filePath,
        fileName: attachment.fileName,
        contentType: attachment.contentType ?? null,
        fileSize: attachment.fileSize,
        isInternal: attachment.isInternal ?? false,
      }),
    });
    return res.data;
  },

  async listAgents(): Promise<EhcLookupItem[]> {
    const res = await apiService.request<ApiEnvelope<EhcLookupItem[]>>('/ehc/internal/lookups/agents', { method: 'GET' });
    return res.data ?? [];
  },

  async listDepartments(): Promise<EhcLookupItem[]> {
    const res = await apiService.request<ApiEnvelope<EhcLookupItem[]>>('/ehc/internal/lookups/departments', { method: 'GET' });
    return res.data ?? [];
  },

  async getMyDepartment(): Promise<EhcLookupItem | null> {
    const res = await apiService.request<ApiEnvelope<EhcLookupItem | null>>('/ehc/internal/lookups/my-department', { method: 'GET' });
    return res.data ?? null;
  },

  async listCategories(): Promise<EhcAdminCategory[]> {
    const res = await apiService.request<ApiEnvelope<EhcAdminCategory[]>>('/ehc/admin/categories', { method: 'GET' });
    return res.data ?? [];
  },

  async getSummary(): Promise<EhcHelpdeskSummary> {
    const res = await apiService.request<ApiEnvelope<EhcHelpdeskSummary>>('/ehc/internal/reports/summary', { method: 'GET' });
    return res.data as any;
  },

  async getSlaCompliance(days = 30): Promise<EhcSlaCompliancePoint[]> {
    const qs = new URLSearchParams({ days: String(days) });
    const res = await apiService.request<ApiEnvelope<EhcSlaCompliancePoint[]>>(`/ehc/internal/reports/sla-compliance?${qs.toString()}`, { method: 'GET' });
    return (res.data ?? []) as any;
  },

  async getAgentPerformance(days = 30): Promise<EhcAgentPerformanceRow[]> {
    const qs = new URLSearchParams({ days: String(days) });
    const res = await apiService.request<ApiEnvelope<EhcAgentPerformanceRow[]>>(`/ehc/internal/reports/agent-performance?${qs.toString()}`, { method: 'GET' });
    return (res.data ?? []) as any;
  },

  async getEscalations(days = 30): Promise<EhcEscalationReportRow[]> {
    const qs = new URLSearchParams({ days: String(days) });
    const res = await apiService.request<ApiEnvelope<EhcEscalationReportRow[]>>(`/ehc/internal/reports/escalations?${qs.toString()}`, { method: 'GET' });
    return (res.data ?? []) as any;
  },

  async searchRelatedEntities(entityType: string, q: string, limit = 20): Promise<EhcRelatedEntityLookupItem[]> {
    const qs = new URLSearchParams({ entityType, q, limit: String(limit) });
    const res = await apiService.request<ApiEnvelope<EhcRelatedEntityLookupItem[]>>(`/ehc/internal/related-entities/search?${qs.toString()}`, { method: 'GET' });
    return (res.data ?? []) as any;
  },

  async resolveRelatedEntity(entityType: string, reference: string): Promise<EhcRelatedEntityResolve> {
    const qs = new URLSearchParams({ entityType, reference });
    const res = await apiService.request<ApiEnvelope<EhcRelatedEntityResolve>>(`/ehc/internal/related-entities/resolve?${qs.toString()}`, { method: 'GET' });
    return res.data as any;
  },

  async listRootCauses(): Promise<EhcRootCauseCode[]> {
    const res = await apiService.request<ApiEnvelope<EhcRootCauseCode[]>>('/ehc/internal/lookups/root-causes', { method: 'GET' });
    return (res.data ?? []) as any;
  },

  async updateRca(ticketId: string, payload: UpdateEhcTicketRcaRequest): Promise<EhcTicketDetail> {
    const res = await apiService.request<ApiEnvelope<EhcTicketDetail>>(`/ehc/internal/tickets/${ticketId}/rca`, {
      method: 'PUT',
      body: JSON.stringify(payload ?? {}),
    });
    return res.data;
  },
};

export default ehcInternalTicketService;
