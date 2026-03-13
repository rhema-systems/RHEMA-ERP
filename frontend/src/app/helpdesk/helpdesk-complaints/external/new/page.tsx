import { redirect } from 'next/navigation';

export default function ExternalHelpdeskComplaintsNewPage() {
  redirect('/helpdesk/tickets/new?scope=support-external');
}
