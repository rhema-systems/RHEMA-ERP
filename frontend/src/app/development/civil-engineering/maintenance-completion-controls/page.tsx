'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { BadgeCheck, ClipboardCheck, RefreshCw, ShieldX } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringMaintenanceCompletionControlService } from '@/services/civil-engineering-maintenance-completion-control.service';
import type { CivilEngineeringMaintenanceCompletionAction, CivilEngineeringMaintenanceCompletionControl, CivilEngineeringMaintenanceCompletionLookups } from '@/types/civil-engineering-maintenance-completion-control';

const readPermission = 'civil-engineering.workspace.read';
const managePermission = 'civil-engineering.maintenance.manage';
const none = '__none__';
const actionOptions: Array<{ value: CivilEngineeringMaintenanceCompletionAction; label: string }> = [
  { value: 'SubmitToHod', label: 'SCE submit to HOD / CE resubmit' },
  { value: 'ReturnToCivilEngineer', label: 'SCE return to Civil Engineer' },
  { value: 'ApproveCompletion', label: 'HOD approve completion' },
  { value: 'DirectInspection', label: 'HOD direct inspection' },
  { value: 'RecordInspectionPassed', label: 'SCE record inspection passed' },
  { value: 'RecordInspectionFailed', label: 'SCE record inspection failed' },
  { value: 'DirectPayment', label: 'HOD direct Finance payment review' },
  { value: 'Close', label: 'HOD close Civil work record' },
];
const blankCreate = () => ({ executionLinkId: '', completionSummary: '', documentKey: '' });
const blankProcess = () => ({ action: 'SubmitToHod' as CivilEngineeringMaintenanceCompletionAction, note: '', documentKey: none });
const errorText = (error: unknown, fallback: string) => {
  const value = error as { response?: { detail?: string; correlationId?: string }; message?: string };
  return `${value.response?.detail || value.message || fallback}${value.response?.correlationId ? ` Reference: ${value.response.correlationId}` : ''}`;
};
const documentKey = (recordId: string, versionId: string) => `${recordId}:${versionId}`;
const date = (value?: string | null) => value ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '—';

