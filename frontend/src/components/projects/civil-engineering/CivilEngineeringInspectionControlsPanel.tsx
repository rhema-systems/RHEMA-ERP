'use client';

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { CheckCircle2, ClipboardCheck, FileWarning, RefreshCw, ShieldCheck, Wrench, XCircle } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringInspectionControlService } from '@/services/civil-engineering-inspection-control.service';
import { CivilEngineeringInspectionAction, type CivilEngineeringInspectionControl, type CivilEngineeringInspectionLookups } from '@/types/civil-engineering-inspection-control';

const workspacePermission = 'civil-engineering.workspace.read';
const supervisionPermission = 'civil-engineering.supervision.manage';
const formatDate = (value?: string | null) => value ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : 'Not recorded';
const errorText = (error: unknown, fallback: string) => {
  const value = error as { response?: { detail?: string; correlationId?: string }; message?: string };
  return `${value.response?.detail || value.message || fallback}${value.response?.correlationId ? ` Reference: ${value.response.correlationId}` : ''}`;
};
type CreateForm = { planningGisValidationId: string; inspectorUserId: string; scheduledAt: string; purpose: string; documentKey: string };
const blank: CreateForm = { planningGisValidationId: '', inspectorUserId: '', scheduledAt: '', purpose: '', documentKey: '' };
type Props = { projectId: string };

