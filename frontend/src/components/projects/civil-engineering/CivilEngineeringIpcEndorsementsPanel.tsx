'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { BadgeCheck, FileCheck2, RefreshCw, Send, Undo2 } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringIpcEndorsementService } from '@/services/civil-engineering-ipc-endorsement.service';
import type { CivilEngineeringIpcEndorsement, CivilEngineeringIpcEndorsementLookups } from '@/types/civil-engineering-ipc-endorsement';

const workspacePermission = 'civil-engineering.workspace.read';
const supervisionPermission = 'civil-engineering.supervision.manage';
const none = '__none__';
const formatDate = (value?: string | null) => value ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : 'Not available';
const errorText = (error: unknown, fallback: string) => {
  const value = error as { response?: { detail?: string; correlationId?: string }; message?: string };
  return `${value.response?.detail || value.message || fallback}${value.response?.correlationId ? ` Reference: ${value.response.correlationId}` : ''}`;
};

export function CivilEngineeringIpcEndorsementsPanel({ projectId }: { projectId: string }) {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canRead = hasPermission(workspacePermission);
  const canManage = hasPermission(supervisionPermission);
  const [items, setItems] = useState<CivilEngineeringIpcEndorsement[]>([]);
  const [lookups, setLookups] = useState<CivilEngineeringIpcEndorsementLookups>();
  const [certificateId, setCertificateId] = useState(none);
  const [handoffNotes, setHandoffNotes] = useState('');
  const [decisionNotes, setDecisionNotes] = useState<Record<string, string>>({});
  const [documentKeys, setDocumentKeys] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    if (!canRead) { setLoading(false); return; }
    setLoading(true);
    const [reviews, context] = await Promise.allSettled([
      civilEngineeringIpcEndorsementService.list(projectId),
      canManage ? civilEngineeringIpcEndorsementService.lookups(projectId) : Promise.resolve(undefined),
    ]);
    if (reviews.status === 'fulfilled') setItems(reviews.value);
    else toast({ variant: 'destructive', title: 'IPC review history could not be loaded', description: errorText(reviews.reason, 'Refresh the project workspace and try again.') });
    if (context.status === 'fulfilled' && context.value) setLookups(context.value);
    else setLookups(undefined);
    setLoading(false);
  }, [canManage, canRead, projectId, toast]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    if (!lookups?.paymentCertificates.length) return;
    setCertificateId((current) => current === none ? lookups.paymentCertificates[0].id : current);
  }, [lookups]);

  const selectedDocument = useCallback((reviewId: string) => {
    const key = documentKeys[reviewId] || '';
    return lookups?.documents.find((item) => `${item.centralDocumentRecordId}:${item.centralDocumentVersionId}` === key);
  }, [documentKeys, lookups]);

  const submit = async () => {
    if (!lookups || certificateId === none || handoffNotes.trim().length < 5) {
      toast({ variant: 'destructive', title: 'Complete the IPC handoff', description: 'Select the completed QS payment certificate and provide concise handoff notes for the assigned Project Engineer.' });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringIpcEndorsementService.submit(certificateId, { clientRequestId: crypto.randomUUID(), notes: handoffNotes.trim() });
      setHandoffNotes('');
      toast({ title: 'IPC sent for engineering review', description: 'The assigned Project Engineer must now endorse or return the certificate before QS workflow submission.' });
      await load();
    } catch (error) {
      toast({ variant: 'destructive', title: 'IPC was not submitted for review', description: errorText(error, 'Check the coordinator assignment, Project Engineer appointment and effective Civil controls.') });
    } finally { setSaving(false); }
  };

  const review = async (item: CivilEngineeringIpcEndorsement, endorse: boolean) => {
    const notes = decisionNotes[item.id]?.trim() || '';
    const evidence = selectedDocument(item.id);
    if (notes.length < 5 || (endorse && item.requiresEndorsementEvidence && !evidence)) {
      toast({ variant: 'destructive', title: 'Complete the Project Engineer decision', description: endorse && item.requiresEndorsementEvidence ? 'Select current Published DMS evidence and provide an endorsement note.' : 'Provide a decision note of at least 5 characters.' });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringIpcEndorsementService.review(item.id, {
        clientRequestId: crypto.randomUUID(), rowVersion: item.rowVersion, endorse, notes,
        evidenceDocumentRecordId: endorse ? evidence?.centralDocumentRecordId ?? null : null,
        evidenceDocumentVersionId: endorse ? evidence?.centralDocumentVersionId ?? null : null,
      });
      setDecisionNotes((current) => ({ ...current, [item.id]: '' }));
      toast({ title: endorse ? 'IPC endorsed' : 'IPC returned to Projects Coordinator', description: endorse ? 'The existing QS certificate workflow can now be submitted.' : 'The coordinator can amend the Draft certificate and submit a new engineering review.' });
      await load();
    } catch (error) {
      toast({ variant: 'destructive', title: 'Project Engineer decision was not saved', description: errorText(error, 'Only the assigned active Project Engineer can decide the awaiting IPC review.') });
    } finally { setSaving(false); }
  };

  const pending = useMemo(() => items.filter((item) => item.status === 'AwaitingProjectEngineerReview'), [items]);
  if (!canRead) return null;
  return <Card className="border-slate-200/70 shadow-sm">
    <CardHeader className="gap-3 pb-3 sm:flex-row sm:items-start sm:justify-between">
      <div><CardTitle className="flex items-center gap-2 text-base"><FileCheck2 className="h-4 w-4 text-emerald-600" />IPC engineering review</CardTitle><CardDescription>Projects Coordinator sends a completed QS payment certificate to the assigned Project Engineer. The existing QS workflow stays blocked until it is endorsed or returned.</CardDescription></div>
      <Button variant="outline" size="sm" disabled={loading || saving} onClick={() => void load()}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>
    </CardHeader>
    <CardContent className="space-y-4">
      {loading ? <p className="text-sm text-slate-500">Loading governed IPC reviews…</p> : null}
      {canManage && !loading && !lookups ? <p className="rounded border border-slate-200 bg-slate-50 p-3 text-sm text-slate-600">IPC review history remains available. New reviews require an active Project Engineer, configured Projects Coordinator role, effective CIV-CFG-005/CIV-CFG-004 controls and the shared QS certificate workflow.</p> : null}
      {canManage && lookups && lookups.paymentCertificates.length ? <section className="rounded-lg border border-emerald-200 bg-emerald-50/40 p-4"><div className="mb-3 flex items-center gap-2 text-sm font-medium text-slate-900"><Send className="h-4 w-4 text-emerald-700" />Send completed IPC to Project Engineer</div><div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_minmax(0,2fr)_auto] md:items-end"><div className="space-y-1"><Label>QS payment certificate</Label><Select value={certificateId} onValueChange={setCertificateId}><SelectTrigger><SelectValue placeholder="Select completed certificate" /></SelectTrigger><SelectContent>{lookups.paymentCertificates.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Coordinator handoff notes</Label><Textarea rows={2} value={handoffNotes} onChange={(event) => setHandoffNotes(event.target.value)} placeholder="Summarise completion, measurement basis and points for engineering review." /></div><Button size="sm" disabled={saving} onClick={() => void submit()}><Send className="mr-2 h-4 w-4" />Send for review</Button></div></section> : null}
      {!loading && !items.length ? <p className="rounded border border-dashed border-slate-300 bg-slate-50 p-3 text-sm text-slate-600">No Project Engineer IPC review has been recorded for this project.</p> : null}
      {items.map((item) => { const awaiting = item.status === 'AwaitingProjectEngineerReview'; const document = selectedDocument(item.id); return <article key={item.id} className="rounded-lg border border-slate-200 bg-white p-4"><div className="flex flex-col gap-2 lg:flex-row lg:justify-between"><div><div className="flex flex-wrap items-center gap-2"><span className="font-medium text-slate-900">{item.paymentCertificateNumber}</span><Badge variant="outline">Attempt {item.sequence}</Badge><Badge variant={item.status === 'Endorsed' ? 'default' : item.status === 'ReturnedToProjectsCoordinator' ? 'destructive' : 'secondary'}>{item.status === 'ReturnedToProjectsCoordinator' ? 'Returned' : item.status}</Badge></div><p className="mt-2 text-sm text-slate-600">{item.paymentCertificateTitle}</p><p className="mt-2 text-xs text-slate-500">Sent by {item.submittedByName} on {formatDate(item.submittedAt)} · Assigned Project Engineer: {item.projectEngineerName}</p><p className="mt-2 whitespace-pre-wrap break-words text-sm text-slate-700">{item.submissionNotes}</p></div></div>{item.reviewNotes ? <section className="mt-3 rounded bg-slate-50 p-3 text-sm text-slate-700"><span className="font-medium">{item.status === 'Endorsed' ? 'Endorsement' : 'Return'} by {item.reviewedByName || 'Project Engineer'}:</span><p className="mt-1 whitespace-pre-wrap break-words">{item.reviewNotes}</p><p className="mt-1 text-xs text-slate-500">{formatDate(item.reviewedAt)}</p></section> : null}{canManage && lookups && awaiting ? <section className="mt-4 rounded border border-amber-200 bg-amber-50 p-3"><div className="mb-2 flex items-center gap-2 text-sm font-medium text-slate-900"><BadgeCheck className="h-4 w-4 text-amber-700" />Project Engineer decision</div><Textarea rows={2} value={decisionNotes[item.id] || ''} onChange={(event) => setDecisionNotes((current) => ({ ...current, [item.id]: event.target.value }))} placeholder="Record the technical endorsement or the correction required before resubmission." />{item.requiresEndorsementEvidence ? <div className="mt-3 space-y-1"><Label>Current Published DMS endorsement evidence</Label><Select value={documentKeys[item.id] || ''} onValueChange={(value) => setDocumentKeys((current) => ({ ...current, [item.id]: value }))}><SelectTrigger><SelectValue placeholder="Select project or IPC DMS evidence" /></SelectTrigger><SelectContent>{lookups.documents.map((value) => <SelectItem key={value.centralDocumentVersionId} value={`${value.centralDocumentRecordId}:${value.centralDocumentVersionId}`}>{value.documentReference} · {value.title} · {value.versionNumber}</SelectItem>)}</SelectContent></Select></div> : null}<div className="mt-3 flex justify-end gap-2"><Button variant="outline" size="sm" disabled={saving} onClick={() => void review(item, false)}><Undo2 className="mr-2 h-4 w-4" />Return</Button><Button size="sm" disabled={saving} onClick={() => void review(item, true)}><BadgeCheck className="mr-2 h-4 w-4" />Endorse</Button></div></section> : null}</article>; })}
      {pending.length > 0 ? <p className="text-xs text-amber-700">{pending.length} IPC review{pending.length === 1 ? '' : 's'} is awaiting the assigned Project Engineer.</p> : null}
    </CardContent>
  </Card>;
}
