import { redirect } from 'next/navigation';

export default function InternalHelpdeskComplaintsFaqPage() {
  redirect('/helpdesk/faq?scope=support-internal');
}
