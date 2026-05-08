import { redirect } from 'next/navigation';

export default function InternalEnquiryProblemsPage() {
  redirect('/helpdesk/problems?scope=enquiry-internal');
}
