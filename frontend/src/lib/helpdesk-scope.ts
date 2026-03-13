import type { EhcTicketListItem, EhcTicketSource, EhcTicketType } from '@/services/ehcTicketService';

export type HelpdeskScope =
  | 'enquiry-internal'
  | 'enquiry-external'
  | 'support-internal'
  | 'support-external';

export interface HelpdeskScopeConfig {
  scope: HelpdeskScope;
  moduleLabel: string;
  listTitle: string;
  listDescription: string;
  queueTitle: string;
  queueDescription: string;
  newTitle: string;
  newDescription: string;
  createButtonLabel: string;
  ticketTypeLabel: string;
  allowedTicketTypes: EhcTicketType[];
  defaultTicketType: EhcTicketType;
  internalOnly: boolean;
  requiredPermission: string;
}

const HELP_DESK_SCOPE_CONFIGS: Record<HelpdeskScope, HelpdeskScopeConfig> = {
  'enquiry-internal': {
    scope: 'enquiry-internal',
    moduleLabel: 'Enquiry',
    listTitle: 'Internal Enquiries',
    listDescription: 'Review, assign, and respond to enquiries raised from internal ERP channels.',
    queueTitle: 'Internal Enquiry Queue',
    queueDescription: 'Unassigned internal enquiries waiting for triage.',
    newTitle: 'Create Internal Enquiry',
    newDescription: 'Log an internal enquiry for routing and SLA tracking.',
    createButtonLabel: 'Create Enquiry',
    ticketTypeLabel: 'Enquiry',
    allowedTicketTypes: ['Enquiry'],
    defaultTicketType: 'Enquiry',
    internalOnly: true,
    requiredPermission: 'enquiry.internal.access',
  },
  'enquiry-external': {
    scope: 'enquiry-external',
    moduleLabel: 'Enquiry',
    listTitle: 'External Enquiries',
    listDescription: 'Manage enquiries submitted from website, email, mobile, and other external channels.',
    queueTitle: 'External Enquiry Queue',
    queueDescription: 'Unassigned external enquiries waiting for triage.',
    newTitle: 'Create External Enquiry',
    newDescription: 'Register an externally-originated enquiry inside the backoffice workspace.',
    createButtonLabel: 'Create Enquiry',
    ticketTypeLabel: 'Enquiry',
    allowedTicketTypes: ['Enquiry'],
    defaultTicketType: 'Enquiry',
    internalOnly: false,
    requiredPermission: 'enquiry.external.access',
  },
  'support-internal': {
    scope: 'support-internal',
    moduleLabel: 'Helpdesk & Complaints',
    listTitle: 'Internal Helpdesk & Complaints',
    listDescription: 'Work internal helpdesk tickets and complaints raised from internal ERP channels.',
    queueTitle: 'Internal Helpdesk & Complaints Queue',
    queueDescription: 'Unassigned internal helpdesk tickets and complaints waiting for triage.',
    newTitle: 'Create Internal Helpdesk / Complaint Ticket',
    newDescription: 'Log an internal helpdesk ticket or complaint for routing and SLA tracking.',
    createButtonLabel: 'Create Ticket',
    ticketTypeLabel: 'Helpdesk / Complaint',
    allowedTicketTypes: ['Helpdesk', 'Complaint'],
    defaultTicketType: 'Helpdesk',
    internalOnly: true,
    requiredPermission: 'support.internal.access',
  },
  'support-external': {
    scope: 'support-external',
    moduleLabel: 'Helpdesk & Complaints',
    listTitle: 'External Helpdesk & Complaints',
    listDescription: 'Work helpdesk tickets and complaints submitted from customers and external channels.',
    queueTitle: 'External Helpdesk & Complaints Queue',
    queueDescription: 'Unassigned external helpdesk tickets and complaints waiting for triage.',
    newTitle: 'Create External Helpdesk / Complaint Ticket',
    newDescription: 'Register an externally-originated helpdesk ticket or complaint inside the backoffice workspace.',
    createButtonLabel: 'Create Ticket',
    ticketTypeLabel: 'Helpdesk / Complaint',
    allowedTicketTypes: ['Helpdesk', 'Complaint'],
    defaultTicketType: 'Helpdesk',
    internalOnly: false,
    requiredPermission: 'support.external.access',
  },
};

export const DEFAULT_HELPDESK_SCOPE: HelpdeskScope = 'support-internal';

export const EXTERNAL_TICKET_SOURCES: EhcTicketSource[] = ['Web', 'Mobile', 'Email', 'PhoneCall', 'Sms', 'WhatsApp'];

export const isExternalTicketSource = (source: EhcTicketSource | '' | null | undefined) =>
  Boolean(source) && source !== 'Internal';

export const normalizeHelpdeskScope = (value: string | null | undefined): HelpdeskScope =>
  value && value in HELP_DESK_SCOPE_CONFIGS ? (value as HelpdeskScope) : DEFAULT_HELPDESK_SCOPE;

export const getHelpdeskScopeConfig = (scope: string | null | undefined): HelpdeskScopeConfig =>
  HELP_DESK_SCOPE_CONFIGS[normalizeHelpdeskScope(scope)];

export const inferHelpdeskScopeFromTicket = (
  ticketType: EhcTicketType | null | undefined,
  source: EhcTicketSource | null | undefined,
): HelpdeskScope => {
  const isEnquiry = ticketType === 'Enquiry';
  const isInternal = source === 'Internal';
  if (isEnquiry) return isInternal ? 'enquiry-internal' : 'enquiry-external';
  return isInternal ? 'support-internal' : 'support-external';
};

export const buildScopedHelpdeskTicketsPath = (scope: HelpdeskScope) => `/helpdesk/tickets?scope=${scope}`;
export const buildScopedHelpdeskQueuePath = (scope: HelpdeskScope) => `/helpdesk/queue?scope=${scope}`;
export const buildScopedHelpdeskNewPath = (scope: HelpdeskScope) => `/helpdesk/tickets/new?scope=${scope}`;
export const buildScopedHelpdeskDetailPath = (scope: HelpdeskScope, ticketId: string) =>
  `/helpdesk/tickets/${ticketId}?scope=${scope}`;

export const getRequiredPermissionForHelpdeskScope = (scope: string | null | undefined) =>
  getHelpdeskScopeConfig(scope).requiredPermission;

export const isTicketInHelpdeskScope = (
  ticket: Pick<EhcTicketListItem, 'ticketType' | 'source'>,
  scope: HelpdeskScope,
) => {
  const config = getHelpdeskScopeConfig(scope);
  const ticketTypeMatches = config.allowedTicketTypes.includes(ticket.ticketType);
  const sourceMatches = config.internalOnly ? ticket.source === 'Internal' : isExternalTicketSource(ticket.source);
  return ticketTypeMatches && sourceMatches;
};
