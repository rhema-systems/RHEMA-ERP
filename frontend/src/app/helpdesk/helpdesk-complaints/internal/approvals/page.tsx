import { redirect } from 'next/navigation';

export default function InternalHelpdeskComplaintsApprovalsPage() {
  redirect('/helpdesk/approvals?scope=support-internal');
}
