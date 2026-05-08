import { redirect } from 'next/navigation';

export default function InternalEnquiryDashboardPage() {
  redirect('/helpdesk/dashboard?scope=enquiry-internal');
}
