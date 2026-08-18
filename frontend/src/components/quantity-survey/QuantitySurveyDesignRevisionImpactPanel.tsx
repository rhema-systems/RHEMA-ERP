'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { GitCompareArrows, RefreshCw, Send, ShieldCheck, ShieldX } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import {
  type DesignImpact,
  type DesignImpactLookups,
  type DesignImpactType,
  quantitySurveyDesignImpactService,
} from '@/services/quantity-survey-design-impact.service';

const impactTypes: Array<{ value: Exclude<DesignImpactType, number>; label: string }> = [
  { value: 'RemeasurementRequired', label: 'Remeasurement required' },
  { value: 'QuantityIncrease', label: 'Quantity increase' },
  { value: 'QuantityDecrease', label: 'Quantity decrease' },
  { value: 'ScopeAddition', label: 'Scope addition' },
  { value: 'Omission', label: 'Omission' },
  { value: 'RateReviewOnly', label: 'Rate review only' },
];

type LineDraft = { selected: boolean; impactType: Exclude<DesignImpactType, number>; indicativeQuantity: string; impactReason: string };
const emptyLookups: DesignImpactLookups = { drawings: [], approvedBoqLines: [] };
const newId = () => crypto.randomUUID();

export function QuantitySurveyDesignRevisionImpactPanel({ projectId }: { projectId: string }) {
  const [lookups, setLookups] = useState(emptyLookups);
  const [items, setItems] = useState<DesignImpact[]>([]);
  const [loading, setLoading] = useState(false);
  const [previousId, setPreviousId] = useState('');
  const [revisedId, setRevisedId] = useState('');
  const [route, setRoute] = useState<'Measurement' | 'Variation'>('Measurement');
  const [title, setTitle] = useState('');
  const [summary, setSummary] = useState('');
  const [lineDrafts, setLineDrafts] = useState<Record<string, LineDraft>>({});
  const [decisionReason, setDecisionReason] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [lookupResult, records] = await Promise.all([
        quantitySurveyDesignImpactService.lookups(projectId),
        quantitySurveyDesignImpactService.list(projectId),
      ]);
      setLookups(lookupResult);
      setItems(records);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Could not load design revision impacts.');
    } finally {
      setLoading(false);
    }
  }, [projectId]);

  useEffect(() => { void load(); }, [load]);

  const revisedOptions = useMemo(() => lookups.drawings.filter((item) => item.supersedesDrawingId === previousId), [lookups.drawings, previousId]);
  const selectedCount = Object.values(lineDrafts).filter((item) => item.selected).length;

  const save = async () => {
    const lines = Object.entries(lineDrafts).filter(([, value]) => value.selected).map(([id, value]) => ({
      projectBoqVersionLineId: id,
      impactType: value.impactType,
      indicativeQuantity: value.indicativeQuantity === '' ? undefined : Number(value.indicativeQuantity),
      impactReason: value.impactReason.trim(),
    }));
    if (!previousId || !revisedId || title.trim().length < 3 || summary.trim().length < 10 || lines.length === 0) {
      toast.error('Select the drawing lineage and affected BoQ lines, then enter a title and change summary.');
      return;
    }
    if (lines.some((line) => line.impactReason.length < 5)) {
      toast.error('Enter an impact reason for every selected BoQ line.');
      return;
    }
    setLoading(true);
    try {
      await quantitySurveyDesignImpactService.create({
        clientRequestId: newId(), projectId, previousDrawingId: previousId,
        revisedDrawingId: revisedId, route, title: title.trim(), changeSummary: summary.trim(), lines,
      });
      toast.success('Design revision impact registered.');
      setTitle(''); setSummary(''); setLineDrafts({}); setRevisedId('');
      await load();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Could not register the design impact.');
    } finally { setLoading(false); }
  };

  const transition = async (item: DesignImpact, action: 'submit' | 'approve' | 'reject') => {
    if (decisionReason.trim().length < 5) { toast.error('Enter a reason of at least 5 characters.'); return; }
    setLoading(true);
    try {
      await quantitySurveyDesignImpactService[action](item.id, {
        clientRequestId: newId(), reason: decisionReason.trim(), rowVersion: item.rowVersion,
      });
      toast.success(`Design impact ${action === 'submit' ? 'submitted' : action === 'approve' ? 'approved' : 'rejected'}.`);
      setDecisionReason(''); await load();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : `Could not ${action} the design impact.`);
    } finally { setLoading(false); }
  };

  return (
    <Card className="border-slate-200/70 shadow-sm">
      <CardHeader className="pb-3">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <CardTitle className="flex items-center gap-2 text-base"><GitCompareArrows className="h-4 w-4" />Design revision impact</CardTitle>
            <CardDescription>Route approved drawing changes to the governed measurement or variation workflow.</CardDescription>
          </div>
          <Button variant="outline" size="sm" onClick={() => void load()} disabled={loading}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>
        </div>
      </CardHeader>
      <CardContent className="space-y-5">
        <div className="grid gap-4 lg:grid-cols-3">
          <div className="space-y-2"><Label>Prior approved revision</Label><Select value={previousId || 'none'} onValueChange={(value) => { setPreviousId(value === 'none' ? '' : value); setRevisedId(''); }}><SelectTrigger><SelectValue placeholder="Select prior drawing" /></SelectTrigger><SelectContent><SelectItem value="none">Select prior drawing</SelectItem>{lookups.drawings.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div>
          <div className="space-y-2"><Label>Revised drawing</Label><Select value={revisedId || 'none'} onValueChange={(value) => setRevisedId(value === 'none' ? '' : value)} disabled={!previousId}><SelectTrigger><SelectValue placeholder="Select linked revision" /></SelectTrigger><SelectContent><SelectItem value="none">Select linked revision</SelectItem>{revisedOptions.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div>
          <div className="space-y-2"><Label>Workflow route</Label><Select value={route} onValueChange={(value) => setRoute(value as 'Measurement' | 'Variation')}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="Measurement">Measurement / remeasurement</SelectItem><SelectItem value="Variation">Variation / change order</SelectItem></SelectContent></Select></div>
        </div>
        <div className="grid gap-4 lg:grid-cols-2"><div className="space-y-2"><Label>Impact title</Label><Input value={title} onChange={(event) => setTitle(event.target.value)} placeholder="Structural revision impact" /></div><div className="space-y-2"><Label>Change summary</Label><Textarea rows={2} value={summary} onChange={(event) => setSummary(event.target.value)} placeholder="Explain what changed and why QS review is required." /></div></div>
        <div className="space-y-2">
          <div className="flex items-center justify-between"><Label>Affected approved BoQ lines</Label><span className="text-xs text-slate-500">{selectedCount} selected</span></div>
          <div className="max-h-80 space-y-2 overflow-y-auto rounded-lg border p-2">
            {lookups.approvedBoqLines.length === 0 ? <p className="p-3 text-sm text-slate-500">No published approved BoQ lines are available.</p> : lookups.approvedBoqLines.map((line) => {
              const draft = lineDrafts[line.id] ?? { selected: false, impactType: 'RemeasurementRequired', indicativeQuantity: '', impactReason: '' };
              return <div key={line.id} className="grid gap-2 rounded-md border p-3 lg:grid-cols-[auto,minmax(220px,1fr),190px,130px,minmax(220px,1fr)] lg:items-center">
                <Checkbox checked={draft.selected} onCheckedChange={(checked) => setLineDrafts((current) => ({ ...current, [line.id]: { ...draft, selected: checked === true } }))} />
                <div><div className="text-sm font-medium">{line.label}</div><div className="text-xs text-slate-500">{line.group} · {line.description}</div></div>
                <Select value={draft.impactType as string} onValueChange={(value) => setLineDrafts((current) => ({ ...current, [line.id]: { ...draft, impactType: value as Exclude<DesignImpactType, number> } }))} disabled={!draft.selected}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{impactTypes.map((type) => <SelectItem key={type.value} value={type.value}>{type.label}</SelectItem>)}</SelectContent></Select>
                <Input type="number" min="0" step="0.0001" value={draft.indicativeQuantity} onChange={(event) => setLineDrafts((current) => ({ ...current, [line.id]: { ...draft, indicativeQuantity: event.target.value } }))} placeholder="Indicative qty" disabled={!draft.selected} />
                <Input value={draft.impactReason} onChange={(event) => setLineDrafts((current) => ({ ...current, [line.id]: { ...draft, impactReason: event.target.value } }))} placeholder="Line impact reason" disabled={!draft.selected} />
              </div>;
            })}
          </div>
        </div>
        <div className="flex justify-end"><Button onClick={() => void save()} disabled={loading}><GitCompareArrows className="mr-2 h-4 w-4" />Register impact</Button></div>

        <div className="border-t pt-4">
          <div className="mb-3 grid gap-3 md:grid-cols-[minmax(240px,1fr),auto]"><Input value={decisionReason} onChange={(event) => setDecisionReason(event.target.value)} placeholder="Submission or approval reason" /><span className="self-center text-xs text-slate-500">Required for every workflow action</span></div>
          <div className="space-y-3">{items.length === 0 ? <p className="rounded-lg border border-dashed p-4 text-sm text-slate-500">No design impacts have been registered.</p> : items.map((item) => <div key={item.id} className="rounded-lg border p-4">
            <div className="flex flex-wrap items-start justify-between gap-3"><div><div className="flex flex-wrap items-center gap-2"><span className="font-semibold">{item.impactNumber}</span><Badge variant="outline">{typeof item.route === 'number' ? (item.route === 0 ? 'Measurement' : 'Variation') : item.route}</Badge><Badge variant="secondary">{item.status}</Badge></div><div className="mt-1 text-sm">{item.title}</div><div className="mt-1 text-xs text-slate-500">{item.previousDrawingLabel} → {item.revisedDrawingLabel} · {item.lines.length} affected line(s)</div></div><div className="flex flex-wrap gap-2">{item.status === 'Draft' ? <Button size="sm" variant="outline" onClick={() => void transition(item, 'submit')} disabled={loading}><Send className="mr-2 h-4 w-4" />Submit</Button> : null}{item.status === 'PendingApproval' ? <><Button size="sm" onClick={() => void transition(item, 'approve')} disabled={loading}><ShieldCheck className="mr-2 h-4 w-4" />Approve</Button><Button size="sm" variant="destructive" onClick={() => void transition(item, 'reject')} disabled={loading}><ShieldX className="mr-2 h-4 w-4" />Reject</Button></> : null}</div></div>
          </div>)}</div>
        </div>
      </CardContent>
    </Card>
  );
}
