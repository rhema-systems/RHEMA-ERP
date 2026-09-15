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
import type { EhcProblemDetail, EhcProblemStatus } from './ehcProblemService';

type ApiEnvelope<T> = { success: boolean; data: T; message?: string };

export interface EhcLookupItem {
  id: string;
  name: string;
}

export interface EhcLookupUser {
  id: string;
  name: string;
  email?: string | null;
}

export interface EhcProblemLookupItem {
  id: string;
  problemNumber: string;
  title: string;
  status: EhcProblemStatus;
  priority: EhcTicketPriority;
  createdAt: string;
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

export interface EhcFeedbackSummary {
  feedbackCount: number;
  avgRating?: number | null;
  resolvedOrClosedTickets: number;
  responseRatePercent?: number | null;
  ratingDistribution: Record<number, number>;
}

export interface EhcFeedbackByAgentRow {
  agentUserId?: string | null;
  agentName: string;
  feedbackCount: number;
  avgRating?: number | null;
}

export interface EhcFeedbackByDepartmentRow {
  departmentId?: string | null;
  departmentName: string;
  feedbackCount: number;
  avgRating?: number | null;
}

export interface EhcFeedbackTrendPoint {
  date: string; // yyyy-mm-dd
  feedbackCount: number;
  avgRating?: number | null;
}

export interface EhcProblemStatusCount {
  status: 'Open' | 'InProgress' | 'Resolved' | 'Closed';
  count: number;
}

export interface EhcProblemPriorityCount {
  priority: EhcTicketPriority;
  count: number;
}

export interface EhcProblemDepartmentCount {
  departmentId?: string | null;
  departmentName: string;
  count: number;
}

export interface EhcTopRecurringProblem {
  problemId: string;
  problemNumber: string;
  title: string;
  status: 'Open' | 'InProgress' | 'Resolved' | 'Closed';
  priority: EhcTicketPriority;
  departmentName: string;
  ownerName: string;
  linkedTicketsCount: number;
  createdAt: string;
}

export interface EhcProblemsSummary {
  totalProblems: number;
  openProblems: number;
  problemsCreatedLastDays: number;
  linkedTicketsLastDays: number;
  byStatus: EhcProblemStatusCount[];
  byPriority: EhcProblemPriorityCount[];
  byDepartment: EhcProblemDepartmentCount[];
  topRecurring: EhcTopRecurringProblem[];
}

export interface EhcProblemLinkTrendPoint {
  date: string; // yyyy-mm-dd
  linkedTickets: number;
  distinctProblems: number;
}

export type EhcTicketLinkType = 'Related' | 'ParentOf' | 'DuplicateOf';

export interface EhcTicketLookupTicket {
  id: string;
  ticketNumber: string;
  subject?: string | null;
  status: EhcTicketStatus;
  priority: EhcTicketPriority;
  createdAt: string;
}

export interface EhcTicketLink {
  id: string;
  linkType: EhcTicketLinkType;
  relationshipLabel: string;
  linkedTicketId: string;
  linkedTicketNumber: string;
  linkedSubject?: string | null;
  linkedStatus: EhcTicketStatus;
  linkedPriority: EhcTicketPriority;
  linkedCreatedAt: string;
  createdAt: string;
  createdBy?: string | null;
}

export interface EhcCloseDuplicateTicketsRequest {
  targetStatus?: EhcTicketStatus;
  notes?: string | null;
}

export interface EhcCloseDuplicateTicketFailure {
  ticketId: string;
  ticketNumber?: string | null;
  reason: string;
}

export interface EhcCloseDuplicateTicketsResult {
  totalDuplicates: number;
  closedCount: number;
  skippedCount: number;
  failed: EhcCloseDuplicateTicketFailure[];
}

export interface EhcKbCategory {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface EhcKbArticleListItem {
  id: string;
  code: string;
  title: string;
  summary?: string | null;
  categoryId?: string | null;
  categoryName?: string | null;
  tagsCsv?: string | null;
  isPublished: boolean;
  isInternalOnly: boolean;
  viewCount: number;
  createdAt: string;
}

export interface EhcKbArticleDetail extends EhcKbArticleListItem {
  body: string;
}

export interface EhcFaqCategory {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface EhcFaqItem {
  id: string;
  question: string;
  answer: string;
  categoryId?: string | null;
  categoryName?: string | null;
  isPublished: boolean;
  isInternalOnly: boolean;
  sortOrder: number;
  viewCount: number;
  createdAt: string;
}

export interface EhcTicketPriorityLevel {
  priority: EhcTicketPriority;
  displayName: string;
  description?: string | null;
  isActive: boolean;
  sortOrder: number;
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

export interface EhcCannedResponse {
  id: string;
  code: string;
  title: string;
  body: string;
  isActive: boolean;
  appliesToType?: EhcTicketType | null;
  categoryId?: string | null;
}

export interface UpdateEhcTicketRcaRequest {
  rootCauseId?: string | null;
  rootCauseDetails?: string | null;
  resolutionSummary?: string | null;
}

export interface EhcAgentReplyProfile {
  signature?: string | null;
  isSignatureEnabled: boolean;
  appendSignatureToReplies: boolean;
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

