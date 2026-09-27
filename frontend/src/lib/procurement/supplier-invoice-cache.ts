import type { QueryClient } from '@tanstack/react-query';
import type { VendorInvoice } from '@/types/ap';

export async function cacheSavedSupplierInvoice(
  queryClient: QueryClient,
  invoice: VendorInvoice
) {
  const invoiceKey = ['vendor-invoice', invoice.id];
  // A pre-save fetch must not replace the server's newly saved version.
  await queryClient.cancelQueries({ queryKey: invoiceKey, exact: true });
  queryClient.setQueryData(invoiceKey, invoice);

  // Refresh dependent data when next viewed, without delaying navigation.
  for (const queryKey of [
    ['vendor-invoices'],
    ['vendor-invoice-three-way-match', invoice.id],
    ['vendor-invoice-match-exceptions', invoice.id],
    ['vendor-invoice-workflow-summary', invoice.id],
  ]) {
    void queryClient.invalidateQueries({ queryKey, refetchType: 'none' });
  }
}
