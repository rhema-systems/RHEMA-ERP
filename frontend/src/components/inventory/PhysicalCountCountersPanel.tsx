'use client';

import React, { useRef, useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { inventoryManagementService, type PhysicalCountDetailDto, type PhysicalCountCounterOptionDto } from '@/services/inventoryManagementService';
import { PhysicalCountCounterSelector, counterErrorMessage } from './PhysicalCountCounterSelector';

type Props = { count: PhysicalCountDetailDto; disabled?: boolean; onSaved: () => Promise<void> };

export function PhysicalCountCountersPanel({ count, disabled = false, onSaved }: Props) {
  const active = (count.counters ?? []).filter(counter => counter.isActive);
  const [selected, setSelected] = useState<PhysicalCountCounterOptionDto[]>(() => active.map(counter => ({
    employeeId: counter.employeeId, employeeNumber: counter.employeeNumber, employeeName: counter.employeeName,
    userId: counter.userId, canAssign: true, hasEmail: true,
  })));
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const retry = useRef<{ signature: string; key: string } | null>(null);
  const recovery = count.status === 'UnderInvestigation';
  const editable = (count.status === 'Draft' || recovery) && count.canManageCounters === true;
  const changed = selected.length !== active.length || selected.some(employee => !active.some(counter => counter.employeeId === employee.employeeId));
  const save = async () => {
    if (!editable || disabled || saving || !changed || selected.length === 0 || (recovery && !reason.trim())) return;
    setSaving(true); setError(null);
    try {
      const employeeIds = selected.map(employee => employee.employeeId);
      const comment = reason.trim() || undefined;
      const signature = JSON.stringify([count.rowVersion, [...employeeIds].sort(), comment]);
      if (retry.current?.signature !== signature) retry.current = { signature, key: crypto.randomUUID() };
      await inventoryManagementService.updatePhysicalCountCounters(count.id, {
        employeeIds, rowVersion: count.rowVersion, idempotencyKey: retry.current.key, comment,
      });
      await onSaved();
      toast.success('Counting committee saved.');
    } catch (failure) {
      setError(counterErrorMessage(failure, 'The counting committee could not be saved.'));
    } finally { setSaving(false); }
  };
  return <section aria-label="Counting committee" className="space-y-3 rounded-md border p-4">
    {editable ? <>
      <PhysicalCountCounterSelector warehouseId={count.warehouseId} locationId={count.locationId}
        selected={selected} onChange={setSelected} disabled={disabled || saving} />
      {recovery && <p className="text-sm text-muted-foreground">Assign a committee for the recount. The submitted count observations remain unchanged.</p>}
      {changed && <div className="space-y-2"><Label htmlFor={`counter-reason-${count.id}`}>{recovery ? 'Investigation reason' : 'Assignment note (optional)'}</Label>
        <Input id={`counter-reason-${count.id}`} value={reason} onChange={event => setReason(event.target.value)} disabled={disabled || saving} />
      </div>}
      <Button type="button" size="sm" variant="outline" onClick={() => void save()}
        disabled={disabled || saving || !changed || selected.length === 0 || (recovery && !reason.trim())}>{saving ? 'Saving counters…' : 'Save counters'}</Button>
      {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
    </> : <>
      <h3 className="text-sm font-medium">Counters / counting committee</h3>
      {active.length === 0 ? <p className="text-sm text-muted-foreground">No counters assigned.</p> :
        <ul className="space-y-2">{active.map(counter => <li key={counter.id} className="text-sm">
          <span className="font-medium">{counter.employeeName}</span> · {counter.employeeNumber}
          <span className="block text-xs text-muted-foreground">Assigned {new Date(counter.assignedAtUtc).toLocaleString()}
            {counter.emailNotificationQueued ? ' · Email notification queued' : ''}</span>
        </li>)}</ul>}
    </>}
    {(count.counters?.length ?? 0) > 0 && <details className="text-sm">
      <summary className="cursor-pointer text-muted-foreground">Assignment history</summary>
      <ul className="mt-2 space-y-2">{[...(count.counters ?? [])].sort((a, b) => Date.parse(b.assignedAtUtc) - Date.parse(a.assignedAtUtc)).map(counter =>
        <li key={counter.id}>
          <span className="font-medium">{counter.employeeName}</span> · {counter.employeeNumber} · {counter.isActive ? 'Active' : 'Removed'}
          <span className="block text-xs text-muted-foreground">Assigned {new Date(counter.assignedAtUtc).toLocaleString()}
            {counter.removedAtUtc ? ` · Removed ${new Date(counter.removedAtUtc).toLocaleString()}` : ''}
            {counter.emailNotificationQueued ? ' · Email notification queued' : ''}</span>
          {counter.changeReason && <p className="text-xs">{counter.changeReason}</p>}
        </li>)}</ul>
    </details>}
  </section>;
}
