import { redirect } from 'next/navigation';

export default function InternalEnquiryNewPage() {
  redirect('/helpdesk/tickets/new?scope=enquiry-internal');
}
