import { redirect } from 'next/navigation';

export default function InternalHelpdeskComplaintsPage() {
  redirect('/helpdesk/tickets?scope=support-internal');
}
