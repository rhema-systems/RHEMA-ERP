'use client';

import React from 'react';
import { Label } from '@/components/ui/label';
import { PostingAccountPicker } from '@/components/finance/PostingAccountPicker';
import type { Account } from '@/types/finance';
import type { BusinessPartnerReceivablesDefaults } from '@/services/businessPartnerService';

const fields = [
  ['defaultArAccountId', 'Accounts Receivable control account', ['Asset'], true],
  ['salesAccountId', 'Sales', ['Revenue'], false],
  ['costOfSalesAccountId', 'Cost of Sales', ['Expense'], false],
  ['inventoryAccountId', 'Inventory', ['Asset'], true],
  ['termsDiscountsTakenAccountId', 'Terms Discounts Taken', ['Revenue', 'Expense'], false],
  ['financeChargesAccountId', 'Finance Charges', ['Revenue'], false],
  ['writeoffAccountId', 'Writeoffs', ['Expense'], false],
  ['overpaymentWriteoffAccountId', 'Overpayment Writeoffs', ['Revenue'], false],
  ['salesReturnsAccountId', 'Sales Order Returns', ['Revenue', 'Expense'], false],
] as const;

export function BusinessPartnerReceivablesFields({ value, onChange, accounts, disabled = false }: {
  value?: BusinessPartnerReceivablesDefaults;
  onChange: (value: BusinessPartnerReceivablesDefaults) => void;
  accounts: Account[];
  disabled?: boolean;
}) {
  return <div className="space-y-4">
    <p className="text-sm text-muted-foreground">Customer accounts apply to sales invoices, receipts, returns and customer balance adjustments. Finance Charges, Writeoffs and Overpayment Writeoffs are used by the corresponding AR adjustment purposes. Overpayment writeoffs apply to AR credit balances; customer advances are settled separately. Explicit transaction accounts take precedence. Supplier accounts are configured separately.</p>
    <div className="overflow-x-auto rounded-lg border"><table className="w-full min-w-[480px] text-sm">
      <thead className="bg-muted/50"><tr><th className="w-56 px-3 py-2 text-left font-medium">Posting Type</th><th className="px-3 py-2 text-left font-medium">Account / Description</th></tr></thead>
      <tbody>{fields.map(([key, label, types, allowControl]) => <tr key={key} className="border-t">
        <td className="px-3 py-1.5"><Label htmlFor={key} className="font-normal">{label}</Label></td>
        <td className="px-3 py-1.5"><PostingAccountPicker id={key} value={value?.[key]} onChange={accountId => onChange({ ...value, [key]: accountId })}
          accounts={accounts.filter(account => account.status === 'Active' && (types as readonly string[]).includes(account.accountType) &&
            (allowControl ? account.allowDirectPosting || account.isControlAccount : account.allowDirectPosting && !account.isControlAccount))} disabled={disabled} /></td>
      </tr>)}</tbody>
    </table></div>
  </div>;
}
