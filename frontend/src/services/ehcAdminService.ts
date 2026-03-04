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
  calendarConfigurationJson?: string | null;
}

export interface CreateEhcSlaTemplateAdmin {
  name: string;
  isActive: boolean;
  ticketType?: EhcTicketType | null;
  priority?: EhcTicketPriority | null;
  categoryId?: string | null;
  firstResponseMinutes: number;
  resolutionMinutes: number;
  calendarConfigurationJson?: string | null;
}

export interface EhcServiceRequestTypeAdmin {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  workflowName?: string | null;
  formDefinitionJson: string;
}

export interface UpsertEhcServiceRequestTypeAdmin {
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  workflowName?: string | null;
  formDefinitionJson: string;
}

export interface EhcInboundEmailChannelAdmin {
  id: string;
  mailboxAddress: string;
  folderName: string;
  isEnabled: boolean;
  requireKnownSender: boolean;
  autoProvisionUnknownSenders: boolean;
  defaultCategoryId?: string | null;
  defaultTicketType: EhcTicketType;
  defaultPriority: EhcTicketPriority;
  useGraphWebhook?: boolean;
  graphSubscriptionId?: string | null;
  graphSubscriptionExpiresAtUtc?: string | null;
  lastWebhookReceivedAtUtc?: string | null;
  lastAttemptAtUtc?: string | null;
  lastSuccessAtUtc?: string | null;
  lastProcessedMessageCount?: number;
  consecutiveFailureCount?: number;
  lastSyncedAtUtc?: string | null;
  lastError?: string | null;
}

export interface UpsertEhcInboundEmailChannelAdmin {
  mailboxAddress: string;
  folderName?: string | null;
  isEnabled: boolean;
  requireKnownSender: boolean;
  autoProvisionUnknownSenders: boolean;
  defaultCategoryId?: string | null;
  defaultTicketType: EhcTicketType;
  defaultPriority: EhcTicketPriority;
  useGraphWebhook?: boolean;
}

export type EhcInboundMessagingProvider = 'Twilio';

export interface EhcInboundMessagingChannelAdmin {
  id: string;
  provider: EhcInboundMessagingProvider | string;
  source: 'Sms' | 'WhatsApp';
  toAddress: string;
  isEnabled: boolean;
  requireKnownSender: boolean;
  autoProvisionUnknownSenders: boolean;
  defaultCategoryId?: string | null;
  defaultTicketType: EhcTicketType;
  defaultPriority: EhcTicketPriority;
}

export interface UpsertEhcInboundMessagingChannelAdmin {
  provider: EhcInboundMessagingProvider | string;
  source: 'Sms' | 'WhatsApp';
  toAddress: string;
  isEnabled: boolean;
  requireKnownSender: boolean;
  autoProvisionUnknownSenders: boolean;
  defaultCategoryId?: string | null;
  defaultTicketType: EhcTicketType;
  defaultPriority: EhcTicketPriority;
}

export interface EhcComplianceSummaryAdmin {
  activeLegalHolds: number;
  activeRetentionCategoryExceptions: number;
  auditExports: number;
}

export interface DataRetentionJobRunAdmin {
  startedAtUtc: string;
  completedAtUtc?: string | null;
  success: boolean;
  countsJson?: string | null;
  error?: string | null;
}

export interface EhcComplianceReportAdmin {
  generatedAtUtc: string;
  summary: EhcComplianceSummaryAdmin;
  retentionCategoryExceptions: EhcRetentionCategoryExceptionAdmin[];
  legalHolds: EhcLegalHoldAdmin[];
  auditExports: EhcComplianceAuditExportAdmin[];
  retentionRuns: DataRetentionJobRunAdmin[];
}

export interface EhcRetentionCategoryExceptionAdmin {
  id: string;
  categoryId: string;
  categoryName?: string | null;
  isActive: boolean;
  auditEventRetentionDays: number;
  notes?: string | null;
}

export interface UpsertEhcRetentionCategoryExceptionAdmin {
  categoryId: string;
  isActive: boolean;
  auditEventRetentionDays: number;
  notes?: string | null;
}

