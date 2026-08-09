'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Check, Loader2, Send, ShieldCheck, Undo2, X } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/components/ui/use-toast';
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import type { FixedAssetDepreciationReversal } from '@/types/fixed-assets';

const REVERSE_PERMISSION = 'Finance.FixedAssets.Depreciation.Reverse';
const APPROVE_PERMISSION = 'Finance.FixedAssets.Depreciation.Reversal.Approve';

const today = () => new Date().toISOString().slice(0, 10);
const compactId = (value?: string) => value ? `${value.slice(0, 8)}…${value.slice(-4)}` : '—';
const formatDateTime = (value?: string) => value ? new Date(value).toLocaleString() : '—';

export interface DepreciationRunOption {
  id: string;
  label: string;
  reversed: boolean;
}

interface Props {
  runs: DepreciationRunOption[];
  onChanged: () => Promise<void> | void;
}

const statusVariant = (status: FixedAssetDepreciationReversal['status']) => {
  if (status === 'Posted' || status === 'Approved') return 'default';
  if (status === 'Rejected' || status === 'Failed') return 'destructive';
  return 'secondary';
};

/**
 * Finance operator workspace for FIN-LIM-0033.
 *
 * The three visible stages are intentional: a request does not alter the ledger, an independent
 * review records the control decision, and posting creates the linked compensating journal. This
 * makes the correction explainable during TDC demonstrations and reconstructable during audit.
 */
