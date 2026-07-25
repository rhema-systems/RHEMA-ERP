import { redirect } from 'next/navigation';

export default function LegacyCurrenciesPage() {
  redirect('/administration/finance/currencies');
}