export interface EhcLegalHoldAdmin {
  id: string;
  ticketId: string;
  ticketNumber?: string | null;
  isActive: boolean;
  reason?: string | null;
  referenceNumber?: string | null;
  createdAtUtc: string;
  releasedAtUtc?: string | null;
}

export interface CreateEhcLegalHoldAdmin {
  ticketId: string;
  reason?: string | null;
  referenceNumber?: string | null;
}

export interface EhcComplianceAuditExportAdmin {
  id: string;
  fromUtc: string;
  toUtc: string;
  ticketId?: string | null;
  categoryId?: string | null;
  filePath: string;
  publicUrl: string;
  sha256: string;
  rowCount: number;
  createdAtUtc: string;
}

export interface CreateEhcComplianceAuditExportAdmin {
  fromUtc: string;
  toUtc: string;
  ticketId?: string | null;
  categoryId?: string | null;
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

export type EhcEscalationTrigger =
  | 'FirstResponseDueSoon'
  | 'FirstResponseBreached'
  | 'ResolutionDueSoon'
  | 'ResolutionBreached';

export interface EhcEscalationPolicyLevelAdmin {
  level: number;
  delayMinutes: number;
  notifyRoles: string[];
  notifyAssignedAgent: boolean;
  notifyUserId?: string | null;
  addInternalComment: boolean;
  reassignToRole?: string | null;
  reassignToUserId?: string | null;
}

export interface EhcEscalationPolicyAdmin {
  id: string;
  name: string;
  isActive: boolean;
  priority: number;
  trigger: EhcEscalationTrigger;
  dueSoonMinutes: number;
  ticketType?: EhcTicketType | null;
  ticketPriority?: EhcTicketPriority | null;
  categoryId?: string | null;
  subcategoryId?: string | null;
  departmentId?: string | null;
  levels: EhcEscalationPolicyLevelAdmin[];
}

export interface CreateEhcEscalationPolicyAdmin {
  name: string;
  isActive: boolean;
  priority: number;
  trigger: EhcEscalationTrigger;
  dueSoonMinutes: number;
  ticketType?: EhcTicketType | null;
  ticketPriority?: EhcTicketPriority | null;
  categoryId?: string | null;
  subcategoryId?: string | null;
  departmentId?: string | null;
  levels: EhcEscalationPolicyLevelAdmin[];
}

export interface EhcCannedResponseAdmin {
  id: string;
  code: string;
  title: string;
  body: string;
  isActive: boolean;
  appliesToType?: EhcTicketType | null;
  categoryId?: string | null;
}

export interface CreateEhcCannedResponseAdmin {
  code: string;
  title: string;
  body: string;
  isActive: boolean;
  appliesToType?: EhcTicketType | null;
  categoryId?: string | null;
}

export interface EhcKnowledgeBaseCategoryAdmin {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface CreateEhcKnowledgeBaseCategoryAdmin {
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface EhcKnowledgeBaseArticleAdmin {
  id: string;
  code: string;
  title: string;
  summary?: string | null;
  body?: string | null;
  categoryId?: string | null;
  categoryName?: string | null;
  tagsCsv?: string | null;
  isPublished: boolean;
  isInternalOnly: boolean;
  viewCount: number;
  createdAt: string;
}

export interface CreateEhcKnowledgeBaseArticleAdmin {
  code: string;
  title: string;
  summary?: string | null;
  body: string;
  categoryId?: string | null;
  tagsCsv?: string | null;
  isPublished: boolean;
  isInternalOnly: boolean;
}

export interface EhcFaqCategoryAdmin {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface CreateEhcFaqCategoryAdmin {
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface EhcFaqItemAdmin {
  id: string;
  question: string;
  categoryId?: string | null;
  categoryName?: string | null;
  isPublished: boolean;
  isInternalOnly: boolean;
  sortOrder: number;
  viewCount: number;
  createdAt: string;
}

export interface EhcFaqItemDetailAdmin extends EhcFaqItemAdmin {
  answer: string;
}

export interface CreateEhcFaqItemAdmin {
  question: string;
  answer: string;
  categoryId?: string | null;
  isPublished: boolean;
  isInternalOnly: boolean;
  sortOrder: number;
}

export interface EhcTicketPriorityLevelAdmin {
  priority: EhcTicketPriority;
  displayName: string;
  description?: string | null;
  isActive: boolean;
  sortOrder: number;
}

export interface UpdateEhcTicketPriorityLevelAdmin {
  displayName: string;
  description?: string | null;
  isActive: boolean;
  sortOrder: number;
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

