import { redirect } from 'next/navigation';

export default function InternalHelpdeskComplaintsProblemsPage() {
  redirect('/helpdesk/problems?scope=support-internal');
}
