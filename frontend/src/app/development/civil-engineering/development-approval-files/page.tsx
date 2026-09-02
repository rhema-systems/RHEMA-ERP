'use client';

import { useCallback, useEffect, useState } from 'react';
import { Building2, ClipboardCheck, FilePlus2, RefreshCw, ShieldX } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringDevelopmentApprovalFileService } from '@/services/civil-engineering-development-approval-file.service';
import type { CivilEngineeringDevelopmentApprovalFile, CivilEngineeringDevelopmentApprovalLookups } from '@/types/civil-engineering-development-approval-file';

const managePermission = 'civil-engineering.permitting.manage';
const readPermission = 'civil-engineering.workspace.read';
const none = '__external__';
const blank = { applicantBusinessPartnerId: none, applicantName: '', projectId: '', estateManagedAssetId: '', applicationReference: '', dueDate: '', siteInspectionDueDate: '', evidenceVersionIds: [] as string[] };
const date = (value?: string | null) => value ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '—';
const errorText = (error: unknown, fallback: string) => {
  const value = error as { response?: { detail?: string; correlationId?: string }; message?: string };
  const detail = value.response?.detail || value.message || fallback;
  return `${detail}${value.response?.correlationId ? ` Reference: ${value.response.correlationId}` : ''}`;
};

