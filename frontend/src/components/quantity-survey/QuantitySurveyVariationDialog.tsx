'use client';

import { useCallback, useMemo, useRef, useState } from 'react';
import { CheckCircle2, FileUp, Plus, RefreshCw, Save, Send, Trash2, XCircle } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { quantitySurveyVariationService as service, type GovernedVariation, type SaveVariationLine, type VariationSourceType, type VariationWorkspace } from '@/services/quantity-survey-variation.service';

type Props = { projectId: string };
type DraftLine = SaveVariationLine & { key: string };
const emptyWorkspace: VariationWorkspace = { contracts: [], siteInstructions: [], changeRequests: [], boqLines: [], variations: [] };
const types = ['ScopeChange', 'QuantityAdjustment', 'ProvisionalSum', 'RateChange', 'Omission', 'Daywork', 'AdditionalWork', 'SiteInstruction', 'ChangeOrder', 'Other'];
const sources: Array<{ value: VariationSourceType; label: string }> = [
  { value: 'siteInstruction', label: 'Site instruction' }, { value: 'changeRequest', label: 'Approved change request' },
  { value: 'directVariation', label: 'Direct variation' }, { value: 'changeOrder', label: 'Change order' },
];
const newLine = (): DraftLine => ({ key: crypto.randomUUID(), projectBoqVersionLineId: '', quantityChange: 0, unitRate: null, valuationReason: '' });
const money = (value: number, currency = 'GHS') => new Intl.NumberFormat(undefined, { style: 'currency', currency, maximumFractionDigits: 2 }).format(value || 0);
const recordTypeForSource = (source: VariationSourceType, current: string) => source === 'siteInstruction' ? 'SiteInstruction' : source === 'changeOrder' ? 'ChangeOrder' : ['SiteInstruction', 'ChangeOrder'].includes(current) ? 'ScopeChange' : current;

