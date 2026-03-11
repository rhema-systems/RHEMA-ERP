import { redirect } from 'next/navigation';

export default function ExternalEnquiryKnowledgeBasePage() {
  redirect('/helpdesk/knowledge-base?scope=enquiry-external');
}
