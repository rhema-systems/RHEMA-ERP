'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { CheckCircle2, Download, FileUp, Plus, RefreshCw, Save, Send, Trash2, XCircle } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { quantitySurveyDayworkService as service, type DayworkSheet, type DayworkWorkspace } from '@/services/quantity-survey-daywork.service';

type Props = { projectId: string; external?: boolean };
type DraftLine = { key: string; rateId: string; quantity: number; note: string };
const empty: DayworkWorkspace = { variations: [], rates: [], sheets: [] };
const newLine = (): DraftLine => ({ key: crypto.randomUUID(), rateId: '', quantity: 1, note: '' });
const label = (value: string) => value.replace(/([a-z])([A-Z])/g, '$1 $2');
const money = (value: number, currency: string) => new Intl.NumberFormat(undefined, { style: 'currency', currency, maximumFractionDigits: 2 }).format(value || 0);

export function QuantitySurveyDayworkWorkspace({ projectId, external = false }: Props) {
  const { hasPermission } = useAuth();
  const canRead = external || hasPermission('quantity-survey.workspace.read');
  const canManage = !external && hasPermission('quantity-survey.variations.manage');
  const requests = useRef<Record<string, string>>({});
  const [workspace, setWorkspace] = useState(empty); const [selectedId, setSelectedId] = useState('');
  const [loading, setLoading] = useState(true); const [busy, setBusy] = useState(false);
  const [variationId, setVariationId] = useState(''); const [workDate, setWorkDate] = useState(new Date().toISOString().slice(0, 10));
  const [location, setLocation] = useState(''); const [description, setDescription] = useState('');
  const [lines, setLines] = useState<DraftLine[]>([newLine()]); const [reason, setReason] = useState('');
  const [evidenceTitle, setEvidenceTitle] = useState(''); const [evidenceFile, setEvidenceFile] = useState<File | null>(null);
  const selected = useMemo(() => workspace.sheets.find(value => value.id === selectedId), [selectedId, workspace.sheets]);
  const variation = workspace.variations.find(value => value.id === variationId);
  const availableRates = workspace.rates.filter(value => !variation || value.currency === variation.currency);
  const draftTotal = lines.reduce((sum, line) => sum + (availableRates.find(rate => rate.id === line.rateId)?.unitRate ?? 0) * (line.quantity || 0), 0);
  const requestId = (key: string) => requests.current[key] ?? (requests.current[key] = crypto.randomUUID());
  const complete = (key: string) => { delete requests.current[key]; };

  const load = useCallback(async (preferred?: string) => {
    setLoading(true);
    try { const value = await service.workspace(projectId, external); setWorkspace(value); setSelectedId(preferred ?? value.sheets[0]?.id ?? ''); }
    catch (error) { toast.error(error instanceof Error ? error.message : 'Daywork sheets could not be loaded.'); }
    finally { setLoading(false); }
  }, [external, projectId]);
  useEffect(() => { if (canRead) void load(); }, [canRead, load]);

  const select = (value: DayworkSheet) => {
    setSelectedId(value.id); setVariationId(value.variationOrderId); setWorkDate(value.workDate.slice(0, 10));
    setLocation(value.workLocation); setDescription(value.description);
    setLines(value.lines.map(line => ({ key: line.id, rateId: line.rateLibraryRateId, quantity: line.quantity, note: line.note ?? '' })));
  };
  const reset = () => { setSelectedId(''); setVariationId(''); setWorkDate(new Date().toISOString().slice(0, 10)); setLocation(''); setDescription(''); setLines([newLine()]); setReason(''); };
  const editable = external && (!selected || ['Draft', 'Rejected'].includes(selected.status));

  const save = async () => {
    if (!editable || !variationId || location.trim().length < 2 || description.trim().length < 10 || !lines.length || lines.some(line => !line.rateId || line.quantity <= 0)) {
      toast.error('Select a controlled record/rate and complete the work date, location, description and positive quantities.'); return;
    }
    if (new Set(lines.map(line => line.rateId)).size !== lines.length) { toast.error('Each controlled rate may appear only once.'); return; }
    const key = `save:${selected?.id ?? 'new'}:${variationId}:${workDate}:${location}:${description}:${JSON.stringify(lines)}`;
    setBusy(true);
    try {
      const value = await service.saveExternal(projectId, { id: selected?.id ?? null, clientRequestId: requestId(key), variationOrderId: variationId,
        workDate: new Date(`${workDate}T00:00:00Z`).toISOString(), workLocation: location.trim(), description: description.trim(),
        rowVersion: selected?.rowVersion ?? null, lines: lines.map(line => ({ rateLibraryRateId: line.rateId, quantity: line.quantity, note: line.note.trim() || null })) });
      complete(key); toast.success('Daywork Draft saved.'); await load(value.id);
    } catch (error) { toast.error(error instanceof Error ? error.message : 'The daywork Draft could not be saved.'); }
    finally { setBusy(false); }
  };
  const upload = async () => {
    if (!selected || !evidenceFile || evidenceTitle.trim().length < 3) { toast.error('Select an editable sheet, evidence title and file.'); return; }
    const key = `evidence:${selected.id}:${evidenceFile.name}:${evidenceFile.size}:${evidenceTitle}`; setBusy(true);
    try { await service.uploadEvidence(projectId, selected.id, requestId(key), evidenceTitle.trim(), evidenceFile, external); complete(key); setEvidenceTitle(''); setEvidenceFile(null); toast.success('Clean evidence retained in the central DMS.'); await load(selected.id); }
    catch (error) { toast.error(error instanceof Error ? error.message : 'Evidence upload failed.'); }
    finally { setBusy(false); }
  };
  const act = async (kind: 'sign' | 'verify' | 'reject') => {
    if (!selected || reason.trim().length < 5) { toast.error('Enter an action reason of at least 5 characters.'); return; }
    const key = `${kind}:${selected.id}:${selected.rowVersion}:${reason}`;
    const request = { clientRequestId: requestId(key), rowVersion: selected.rowVersion, reason: reason.trim() }; setBusy(true);
    try { const value = kind === 'sign' ? await service.signExternal(projectId, selected.id, request) : await service.verify(selected.id, kind === 'verify', request); complete(key); setReason(''); toast.success(`Daywork sheet ${kind === 'sign' ? 'signed' : kind === 'verify' ? 'verified' : 'rejected'}.`); await load(value.id); }
    catch (error) { toast.error(error instanceof Error ? error.message : 'The daywork action failed.'); }
    finally { setBusy(false); }
  };
  const download = async (evidenceId: string, fileName: string) => {
    if (!selected) return;
    try { const blob = await service.evidenceContent(projectId, selected.id, evidenceId, external); const url = URL.createObjectURL(blob); const anchor = document.createElement('a'); anchor.href = url; anchor.download = fileName; anchor.click(); URL.revokeObjectURL(url); }
    catch (error) { toast.error(error instanceof Error ? error.message : 'Evidence could not be opened.'); }
  };
  if (!canRead) return null;

  return <Card data-testid={external ? 'qs-contractor-daywork-portal' : 'qs-daywork-workspace'}>
    <CardHeader className="pb-3"><div className="flex flex-wrap items-center justify-between gap-2"><div><CardTitle>Daywork & additional works</CardTitle><CardDescription>{external ? 'Record controlled labour, material and plant usage, attach evidence and sign.' : 'Verify contractor-signed detail and monitor certificate eligibility.'}</CardDescription></div><div className="flex gap-2">{external && <Button size="sm" variant="outline" onClick={reset}><Plus className="mr-2 h-4 w-4" />New sheet</Button>}<Button size="sm" variant="outline" onClick={() => void load(selectedId)} disabled={loading}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button></div></div></CardHeader>
    <CardContent className="space-y-4">
      <div className="flex flex-wrap gap-2">{workspace.sheets.map(value => <Button key={value.id} size="sm" variant={selectedId === value.id ? 'default' : 'outline'} onClick={() => select(value)}>{value.sheetNumber}<Badge className="ml-2" variant="secondary">{label(value.status)}</Badge></Button>)}</div>
      {external && <div className="space-y-3 rounded-md border p-4">
        <div className="grid gap-3 md:grid-cols-4"><div className="grid gap-2 md:col-span-2"><Label>Daywork / Additional Work record</Label><Select disabled={!editable} value={variationId} onValueChange={setVariationId}><SelectTrigger><SelectValue placeholder="Select governed record" /></SelectTrigger><SelectContent>{workspace.variations.filter(value => ['Draft', 'Rejected'].includes(value.status)).map(value => <SelectItem key={value.id} value={value.id}>{value.reference} · {label(value.type)} · {value.contractor}</SelectItem>)}</SelectContent></Select></div><div className="grid gap-2"><Label>Work date</Label><Input disabled={!editable} type="date" max={new Date().toISOString().slice(0, 10)} value={workDate} onChange={event => setWorkDate(event.target.value)} /></div><div className="grid gap-2"><Label>Work location</Label><Input disabled={!editable} value={location} onChange={event => setLocation(event.target.value)} /></div><div className="grid gap-2 md:col-span-4"><Label>Work description</Label><Textarea disabled={!editable} rows={2} value={description} onChange={event => setDescription(event.target.value)} /></div></div>
        <div className="space-y-2"><Label>Controlled rate lines</Label>{lines.map((line, index) => { const rate = availableRates.find(value => value.id === line.rateId); return <div key={line.key} className="grid gap-2 md:grid-cols-12"><div className="md:col-span-5"><Select disabled={!editable} value={line.rateId} onValueChange={value => setLines(current => current.map(item => item.key === line.key ? { ...item, rateId: value } : item))}><SelectTrigger><SelectValue placeholder="Select Published labour/material/plant rate" /></SelectTrigger><SelectContent>{availableRates.map(value => <SelectItem key={value.id} value={value.id}>{value.code} · {value.name} · {label(value.lineType)} · {money(value.unitRate, value.currency)}/{value.unit}</SelectItem>)}</SelectContent></Select></div><Input className="md:col-span-2" disabled={!editable} type="number" min="0.0001" step="0.0001" value={line.quantity} onChange={event => setLines(current => current.map(item => item.key === line.key ? { ...item, quantity: Number(event.target.value) } : item))} /><Input className="md:col-span-4" disabled={!editable} placeholder="Line note" value={line.note} onChange={event => setLines(current => current.map(item => item.key === line.key ? { ...item, note: event.target.value } : item))} /><Button className="md:col-span-1" disabled={!editable || lines.length === 1} variant="ghost" onClick={() => setLines(current => current.filter(item => item.key !== line.key))}><Trash2 className="h-4 w-4" /></Button>{rate && <p className="text-xs text-muted-foreground md:col-span-12">Line {index + 1}: {money(rate.unitRate * line.quantity, rate.currency)}</p>}</div>; })}</div>
        {editable && <div className="flex flex-wrap items-center justify-between gap-2"><Button variant="outline" size="sm" onClick={() => setLines(current => [...current, newLine()])}><Plus className="mr-2 h-4 w-4" />Add rate line</Button><div className="flex items-center gap-3"><strong>Total: {money(draftTotal, variation?.currency ?? 'GHS')}</strong><Button onClick={() => void save()} disabled={busy}><Save className="mr-2 h-4 w-4" />Save Draft</Button></div></div>}
      </div>}
      {selected && <div className="space-y-4 rounded-md border p-4"><div className="grid gap-3 md:grid-cols-5"><div><Label>Record</Label><p>{selected.variationReference} · {label(selected.variationType)}</p></div><div><Label>Contractor</Label><p>{selected.contractor}</p></div><div><Label>Work date</Label><p>{new Date(selected.workDate).toLocaleDateString()}</p></div><div><Label>Total</Label><p>{money(selected.totalAmount, selected.currency)}</p></div><div><Label>Certificate status</Label><p>{selected.certificateEligible ? 'Eligible after QS-0511 application' : 'Not eligible'}</p></div></div>
        <div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="border-b text-left"><th className="p-2">Type</th><th className="p-2">Controlled rate</th><th className="p-2">Quantity</th><th className="p-2">Rate</th><th className="p-2">Amount</th></tr></thead><tbody>{selected.lines.map(value => <tr key={value.id} className="border-b"><td className="p-2">{label(value.lineType)}</td><td className="p-2">{value.code} · {value.name}</td><td className="p-2">{value.quantity} {value.unit}</td><td className="p-2">{money(value.unitRate, selected.currency)}</td><td className="p-2">{money(value.amount, selected.currency)}</td></tr>)}</tbody></table></div>
        <div className="space-y-2"><Label>Central DMS evidence</Label>{selected.evidence.length ? selected.evidence.map(value => <div key={value.id} className="flex items-center justify-between rounded border p-2 text-sm"><span>{value.title} · {value.fileName}</span><Button size="sm" variant="ghost" onClick={() => void download(value.id, value.fileName)}><Download className="mr-2 h-4 w-4" />Open</Button></div>) : <p className="text-sm text-muted-foreground">No evidence attached.</p>}</div>
        {external && ['Draft', 'Rejected'].includes(selected.status) && <div className="grid gap-2 md:grid-cols-4"><Input placeholder="Evidence title" value={evidenceTitle} onChange={event => setEvidenceTitle(event.target.value)} /><Input className="md:col-span-2" type="file" onChange={event => setEvidenceFile(event.target.files?.[0] ?? null)} /><Button variant="outline" onClick={() => void upload()} disabled={busy}><FileUp className="mr-2 h-4 w-4" />Upload evidence</Button></div>}
        <div className="grid gap-2"><Label>Signature / verification reason</Label><Textarea value={reason} onChange={event => setReason(event.target.value)} /></div>
        <div className="flex flex-wrap justify-end gap-2">{external && ['Draft', 'Rejected'].includes(selected.status) && <Button onClick={() => void act('sign')} disabled={busy}><Send className="mr-2 h-4 w-4" />Sign & submit</Button>}{canManage && selected.status === 'ContractorSigned' && <><Button onClick={() => void act('verify')} disabled={busy}><CheckCircle2 className="mr-2 h-4 w-4" />Verify</Button><Button variant="destructive" onClick={() => void act('reject')} disabled={busy}><XCircle className="mr-2 h-4 w-4" />Reject</Button></>}</div>
      </div>}
      {!loading && !workspace.variations.length && <p className="text-sm text-muted-foreground">No governed Daywork or Additional Work variation is available for this project and contractor.</p>}
    </CardContent>
  </Card>;
}
