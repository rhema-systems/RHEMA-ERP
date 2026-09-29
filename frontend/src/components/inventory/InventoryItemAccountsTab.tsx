'use client';

import React, { useEffect, useState } from 'react';
import { Label } from '@/components/ui/label';
import { PostingAccountPicker } from '@/components/finance/PostingAccountPicker';
import type { AccountType } from '@/types/finance';
import { inventoryManagementService, type InventoryItemPostingAccountsDto, type InventoryPostingAccountOptionDto } from '@/services/inventoryManagementService';

export const inventoryPostingAccountFields: { key: keyof InventoryItemPostingAccountsDto; label: string; types: AccountType[] }[] = [
  { key: 'inventoryAccountId', label: 'Inventory', types: ['Asset'] },
  { key: 'inventoryDisposalAccountId', label: 'Inventory Disposal', types: ['Expense', 'Revenue'] },
  { key: 'inventoryOffsetAccountId', label: 'Inventory Offset', types: ['Asset', 'Liability'] },
  { key: 'costOfGoodsSoldAccountId', label: 'Cost Of Goods Sold', types: ['Expense'] },
  { key: 'salesAccountId', label: 'Sales', types: ['Revenue'] },
  { key: 'markdownsAccountId', label: 'Markdowns', types: ['Revenue', 'Expense'] },
  { key: 'salesReturnsAccountId', label: 'Sales Returns', types: ['Revenue', 'Expense'] },
  { key: 'inUseAccountId', label: 'In Use', types: ['Expense'] },
  { key: 'inServiceAccountId', label: 'In Service', types: ['Expense'] },
  { key: 'damagedAccountId', label: 'Damaged', types: ['Expense'] },
  { key: 'varianceAccountId', label: 'Variance', types: ['Expense', 'Revenue'] },
  { key: 'dropShipItemsAccountId', label: 'Drop Ship Items', types: ['Asset', 'Expense'] },
  { key: 'purchasePriceVarianceAccountId', label: 'Purchase Price Variance', types: ['Expense', 'Revenue'] },
  { key: 'unrealisedPurchasePriceVarianceAccountId', label: 'Unrealised Purchase Price Variance', types: ['Asset', 'Liability', 'Expense', 'Revenue'] },
  { key: 'inventoryReturnsAccountId', label: 'Inventory Returns', types: ['Asset'] },
  { key: 'assemblyVarianceAccountId', label: 'Assembly Variance', types: ['Expense', 'Revenue'] },
  { key: 'standardCostRevaluationAccountId', label: 'Standard Cost Revaluation', types: ['Expense', 'Revenue'] },
];

export function InventoryItemAccountsTab({ value, onChange }: {
  value?: InventoryItemPostingAccountsDto;
  onChange: (value: InventoryItemPostingAccountsDto) => void;
}) {
  const [accounts, setAccounts] = useState<InventoryPostingAccountOptionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  useEffect(() => {
    let active = true;
    inventoryManagementService.getItemPostingAccounts()
      .then(data => { if (active) setAccounts(data.filter(account => account.allowDirectPosting || account.isControlAccount)); })
      .catch(() => { if (active) setError(true); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);

  return <div className="space-y-3">
    <p className="text-sm text-muted-foreground">Unset accounts use Finance defaults, except Inventory Disposal, which must be selected before disposal. Existing stock is not reclassified.</p>
    {error && <p role="alert" className="text-sm text-destructive">GL accounts could not be loaded. Existing mappings are unchanged.</p>}
    <div className="grid grid-cols-1 gap-x-6 gap-y-2 md:grid-cols-2">
      {inventoryPostingAccountFields.map(field => <div key={field.key} className="grid min-w-0 grid-cols-[minmax(100px,150px)_minmax(0,1fr)] items-center gap-3">
        <Label htmlFor={`item-${field.key}`} className="text-xs">{field.label}</Label>
        <PostingAccountPicker id={`item-${field.key}`} value={value?.[field.key]}
          accounts={accounts.filter(account => field.types.includes(account.accountType))}
          disabled={loading || error} onChange={accountId => onChange({ ...value, [field.key]: accountId })} />
      </div>)}
    </div>
  </div>;
}
