import type { Invoice } from '@/types/ar';

type ReceiptInvoice = Pick<
  Invoice,
  'status' | 'balanceAmount' | 'isOpeningBalance' | 'journalEntryId'
>;

const COLLECTIBLE_STATUSES = new Set<Invoice['status']>([
  'Sent',
  'Posted',
  'PartiallyPaid',
  'Overdue',
]);

/**
 * Mirrors the server's AR receipt boundary for action visibility. Opening-balance
 * invoices need explicit posting evidence; workflow review alone never makes them
 * collectible. The server remains authoritative for every write.
 */
export function canRecordArReceipt(
  invoice: ReceiptInvoice,
  canReceiveCustomerPayments: boolean
): boolean {
  return (
    canReceiveCustomerPayments &&
    COLLECTIBLE_STATUSES.has(invoice.status) &&
    Number(invoice.balanceAmount) > 0 &&
    (!invoice.isOpeningBalance || Boolean(invoice.journalEntryId))
  );
}
