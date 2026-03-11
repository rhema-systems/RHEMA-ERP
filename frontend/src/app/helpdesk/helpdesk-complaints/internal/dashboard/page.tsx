import { redirect } from 'next/navigation';

export default function InternalHelpdeskComplaintsDashboardPage() {
  redirect('/helpdesk/dashboard?scope=support-internal');
}
