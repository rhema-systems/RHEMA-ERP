import { redirect } from 'next/navigation';

export default function ExternalHelpdeskComplaintsKnowledgeBasePage() {
  redirect('/helpdesk/knowledge-base?scope=support-external');
}
