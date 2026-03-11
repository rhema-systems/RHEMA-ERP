import { redirect } from 'next/navigation';

export default function InternalHelpdeskComplaintsNewPage() {
  redirect('/helpdesk/tickets/new?scope=support-internal');
}
