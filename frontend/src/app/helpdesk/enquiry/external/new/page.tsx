import { redirect } from 'next/navigation';

export default function ExternalEnquiryNewPage() {
  redirect('/helpdesk/tickets/new?scope=enquiry-external');
}
