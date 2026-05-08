import { redirect } from 'next/navigation';

export default function ExternalEnquiryPage() {
  redirect('/helpdesk/tickets?scope=enquiry-external');
}
