import { redirect } from 'next/navigation';

export default function NewSupportServiceRequestPage() {
  // Support portal is now part of the External Portal UI.
  redirect('/external-portal/support/requests/new');
}

