'use client';

import React from 'react';
import { formatInventoryMoney } from '@/lib/inventory-currency';

const descriptions = {
  item: 'Stored item-wide average cost across warehouses. Reference only; not the exact-bin posting cost.',
  warehouse: 'Stored average cost for this warehouse assignment, not the item-wide or exact-bin cost.',
  count: 'Saved valuation unit cost used by this count line, not the current item-wide average.',
  standard: 'Standard cost maintained on the inventory item.',
};

export function InventoryCostValue({ value, kind, currencyCode }: {
  value?: number | null;
  kind: keyof typeof descriptions;
  currencyCode?: string;
}) {
  const amount = typeof value === 'number' && Number.isFinite(value)
    ? currencyCode ? formatInventoryMoney(value, currencyCode)
      : value.toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
    : '—';
  return <span className="whitespace-nowrap tabular-nums" title={`${descriptions[kind]}${currencyCode ? '' : ' Amount is in base currency.'}`}>{amount}</span>;
}
