import { redirect } from 'next/navigation';

export default function ExternalHelpdeskComplaintsPage() {
  redirect('/helpdesk/tickets?scope=support-external');
}
