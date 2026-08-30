'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { CheckCircle2, RefreshCw, Send, ShieldX } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringPermittingHodDecisionService } from '@/services/civil-engineering-permitting-hod-decision.service';
import type { CivilEngineeringPermittingHodDecisionOutcome, CivilEngineeringPermittingHodDecisionQueueItem } from '@/types/civil-engineering-permitting-hod-decision';

const managePermission = 'civil-engineering.permitting.manage';
const none = '__select__';
const decisionLabels: Record<CivilEngineeringPermittingHodDecisionOutcome, string> = { Approve: 'Approve recommendation', Reject: 'Reject recommendation', ReturnForCorrection: 'Return for correction' };
const recommendationLabels: Record<string, string> = { RecommendApproval: 'Recommend approval', RecommendApprovalWithConditions: 'Recommend approval with conditions', RecommendRejection: 'Recommend rejection', ReturnForCorrection: 'Return for correction' };
const formatDate = (value: string) => new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
const errorText = (error: unknown, fallback: string) => {
  const value = error as { response?: { detail?: string; correlationId?: string }; message?: string };
  const detail = value.response?.detail || value.message || fallback;
  return `${detail}${value.response?.correlationId ? ` Reference: ${value.response.correlationId}` : ''}`;
};

export default function CivilEngineeringPermittingHodDecisionsPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canManage = hasPermission(managePermission);
  const [items, setItems] = useState<CivilEngineeringPermittingHodDecisionQueueItem[]>([]);
  const [reviewId, setReviewId] = useState('');
  const [outcome, setOutcome] = useState<CivilEngineeringPermittingHodDecisionOutcome | ''>('');
  const [reason, setReason] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const selected = useMemo(() => items.find((item) => item.engineeringReviewId === reviewId), [items, reviewId]);

  const load = useCallback(async () => {
    if (!canManage) { setLoading(false); return; }
    setLoading(true);
    try {
      const pending = await civilEngineeringPermittingHodDecisionService.pending();
      setItems(pending); setReviewId((current) => pending.some((item) => item.engineeringReviewId === current) ? current : pending[0]?.engineeringReviewId || '');
    } catch (error) {
      toast({ variant: 'destructive', title: 'HOD decision queue could not be loaded', description: errorText(error, 'Confirm that you are the assigned HOD and current workflow approver.') });
    } finally { setLoading(false); }
  }, [canManage, toast]);

  useEffect(() => { void load(); }, [load]);

  const submit = async () => {
    if (!selected || !outcome || ((outcome === 'Reject' || outcome === 'ReturnForCorrection') && !reason.trim())) {
      toast({ variant: 'destructive', title: 'Complete the final decision', description: 'Select the decision. A reason is required when rejecting or returning a recommendation.' });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringPermittingHodDecisionService.decide(selected.engineeringReviewId, { clientRequestId: crypto.randomUUID(), outcome, reason: reason.trim() || null });
      setOutcome(''); setReason('');
      toast({ title: 'Final HOD decision recorded', description: outcome === 'ReturnForCorrection' ? 'The review is closed as returned; use controlled development-file handoffs for the next recipient.' : 'The final shared-workflow outcome and audit history are recorded.' });
      await load();
    } catch (error) {
      toast({ variant: 'destructive', title: 'Final HOD decision was not recorded', description: errorText(error, 'Refresh the queue and confirm that this review is assigned to you in both the handoff and workflow.') });
    } finally { setSaving(false); }
  };

  if (!canManage) return <Alert><ShieldX className="h-4 w-4" /><AlertTitle>Civil permitting access required</AlertTitle><AlertDescription>You do not have access to the HOD permitting decision queue.</AlertDescription></Alert>;

  return <div className="space-y-5"><div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><h1 className="flex items-center gap-2 text-2xl font-semibold"><CheckCircle2 className="h-6 w-6" />HOD permitting decisions</h1><p className="mt-1 text-sm text-muted-foreground">Final decisions for SCE recommendations routed to you and assigned by the shared workflow.</p></div><Button variant="outline" size="sm" onClick={() => void load()} disabled={loading || saving}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button></div>
    <Card><CardHeader><CardTitle className="text-base">Pending recommendation</CardTitle></CardHeader><CardContent><Select value={reviewId || none} onValueChange={(value) => { setReviewId(value === none ? '' : value); setOutcome(''); setReason(''); }}><SelectTrigger className="max-w-3xl"><SelectValue placeholder="Select recommendation" /></SelectTrigger><SelectContent><SelectItem value={none} disabled>Select recommendation</SelectItem>{items.map((item) => <SelectItem key={item.engineeringReviewId} value={item.engineeringReviewId}>{item.fileNumber} · {item.applicationReference} · {item.applicantName}</SelectItem>)}</SelectContent></Select>{loading ? <p className="mt-3 text-sm text-muted-foreground">Loading your assigned decision queue…</p> : null}{!loading && !items.length ? <p className="mt-3 rounded border border-dashed p-3 text-sm text-muted-foreground">No permitting recommendation is currently assigned to you for final HOD decision.</p> : null}</CardContent></Card>
    {selected ? <Card><CardHeader><CardTitle className="text-base">Review recommendation</CardTitle><CardDescription>{selected.projectLabel} · Handoff due {formatDate(selected.handoffDueDate)}</CardDescription></CardHeader><CardContent className="space-y-4"><div className="flex flex-wrap gap-2"><Badge variant="outline">{recommendationLabels[selected.recommendedOutcome] ?? selected.recommendedOutcome}</Badge><Badge variant="secondary">{selected.commentCategoryLabel}</Badge></div><div className="rounded-md border p-3"><p className="text-sm font-medium">{selected.reviewerName}</p><p className="mt-2 whitespace-pre-wrap break-words text-sm text-muted-foreground">{selected.reviewComment}</p>{selected.documentReference ? <p className="mt-2 text-xs text-muted-foreground">DMS evidence: {selected.documentReference}</p> : null}<p className="mt-2 text-xs text-muted-foreground">Submitted {formatDate(selected.reviewedAt)}</p></div><div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_minmax(0,2fr)]"><div className="space-y-1"><Label>Final decision</Label><Select value={outcome || none} onValueChange={(value) => setOutcome(value === none ? '' : value as CivilEngineeringPermittingHodDecisionOutcome)}><SelectTrigger><SelectValue placeholder="Select decision" /></SelectTrigger><SelectContent><SelectItem value={none} disabled>Select decision</SelectItem>{(Object.keys(decisionLabels) as CivilEngineeringPermittingHodDecisionOutcome[]).map((item) => <SelectItem key={item} value={item}>{decisionLabels[item]}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Reason {outcome === 'Reject' || outcome === 'ReturnForCorrection' ? '(required)' : '(optional)'}</Label><Textarea value={reason} maxLength={2000} rows={3} placeholder="Explain the decision or required correction." onChange={(event) => setReason(event.target.value)} /></div></div><div className="flex justify-end"><Button disabled={saving} onClick={() => void submit()}><Send className="mr-2 h-4 w-4" />Record final decision</Button></div></CardContent></Card> : null}
  </div>;
}
