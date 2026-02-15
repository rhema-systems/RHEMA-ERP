import { apiService } from './api.service';

export type EhcTicketType = 'Enquiry' | 'Complaint' | 'Helpdesk';
export type EhcTicketPriority = 'Low' | 'Medium' | 'High' | 'Critical';
export type EhcTicketSource = 'Web' | 'Mobile' | 'Email' | 'Internal' | 'PhoneCall' | 'Sms' | 'WhatsApp';
export type EhcTicketStatus =
  | 'New'
  | 'Acknowledged'
  | 'InProgress'
  | 'PendingUser'
  | 'PendingThirdParty'
  | 'Resolved'
  | 'Closed'
  | 'Reopened';

export interface EhcTicketListItem {
  id: string;
  ticketNumber: string;
  ticketType: EhcTicketType;
  priority: EhcTicketPriority;
  source: EhcTicketSource;
  status: EhcTicketStatus;
  subject?: string;
  categoryName?: string;
  createdAt: string;
  updatedAt?: string;
  assignedDepartmentName?: string | null;
  assignedToName?: string | null;
  requesterName?: string | null;
  requesterAuthenticationProvider?: string | null;
}

export interface CreateEhcTicketRequest {
  ticketType: EhcTicketType;
  priority: EhcTicketPriority;
  source?: EhcTicketSource;
  subject?: string;
  description: string;
  categoryId?: string;
  subcategoryId?: string;

  // Internal-only (ignored for external portal create)
  assignedDepartmentId?: string;

  relatedEntityType?: string;
  relatedEntityReference?: string;
  captchaToken?: string | null;
}

export interface EhcTicketMessage {
  id: string;
  body: string;
  isInternal: boolean;
  authorUserId?: string;
  authorName?: string;
  createdAt: string;
  attachments?: EhcTicketAttachment[];
}

export interface EhcTicketAttachment {
  id: string;
  filePath: string;
  publicUrl?: string | null;
  fileName: string;
  contentType?: string | null;
  fileSize: number;
  isInternal: boolean;
  createdAt: string;
  createdBy?: string | null;
}

export interface EhcTicketStatusHistory {
  id: string;
  fromStatus?: EhcTicketStatus | null;
  toStatus: EhcTicketStatus;
  changedByUserId?: string | null;
  changedByName?: string | null;
  notes?: string | null;
  createdAt: string;
}

export interface EhcTicketAuditEvent {
  id: string;
  eventType: string;
  title?: string | null;
  body?: string | null;
  isInternal: boolean;
  actorUserId?: string | null;
  actorName?: string | null;
  createdAt: string;
}

export interface EhcTicketDetail {
  id: string;
  ticketNumber: string;
  ticketType: EhcTicketType;
  priority: EhcTicketPriority;
  source: EhcTicketSource;
  status: EhcTicketStatus;
  subject?: string;
  description: string;
  categoryName?: string;
  subcategoryName?: string;
  createdAt: string;
  updatedAt?: string | null;
  createdBy?: string | null;
  updatedBy?: string | null;
  firstResponseDueAt?: string | null;
  resolutionDueAt?: string | null;
  firstRespondedAt?: string | null;
  resolvedAt?: string | null;
  closedAt?: string | null;
  assignedDepartmentId?: string | null;
  assignedDepartmentName?: string | null;
  assignedToUserId?: string | null;
  assignedToName?: string | null;
  relatedEntityType?: string | null;
  relatedEntityReference?: string | null;
  messages: EhcTicketMessage[];
  attachments?: EhcTicketAttachment[];
  statusHistory?: EhcTicketStatusHistory[];
  auditTrail?: EhcTicketAuditEvent[];
}

type ApiEnvelope<T> = { success: boolean; data: T; message?: string };

export interface EhcTicketCategoryTree {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  appliesToType?: EhcTicketType | null;
  subcategories: EhcTicketCategoryTree[];
}

export interface EhcMyTicketsFilters {
  q?: string | null;
  status?: EhcTicketStatus | null;
  ticketType?: EhcTicketType | null;
  priority?: EhcTicketPriority | null;
  source?: EhcTicketSource | null;
  categoryId?: string | null;
  createdFrom?: string | null; // yyyy-mm-dd
  createdTo?: string | null; // yyyy-mm-dd
}

export const ehcTicketService = {
  async listCategories(): Promise<EhcTicketCategoryTree[]> {
    const res = await apiService.request<ApiEnvelope<EhcTicketCategoryTree[]>>('/ehc/external/metadata/categories', {
      method: 'GET',
    });
    return res.data ?? [];
  },

  async listMyTickets(page = 1, pageSize = 25, filters?: EhcMyTicketsFilters): Promise<EhcTicketListItem[]> {
    const qs = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (filters?.q) qs.set('q', filters.q);
    if (filters?.status) qs.set('status', filters.status);
    if (filters?.ticketType) qs.set('ticketType', filters.ticketType);
    if (filters?.priority) qs.set('priority', filters.priority);
    if (filters?.source) qs.set('source', filters.source);
    if (filters?.categoryId) qs.set('categoryId', filters.categoryId);
    if (filters?.createdFrom) qs.set('createdFrom', filters.createdFrom);
    if (filters?.createdTo) qs.set('createdTo', filters.createdTo);

    const res = await apiService.request<ApiEnvelope<EhcTicketListItem[]>>(`/ehc/external/tickets?${qs.toString()}`, { method: 'GET' });
    return res.data ?? [];
  },

  async getMyTicket(id: string): Promise<EhcTicketDetail> {
    const res = await apiService.request<ApiEnvelope<EhcTicketDetail>>(`/ehc/external/tickets/${id}`, {
      method: 'GET',
    });
    return res.data;
  },

  async createTicket(request: CreateEhcTicketRequest) {
    const res = await apiService.request<ApiEnvelope<any>>('/ehc/external/tickets', {
      method: 'POST',
      body: JSON.stringify(request),
    });
    return res.data;
  },

  async addMessage(ticketId: string, body: string) {
    const res = await apiService.request<ApiEnvelope<EhcTicketMessage>>(`/ehc/external/tickets/${ticketId}/messages`, {
      method: 'POST',
      body: JSON.stringify({ body }),
    });
    return res.data;
  },

  async addAttachment(
    ticketId: string,
    attachment: { filePath: string; fileName: string; contentType?: string | null; fileSize: number; messageId?: string | null }
  ) {
    const res = await apiService.request<ApiEnvelope<EhcTicketAttachment>>(`/ehc/external/tickets/${ticketId}/attachments`, {
      method: 'POST',
      body: JSON.stringify({
        messageId: attachment.messageId ?? null,
        filePath: attachment.filePath,
        fileName: attachment.fileName,
        contentType: attachment.contentType ?? null,
        fileSize: attachment.fileSize,
      }),
    });
    return res.data;
  },

  async validateRelatedEntity(entityType: string, reference: string): Promise<{ exists: boolean; entityType?: string; reference?: string }> {
    const qs = new URLSearchParams({ entityType, reference });
    const res = await apiService.request<ApiEnvelope<{ exists: boolean; entityType?: string; reference?: string }>>(
      `/ehc/external/related-entities/validate?${qs.toString()}`,
      { method: 'GET' }
    );
    return res.data as any;
  },
};

export default ehcTicketService;
