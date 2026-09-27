'use client';

import React, { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { inventoryManagementService, type LandedCostDetailDto } from '@/services/inventoryManagementService';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';

export function LandedCostReceiptWeights({ voucher, onSaved, disabled = false }: {
  voucher: LandedCostDetailDto; onSaved: (value: LandedCostDetailDto) => void; disabled?: boolean;
}) {
  const [editing, setEditing] = useState<string | null>(null);
  const [weight, setWeight] = useState('');
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const save = async () => {
    if (saving || !editing) return;
    if (!weight.trim() || !Number.isFinite(Number(weight)) || Number(weight) <= 0 || !reason.trim()) {
      setError('Enter a positive weight in kilograms and a reason.'); return;
    }
    setSaving(true); setError('');
    try {
      const updated = await inventoryManagementService.setLandedCostReceiptWeight(voucher.id, editing,
        { unitWeightKg: Number(weight), reason: reason.trim(), editToken: voucher.editToken });
      onSaved(updated); setEditing(null);
    } catch (e) { setError(getProcurementProblemMessage(e, 'Could not save the voucher weight.')); }
    finally { setSaving(false); }
  };
  if (!voucher.receiptWeights?.length) return null;
  return <section className="space-y-3 rounded-lg border p-4" aria-label="Landed cost receipt weights">
    <h3 className="font-medium">Receipt weights</h3>
    <p className="text-sm text-muted-foreground">Weight allocation uses kilograms per stock unit. A voucher override changes only this draft’s allocation basis and requires a reason.</p>
    {voucher.receiptWeights.map(item => <div key={item.goodsReceiptNoteItemId} className="space-y-2 border-t pt-2">
      <div className="flex flex-wrap items-center justify-between gap-2"><span>{item.itemCode} — {item.itemName}: {item.unitWeightKg == null ? 'Weight not recorded' : `${item.unitWeightKg} kg / ${item.stockUom}`} {item.isOverridden ? '(voucher override)' : ''}</span>
        {voucher.status === 'Draft' && <Button size="sm" variant="outline" disabled={disabled || saving} onClick={() => {
          setEditing(item.goodsReceiptNoteItemId); setWeight(item.unitWeightKg?.toString() ?? ''); setReason(item.reason ?? ''); setError('');
        }}>Set weight for {item.itemCode}</Button>}</div>
      {item.reason && <p className="text-sm text-muted-foreground">{item.reason}</p>}
      {editing === item.goodsReceiptNoteItemId && voucher.status === 'Draft' && <div className="grid gap-2 sm:grid-cols-3">
        <Input aria-label="Voucher unit weight in kilograms" type="number" min="0.000001" step="0.000001" value={weight} disabled={saving} onChange={e => setWeight(e.target.value)} />
        <Input aria-label="Weight override reason" maxLength={500} value={reason} disabled={saving} onChange={e => setReason(e.target.value)} />
        <div className="flex gap-2"><Button disabled={disabled || saving} onClick={() => void save()}>Save weight</Button><Button variant="outline" disabled={saving} onClick={() => setEditing(null)}>Cancel</Button></div>
      </div>}
    </div>)}
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
  </section>;
}
