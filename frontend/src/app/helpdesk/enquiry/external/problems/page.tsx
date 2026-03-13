import { redirect } from 'next/navigation';

export default function ExternalEnquiryProblemsPage() {
  redirect('/helpdesk/problems?scope=enquiry-external');
}
