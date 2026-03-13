import { redirect } from 'next/navigation';

export default function InternalHelpdeskComplaintsRequestsNewPage() {
  redirect('/helpdesk/requests/new?scope=support-internal');
}
