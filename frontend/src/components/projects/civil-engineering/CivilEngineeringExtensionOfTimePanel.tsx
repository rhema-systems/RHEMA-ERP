'use client';

import { useCallback, useEffect, useState } from 'react';
import { Check, Clock3, FileText, RefreshCw, ShieldCheck, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringExtensionOfTimeService } from '@/services/civil-engineering-extension-of-time.service';
import type { CivilEngineeringExtensionOfTimeControl, CivilEngineeringExtensionOfTimeLookups } from '@/types/civil-engineering-extension-of-time';

const workspacePermission = 'civil-engineering.workspace.read';
const commercialPermission = 'civil-engineering.commercial.manage';
const none = '__none__';

type Form = {
  contractId: string; variationId: string; title: string; reason: string; scopeSummary: string; daysRequested: string;
  proposedRevisedCompletionDate: string; hasCostImpact: boolean; evidenceKey: string;
};

const blank = (): Form => ({ contractId: '', variationId: none, title: '', reason: '', scopeSummary: '', daysRequested: '', proposedRevisedCompletionDate: '', hasCostImpact: false, evidenceKey: '' });
const documentKey = (recordId: string, versionId: string) => `${recordId}:${versionId}`;
const errorText = (error: unknown, fallback: string) => {
  const value = error as { response?: { detail?: string; correlationId?: string }; message?: string };
  const message = value.response?.detail || value.message || fallback;
  return `${message}${value.response?.correlationId ? ` Reference: ${value.response.correlationId}` : ''}`;
};