  async assignTicket(ticketId: string, assignedToUserId: string, assignedOrganizationUnitId?: string | null): Promise<void> {
    const qs = new URLSearchParams({ assignedToUserId });
    if (assignedOrganizationUnitId) qs.set('assignedOrganizationUnitId', assignedOrganizationUnitId);
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

  async convertTicketToProblem(ticketId: string, payload?: { title?: string | null; description?: string | null; priority?: EhcTicketPriority | null; departmentId?: string | null; ownerUserId?: string | null }): Promise<EhcProblemDetail> {
    const res = await apiService.request<ApiEnvelope<EhcProblemDetail>>(`/ehc/internal/tickets/${ticketId}/convert-to-problem`, {
      method: 'POST',
      body: JSON.stringify(payload ?? {}),
    });
    return res.data;
  },

  async listTicketProblems(ticketId: string): Promise<EhcProblemLookupItem[]> {
    const res = await apiService.request<ApiEnvelope<EhcProblemLookupItem[]>>(`/ehc/internal/tickets/${ticketId}/problems`, { method: 'GET' });
    return res.data ?? [];
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

  async listOrganizationUnits(): Promise<EhcLookupItem[]> {
    const res = await apiService.request<ApiEnvelope<EhcLookupItem[]>>('/ehc/internal/lookups/organization-units', { method: 'GET' });
    return res.data ?? [];
  },

  async searchUsers(q: string, limit = 20): Promise<EhcLookupUser[]> {
    const qs = new URLSearchParams({ q, limit: String(limit) });
    const res = await apiService.request<ApiEnvelope<EhcLookupUser[]>>(`/ehc/internal/lookups/users?${qs.toString()}`, { method: 'GET' });
    return res.data ?? [];
  },

  async searchTickets(q: string, limit = 20): Promise<EhcTicketLookupTicket[]> {
    const qs = new URLSearchParams({ q, limit: String(limit) });
    const res = await apiService.request<ApiEnvelope<EhcTicketLookupTicket[]>>(`/ehc/internal/lookups/tickets?${qs.toString()}`, { method: 'GET' });
    return (res.data ?? []) as any;
  },

  async listWatchers(ticketId: string): Promise<EhcLookupUser[]> {
    const res = await apiService.request<ApiEnvelope<EhcLookupUser[]>>(`/ehc/internal/tickets/${ticketId}/watchers`, { method: 'GET' });
    return res.data ?? [];
  },

  async addWatcher(ticketId: string, userId: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/internal/tickets/${ticketId}/watchers`, {
      method: 'POST',
      body: JSON.stringify({ userId }),
    });
  },

  async removeWatcher(ticketId: string, userId: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/internal/tickets/${ticketId}/watchers/${userId}`, { method: 'DELETE' });
  },

  async getIsWatching(ticketId: string): Promise<boolean> {
    const res = await apiService.request<ApiEnvelope<{ isWatching: boolean }>>(`/ehc/internal/tickets/${ticketId}/watchers/me`, { method: 'GET' });
    return Boolean(res.data?.isWatching);
  },

  async watchMe(ticketId: string): Promise<boolean> {
    const res = await apiService.request<ApiEnvelope<{ isWatching: boolean }>>(`/ehc/internal/tickets/${ticketId}/watchers/me`, { method: 'POST' });
    return Boolean(res.data?.isWatching);
  },

  async unwatchMe(ticketId: string): Promise<boolean> {
    const res = await apiService.request<ApiEnvelope<{ isWatching: boolean }>>(`/ehc/internal/tickets/${ticketId}/watchers/me`, { method: 'DELETE' });
    return Boolean(res.data?.isWatching);
  },

  async listTicketLinks(ticketId: string): Promise<EhcTicketLink[]> {
    const res = await apiService.request<ApiEnvelope<EhcTicketLink[]>>(`/ehc/internal/tickets/${ticketId}/links`, { method: 'GET' });
    return (res.data ?? []) as any;
  },

  async createTicketLink(
    ticketId: string,
    payload: { relatedTicketId: string; linkType: EhcTicketLinkType; notes?: string | null; reverseDirection?: boolean }
  ): Promise<EhcTicketLink> {
    const res = await apiService.request<ApiEnvelope<EhcTicketLink>>(`/ehc/internal/tickets/${ticketId}/links`, {
      method: 'POST',
      body: JSON.stringify({
        relatedTicketId: payload.relatedTicketId,
        linkType: payload.linkType,
        notes: payload.notes ?? null,
        reverseDirection: Boolean(payload.reverseDirection),
      }),
    });
    return res.data as any;
  },

  async deleteTicketLink(ticketId: string, linkId: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/internal/tickets/${ticketId}/links/${linkId}`, { method: 'DELETE' });
  },

  async closeDuplicateTickets(ticketId: string, payload?: EhcCloseDuplicateTicketsRequest): Promise<EhcCloseDuplicateTicketsResult> {
    const res = await apiService.request<ApiEnvelope<EhcCloseDuplicateTicketsResult>>(`/ehc/internal/tickets/${ticketId}/links/close-duplicates`, {
      method: 'POST',
      body: JSON.stringify({
        targetStatus: payload?.targetStatus ?? 'Closed',
        notes: payload?.notes ?? null,
      }),
    });
    return res.data as any;
  },

  async getMyReplyProfile(): Promise<EhcAgentReplyProfile> {
    const res = await apiService.request<ApiEnvelope<EhcAgentReplyProfile>>('/ehc/internal/agent-profile/reply', { method: 'GET' });
    return res.data as any;
  },

  async updateMyReplyProfile(request: Partial<EhcAgentReplyProfile>): Promise<EhcAgentReplyProfile> {
    const res = await apiService.request<ApiEnvelope<EhcAgentReplyProfile>>('/ehc/internal/agent-profile/reply', {
      method: 'PUT',
      body: JSON.stringify({
        signature: request.signature ?? null,
        isSignatureEnabled: request.isSignatureEnabled ?? true,
        appendSignatureToReplies: request.appendSignatureToReplies ?? true,
      }),
    });
    return res.data as any;
  },

  async getMyDepartment(): Promise<EhcLookupItem | null> {
    const res = await apiService.request<ApiEnvelope<EhcLookupItem | null>>('/ehc/internal/lookups/my-department', { method: 'GET' });
    return res.data ?? null;
  },

  async getMyOrganizationUnit(): Promise<EhcLookupItem | null> {
    const res = await apiService.request<ApiEnvelope<EhcLookupItem | null>>('/ehc/internal/lookups/my-organization-unit', { method: 'GET' });
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

  async getFeedbackSummary(days = 30): Promise<EhcFeedbackSummary> {
    const qs = new URLSearchParams({ days: String(days) });
    const res = await apiService.request<ApiEnvelope<EhcFeedbackSummary>>(`/ehc/internal/reports/feedback/summary?${qs.toString()}`, { method: 'GET' });
    return res.data as any;
  },

  async getFeedbackByAgent(days = 30): Promise<EhcFeedbackByAgentRow[]> {
    const qs = new URLSearchParams({ days: String(days) });
    const res = await apiService.request<ApiEnvelope<EhcFeedbackByAgentRow[]>>(`/ehc/internal/reports/feedback/by-agent?${qs.toString()}`, { method: 'GET' });
    return (res.data ?? []) as any;
  },

  async getFeedbackByDepartment(days = 30): Promise<EhcFeedbackByDepartmentRow[]> {
    const qs = new URLSearchParams({ days: String(days) });
    const res = await apiService.request<ApiEnvelope<EhcFeedbackByDepartmentRow[]>>(`/ehc/internal/reports/feedback/by-department?${qs.toString()}`, { method: 'GET' });
    return (res.data ?? []) as any;
  },

  async getFeedbackTrend(days = 30): Promise<EhcFeedbackTrendPoint[]> {
    const qs = new URLSearchParams({ days: String(days) });
    const res = await apiService.request<ApiEnvelope<EhcFeedbackTrendPoint[]>>(`/ehc/internal/reports/feedback/trend?${qs.toString()}`, { method: 'GET' });
    return (res.data ?? []) as any;
  },

  async getProblemsSummary(days = 30, top = 15): Promise<EhcProblemsSummary> {
    const qs = new URLSearchParams({ days: String(days), top: String(top) });
    const res = await apiService.request<ApiEnvelope<EhcProblemsSummary>>(`/ehc/internal/reports/problems/summary?${qs.toString()}`, { method: 'GET' });
    return res.data as any;
  },

  async getProblemLinkTrend(days = 30): Promise<EhcProblemLinkTrendPoint[]> {
    const qs = new URLSearchParams({ days: String(days) });
    const res = await apiService.request<ApiEnvelope<EhcProblemLinkTrendPoint[]>>(`/ehc/internal/reports/problems/link-trend?${qs.toString()}`, { method: 'GET' });
    return (res.data ?? []) as any;
  },

  async kbListCategories(): Promise<EhcKbCategory[]> {
    const res = await apiService.request<ApiEnvelope<EhcKbCategory[]>>('/ehc/internal/kb/categories', { method: 'GET' });
    return (res.data ?? []) as any;
  },

  async kbSearchArticles(q: string, categoryId?: string | null, limit = 20): Promise<EhcKbArticleListItem[]> {
    const qs = new URLSearchParams({ q, limit: String(limit) });
    if (categoryId) qs.set('categoryId', categoryId);
    const res = await apiService.request<ApiEnvelope<EhcKbArticleListItem[]>>(`/ehc/internal/kb/articles/search?${qs.toString()}`, { method: 'GET' });
    return (res.data ?? []) as any;
  },

  async kbGetArticle(id: string): Promise<EhcKbArticleDetail | null> {
    const res = await apiService.request<ApiEnvelope<EhcKbArticleDetail | null>>(`/ehc/internal/kb/articles/${id}`, { method: 'GET' });
    return res.data ?? null;
  },

  async kbTrackView(id: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/internal/kb/articles/${id}/view`, { method: 'POST' });
  },

