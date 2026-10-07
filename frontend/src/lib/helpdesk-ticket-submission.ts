import type { EhcTicketListItem } from '@/services/ehcTicketService';

type TicketSubmission = Pick<
  EhcTicketListItem,
  'isPublicSiteSubmission' | 'requesterAuthenticationProvider'
>;

export function getTicketSubmissionLabel(ticket: TicketSubmission): string | null {
  if (ticket.isPublicSiteSubmission) return 'Public Site';

  const provider = ticket.requesterAuthenticationProvider;
  if (provider === 'Local') return 'External Portal';
  return provider ? 'Internal ERP' : null;
}