export default function CivilEngineeringDevelopmentApprovalFilesPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canRead = hasPermission(readPermission);
  const canManage = hasPermission(managePermission);
  const [files, setFiles] = useState<CivilEngineeringDevelopmentApprovalFile[]>([]);
  const [lookups, setLookups] = useState<CivilEngineeringDevelopmentApprovalLookups>();
  const [form, setForm] = useState(blank);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [inspectionFile, setInspectionFile] = useState<CivilEngineeringDevelopmentApprovalFile>();
  const [inspectionAt, setInspectionAt] = useState('');
  const [inspectionEvidenceVersionIds, setInspectionEvidenceVersionIds] = useState<string[]>([]);

  const load = useCallback(async () => {
    if (!canRead && !canManage) { setLoading(false); return; }
    setLoading(true);
    const [history, context] = await Promise.allSettled([
      canRead ? civilEngineeringDevelopmentApprovalFileService.list() : Promise.resolve([]),
      canManage ? civilEngineeringDevelopmentApprovalFileService.lookups() : Promise.resolve(undefined),
    ]);
    if (history.status === 'fulfilled') setFiles(history.value);
    else toast({ variant: 'destructive', title: 'Development approval files could not be loaded', description: errorText(history.reason, 'Refresh and try again.') });
    if (context.status === 'fulfilled') setLookups(context.value);
    else setLookups(undefined);
    setLoading(false);
  }, [canManage, canRead, toast]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    if (!lookups) return;
    setForm((current) => ({ ...current, projectId: current.projectId || lookups.projects[0]?.id || '', estateManagedAssetId: current.estateManagedAssetId || lookups.properties[0]?.id || '' }));
  }, [lookups]);

  const evidence = (ids: string[]) => (lookups?.documents ?? []).filter((item) => ids.includes(item.centralDocumentVersionId)).map((item) => ({ centralDocumentRecordId: item.centralDocumentRecordId, centralDocumentVersionId: item.centralDocumentVersionId }));
  const toggleEvidence = (versionId: string, inspection = false) => {
    const update = (values: string[]) => values.includes(versionId) ? values.filter((item) => item !== versionId) : [...values, versionId];
    if (inspection) setInspectionEvidenceVersionIds(update); else setForm((current) => ({ ...current, evidenceVersionIds: update(current.evidenceVersionIds) }));
  };

  const create = async () => {
    if (!lookups || !form.projectId || !form.estateManagedAssetId || form.applicationReference.trim().length < 3 || !form.dueDate || !form.evidenceVersionIds.length || (form.applicantBusinessPartnerId === none && form.applicantName.trim().length < 2)) {
      toast({ variant: 'destructive', title: 'Complete the governed approval file', description: 'Choose the controlled project, property and current Published DMS documents. Select an approved business partner or enter an external applicant name.' });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringDevelopmentApprovalFileService.create({ clientRequestId: crypto.randomUUID(), applicantBusinessPartnerId: form.applicantBusinessPartnerId === none ? null : form.applicantBusinessPartnerId, applicantName: form.applicantBusinessPartnerId === none ? form.applicantName.trim() : null, projectId: form.projectId, estateManagedAssetId: form.estateManagedAssetId, applicationReference: form.applicationReference.trim(), dueDate: form.dueDate, siteInspectionDueDate: form.siteInspectionDueDate || null, applicationEvidence: evidence(form.evidenceVersionIds) });
      setForm({ ...blank, projectId: form.projectId, estateManagedAssetId: form.estateManagedAssetId });
      toast({ title: 'Development approval file opened', description: 'The Building Inspectorate file is frozen to the active Civil permitting and DMS policies.' });
      await load();
    } catch (error) {
      toast({ variant: 'destructive', title: 'Development approval file was not opened', description: errorText(error, 'Check the effective CIV-CFG-009/CIV-CFG-004 policies, permitting role, selected records and DMS documents.') });
    } finally { setSaving(false); }
  };

  const recordInspection = async () => {
    if (!inspectionFile || !inspectionAt || !inspectionEvidenceVersionIds.length) {
      toast({ variant: 'destructive', title: 'Site-inspection evidence is required', description: 'Select the inspection timestamp and at least one current Published DMS inspection document.' });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringDevelopmentApprovalFileService.recordSiteInspection(inspectionFile.id, { clientRequestId: crypto.randomUUID(), siteInspectedAt: new Date(inspectionAt).toISOString(), evidence: evidence(inspectionEvidenceVersionIds), rowVersion: inspectionFile.rowVersion });
      toast({ title: 'Site inspection recorded', description: 'The file remains with Building Inspectorate until the controlled CIV-0402 handoff is performed.' });
      setInspectionFile(undefined); setInspectionAt(''); setInspectionEvidenceVersionIds([]); await load();
    } catch (error) {
      toast({ variant: 'destructive', title: 'Site inspection was not recorded', description: errorText(error, 'Refresh the file and confirm its DMS evidence and row version.') });
    } finally { setSaving(false); }
  };

  if (!canRead && !canManage) return <Alert><ShieldX className="h-4 w-4" /><AlertTitle>Civil permitting access required</AlertTitle><AlertDescription>You do not have access to Civil development approval files.</AlertDescription></Alert>;

  return <div className="space-y-5">
    <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><h1 className="flex items-center gap-2 text-2xl font-semibold"><Building2 className="h-6 w-6" />Development approval files</h1><p className="mt-1 text-sm text-muted-foreground">Building Inspectorate register for a development application, linked property, current-published DMS evidence and site inspection.</p></div><Button variant="outline" size="sm" onClick={() => void load()} disabled={loading || saving}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button></div>
    {canManage && !loading && !lookups ? <Alert><AlertTitle>Permitting configuration is unavailable</AlertTitle><AlertDescription>Opening a file requires effective CIV-CFG-009 permitting controls, CIV-CFG-004 DMS controls, a Published permitting workflow and a configured handoff role.</AlertDescription></Alert> : null}
    {canManage && lookups ? <Card><CardHeader><CardTitle className="text-base">Open a Building Inspectorate approval file</CardTitle><CardDescription>Project, property, approved applicant and attachments are controlled selections. An external applicant name is permitted only where no registered business partner exists.</CardDescription></CardHeader><CardContent className="space-y-4"><div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4"><div className="space-y-1"><Label>Applicant</Label><Select value={form.applicantBusinessPartnerId} onValueChange={(applicantBusinessPartnerId) => setForm((current) => ({ ...current, applicantBusinessPartnerId }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value={none}>External applicant</SelectItem>{lookups.applicants.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div>{form.applicantBusinessPartnerId === none ? <div className="space-y-1"><Label>External applicant name</Label><Input value={form.applicantName} maxLength={250} placeholder="Applicant not yet registered" onChange={(event) => setForm((current) => ({ ...current, applicantName: event.target.value }))} /></div> : <div className="space-y-1"><Label>File reference</Label><Input value={form.applicationReference} maxLength={120} placeholder="Development application reference" onChange={(event) => setForm((current) => ({ ...current, applicationReference: event.target.value }))} /></div>}<div className="space-y-1"><Label>Project</Label><Select value={form.projectId} onValueChange={(projectId) => setForm((current) => ({ ...current, projectId }))}><SelectTrigger><SelectValue placeholder="Select project" /></SelectTrigger><SelectContent>{lookups.projects.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Property / building</Label><Select value={form.estateManagedAssetId} onValueChange={(estateManagedAssetId) => setForm((current) => ({ ...current, estateManagedAssetId }))}><SelectTrigger><SelectValue placeholder="Select property" /></SelectTrigger><SelectContent>{lookups.properties.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div>{form.applicantBusinessPartnerId === none ? <div className="space-y-1"><Label>File reference</Label><Input value={form.applicationReference} maxLength={120} placeholder="Development application reference" onChange={(event) => setForm((current) => ({ ...current, applicationReference: event.target.value }))} /></div> : null}<div className="space-y-1"><Label>File due date</Label><Input type="date" value={form.dueDate} onChange={(event) => setForm((current) => ({ ...current, dueDate: event.target.value }))} /></div><div className="space-y-1"><Label>Planned site inspection (optional)</Label><Input type="date" value={form.siteInspectionDueDate} onChange={(event) => setForm((current) => ({ ...current, siteInspectionDueDate: event.target.value }))} /></div></div><div className="space-y-2"><Label>Current Published DMS application package</Label><div className="max-h-48 space-y-2 overflow-auto rounded-md border p-3">{lookups.documents.map((item) => <label key={item.centralDocumentVersionId} className="flex items-start gap-2 text-sm"><Checkbox checked={form.evidenceVersionIds.includes(item.centralDocumentVersionId)} onCheckedChange={() => toggleEvidence(item.centralDocumentVersionId)} /><span>{item.documentReference} · v{item.versionNumber} · {item.title}</span></label>)}{!lookups.documents.length ? <p className="text-sm text-muted-foreground">No current Published document is available under the active CIV-CFG-004 DMS template.</p> : null}</div></div><div className="flex justify-end"><Button disabled={saving || !lookups.documents.length} onClick={() => void create()}><FilePlus2 className="mr-2 h-4 w-4" />Open approval file</Button></div></CardContent></Card> : null}
    <Card><CardHeader><CardTitle className="text-base">Development approval file register</CardTitle><CardDescription>File handoff, SCE recommendation and HOD decision are separate controlled Civil stages. This register does not create a competing Workflow or document repository.</CardDescription></CardHeader><CardContent className="space-y-3">{loading ? <p className="text-sm text-muted-foreground">Loading development approval files…</p> : null}{!loading && !files.length ? <p className="rounded border border-dashed p-3 text-sm text-muted-foreground">No development approval file has been opened.</p> : null}{files.map((file) => <article key={file.id} className="rounded-lg border p-4"><div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between"><div className="min-w-0"><div className="flex flex-wrap items-center gap-2"><span className="font-medium">{file.fileNumber}</span><Badge variant="outline">{file.status}</Badge><Badge variant="secondary">{file.currentSection}</Badge></div><p className="mt-2 font-medium break-words">{file.applicationReference} · {file.applicantName}</p><p className="mt-1 text-sm text-muted-foreground">{file.projectLabel} · {file.propertyLabel}</p><p className="mt-2 text-xs text-muted-foreground">Due {date(file.dueDate)} · Site inspection {file.siteInspectedAt ? date(file.siteInspectedAt) : file.siteInspectionDueDate ? `planned ${date(file.siteInspectionDueDate)}` : 'not yet planned'} · {file.evidence.length} DMS document(s)</p></div>{canManage && file.status !== 'SiteInspectionCompleted' ? <Button size="sm" variant="outline" onClick={() => { setInspectionFile(file); setInspectionAt(''); setInspectionEvidenceVersionIds([]); }}><ClipboardCheck className="mr-2 h-4 w-4" />Record site inspection</Button> : null}</div></article>)}</CardContent></Card>
    <Dialog open={!!inspectionFile} onOpenChange={(open) => { if (!open) setInspectionFile(undefined); }}><DialogContent className="max-w-2xl"><DialogHeader><DialogTitle>Record site inspection</DialogTitle><DialogDescription>Attach current Published central-DMS inspection evidence. The file remains with Building Inspectorate; inter-section handoff is not implied by this action.</DialogDescription></DialogHeader><div className="space-y-4"><div className="space-y-1"><Label>Inspection date and time</Label><Input type="datetime-local" value={inspectionAt} onChange={(event) => setInspectionAt(event.target.value)} /></div><div className="space-y-2"><Label>Current Published DMS inspection evidence</Label><div className="max-h-52 space-y-2 overflow-auto rounded-md border p-3">{(lookups?.documents ?? []).map((item) => <label key={item.centralDocumentVersionId} className="flex items-start gap-2 text-sm"><Checkbox checked={inspectionEvidenceVersionIds.includes(item.centralDocumentVersionId)} onCheckedChange={() => toggleEvidence(item.centralDocumentVersionId, true)} /><span>{item.documentReference} · v{item.versionNumber} · {item.title}</span></label>)}</div></div></div><DialogFooter><Button variant="outline" onClick={() => setInspectionFile(undefined)} disabled={saving}>Cancel</Button><Button onClick={() => void recordInspection()} disabled={saving}><ClipboardCheck className="mr-2 h-4 w-4" />Save inspection</Button></DialogFooter></DialogContent></Dialog>
  </div>;
}
