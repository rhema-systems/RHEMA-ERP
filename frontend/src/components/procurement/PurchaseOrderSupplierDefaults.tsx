'use client';

import { useEffect, useState } from 'react';
import type { PurchaseOrderSupplierDefaultsDto } from '@/services/purchasingService';
import { businessPartnerService, type BusinessPartnerPostingOptions } from '@/services/businessPartnerService';
import { formatProcurementMoney } from '@/lib/procurement-currency';

export function PurchaseOrderSupplierDefaults({ defaults, paymentTerms }: {
  defaults?: PurchaseOrderSupplierDefaultsDto | null;
  paymentTerms?: string;
}) {
  const [options, setOptions] = useState<BusinessPartnerPostingOptions | null>(null);
  const taxId = defaults?.postingDefaults.defaultTaxGroupId;
  const bankId = defaults?.postingDefaults.defaultBankAccountId;
  useEffect(() => {
    let current = true;
    if (!taxId && !bankId) return;
    void businessPartnerService.getPostingOptions('Supplier').then(data => { if (current) setOptions(data); }).catch(() => { if (current) setOptions(null); });
    return () => { current = false; };
  }, [taxId, bankId]);
  if (!defaults) return null;
  const saved = defaults.postingDefaults;
  const tax = options?.taxGroups.find(group => group.id === taxId);
  const bank = options?.bankAccounts.find(account => account.id === bankId);
  const fields: Array<[string, string]> = [];
  if (defaults.paymentTerms && defaults.paymentTerms !== paymentTerms) fields.push(['Default Payment Terms', defaults.paymentTerms]);
  if (defaults.tin) fields.push(['TIN', defaults.tin]);
  if (bankId) fields.push(['ChequeBook ID', bank ? `${bank.accountNumber} - ${bank.accountName}` : 'Saved chequebook']);
  if (saved.subjectToWithholdingDeduction) fields.push(['WHT Rate', `${saved.withholdingTaxRate}%`]);
  if (taxId) fields.push(['Tax', tax ? `${tax.code} - ${tax.name}` : 'Saved tax schedule']);
  if (defaults.creditLimit != null) fields.push(['Credit Limit', defaults.currency ? formatProcurementMoney(defaults.creditLimit, defaults.currency) : Number(defaults.creditLimit).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })]);
  if (!fields.length) return null;
  return <section aria-label="Saved supplier defaults" className="border-t pt-3">
    <h3 className="mb-2 text-sm font-medium">Supplier defaults</h3>
    <dl className="grid grid-cols-1 gap-x-4 gap-y-2 text-sm sm:grid-cols-2">
      {fields.map(([label, value]) => <div key={label} className="min-w-0"><dt className="text-muted-foreground">{label}</dt><dd className="break-words font-medium">{value}</dd></div>)}
    </dl>
  </section>;
}
