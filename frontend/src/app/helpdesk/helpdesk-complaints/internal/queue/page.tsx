import { redirect } from 'next/navigation';

export default function InternalHelpdeskComplaintsQueuePage() {
  redirect('/helpdesk/queue?scope=support-internal');
}
