import { redirect } from 'next/navigation';

export default function InternalHelpdeskComplaintsKnowledgeBasePage() {
  redirect('/helpdesk/knowledge-base?scope=support-internal');
}