export function CivilEngineeringInspectionControlsPanel({ projectId }: Props) {
  const { toast } = useToast();
  const { hasPermission } = useAuth();
  const canRead = hasPermission(workspacePermission);
  const canManage = hasPermission(supervisionPermission);
  const [items, setItems] = useState<CivilEngineeringInspectionControl[]>([]);
  const [lookups, setLookups] = useState<CivilEngineeringInspectionLookups>();
  const [form, setForm] = useState<CreateForm>(blank);
  const [findings, setFindings] = useState<Record<string, string>>({});
  const [reviewComments, setReviewComments] = useState<Record<string, string>>({});
  const [corrective, setCorrective] = useState<Record<string, string>>({});
  const [reinspectors, setReinspectors] = useState<Record<string, string>>({});
  const [documents, setDocuments] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    if (!canRead) { setLoading(false); return; }
    setLoading(true);
    const [controls, context] = await Promise.allSettled([
      civilEngineeringInspectionControlService.list(projectId),
      canManage ? civilEngineeringInspectionControlService.lookups(projectId) : Promise.resolve(undefined),
    ]);
    if (controls.status === 'fulfilled') setItems(controls.value);
    else toast({ variant: 'destructive', title: 'Civil inspections could not be loaded', description: errorText(controls.reason, 'Refresh the project workspace and try again.') });
    if (context.status === 'fulfilled' && context.value) setLookups(context.value); else setLookups(undefined);
    setLoading(false);
  }, [canManage, canRead, projectId, toast]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    if (!lookups) return;
    setForm((current) => ({ ...current, planningGisValidationId: current.planningGisValidationId || lookups.planningGisValidations[0]?.id || '', inspectorUserId: current.inspectorUserId || lookups.inspectors[0]?.id || '', documentKey: current.documentKey || documentKey(lookups.documents[0]) }));
  }, [lookups]);

  const selectedPlanDocument = useMemo(() => lookups?.documents.find((item) => documentKey(item) === form.documentKey), [form.documentKey, lookups]);
  const create = async () => {
    if (!lookups || !selectedPlanDocument || !form.planningGisValidationId || !form.inspectorUserId || !form.scheduledAt || form.purpose.trim().length < 3) {
      toast({ variant: 'destructive', title: 'Complete the governed inspection plan', description: 'Use the approved Planning/GIS site, qualified inspector and current Published DMS plan evidence selectors.' });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringInspectionControlService.create(projectId, { clientRequestId: crypto.randomUUID(), planningGisValidationId: form.planningGisValidationId, inspectorUserId: form.inspectorUserId, scheduledAt: new Date(form.scheduledAt).toISOString(), purpose: form.purpose.trim(), planDocumentRecordId: selectedPlanDocument.centralDocumentRecordId, planDocumentVersionId: selectedPlanDocument.centralDocumentVersionId });
      setForm(blank);
      toast({ title: 'Inspection plan submitted', description: 'The plan is awaiting independent approval through the configured shared workflow before the inspection is scheduled.' });
      await load();
    } catch (error) { toast({ variant: 'destructive', title: 'Inspection plan was not submitted', description: errorText(error, 'Check the effective Civil supervision, workflow and quality controls.') }); }
    finally { setSaving(false); }
  };

  const actionDocument = (id: string) => lookups?.documents.find((item) => documentKey(item) === (documents[id] || documentKey(lookups?.documents[0])));
  const process = async (item: CivilEngineeringInspectionControl, action: CivilEngineeringInspectionAction) => {
    const document = actionDocument(item.id);
    const note = findings[item.id]?.trim() || '';
    const actionText = corrective[item.id]?.trim() || '';
    const reviewComment = reviewComments[item.id]?.trim() || '';
    const reinspectionInspectorUserId = reinspectors[item.id] || '';
    const needsFindings = [CivilEngineeringInspectionAction.RecordPassed, CivilEngineeringInspectionAction.RecordFailed, CivilEngineeringInspectionAction.RecordReinspectionPassed, CivilEngineeringInspectionAction.RecordReinspectionFailed].includes(action);
    const reviewsPlan = action === CivilEngineeringInspectionAction.ApprovePlan || action === CivilEngineeringInspectionAction.RejectPlan;
    if ((!reviewsPlan && !document) || (needsFindings && note.length < 3) || (action === CivilEngineeringInspectionAction.RecordCorrectiveAction && (actionText.length < 3 || !reinspectionInspectorUserId)) || (action === CivilEngineeringInspectionAction.RejectPlan && reviewComment.length < 3)) {
      toast({ variant: 'destructive', title: 'Complete the controlled action', description: action === CivilEngineeringInspectionAction.RejectPlan ? 'Enter a clear rejection reason.' : action === CivilEngineeringInspectionAction.RecordCorrectiveAction ? 'Record the corrective action, select a different qualified reinspection inspector and choose DMS evidence.' : 'Record the findings and choose current Published DMS evidence.' });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringInspectionControlService.process(item.id, { clientRequestId: crypto.randomUUID(), rowVersion: item.rowVersion, action, findings: needsFindings ? note : null, correctiveAction: action === CivilEngineeringInspectionAction.RecordCorrectiveAction ? actionText : null, reviewComment: reviewsPlan ? reviewComment : null, reinspectionInspectorUserId: action === CivilEngineeringInspectionAction.RecordCorrectiveAction ? reinspectionInspectorUserId : null, centralDocumentRecordId: reviewsPlan ? null : document?.centralDocumentRecordId ?? null, centralDocumentVersionId: reviewsPlan ? null : document?.centralDocumentVersionId ?? null });
      setFindings((current) => ({ ...current, [item.id]: '' })); setCorrective((current) => ({ ...current, [item.id]: '' })); setReviewComments((current) => ({ ...current, [item.id]: '' }));
      toast({ title: action === CivilEngineeringInspectionAction.ApprovePlan ? 'Inspection plan approved' : action === CivilEngineeringInspectionAction.RejectPlan ? 'Inspection plan rejected' : action === CivilEngineeringInspectionAction.RecordFailed || action === CivilEngineeringInspectionAction.RecordReinspectionFailed ? 'Failure control recorded' : action === CivilEngineeringInspectionAction.Close ? 'Inspection closed' : 'Inspection action recorded', description: action === CivilEngineeringInspectionAction.RecordFailed || action === CivilEngineeringInspectionAction.RecordReinspectionFailed ? 'The linked Projects non-conformance remains open and prevents governed quality sign-off.' : 'The shared workflow, inspection record and immutable audit history were updated.' });
      await load();
    } catch (error) { toast({ variant: 'destructive', title: 'Inspection action was not saved', description: errorText(error, 'Refresh the record and use the assigned controlled actor.') }); }
    finally { setSaving(false); }
  };

  if (!canRead) return null;
  return <Card className="border-slate-200/70 shadow-sm">
    <CardHeader className="gap-3 pb-3 sm:flex-row sm:items-start sm:justify-between"><div><CardTitle className="flex items-center gap-2 text-base"><ClipboardCheck className="h-4 w-4 text-sky-700" />Civil inspections and corrective actions</CardTitle><CardDescription>Scheduled against approved Planning/GIS site evidence. A failed inspection creates a Projects non-conformance and cannot be signed off until correction and independent reinspection pass.</CardDescription></div><Button variant="outline" size="sm" onClick={() => void load()} disabled={loading || saving}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button></CardHeader>
    <CardContent className="space-y-4">
      {loading ? <p className="text-sm text-slate-500">Loading governed inspections…</p> : null}
      {canManage && !loading && !lookups ? <p className="rounded border border-slate-200 bg-slate-50 p-3 text-sm text-slate-600">Inspection history is available, but scheduling requires an effective approved and verified CIV-CFG-005 supervision policy, CIV-CFG-011 quality policy, Planning/GIS validation, configured project engineers and a published DMS template.</p> : null}
      {canManage && lookups ? <section className="rounded-lg border border-sky-200 bg-sky-50/40 p-4"><div className="mb-3 flex items-center gap-2 text-sm font-medium text-slate-900"><ShieldCheck className="h-4 w-4 text-sky-700" />Submit a governed inspection plan</div><div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4"><div className="space-y-1"><Label>Approved Planning/GIS site</Label><Select value={form.planningGisValidationId} onValueChange={(value) => setForm((current) => ({ ...current, planningGisValidationId: value }))}><SelectTrigger><SelectValue placeholder="Select site validation" /></SelectTrigger><SelectContent>{lookups.planningGisValidations.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Qualified inspector</Label><Select value={form.inspectorUserId} onValueChange={(value) => setForm((current) => ({ ...current, inspectorUserId: value }))}><SelectTrigger><SelectValue placeholder="Select project engineer" /></SelectTrigger><SelectContent>{lookups.inspectors.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Proposed date</Label><Input type="datetime-local" value={form.scheduledAt} onChange={(event) => setForm((current) => ({ ...current, scheduledAt: event.target.value }))} /></div><div className="space-y-1"><Label>Current Published DMS plan</Label><Select value={form.documentKey} onValueChange={(value) => setForm((current) => ({ ...current, documentKey: value }))}><SelectTrigger><SelectValue placeholder="Select evidence" /></SelectTrigger><SelectContent>{lookups.documents.map((item) => <SelectItem key={item.centralDocumentVersionId} value={documentKey(item)}>{item.documentReference} · v{item.versionNumber}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1 md:col-span-2 xl:col-span-4"><Label>Purpose</Label><Textarea rows={2} value={form.purpose} onChange={(event) => setForm((current) => ({ ...current, purpose: event.target.value }))} placeholder="Describe the controlled site inspection purpose." /></div></div><div className="mt-3 flex justify-end"><Button size="sm" disabled={saving || !lookups.planningGisValidations.length || !lookups.inspectors.length || !lookups.documents.length} onClick={() => void create()}><ClipboardCheck className="mr-2 h-4 w-4" />Submit for approval</Button></div></section> : null}
      {!loading && !items.length ? <p className="rounded border border-dashed border-slate-300 bg-slate-50 p-3 text-sm text-slate-600">No governed Civil inspections have been scheduled for this project.</p> : null}
      {items.map((item) => <InspectionCard key={item.id} item={item} lookups={lookups} canManage={canManage} saving={saving} findings={findings[item.id] || ''} corrective={corrective[item.id] || ''} reviewComment={reviewComments[item.id] || ''} reinspectionInspectorId={reinspectors[item.id] || ''} documentKey={documents[item.id] || documentKey(lookups?.documents[0])} setFindings={(value) => setFindings((current) => ({ ...current, [item.id]: value }))} setCorrective={(value) => setCorrective((current) => ({ ...current, [item.id]: value }))} setReviewComment={(value) => setReviewComments((current) => ({ ...current, [item.id]: value }))} setReinspector={(value) => setReinspectors((current) => ({ ...current, [item.id]: value }))} setDocument={(value) => setDocuments((current) => ({ ...current, [item.id]: value }))} process={process} />)}
    </CardContent>
  </Card>;
}

