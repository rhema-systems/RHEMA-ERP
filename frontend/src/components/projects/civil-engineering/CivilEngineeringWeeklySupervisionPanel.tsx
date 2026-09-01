'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { BellRing, CalendarDays, ClipboardList, FileCheck2, Plus, RefreshCw, Send, ShieldCheck, Trash2 } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringWeeklySupervisionService } from '@/services/civil-engineering-weekly-supervision.service';
import type { CivilEngineeringWeeklySupervisionDocument, CivilEngineeringWeeklySupervisionLookups, CivilEngineeringWeeklySupervisionReport } from '@/types/civil-engineering-weekly-supervision';

const workspacePermission = 'civil-engineering.workspace.read';
const supervisionPermission = 'civil-engineering.supervision.manage';
const none = '__none__';
const formatDate = (value?: string | null) => value ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : 'Not available';
const formatDay = (value?: string | null) => value ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(new Date(value)) : 'Not recorded';
const errorText = (error: unknown, fallback: string) => {
  const value = error as { response?: { detail?: string; correlationId?: string }; message?: string };
  return `${value.response?.detail || value.message || fallback}${value.response?.correlationId ? ` Reference: ${value.response.correlationId}` : ''}`;
};
const monday = () => {
  const today = new Date();
  const offset = (today.getDay() + 6) % 7;
  today.setDate(today.getDate() - offset);
  return today.toISOString().slice(0, 10);
};
type ActivityForm = { activityCategoryId: string; actorType: string; contractorBusinessPartnerId: string; description: string; progressPercent: string };
type EvidenceForm = { documentKey: string; evidenceRole: string };
type Form = { weekStart: string; projectMilestoneId: string; projectRiskId: string; projectIssueId: string; projectTaskDependencyId: string; overallProgressPercent: string; siteStatus: string; delayReason: string; recoveryAction: string; recoveryOwnerUserId: string; recoveryDueDate: string; materialUsageSummary: string; safetyNotes: string; testSummary: string; activities: ActivityForm[]; evidence: EvidenceForm[] };
type Props = { projectId: string };
const blank = (): Form => ({ weekStart: monday(), projectMilestoneId: '', projectRiskId: none, projectIssueId: none, projectTaskDependencyId: none, overallProgressPercent: '', siteStatus: 'OnTrack', delayReason: '', recoveryAction: '', recoveryOwnerUserId: none, recoveryDueDate: '', materialUsageSummary: '', safetyNotes: '', testSummary: '', activities: [{ activityCategoryId: '', actorType: '', contractorBusinessPartnerId: none, description: '', progressPercent: '' }], evidence: [{ documentKey: '', evidenceRole: 'Report' }] });

