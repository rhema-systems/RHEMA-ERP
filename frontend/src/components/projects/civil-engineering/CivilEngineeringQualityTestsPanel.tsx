'use client';

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { BadgeCheck, ClipboardCheck, FileCheck2, RefreshCw, Send, ShieldAlert } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringQualityTestService } from '@/services/civil-engineering-quality-test.service';
import type { CivilEngineeringQualityTestLookups, CivilEngineeringQualityTestReport } from '@/types/civil-engineering-quality-test';

const workspacePermission = 'civil-engineering.workspace.read';
const supervisionPermission = 'civil-engineering.supervision.manage';
const none = '__none__';
const formatDate = (value?: string | null) => value ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : 'Not available';
const errorText = (error: unknown, fallback: string) => {
  const value = error as { response?: { detail?: string; correlationId?: string }; message?: string };
  return `${value.response?.detail || value.message || fallback}${value.response?.correlationId ? ` Reference: ${value.response.correlationId}` : ''}`;
};
type Form = {
  reportReference: string; testCategory: string; sourceType: string; sourceBusinessPartnerId: string;
  testedAt: string; resultStatus: string; resultSummary: string; reviewerUserId: string;
  projectPackageId: string; projectPaymentCertificateId: string; documentKey: string;
};
const blank: Form = { reportReference: '', testCategory: '', sourceType: '', sourceBusinessPartnerId: '', testedAt: '', resultStatus: '', resultSummary: '', reviewerUserId: '', projectPackageId: none, projectPaymentCertificateId: none, documentKey: '' };
type Props = { projectId: string };

