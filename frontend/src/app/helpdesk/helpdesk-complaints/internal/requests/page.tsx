import { redirect } from 'next/navigation';

export default function InternalHelpdeskComplaintsRequestsPage() {
  redirect('/helpdesk/requests?scope=support-internal');
}
