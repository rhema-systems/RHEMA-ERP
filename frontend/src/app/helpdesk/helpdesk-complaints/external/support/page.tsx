import { redirect } from 'next/navigation';

export default function ExternalHelpdeskComplaintsSupportPage() {
  redirect('/helpdesk/support?scope=support-external');
}