  async listServiceRequestTypes(): Promise<EhcServiceRequestTypeAdmin[]> {
    const res = await apiService.request<ApiEnvelope<EhcServiceRequestTypeAdmin[]>>('/ehc/admin/service-catalog/request-types', { method: 'GET' });
    return res.data ?? [];
  },

  async createServiceRequestType(payload: UpsertEhcServiceRequestTypeAdmin): Promise<EhcServiceRequestTypeAdmin> {
    const res = await apiService.request<ApiEnvelope<EhcServiceRequestTypeAdmin>>('/ehc/admin/service-catalog/request-types', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return res.data;
  },

  async updateServiceRequestType(id: string, payload: UpsertEhcServiceRequestTypeAdmin): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/service-catalog/request-types/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
  },

  async deleteServiceRequestType(id: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/service-catalog/request-types/${id}`, { method: 'DELETE' });
  },

  async listInboundEmailChannels(): Promise<EhcInboundEmailChannelAdmin[]> {
    const res = await apiService.request<ApiEnvelope<EhcInboundEmailChannelAdmin[]>>('/ehc/admin/channels/email-inbound', { method: 'GET' });
    return res.data ?? [];
  },

  async createInboundEmailChannel(payload: UpsertEhcInboundEmailChannelAdmin): Promise<EhcInboundEmailChannelAdmin> {
    const res = await apiService.request<ApiEnvelope<EhcInboundEmailChannelAdmin>>('/ehc/admin/channels/email-inbound', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return res.data;
  },

  async updateInboundEmailChannel(id: string, payload: UpsertEhcInboundEmailChannelAdmin): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/channels/email-inbound/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
  },

  async deleteInboundEmailChannel(id: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/channels/email-inbound/${id}`, { method: 'DELETE' });
  },

  async testInboundEmailChannel(id: string): Promise<any> {
    const res = await apiService.request<ApiEnvelope<any>>(`/ehc/admin/channels/email-inbound/${id}/test`, { method: 'POST' });
    return res.data;
  },

  async subscribeInboundEmailWebhook(id: string, expiresInMinutes?: number | null): Promise<any> {
    const res = await apiService.request<ApiEnvelope<any>>(`/ehc/admin/channels/email-inbound/${id}/webhook/subscribe`, {
      method: 'POST',
      body: JSON.stringify({ expiresInMinutes: expiresInMinutes ?? null }),
    });
    return res.data;
  },

  async listInboundMessagingChannels(): Promise<EhcInboundMessagingChannelAdmin[]> {
    const res = await apiService.request<ApiEnvelope<EhcInboundMessagingChannelAdmin[]>>('/ehc/admin/channels/messaging-inbound', { method: 'GET' });
    return res.data ?? [];
  },

  async createInboundMessagingChannel(payload: UpsertEhcInboundMessagingChannelAdmin): Promise<EhcInboundMessagingChannelAdmin> {
    const res = await apiService.request<ApiEnvelope<EhcInboundMessagingChannelAdmin>>('/ehc/admin/channels/messaging-inbound', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return res.data;
  },

  async updateInboundMessagingChannel(id: string, payload: UpsertEhcInboundMessagingChannelAdmin): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/channels/messaging-inbound/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
  },