function InspectionCard({ item, lookups, canManage, saving, findings, corrective, reviewComment, reinspectionInspectorId, documentKey: evidenceKey, setFindings, setCorrective, setReviewComment, setReinspector, setDocument, process }: { item: CivilEngineeringInspectionControl; lookups?: CivilEngineeringInspectionLookups; canManage: boolean; saving: boolean; findings: string; corrective: string; reviewComment: string; reinspectionInspectorId: string; documentKey: string; setFindings: (value: string) => void; setCorrective: (value: string) => void; setReviewComment: (value: string) => void; setReinspector: (value: string) => void; setDocument: (value: string) => void; process: (item: CivilEngineeringInspectionControl, action: CivilEngineeringInspectionAction) => Promise<void> }) {
  const pendingApproval = item.stage === 'PendingApproval';
  const planned = item.stage === 'Scheduled';
  const correctiveRequired = item.stage === 'CorrectiveActionRequired';
  const reinspecting = item.stage === 'ReinspectionScheduled';
  const passed = item.stage === 'Passed';
  return <article className="rounded-lg border border-slate-200 bg-white p-4">
    <div className="flex flex-col gap-2 lg:flex-row lg:justify-between"><div><div className="flex flex-wrap items-center gap-2"><span className="font-medium text-slate-900">{item.purpose}</span><Badge variant={item.status === 'Blocked' || item.status === 'Rejected' ? 'destructive' : item.status === 'Closed' ? 'default' : 'secondary'}>{item.stage}</Badge><Badge variant="outline">Plan: {item.planApprovalStatus}</Badge>{item.nonConformanceId ? <Badge variant="destructive"><FileWarning className="mr-1 h-3 w-3" />Non-conformance linked</Badge> : null}</div><p className="mt-2 text-sm text-slate-600">{item.planningGisValidationLabel} · {item.spatialReferenceSnapshot}</p><p className="mt-1 text-xs text-slate-500">Proposed {formatDate(item.scheduledAt)} · Inspector: {item.inspectorName}{item.reinspectionInspectorName ? ` · Reinspection: ${item.reinspectionInspectorName}` : ''}</p>{item.planRejectionReason ? <p className="mt-2 whitespace-pre-wrap break-words text-sm text-red-700">Plan rejection: {item.planRejectionReason}</p> : null}{item.findings ? <p className="mt-2 whitespace-pre-wrap break-words text-sm text-slate-700">Findings: {item.findings}</p> : null}{item.correctiveAction ? <p className="mt-1 whitespace-pre-wrap break-words text-sm text-slate-700">Corrective action: {item.correctiveAction}</p> : null}</div></div>
    {canManage && pendingApproval ? <section className="mt-4 rounded border border-amber-200 bg-amber-50/60 p-3"><div className="space-y-1"><Label>Independent review comment</Label><Textarea rows={2} value={reviewComment} onChange={(event) => setReviewComment(event.target.value)} placeholder="Required when rejecting; optional when approving." /></div><div className="mt-3 flex flex-wrap justify-end gap-2"><Button variant="outline" size="sm" disabled={saving} onClick={() => void process(item, CivilEngineeringInspectionAction.RejectPlan)}><XCircle className="mr-2 h-4 w-4" />Reject plan</Button><Button size="sm" disabled={saving} onClick={() => void process(item, CivilEngineeringInspectionAction.ApprovePlan)}><ShieldCheck className="mr-2 h-4 w-4" />Approve plan</Button></div></section> : null}
    {canManage && lookups && (planned || correctiveRequired || reinspecting || passed) ? <section className="mt-4 rounded border border-slate-200 bg-slate-50 p-3"><div className="grid gap-3 md:grid-cols-2"><div className="space-y-1"><Label>Current Published DMS evidence</Label><Select value={evidenceKey} onValueChange={setDocument}><SelectTrigger><SelectValue placeholder="Select evidence" /></SelectTrigger><SelectContent>{lookups.documents.map((value) => <SelectItem key={value.centralDocumentVersionId} value={documentKey(value)}>{value.documentReference} · v{value.versionNumber}</SelectItem>)}</SelectContent></Select></div>{(planned || reinspecting) ? <div className="space-y-1"><Label>Findings</Label><Textarea rows={2} value={findings} onChange={(event) => setFindings(event.target.value)} placeholder="Record the observed outcome and supporting finding." /></div> : null}{correctiveRequired ? <><div className="space-y-1"><Label>Corrective action</Label><Textarea rows={2} value={corrective} onChange={(event) => setCorrective(event.target.value)} placeholder="Record the action taken to correct the failed inspection." /></div><div className="space-y-1"><Label>Independent reinspection inspector</Label><Select value={reinspectionInspectorId} onValueChange={setReinspector}><SelectTrigger><SelectValue placeholder="Select a different qualified inspector" /></SelectTrigger><SelectContent>{lookups.inspectors.filter((value) => value.id !== item.inspectorUserId).map((value) => <SelectItem key={value.id} value={value.id}>{value.label}</SelectItem>)}</SelectContent></Select></div></> : null}</div><div className="mt-3 flex flex-wrap justify-end gap-2">{planned ? <><Button variant="outline" size="sm" disabled={saving} onClick={() => void process(item, CivilEngineeringInspectionAction.RecordFailed)}><FileWarning className="mr-2 h-4 w-4" />Record failure</Button><Button size="sm" disabled={saving} onClick={() => void process(item, CivilEngineeringInspectionAction.RecordPassed)}><CheckCircle2 className="mr-2 h-4 w-4" />Record pass</Button></> : null}{correctiveRequired ? <Button size="sm" disabled={saving} onClick={() => void process(item, CivilEngineeringInspectionAction.RecordCorrectiveAction)}><Wrench className="mr-2 h-4 w-4" />Submit for reinspection</Button> : null}{reinspecting ? <><Button variant="outline" size="sm" disabled={saving} onClick={() => void process(item, CivilEngineeringInspectionAction.RecordReinspectionFailed)}>Reinspection failed</Button><Button size="sm" disabled={saving} onClick={() => void process(item, CivilEngineeringInspectionAction.RecordReinspectionPassed)}>Reinspection passed</Button></> : null}{passed ? <Button size="sm" disabled={saving} onClick={() => void process(item, CivilEngineeringInspectionAction.Close)}><ShieldCheck className="mr-2 h-4 w-4" />Independent close</Button> : null}</div></section> : null}
  </article>;
}

function documentKey(item?: { centralDocumentRecordId: string; centralDocumentVersionId: string }) { return item ? `${item.centralDocumentRecordId}:${item.centralDocumentVersionId}` : ''; }
