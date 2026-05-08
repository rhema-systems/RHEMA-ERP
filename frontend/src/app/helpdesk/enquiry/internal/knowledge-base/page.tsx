import { redirect } from 'next/navigation';

export default function InternalEnquiryKnowledgeBasePage() {
  redirect('/helpdesk/knowledge-base?scope=enquiry-internal');
}
