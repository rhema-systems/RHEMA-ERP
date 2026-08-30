'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { ClipboardPlus, RefreshCw, Send, ShieldX } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringMaintenanceIntakeService } from '@/services/civil-engineering-maintenance-intake.service';
import type { CivilEngineeringMaintenanceIntake, CivilEngineeringMaintenanceIntakeLookups } from '@/types/civil-engineering-maintenance-intake';

const managePermission = 'civil-engineering.maintenance.manage';
const readPermission = 'civil-engineering.workspace.read';
const none = '__none__';
const labels: Record<string, string> = {
  NewProjectDesign: 'New project design', ConstructionSupervision: 'Construction supervision', ScheduledMaintenance: 'Scheduled maintenance',
  BreakdownMaintenance: 'Breakdown maintenance', PermittingReview: 'Permitting review', AssetComplaintResolution: 'Company asset complaint',
  Project: 'Project', Maintenance: 'Maintenance', Estate: 'Estate', TenantComplaint: 'Tenant complaint', InternalInspection: 'Internal inspection',
  ManagementDirective: 'Management directive', Routine: 'Routine', Priority: 'Priority', Urgent: 'Urgent', Emergency: 'Emergency',
};
type Form = { workClassification: string; source: string; urgency: string; title: string; description: string; projectId: string; targetKind: 'maintenance' | 'estate'; targetId: string; scheduleId: string; complaintTicketId: string; requesterUserId: string; priorityLevelId: string; documentKey: string };
const blank: Form = { workClassification: 'BreakdownMaintenance', source: 'Maintenance', urgency: 'Priority', title: '', description: '', projectId: none, targetKind: 'maintenance', targetId: '', scheduleId: none, complaintTicketId: none, requesterUserId: '', priorityLevelId: '', documentKey: '' };
const errorText = (error: unknown, fallback: string) => {
  const value = error as { response?: { detail?: string; correlationId?: string }; message?: string };
  const message = value.response?.detail || value.message || fallback;
  return `${message}${value.response?.correlationId ? ` Reference: ${value.response.correlationId}` : ''}`;
};
const date = (value: string) => new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));

export default function CivilEngineeringMaintenanceIntakesPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canRead = hasPermission(readPermission);
  const canManage = hasPermission(managePermission);
  const [items, setItems] = useState<CivilEngineeringMaintenanceIntake[]>([]);
  const [lookups, setLookups] = useState<CivilEngineeringMaintenanceIntakeLookups>();
  const [form, setForm] = useState<Form>(blank);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const isScheduled = form.workClassification === 'ScheduledMaintenance';
  const isComplaint = form.workClassification === 'AssetComplaintResolution';
  const targets = form.targetKind === 'maintenance' ? lookups?.maintenanceAssets ?? [] : lookups?.buildingsOrProperties ?? [];
  const sourceOptions = isComplaint ? (lookups?.sources.filter((value) => value === 'TenantComplaint' || value === 'Estate') ?? []) : lookups?.sources ?? [];
  const document = useMemo(() => lookups?.documents.find((value) => `${value.centralDocumentRecordId}:${value.centralDocumentVersionId}` === form.documentKey), [form.documentKey, lookups]);

  const load = useCallback(async () => {
    if (!canRead && !canManage) { setLoading(false); return; }
    setLoading(true);
    const [intakes, context] = await Promise.allSettled([
      canRead ? civilEngineeringMaintenanceIntakeService.list() : Promise.resolve([]),
      canManage ? civilEngineeringMaintenanceIntakeService.lookups() : Promise.resolve(undefined),
    ]);
    if (intakes.status === 'fulfilled') setItems(intakes.value);
    else toast({ variant: 'destructive', title: 'Maintenance intake history could not be loaded', description: errorText(intakes.reason, 'Refresh and try again.') });
    if (context.status === 'fulfilled' && context.value) setLookups(context.value);
    else setLookups(undefined);
    setLoading(false);
  }, [canManage, canRead, toast]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    if (!lookups) return;
    setForm((current) => ({
      ...current,
      workClassification: lookups.workClassifications.includes(current.workClassification) ? current.workClassification : (lookups.workClassifications[0] || 'BreakdownMaintenance'),
      source: lookups.sources.includes(current.source) ? current.source : (lookups.sources[0] || 'Maintenance'), urgency: lookups.urgencies.includes(current.urgency) ? current.urgency : (lookups.urgencies[0] || 'Priority'),
      requesterUserId: current.requesterUserId || lookups.requesters[0]?.id || '', priorityLevelId: current.priorityLevelId || lookups.priorities[0]?.id || '',
      documentKey: current.documentKey || (lookups.documents[0] ? `${lookups.documents[0].centralDocumentRecordId}:${lookups.documents[0].centralDocumentVersionId}` : ''),
    }));
  }, [lookups]);

  const updateClassification = (workClassification: string) => setForm((current) => ({ ...current, workClassification, source: workClassification === 'ScheduledMaintenance' ? 'Maintenance' : workClassification === 'AssetComplaintResolution' ? (lookups?.sources.includes('TenantComplaint') ? 'TenantComplaint' : lookups?.sources.includes('Estate') ? 'Estate' : current.source) : current.source, targetKind: workClassification === 'ScheduledMaintenance' ? 'maintenance' : current.targetKind, targetId: workClassification === 'ScheduledMaintenance' && current.targetKind !== 'maintenance' ? '' : current.targetId, scheduleId: workClassification === 'ScheduledMaintenance' ? current.scheduleId : none, complaintTicketId: workClassification === 'AssetComplaintResolution' ? current.complaintTicketId : none }));
  const create = async () => {
    if (!lookups || !document || !form.targetId || form.title.trim().length < 3 || form.description.trim().length < 3 || !form.requesterUserId || !form.priorityLevelId || (isScheduled && form.scheduleId === none) || (isComplaint && form.complaintTicketId === none)) {
      toast({ variant: 'destructive', title: 'Complete the governed intake', description: 'Use the controlled asset or property, requester, priority and current Published DMS evidence selections. Scheduled and complaint intake also require their original source record.' });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringMaintenanceIntakeService.create({ clientRequestId: crypto.randomUUID(), workClassification: form.workClassification, source: form.source, urgency: form.urgency, title: form.title.trim(), description: form.description.trim(), projectId: form.projectId === none ? null : form.projectId, maintenanceAssetId: form.targetKind === 'maintenance' ? form.targetId : null, estateManagedAssetId: form.targetKind === 'estate' ? form.targetId : null, maintenanceScheduleId: isScheduled ? form.scheduleId : null, helpdeskTicketId: isComplaint ? form.complaintTicketId : null, requesterUserId: form.requesterUserId, priorityLevelId: form.priorityLevelId, centralDocumentRecordId: document.centralDocumentRecordId, centralDocumentVersionId: document.centralDocumentVersionId });
      setForm({ ...blank, workClassification: form.workClassification, source: isScheduled ? 'Maintenance' : isComplaint ? 'TenantComplaint' : form.source, urgency: form.urgency, requesterUserId: form.requesterUserId, priorityLevelId: form.priorityLevelId, documentKey: form.documentKey });
      toast({ title: 'Maintenance intake logged', description: 'The evidence-backed intake is frozen to the active Civil policy and ready for the assessment workflow.' });
      await load();
    } catch (error) {
      toast({ variant: 'destructive', title: 'Maintenance intake was not logged', description: errorText(error, 'Check the current Civil policy, workflow, DMS evidence, and selected source record.') });
    } finally { setSaving(false); }
  };

  if (!canRead && !canManage) return <Alert><ShieldX className="h-4 w-4" /><AlertTitle>Civil maintenance access required</AlertTitle><AlertDescription>You do not have access to Civil Engineering maintenance intake records.</AlertDescription></Alert>;
  return <div className="space-y-5"><div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><h1 className="flex items-center gap-2 text-2xl font-semibold"><ClipboardPlus className="h-6 w-6" />Civil maintenance intake</h1><p className="mt-1 text-sm text-muted-foreground">Log governed scheduled or breakdown maintenance and company asset complaints using existing Maintenance, Helpdesk, Estate and DMS records.</p></div><Button variant="outline" size="sm" onClick={() => void load()} disabled={loading || saving}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button></div>{canManage && !loading && !lookups ? <Alert><AlertTitle>Controlled intake configuration is unavailable</AlertTitle><AlertDescription>Registration requires an effective CIV-CFG-007 policy, its published workflow, and its active DMS evidence template.</AlertDescription></Alert> : null}{canManage && lookups ? <Card><CardHeader><CardTitle className="text-base">Log a controlled maintenance or complaint intake</CardTitle><CardDescription>Source records and evidence are selected from their authoritative modules; Civil does not create replacement work orders, schedules, complaint tickets, or files.</CardDescription></CardHeader><CardContent className="space-y-4"><div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4"><div className="space-y-1"><Label>Work classification</Label><Select value={form.workClassification} onValueChange={updateClassification}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{lookups.workClassifications.map((value) => <SelectItem key={value} value={value}>{labels[value] || value}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Controlled source</Label><Select value={form.source} disabled={isScheduled} onValueChange={(source) => setForm((current) => ({ ...current, source }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{sourceOptions.map((value) => <SelectItem key={value} value={value}>{labels[value] || value}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Urgency</Label><Select value={form.urgency} onValueChange={(urgency) => setForm((current) => ({ ...current, urgency }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{lookups.urgencies.map((value) => <SelectItem key={value} value={value}>{labels[value] || value}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Related project (optional)</Label><Select value={form.projectId} onValueChange={(projectId) => setForm((current) => ({ ...current, projectId }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value={none}>Not project-linked</SelectItem>{lookups.projects.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1 md:col-span-2"><Label>Intake title</Label><Input value={form.title} maxLength={300} onChange={(event) => setForm((current) => ({ ...current, title: event.target.value }))} placeholder="Concise maintenance issue or complaint" /></div><div className="space-y-1"><Label>Target type</Label><Select value={form.targetKind} disabled={isScheduled} onValueChange={(targetKind: 'maintenance' | 'estate') => setForm((current) => ({ ...current, targetKind, targetId: '', scheduleId: none }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="maintenance">Maintenance asset</SelectItem><SelectItem value="estate">Building / property</SelectItem></SelectContent></Select></div><div className="space-y-1"><Label>{form.targetKind === 'maintenance' ? 'Maintenance asset' : 'Building / property'}</Label><Select value={form.targetId} onValueChange={(targetId) => setForm((current) => ({ ...current, targetId, scheduleId: none }))}><SelectTrigger><SelectValue placeholder="Select controlled target" /></SelectTrigger><SelectContent>{targets.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Requester</Label><Select value={form.requesterUserId} onValueChange={(requesterUserId) => setForm((current) => ({ ...current, requesterUserId }))}><SelectTrigger><SelectValue placeholder="Select requester" /></SelectTrigger><SelectContent>{lookups.requesters.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Maintenance priority</Label><Select value={form.priorityLevelId} onValueChange={(priorityLevelId) => setForm((current) => ({ ...current, priorityLevelId }))}><SelectTrigger><SelectValue placeholder="Select priority" /></SelectTrigger><SelectContent>{lookups.priorities.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div>{isScheduled ? <div className="space-y-1 md:col-span-2"><Label>Originating active maintenance schedule</Label><Select value={form.scheduleId} onValueChange={(scheduleId) => setForm((current) => ({ ...current, scheduleId }))}><SelectTrigger><SelectValue placeholder="Select active schedule" /></SelectTrigger><SelectContent>{lookups.maintenanceSchedules.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div> : null}{isComplaint ? <div className="space-y-1 md:col-span-2"><Label>Authoritative Helpdesk complaint ticket</Label><Select value={form.complaintTicketId} onValueChange={(complaintTicketId) => setForm((current) => ({ ...current, complaintTicketId }))}><SelectTrigger><SelectValue placeholder="Select open complaint ticket" /></SelectTrigger><SelectContent>{lookups.complaintTickets.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div> : null}<div className="space-y-1 md:col-span-2"><Label>Current Published DMS intake evidence</Label><Select value={form.documentKey} onValueChange={(documentKey) => setForm((current) => ({ ...current, documentKey }))}><SelectTrigger><SelectValue placeholder="Select governed evidence" /></SelectTrigger><SelectContent>{lookups.documents.map((item) => <SelectItem key={item.centralDocumentVersionId} value={`${item.centralDocumentRecordId}:${item.centralDocumentVersionId}`}>{item.documentReference} · v{item.versionNumber} · {item.title}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1 md:col-span-2"><Label>Issue / complaint description</Label><Textarea rows={3} maxLength={4000} value={form.description} onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))} placeholder="Record the observed condition, impact and required assessment context." /></div></div><div className="flex justify-end"><Button disabled={saving || !lookups.documents.length} onClick={() => void create()}><Send className="mr-2 h-4 w-4" />Log intake</Button></div></CardContent></Card> : null}<Card><CardHeader><CardTitle className="text-base">Maintenance and complaint intake history</CardTitle><CardDescription>Tenant-scoped, immutable intake history. Assessment, costing and controlled job-card/work-order linkage follow in later Civil tasks.</CardDescription></CardHeader><CardContent className="space-y-3">{loading ? <p className="text-sm text-muted-foreground">Loading maintenance intake history…</p> : null}{!loading && !items.length ? <p className="rounded border border-dashed p-3 text-sm text-muted-foreground">No Civil maintenance or complaint intakes have been logged.</p> : null}{items.map((item) => <article key={item.id} className="rounded-lg border p-4"><div className="flex flex-col gap-2 md:flex-row md:items-start md:justify-between"><div><div className="flex flex-wrap items-center gap-2"><span className="font-medium">{item.intakeNumber}</span><Badge variant="outline">{labels[item.workClassification] || item.workClassification}</Badge><Badge variant="secondary">{item.status}</Badge></div><p className="mt-2 font-medium">{item.title}</p><p className="mt-1 whitespace-pre-wrap break-words text-sm text-muted-foreground">{item.description}</p><p className="mt-2 text-xs text-muted-foreground">{item.maintenanceAssetName || item.buildingOrPropertyName} · {item.priorityName} · {item.requesterName}{item.maintenanceScheduleName ? ` · ${item.maintenanceScheduleName}` : ''}{item.complaintTicketNumber ? ` · ${item.complaintTicketNumber}` : ''}</p></div><p className="shrink-0 text-xs text-muted-foreground">{date(item.createdAt)}</p></div></article>)}</CardContent></Card></div>;
}
