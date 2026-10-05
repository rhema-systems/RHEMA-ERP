'use client';

import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
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
import { fmtTravelMoney } from '@/components/hr/travel/travel-format';
import type { StaffTravelViewerActions } from '@/types/hr/travel';

/** Who the caller approves as, in words — for the dialog and the request page. */
export function decidesAsSentence(viewer: StaffTravelViewerActions, traveller: string): string {
  const stage = viewer.stageName ?? 'this';
  switch (viewer.decidesAs) {
    case 'LineAuthority':
      return `You decide the ${stage} stage as ${traveller}'s ${viewer.relation ?? 'line manager'}.`;
    case 'TravelDesk':
      return (
        `You decide the ${stage} stage for the travel desk: ${traveller} has no line manager who can approve ` +
        'in the system. A note saying so is kept on the request.'
      );
    default:
      return `You decide the ${stage} stage.`;
  }
}

/**
 * Approving a travel request at the stage it is on (travel final closure, lane 2 — D-7, O-9, T-10).
 *
 * The approved budget is asked for only at the LAST stage of the route — the approval that approves the
 * trip — prefilled with the estimate. Before lane 2 no screen sent one, so the server copied the estimate
 * and the field recorded no decision. An earlier stage approves without one; the server refuses a budget
 * sent there rather than discarding it. The server also refuses nothing, zero, and a figure above the
 * policy's single-trip limit — that refusal comes back as the error.
 */
export function TravelApproveDialog({
  open,
  onOpenChange,
  requestNumber,
  traveller,
  estimate,
  currencyCode,
  viewer,
  pending = false,
  onConfirm,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  requestNumber: string;
  traveller: string;
  estimate: number;
  currencyCode: string;
  viewer: StaffTravelViewerActions;
  pending?: boolean;
  /** Resolves when the approval went through; the dialog then closes. */
  onConfirm: (decision: { approvedBudget?: number; notes?: string }) => Promise<unknown>;
}) {
  const [budget, setBudget] = useState(String(estimate));
  const [notes, setNotes] = useState('');

  // A fresh dialog starts from the estimate, not from what was typed last time.
  useEffect(() => {
    if (open) {
      setBudget(String(estimate));
      setNotes('');
    }
  }, [open, estimate]);

  const asksBudget = viewer.isFinalStage;
  const parsed = Number(budget);
  const budgetValid = !asksBudget || (budget.trim() !== '' && Number.isFinite(parsed) && parsed > 0);

  const confirm = async () => {
    try {
      await onConfirm({
        approvedBudget: asksBudget ? parsed : undefined,
        notes: notes.trim() || undefined,
      });
      onOpenChange(false);
    } catch {
      // The caller's mutation says why; the dialog stays open with what was typed.
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Approve {requestNumber}</DialogTitle>
          <DialogDescription>
            {decidesAsSentence(viewer, traveller)}{' '}
            {asksBudget
              ? 'Your approval approves the trip.'
              : `After your approval it goes to ${viewer.nextStageName ?? 'the next approver'}, who sets the budget.`}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {asksBudget && (
            <div className="space-y-2">
              <Label htmlFor="travel-approved-budget">Approved budget ({currencyCode})</Label>
              <Input
                id="travel-approved-budget"
                type="number"
                min={0}
                step="0.01"
                inputMode="decimal"
                value={budget}
                onChange={(e) => setBudget(e.target.value)}
              />
              <p className="text-xs text-muted-foreground">
                The most this trip may spend. The estimate was {fmtTravelMoney(estimate, currencyCode)}; it
                cannot exceed the travel policy's single-trip limit.
              </p>
            </div>
          )}
          <div className="space-y-2">
            <Label htmlFor="travel-approve-notes">Notes (optional)</Label>
            <Textarea
              id="travel-approve-notes"
              rows={3}
              maxLength={2000}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Anything the next approver or the traveller should know"
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={pending}>
            Not now
          </Button>
          <Button onClick={() => void confirm()} disabled={pending || !budgetValid}>
            {asksBudget ? 'Approve the trip' : `Approve — send to ${viewer.nextStageName ?? 'the next stage'}`}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
