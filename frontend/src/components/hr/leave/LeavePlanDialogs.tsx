'use client';

import { useEffect, useState } from 'react';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { leavePlanService } from '@/services/hr/leave.service';
import type { LeavePlan } from '@/types/hr/leave-request';

/**
 * The decisions a leave plan takes that need more than a click — each its own dialog, so the row
 * menu on /hr/leave/plans and the plan's detail window (round 5 lane E4) open the same one.
 */

interface PlanDialogProps {
  /** The plan the dialog is for; null keeps it closed. */
  plan: LeavePlan | null;
  onClose: () => void;
  /** Refetch whatever shows the plan. Called after a successful change. */
  onDone: () => Promise<unknown> | void;
}

const dateInput =
  'flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm';

function DateRangeFields({
  start,
  end,
  onStart,
  onEnd,
  startLabel,
  endLabel,
}: {
  start: string;
  end: string;
  onStart: (v: string) => void;
  onEnd: (v: string) => void;
  startLabel: string;
  endLabel: string;
}) {
  return (
    <div className="grid grid-cols-2 gap-3">
      <div className="space-y-2">
        <Label>{startLabel}</Label>
        <input type="date" className={dateInput} value={start} onChange={(e) => onStart(e.target.value)} />
      </div>
      <div className="space-y-2">
        <Label>{endLabel}</Label>
        <input type="date" className={dateInput} value={end} onChange={(e) => onEnd(e.target.value)} />
      </div>
    </div>
  );
}

/** The approver sends the plan back with dates of their own. */
export function SuggestPlanDatesDialog({ plan, onClose, onDone }: PlanDialogProps) {
  const { toast } = useToast();
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [notes, setNotes] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    setStart(plan?.startDate?.slice(0, 10) ?? '');
    setEnd(plan?.endDate?.slice(0, 10) ?? '');
    setNotes('');
  }, [plan?.id]);

  return (
    <ConfirmationDialog
      open={plan !== null}
      onOpenChange={(open) => !open && onClose()}
      title="Suggest different dates"
      description="The plan goes back to the employee, who accepts these dates or proposes their own."
      confirmText="Send suggestion"
      isLoading={busy}
      onConfirm={async () => {
        if (!plan) return false;
        if (!start || !end || end < start) {
          toast({ title: 'Both dates are required, the end on or after the start', variant: 'destructive' });
          return false;
        }
        setBusy(true);
        try {
          await leavePlanService.suggestChanges(plan.id, {
            suggestedStartDate: start,
            suggestedEndDate: end,
            notes: notes.trim() || null,
          });
          await onDone();
          toast({ title: 'Sent', description: 'Suggested dates sent to the employee.' });
          onClose();
          return true;
        } catch (e: any) {
          toast({ title: 'Could not send the suggestion', description: e?.message, variant: 'destructive' });
          return false;
        } finally {
          setBusy(false);
        }
      }}
    >
      <div className="space-y-3">
        <DateRangeFields
          start={start}
          end={end}
          onStart={setStart}
          onEnd={setEnd}
          startLabel="Suggested start"
          endLabel="Suggested end"
        />
        <div className="space-y-2">
          <Label>Notes</Label>
          <Textarea rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
        </div>
      </div>
    </ConfirmationDialog>
  );
}

/**
 * The employee's answer to a suggestion when they do not take it: dates of their own.
 *
 * ⚠ Round 5 lane E4: "Decline suggestion" used to send `{ accept: false }` with no dates, which the
 * server always refused ("Provide your preferred start and end dates") — declining means proposing.
 */
