import { redirect } from 'next/navigation';

export default function LegacySalesCrmQuotesPage() {
  // Keep this legacy Sales route as a redirect; the current CRM quotes workflow lives under /crm/quotes.
  redirect('/crm/quotes');
}