export function QuantitySurveyVariationDialog({ projectId }: Props) {
  const { hasPermission } = useAuth();
  const canRead = hasPermission('quantity-survey.workspace.read');
  const canManage = hasPermission('quantity-survey.variations.manage');
  const canApprove = hasPermission('quantity-survey.transactions.approve');
  const requestIds = useRef<Record<string, string>>({});
  const [open, setOpen] = useState(false); const [loading, setLoading] = useState(false); const [working, setWorking] = useState(false);
  const [workspace, setWorkspace] = useState(emptyWorkspace); const [selectedId, setSelectedId] = useState('');
  const [contractId, setContractId] = useState(''); const [sourceType, setSourceType] = useState<VariationSourceType>('siteInstruction');
  const [sourceId, setSourceId] = useState(''); const [title, setTitle] = useState(''); const [reason, setReason] = useState('');
  const [variationType, setVariationType] = useState(types[0]); const [scheduleDays, setScheduleDays] = useState(0); const [lines, setLines] = useState<DraftLine[]>([newLine()]);
  const [actionReason, setActionReason] = useState(''); const [evidenceTitle, setEvidenceTitle] = useState(''); const [evidenceFile, setEvidenceFile] = useState<File | null>(null);
  const selected = useMemo(() => workspace.variations.find(value => value.id === selectedId), [selectedId, workspace.variations]);
  const selectedContract = workspace.contracts.find(value => value.id === contractId);
  const sourceOptions = sourceType === 'siteInstruction' ? workspace.siteInstructions : sourceType === 'changeRequest' ? workspace.changeRequests : [];
  const nextRequest = (key: string) => requestIds.current[key] ?? (requestIds.current[key] = crypto.randomUUID());
  const completeRequest = (key: string) => { delete requestIds.current[key]; };

  const load = useCallback(async (preferred?: string) => {
    setLoading(true); try { const value = await service.workspace(projectId); setWorkspace(value); setSelectedId(preferred ?? value.variations[0]?.id ?? ''); }
    catch (error) { toast.error(error instanceof Error ? error.message : 'Unable to load governed variations.'); } finally { setLoading(false); }
  }, [projectId]);
  const openChanged = (value: boolean) => { setOpen(value); if (value && canRead) void load(); };

  const selectVariation = (value: GovernedVariation) => {
    setSelectedId(value.id); setContractId(value.contractId); setSourceType(value.sourceType); setSourceId(value.siteInstructionId ?? value.changeRequestId ?? '');
    setTitle(value.title); setVariationType(value.variationType); setScheduleDays(value.scheduleImpactDays); setReason('Amend governed variation valuation.');
    setLines(value.lines.map(line => ({ key: line.id, projectBoqVersionLineId: line.projectBoqVersionLineId, quantityChange: line.quantityChange, unitRate: line.unitRate, valuationReason: line.valuationReason })));
  };
  const updateLine = (key: string, patch: Partial<DraftLine>) => setLines(current => current.map(line => line.key === key ? { ...line, ...patch } : line));
  const save = async () => {
    const versionIds = [...new Set(lines.map(line => workspace.boqLines.find(option => option.id === line.projectBoqVersionLineId)?.versionId).filter(Boolean))];
    if (!contractId || !title.trim() || reason.trim().length < 10 || lines.some(line => !line.projectBoqVersionLineId || !line.quantityChange || line.valuationReason.trim().length < 5) || versionIds.length !== 1) { toast.error('Select one contract, controlled BoQ lines from one Approved version, non-zero quantities, and complete the reasons.'); return; }
    const selectedVersionId = versionIds[0];
    if (!selectedVersionId) return;
    const key = `save:${selected?.id ?? 'new'}:${contractId}:${sourceType}:${sourceId}:${title}:${reason}:${variationType}:${scheduleDays}:${JSON.stringify(lines)}`;
    setWorking(true); try {
      const value = await service.save(projectId, { id: selected?.id ?? null, clientRequestId: nextRequest(key), contractId, approvedBoqVersionId: selectedVersionId, sourceType,
        siteInstructionId: sourceType === 'siteInstruction' ? sourceId : null, changeRequestId: sourceType === 'changeRequest' ? sourceId : null,
        title: title.trim(), reason: reason.trim(), variationType, scheduleImpactDays: scheduleDays, rowVersion: selected?.rowVersion, lines: lines.map(({ projectBoqVersionLineId, quantityChange, unitRate, valuationReason }) => ({ projectBoqVersionLineId, quantityChange, unitRate, valuationReason: valuationReason.trim() })) });
      completeRequest(key); toast.success('Variation valuation saved.'); await load(value.id);
    } catch (error) { toast.error(error instanceof Error ? error.message : 'Unable to save variation.'); } finally { setWorking(false); }
  };
  const upload = async () => {
    if (!selected || !evidenceFile || evidenceTitle.trim().length < 3) { toast.error('Select a Draft variation, evidence title and file.'); return; }
    const key = `evidence:${selected.id}:${evidenceTitle}:${evidenceFile.name}:${evidenceFile.size}`; setWorking(true);
    try { await service.uploadEvidence(selected.id, nextRequest(key), evidenceTitle.trim(), evidenceFile); completeRequest(key); setEvidenceFile(null); setEvidenceTitle(''); toast.success('Clean evidence saved in the central DMS.'); await load(selected.id); }
    catch (error) { toast.error(error instanceof Error ? error.message : 'Unable to upload evidence.'); } finally { setWorking(false); }
  };
  const action = async (kind: 'submit' | 'approve' | 'reject' | 'apply') => {
    if (!selected || actionReason.trim().length < 5) { toast.error('Enter an action reason of at least 5 characters.'); return; }
    const key = `${kind}:${selected.id}:${selected.rowVersion}:${actionReason}`; setWorking(true);
    try { const value = await service[kind](selected.id, { clientRequestId: nextRequest(key), rowVersion: selected.rowVersion, reason: actionReason.trim() }); completeRequest(key); setActionReason(''); toast.success(`Variation ${kind} completed.`); await load(value.id); }
    catch (error) { toast.error(error instanceof Error ? error.message : `Unable to ${kind} variation.`); } finally { setWorking(false); }
  };
  if (!canRead) return null;
  return <Dialog open={open} onOpenChange={openChanged}>
    <DialogTrigger asChild><Button><Plus className="mr-2 h-4 w-4" />Governed variation</Button></DialogTrigger>
    <DialogContent className="max-h-[92vh] max-w-6xl overflow-y-auto">
      <DialogHeader><DialogTitle>Variation and change-order control</DialogTitle></DialogHeader>
      <div className="flex flex-wrap gap-2"><Button variant="outline" size="sm" onClick={() => void load(selectedId)} disabled={loading}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>{workspace.variations.map(value => <Button key={value.id} variant={selectedId === value.id ? 'default' : 'outline'} size="sm" onClick={() => selectVariation(value)}>{value.referenceNumber} <Badge className="ml-2" variant="secondary">{value.status}</Badge></Button>)}</div>
      <div className="grid gap-4 md:grid-cols-4">
        <div className="grid gap-2 md:col-span-2"><Label>Active Works contract</Label><Select value={contractId} onValueChange={setContractId}><SelectTrigger><SelectValue placeholder="Select contract" /></SelectTrigger><SelectContent>{workspace.contracts.map(value => <SelectItem key={value.id} value={value.id}>{value.number} · {value.contractor}</SelectItem>)}</SelectContent></Select></div>
        <div className="grid gap-2"><Label>Source type</Label><Select value={sourceType} onValueChange={value => { const next = value as VariationSourceType; setSourceType(next); setVariationType(current => recordTypeForSource(next, current)); setSourceId(''); }}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{sources.map(value => <SelectItem key={value.value} value={value.value}>{value.label}</SelectItem>)}</SelectContent></Select></div>
        <div className="grid gap-2"><Label>Source record</Label>{sourceOptions.length ? <Select value={sourceId} onValueChange={setSourceId}><SelectTrigger><SelectValue placeholder="Select governed source" /></SelectTrigger><SelectContent>{sourceOptions.map(value => <SelectItem key={value.id} value={value.id}>{value.reference} · {value.title}</SelectItem>)}</SelectContent></Select> : <Input value="No source record required" disabled />}</div>
        <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={title} onChange={event => setTitle(event.target.value)} /></div>
        <div className="grid gap-2"><Label>Commercial record type</Label><Select value={variationType} onValueChange={setVariationType}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{types.map(value => <SelectItem key={value} value={value}>{value.replace(/([A-Z])/g, ' $1').trim()}</SelectItem>)}</SelectContent></Select></div>
        <div className="grid gap-2"><Label>Schedule impact (days)</Label><Input type="number" value={scheduleDays} onChange={event => setScheduleDays(Number(event.target.value))} /></div>
        <div className="grid gap-2 md:col-span-4"><Label>Reason and scope</Label><Textarea value={reason} onChange={event => setReason(event.target.value)} /></div>
      </div>
      <div className="space-y-2"><div className="flex items-center justify-between"><h3 className="font-semibold">Valuation lines</h3><Button size="sm" variant="outline" onClick={() => setLines(current => [...current, newLine()])}><Plus className="mr-2 h-4 w-4" />Line</Button></div>
        {lines.map(line => { const option = workspace.boqLines.find(value => value.id === line.projectBoqVersionLineId); return <div key={line.key} className="grid gap-2 rounded-md border p-3 md:grid-cols-12">
          <div className="md:col-span-5"><Label>Approved BoQ line</Label><Select value={line.projectBoqVersionLineId} onValueChange={value => { const source = workspace.boqLines.find(item => item.id === value); updateLine(line.key, { projectBoqVersionLineId: value, unitRate: source?.unitRate ?? null }); }}><SelectTrigger><SelectValue placeholder="Select approved line" /></SelectTrigger><SelectContent>{workspace.boqLines.map(value => <SelectItem key={value.id} value={value.id}>{value.reference} · {value.description}</SelectItem>)}</SelectContent></Select></div>
          <div className="md:col-span-2"><Label>Quantity change</Label><Input type="number" step="0.0001" value={line.quantityChange || ''} onChange={event => updateLine(line.key, { quantityChange: Number(event.target.value) })} /></div>
          <div className="md:col-span-2"><Label>Unit rate</Label><Input type="number" step="0.000001" value={line.unitRate ?? ''} onChange={event => updateLine(line.key, { unitRate: event.target.value ? Number(event.target.value) : null })} /></div>
          <div className="md:col-span-2"><Label>Amount</Label><Input value={money((line.quantityChange || 0) * (line.unitRate ?? option?.unitRate ?? 0), selectedContract?.currency)} disabled /></div>
          <div className="flex items-end"><Button variant="ghost" size="icon" onClick={() => setLines(current => current.filter(value => value.key !== line.key))} disabled={lines.length === 1}><Trash2 className="h-4 w-4" /></Button></div>
          <div className="md:col-span-12"><Label>Line valuation reason</Label><Input value={line.valuationReason} onChange={event => updateLine(line.key, { valuationReason: event.target.value })} /></div>
        </div>; })}
      </div>
      {canManage && <div className="flex justify-end"><Button onClick={() => void save()} disabled={working}><Save className="mr-2 h-4 w-4" />Save Draft</Button></div>}
      {selected && <div className="grid gap-4 rounded-md border p-4 md:grid-cols-4"><div><Label>Valued amount</Label><p className="font-semibold">{money(selected.valuedAmount, selected.currency)}</p></div><div><Label>Original contract sum</Label><p>{money(selected.originalContractSum, selected.currency)}</p></div><div><Label>Revised sum after approval</Label><p>{selected.revisedContractSum == null ? 'Pending approval' : money(selected.revisedContractSum, selected.currency)}</p></div><div><Label>Evidence</Label><p>{selected.evidence.length} central DMS file(s)</p></div>
        <div><Label>Downstream application</Label><p><Badge variant={selected.downstreamApplicationStatus === 'Applied' ? 'default' : 'secondary'}>{selected.downstreamApplicationStatus}</Badge></p></div>
        <div><Label>Revised BoQ</Label><p>{selected.revisedBoqVersionNumber ? `v${selected.revisedBoqVersionNumber} · ${selected.revisedBoqStatus}` : 'Not created'}</p></div>
        <div><Label>Budget / forecast</Label><p>{selected.budgetRevisionId ? 'Budget updated' : 'Budget not configured'} · {selected.forecastVersionId ? 'Forecast updated' : 'Forecast not configured'}</p></div>
        <div><Label>Next certificate</Label><p>{selected.certificateEligible ? 'Eligible on revised approved BoQ' : 'Await revised BoQ approval'}</p></div>
        {canManage && ['Draft', 'Rejected'].includes(selected.status) && <div className="md:col-span-4 grid gap-2 md:grid-cols-4"><Input placeholder="Evidence title" value={evidenceTitle} onChange={event => setEvidenceTitle(event.target.value)} /><Input type="file" onChange={event => setEvidenceFile(event.target.files?.[0] ?? null)} /><Button variant="outline" onClick={() => void upload()} disabled={working}><FileUp className="mr-2 h-4 w-4" />Upload evidence</Button></div>}
        <div className="md:col-span-4"><Label>Action reason</Label><Textarea value={actionReason} onChange={event => setActionReason(event.target.value)} /></div>
        <div className="md:col-span-4 flex flex-wrap justify-end gap-2">{canManage && ['Draft', 'Rejected'].includes(selected.status) && <Button onClick={() => void action('submit')} disabled={working}><Send className="mr-2 h-4 w-4" />Submit</Button>}{canApprove && selected.status === 'PendingApproval' && <><Button onClick={() => void action('approve')} disabled={working}><CheckCircle2 className="mr-2 h-4 w-4" />Approve and apply</Button><Button variant="destructive" onClick={() => void action('reject')} disabled={working}><XCircle className="mr-2 h-4 w-4" />Reject</Button></>}{canApprove && selected.status === 'Approved' && selected.downstreamApplicationStatus === 'NotApplied' && <Button onClick={() => void action('apply')} disabled={working}><CheckCircle2 className="mr-2 h-4 w-4" />Retry downstream application</Button>}</div>
      </div>}
    </DialogContent>
  </Dialog>;
}
