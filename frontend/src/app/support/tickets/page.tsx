import { redirect } from 'next/navigation';

export default function SupportTicketsPage() {
  // Support portal is now part of the External Portal UI.
  redirect('/external-portal/support/tickets');
}