  async deleteInboundMessagingChannel(id: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/channels/messaging-inbound/${id}`, { method: 'DELETE' });
  },

  async getComplianceSummary(): Promise<EhcComplianceSummaryAdmin> {
    const res = await apiService.request<ApiEnvelope<EhcComplianceSummaryAdmin>>('/ehc/admin/compliance/summary', { method: 'GET' });
    return res.data ?? { activeLegalHolds: 0, activeRetentionCategoryExceptions: 0, auditExports: 0 };
  },

  async listRetentionCategoryExceptions(): Promise<EhcRetentionCategoryExceptionAdmin[]> {
    const res = await apiService.request<ApiEnvelope<EhcRetentionCategoryExceptionAdmin[]>>('/ehc/admin/compliance/retention/category-exceptions', { method: 'GET' });
    return res.data ?? [];
  },

  async upsertRetentionCategoryException(payload: UpsertEhcRetentionCategoryExceptionAdmin): Promise<void> {
    await apiService.request<ApiEnvelope<any>>('/ehc/admin/compliance/retention/category-exceptions', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async listLegalHolds(includeInactive = false): Promise<EhcLegalHoldAdmin[]> {
    const res = await apiService.request<ApiEnvelope<EhcLegalHoldAdmin[]>>(`/ehc/admin/compliance/legal-holds?includeInactive=${includeInactive ? 'true' : 'false'}`, {
      method: 'GET',
    });
    return res.data ?? [];
  },

  async createLegalHold(payload: CreateEhcLegalHoldAdmin): Promise<void> {
    await apiService.request<ApiEnvelope<any>>('/ehc/admin/compliance/legal-holds', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async releaseLegalHold(id: string, notes?: string | null): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/compliance/legal-holds/${id}/release`, {
      method: 'POST',
      body: JSON.stringify({ notes: notes || null }),
    });
  },

  async listAuditExports(): Promise<EhcComplianceAuditExportAdmin[]> {
    const res = await apiService.request<ApiEnvelope<EhcComplianceAuditExportAdmin[]>>('/ehc/admin/compliance/audit-exports', { method: 'GET' });
    return res.data ?? [];
  },

  async createAuditExport(payload: CreateEhcComplianceAuditExportAdmin): Promise<EhcComplianceAuditExportAdmin> {
    const res = await apiService.request<ApiEnvelope<EhcComplianceAuditExportAdmin>>('/ehc/admin/compliance/audit-exports', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return res.data;
  },

  async listComplianceRetentionRuns(take = 50): Promise<DataRetentionJobRunAdmin[]> {
    const res = await apiService.request<ApiEnvelope<DataRetentionJobRunAdmin[]>>(`/ehc/admin/compliance/retention/runs?take=${take}`, { method: 'GET' });
    return res.data ?? [];
  },

  async getComplianceReport(): Promise<EhcComplianceReportAdmin> {
    const res = await apiService.request<ApiEnvelope<EhcComplianceReportAdmin>>('/ehc/admin/compliance/report', { method: 'GET' });
    return res.data;
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

  async listEscalationPolicies(): Promise<EhcEscalationPolicyAdmin[]> {
    const res = await apiService.request<ApiEnvelope<EhcEscalationPolicyAdmin[]>>('/ehc/admin/escalation-policies', { method: 'GET' });
    return res.data ?? [];
  },

  async createEscalationPolicy(payload: CreateEhcEscalationPolicyAdmin): Promise<EhcEscalationPolicyAdmin> {
    const res = await apiService.request<ApiEnvelope<EhcEscalationPolicyAdmin>>('/ehc/admin/escalation-policies', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return res.data;
  },

  async updateEscalationPolicy(id: string, payload: CreateEhcEscalationPolicyAdmin): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/escalation-policies/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
  },

  async deleteEscalationPolicy(id: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/escalation-policies/${id}`, { method: 'DELETE' });
  },

  async listCannedResponses(): Promise<EhcCannedResponseAdmin[]> {
    const res = await apiService.request<ApiEnvelope<EhcCannedResponseAdmin[]>>('/ehc/admin/canned-responses', { method: 'GET' });
    return res.data ?? [];
  },

  async createCannedResponse(payload: CreateEhcCannedResponseAdmin): Promise<EhcCannedResponseAdmin> {
    const res = await apiService.request<ApiEnvelope<EhcCannedResponseAdmin>>('/ehc/admin/canned-responses', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return res.data;
  },

  async updateCannedResponse(id: string, payload: CreateEhcCannedResponseAdmin): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/canned-responses/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
  },

  async deleteCannedResponse(id: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/canned-responses/${id}`, { method: 'DELETE' });
  },

  async listKbCategories(): Promise<EhcKnowledgeBaseCategoryAdmin[]> {
    const res = await apiService.request<ApiEnvelope<EhcKnowledgeBaseCategoryAdmin[]>>('/ehc/admin/kb/categories', { method: 'GET' });
    return res.data ?? [];
  },

  async createKbCategory(payload: CreateEhcKnowledgeBaseCategoryAdmin): Promise<void> {
    await apiService.request<ApiEnvelope<any>>('/ehc/admin/kb/categories', { method: 'POST', body: JSON.stringify(payload) });
  },

  async updateKbCategory(id: string, payload: CreateEhcKnowledgeBaseCategoryAdmin): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/kb/categories/${id}`, { method: 'PUT', body: JSON.stringify(payload) });
  },

  async deleteKbCategory(id: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/kb/categories/${id}`, { method: 'DELETE' });
  },