export default function CivilEngineeringMaintenanceCompletionControlsPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canRead = hasPermission(readPermission);
  const canManage = hasPermission(managePermission);
  const [lookups, setLookups] = useState<CivilEngineeringMaintenanceCompletionLookups>();
  const [items, setItems] = useState<CivilEngineeringMaintenanceCompletionControl[]>([]);
  const [createForm, setCreateForm] = useState(blankCreate);
  const [processForms, setProcessForms] = useState<Record<string, ReturnType<typeof blankProcess>>>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    if (!canRead && !canManage) { setLoading(false); return; }
    setLoading(true);
    const [records, options] = await Promise.allSettled([
      canRead ? civilEngineeringMaintenanceCompletionControlService.list() : Promise.resolve([]),
      canManage ? civilEngineeringMaintenanceCompletionControlService.lookups() : Promise.resolve(undefined),
    ]);
    if (records.status === 'fulfilled') setItems(records.value);
    else toast({ variant: 'destructive', title: 'Completion controls could not be loaded', description: errorText(records.reason, 'Refresh and try again.') });
    setLookups(options.status === 'fulfilled' ? options.value : undefined);
    setLoading(false);
  }, [canManage, canRead, toast]);

  useEffect(() => { void load(); }, [load]);
  const documentByKey = useMemo(() => new Map((lookups?.documents || []).map((value) => [documentKey(value.centralDocumentRecordId, value.centralDocumentVersionId), value])), [lookups]);
  const selectedCreateDocument = documentByKey.get(createForm.documentKey);
  const formFor = (id: string) => processForms[id] || blankProcess();
  const updateProcess = (id: string, patch: Partial<ReturnType<typeof blankProcess>>) => setProcessForms((current) => ({ ...current, [id]: { ...formFor(id), ...patch } }));

  const create = async () => {
    if (!createForm.executionLinkId || !createForm.completionSummary.trim() || !selectedCreateDocument) { toast({ variant: 'destructive', title: 'Complete the controlled completion report fields', description: 'Select the completed execution link, write the completion narrative and select its DMS evidence version.' }); return; }
    setSaving(true);
    try {
      await civilEngineeringMaintenanceCompletionControlService.create({ clientRequestId: crypto.randomUUID(), executionLinkId: createForm.executionLinkId, completionSummary: createForm.completionSummary.trim(), completionDocumentRecordId: selectedCreateDocument.centralDocumentRecordId, completionDocumentVersionId: selectedCreateDocument.centralDocumentVersionId });
      toast({ title: 'Completion report submitted to SCE', description: 'The report is now in the Civil review sequence. Maintenance work and Finance posting remain owner-controlled.' });
      setCreateForm(blankCreate()); await load();
    } catch (error) { toast({ variant: 'destructive', title: 'Completion report was not submitted', description: errorText(error, 'Check the synchronized completed work order, approved weekly report, assignment and DMS evidence.') }); }
    finally { setSaving(false); }
  };

  const process = async (item: CivilEngineeringMaintenanceCompletionControl) => {
    const form = formFor(item.id); const document = form.documentKey === none ? undefined : documentByKey.get(form.documentKey);
    setSaving(true);
    try {
      await civilEngineeringMaintenanceCompletionControlService.process(item.id, { clientRequestId: crypto.randomUUID(), rowVersion: item.rowVersion, action: form.action, note: form.note.trim() || null, centralDocumentRecordId: document?.centralDocumentRecordId || null, centralDocumentVersionId: document?.centralDocumentVersionId || null });
      toast({ title: 'Civil completion control updated', description: 'The next permitted role can continue the reviewed completion, inspection, Finance direction or closure flow.' });
      setProcessForms((current) => ({ ...current, [item.id]: blankProcess() })); await load();
    } catch (error) { toast({ variant: 'destructive', title: 'Completion action was not applied', description: errorText(error, 'The API validates the assigned role, current stage, evidence and owner state.') }); }
    finally { setSaving(false); }
  };

  if (!canRead && !canManage) return <Alert><ShieldX className="h-4 w-4" /><AlertTitle>Civil maintenance access required</AlertTitle><AlertDescription>You do not have access to Civil completion controls.</AlertDescription></Alert>;

  return <div className="space-y-5">
    <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><h1 className="flex items-center gap-2 text-2xl font-semibold"><BadgeCheck className="h-6 w-6" />Maintenance completion controls</h1><p className="mt-1 text-sm text-muted-foreground">Controlled Civil completion review, inspection outcome, Finance direction and work-level closure for completed Maintenance execution.</p></div><Button variant="outline" size="sm" onClick={() => void load()} disabled={loading || saving}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button></div>
    {canManage && lookups ? <Card><CardHeader><CardTitle className="text-base">Submit completion report</CardTitle><CardDescription>Only an assigned Civil Engineer can submit after Maintenance work is completed and an approved weekly supervision report exists.</CardDescription></CardHeader><CardContent className="grid gap-3 md:grid-cols-3"><div className="space-y-1"><Label>Completed execution link</Label><Select value={createForm.executionLinkId} onValueChange={(executionLinkId) => setCreateForm((value) => ({ ...value, executionLinkId }))}><SelectTrigger><SelectValue placeholder="Select completed work" /></SelectTrigger><SelectContent>{lookups.completedExecutionLinks.map((value) => <SelectItem key={value.id} value={value.id}>{value.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1 md:col-span-2"><Label>Completion report evidence</Label><Select value={createForm.documentKey} onValueChange={(documentKeyValue) => setCreateForm((value) => ({ ...value, documentKey: documentKeyValue }))}><SelectTrigger><SelectValue placeholder="Select current published DMS version" /></SelectTrigger><SelectContent>{lookups.documents.map((value) => <SelectItem key={documentKey(value.centralDocumentRecordId, value.centralDocumentVersionId)} value={documentKey(value.centralDocumentRecordId, value.centralDocumentVersionId)}>{value.documentReference} · {value.title} v{value.versionNumber}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1 md:col-span-2"><Label>Completion summary</Label><Textarea value={createForm.completionSummary} onChange={(event) => setCreateForm((value) => ({ ...value, completionSummary: event.target.value }))} rows={3} placeholder="Describe the completed scope, material result and outstanding conditions." /></div><div className="flex items-end"><Button disabled={saving} onClick={() => void create()}><ClipboardCheck className="mr-2 h-4 w-4" />Submit to SCE</Button></div></CardContent></Card> : null}
    <Card><CardHeader><CardTitle className="text-base">Completion direction register</CardTitle><CardDescription>Finance receives a controlled direction only; this page cannot create invoices, payments or journal entries.</CardDescription></CardHeader><CardContent className="space-y-4">{loading ? <p className="text-sm text-muted-foreground">Loading completion controls…</p> : null}{!loading && !items.length ? <p className="rounded border border-dashed p-3 text-sm text-muted-foreground">No completed Maintenance execution is awaiting a Civil completion report.</p> : null}{items.map((item) => { const form = formFor(item.id); return <article key={item.id} className="rounded-lg border p-4"><div className="flex flex-col gap-1"><div className="flex flex-wrap items-center gap-2"><span className="font-medium">{item.intakeNumber}</span><Badge variant="outline">{item.stage}</Badge><Badge variant="secondary">{item.status}</Badge></div><p className="text-sm text-muted-foreground">{item.projectLabel} · {item.maintenanceAssetLabel} · Work order {item.workOrderNumber || 'unavailable'} ({item.workOrderStatus || 'unavailable'})</p><p className="mt-2 text-sm">{item.completionSummary}</p><p className="mt-1 text-sm text-muted-foreground">Completion evidence: {item.completionEvidenceReference} · Submitted {date(item.completionReportedAt)} · Inspection: {item.inspectionStatus} · Finance direction: {item.paymentDirectionStatus}</p></div>{canManage && item.stage !== 'Closed' ? <div className="mt-4 grid gap-3 border-t pt-4 md:grid-cols-4"><div className="space-y-1"><Label>Permitted role action</Label><Select value={form.action} onValueChange={(action: CivilEngineeringMaintenanceCompletionAction) => updateProcess(item.id, { action })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{actionOptions.map((option) => <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1 md:col-span-2"><Label>Direction/outcome evidence</Label><Select value={form.documentKey} onValueChange={(documentKeyValue) => updateProcess(item.id, { documentKey: documentKeyValue })}><SelectTrigger><SelectValue placeholder="Select when the action requires evidence" /></SelectTrigger><SelectContent><SelectItem value={none}>No document selected</SelectItem>{lookups?.documents.map((value) => <SelectItem key={documentKey(value.centralDocumentRecordId, value.centralDocumentVersionId)} value={documentKey(value.centralDocumentRecordId, value.centralDocumentVersionId)}>{value.documentReference} · {value.title} v{value.versionNumber}</SelectItem>)}</SelectContent></Select></div><div className="flex items-end"><Button disabled={saving} onClick={() => void process(item)}>Apply action</Button></div><div className="space-y-1 md:col-span-4"><Label>Decision, direction or outcome note</Label><Textarea rows={2} value={form.note} onChange={(event) => updateProcess(item.id, { note: event.target.value })} placeholder="Required for returns, approvals, inspection outcomes, Finance direction and closure where the policy requires it." /></div></div> : null}{item.closedAt ? <p className="mt-3 text-sm text-emerald-700">Closed {date(item.closedAt)}</p> : null}</article>; })}</CardContent></Card>
  </div>;
}
