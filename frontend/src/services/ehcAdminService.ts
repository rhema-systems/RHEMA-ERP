import { apiService } from './api.service';
import type { EhcTicketPriority, EhcTicketType } from './ehcTicketService';

type ApiEnvelope<T> = { success: boolean; data: T; message?: string };

export interface EhcTicketCategoryAdmin {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  appliesToType?: EhcTicketType | null;
  parentCategoryId?: string | null;
}

export interface CreateEhcTicketCategoryAdmin {
  code: string;
  name: string;
  description?: string | null;
  appliesToType?: EhcTicketType | null;
  parentCategoryId?: string | null;
}

export interface EhcSlaTemplateAdmin {
  id: string;
  name: string;
  isActive: boolean;
  ticketType?: EhcTicketType | null;
  priority?: EhcTicketPriority | null;
  categoryId?: string | null;
  categoryName?: string | null;
  firstResponseMinutes: number;
  resolutionMinutes: number;
}

export interface CreateEhcSlaTemplateAdmin {
  name: string;
  isActive: boolean;
  ticketType?: EhcTicketType | null;
  priority?: EhcTicketPriority | null;
  categoryId?: string | null;
  firstResponseMinutes: number;
  resolutionMinutes: number;
}

export interface EhcWorkflowRoutingRuleAdmin {
  id: string;
  name: string;
  isActive: boolean;
  priority: number;
  workflowName: string;
  ticketType?: EhcTicketType | null;
  ticketPriority?: EhcTicketPriority | null;
  categoryId?: string | null;
  categoryName?: string | null;
  subcategoryId?: string | null;
  subcategoryName?: string | null;
  assignedDepartmentId?: string | null;
  assignedDepartmentName?: string | null;
}

export interface CreateEhcWorkflowRoutingRuleAdmin {
  name: string;
  isActive: boolean;
  priority: number;
  workflowName: string;
  ticketType?: EhcTicketType | null;
  ticketPriority?: EhcTicketPriority | null;
  categoryId?: string | null;
  subcategoryId?: string | null;
  assignedDepartmentId?: string | null;
}

export const ehcAdminService = {
  async listCategories(): Promise<EhcTicketCategoryAdmin[]> {
    const res = await apiService.request<ApiEnvelope<EhcTicketCategoryAdmin[]>>('/ehc/admin/categories', { method: 'GET' });
    return res.data ?? [];
  },

  async createCategory(payload: CreateEhcTicketCategoryAdmin): Promise<EhcTicketCategoryAdmin> {
    const res = await apiService.request<ApiEnvelope<EhcTicketCategoryAdmin>>('/ehc/admin/categories', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return res.data;
  },

  async updateCategory(id: string, payload: CreateEhcTicketCategoryAdmin): Promise<EhcTicketCategoryAdmin> {
    const res = await apiService.request<ApiEnvelope<EhcTicketCategoryAdmin>>(`/ehc/admin/categories/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
    return res.data;
  },

  async deleteCategory(id: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/categories/${id}`, { method: 'DELETE' });
  },

  async listSlaTemplates(): Promise<EhcSlaTemplateAdmin[]> {
    const res = await apiService.request<ApiEnvelope<EhcSlaTemplateAdmin[]>>('/ehc/admin/sla-templates', { method: 'GET' });
    return res.data ?? [];
  },

  async createSlaTemplate(payload: CreateEhcSlaTemplateAdmin): Promise<EhcSlaTemplateAdmin> {
    const res = await apiService.request<ApiEnvelope<EhcSlaTemplateAdmin>>('/ehc/admin/sla-templates', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return res.data;
  },

  async updateSlaTemplate(id: string, payload: CreateEhcSlaTemplateAdmin): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/sla-templates/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
  },

  async deleteSlaTemplate(id: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/sla-templates/${id}`, { method: 'DELETE' });
  },

  async listWorkflowRoutingRules(): Promise<EhcWorkflowRoutingRuleAdmin[]> {
    const res = await apiService.request<ApiEnvelope<EhcWorkflowRoutingRuleAdmin[]>>('/ehc/admin/workflow-routing-rules', { method: 'GET' });
    return res.data ?? [];
  },

  async createWorkflowRoutingRule(payload: CreateEhcWorkflowRoutingRuleAdmin): Promise<{ id: string }> {
    const res = await apiService.request<ApiEnvelope<{ id: string }>>('/ehc/admin/workflow-routing-rules', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return res.data;
  },

  async updateWorkflowRoutingRule(id: string, payload: CreateEhcWorkflowRoutingRuleAdmin): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/workflow-routing-rules/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
  },

  async deleteWorkflowRoutingRule(id: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/workflow-routing-rules/${id}`, { method: 'DELETE' });
  },
};

export default ehcAdminService;
