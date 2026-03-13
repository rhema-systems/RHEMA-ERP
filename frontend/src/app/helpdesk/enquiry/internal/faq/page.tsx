import { redirect } from 'next/navigation';

export default function InternalEnquiryFaqPage() {
  redirect('/helpdesk/faq?scope=enquiry-internal');
}