export function FixedAssetDepreciationReversalPanel({ runs, onChanged }: Props) {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canReverse = hasPermission(REVERSE_PERMISSION);
  const canApprove = hasPermission(APPROVE_PERMISSION);
  const [selectedRunId, setSelectedRunId] = useState(runs[0]?.id ?? '');
  const [items, setItems] = useState<FixedAssetDepreciationReversal[]>([]);
  const [loading, setLoading] = useState(false);
  const [busyId, setBusyId] = useState<string>();
  const [requestOpen, setRequestOpen] = useState(false);
  const [reviewTarget, setReviewTarget] = useState<FixedAssetDepreciationReversal>();
  const [reviewApproved, setReviewApproved] = useState(true);
  const [reviewComment, setReviewComment] = useState('');
  const [requestForm, setRequestForm] = useState({ reversalDate: today(), reason: '', impactAssessment: '' });

  useEffect(() => {
    if (!runs.some(run => run.id === selectedRunId)) setSelectedRunId(runs[0]?.id ?? '');
  }, [runs, selectedRunId]);

  const selectedRun = useMemo(
    () => runs.find(run => run.id === selectedRunId),
    [runs, selectedRunId]
  );

  const load = useCallback(async () => {
    if (!selectedRunId) {
      setItems([]);
      return;
    }

    try {
      setLoading(true);
      setItems(await fixedAssetsDataService.getDepreciationReversals(selectedRunId));
    } catch (error) {
      toast({
        title: 'Could not load depreciation corrections',
        description: error instanceof Error ? error.message : 'The correction history could not be loaded.',
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  }, [selectedRunId, toast]);

  useEffect(() => { void load(); }, [load]);

  const refreshWorkspace = async () => {
    await load();
    await onChanged();
  };

  const submitRequest = async () => {
    if (!selectedRunId || requestForm.reason.trim().length < 20 || requestForm.impactAssessment.trim().length < 20) {
      toast({ title: 'More evidence is required', description: 'Provide a clear reason and impact assessment of at least 20 characters each.', variant: 'destructive' });
      return;
    }

    try {
      setBusyId('request');
      await fixedAssetsDataService.requestDepreciationReversal(selectedRunId, requestForm);
      setRequestOpen(false);
      setRequestForm({ reversalDate: today(), reason: '', impactAssessment: '' });
      toast({ title: 'Correction submitted', description: 'The posted run remains unchanged until independent review and posting.' });
      await refreshWorkspace();
    } catch (error) {
      toast({ title: 'Request failed', description: error instanceof Error ? error.message : 'The correction could not be submitted.', variant: 'destructive' });
    } finally {
      setBusyId(undefined);
    }
  };

  const submitReview = async () => {
    if (!reviewTarget || reviewComment.trim().length < 20) {
      toast({ title: 'Review evidence is required', description: 'Enter a review comment of at least 20 characters.', variant: 'destructive' });
      return;
    }

    try {
      setBusyId(reviewTarget.id);
      await fixedAssetsDataService.reviewDepreciationReversal(selectedRunId, reviewTarget.id, { approved: reviewApproved, reviewComment });
      setReviewTarget(undefined);
      setReviewComment('');
      toast({ title: reviewApproved ? 'Correction approved' : 'Correction rejected', description: 'The reviewer and decision are now part of the audit trail.' });
      await refreshWorkspace();
    } catch (error) {
      toast({ title: 'Review failed', description: error instanceof Error ? error.message : 'The review could not be saved.', variant: 'destructive' });
    } finally {
      setBusyId(undefined);
    }
  };

  const postApproved = async (item: FixedAssetDepreciationReversal) => {
    try {
      setBusyId(item.id);
      await fixedAssetsDataService.postDepreciationReversal(selectedRunId, item.id);
      toast({ title: 'Depreciation reversed', description: 'The compensating journal and asset-register correction were posted together.' });
      await refreshWorkspace();
    } catch (error) {
      toast({ title: 'Posting failed', description: error instanceof Error ? error.message : 'The approved correction could not be posted.', variant: 'destructive' });
    } finally {
      setBusyId(undefined);
    }
  };

  return (
    <>
      <Card>
        <CardHeader className="gap-3 md:flex-row md:items-start md:justify-between">
          <div>
            <CardTitle className="flex items-center gap-2 text-base"><ShieldCheck className="h-4 w-4" />Depreciation correction control</CardTitle>
            <CardDescription>Preserves the original run, requires independent review, and creates a linked compensating journal before a corrected revision can be run.</CardDescription>
          </div>
          {canReverse && selectedRun && !selectedRun.reversed && (
            <Button variant="destructive" onClick={() => setRequestOpen(true)}><Undo2 className="mr-2 h-4 w-4" />Request reversal</Button>
          )}
        </CardHeader>
        <CardContent className="space-y-4">
          {runs.length === 0 ? (
            <p className="text-sm text-muted-foreground">Select a period containing posted depreciation to use the correction workspace.</p>
          ) : (
            <div className="max-w-xl space-y-2">
              <Label>Depreciation run</Label>
              <Select value={selectedRunId} onValueChange={setSelectedRunId}>
                <SelectTrigger><SelectValue placeholder="Select a posted run" /></SelectTrigger>
                <SelectContent>{runs.map(run => <SelectItem key={run.id} value={run.id}>{run.label}{run.reversed ? ' · Reversed' : ''}</SelectItem>)}</SelectContent>
              </Select>
            </div>
          )}
          {loading ? (
            <div className="flex items-center gap-2 text-sm text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin" />Loading correction history…</div>
          ) : items.length === 0 ? (
            selectedRunId && <p className="text-sm text-muted-foreground">No correction requests have been recorded for this run.</p>
          ) : (
            <div className="space-y-3">
              {items.map(item => (
                <div key={item.id} className="rounded-lg border p-4">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                      <div className="flex items-center gap-2"><Badge variant={statusVariant(item.status)}>{item.status}</Badge><span className="text-xs text-muted-foreground">Requested {formatDateTime(item.requestedAt)}</span></div>
                      <p className="mt-2 text-sm font-medium">{item.reason}</p>
                      <p className="mt-1 text-sm text-muted-foreground">{item.impactAssessment}</p>
                    </div>
                    <div className="flex gap-2">
                      {canApprove && item.status === 'PendingApproval' && <>
                        <Button size="sm" onClick={() => { setReviewTarget(item); setReviewApproved(true); }}><Check className="mr-1 h-4 w-4" />Review</Button>
                        <Button size="sm" variant="outline" onClick={() => { setReviewTarget(item); setReviewApproved(false); }}><X className="mr-1 h-4 w-4" />Reject</Button>
                      </>}
                      {canReverse && item.status === 'Approved' && <Button size="sm" onClick={() => void postApproved(item)} disabled={busyId === item.id}><Send className="mr-1 h-4 w-4" />Post reversal</Button>}
                    </div>
                  </div>
                  <div className="mt-3 grid gap-2 text-xs text-muted-foreground md:grid-cols-3">
                    <span>Maker: {item.requestedByUserName}</span><span>Amount: {item.totalDepreciationAmount.toFixed(2)}</span><span>Revision: {item.originalCorrectionSequence}</span>
                    <span>Original journal: {compactId(item.originalJournalEntryId)}</span><span>Reversal journal: {compactId(item.reversalJournalEntryId)}</span>{item.reviewedByUserName && <span>Reviewer: {item.reviewedByUserName}</span>}
                    {item.reviewComment && <span className="md:col-span-3">Review: {item.reviewComment}</span>}
                    {item.failureReason && <span className="text-destructive md:col-span-3">Last posting failure: {item.failureReason}</span>}
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog open={requestOpen} onOpenChange={setRequestOpen}>
        <DialogContent>
          <DialogHeader><DialogTitle>Request depreciation reversal</DialogTitle><DialogDescription>The posted run is unchanged by this request. A different authorised user must review it before posting.</DialogDescription></DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2"><Label htmlFor="depreciationReversalDate">Requested posting date</Label><Input id="depreciationReversalDate" type="date" value={requestForm.reversalDate} onChange={event => setRequestForm({ ...requestForm, reversalDate: event.target.value })} /></div>
            <div className="space-y-2"><Label htmlFor="depreciationReversalReason">Reason</Label><Textarea id="depreciationReversalReason" value={requestForm.reason} onChange={event => setRequestForm({ ...requestForm, reason: event.target.value })} placeholder="Explain the depreciation error and required correction." /></div>
            <div className="space-y-2"><Label htmlFor="depreciationImpactAssessment">Impact assessment</Label><Textarea id="depreciationImpactAssessment" value={requestForm.impactAssessment} onChange={event => setRequestForm({ ...requestForm, impactAssessment: event.target.value })} placeholder="Describe affected assets, periods, reports, and the corrected rerun." /></div>
          </div>
          <DialogFooter><Button variant="outline" onClick={() => setRequestOpen(false)}>Cancel</Button><Button onClick={() => void submitRequest()} disabled={busyId === 'request'}>{busyId === 'request' && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Submit for review</Button></DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!reviewTarget} onOpenChange={open => !open && setReviewTarget(undefined)}>
        <DialogContent>
          <DialogHeader><DialogTitle>{reviewApproved ? 'Approve' : 'Reject'} depreciation reversal</DialogTitle><DialogDescription>Confirm the correction independently. The requester cannot review their own request.</DialogDescription></DialogHeader>
          <div className="space-y-2"><Label htmlFor="depreciationReviewComment">Review comment</Label><Textarea id="depreciationReviewComment" value={reviewComment} onChange={event => setReviewComment(event.target.value)} placeholder="Record the evidence checked and the basis for this decision." /></div>
          <DialogFooter><Button variant="outline" onClick={() => setReviewTarget(undefined)}>Cancel</Button><Button variant={reviewApproved ? 'default' : 'destructive'} onClick={() => void submitReview()} disabled={busyId === reviewTarget?.id}>{reviewApproved ? 'Approve request' : 'Reject request'}</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
