import { redirect } from 'next/navigation';

export default async function SupportTicketDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  // Support portal is now part of the External Portal UI.
  redirect(`/external-portal/support/tickets/${id}`);
}
