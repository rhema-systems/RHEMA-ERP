import { describe, expect, it } from 'vitest';
import { vendorInvoiceStatusLabel } from './vendor-invoice-status';

describe('vendor invoice direct-completion labels', () => {
  it.each([
    [{ status: 'Approved', approvalRequired: false, journalEntryId: 'journal' }, 'Posted'],
    [{ status: 'Approved', approvalRequired: false }, 'Ready to post'],
    [{ status: 'Approved', approvalRequired: false, isOpeningBalance: true }, 'Completed'],
    [{ status: 'Approved', approvalRequired: true }, 'Approved'],
    [{ status: 'Approved' }, 'Approved'],
    [{ status: 'Paid', approvalRequired: false, journalEntryId: 'journal' }, 'Paid'],
    [{ status: 'PartiallyPaid', approvalRequired: false }, 'PartiallyPaid'],
    [{ status: 'Voided', approvalRequired: false }, 'Voided'],
  ])('preserves business status and does not invent human approval: %j', (invoice, label) => {
    expect(vendorInvoiceStatusLabel(invoice)).toBe(label);
  });
});
