'use client';

import React, { useEffect, useRef, useState } from 'react';
import axios from 'axios';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { inventoryManagementService as service, PhysicalCountDetailDto, PhysicalCountDecisionSetup } from '@/services/inventoryManagementService';
import { useWorkflowSummary } from '@/hooks/useWorkflowSummary';

export const countReviewError = (error: unknown) => {
  const data = axios.isAxiosError(error) ? error.response?.data : null;
  const message = typeof data === 'string' ? data : data?.detail ?? data?.message;
  return `${message ?? (error instanceof Error ? error.message : 'The count could not be updated.')}${data?.code ? ` (${data.code})` : ''}`;
};

export function PhysicalCountReviewActions({ count, unsaved, saving = false, onSaveCounts, onUploadSheet, onChanged }: {
  count: PhysicalCountDetailDto; unsaved: boolean; saving?: boolean;
  onSaveCounts: () => void; onUploadSheet: () => void; onChanged: () => Promise<void>;
}) {
  const approval = useWorkflowSummary({ entityType: 'StockAdjustment', entityId: count.stockAdjustmentId || count.id });
  const priorApprovalCycle = count.actions.some(action => action.actionType === 'Submitted');
  const directCompletion = count.approvalRequired === false || (!priorApprovalCycle && approval.visibility.direct);
  const canDecide = count.approvalRequired !== false && count.canDecide;
  const submitLabel = directCompletion ? 'Complete count' : 'Submit for approval';
  const [setup, setSetup] = useState<PhysicalCountDecisionSetup | null>(null);
  const [setupLoading, setSetupLoading] = useState(false);
  const [setupError, setSetupError] = useState<string | null>(null);
  const [setupScope, setSetupScope] = useState('');
  const [setupAttempt, setSetupAttempt] = useState(0);
  const [decisionCode, setDecisionCode] = useState('');
  const [notes, setNotes] = useState('');
  const [busy, setBusy] = useState(false);
  const [confirm, setConfirm] = useState(false);
  const attempt = useRef({ signature: '', key: '' });
  useEffect(() => {
    setDecisionCode(''); setNotes('');
  }, [count.id, count.status]);
  useEffect(() => {
    let current = true;
    setSetup(null); setSetupScope(''); setSetupError(null); setSetupLoading(!!canDecide);
    if (canDecide) void service.getPhysicalCountDecisions(false, count.id)
      .then(value => { if (current) { setSetup(value); setSetupScope(`${count.id}:${count.status}`); } })
      .catch(error => { if (current) setSetupError(countReviewError(error)); })
      .finally(() => { if (current) setSetupLoading(false); });
    return () => { current = false; };
  }, [count.id, count.status, canDecide, setupAttempt]);
  const decisionsReady = setupScope === `${count.id}:${count.status}` && !setupLoading && !setupError;
  const decision = decisionsReady ? setup?.decisions.find(d => d.code === decisionCode && d.isActive) : undefined;
  const investigate = decision?.effect === 'Investigate';
  const reviewable = ['InProgress', 'RecountRequired', 'UnderInvestigation'].includes(count.status);
  const complete = count.totalItems > 0 && count.countedItems === count.totalItems;
  const evidence = count.evidence ?? [];
  const evidenceReady = evidence.length > 0;
  const run = async (kind: 'review' | 'submit' | 'decide') => {
    if (busy) return false;
    setBusy(true);
    const signature = JSON.stringify([kind, count.id, count.rowVersion, decisionCode, setup?.revision, notes]);
    if (attempt.current.signature !== signature) attempt.current = { signature, key: crypto.randomUUID() };
    const request = { rowVersion: count.rowVersion, idempotencyKey: attempt.current.key, comment: notes.trim() || undefined };
    try {
      if (kind === 'review') await service.reviewPhysicalCount(count.id, request);
      else if (kind === 'submit') await service.submitReviewedPhysicalCount(count.id, request);
      else {
        if (!decision || !setup) return false;
        const choice = { ...request, decisionCode, decisionRevision: setup.revision, approved: !investigate, reason: investigate ? notes.trim() : undefined };
        if (count.status === 'PendingStoresApproval') await service.decidePhysicalCountStores(count.id, choice);
        else if (count.status === 'PendingFinanceApproval') await service.decidePhysicalCountFinance(count.id, choice);
        else if (count.status === 'PendingAuditAttestation') await service.attestPhysicalCountAudit(count.id, choice);
      }
      await onChanged();
      toast.success(kind === 'review' ? (count.observationSubmittedAtUtc ? 'Original quantities remain retained. Review passed items and the linked recount sheets.' : 'Review the variance and correct any entry mistakes.') : kind === 'submit' ? 'Count submitted. Stock is unchanged until Post.' : investigate ? 'Sent for investigation. Posting is blocked.' : 'Decision saved.');
      return true;
    } catch (error) { toast.error(countReviewError(error)); return false; }
    finally { setBusy(false); }
  };
  if (!count.canReview && !canDecide) return null;
  return <div className="space-y-2">
    {count.canReview && count.status === 'UnderInvestigation' && <div><Label htmlFor="investigation-findings">Investigation findings</Label><Textarea id="investigation-findings" value={notes} onChange={e => setNotes(e.target.value)} placeholder="Record what was checked and what needs correcting." /></div>}
    {count.canReview && (reviewable || count.status === 'UnderReview') && <>
      <div className="flex flex-wrap items-center gap-2">
        <div className="flex shrink-0 flex-nowrap items-center gap-2" role="group" aria-label="Count actions">
          {!count.observationSubmittedAtUtc && ['InProgress', 'UnderReview'].includes(count.status) && <Button disabled={busy || saving || !unsaved} onClick={onSaveCounts}>{saving ? 'Saving...' : 'Save Counts'}</Button>}
          {reviewable && <Button disabled={busy || saving || unsaved || !complete || (count.status === 'UnderInvestigation' && !notes.trim())} onClick={() => void run('review')}>{count.status === 'UnderInvestigation' ? 'Resume review' : 'Review variance'}</Button>}
          {count.status === 'UnderReview' && <Button variant="destructive" disabled={busy || saving || unsaved || !complete || !evidenceReady || !approval.visibility.known} onClick={() => setConfirm(true)}>{submitLabel}</Button>}
        </div>
        {!count.observationSubmittedAtUtc && count.status === 'UnderReview' && !evidenceReady && <Button variant="outline" disabled={busy || saving || unsaved} onClick={onUploadSheet}>Upload count sheet</Button>}
        {['InProgress', 'UnderReview'].includes(count.status) && <span className="text-xs text-muted-foreground" role="status">{unsaved ? 'Unsaved quantity changes.' : 'Quantities saved.'}</span>}
      </div>
      {count.status === 'UnderReview' && !evidenceReady && <p className="text-xs text-amber-700">Attach supporting evidence in Details or upload a count sheet before submitting.</p>}
      {count.status === 'UnderReview' && !approval.visibility.known && <div className="flex items-center gap-2 text-xs text-muted-foreground"><span>{approval.loading ? 'Checking approval setup...' : 'Approval setup could not be checked.'}</span>{approval.error && <Button size="sm" variant="outline" onClick={() => void approval.refresh()}>Retry</Button>}</div>}
    </>}
    {canDecide && <>
      <Label>Decision</Label>
      {setupLoading && <p role="status" className="text-sm text-muted-foreground">Loading decisions...</p>}
      {setupError && <div role="alert" className="flex flex-wrap items-center gap-2 text-sm text-destructive"><span>Could not load decisions. {setupError}</span><Button variant="outline" size="sm" onClick={() => setSetupAttempt(value => value + 1)}>Retry decisions</Button></div>}
      {decisionsReady && setup && !setup.decisions.some(d => d.isActive) && <p role="alert" className="text-sm text-destructive">No active count decisions are configured. Ask an inventory administrator to review Count Decisions.</p>}
      <Select value={decisionCode} onValueChange={setDecisionCode} disabled={!decisionsReady || !setup?.decisions.some(d => d.isActive) || busy}><SelectTrigger aria-label="Variance decision"><SelectValue placeholder={setupLoading ? 'Loading decisions...' : 'Select a decision'} /></SelectTrigger><SelectContent>{decisionsReady && setup?.decisions.filter(d => d.isActive).map(d => <SelectItem key={d.code} value={d.code}>{d.label}</SelectItem>)}</SelectContent></Select>
      {decision && <p className="text-sm text-muted-foreground">{investigate ? 'Stops posting and returns the count for investigation and corrections.' : 'Approves this stage. Stock changes only after all approvals and Post.'}</p>}
      <Label htmlFor="count-decision-comment">{investigate ? 'Reason for investigation' : 'Comments (optional)'}</Label>
      <Textarea id="count-decision-comment" value={notes} onChange={e => setNotes(e.target.value)} />
      <Button disabled={busy || !decision || (investigate && !notes.trim())} onClick={() => void run('decide')}>Save decision</Button>
    </>}
    <ConfirmationDialog open={confirm} onOpenChange={setConfirm} title={directCompletion ? 'Complete reviewed count?' : 'Submit reviewed count?'} description={directCompletion ? 'Save the reviewed quantities as ready for posting. Stock balances will not change until Post.' : 'Quantities will be locked for independent approval. Stock balances will not change until posting.'} confirmText={submitLabel} variant="destructive" isLoading={busy} onConfirm={() => run('submit')} />
  </div>;
}
