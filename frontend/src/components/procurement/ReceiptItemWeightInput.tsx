'use client';

import React, { useEffect, useState } from 'react';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { inventoryManagementService } from '@/services/inventoryManagementService';

export function ReceiptItemWeightInput({ inventoryItemId, stockUnit, value, disabled, onChange }: {
  inventoryItemId?: string | null;
  stockUnit: string;
  value?: number;
  disabled?: boolean;
  onChange: (value?: number) => void;
}) {
  const [master, setMaster] = useState<{ unit: string; kg?: number }>({ unit: stockUnit });
  const [lookupError, setLookupError] = useState(false);
  const [loading, setLoading] = useState(Boolean(inventoryItemId));
  useEffect(() => {
    let active = true;
    setMaster({ unit: stockUnit });
    setLookupError(false);
    setLoading(Boolean(inventoryItemId));
    if (inventoryItemId) {
      inventoryManagementService.getInventoryItemById(inventoryItemId).then(item => {
        if (!active) return;
        const factor = item.weightUnit === 'kg' ? 1 : item.weightUnit === 'g' ? 0.001 : item.weightUnit === 'lb' ? 0.45359237 : undefined;
        setMaster({ unit: item.unitOfMeasure, kg: item.weight != null && factor != null ? Math.round(item.weight * factor * 1e6) / 1e6 : undefined });
        setLoading(false);
      }).catch(() => { if (active) { setLookupError(true); setLoading(false); } });
    }
    return () => { active = false; };
  }, [inventoryItemId, stockUnit]);
  return <div className="space-y-2">
    <Label className="text-xs">Weight (kg / {master.unit || 'stock unit'})</Label>
    <Input aria-label={`Receipt weight for ${inventoryItemId || stockUnit}`} type="number" min="0" step="0.000001"
      disabled={disabled || lookupError || loading} value={value ?? ''} placeholder={master.kg != null ? `Default: ${master.kg}` : 'Not specified'}
      onChange={event => onChange(event.target.value === '' ? undefined : Number(event.target.value))} />
    <p className="text-xs text-muted-foreground">{loading ? 'Loading stock unit and default weight…' : lookupError ? 'Unable to load the stock unit. Reload before entering a weight.' : value != null ? 'Override for this receipt only.' : 'Blank copies the master weight when its unit is known.'}</p>
  </div>;
}
