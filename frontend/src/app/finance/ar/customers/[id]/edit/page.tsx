import { redirect } from 'next/navigation';

/**
 * Finance does not own customer master-data editing. Preserve old bookmarks while sending users
 * to the same canonical Business Partner record used by AR transactions and reporting.
 */
export default async function EditFinanceCustomerRedirect({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  redirect(`/procurement/business-partners/${id}/edit`);
}
