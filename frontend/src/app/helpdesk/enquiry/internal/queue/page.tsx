import { redirect } from 'next/navigation';

export default function InternalEnquiryQueuePage() {
  redirect('/helpdesk/queue?scope=enquiry-internal');
}
