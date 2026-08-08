'use client';

import { useCallback, useEffect, useState } from 'react';
import { Check, Loader2, Send, ShieldCheck, Undo2, X } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/components/ui/use-toast';
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import type {
  FixedAsset,
  FixedAssetCapitalizationReversal,
} from '@/types/fixed-assets';

const REVERSE_PERMISSION = 'Finance.FixedAssets.Capitalization.Reverse';
const APPROVE_PERMISSION = 'Finance.FixedAssets.Capitalization.Reversal.Approve';

const today = () => new Date().toISOString().slice(0, 10);
const compactId = (value?: string) => value ? `${value.slice(0, 8)}…${value.slice(-4)}` : '—';
const formatDateTime = (value?: string) => value ? new Date(value).toLocaleString() : '—';

const statusVariant = (status: FixedAssetCapitalizationReversal['status']) => {
  if (status === 'Posted' || status === 'Approved') return 'default';
  if (status === 'Rejected' || status === 'Failed') return 'destructive';
  return 'secondary';
};

interface Props {
  asset: FixedAsset;
  onChanged: () => Promise<void> | void;
}

/**
 * Operator workspace for FIN-LIM-0030 and TDC FR-GL-008/FR-GL-010.
 *
 * A reversal is intentionally presented as three visible stages—request, independent review,
 * and post—because a single "undo" button would hide the maker-checker control and the linked
 * journal evidence stakeholders need to inspect during audit and demonstrations.
 */
