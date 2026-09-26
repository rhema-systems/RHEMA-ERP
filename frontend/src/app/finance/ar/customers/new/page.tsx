import { redirect } from 'next/navigation';

/**
 * Customer identity is owned by the canonical Business Partner register. Finance keeps this
 * legacy route only as a durable bookmark redirect; it must not create a second customer master.
 */
export default function NewFinanceCustomerRedirect() {
  redirect('/procurement/business-partners/new');
}