  async faqListCategories(): Promise<EhcFaqCategory[]> {
    const res = await apiService.request<ApiEnvelope<EhcFaqCategory[]>>('/ehc/internal/faq/categories', { method: 'GET' });
    return (res.data ?? []) as any;
  },

  async faqSearch(q: string, categoryId?: string | null, limit = 100): Promise<EhcFaqItem[]> {
    const qs = new URLSearchParams({ q, limit: String(limit) });
    if (categoryId) qs.set('categoryId', categoryId);
    const res = await apiService.request<ApiEnvelope<EhcFaqItem[]>>(`/ehc/internal/faq/items/search?${qs.toString()}`, { method: 'GET' });
    return (res.data ?? []) as any;
  },

  async faqGetItem(id: string): Promise<EhcFaqItem | null> {
    const res = await apiService.request<ApiEnvelope<EhcFaqItem | null>>(`/ehc/internal/faq/items/${id}`, { method: 'GET' });
    return res.data ?? null;
  },

  async faqTrackView(id: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/internal/faq/items/${id}/view`, { method: 'POST' });
  },

  async listPriorityLevels(): Promise<EhcTicketPriorityLevel[]> {
    const res = await apiService.request<ApiEnvelope<EhcTicketPriorityLevel[]>>('/ehc/internal/lookups/priorities', { method: 'GET' });
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

  async listCannedResponses(ticketType?: EhcTicketType | null, categoryId?: string | null): Promise<EhcCannedResponse[]> {
    const qs = new URLSearchParams();
    if (ticketType) qs.set('ticketType', ticketType);
    if (categoryId) qs.set('categoryId', categoryId);
    const suffix = qs.toString() ? `?${qs.toString()}` : '';
    const res = await apiService.request<ApiEnvelope<EhcCannedResponse[]>>(`/ehc/internal/lookups/canned-responses${suffix}`, { method: 'GET' });
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