export function FixedAssetCapitalizationReversalPanel({ asset, onChanged }: Props) {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canReverse = hasPermission(REVERSE_PERMISSION);
  const canApprove = hasPermission(APPROVE_PERMISSION);
  const [items, setItems] = useState<FixedAssetCapitalizationReversal[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string>();
  const [requestOpen, setRequestOpen] = useState(false);
  const [reviewTarget, setReviewTarget] = useState<FixedAssetCapitalizationReversal>();
  const [reviewApproved, setReviewApproved] = useState(true);
  const [reviewComment, setReviewComment] = useState('');
  const [requestForm, setRequestForm] = useState({
    reversalDate: today(),
    reason: '',
    impactAssessment: '',
  });

  const load = useCallback(async () => {
    try {
      setLoading(true);
      setItems(await fixedAssetsDataService.getCapitalizationReversals(asset.id));
    } catch (error) {
      toast({
        title: 'Could not load capitalization corrections',
        description: error instanceof Error ? error.message : 'The correction history could not be loaded.',
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  }, [asset.id, toast]);

  useEffect(() => {
    void load();
  }, [load]);

  const refreshWorkspace = async () => {
    await load();
    await onChanged();
  };

  const submitRequest = async () => {
    if (requestForm.reason.trim().length < 20 || requestForm.impactAssessment.trim().length < 20) {
      toast({
        title: 'More evidence is required',
        description: 'Provide a clear reason and impact assessment of at least 20 characters each.',
        variant: 'destructive',
      });
      return;
    }

    try {
      setBusyId('request');
      await fixedAssetsDataService.requestCapitalizationReversal(asset.id, requestForm);
      setRequestOpen(false);
      setRequestForm({ reversalDate: today(), reason: '', impactAssessment: '' });
      toast({ title: 'Correction submitted', description: 'The posted cost remains unchanged until an independent review and posting.' });
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
      await fixedAssetsDataService.reviewCapitalizationReversal(asset.id, reviewTarget.id, {
        approved: reviewApproved,
        reviewComment,
      });
      setReviewTarget(undefined);
      setReviewComment('');
      toast({ title: reviewApproved ? 'Correction approved' : 'Correction rejected', description: 'The review decision and reviewer identity are now part of the audit trail.' });
      await refreshWorkspace();
    } catch (error) {
      toast({ title: 'Review failed', description: error instanceof Error ? error.message : 'The review could not be saved.', variant: 'destructive' });
    } finally {
      setBusyId(undefined);
    }
  };

  const postApproved = async (item: FixedAssetCapitalizationReversal) => {
    try {
      setBusyId(item.id);
      await fixedAssetsDataService.postCapitalizationReversal(asset.id, item.id);
      toast({ title: 'Capitalization reversed', description: 'A linked compensating journal was posted and the current register cost was removed.' });
      await refreshWorkspace();
    } catch (error) {
      toast({ title: 'Posting failed', description: error instanceof Error ? error.message : 'The approved correction could not be posted.', variant: 'destructive' });
    } finally {
      setBusyId(undefined);
    }
  };

  const directCapitalization = asset.sourceDocumentType?.toLowerCase() === 'fixedasset';
  const canRequestCurrent = asset.status === 'Capitalized' && !!asset.postingEventId && directCapitalization;

  return (
    <>
      <Card>
        <CardHeader className="gap-3 md:flex-row md:items-start md:justify-between">
          <div>
            <CardTitle className="flex items-center gap-2 text-base"><ShieldCheck className="h-4 w-4" />Capitalization correction control</CardTitle>
            <CardDescription>
              Preserves the original journal, requires independent approval, and posts a linked compensating entry instead of editing posted cost.
            </CardDescription>
          </div>
          {canReverse && canRequestCurrent && (
            <Button variant="destructive" onClick={() => setRequestOpen(true)}>
              <Undo2 className="mr-2 h-4 w-4" />Request reversal
            </Button>
          )}
        </CardHeader>
        <CardContent className="space-y-4">
          {!directCapitalization && asset.postingEventId && (
            <div className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
              This cost originated from {asset.sourceDocumentType || 'a source document'}. Reverse or void that source document so Finance creates one shared reversal journal and keeps both records synchronized.
            </div>
          )}
          {asset.capitalizationReversalPostingEventId && (
            <div className="rounded-md border border-emerald-300 bg-emerald-50 p-3 text-sm text-emerald-900">
              Current capitalization reversed. Original event {compactId(asset.postingEventId)} is linked to reversal event {compactId(asset.capitalizationReversalPostingEventId)}.
            </div>
          )}
          {loading ? (
            <div className="flex items-center gap-2 text-sm text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin" />Loading correction history…</div>
          ) : items.length === 0 ? (
            <p className="text-sm text-muted-foreground">No capitalization correction requests have been recorded for this asset.</p>
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
                      {canApprove && item.status === 'PendingApproval' && (
                        <>
                          <Button size="sm" onClick={() => { setReviewTarget(item); setReviewApproved(true); }}><Check className="mr-1 h-4 w-4" />Review</Button>
                          <Button size="sm" variant="outline" onClick={() => { setReviewTarget(item); setReviewApproved(false); }}><X className="mr-1 h-4 w-4" />Reject</Button>
                        </>
                      )}
                      {canReverse && item.status === 'Approved' && (
                        <Button size="sm" onClick={() => void postApproved(item)} disabled={busyId === item.id}><Send className="mr-1 h-4 w-4" />Post reversal</Button>
                      )}
                    </div>
                  </div>
                  <div className="mt-3 grid gap-2 text-xs text-muted-foreground md:grid-cols-3">
                    <span>Maker: {item.requestedByUserName}</span>
                    <span>Original journal: {compactId(item.originalJournalEntryId)}</span>
                    <span>Reversal journal: {compactId(item.reversalJournalEntryId)}</span>
                    {item.reviewedByUserName && <span>Reviewer: {item.reviewedByUserName}</span>}
                    {item.reviewComment && <span className="md:col-span-2">Review: {item.reviewComment}</span>}
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog open={requestOpen} onOpenChange={setRequestOpen}>
        <DialogContent>
          <DialogHeader><DialogTitle>Request capitalization reversal</DialogTitle><DialogDescription>The posted journal is not changed by this request. A different authorised user must review it before posting.</DialogDescription></DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2"><Label htmlFor="reversalDate">Requested posting date</Label><Input id="reversalDate" type="date" value={requestForm.reversalDate} onChange={event => setRequestForm({ ...requestForm, reversalDate: event.target.value })} /></div>
            <div className="space-y-2"><Label htmlFor="reversalReason">Reason</Label><Textarea id="reversalReason" value={requestForm.reason} onChange={event => setRequestForm({ ...requestForm, reason: event.target.value })} placeholder="Explain the capitalization error and the required correction." /></div>
            <div className="space-y-2"><Label htmlFor="impactAssessment">Impact assessment</Label><Textarea id="impactAssessment" value={requestForm.impactAssessment} onChange={event => setRequestForm({ ...requestForm, impactAssessment: event.target.value })} placeholder="Describe the register, GL, reporting, and corrected-reposting impact." /></div>
          </div>
          <DialogFooter><Button variant="outline" onClick={() => setRequestOpen(false)}>Cancel</Button><Button onClick={() => void submitRequest()} disabled={busyId === 'request'}>{busyId === 'request' && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Submit for review</Button></DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!reviewTarget} onOpenChange={open => !open && setReviewTarget(undefined)}>
        <DialogContent>
          <DialogHeader><DialogTitle>{reviewApproved ? 'Approve' : 'Reject'} capitalization reversal</DialogTitle><DialogDescription>Confirm the evidence independently. The requester cannot review their own correction.</DialogDescription></DialogHeader>
          <div className="space-y-2"><Label htmlFor="reviewComment">Review comment</Label><Textarea id="reviewComment" value={reviewComment} onChange={event => setReviewComment(event.target.value)} placeholder="Record the evidence checked and the basis for this decision." /></div>
          <DialogFooter><Button variant="outline" onClick={() => setReviewTarget(undefined)}>Cancel</Button><Button variant={reviewApproved ? 'default' : 'destructive'} onClick={() => void submitReview()} disabled={busyId === reviewTarget?.id}>{reviewApproved ? 'Approve request' : 'Reject request'}</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
