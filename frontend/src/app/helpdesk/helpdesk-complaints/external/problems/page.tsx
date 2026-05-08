import { redirect } from 'next/navigation';

export default function ExternalHelpdeskComplaintsProblemsPage() {
  redirect('/helpdesk/problems?scope=support-external');
}