export function CivilEngineeringWeeklySupervisionPanel({ projectId }: Props) {
  const { toast } = useToast();
  const { hasPermission } = useAuth();
  const canRead = hasPermission(workspacePermission);
  const canManage = hasPermission(supervisionPermission);
  const [items, setItems] = useState<CivilEngineeringWeeklySupervisionReport[]>([]);
  const [lookups, setLookups] = useState<CivilEngineeringWeeklySupervisionLookups>();
  const [form, setForm] = useState<Form>(blank);
  const [reviewComments, setReviewComments] = useState<Record<string, string>>({});
  const [reviewDocuments, setReviewDocuments] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    if (!canRead) { setLoading(false); return; }
    setLoading(true);
    const [reports, context] = await Promise.allSettled([
      civilEngineeringWeeklySupervisionService.list(projectId),
      canManage ? civilEngineeringWeeklySupervisionService.lookups(projectId) : Promise.resolve(undefined),
    ]);
    if (reports.status === 'fulfilled') setItems(reports.value);
    else toast({ variant: 'destructive', title: 'Weekly reports could not be loaded', description: errorText(reports.reason, 'Refresh the project workspace and try again.') });
    if (context.status === 'fulfilled' && context.value) setLookups(context.value);
    else setLookups(undefined);
    setLoading(false);
  }, [canManage, canRead, projectId, toast]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    if (!lookups) return;
    setForm((current) => ({
      ...current,
      projectMilestoneId: current.projectMilestoneId || lookups.milestones[0]?.id || '',
      siteStatus: current.siteStatus || lookups.siteStatuses[0] || 'OnTrack',
      activities: current.activities.map((activity) => ({ ...activity, activityCategoryId: activity.activityCategoryId || lookups.activityCategories[0]?.id || '', actorType: activity.actorType || lookups.actorTypes[0] || '' })),
      evidence: current.evidence.map((evidence) => ({ ...evidence, evidenceRole: evidence.evidenceRole || lookups.evidenceRoles[0] || '', documentKey: evidence.documentKey || documentKey(lookups.documents[0]) })),
    }));
  }, [lookups]);

  const selectedDocument = useCallback((key: string) => lookups?.documents.find((document) => documentKey(document) === key), [lookups]);
  const photoCount = useMemo(() => form.evidence.filter((item) => item.evidenceRole === 'Photo').length, [form.evidence]);
  const updateActivity = (index: number, patch: Partial<ActivityForm>) => setForm((current) => ({ ...current, activities: current.activities.map((item, itemIndex) => itemIndex === index ? { ...item, ...patch } : item) }));
  const updateEvidence = (index: number, patch: Partial<EvidenceForm>) => setForm((current) => ({ ...current, evidence: current.evidence.map((item, itemIndex) => itemIndex === index ? { ...item, ...patch } : item) }));

  const create = async () => {
    if (!lookups || !form.weekStart || !form.projectMilestoneId || !form.activities.length || !form.evidence.length) return;
    const selectedEvidence = form.evidence.map((item) => ({ item, document: selectedDocument(item.documentKey) }));
    const governedEvidence = selectedEvidence.filter((value): value is { item: EvidenceForm; document: CivilEngineeringWeeklySupervisionDocument } => Boolean(value.document));
    const invalidActivity = form.activities.some((item) => !item.activityCategoryId || !item.actorType || item.description.trim().length < 3 || (item.actorType === 'Contractor' && item.contractorBusinessPartnerId === none));
    const duplicateEvidence = new Set(selectedEvidence.map((item) => item.document?.centralDocumentVersionId)).size !== selectedEvidence.length;
    const needsRecovery = form.siteStatus !== 'OnTrack';
    if (invalidActivity || governedEvidence.length !== selectedEvidence.length || selectedEvidence.some((item) => !item.item.evidenceRole) || duplicateEvidence || photoCount < lookups.minimumPhotoCount || form.overallProgressPercent === '' || !form.siteStatus || (needsRecovery && (!form.delayReason.trim() || !form.recoveryAction.trim() || form.recoveryOwnerUserId === none || !form.recoveryDueDate))) {
      toast({ variant: 'destructive', title: 'Complete the weekly report', description: needsRecovery ? 'Select the milestone, controlled site status, recovery owner and due date, then provide the delay and recovery action.' : `Select the milestone, progress, controlled activities and current DMS evidence. This policy requires at least ${lookups.minimumPhotoCount} photo item(s).` });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringWeeklySupervisionService.create(projectId, {
        clientRequestId: crypto.randomUUID(), weekStart: new Date(`${form.weekStart}T00:00:00`).toISOString(),
        projectMilestoneId: form.projectMilestoneId, projectRiskId: form.projectRiskId === none ? null : form.projectRiskId, projectIssueId: form.projectIssueId === none ? null : form.projectIssueId, projectTaskDependencyId: form.projectTaskDependencyId === none ? null : form.projectTaskDependencyId,
        overallProgressPercent: form.overallProgressPercent === '' ? null : Number(form.overallProgressPercent),
        siteStatus: form.siteStatus, delayReason: needsRecovery ? form.delayReason.trim() : null, recoveryAction: needsRecovery ? form.recoveryAction.trim() : null, recoveryOwnerUserId: needsRecovery ? form.recoveryOwnerUserId : null, recoveryDueDate: needsRecovery ? new Date(`${form.recoveryDueDate}T00:00:00`).toISOString() : null,
        materialUsageSummary: form.materialUsageSummary.trim() || null, safetyNotes: form.safetyNotes.trim() || null, testSummary: form.testSummary.trim() || null,
        activities: form.activities.map((item) => ({ activityCategoryId: item.activityCategoryId, actorType: item.actorType, contractorBusinessPartnerId: item.actorType === 'Contractor' ? item.contractorBusinessPartnerId : null, description: item.description.trim(), progressPercent: item.progressPercent === '' ? null : Number(item.progressPercent) })),
        evidence: governedEvidence.map(({ item, document }) => ({ centralDocumentRecordId: document.centralDocumentRecordId, centralDocumentVersionId: document.centralDocumentVersionId, evidenceRole: item.evidenceRole })),
      });
      setForm(blank());
      toast({ title: 'Weekly report submitted', description: 'The report is now in the configured independent review workflow.' });
      await load();
    } catch (error) {
      toast({ variant: 'destructive', title: 'Weekly report was not submitted', description: errorText(error, 'Check the controlled selections and effective Civil policy.') });
    } finally { setSaving(false); }
  };

  const review = async (report: CivilEngineeringWeeklySupervisionReport, approve: boolean) => {
    const comment = reviewComments[report.id]?.trim() || '';
    const document = selectedDocument(reviewDocuments[report.id] || '');
    if (comment.length < 3) {
      toast({ variant: 'destructive', title: 'Add a review comment', description: 'Record the technical decision before sending it to Workflow.' });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringWeeklySupervisionService.review(report.id, { clientRequestId: crypto.randomUUID(), rowVersion: report.rowVersion, approve, comment, centralDocumentRecordId: document?.centralDocumentRecordId ?? null, centralDocumentVersionId: document?.centralDocumentVersionId ?? null });
      setReviewComments((current) => ({ ...current, [report.id]: '' }));
      toast({ title: approve ? 'Review advanced' : 'Report returned', description: approve ? 'The shared weekly-report workflow has recorded the decision.' : 'The recorded return reason is now available to the workflow.' });
      await load();
    } catch (error) {
      toast({ variant: 'destructive', title: 'Workflow decision was not saved', description: errorText(error, 'Only the independently assigned workflow reviewer can decide this report.') });
    } finally { setSaving(false); }
  };

  const escalate = async () => {
    setSaving(true);
    try {
      await civilEngineeringWeeklySupervisionService.escalateOverdue(projectId, crypto.randomUUID());
      toast({ title: 'Overdue reports escalated', description: 'Central notifications were queued for the configured active project recipients.' });
      await load();
    } catch (error) {
      toast({ variant: 'destructive', title: 'Overdue escalation was not queued', description: errorText(error, 'Check the configured escalation roles and active project membership.') });
    } finally { setSaving(false); }
  };

  if (!canRead) return null;
  return <Card className="border-slate-200/70 shadow-sm">
    <CardHeader className="gap-3 pb-3 sm:flex-row sm:items-start sm:justify-between">
      <div><CardTitle className="flex items-center gap-2 text-base"><ClipboardList className="h-4 w-4 text-indigo-600" />Weekly supervision reports</CardTitle><CardDescription>Project Engineer activity, progress and DMS evidence with controlled SCE-to-HOD workflow review.</CardDescription></div>
      <div className="flex gap-2"><Button variant="outline" size="sm" onClick={() => void load()} disabled={loading || saving}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>{canManage ? <Button variant="outline" size="sm" onClick={() => void escalate()} disabled={loading || saving}><BellRing className="mr-2 h-4 w-4" />Escalate overdue</Button> : null}</div>
    </CardHeader>
    <CardContent className="space-y-4">
      {loading ? <p className="text-sm text-slate-500">Loading weekly supervision reports…</p> : null}
      {canManage && !loading && !lookups ? <p className="rounded border border-slate-200 bg-slate-50 p-3 text-sm text-slate-600">Weekly-report history is available, but creation needs the effective Civil policy, published workflow, controlled activity categories and published DMS template.</p> : null}
      {canManage && lookups ? <section className="rounded-lg border border-indigo-200 bg-indigo-50/30 p-4"><div className="mb-3 flex items-center gap-2 text-sm font-medium text-slate-900"><CalendarDays className="h-4 w-4 text-indigo-700" />Prepare weekly supervision report</div>
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4"><div className="space-y-1"><Label>Week starting (Monday)</Label><Input type="date" value={form.weekStart} onChange={(event) => setForm((current) => ({ ...current, weekStart: event.target.value }))} /></div><div className="space-y-1"><Label>Overall progress (%)</Label><Input type="number" min="0" max="100" value={form.overallProgressPercent} onChange={(event) => setForm((current) => ({ ...current, overallProgressPercent: event.target.value }))} /></div><div className="space-y-1 md:col-span-2"><Label>Material usage</Label><Textarea rows={1} value={form.materialUsageSummary} onChange={(event) => setForm((current) => ({ ...current, materialUsageSummary: event.target.value }))} /></div><div className="space-y-1 md:col-span-2"><Label>Safety notes</Label><Textarea rows={1} value={form.safetyNotes} onChange={(event) => setForm((current) => ({ ...current, safetyNotes: event.target.value }))} /></div><div className="space-y-1 md:col-span-2"><Label>Test observations</Label><Textarea rows={1} value={form.testSummary} onChange={(event) => setForm((current) => ({ ...current, testSummary: event.target.value }))} /></div></div>
        <div className="mt-4 grid gap-3 rounded border border-slate-200 bg-white p-3 md:grid-cols-2 xl:grid-cols-3"><div className="space-y-1"><Label>Covered milestone</Label><Select value={form.projectMilestoneId} onValueChange={(value) => setForm((current) => ({ ...current, projectMilestoneId: value }))}><SelectTrigger><SelectValue placeholder="Select milestone" /></SelectTrigger><SelectContent>{lookups.milestones.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Site status</Label><Select value={form.siteStatus} onValueChange={(value) => setForm((current) => ({ ...current, siteStatus: value, ...(value === 'OnTrack' ? { delayReason: '', recoveryAction: '', recoveryOwnerUserId: none, recoveryDueDate: '' } : {}) }))}><SelectTrigger><SelectValue placeholder="Select site status" /></SelectTrigger><SelectContent>{lookups.siteStatuses.map((item) => <SelectItem key={item} value={item}>{item === 'AtRisk' ? 'At risk' : item}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Related risk (optional)</Label><Select value={form.projectRiskId} onValueChange={(value) => setForm((current) => ({ ...current, projectRiskId: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value={none}>No related risk</SelectItem>{lookups.risks.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Related issue (optional)</Label><Select value={form.projectIssueId} onValueChange={(value) => setForm((current) => ({ ...current, projectIssueId: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value={none}>No related issue</SelectItem>{lookups.issues.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Dependency (optional)</Label><Select value={form.projectTaskDependencyId} onValueChange={(value) => setForm((current) => ({ ...current, projectTaskDependencyId: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value={none}>No related dependency</SelectItem>{lookups.dependencies.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div></div>
        {form.siteStatus !== 'OnTrack' ? <div className="mt-3 grid gap-3 rounded border border-amber-200 bg-amber-50/60 p-3 md:grid-cols-2 xl:grid-cols-4"><div className="space-y-1 xl:col-span-2"><Label>Delay or risk reason</Label><Textarea rows={2} value={form.delayReason} onChange={(event) => setForm((current) => ({ ...current, delayReason: event.target.value }))} placeholder="Describe the delay, risk or site stoppage." /></div><div className="space-y-1 xl:col-span-2"><Label>Recovery action</Label><Textarea rows={2} value={form.recoveryAction} onChange={(event) => setForm((current) => ({ ...current, recoveryAction: event.target.value }))} placeholder="Describe the controlled recovery action." /></div><div className="space-y-1"><Label>Recovery owner</Label><Select value={form.recoveryOwnerUserId} onValueChange={(value) => setForm((current) => ({ ...current, recoveryOwnerUserId: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value={none}>Select project member</SelectItem>{lookups.recoveryOwners.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Recovery due date</Label><Input type="date" value={form.recoveryDueDate} onChange={(event) => setForm((current) => ({ ...current, recoveryDueDate: event.target.value }))} /></div><p className="text-xs text-amber-800 md:col-span-2">A reduced progress figure is retained as a proposed correction and becomes an approved management decision only when the shared workflow reaches its final approval.</p></div> : null}
        <div className="mt-4 space-y-3"><div className="flex items-center justify-between"><h4 className="text-sm font-medium text-slate-900">Contractor and labour-gang activities</h4><Button variant="outline" size="sm" onClick={() => setForm((current) => ({ ...current, activities: [...current.activities, { activityCategoryId: lookups.activityCategories[0]?.id || '', actorType: lookups.actorTypes[0] || '', contractorBusinessPartnerId: none, description: '', progressPercent: '' }] }))}><Plus className="mr-1 h-3.5 w-3.5" />Add activity</Button></div>{form.activities.map((activity, index) => <div key={index} className="grid gap-2 rounded border border-slate-200 bg-white p-3 md:grid-cols-2 xl:grid-cols-5"><Select value={activity.activityCategoryId} onValueChange={(value) => updateActivity(index, { activityCategoryId: value })}><SelectTrigger><SelectValue placeholder="Activity category" /></SelectTrigger><SelectContent>{lookups.activityCategories.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select><Select value={activity.actorType} onValueChange={(value) => updateActivity(index, { actorType: value, contractorBusinessPartnerId: value === 'Contractor' ? activity.contractorBusinessPartnerId : none })}><SelectTrigger><SelectValue placeholder="Activity owner" /></SelectTrigger><SelectContent>{lookups.actorTypes.map((item) => <SelectItem key={item} value={item}>{item === 'LabourGang' ? 'Labour gang' : item}</SelectItem>)}</SelectContent></Select>{activity.actorType === 'Contractor' ? <Select value={activity.contractorBusinessPartnerId} onValueChange={(value) => updateActivity(index, { contractorBusinessPartnerId: value })}><SelectTrigger><SelectValue placeholder="Contractor" /></SelectTrigger><SelectContent>{lookups.contractors.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select> : <div className="rounded border border-dashed px-3 py-2 text-sm text-slate-500">Internal labour gang</div>}<Input type="number" min="0" max="100" placeholder="Progress %" value={activity.progressPercent} onChange={(event) => updateActivity(index, { progressPercent: event.target.value })} /><div className="flex gap-2"><Textarea rows={1} className="min-w-0" placeholder="Activity description" value={activity.description} onChange={(event) => updateActivity(index, { description: event.target.value })} />{form.activities.length > 1 ? <Button variant="ghost" size="icon" aria-label="Remove activity" onClick={() => setForm((current) => ({ ...current, activities: current.activities.filter((_, itemIndex) => itemIndex !== index) }))}><Trash2 className="h-4 w-4 text-rose-600" /></Button> : null}</div></div>)}</div>
        <div className="mt-4 space-y-3"><div className="flex items-center justify-between"><div><h4 className="text-sm font-medium text-slate-900">Current Published DMS evidence</h4><p className="text-xs text-slate-500">Photo evidence: {photoCount}/{lookups.minimumPhotoCount} minimum.</p></div><Button variant="outline" size="sm" onClick={() => setForm((current) => ({ ...current, evidence: [...current.evidence, { documentKey: documentKey(lookups.documents[0]), evidenceRole: 'Photo' }] }))}><Plus className="mr-1 h-3.5 w-3.5" />Add evidence</Button></div>{form.evidence.map((evidence, index) => <div key={index} className="flex flex-col gap-2 rounded border border-slate-200 bg-white p-3 md:flex-row"><Select value={evidence.documentKey} onValueChange={(value) => updateEvidence(index, { documentKey: value })}><SelectTrigger className="md:flex-1"><SelectValue placeholder="Select DMS document" /></SelectTrigger><SelectContent>{lookups.documents.map((item) => <SelectItem key={item.centralDocumentVersionId} value={documentKey(item)}>{item.documentReference} · v{item.versionNumber}</SelectItem>)}</SelectContent></Select><Select value={evidence.evidenceRole} onValueChange={(value) => updateEvidence(index, { evidenceRole: value })}><SelectTrigger className="md:w-40"><SelectValue placeholder="Role" /></SelectTrigger><SelectContent>{lookups.evidenceRoles.filter((item) => item !== 'Review').map((item) => <SelectItem key={item} value={item}>{item}</SelectItem>)}</SelectContent></Select>{form.evidence.length > 1 ? <Button variant="ghost" size="icon" aria-label="Remove evidence" onClick={() => setForm((current) => ({ ...current, evidence: current.evidence.filter((_, itemIndex) => itemIndex !== index) }))}><Trash2 className="h-4 w-4 text-rose-600" /></Button> : null}</div>)}</div>
        <div className="mt-4 flex justify-end"><Button size="sm" disabled={saving || !lookups.activityCategories.length || !lookups.documents.length} onClick={() => void create()}><Send className="mr-2 h-4 w-4" />Submit for workflow review</Button></div>
      </section> : null}
      {!loading && !items.length ? <p className="rounded border border-dashed border-slate-300 bg-slate-50 p-3 text-sm text-slate-600">No weekly supervision reports have been registered for this project.</p> : null}
      {items.map((item) => { const pending = item.status === 'PendingApproval' && item.approvalStatus === 'Pending'; return <article key={item.id} className="rounded-lg border border-slate-200 bg-white p-4"><div className="flex flex-col gap-2 lg:flex-row lg:items-start lg:justify-between"><div><div className="flex flex-wrap items-center gap-2"><span className="font-medium text-slate-900">Week of {formatDate(item.weekStart)}</span><Badge variant={item.approvalStatus === 'Approved' ? 'default' : item.approvalStatus === 'Rejected' ? 'destructive' : 'secondary'}>{item.approvalStatus}</Badge><Badge variant={item.siteStatus === 'OnTrack' ? 'outline' : item.siteStatus === 'AtRisk' ? 'secondary' : 'destructive'}>{item.siteStatus === 'AtRisk' ? 'At risk' : item.siteStatus}</Badge>{item.isProgressCorrection ? <Badge variant="secondary">Progress correction</Badge> : null}{item.isOverdue ? <Badge variant="destructive">Overdue</Badge> : null}{item.escalatedAt ? <Badge variant="outline">Escalated</Badge> : null}</div><p className="mt-2 text-sm text-slate-600">Engineer: {item.projectEngineerName} · Milestone: {item.projectMilestoneTitle} · Progress: {item.overallProgressPercent ?? '—'}% · Due {formatDate(item.dueAt)}</p></div></div>{item.milestoneTargetDateSnapshot ? <p className="mt-2 text-xs text-slate-600">Milestone schedule snapshot: planned {formatDay(item.milestoneTargetDateSnapshot)} · actual {formatDay(item.milestoneActualDateSnapshot)}</p> : null}{item.delayReason ? <p className="mt-3 rounded bg-amber-50 p-2 text-sm text-amber-900">{item.delayReason}{item.recoveryActionTitle ? ` Recovery action: ${item.recoveryActionTitle}.` : ''}{item.progressCorrectionDecisionTitle ? ` Approved management decision: ${item.progressCorrectionDecisionTitle}.` : ''}</p> : null}{item.projectRiskTitle || item.projectIssueTitle || item.projectTaskDependencyLabel ? <p className="mt-2 text-xs text-slate-600">{[item.projectRiskTitle && `Risk: ${item.projectRiskTitle}`, item.projectIssueTitle && `Issue: ${item.projectIssueTitle}`, item.projectTaskDependencyLabel && `Dependency: ${item.projectTaskDependencyLabel}`].filter(Boolean).join(' · ')}</p> : null}{item.activities.length ? <ul className="mt-3 space-y-1 text-sm text-slate-700">{item.activities.map((activity) => <li key={activity.sequence}><span className="font-medium">{activity.activityCategoryLabel}</span> · {activity.actorType === 'LabourGang' ? 'Labour gang' : activity.contractorName || 'Contractor'} · {activity.progressPercent ?? '—'}% · {activity.description}</li>)}</ul> : null}<div className="mt-3 flex flex-wrap gap-2 text-xs text-slate-600">{item.evidence.map((evidence) => <span key={evidence.centralDocumentVersionId} className="rounded bg-slate-100 px-2 py-1">{evidence.evidenceRole}: {evidence.documentReference} v{evidence.versionNumber}</span>)}</div>{item.rejectionReason ? <p className="mt-3 rounded bg-rose-50 p-2 text-sm text-rose-800">Return reason: {item.rejectionReason}</p> : null}{item.reviews.length ? <div className="mt-3 space-y-2 border-t pt-3">{item.reviews.map((review) => <div key={review.sequence} className="text-sm text-slate-600"><span className="font-medium text-slate-900">{review.actorName}</span> · {review.action} · {formatDate(review.createdAt)}<p className="whitespace-pre-wrap break-words">{review.comment}</p>{review.documentReference ? <p className="text-xs">Review evidence: {review.documentReference} v{review.versionNumber}</p> : null}</div>)}</div> : null}{canManage && lookups && pending ? <section className="mt-4 rounded border border-amber-200 bg-amber-50 p-3"><div className="mb-2 flex items-center gap-2 text-sm font-medium text-slate-900"><ShieldCheck className="h-4 w-4 text-amber-700" />Assigned workflow review</div><Textarea rows={2} value={reviewComments[item.id] || ''} onChange={(event) => setReviewComments((current) => ({ ...current, [item.id]: event.target.value }))} placeholder="Record the SCE or HOD technical decision." /><div className="mt-3"><Label>Review evidence (optional)</Label><Select value={reviewDocuments[item.id] || none} onValueChange={(value) => setReviewDocuments((current) => ({ ...current, [item.id]: value === none ? '' : value }))}><SelectTrigger className="mt-1"><SelectValue /></SelectTrigger><SelectContent><SelectItem value={none}>No additional evidence</SelectItem>{lookups.documents.map((document) => <SelectItem key={document.centralDocumentVersionId} value={documentKey(document)}>{document.documentReference} · v{document.versionNumber}</SelectItem>)}</SelectContent></Select></div><div className="mt-3 flex justify-end gap-2"><Button variant="outline" size="sm" disabled={saving} onClick={() => void review(item, false)}>Return</Button><Button size="sm" disabled={saving} onClick={() => void review(item, true)}>Approve / advance</Button></div></section> : null}</article>; })}
    </CardContent>
  </Card>;
}

function documentKey(document?: CivilEngineeringWeeklySupervisionDocument) {
  return document ? `${document.centralDocumentRecordId}:${document.centralDocumentVersionId}` : '';
}
