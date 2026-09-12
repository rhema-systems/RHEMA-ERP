'use client';

import Link from 'next/link';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useParams } from 'next/navigation';
import { ArrowLeft, CheckCircle2, FileCheck2, Loader2, RefreshCw, ShieldAlert } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useWorkflowSummary } from '@/hooks/useWorkflowSummary';
import {
  getPrequalificationActions,
  prequalificationApplicationStatusLabel,
  prequalificationStatusLabel,
  qualifiedEntryStatusLabel,
} from '@/lib/procurement-prequalification';
import { procurementPrequalificationService as service } from '@/services/procurement-prequalification.service';
import {
  ProcurementPrequalificationApplicationStatus as ApplicationStatus,
  ProcurementPrequalificationStatus as Status,
  type ProcurementPrequalificationApplication,
  type ProcurementPrequalificationExercise,
  type ProcurementPrequalificationReadiness,
} from '@/types/procurement-prequalification';

const formatDate = (value?: string) => value ? new Date(value).toLocaleString() : '—';

export default function PrequalificationDetailPage() {
  const { id } = useParams<{ id: string }>();
  const workflow = useWorkflowSummary({ entityType: 'ProcurementSourcing', entityId: id });
  const { hasPermission } = useAuth();
  const canManage = hasPermission('procurement.sourcing.manage');
  const canEvaluate = hasPermission('procurement.tender.evaluate');
  const canApprove = hasPermission('procurement.sourcing.approve');
  const [exercise, setExercise] = useState<ProcurementPrequalificationExercise | null>(null);
  const [readiness, setReadiness] = useState<ProcurementPrequalificationReadiness | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [busy, setBusy] = useState<string | null>(null);
  const [advertisementReference, setAdvertisementReference] = useState('');
  const [advertisementEvidence, setAdvertisementEvidence] = useState('');
  const [supplierId, setSupplierId] = useState('');
  const [categoryIds, setCategoryIds] = useState<string[]>([]);
  const [applicationEvidence, setApplicationEvidence] = useState<Record<string, { evidenceReference: string; verificationReference: string }>>({});
  const [evaluation, setEvaluation] = useState<ProcurementPrequalificationApplication | null>(null);
  const [scores, setScores] = useState<Record<string, { score: number; meetsRequirement: boolean; reason: string; evidenceReference: string }>>({});
  const [evaluationRemarks, setEvaluationRemarks] = useState('');
  const [recommendationEvidence, setRecommendationEvidence] = useState('');
  const [decisionReference, setDecisionReference] = useState('');
  const [decisionEvidence, setDecisionEvidence] = useState('');
  const [decisionReason, setDecisionReason] = useState('');

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setLoadError('');
    try {
      const [nextExercise, nextReadiness] = await Promise.all([service.get(id), service.readiness()]);
      setExercise(nextExercise);
      setReadiness(nextReadiness);
      setAdvertisementReference(nextExercise.advertisementReference ?? '');
      setAdvertisementEvidence(nextExercise.advertisementEvidenceReference ?? '');
      setDecisionReference(nextExercise.decisionReference ?? '');
      setDecisionEvidence(nextExercise.decisionEvidenceReference ?? '');
      setDecisionReason(nextExercise.decisionReason ?? '');
      setApplicationEvidence(Object.fromEntries(nextExercise.criteria.filter((item) => item.requiresEvidence)
        .map((item) => [item.code, { evidenceReference: '', verificationReference: '' }])));
    } catch (error) {
      setLoadError(error instanceof Error ? error.message : 'Unable to load the prequalification record.');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => { void load(); }, [load]);
  const actions = useMemo(() => exercise ? getPrequalificationActions(exercise) : null, [exercise]);

  const run = async (key: string, action: () => Promise<unknown>, success: string) => {
    try {
      setBusy(key);
      await action();
      toast.success(success);
      await Promise.all([load(), workflow.refresh()]);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Prequalification action failed');
    } finally {
      setBusy(null);
    }
  };

  const openEvaluation = (application: ProcurementPrequalificationApplication) => {
    if (!exercise) return;
    setEvaluation(application);
    setEvaluationRemarks('');
    setRecommendationEvidence('');
    setScores(Object.fromEntries(exercise.criteria.map((criterion) => [criterion.id, {
      score: criterion.minimumScore,
      meetsRequirement: true,
      reason: '',
      evidenceReference: '',
    }])));
  };

  const submitApplication = () => {
    if (!exercise || !supplierId || !categoryIds.length) { toast.error('Select a supplier and at least one advertised category.'); return; }
    const evidence = exercise.criteria.filter((item) => item.requiresEvidence).map((item) => ({
      criterionCode: item.code,
      evidenceReference: applicationEvidence[item.code]?.evidenceReference ?? '',
      verificationReference: applicationEvidence[item.code]?.verificationReference ?? '',
    }));
    if (evidence.some((item) => !item.evidenceReference.trim() || !item.verificationReference.trim())) {
      toast.error('Complete every mandatory evidence and verification reference.'); return;
    }
    void run('apply', () => service.apply(exercise.id, { businessPartnerId: supplierId, categoryIds, evidence }), 'Application submitted and sealed');
  };

  const submitEvaluation = () => {
    if (!exercise || !evaluation) return;
    const requestScores = exercise.criteria.map((criterion) => ({
      criterionId: criterion.id,
      score: scores[criterion.id]?.score ?? 0,
      meetsRequirement: scores[criterion.id]?.meetsRequirement ?? false,
      reason: scores[criterion.id]?.reason ?? '',
      evidenceReference: scores[criterion.id]?.evidenceReference || undefined,
    }));
    if (!evaluationRemarks.trim() || !recommendationEvidence.trim() || requestScores.some((item) => !item.reason.trim())) {
      toast.error('Evaluation remarks, signed recommendation evidence, and every score reason are required.'); return;
    }
    if (exercise.criteria.some((criterion) => criterion.requiresEvidence && !scores[criterion.id]?.evidenceReference.trim())) {
      toast.error('Attach evaluator evidence for each evidence-controlled criterion.'); return;
    }
    void run('evaluate', () => service.evaluate(exercise.id, evaluation.id, {
      remarks: evaluationRemarks,
      recommendationEvidenceReference: recommendationEvidence,
      rowVersion: evaluation.rowVersion,
      scores: requestScores,
    }), 'Application evaluation recorded').then(() => setEvaluation(null));
  };

  if (loading) return <div className="flex min-h-[50vh] items-center justify-center"><Loader2 className="h-7 w-7 animate-spin" /></div>;
  if (loadError || !exercise || !readiness || !actions) return <div className="space-y-4 p-6"><Button variant="ghost" asChild><Link href="/procurement/prequalification"><ArrowLeft className="mr-2 h-4 w-4" />Prequalification</Link></Button><Card><CardContent className="p-6 text-sm text-destructive">{loadError || 'Record unavailable.'}</CardContent></Card></div>;

  return (
    <div className="space-y-6 p-6" data-testid="prequalification-detail-page">
      {workflow.error && <div role="alert" className="flex items-center gap-3 rounded border p-3 text-sm text-destructive">
        {workflow.error}<Button variant="outline" size="sm" onClick={() => void workflow.refresh()}>Retry workflow setup</Button>
      </div>}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div><Button variant="ghost" asChild className="mb-2 px-0"><Link href="/procurement/prequalification"><ArrowLeft className="mr-2 h-4 w-4" />Prequalification</Link></Button><h1 className="text-2xl font-semibold">{exercise.reference} · {exercise.title}</h1><p className="text-sm text-muted-foreground">{exercise.description}</p></div>
        <div className="flex items-center gap-2"><Badge variant={exercise.status === Status.Approved ? 'default' : exercise.status >= Status.Rejected ? 'destructive' : 'secondary'}>{exercise.approvalRequired === false && exercise.status === Status.Approved ? 'Qualification complete' : prequalificationStatusLabel[exercise.status]}</Badge><Button variant="outline" size="sm" onClick={() => void load()}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button></div>
      </div>

      <div className="grid gap-3 md:grid-cols-4">
        <Summary label="Application window" value={`${formatDate(exercise.opensAtUtc)} — ${formatDate(exercise.closesAtUtc)}`} />
        <Summary label="Applications" value={String(exercise.applicationCount)} />
        <Summary label="Active qualified entries" value={String(exercise.qualifiedCount)} />
        <Summary label="Integrity" value={`${exercise.integrityHash.slice(0, 16)}…`} />
      </div>

      <Card data-testid="prequalification-milestones"><CardHeader><CardTitle className="flex items-center gap-2"><CheckCircle2 className="h-5 w-5" />Immutable control history</CardTitle></CardHeader><CardContent className="grid gap-3 md:grid-cols-2">{exercise.milestones.map((item) => <div key={item.code} className="rounded border p-3" data-testid={`milestone-${item.code}`}><div className="flex justify-between gap-2"><span className="font-medium">{item.label}</span><Badge variant={item.completedAtUtc ? 'default' : 'outline'}>{item.completedAtUtc ? 'Complete' : 'Pending'}</Badge></div><p className="mt-1 text-xs text-muted-foreground">{formatDate(item.completedAtUtc)}{item.reference ? ` · ${item.reference}` : ''}</p></div>)}</CardContent></Card>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card><CardHeader><CardTitle>Locked categories and criteria</CardTitle></CardHeader><CardContent className="space-y-3"><div className="flex flex-wrap gap-2">{exercise.categories.map((item) => <Badge key={item.id} variant="outline">{item.code} · {item.name}</Badge>)}</div>{exercise.criteria.map((item) => <div key={item.id} className="rounded border p-3 text-sm"><div className="flex justify-between gap-2"><strong>{item.code} · {item.name}</strong><span>{item.weight}%</span></div><p className="text-xs text-muted-foreground">Minimum {item.minimumScore}% · {item.isMandatory ? 'Mandatory' : 'Optional'} · {item.requiresEvidence ? 'Verified evidence required' : 'No evidence requirement'}</p></div>)}</CardContent></Card>
        <Card><CardHeader><CardTitle>Source policy, advertisement, and decision lineage</CardTitle></CardHeader><CardContent className="space-y-2 text-sm"><Line label="Source policy" value={`${exercise.policySetCode} · v${exercise.policySetVersion}`} /><Line label="Source configuration" value={exercise.sourceConfigurationProfileId} /><Line label="Advertisement" value={exercise.advertisementReference} /><Line label="Advertisement evidence" value={exercise.advertisementEvidenceReference} />{exercise.workflowInstanceId && <Line label="Workflow instance" value={exercise.workflowInstanceId} />}<Line label="Decision" value={exercise.decisionReference} /><Line label="Decision evidence" value={exercise.decisionEvidenceReference} /><Line label="Decision reason" value={exercise.decisionReason} /><Line label="Qualified-list expiry" value={formatDate(exercise.expiresAtUtc)} /></CardContent></Card>
      </div>

      {actions.canAdvertise && canManage && <ActionCard title="Publish approved advertisement"><div className="grid gap-3 md:grid-cols-2"><Field label="Advertisement reference"><Input value={advertisementReference} onChange={(event) => setAdvertisementReference(event.target.value)} /></Field><Field label="Publication evidence"><Input value={advertisementEvidence} onChange={(event) => setAdvertisementEvidence(event.target.value)} /></Field></div><Button disabled={busy !== null} onClick={() => void run('advertise', () => service.advertise(exercise.id, { advertisementReference, advertisementEvidenceReference: advertisementEvidence, rowVersion: exercise.rowVersion }), 'Advertisement published')}>Publish advertisement</Button></ActionCard>}

      {actions.canApply && <ActionCard title="Submit controlled supplier application"><div className="grid gap-3 md:grid-cols-2"><Field label="Supplier account"><Select value={supplierId} onValueChange={setSupplierId}><SelectTrigger><SelectValue placeholder="Select supplier" /></SelectTrigger><SelectContent>{readiness.suppliers.map((item) => <SelectItem key={item.id} value={item.id}>{item.code} · {item.name}</SelectItem>)}</SelectContent></Select></Field><div className="space-y-2"><Label>Applied categories</Label>{exercise.categories.map((item) => <label key={item.id} className="mr-3 inline-flex items-center gap-2 text-sm"><Checkbox checked={categoryIds.includes(item.id)} onCheckedChange={(checked) => setCategoryIds((current) => checked ? [...current, item.id] : current.filter((id) => id !== item.id))} />{item.code}</label>)}</div></div><div className="space-y-3">{exercise.criteria.filter((item) => item.requiresEvidence).map((item) => <div key={item.id} className="grid gap-3 rounded border p-3 md:grid-cols-2"><Field label={`${item.name} evidence`}><Input value={applicationEvidence[item.code]?.evidenceReference ?? ''} onChange={(event) => setApplicationEvidence((current) => ({ ...current, [item.code]: { ...current[item.code], evidenceReference: event.target.value } }))} /></Field><Field label="Verified shared-evidence reference"><Input value={applicationEvidence[item.code]?.verificationReference ?? ''} onChange={(event) => setApplicationEvidence((current) => ({ ...current, [item.code]: { ...current[item.code], verificationReference: event.target.value } }))} /></Field></div>)}</div><Button disabled={busy !== null} onClick={submitApplication}>Submit application</Button></ActionCard>}

      {actions.canClose && canManage && <ActionCard title="Close advertised submissions"><p className="text-sm text-muted-foreground">The advertised deadline has elapsed. Closing freezes the application population for evaluation.</p><Button disabled={busy !== null} onClick={() => void run('close', () => service.close(exercise.id, exercise.rowVersion), 'Submissions closed')}>Close submissions</Button></ActionCard>}
      {exercise.status === Status.Advertised && !actions.canClose && <Card><CardContent className="flex items-center gap-3 p-4 text-sm text-amber-800"><ShieldAlert className="h-5 w-5" />Submission closure remains blocked until {formatDate(exercise.closesAtUtc)}.</CardContent></Card>}

      <Card data-testid="prequalification-applications"><CardHeader><CardTitle>Application and evaluation history</CardTitle></CardHeader><CardContent>{exercise.applications.length === 0 ? <p className="text-sm text-muted-foreground">No applications submitted.</p> : <Table><TableHeader><TableRow><TableHead>Application</TableHead><TableHead>Supplier</TableHead><TableHead>Status</TableHead><TableHead>Score</TableHead><TableHead>Submitted</TableHead><TableHead /></TableRow></TableHeader><TableBody>{exercise.applications.map((item) => <TableRow key={item.id}><TableCell className="font-medium">{item.applicationNumber}</TableCell><TableCell>{item.supplierName}</TableCell><TableCell><Badge variant={item.passed === false ? 'destructive' : item.passed === true ? 'default' : 'secondary'}>{prequalificationApplicationStatusLabel[item.status]}</Badge></TableCell><TableCell>{item.totalScore == null ? '—' : `${item.totalScore}%`}</TableCell><TableCell>{formatDate(item.submittedAtUtc)}</TableCell><TableCell className="text-right">{actions.canEvaluate && canEvaluate && item.status === ApplicationStatus.Submitted && <Button size="sm" variant="outline" onClick={() => openEvaluation(item)}>Evaluate</Button>}</TableCell></TableRow>)}</TableBody></Table>}</CardContent></Card>

      {actions.canSubmitDecision && canManage && <ActionCard title={workflow.visibility.direct ? 'Complete qualification' : 'Submit qualification decision'}>
        <p className="text-sm text-muted-foreground">{workflow.visibility.direct ? 'Review the completed scorecards and record the signed decision before creating the qualified list.' : 'Send the completed scorecards through the configured approval process.'}</p>
        {workflow.visibility.direct && <>
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="Decision reference"><Input aria-label="Decision reference" value={decisionReference} onChange={(event) => setDecisionReference(event.target.value)} /></Field>
            <Field label="Signed decision evidence"><Input aria-label="Signed decision evidence" value={decisionEvidence} onChange={(event) => setDecisionEvidence(event.target.value)} /></Field>
          </div>
          <Field label="Decision reason"><Textarea aria-label="Decision reason" value={decisionReason} onChange={(event) => setDecisionReason(event.target.value)} /></Field>
        </>}
        <Button disabled={busy !== null || !workflow.visibility.known || (workflow.visibility.direct && (!decisionReference.trim() || !decisionEvidence.trim() || !decisionReason.trim()))}
          onClick={() => void run('submit-decision', () => service.submitDecision(exercise.id, exercise.rowVersion, workflow.visibility.direct ? {
            decisionReference, decisionEvidenceReference: decisionEvidence, reason: decisionReason,
          } : undefined), workflow.visibility.direct ? 'Qualification completed' : 'Decision workflow submitted')}>
          {workflow.visibility.direct ? 'Complete qualification' : 'Submit for approval'}
        </Button>
      </ActionCard>}

      {actions.canDecide && workflow.visibility.showApprovalControls && canApprove && <ActionCard title="Record workflow decision"><div className="grid gap-3 md:grid-cols-2"><Field label="Decision reference"><Input value={decisionReference} onChange={(event) => setDecisionReference(event.target.value)} /></Field><Field label="Signed decision evidence"><Input value={decisionEvidence} onChange={(event) => setDecisionEvidence(event.target.value)} /></Field></div><Field label="Decision reason"><Textarea value={decisionReason} onChange={(event) => setDecisionReason(event.target.value)} /></Field><div className="flex gap-2"><Button disabled={busy !== null} onClick={() => void run('approve', () => service.decide(exercise.id, { action: 'Approve', decisionReference, decisionEvidenceReference: decisionEvidence, reason: decisionReason, rowVersion: exercise.rowVersion }), 'Qualified list approved')}>Approve workflow step</Button><Button variant="destructive" disabled={busy !== null} onClick={() => void run('reject', () => service.decide(exercise.id, { action: 'Reject', decisionReference, decisionEvidenceReference: decisionEvidence, reason: decisionReason, rowVersion: exercise.rowVersion }), 'Rejection recorded')}>Reject</Button></div></ActionCard>}

      <Card data-testid="qualified-list-history"><CardHeader><CardTitle>Reusable qualified-list history</CardTitle></CardHeader><CardContent>{exercise.qualifiedEntries.length === 0 ? <p className="text-sm text-muted-foreground">Qualified-list entries appear after the qualification decision is completed.</p> : <Table><TableHeader><TableRow><TableHead>Supplier</TableHead><TableHead>Category</TableHead><TableHead>Status</TableHead><TableHead>Valid from</TableHead><TableHead>Expires</TableHead><TableHead>Decision</TableHead></TableRow></TableHeader><TableBody>{exercise.qualifiedEntries.map((item) => <TableRow key={item.id}><TableCell>{item.supplierName}</TableCell><TableCell>{item.categoryCode} · {item.categoryName}</TableCell><TableCell><Badge variant={item.status === 0 ? 'default' : 'secondary'}>{qualifiedEntryStatusLabel[item.status]}</Badge></TableCell><TableCell>{formatDate(item.validFromUtc)}</TableCell><TableCell>{formatDate(item.expiresAtUtc)}</TableCell><TableCell className="text-xs">{item.approvalReference}<br />{item.integrityHash.slice(0, 12)}…</TableCell></TableRow>)}</TableBody></Table>}</CardContent></Card>

      {actions.canExpire && canManage && <ActionCard title="Apply due expiry"><p className="text-sm text-muted-foreground">Expire only entries whose approved validity has elapsed; later sourcing eligibility will fail closed immediately.</p><Button disabled={busy !== null} onClick={() => void run('expire', () => service.expire(exercise.id, exercise.rowVersion), 'Due qualified-list entries expired')}>Apply expiry</Button></ActionCard>}
      {actions.immutable && <Card><CardContent className="flex items-center gap-3 p-5 text-sm"><FileCheck2 className="h-5 w-5 text-emerald-600" />This terminal record is read-only. Criteria, evidence, scores, workflow outcome, entries, expiry, and hashes remain available for audit.</CardContent></Card>}

      <Dialog open={Boolean(evaluation)} onOpenChange={(open) => !open && setEvaluation(null)}>
        <DialogContent className="max-h-[90vh] max-w-4xl overflow-y-auto"><DialogHeader><DialogTitle>Evaluate {evaluation?.applicationNumber}</DialogTitle></DialogHeader><div className="space-y-3">{exercise.criteria.map((criterion) => <div key={criterion.id} className="grid gap-3 rounded border p-3 md:grid-cols-5"><div className="md:col-span-2"><p className="font-medium">{criterion.code} · {criterion.name}</p><p className="text-xs text-muted-foreground">{criterion.weight}% · minimum {criterion.minimumScore}%{criterion.isMandatory ? ' · mandatory' : ''}</p></div><Input type="number" min={0} max={100} value={scores[criterion.id]?.score ?? 0} onChange={(event) => setScores((current) => ({ ...current, [criterion.id]: { ...current[criterion.id], score: Number(event.target.value) } }))} /><label className="flex items-center gap-2 text-sm"><Checkbox checked={scores[criterion.id]?.meetsRequirement ?? false} onCheckedChange={(checked) => setScores((current) => ({ ...current, [criterion.id]: { ...current[criterion.id], meetsRequirement: Boolean(checked) } }))} />Meets requirement</label><Input value={scores[criterion.id]?.reason ?? ''} onChange={(event) => setScores((current) => ({ ...current, [criterion.id]: { ...current[criterion.id], reason: event.target.value } }))} placeholder="Score reason" />{criterion.requiresEvidence && <div className="md:col-span-5"><Input value={scores[criterion.id]?.evidenceReference ?? ''} onChange={(event) => setScores((current) => ({ ...current, [criterion.id]: { ...current[criterion.id], evidenceReference: event.target.value } }))} placeholder="Evaluator evidence reference" /></div>}</div>)}</div><Field label="Evaluation remarks"><Textarea value={evaluationRemarks} onChange={(event) => setEvaluationRemarks(event.target.value)} /></Field><Field label="Signed recommendation evidence"><Input value={recommendationEvidence} onChange={(event) => setRecommendationEvidence(event.target.value)} /></Field><DialogFooter><Button variant="outline" onClick={() => setEvaluation(null)}>Cancel</Button><Button disabled={busy !== null} onClick={submitEvaluation}>{busy === 'evaluate' && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Record immutable evaluation</Button></DialogFooter></DialogContent>
      </Dialog>
    </div>
  );
}

function ActionCard({ title, children }: { title: string; children: React.ReactNode }) {
  return <Card><CardHeader><CardTitle>{title}</CardTitle></CardHeader><CardContent className="space-y-4">{children}</CardContent></Card>;
}
function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return <div className="space-y-2"><Label>{label}</Label>{children}</div>;
}
function Summary({ label, value }: { label: string; value: string }) {
  return <Card><CardContent className="p-4"><p className="text-xs uppercase text-muted-foreground">{label}</p><p className="mt-1 break-words font-medium">{value}</p></CardContent></Card>;
}
function Line({ label, value }: { label: string; value?: string }) {
  return <div><span className="text-muted-foreground">{label}: </span><span>{value || '—'}</span></div>;
}
