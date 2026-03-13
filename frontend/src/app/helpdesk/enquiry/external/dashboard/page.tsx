import { redirect } from 'next/navigation';

export default function ExternalEnquiryDashboardPage() {
  redirect('/helpdesk/dashboard?scope=enquiry-external');
}
