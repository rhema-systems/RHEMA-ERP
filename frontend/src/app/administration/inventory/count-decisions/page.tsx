'use client';

import React, { useEffect, useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { inventoryManagementService as service, PhysicalCountDecisionOption, PhysicalCountDecisionSetup } from '@/services/inventoryManagementService';
import { countReviewError } from '@/components/inventory/PhysicalCountReviewActions';

export default function CountDecisionsPage() {
  const [setup, setSetup] = useState<PhysicalCountDecisionSetup | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const load = async () => {
    setBusy(true); setError('');
    try { setSetup(await service.getPhysicalCountDecisions(true)); }
    catch (ex) { setError(countReviewError(ex)); }
    finally { setBusy(false); }
  };
  useEffect(() => { void load(); }, []);
  const update = (index: number, changes: Partial<PhysicalCountDecisionOption>) => setSetup(current => current ? {
    ...current, decisions: current.decisions.map((d, i) => i === index ? { ...d, ...changes } : d)
  } : null);
  const save = async () => {
    if (!setup) return;
    setBusy(true); setError('');
    try { setSetup(await service.savePhysicalCountDecisions(setup)); toast.success('Count decisions saved.'); }
    catch (ex) { setError(countReviewError(ex)); }
    finally { setBusy(false); }
  };
  return <div className="space-y-5 p-6">
    <div><h1 className="text-2xl font-semibold">Physical count decisions</h1><p className="text-sm text-muted-foreground">Set the choices available during Stores, Finance and Audit review.</p></div>
    {error && <div role="alert" className="rounded-lg border border-red-200 p-3 text-red-700">{error}</div>}
    <Card><CardHeader><CardTitle>Approval decisions</CardTitle></CardHeader><CardContent className="space-y-4">
      <p className="text-sm text-muted-foreground">Approve adjustment advances the approval stage; stock changes only after final approval and Post. Investigate stops posting and returns the count for investigation. Keep at least one active choice for each effect.</p>
      <div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>Code</TableHead><TableHead>Decision label</TableHead><TableHead>Effect</TableHead><TableHead>Active</TableHead></TableRow></TableHeader><TableBody>
        {setup?.decisions.map((d, index) => <TableRow key={index}>
          <TableCell><Input aria-label={`Decision code ${index + 1}`} value={d.code} maxLength={40} onChange={e => update(index, { code: e.target.value.toUpperCase() })} disabled={busy} /></TableCell>
          <TableCell><Input aria-label={`Decision label ${index + 1}`} value={d.label} maxLength={100} onChange={e => update(index, { label: e.target.value })} disabled={busy} /></TableCell>
          <TableCell><Select value={d.effect} disabled={busy} onValueChange={effect => update(index, { effect: effect as PhysicalCountDecisionOption['effect'] })}><SelectTrigger aria-label={`Decision effect ${index + 1}`}><SelectValue /></SelectTrigger><SelectContent><SelectItem value="ApproveAdjustment">Approve adjustment</SelectItem><SelectItem value="Investigate">Start investigation</SelectItem></SelectContent></Select></TableCell>
          <TableCell><Switch aria-label={`Active decision ${index + 1}`} checked={d.isActive} disabled={busy} onCheckedChange={isActive => update(index, { isActive })} /></TableCell>
        </TableRow>)}
      </TableBody></Table></div>
      <div className="flex gap-2"><Button variant="outline" disabled={busy || !setup || setup.decisions.length >= 30} onClick={() => setSetup(current => current ? { ...current, decisions: [...current.decisions, { code: '', label: '', effect: 'Investigate', isActive: true }] } : null)}>Add decision</Button><Button variant="outline" disabled={busy} onClick={() => void load()}>Refresh</Button><Button disabled={busy || !setup} onClick={() => void save()}>{busy ? 'Please wait…' : 'Save changes'}</Button></div>
    </CardContent></Card>
  </div>;
}
