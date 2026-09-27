'use client';

import React, { useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { inventoryManagementService as service, type PhysicalCountDetailDto, type PhysicalCountDto } from '@/services/inventoryManagementService';

type Props = { count: PhysicalCountDetailDto; unsaved: boolean; onOpen: (count: PhysicalCountDto) => Promise<void>; errorMessage: (error: unknown, fallback: string) => string };

export function PhysicalCountRecountPanel({ count, unsaved, onOpen, errorMessage }: Props) {
  const [open, setOpen] = useState(false);
  const [reasons, setReasons] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const replay = useRef<{ payload: string; key: string } | undefined>(undefined);
  const candidates = count.items.filter(item => item.isCounted && !item.supersededByPhysicalCountId);
  const save = async () => {
    const items = candidates.filter(item => item.id in reasons).map(item => ({ physicalCountItemId: item.id, itemRowVersion: item.rowVersion, reason: reasons[item.id].trim() }));
    if (!items.length || items.some(item => !item.reason)) { setError('Select items and give a reason for each recount.'); return; }
    const payload = JSON.stringify({ rowVersion: count.rowVersion, items });
    if (replay.current?.payload !== payload) replay.current = { payload, key: crypto.randomUUID() };
    setBusy(true); setError('');
    try {
      const child = await service.createPhysicalCountRecount(count.id, { rowVersion: count.rowVersion, items, idempotencyKey: replay.current.key });
      setOpen(false); await onOpen(child);
    } catch (failure) { setError(errorMessage(failure, 'The recount sheet could not be created.')); }
    finally { setBusy(false); }
  };
  return <div className="flex shrink-0 flex-wrap items-center gap-2">
    {(count.sheets?.length ?? 0) > 1 && <label className="flex items-center gap-2 text-xs">Count sheet<select aria-label="Count sheet" value={count.id} disabled={busy || unsaved} className="h-8 max-w-80 rounded border bg-background px-2" onChange={e => { const sheet = count.sheets?.find(item => item.id === e.target.value); if (sheet) void onOpen(sheet); }}>{count.sheets?.map(sheet => <option key={sheet.id} value={sheet.id}>{sheet.recountAttempt ? `Recount #${sheet.recountAttempt}` : 'Original'} · {sheet.countNumber} · {sheet.status}</option>)}</select></label>}
    {!!count.recountAttempt && <span className="text-xs text-muted-foreground">Selected items only · Recount #{count.recountAttempt}</span>}
    {count.canCreateRecount && <Button variant="outline" size="sm" disabled={busy || unsaved || !candidates.length} onClick={() => setOpen(true)}>Select items for recount</Button>}
    <Dialog open={open} onOpenChange={value => { if (!busy) setOpen(value); }}><DialogContent className="max-h-[85dvh] overflow-y-auto sm:max-w-2xl"><DialogHeader><DialogTitle>Select items for recount</DialogTitle></DialogHeader>
      {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
      <div className="space-y-2">{candidates.map(item => <div key={item.id} className="grid grid-cols-[auto_1fr] items-center gap-2 rounded border p-2"><input type="checkbox" aria-label={`Recount ${item.itemCode}`} checked={item.id in reasons} disabled={busy} onChange={e => setReasons(previous => { const next = { ...previous }; if (e.target.checked) next[item.id] = ''; else delete next[item.id]; return next; })} /><span className="text-sm">{item.itemCode} · {item.itemName} · {item.locationName || 'No location'}</span>{item.id in reasons && <Input className="col-start-2 h-8" aria-label={`Recount reason ${item.itemCode}`} maxLength={2000} value={reasons[item.id]} disabled={busy} onChange={e => setReasons(previous => ({ ...previous, [item.id]: e.target.value }))} placeholder="Investigation reason" />}</div>)}</div>
      <DialogFooter><Button variant="outline" disabled={busy} onClick={() => setOpen(false)}>Close</Button><Button disabled={busy || !Object.keys(reasons).length} onClick={() => void save()}>{busy ? 'Creating…' : 'Create recount sheet'}</Button></DialogFooter>
    </DialogContent></Dialog>
  </div>;
}
