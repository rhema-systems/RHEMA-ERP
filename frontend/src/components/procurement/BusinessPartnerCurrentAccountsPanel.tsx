'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { businessPartnerService, type BusinessPartnerDetailDto } from '@/services/businessPartnerService';
import { businessPartnerFinanceProfileService, type BusinessPartnerFinanceProfileSet } from '@/services/businessPartnerFinanceProfileService';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { Account, FinanceSettings } from '@/types/finance';

interface Props {
  businessPartnerId: string;
  partnerType: string;
  ledger: 'payables' | 'receivables';
  onOpenFinanceProfiles?: () => void;
}

interface AccountRow {
  purpose: string;
  accountId?: string | null;
  source: string;
  message?: string;
  unavailable?: boolean;
}

export function BusinessPartnerCurrentAccountsPanel({ businessPartnerId, partnerType, ledger, onOpenFinanceProfiles }: Props) {
  const [settings, setSettings] = useState<FinanceSettings | null>(null);
  const [partner, setPartner] = useState<BusinessPartnerDetailDto | null>(null);
  const [profiles, setProfiles] = useState<BusinessPartnerFinanceProfileSet | null>(null);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [loading, setLoading] = useState(true);
  const [errors, setErrors] = useState<string[]>([]);
  const [revision, setRevision] = useState(0);
  const [accountingDate, setAccountingDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [roleId, setRoleId] = useState('');
  const payable = ledger === 'payables';
  const profileLink = `/procurement/business-partners/${encodeURIComponent(businessPartnerId)}/edit?tab=finance-profiles`;

  useEffect(() => {
    let current = true;
    setLoading(true);
    setSettings(null);
    setPartner(null);
    setProfiles(null);
    setAccounts([]);
    setRoleId('');
    setErrors([]);
    void Promise.allSettled([
      financeDataService.getFinanceSettings(),
      businessPartnerService.getPostingOptions(payable ? 'Supplier' : 'Customer'),
      businessPartnerFinanceProfileService.get(businessPartnerId),
      businessPartnerService.getById(businessPartnerId),
    ]).then(([finance, catalogue, financeProfiles, details]) => {
      if (!current) return;
      const unavailable: string[] = [];
      if (finance.status === 'fulfilled') setSettings(finance.value);
      else unavailable.push('Finance settings could not be loaded.');
      if (catalogue.status === 'fulfilled') setAccounts(catalogue.value.accounts);
      else unavailable.push('Account names could not be loaded.');
      if (financeProfiles.status === 'fulfilled') setProfiles(financeProfiles.value);
      else unavailable.push('Finance profiles could not be loaded. Finance read access is required.');
      if (details.status === 'fulfilled') setPartner(details.value);
      else unavailable.push('Partner account details could not be loaded.');
      setErrors(unavailable);
      setLoading(false);
    });
    return () => { current = false; };
  }, [businessPartnerId, partnerType, payable, revision]);

  const roles = profiles?.roles.filter(role => payable
    ? role.roleType === 'Supplier' || role.roleType === 'Contractor'
    : role.roleType === 'Customer') ?? [];
  const role = roles.find(value => value.id === roleId) ?? roles.find(value => value.status === 'Active') ?? roles[0];
  // Finance selects approved defaults by accounting date, never by the newest draft.
  const versions = payable ? role?.apProfiles : role?.arProfiles;
  const effective = role?.status === 'Active' && accountingDate ? [...(versions ?? [])]
    .filter(profile => profile.status === 'Approved' && profile.effectiveFrom.slice(0, 10) <= accountingDate &&
      (!profile.effectiveTo || profile.effectiveTo.slice(0, 10) >= accountingDate))
    .sort((a, b) => b.effectiveFrom.localeCompare(a.effectiveFrom) || b.versionNumber - a.versionNumber)[0] : undefined;
  const expenseAccountId = effective && 'defaultExpenseAccountId' in effective ? effective.defaultExpenseAccountId : undefined;
  const tenant = (purpose: string, accountId?: string | null, source = 'Finance settings'): AccountRow =>
    ({ purpose, accountId, source, unavailable: !settings });
  const transaction = (purpose: string, source: string): AccountRow =>
    ({ purpose, source, message: 'Selected on transaction' });
  // Only these receipt overrides and the Sales-return override retain current posting consumers.
  const receiptAccount = (purpose: string, retained?: string | null, fallback?: string, source = 'Finance fallback'): AccountRow =>
    ({ purpose, accountId: retained || fallback, source: retained
      ? purpose === 'Receipt purchase price variance' ? 'Supplier default · item may override' : 'Supplier receipt default'
      : source,
      unavailable: !partner || (!retained && !settings) });
  const rows: AccountRow[] = payable ? [
    transaction('Cash', 'Payment bank / chequebook'),
    tenant('Accounts payable', settings?.controlAccountApId),
    { purpose: 'Purchases / expense', accountId: expenseAccountId, source: effective ? `${role?.roleType} AP profile v${effective.versionNumber}` : 'Finance profile',
      unavailable: !profiles, message: profiles && !effective ? 'No approved profile for this date' : undefined },
    tenant('Terms discounts taken', settings?.discountReceivedAccountId),
    { purpose: 'Terms discounts available', source: 'Payment terms', message: 'Recognized when taken' },
    { purpose: 'Trade discounts', source: 'Invoice calculation', message: 'Deducted from invoice cost' },
    transaction('Finance charges', 'Supplier invoice line'),
    transaction('Miscellaneous', 'Supplier invoice line'),
    transaction('Freight', 'Supplier invoice line'),
    transaction('Tax', 'Applicable tax configuration'),
    transaction('Writeoffs', 'AP adjustment'),
    receiptAccount('Accrued purchases', partner?.postingDefaults?.defaultAccruedPurchasesAccountId,
      settings?.controlAccountGRVAccrualId, 'Finance fallback · item may override'),
    receiptAccount('Receipt purchase price variance', partner?.postingDefaults?.defaultPurchasePriceVarianceAccountId,
      settings?.writeOffExpenseAccountId, 'Finance fallback · item may override'),
    transaction('Invoice purchase price variance', 'Item posting accounts'),
    tenant('Supplier advances', settings?.supplierAdvanceAccountId),
    tenant('Return-to-vendor clearing', settings?.returnToVendorClearingAccountId),
    tenant('Purchase return variance', settings?.purchaseReturnVarianceAccountId),
  ] : [
    transaction('Cash', 'Receipt bank / chequebook'),
    tenant('Accounts receivable', settings?.controlAccountArId),
    transaction('Sales', 'Invoice line / item sales account'),
    { ...tenant('Cost of sales', settings?.controlAccountCOGSId, 'Finance fallback · item may override'),
      unavailable: !settings || !Object.prototype.hasOwnProperty.call(settings, 'controlAccountCOGSId') },
    tenant('Inventory', settings?.controlAccountInventoryId, 'Finance fallback · item may override'),
    tenant('Terms discounts taken', settings?.discountAllowedAccountId),
    { purpose: 'Terms discounts available', source: 'Payment terms', message: 'Recognized when taken' },
    transaction('Finance charges', 'AR finance-charge adjustment'),
    transaction('Writeoffs', 'AR writeoff adjustment'),
    transaction('Overpayment writeoffs', 'AR credit-balance adjustment'),
    { purpose: 'Sales order returns', accountId: partner?.receivablesDefaults?.salesReturnsAccountId || settings?.discountAllowedAccountId,
      source: partner?.receivablesDefaults?.salesReturnsAccountId ? 'Customer sales-return default' : 'Finance discount-allowed fallback',
      unavailable: !partner || (!partner.receivablesDefaults?.salesReturnsAccountId && !settings) },
    tenant('Customer advances', settings?.customerAdvanceAccountId),
  ];

  return <Card>
    <CardHeader className="space-y-3 pb-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <CardTitle>{payable ? 'Accounts Payable' : 'Accounts Receivable'}</CardTitle>
        <div className="flex flex-wrap gap-2">
          <Button asChild type="button" size="sm" variant="outline"><Link href="/finance/settings">Finance settings</Link></Button>
          {onOpenFinanceProfiles
            ? <Button type="button" size="sm" variant="outline" onClick={onOpenFinanceProfiles}>Finance profiles</Button>
            : <Button asChild type="button" size="sm" variant="outline"><Link href={profileLink}>Finance profiles</Link></Button>}
          <Button asChild type="button" size="sm" variant="outline"><Link href={`/finance/subledger-adjustments/new?module=${payable ? 'AP' : 'AR'}`}>{payable ? 'Supplier adjustments' : 'Customer adjustments'}</Link></Button>
        </div>
      </div>
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2 text-sm">
        <div className="flex items-center gap-2">
          <Label htmlFor={`${ledger}-account-date`}>Profile date</Label>
          <Input id={`${ledger}-account-date`} type="date" value={accountingDate}
            onChange={event => setAccountingDate(event.target.value)} className="h-8 w-40" />
        </div>
        {roles.length > 1 && <div className="flex items-center gap-2">
          <Label htmlFor={`${ledger}-account-role`}>Role</Label>
          <select id={`${ledger}-account-role`} value={role?.id ?? ''} onChange={event => setRoleId(event.target.value)}
            className="h-8 rounded-md border bg-background px-2">
            {roles.map(value => <option key={value.id} value={value.id}>{value.roleType}{value.status !== 'Active' ? ' (inactive)' : ''}</option>)}
          </select>
        </div>}
        {!loading && profiles && <span className="text-muted-foreground">{effective
          ? `Approved ${payable ? 'AP' : 'AR'} profile v${effective.versionNumber}`
          : `No approved ${payable ? 'AP' : 'AR'} profile for this date`}</span>}
      </div>
    </CardHeader>
    <CardContent className="space-y-3">
      {loading ? <p role="status" className="text-sm">Loading accounts…</p> : <>
        {errors.length > 0 && <div role="alert" className="flex flex-wrap items-center gap-2 text-sm text-amber-700">
          <span>{errors.join(' ')}</span>
          <Button type="button" variant="outline" size="sm" onClick={() => setRevision(value => value + 1)}>Retry accounts</Button>
        </div>}
        <div className="overflow-x-auto rounded-md border">
          <Table aria-label={payable ? 'Supplier posting accounts' : 'Customer posting accounts'} className="min-w-[640px] text-sm">
            <TableHeader><TableRow className="bg-muted/40">
              <TableHead className="h-9">Purpose</TableHead><TableHead className="h-9">Account code</TableHead>
              <TableHead className="h-9">Account name</TableHead><TableHead className="h-9">Source</TableHead>
            </TableRow></TableHeader>
            <TableBody>{rows.map(row => {
              const account = row.accountId ? accounts.find(value => value.id === row.accountId) : undefined;
              const name = row.unavailable ? 'Unavailable' : row.message ||
                (account ? account.accountName : row.accountId ? 'Configured account unavailable' : 'Not configured');
              return <TableRow key={row.purpose}>
                <TableCell className="py-1.5 font-medium">{row.purpose}</TableCell>
                <TableCell className="py-1.5 font-mono whitespace-nowrap">{!row.unavailable && account ? account.accountCode || account.accountNumber : '—'}</TableCell>
                <TableCell className="py-1.5">{name}</TableCell>
                <TableCell className="py-1.5 text-muted-foreground">{row.source}</TableCell>
              </TableRow>;
            })}</TableBody>
          </Table>
        </div>
        <p className="text-xs text-muted-foreground">The transaction distribution confirms the final posting accounts. Posted journals keep their original accounts.</p>
      </>}
    </CardContent>
  </Card>;
}
