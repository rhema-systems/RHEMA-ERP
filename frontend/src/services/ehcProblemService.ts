import { apiService } from './api.service';
import type { EhcTicketPriority, EhcTicketStatus } from './ehcTicketService';

type ApiEnvelope<T> = { success: boolean; data: T; message?: string };

export type EhcProblemStatus = 'Open' | 'InProgress' | 'Resolved' | 'Closed';
export type EhcCapaTaskStatus = 'Open' | 'InProgress' | 'Done' | 'Cancelled';

export interface EhcProblemListItem {
  id: string;
  problemNumber: string;
  title: string;
  status: EhcProblemStatus;
  priority: EhcTicketPriority;
  departmentName?: string | null;
  ownerName?: string | null;
  linkedTicketsCount: number;
  createdAt: string;
}

export interface EhcProblemLinkedTicket {
  ticketId: string;
  ticketNumber: string;
  subject?: string | null;
  status: EhcTicketStatus;
  priority: EhcTicketPriority;
  createdAt: string;
  notes?: string | null;
  linkedAt: string;
}

export interface EhcCapaTask {
  id: string;
  title: string;
  description?: string | null;
  status: EhcCapaTaskStatus;
  assignedToUserId?: string | null;
  assignedToName?: string | null;
  assignedDepartmentId?: string | null;
  assignedDepartmentName?: string | null;
  dueAt?: string | null;
  completedAt?: string | null;
  createdAt: string;
}

export interface EhcProblemAuditEvent {
  id: string;
  eventType: string;
  title?: string | null;
  body?: string | null;
  actorUserId?: string | null;
  actorName?: string | null;
  createdAt: string;
}

export interface EhcProblemDetail {
  id: string;
  problemNumber: string;
  title: string;
  description: string;
  status: EhcProblemStatus;
  priority: EhcTicketPriority;
  categoryId?: string | null;
  categoryName?: string | null;
  subcategoryId?: string | null;
  subcategoryName?: string | null;
  departmentId?: string | null;
  departmentName?: string | null;
  ownerUserId?: string | null;
  ownerName?: string | null;
  rootCauseId?: string | null;
  rootCauseCode?: string | null;
  rootCauseName?: string | null;
  rootCauseDetails?: string | null;
  resolutionSummary?: string | null;
  createdFromTicketId?: string | null;
  createdFromTicketNumber?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  linkedTickets: EhcProblemLinkedTicket[];
  capaTasks: EhcCapaTask[];
  auditTrail: EhcProblemAuditEvent[];
}

export interface CreateEhcProblemRequest {
  title?: string | null;
  description?: string | null;
  priority?: EhcTicketPriority;
  categoryId?: string | null;
  subcategoryId?: string | null;
  departmentId?: string | null;
  ownerUserId?: string | null;
}

export interface UpdateEhcProblemRequest extends CreateEhcProblemRequest {
  status: EhcProblemStatus;
  rootCauseId?: string | null;
  rootCauseDetails?: string | null;
  resolutionSummary?: string | null;
}

export interface LinkTicketToProblemRequest {
  ticketId: string;
  notes?: string | null;
}

export interface CreateCapaTaskRequest {
  title?: string | null;
  description?: string | null;
  assignedToUserId?: string | null;
  assignedDepartmentId?: string | null;
  dueAt?: string | null;
}

export interface UpdateCapaTaskRequest extends CreateCapaTaskRequest {
  status: EhcCapaTaskStatus;
}

export interface EhcProblemFilters {
  q?: string | null;
  status?: EhcProblemStatus | null;
  priority?: EhcTicketPriority | null;
  departmentId?: string | null;
  ownerUserId?: string | null;
  createdFrom?: string | null; // yyyy-mm-dd
  createdTo?: string | null; // yyyy-mm-dd
}

export const ehcProblemService = {
  async listProblems(page = 1, pageSize = 25, filters?: EhcProblemFilters): Promise<EhcProblemListItem[]> {
    const qs = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (filters?.q) qs.set('q', filters.q);
    if (filters?.status) qs.set('status', filters.status);
    if (filters?.priority) qs.set('priority', filters.priority);
    if (filters?.departmentId) qs.set('departmentId', filters.departmentId);
    if (filters?.ownerUserId) qs.set('ownerUserId', filters.ownerUserId);
    if (filters?.createdFrom) qs.set('createdFrom', filters.createdFrom);
    if (filters?.createdTo) qs.set('createdTo', filters.createdTo);
    const res = await apiService.request<ApiEnvelope<EhcProblemListItem[]>>(`/ehc/internal/problems?${qs.toString()}`, { method: 'GET' });
    return res.data ?? [];
  },

  async getProblem(id: string): Promise<EhcProblemDetail> {
    const res = await apiService.request<ApiEnvelope<EhcProblemDetail>>(`/ehc/internal/problems/${id}`, { method: 'GET' });
    return res.data;
  },

  async createProblem(payload: CreateEhcProblemRequest): Promise<EhcProblemDetail> {
    const res = await apiService.request<ApiEnvelope<EhcProblemDetail>>('/ehc/internal/problems', {
      method: 'POST',
      body: JSON.stringify(payload ?? {}),
    });
    return res.data;
  },

  async updateProblem(id: string, payload: UpdateEhcProblemRequest): Promise<EhcProblemDetail> {
    const res = await apiService.request<ApiEnvelope<EhcProblemDetail>>(`/ehc/internal/problems/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload ?? {}),
    });
    return res.data;
  },

  async deleteProblem(id: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/internal/problems/${id}`, { method: 'DELETE' });
  },

  async linkTicket(problemId: string, payload: LinkTicketToProblemRequest): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/internal/problems/${problemId}/tickets`, {
      method: 'POST',
      body: JSON.stringify(payload ?? {}),
    });
  },

  async unlinkTicket(problemId: string, ticketId: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/internal/problems/${problemId}/tickets/${ticketId}`, { method: 'DELETE' });
  },

  async createCapaTask(problemId: string, payload: CreateCapaTaskRequest): Promise<EhcCapaTask> {
    const res = await apiService.request<ApiEnvelope<EhcCapaTask>>(`/ehc/internal/problems/${problemId}/tasks`, {
      method: 'POST',
      body: JSON.stringify(payload ?? {}),
    });
    return res.data;
  },

  async updateCapaTask(problemId: string, taskId: string, payload: UpdateCapaTaskRequest): Promise<EhcCapaTask> {
    const res = await apiService.request<ApiEnvelope<EhcCapaTask>>(`/ehc/internal/problems/${problemId}/tasks/${taskId}`, {
      method: 'PUT',
      body: JSON.stringify(payload ?? {}),
    });
    return res.data;
  },

  async deleteCapaTask(problemId: string, taskId: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/internal/problems/${problemId}/tasks/${taskId}`, { method: 'DELETE' });
  },
};