  async listKbArticles(): Promise<EhcKnowledgeBaseArticleAdmin[]> {
    const res = await apiService.request<ApiEnvelope<EhcKnowledgeBaseArticleAdmin[]>>('/ehc/admin/kb/articles', { method: 'GET' });
    return res.data ?? [];
  },

  async getKbArticle(id: string): Promise<EhcKnowledgeBaseArticleAdmin | null> {
    const res = await apiService.request<ApiEnvelope<EhcKnowledgeBaseArticleAdmin | null>>(`/ehc/admin/kb/articles/${id}`, { method: 'GET' });
    return res.data ?? null;
  },

  async createKbArticle(payload: CreateEhcKnowledgeBaseArticleAdmin): Promise<void> {
    await apiService.request<ApiEnvelope<any>>('/ehc/admin/kb/articles', { method: 'POST', body: JSON.stringify(payload) });
  },

  async updateKbArticle(id: string, payload: CreateEhcKnowledgeBaseArticleAdmin): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/kb/articles/${id}`, { method: 'PUT', body: JSON.stringify(payload) });
  },

  async deleteKbArticle(id: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/kb/articles/${id}`, { method: 'DELETE' });
  },

  async listFaqCategories(): Promise<EhcFaqCategoryAdmin[]> {
    const res = await apiService.request<ApiEnvelope<EhcFaqCategoryAdmin[]>>('/ehc/admin/faq/categories', { method: 'GET' });
    return res.data ?? [];
  },

  async createFaqCategory(payload: CreateEhcFaqCategoryAdmin): Promise<void> {
    await apiService.request<ApiEnvelope<any>>('/ehc/admin/faq/categories', { method: 'POST', body: JSON.stringify(payload) });
  },

  async updateFaqCategory(id: string, payload: CreateEhcFaqCategoryAdmin): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/faq/categories/${id}`, { method: 'PUT', body: JSON.stringify(payload) });
  },

  async deleteFaqCategory(id: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/faq/categories/${id}`, { method: 'DELETE' });
  },

  async listFaqItems(): Promise<EhcFaqItemAdmin[]> {
    const res = await apiService.request<ApiEnvelope<EhcFaqItemAdmin[]>>('/ehc/admin/faq/items', { method: 'GET' });
    return res.data ?? [];
  },

  async getFaqItem(id: string): Promise<EhcFaqItemDetailAdmin | null> {
    const res = await apiService.request<ApiEnvelope<EhcFaqItemDetailAdmin | null>>(`/ehc/admin/faq/items/${id}`, { method: 'GET' });
    return res.data ?? null;
  },

  async createFaqItem(payload: CreateEhcFaqItemAdmin): Promise<void> {
    await apiService.request<ApiEnvelope<any>>('/ehc/admin/faq/items', { method: 'POST', body: JSON.stringify(payload) });
  },

  async updateFaqItem(id: string, payload: CreateEhcFaqItemAdmin): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/faq/items/${id}`, { method: 'PUT', body: JSON.stringify(payload) });
  },

  async deleteFaqItem(id: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/faq/items/${id}`, { method: 'DELETE' });
  },

  async listTicketPriorityLevels(): Promise<EhcTicketPriorityLevelAdmin[]> {
    const res = await apiService.request<ApiEnvelope<EhcTicketPriorityLevelAdmin[]>>('/ehc/admin/priorities', { method: 'GET' });
    return res.data ?? [];
  },

  async updateTicketPriorityLevel(priority: EhcTicketPriority, payload: UpdateEhcTicketPriorityLevelAdmin): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/priorities/${priority}`, { method: 'PUT', body: JSON.stringify(payload) });
  },
};

export default ehcAdminService;
