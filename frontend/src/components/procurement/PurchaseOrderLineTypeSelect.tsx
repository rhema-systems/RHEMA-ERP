'use client';

import React from 'react';

import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { purchaseOrderLineType, type PurchaseOrderLineType } from '@/lib/purchase-order-line-types';

export function PurchaseOrderLineTypeSelect({ value, onChange }: {
  value?: PurchaseOrderLineType;
  onChange: (value: PurchaseOrderLineType) => void;
}) {
  return <Select value={String(purchaseOrderLineType(value))} onValueChange={value => onChange(Number(value) as PurchaseOrderLineType)}>
    <SelectTrigger aria-label="Purchase order line type" className="mb-2"><SelectValue /></SelectTrigger>
    <SelectContent>
      <SelectItem value="1">Stock goods</SelectItem>
      <SelectItem value="3">Non-stock goods</SelectItem>
      <SelectItem value="2">Service</SelectItem>
      <SelectItem value="4">Fixed asset</SelectItem>
    </SelectContent>
  </Select>;
}