export function CivilEngineeringExtensionOfTimePanel({ projectId }: { projectId: string }) {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canRead = hasPermission(workspacePermission);
  const canManage = hasPermission(commercialPermission);
  const [items, setItems] = useState<CivilEngineeringExtensionOfTimeControl[]>([]);
  const [lookups, setLookups] = useState<CivilEngineeringExtensionOfTimeLookups>();
  const [form, setForm] = useState<Form>(blank);
  const [reviewNotes, setReviewNotes] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    if (!canRead) return;
    setLoading(true);
    try {
      const [values, options] = await Promise.all([
        civilEngineeringExtensionOfTimeService.list(projectId),
        civilEngineeringExtensionOfTimeService.lookups(projectId),
      ]);
      setItems(values); setLookups(options);
    } catch (error) {
      toast({ variant: 'destructive', title: 'Civil EOT controls could not be loaded', description: errorText(error, 'Check your project scope and the effective Civil configuration.') });
    } finally { setLoading(false); }
  }, [canRead, projectId, toast]);

  useEffect(() => { void load(); }, [load]);

  const create = async () => {
    const [evidenceDocumentRecordId, evidenceDocumentVersionId] = form.evidenceKey.split(':');
    const daysRequested = Number(form.daysRequested);
    if (!form.contractId || !form.title.trim() || !form.reason.trim() || !form.scopeSummary.trim() || !Number.isInteger(daysRequested) || daysRequested < 1 || !form.proposedRevisedCompletionDate || !evidenceDocumentRecordId || !evidenceDocumentVersionId || (form.hasCostImpact && form.variationId === none)) {
      toast({ variant: 'destructive', title: 'Complete the controlled EOT request', description: 'Select the Works contract and DMS evidence, provide the scope/time rationale, and select the applied QS variation when the EOT has cost impact.' });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringExtensionOfTimeService.create(projectId, {
        clientRequestId: crypto.randomUUID(), contractId: form.contractId, quantitySurveyVariationOrderId: form.variationId === none ? null : form.variationId,
        title: form.title.trim(), reason: form.reason.trim(), scopeSummary: form.scopeSummary.trim(), daysRequested, proposedRevisedCompletionDate: form.proposedRevisedCompletionDate,
        hasCostImpact: form.hasCostImpact, evidenceDocumentRecordId, evidenceDocumentVersionId,
      });
      setForm(blank()); toast({ title: 'Civil EOT submitted', description: 'The authoritative Projects EOT is now awaiting its configured shared workflow review.' }); await load();
    } catch (error) {
      toast({ variant: 'destructive', title: 'Civil EOT was not submitted', description: errorText(error, 'Recheck the current Works contract, QS variation, DMS evidence and policy.') });
    } finally { setSaving(false); }
  };

  const review = async (item: CivilEngineeringExtensionOfTimeControl, approve: boolean) => {
    const comment = reviewNotes[item.id]?.trim() || '';
    if (comment.length < 3) {
      toast({ variant: 'destructive', title: 'Add a review comment', description: 'The shared workflow records the reviewer comment with the EOT decision.' });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringExtensionOfTimeService.review(item.id, { clientRequestId: crypto.randomUUID(), rowVersion: item.rowVersion, approve, comment });
      toast({ title: approve ? 'Civil EOT advanced' : 'Civil EOT returned', description: 'The Projects EOT status follows the authoritative workflow outcome.' }); await load();
    } catch (error) {
      toast({ variant: 'destructive', title: 'Civil EOT review was not completed', description: errorText(error, 'You may not be the assigned workflow reviewer, or a governed source changed.') });
    } finally { setSaving(false); }
  };

  if (!canRead) return null;
  return <Card className="border-blue-100">
    <CardHeader className="pb-3"><div className="flex items-start justify-between gap-3"><div><CardTitle className="flex items-center gap-2 text-base"><Clock3 className="h-4 w-4 text-blue-600" />Civil variation and extension of time</CardTitle><CardDescription>Extension requests reuse the existing Projects EOT, approved QS variation, active Works contract and central-DMS evidence.</CardDescription></div><Button variant="outline" size="sm" disabled={loading} onClick={() => void load()}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button></div></CardHeader>
    <CardContent className="space-y-5">
      {canManage ? <section className="rounded-lg border bg-muted/20 p-4"><div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <div className="space-y-1"><Label>Works contract</Label><Select value={form.contractId} onValueChange={(contractId) => setForm(value => ({ ...value, contractId }))}><SelectTrigger><SelectValue placeholder="Select contract" /></SelectTrigger><SelectContent>{lookups?.contracts.map(value => <SelectItem key={value.id} value={value.id}>{value.label}</SelectItem>)}</SelectContent></Select></div>
        <div className="space-y-1"><Label>Requested days</Label><Input type="number" min={1} max={3650} value={form.daysRequested} onChange={event => setForm(value => ({ ...value, daysRequested: event.target.value }))} /></div>
        <div className="space-y-1"><Label>Proposed completion</Label><Input type="date" value={form.proposedRevisedCompletionDate} onChange={event => setForm(value => ({ ...value, proposedRevisedCompletionDate: event.target.value }))} /></div>
        <div className="flex items-end gap-2 pb-2"><Switch checked={form.hasCostImpact} onCheckedChange={hasCostImpact => setForm(value => ({ ...value, hasCostImpact, variationId: hasCostImpact ? value.variationId : none }))} /><Label>Has cost impact</Label></div>
        <div className="space-y-1 xl:col-span-2"><Label>Title</Label><Input value={form.title} onChange={event => setForm(value => ({ ...value, title: event.target.value }))} placeholder="EOT title" /></div>
        <div className="space-y-1 xl:col-span-2"><Label>QS variation {form.hasCostImpact ? '(required)' : '(if applicable)'}</Label><Select value={form.variationId} onValueChange={variationId => setForm(value => ({ ...value, variationId }))}><SelectTrigger><SelectValue placeholder="Select applied QS variation" /></SelectTrigger><SelectContent><SelectItem value={none}>No linked QS variation</SelectItem>{lookups?.quantitySurveyVariations.map(value => <SelectItem key={value.id} value={value.id}>{value.label}</SelectItem>)}</SelectContent></Select></div>
        <div className="space-y-1 xl:col-span-2"><Label>Extension reason</Label><Textarea value={form.reason} onChange={event => setForm(value => ({ ...value, reason: event.target.value }))} placeholder="Narrative reason for the EOT" /></div>
        <div className="space-y-1 xl:col-span-2"><Label>Affected scope</Label><Textarea value={form.scopeSummary} onChange={event => setForm(value => ({ ...value, scopeSummary: event.target.value }))} placeholder="Affected works and time impact" /></div>
        <div className="space-y-1 xl:col-span-3"><Label>Published DMS evidence</Label><Select value={form.evidenceKey} onValueChange={evidenceKey => setForm(value => ({ ...value, evidenceKey }))}><SelectTrigger><SelectValue placeholder="Select supporting document version" /></SelectTrigger><SelectContent>{lookups?.documents.map(value => <SelectItem key={documentKey(value.centralDocumentRecordId, value.centralDocumentVersionId)} value={documentKey(value.centralDocumentRecordId, value.centralDocumentVersionId)}>{value.documentReference} — {value.title} v{value.versionNumber}</SelectItem>)}</SelectContent></Select></div>
        <div className="flex items-end"><Button className="w-full" disabled={saving || loading} onClick={() => void create()}><ShieldCheck className="mr-2 h-4 w-4" />Submit EOT</Button></div>
      </div></section> : null}
      <div className="space-y-3">{items.length === 0 ? <p className="py-3 text-sm text-muted-foreground">No governed Civil extension-of-time request has been submitted for this project.</p> : items.map(item => <article key={item.id} className="rounded-lg border p-4"><div className="flex flex-col gap-2 lg:flex-row lg:items-start lg:justify-between"><div><p className="font-medium">{item.referenceNumber} · {item.title}</p><p className="text-sm text-muted-foreground">{item.contractLabel} · {item.daysRequested} requested day(s) · {item.status}</p><p className="mt-1 text-sm">{item.scopeSummary}</p></div><div className="text-sm text-muted-foreground"><p>{item.hasCostImpact ? item.quantitySurveyVariationLabel || 'Cost impact' : 'No cost impact'}</p><p className="flex items-center gap-1"><FileText className="h-3.5 w-3.5" />{item.evidenceDocumentReference} v{item.evidenceVersionNumber}</p></div></div>
        {item.status === 'PendingApproval' && canManage ? <div className="mt-3 grid gap-2 md:grid-cols-[1fr_auto_auto]"><Textarea value={reviewNotes[item.id] || ''} onChange={event => setReviewNotes(value => ({ ...value, [item.id]: event.target.value }))} placeholder="Workflow review comment" /><Button variant="outline" disabled={saving} onClick={() => void review(item, false)}><X className="mr-1 h-4 w-4" />Return</Button><Button disabled={saving} onClick={() => void review(item, true)}><Check className="mr-1 h-4 w-4" />Approve / advance</Button></div> : null}
        {item.rejectionReason ? <p className="mt-2 text-sm text-destructive">{item.rejectionReason}</p> : null}
      </article>)}</div>
    </CardContent>
  </Card>;
}
