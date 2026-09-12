'use client';

import React from 'react';
import { groupLandedCostsBySupplier, type SupplierCostLine } from '@/lib/landed-cost-suppliers';

export function LandedCostSupplierSummary({ lines }: { lines: SupplierCostLine[] }) {
  if (lines.length === 0) return null;
  const groups = groupLandedCostsBySupplier(lines);
  return <details className="rounded-lg border p-3 mt-3">
    <summary className="cursor-pointer text-sm font-medium">Supplier totals</summary>
    <div className="mt-2 space-y-2">
      {groups.map(group => <div key={group.key} className="flex justify-between gap-3 text-sm">
        <span>{group.supplierName}<span className="block text-xs text-muted-foreground">
          {group.count} charge{group.count === 1 ? '' : 's'}{group.reference ? ` · ${group.reference}` : ''}
          {group.linked ? ' · Invoice linked' : ' · No invoice linked'}
        </span></span>
        <span className="whitespace-nowrap">{group.currency} {group.amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span>
      </div>)}
      <p className="text-xs text-muted-foreground">Grouped by supplier, currency and bill reference. These are cost totals, not posted supplier invoices.</p>
    </div>
  </details>;
}
