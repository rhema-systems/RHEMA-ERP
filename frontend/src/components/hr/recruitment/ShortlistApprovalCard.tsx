'use client';

import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Loader2, Send, ThumbsDown, ThumbsUp, Undo2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Textarea } from '@/components/ui/textarea';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { jobApplicationService } from '@/services/hr/recruitment-pipeline.service';
import type { ShortlistSummary } from '@/types/hr/recruitment-pipeline';

type Step = 'submit' | 'approve' | 'reject' | 'recall';

/**
 * Shortlist approval for one vacancy.
 *
 * ⚠ **Not on the workflow engine.** This is a flag on the vacancy with several writers —
 * shortlisting, un-shortlisting and auto-shortlisting all reset an in-flight decision back to
 * NotSubmitted so the approver sees the full list again. That is the same many-writers shape that
 * kept `AppraisalStatus` off the engine.
 *
 * Two rules the server enforces and this card explains up front, because both are easy to trip:
 * an **empty shortlist cannot be submitted**, and **the submitter cannot approve their own**.
 */
export function ShortlistApprovalCard({
  vacancyId,
  summary,
  onChanged,
}: {
  vacancyId: string;
  summary?: ShortlistSummary;
  onChanged: () => Promise<unknown>;
}) {
  const { toast } = useToast();
  const [step, setStep] = useState<Step | null>(null);
  const [notes, setNotes] = useState('');

  const run = useMutation({
    mutationFn: () => {
      const note = notes.trim() || null;
      switch (step) {
        case 'submit':
          return jobApplicationService.submitShortlistForApproval(vacancyId, note);
        case 'approve':
          return jobApplicationService.reviewShortlistApproval(vacancyId, true, note);
        case 'reject':
          return jobApplicationService.reviewShortlistApproval(vacancyId, false, note);
        case 'recall':
          return jobApplicationService.recallShortlistApproval(vacancyId);
        default:
          return Promise.resolve();
      }
    },
    onSuccess: async () => {
      await onChanged();
      setStep(null);
      setNotes('');
      toast({ title: 'Shortlist updated' });
    },
    onError: (e: any) => toast({ title: 'Refused', description: e?.message, variant: 'destructive' }),
  });

  const status = summary?.approvalStatus ?? 'NotSubmitted';
  const nothingShortlisted = (summary?.shortlisted ?? 0) === 0;

  return (
    <>
      <Card>
        <CardHeader className="flex-row items-center justify-between space-y-0 pb-3">
          <CardTitle className="text-base">Shortlist approval</CardTitle>
          <StatusBadge status={summary?.approvalStatusName ?? 'Not submitted'} />
        </CardHeader>
        <CardContent className="space-y-3">
          <p className="text-sm text-muted-foreground">
            {status === 'NotSubmitted' &&
              (nothingShortlisted
                ? 'Nothing is shortlisted yet, so there is nothing to send for approval.'
                : `${summary?.shortlisted} shortlisted and ready to send for approval.`)}
            {status === 'PendingApproval' &&
              'Waiting on an approver. Whoever submitted it cannot be the one to sign it off.'}
            {status === 'Approved' &&
              'Approved. Changing the shortlist now resets this back to not submitted.'}
            {status === 'Rejected' && 'Sent back. Revise the shortlist and submit again.'}
          </p>

          <div className="flex flex-wrap gap-2">
            {status !== 'PendingApproval' && (
              <Button
                size="sm"
                disabled={nothingShortlisted}
                title={nothingShortlisted ? 'Shortlist at least one application first.' : undefined}
                onClick={() => setStep('submit')}
              >
                <Send className="mr-2 h-3.5 w-3.5" />
                Send for approval
              </Button>
            )}
            {status === 'PendingApproval' && (
              <>
                <Button size="sm" onClick={() => setStep('approve')}>
                  <ThumbsUp className="mr-2 h-3.5 w-3.5" />
                  Approve
                </Button>
                <Button size="sm" variant="outline" onClick={() => setStep('reject')}>
                  <ThumbsDown className="mr-2 h-3.5 w-3.5" />
                  Send back
                </Button>
                <Button size="sm" variant="ghost" onClick={() => setStep('recall')}>
                  <Undo2 className="mr-2 h-3.5 w-3.5" />
                  Recall
                </Button>
              </>
            )}
          </div>
        </CardContent>
      </Card>

      <Dialog open={!!step} onOpenChange={(o) => !o && setStep(null)}>
        <DialogContent className="sm:max-w-[480px]">
          <DialogHeader>
            <DialogTitle>
              {step === 'submit' && 'Send the shortlist for approval'}
              {step === 'approve' && 'Approve this shortlist'}
              {step === 'reject' && 'Send this shortlist back'}
              {step === 'recall' && 'Recall the submission'}
            </DialogTitle>
            <DialogDescription>
              {step === 'submit' &&
                'The approver reviews everyone currently shortlisted. Adding or removing anyone afterwards resets the decision.'}
              {step === 'approve' &&
                'Refused if you are the person who submitted it — approval has to come from someone else.'}
              {step === 'reject' && 'The shortlist returns to the recruiter to revise.'}
              {step === 'recall' && 'Takes the shortlist back from the approver without a decision.'}
            </DialogDescription>
          </DialogHeader>

          {step !== 'recall' && (
            <Textarea
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              rows={4}
              placeholder="Notes (optional)"
            />
          )}

          <DialogFooter>
            <Button variant="outline" onClick={() => setStep(null)}>
              Cancel
            </Button>
            <Button disabled={run.isPending} onClick={() => run.mutate()}>
              {run.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Confirm
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
