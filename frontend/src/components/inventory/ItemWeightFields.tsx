'use client';

import React from 'react';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

export function ItemWeightFields({ weight, weightUnit, stockUnit, onChange }: {
  weight?: number | null;
  weightUnit?: string | null;
  stockUnit: string;
  onChange: (values: { weight?: number; weightUnit?: string }) => void;
}) {
  return <div className="grid grid-cols-2 gap-4">
    <div className="space-y-2">
      <Label>Item weight per {stockUnit || 'stock unit'}</Label>
      <Input aria-label="Item weight" type="number" min="0" step="0.000001" value={weight ?? ''}
        onChange={event => onChange({ weight: event.target.value === '' ? undefined : Number(event.target.value), weightUnit: weightUnit ?? undefined })} />
      <p className="text-xs text-muted-foreground">Copied to new receipts for landed-cost allocation.</p>
    </div>
    <div className="space-y-2">
      <Label>Weight unit</Label>
      <select aria-label="Weight unit" className="h-10 w-full rounded-md border bg-background px-3" value={weightUnit ?? ''}
        onChange={event => onChange({ weight: weight ?? undefined, weightUnit: event.target.value || undefined })}>
        <option value="">Unit not specified</option>
        <option value="kg">Kilograms (kg)</option>
        <option value="g">Grams (g)</option>
        <option value="lb">Pounds (lb)</option>
      </select>
      {weight != null && !weightUnit && <p className="text-xs text-amber-700">Confirm the unit before this weight can be copied to receipts.</p>}
    </div>
  </div>;
}
