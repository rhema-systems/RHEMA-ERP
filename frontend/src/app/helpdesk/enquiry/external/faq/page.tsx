import { redirect } from 'next/navigation';

export default function ExternalEnquiryFaqPage() {
  redirect('/helpdesk/faq?scope=enquiry-external');
}
