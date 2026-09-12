export function vendorInvoiceStatusLabel(invoice: {
  status: string;
  approvalRequired?: boolean;
  journalEntryId?: string | null;
  isOpeningBalance?: boolean;
}) {
  if (invoice.status === 'Approved' && invoice.approvalRequired === false) {
    if (invoice.journalEntryId) return 'Posted';
    return invoice.isOpeningBalance ? 'Completed' : 'Ready to post';
  }
  return invoice.status;
}
