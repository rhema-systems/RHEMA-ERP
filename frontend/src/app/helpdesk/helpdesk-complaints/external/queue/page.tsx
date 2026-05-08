import { redirect } from 'next/navigation';

export default function ExternalHelpdeskComplaintsQueuePage() {
  redirect('/helpdesk/queue?scope=support-external');
}
