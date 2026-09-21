'use client';

import { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { CheckCircle2, Loader2, MoveRight, PauseCircle, Undo2, UserMinus, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { jobVacancyService } from '@/services/hr/recruitment.service';
import {
  jobApplicationService,
  recruitmentPipelineService,
} from '@/services/hr/recruitment-pipeline.service';
import {
  TERMINAL_APPLICATION_STATUSES,
  type JobApplication,
} from '@/types/hr/recruitment-pipeline';

type Action = 'shortlist' | 'unshortlist' | 'waitlist' | 'reject' | 'withdraw' | 'move';

const ACTION_COPY: Record<Action, { title: string; description: string; label: string; required: boolean }> = {
  shortlist: {
    title: 'Shortlist this application',
    description:
      'Refused after the vacancy’s shortlisting deadline. Shortlisting also resets any shortlist already sent for approval, so the approver reviews the full list again.',
    label: 'Shortlisting notes',
    required: false,
  },
  unshortlist: {
    title: 'Remove from the shortlist',
    description: 'The application returns to Under Review. An approved shortlist is reset.',
    label: 'Reason',
    required: false,
  },
  waitlist: {
    title: 'Waitlist this application',
    description: 'A hold, not a rejection — the candidate stays in contention if someone drops out.',
    label: 'Reason',
    required: false,
  },
  reject: {
    title: 'Reject this application',
    description:
      'Records the decision and closes the pipeline stage. The candidate is not emailed until rejection notifications are sent for the vacancy.',
    label: 'Rejection reason',
    required: true,
  },
  withdraw: {
    title: 'Record a withdrawal',
    description: 'Use when the candidate has withdrawn. This is not a rejection.',
    label: 'Withdrawal reason',
    required: true,
  },
  move: {
    title: 'Move to a pipeline stage',
    description:
      'The server enforces the transition rules — stage order, whether a stage can be re-entered, and its attempt limit. An illegal move comes back explained.',
    label: 'Note',
    required: false,
  },
};

/**
 * The decision actions on one application.
 *
 * ⚠ **The server owns every rule here.** Terminal statuses are dimmed as a courtesy, but the real
 * refusals — a passed shortlisting deadline, an already-shortlisted application, a stage at its
 * attempt limit — come back as 422 with the rule's own sentence, which is what gets shown. Nothing
 * is mirrored client-side.
 *
 * ⚠ **Permission gate added 2026-09-15 (G-8.2).** Every button here used to render for any viewer
 * who could load the page. Nothing unsafe happened — the server refuses with 403 for anyone
 * lacking `HR.Recruitment.Write` — but the same page gates *Record an application* on
 * `hasAnyPermission`, and so does the list page, so the inconsistency sat inside one feature. A
 * read-only viewer now sees the record without a row of buttons that would all fail.
 */
export function ApplicationDecisionBar({
  application,
  onChanged,
}: {
  application: JobApplication;
  onChanged: () => Promise<unknown>;
}) {
  const { toast } = useToast();
  const { hasAnyPermission } = useAuth();
  const canDecide = hasAnyPermission(['HR.Recruitment.Write', 'HR.Recruitment.Admin']);
  const [action, setAction] = useState<Action | null>(null);
  const [text, setText] = useState('');
  const [stageId, setStageId] = useState('');

  // Stages come from the vacancy's own pipeline; loaded only when the move dialog opens.
  const vacancy = useQuery({
    queryKey: ['hr', 'vacancies', application.jobVacancyId],
    queryFn: () => jobVacancyService.getById(application.jobVacancyId),
    enabled: action === 'move',
  });

  const pipelineId = vacancy.data?.recruitmentPipelineId ?? undefined;

  const stages = useQuery({
    queryKey: ['hr', 'pipeline-stages', pipelineId],
    queryFn: () =>
      pipelineId ? recruitmentPipelineService.getStages(pipelineId) : Promise.resolve([]),
    enabled: action === 'move' && !!pipelineId,
  });

  const run = useMutation({
    mutationFn: async () => {
      const id = application.id;
      const note = text.trim();
      switch (action) {
        case 'shortlist':
          return jobApplicationService.shortlist(id, note || null);
        case 'unshortlist':
          return jobApplicationService.unshortlist(id, note || null);
        case 'waitlist':
          return jobApplicationService.waitlist(id, note || null);
        case 'reject':
          return jobApplicationService.reject(id, note);
        case 'withdraw':
          return jobApplicationService.withdraw(id, note);
        case 'move':
          return jobApplicationService.moveToStage(id, stageId, note || null);
        default:
          return undefined;
      }
    },
    onSuccess: async () => {
      await onChanged();
      close();
      toast({ title: 'Application updated' });
    },
    // The refusal carries the rule's own message — the deadline date, the attempt limit, the
    // current status. That sentence is the useful part.
    onError: (e: any) => toast({ title: 'Refused', description: e?.message, variant: 'destructive' }),
  });

  const close = () => {
    setAction(null);
    setText('');
    setStageId('');
  };

  const isTerminal = TERMINAL_APPLICATION_STATUSES.includes(application.status);
  const copy = action ? ACTION_COPY[action] : null;
  const canSubmit =
    action === 'move'
      ? !!stageId
      : copy
        ? !copy.required || text.trim().length > 0
        : false;

  if (!canDecide) return null;

  return (
    <>
      <div className="flex flex-wrap gap-2">
        {application.isShortlisted ? (
          <Button variant="outline" disabled={isTerminal} onClick={() => setAction('unshortlist')}>
            <Undo2 className="mr-2 h-4 w-4" />
            Un-shortlist
          </Button>
        ) : (
          <Button disabled={isTerminal} onClick={() => setAction('shortlist')}>
            <CheckCircle2 className="mr-2 h-4 w-4" />
            Shortlist
          </Button>
        )}
        <Button variant="outline" disabled={isTerminal} onClick={() => setAction('move')}>
          <MoveRight className="mr-2 h-4 w-4" />
          Move stage
        </Button>
        <Button variant="outline" disabled={isTerminal} onClick={() => setAction('waitlist')}>
          <PauseCircle className="mr-2 h-4 w-4" />
          Waitlist
        </Button>
        <Button variant="outline" disabled={isTerminal} onClick={() => setAction('reject')}>
          <XCircle className="mr-2 h-4 w-4" />
          Reject
        </Button>
        <Button
          variant="outline"
          disabled={application.status === 'Hired'}
          onClick={() => setAction('withdraw')}
        >
          <UserMinus className="mr-2 h-4 w-4" />
          Withdrawn
        </Button>
      </div>

      <Dialog open={!!action} onOpenChange={(o) => !o && close()}>
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>{copy?.title}</DialogTitle>
            <DialogDescription>{copy?.description}</DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            {action === 'move' && (
              <div className="space-y-1.5">
                <Label>Target stage</Label>
                {!pipelineId && !vacancy.isLoading ? (
                  <p className="text-sm text-muted-foreground">
                    This vacancy has no pipeline assigned, so its applications cannot be moved
                    through stages. Assign one on the vacancy first.
                  </p>
                ) : (
                  <Select value={stageId} onValueChange={setStageId}>
                    <SelectTrigger>
                      <SelectValue placeholder={stages.isLoading ? 'Loading…' : 'Select a stage'} />
                    </SelectTrigger>
                    <SelectContent>
                      {[...(stages.data ?? [])]
                        .sort((a, b) => a.order - b.order)
                        .map((s) => (
                          <SelectItem key={s.id} value={s.id}>
                            {s.order}. {s.name}
                          </SelectItem>
                        ))}
                    </SelectContent>
                  </Select>
                )}
              </div>
            )}

            <div className="space-y-1.5">
              <Label>
                {copy?.label}
                {copy?.required && <span className="ml-0.5 text-red-500">*</span>}
              </Label>
              <Textarea value={text} onChange={(e) => setText(e.target.value)} rows={4} />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={close}>
              Cancel
            </Button>
            <Button disabled={!canSubmit || run.isPending} onClick={() => run.mutate()}>
              {run.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Confirm
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
