import { redirect } from 'next/navigation';

export default function ExternalEnquiryQueuePage() {
  redirect('/helpdesk/queue?scope=enquiry-external');
}
