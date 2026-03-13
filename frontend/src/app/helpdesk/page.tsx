import { redirect } from 'next/navigation';

export default function HelpdeskHomePage() {
  redirect('/helpdesk/helpdesk-complaints/internal');
}

