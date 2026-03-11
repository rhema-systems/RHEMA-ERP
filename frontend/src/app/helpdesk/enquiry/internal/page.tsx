import { redirect } from 'next/navigation';

export default function InternalEnquiryPage() {
  redirect('/helpdesk/tickets?scope=enquiry-internal');
}