export function ProposePlanDatesDialog({ plan, onClose, onDone }: PlanDialogProps) {
  const { toast } = useToast();
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [notes, setNotes] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    setStart(plan?.startDate?.slice(0, 10) ?? '');
    setEnd(plan?.endDate?.slice(0, 10) ?? '');
    setNotes('');
  }, [plan?.id]);

  return (
    <ConfirmationDialog
      open={plan !== null}
      onOpenChange={(open) => !open && onClose()}
      title="Propose other dates"
      description={
        plan?.suggestedStartDate
          ? `The suggestion was ${plan.suggestedStartDate.slice(0, 10)} to ${plan.suggestedEndDate?.slice(0, 10) ?? ''}. ` +
            'Propose the dates that work instead — the plan goes back for review.'
          : 'Propose the dates that work — the plan goes back for review.'
      }
      confirmText="Send my dates"
      isLoading={busy}
      onConfirm={async () => {
        if (!plan) return false;
        if (!start || !end || end < start) {
          toast({ title: 'Both dates are required, the end on or after the start', variant: 'destructive' });
          return false;
        }
        setBusy(true);
        try {
          await leavePlanService.respondToSuggestion(plan.id, {
            accept: false,
            startDate: start,
            endDate: end,
            notes: notes.trim() || null,
          });
          await onDone();
          toast({ title: 'Sent', description: 'The plan is back for review with the new dates.' });
          onClose();
          return true;
        } catch (e: any) {
          toast({ title: 'Could not send the dates', description: e?.message, variant: 'destructive' });
          return false;
        } finally {
          setBusy(false);
        }
      }}
    >
      <div className="space-y-3">
        <DateRangeFields start={start} end={end} onStart={setStart} onEnd={setEnd} startLabel="From" endLabel="To" />
        <div className="space-y-2">
          <Label>Notes</Label>
          <Textarea rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
        </div>
      </div>
    </ConfirmationDialog>
  );
}

/** The approver turns the plan down, saying why. */
export function RejectPlanDialog({ plan, onClose, onDone }: PlanDialogProps) {
  const { toast } = useToast();
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => setReason(''), [plan?.id]);

  return (
    <ConfirmationDialog
      open={plan !== null}
      onOpenChange={(open) => !open && onClose()}
      title="Reject leave plan?"
      confirmText="Reject"
      variant="destructive"
      isLoading={busy}
      onConfirm={async () => {
        if (!plan) return false;
        if (!reason.trim()) {
          toast({ title: 'A reason is required', variant: 'destructive' });
          return false;
        }
        setBusy(true);
        try {
          await leavePlanService.reject(plan.id, reason.trim());
          await onDone();
          toast({ title: 'Rejected', description: 'Leave plan rejected.' });
          onClose();
          return true;
        } catch (e: any) {
          toast({ title: 'Could not reject the plan', description: e?.message, variant: 'destructive' });
          return false;
        } finally {
          setBusy(false);
        }
      }}
    >
      <div className="space-y-2">
        <Label>Reason</Label>
        <Textarea rows={3} value={reason} onChange={(e) => setReason(e.target.value)} />
      </div>
    </ConfirmationDialog>
  );
}

/**
 * Cancel a plan (round 5 lane E5). A reason is required for an APPROVED plan — cancelling it takes
 * back something that was agreed — and optional otherwise. The server applies the same rule.
 */
export function CancelPlanDialog({ plan, onClose, onDone }: PlanDialogProps) {
  const { toast } = useToast();
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const reasonRequired = plan?.status === 'Approved';

  useEffect(() => setReason(''), [plan?.id]);

  return (
    <ConfirmationDialog
      open={plan !== null}
      onOpenChange={(open) => !open && onClose()}
      title="Cancel this leave plan?"
      description={
        reasonRequired
          ? 'This plan was approved. Cancelling takes back something that was agreed, so say why — the employee will see it.'
          : plan?.status === 'Submitted'
            ? 'The plan is withdrawn from its approver as well.'
            : undefined
      }
      confirmText="Cancel plan"
      cancelText="Keep it"
      variant="destructive"
      isLoading={busy}
      onConfirm={async () => {
        if (!plan) return false;
        if (reasonRequired && !reason.trim()) {
          toast({ title: 'Say why the approved plan is being cancelled', variant: 'destructive' });
          return false;
        }
        setBusy(true);
        try {
          await leavePlanService.cancel(plan.id, reason);
          await onDone();
          toast({ title: 'Cancelled', description: 'The leave plan was cancelled.' });
          onClose();
          return true;
        } catch (e: any) {
          toast({ title: 'Could not cancel the plan', description: e?.message, variant: 'destructive' });
          return false;
        } finally {
          setBusy(false);
        }
      }}
    >
      <div className="space-y-2">
        <Label>Reason{reasonRequired ? '' : ' (optional)'}</Label>
        <Textarea rows={3} value={reason} onChange={(e) => setReason(e.target.value)} />
      </div>
    </ConfirmationDialog>
  );
}