export function CivilEngineeringQualityTestsPanel({ projectId }: Props) {
  const { toast } = useToast();
  const { hasPermission } = useAuth();
  const canRead = hasPermission(workspacePermission);
  const canManage = hasPermission(supervisionPermission);
  const [items, setItems] = useState<CivilEngineeringQualityTestReport[]>([]);
  const [lookups, setLookups] = useState<CivilEngineeringQualityTestLookups>();
  const [form, setForm] = useState<Form>(blank);
  const [decisionReason, setDecisionReason] = useState<Record<string, string>>({});
  const [endorsementKeys, setEndorsementKeys] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    if (!canRead) { setLoading(false); return; }
    setLoading(true);
    const [reports, context] = await Promise.allSettled([
      civilEngineeringQualityTestService.list(projectId),
      canManage ? civilEngineeringQualityTestService.lookups(projectId) : Promise.resolve(undefined),
    ]);
    if (reports.status === 'fulfilled') setItems(reports.value);
    else toast({ variant: 'destructive', title: 'Test reports could not be loaded', description: errorText(reports.reason, 'Refresh the project workspace and try again.') });
    if (context.status === 'fulfilled' && context.value) setLookups(context.value);
    else setLookups(undefined);
    setLoading(false);
  }, [canManage, canRead, projectId, toast]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    if (!lookups) return;
    setForm((current) => ({
      ...current,
      testCategory: current.testCategory || String(lookups.testCategories[0]?.value ?? ''),
      sourceType: current.sourceType || lookups.sourceTypes[0] || '',
      resultStatus: current.resultStatus || lookups.resultStatuses[0] || '',
      reviewerUserId: current.reviewerUserId || lookups.reviewers[0]?.id || '',
      documentKey: current.documentKey || (lookups.documents[0] ? `${lookups.documents[0].centralDocumentRecordId}:${lookups.documents[0].centralDocumentVersionId}` : ''),
    }));
  }, [lookups]);

  const selectedDocument = useMemo(() => lookups?.documents.find((item) => `${item.centralDocumentRecordId}:${item.centralDocumentVersionId}` === form.documentKey), [form.documentKey, lookups]);
  const externalSource = form.sourceType !== 'Internal';
  const create = async () => {
    const document = selectedDocument;
    if (!lookups || !document || !form.reportReference.trim() || !form.testCategory || !form.sourceType || !form.testedAt || !form.resultStatus || form.resultSummary.trim().length < 3 || !form.reviewerUserId || (externalSource && !form.sourceBusinessPartnerId)) {
      toast({ variant: 'destructive', title: 'Complete the governed test report', description: 'Use the controlled category, source, partner, reviewer and current Published DMS evidence selectors.' });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringQualityTestService.create(projectId, {
        clientRequestId: crypto.randomUUID(), reportReference: form.reportReference.trim(), testCategory: Number(form.testCategory), sourceType: form.sourceType,
        sourceBusinessPartnerId: externalSource ? form.sourceBusinessPartnerId : null, testedAt: new Date(form.testedAt).toISOString(), resultStatus: form.resultStatus,
        resultSummary: form.resultSummary.trim(), reviewerUserId: form.reviewerUserId, projectPackageId: form.projectPackageId === none ? null : form.projectPackageId,
        projectPaymentCertificateId: form.projectPaymentCertificateId === none ? null : form.projectPaymentCertificateId,
        centralDocumentRecordId: document.centralDocumentRecordId, centralDocumentVersionId: document.centralDocumentVersionId,
      });
      setForm(blank);
      toast({ title: 'Test report submitted', description: 'The DMS-backed report is now awaiting its controlled independent workflow review.' });
      await load();
    } catch (error) {
      toast({ variant: 'destructive', title: 'Test report was not submitted', description: errorText(error, 'Check the controlled selections and effective Civil test policy.') });
    } finally { setSaving(false); }
  };

  const endorsementDocument = useCallback((reportId: string) => {
    const key = endorsementKeys[reportId] || (lookups?.documents[0] ? `${lookups.documents[0].centralDocumentRecordId}:${lookups.documents[0].centralDocumentVersionId}` : '');
    return lookups?.documents.find((item) => `${item.centralDocumentRecordId}:${item.centralDocumentVersionId}` === key);
  }, [endorsementKeys, lookups]);

  const review = async (report: CivilEngineeringQualityTestReport, approve: boolean) => {
    const reason = decisionReason[report.id]?.trim() || '';
    const document = endorsementDocument(report.id);
    if (reason.length < 3 || (approve && lookups?.requiresEndorsementEvidence && !document)) {
      toast({ variant: 'destructive', title: 'Complete the independent review', description: approve && lookups?.requiresEndorsementEvidence ? 'Select the current Published DMS endorsement evidence and provide the reviewer decision.' : 'Provide a concise reviewer decision.' });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringQualityTestService.review(report.id, {
        clientRequestId: crypto.randomUUID(), rowVersion: report.rowVersion, approve, reason,
        endorsementDocumentRecordId: approve ? document?.centralDocumentRecordId ?? null : null,
        endorsementDocumentVersionId: approve ? document?.centralDocumentVersionId ?? null : null,
      });
      setDecisionReason((current) => ({ ...current, [report.id]: '' }));
      toast({ title: approve ? 'Test report endorsed' : 'Test report rejected', description: approve ? 'The shared quality-test workflow recorded the controlled endorsement.' : 'The reviewer decision and reason were recorded.' });
      await load();
    } catch (error) {
      toast({ variant: 'destructive', title: 'Test-report review was not saved', description: errorText(error, 'Only the assigned independent reviewer at the active workflow step can decide.') });
    } finally { setSaving(false); }
  };

  if (!canRead) return null;
  return (
    <Card className="border-slate-200/70 shadow-sm">
      <CardHeader className="gap-3 pb-3 sm:flex-row sm:items-start sm:justify-between">
        <div><CardTitle className="flex items-center gap-2 text-base"><ClipboardCheck className="h-4 w-4 text-emerald-600" />Engineering test reports</CardTitle><CardDescription>Controlled laboratory, field and material test evidence linked to project packages or QS payment certificates and independently endorsed through Workflow.</CardDescription></div>
        <Button variant="outline" size="sm" onClick={() => void load()} disabled={loading || saving}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>
      </CardHeader>
      <CardContent className="space-y-4">
        {loading ? <p className="text-sm text-slate-500">Loading governed test reports…</p> : null}
        {canManage && !loading && !lookups ? <p className="rounded border border-slate-200 bg-slate-50 p-3 text-sm text-slate-600">Test-report history is available, but controlled registration requires an effective CIV-CFG-011 policy, published quality-test workflow, active reviewer roles and published DMS template.</p> : null}
        {canManage && lookups ? <section className="rounded-lg border border-emerald-200 bg-emerald-50/40 p-4"><div className="mb-3 flex items-center gap-2 text-sm font-medium text-slate-900"><FileCheck2 className="h-4 w-4 text-emerald-700" />Register a DMS-backed engineering test report</div><div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <div className="space-y-1"><Label>Report reference</Label><Input value={form.reportReference} onChange={(event) => setForm((current) => ({ ...current, reportReference: event.target.value }))} placeholder="Laboratory report number" /></div>
          <div className="space-y-1"><Label>Test category / type</Label><Select value={form.testCategory} onValueChange={(value) => setForm((current) => ({ ...current, testCategory: value }))}><SelectTrigger><SelectValue placeholder="Select category" /></SelectTrigger><SelectContent>{lookups.testCategories.map((item) => <SelectItem key={item.value} value={String(item.value)}>{item.label}</SelectItem>)}</SelectContent></Select></div>
          <div className="space-y-1"><Label>Controlled source type</Label><Select value={form.sourceType} onValueChange={(value) => setForm((current) => ({ ...current, sourceType: value, sourceBusinessPartnerId: value === 'Internal' ? '' : current.sourceBusinessPartnerId }))}><SelectTrigger><SelectValue placeholder="Select source" /></SelectTrigger><SelectContent>{lookups.sourceTypes.map((item) => <SelectItem key={item} value={item}>{item}</SelectItem>)}</SelectContent></Select></div>
          <div className="space-y-1"><Label>Tested date</Label><Input type="datetime-local" value={form.testedAt} onChange={(event) => setForm((current) => ({ ...current, testedAt: event.target.value }))} /></div>
          {externalSource ? <div className="space-y-1"><Label>Laboratory / source partner</Label><Select value={form.sourceBusinessPartnerId} onValueChange={(value) => setForm((current) => ({ ...current, sourceBusinessPartnerId: value }))}><SelectTrigger><SelectValue placeholder="Select active partner" /></SelectTrigger><SelectContent>{lookups.sourcePartners.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div> : null}
          <div className="space-y-1"><Label>Result</Label><Select value={form.resultStatus} onValueChange={(value) => setForm((current) => ({ ...current, resultStatus: value }))}><SelectTrigger><SelectValue placeholder="Select result" /></SelectTrigger><SelectContent>{lookups.resultStatuses.map((item) => <SelectItem key={item} value={item}>{item}</SelectItem>)}</SelectContent></Select></div>
          <div className="space-y-1"><Label>Independent reviewer</Label><Select value={form.reviewerUserId} onValueChange={(value) => setForm((current) => ({ ...current, reviewerUserId: value }))}><SelectTrigger><SelectValue placeholder="Select reviewer" /></SelectTrigger><SelectContent>{lookups.reviewers.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div>
          <div className="space-y-1"><Label>Work package (optional)</Label><Select value={form.projectPackageId} onValueChange={(value) => setForm((current) => ({ ...current, projectPackageId: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value={none}>Not linked</SelectItem>{lookups.projectPackages.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div>
          <div className="space-y-1"><Label>QS payment certificate (optional)</Label><Select value={form.projectPaymentCertificateId} onValueChange={(value) => setForm((current) => ({ ...current, projectPaymentCertificateId: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value={none}>Not linked</SelectItem>{lookups.paymentCertificates.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div>
          <div className="space-y-1 md:col-span-2"><Label>Current Published DMS test evidence</Label><Select value={form.documentKey} onValueChange={(value) => setForm((current) => ({ ...current, documentKey: value }))}><SelectTrigger><SelectValue placeholder="Select governed evidence" /></SelectTrigger><SelectContent>{lookups.documents.map((item) => <SelectItem key={item.centralDocumentVersionId} value={`${item.centralDocumentRecordId}:${item.centralDocumentVersionId}`}>{item.documentReference} · v{item.versionNumber}</SelectItem>)}</SelectContent></Select></div>
          <div className="space-y-1 md:col-span-2"><Label>Result summary</Label><Textarea rows={2} value={form.resultSummary} onChange={(event) => setForm((current) => ({ ...current, resultSummary: event.target.value }))} placeholder="Record the measured outcome, observation and relevant technical context." /></div>
        </div><div className="mt-3 flex justify-end"><Button size="sm" disabled={saving || !lookups.documents.length || !lookups.reviewers.length} onClick={() => void create()}><Send className="mr-2 h-4 w-4" />Submit for independent review</Button></div></section> : null}
        {!loading && !items.length ? <p className="rounded border border-dashed border-slate-300 bg-slate-50 p-3 text-sm text-slate-600">No governed engineering test reports have been registered for this project.</p> : null}
        {items.map((item) => { const pending = item.status === 'PendingApproval' && item.approvalStatus === 'Pending'; const document = endorsementDocument(item.id); return <article key={item.id} className="rounded-lg border border-slate-200 bg-white p-4"><div className="flex flex-col gap-2 lg:flex-row lg:justify-between"><div><div className="flex flex-wrap items-center gap-2"><span className="font-medium text-slate-900">{item.reportReference}</span><Badge variant="outline">{item.testCategoryLabel}</Badge><Badge variant={item.resultStatus === 'Fail' ? 'destructive' : item.resultStatus === 'Pass' ? 'default' : 'secondary'}>{item.resultStatus}</Badge><Badge variant={item.approvalStatus === 'Approved' ? 'default' : item.approvalStatus === 'Rejected' ? 'destructive' : 'secondary'}>{item.approvalStatus}</Badge>{item.acceptanceBlocked ? <Badge variant="destructive"><ShieldAlert className="mr-1 h-3 w-3" />Acceptance blocked</Badge> : null}</div><p className="mt-2 whitespace-pre-wrap break-words text-sm text-slate-600">{item.resultSummary}</p><p className="mt-2 text-xs text-slate-500">{item.sourceType}{item.sourceBusinessPartnerName ? ` · ${item.sourceBusinessPartnerName}` : ''} · Tested {formatDate(item.testedAt)} · Reviewer: {item.reviewerName}{item.projectPackageName ? ` · ${item.projectPackageName}` : ''}{item.paymentCertificateNumber ? ` · IPC ${item.paymentCertificateNumber}` : ''}</p></div></div>{item.rejectionReason ? <p className="mt-3 rounded bg-rose-50 p-2 text-sm text-rose-800">Reviewer reason: {item.rejectionReason}</p> : null}{canManage && lookups && pending ? <section className="mt-4 rounded border border-amber-200 bg-amber-50 p-3"><div className="mb-2 flex items-center gap-2 text-sm font-medium text-slate-900"><BadgeCheck className="h-4 w-4 text-amber-700" />Independent reviewer decision</div><Textarea rows={2} value={decisionReason[item.id] || ''} onChange={(event) => setDecisionReason((current) => ({ ...current, [item.id]: event.target.value }))} placeholder="Record the endorsement or rejection decision." />{lookups.requiresEndorsementEvidence ? <div className="mt-3 space-y-1"><Label>Current Published DMS endorsement evidence</Label><Select value={endorsementKeys[item.id] || (document ? `${document.centralDocumentRecordId}:${document.centralDocumentVersionId}` : '')} onValueChange={(value) => setEndorsementKeys((current) => ({ ...current, [item.id]: value }))}><SelectTrigger><SelectValue placeholder="Select endorsement evidence" /></SelectTrigger><SelectContent>{lookups.documents.map((value) => <SelectItem key={value.centralDocumentVersionId} value={`${value.centralDocumentRecordId}:${value.centralDocumentVersionId}`}>{value.documentReference} · v{value.versionNumber}</SelectItem>)}</SelectContent></Select></div> : null}<div className="mt-3 flex justify-end gap-2"><Button variant="outline" size="sm" disabled={saving} onClick={() => void review(item, false)}>Reject</Button><Button size="sm" disabled={saving} onClick={() => void review(item, true)}>Endorse</Button></div></section> : null}</article>; })}
      </CardContent>
    </Card>
  );
}
