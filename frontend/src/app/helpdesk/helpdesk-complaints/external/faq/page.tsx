import { redirect } from 'next/navigation';

export default function ExternalHelpdeskComplaintsFaqPage() {
  redirect('/helpdesk/faq?scope=support-external');
}
