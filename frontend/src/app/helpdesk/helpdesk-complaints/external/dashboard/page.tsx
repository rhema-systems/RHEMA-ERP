import { redirect } from 'next/navigation';

export default function ExternalHelpdeskComplaintsDashboardPage() {
  redirect('/helpdesk/dashboard?scope=support-external');
}
