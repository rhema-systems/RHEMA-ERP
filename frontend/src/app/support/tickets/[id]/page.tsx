import { redirect } from 'next/navigation';

export default function SupportTicketDetailPage({ params }: { params: { id: string } }) {
  // Support portal is now part of the External Portal UI.
  redirect(`/external-portal/support/tickets/${params.id}`);
}
