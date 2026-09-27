'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { businessPartnerService } from '@/services/businessPartnerService';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { Account, FinanceSettings } from '@/types/finance';

interface Props {
  businessPartnerId: string;
  partnerType: string;
  ledger: 'payables' | 'receivables';
}

export function BusinessPartnerCurrentAccountsPanel({ businessPartnerId, partnerType, ledger }: Props) {
  const [settings, setSettings] = useState<FinanceSettings | null>(null);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [loading, setLoading] = useState(true);
  const [errors, setErrors] = useState<string[]>([]);
  const [revision, setRevision] = useState(0);

  useEffect(() => {
    let current = true;
    setLoading(true);
    setSettings(null);
    setAccounts([]);
    setErrors([]);
    void Promise.allSettled([
      financeDataService.getFinanceSettings(),
      businessPartnerService.getPostingOptions(partnerType),
    ]).then(([finance, catalogue]) => {
      if (!current) return;
      const unavailable: string[] = [];
      if (finance.status === 'fulfilled') setSettings(finance.value);
      else unavailable.push('Finance settings could not be loaded. Your access may be restricted.');
      if (catalogue.status === 'fulfilled') setAccounts(catalogue.value.accounts);
      else unavailable.push('Account names could not be loaded. Your access may be restricted.');
      setErrors(unavailable);
      setLoading(false);
    });
    return () => { current = false; };
  }, [businessPartnerId, partnerType, revision]);

  const payable = ledger === 'payables';
  const defaults = payable
    ? [{ label: 'Accounts payable control', id: settings?.controlAccountApId }, { label: 'Discount received', id: settings?.discountReceivedAccountId }]
    : [{ label: 'Accounts receivable control', id: settings?.controlAccountArId }, { label: 'Discount allowed', id: settings?.discountAllowedAccountId },
      { label: 'Inventory', id: settings?.controlAccountInventoryId }];
  const transactionSources = payable
    ? [{ label: 'Invoice expense / cost', source: 'Resolved on the invoice from its lines and applicable Finance Profile.' },
      { label: 'Freight / miscellaneous / finance charges', source: 'Invoice line account, with applicable invoice and Finance profile defaults.' },
      { label: 'Receipt accrual / purchase price variance', source: 'Receipt distribution, using applicable PO, supplier, item and Finance defaults.' },
      { label: 'Cash / bank', source: 'Selected on the payment through its bank account or chequebook.' }, { label: 'Tax', source: 'Resolved on the invoice from its tax configuration.' }]
    : [{ label: 'Sales / revenue', source: 'Selected on the invoice line.' }, { label: 'Cost of sales', source: 'Finance COGS configuration used for inventory invoice posting.' }, { label: 'Cash / bank', source: 'Selected on the payment through its bank account or chequebook.' },
      { label: 'Finance charges', source: 'Selected on the finance-charge adjustment as its contra account.' },
      { label: 'Writeoffs', source: 'Selected on the writeoff adjustment as its contra account.' },
      { label: 'Overpayment writeoffs', source: 'Selected on the credit-balance adjustment as its contra account.' },
      { label: 'Sales returns', source: 'The saved customer sales-return account, falling back to Discount allowed. A linked credit note retains the original invoice AR control account.' }];
  const accountLabel = (id?: string) => {
    if (!settings) return 'Unavailable';
    if (!id) return 'Not configured';
    const account = accounts.find(value => value.id === id);
    return account ? `${account.accountCode || account.accountNumber} · ${account.accountName}` : 'Configured account — details unavailable';
  };

  return <Card>
    <CardHeader>
      <CardTitle>{payable ? 'Accounts Payable' : 'Accounts Receivable'}</CardTitle>
      <CardDescription>Current account sources for this partner&apos;s transactions.</CardDescription>
    </CardHeader>
    <CardContent className="space-y-4">
      <p className="text-sm">Control accounts and discounts come from Finance Settings. {payable ? 'Partner payment, tax and expense defaults are maintained in Finance Profiles.' : 'Customer payment terms and credit limits are maintained in Finance Profiles.'}</p>
      {loading ? <p role="status">Loading current account sources…</p> : <>
        {errors.length > 0 && <div role="alert" className="space-y-2 text-sm text-amber-700">{errors.map(error => <p key={error}>{error}</p>)}<Button type="button" variant="outline" size="sm" onClick={() => setRevision(value => value + 1)}>Retry account sources</Button></div>}
        <Table aria-label="Current account sources">
          <TableHeader><TableRow><TableHead>Purpose</TableHead><TableHead>Account / source</TableHead><TableHead>Scope</TableHead></TableRow></TableHeader>
          <TableBody>
            {defaults.map(row => <TableRow key={row.label}><TableCell>{row.label}</TableCell><TableCell>{accountLabel(row.id)}</TableCell><TableCell>Tenant default</TableCell></TableRow>)}
            {transactionSources.map(row => <TableRow key={row.label}><TableCell>{row.label}</TableCell><TableCell>{row.source}</TableCell><TableCell>Transaction</TableCell></TableRow>)}
          </TableBody>
        </Table>
      </>}
      <p className="text-sm text-muted-foreground">Where supported, explicit transaction selections determine the final accounts. Existing posted journals retain their original accounts. Review the transaction&apos;s accounting distribution for the accounts actually used.</p>
      <div className="flex flex-wrap gap-3">
        <Button asChild variant="outline"><Link href="/finance/settings">Open Finance Settings</Link></Button>
        <Button asChild variant="outline"><Link href={`/procurement/business-partners/${encodeURIComponent(businessPartnerId)}/edit?tab=finance-profiles`}>Open Finance Profiles</Link></Button>
      </div>
    </CardContent>
  </Card>;
}
