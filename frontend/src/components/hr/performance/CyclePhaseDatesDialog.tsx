'use client';

import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Form } from '@/components/ui/form';
import { CyclePhaseDateFields } from '@/components/hr/performance/CycleFormFields';
import { useToast } from '@/hooks/use-toast';
import { appraisalCycleService } from '@/services/hr/appraisal.service';
import type { AppraisalCycle } from '@/types/hr/appraisal';

/** `<input type="date">` wants an empty string, never null. */
const dayInput = (value?: string | null) => value?.slice(0, 10) ?? '';
/** Blank means "this phase has no deadline", which is null on the wire. */
const nullable = (value?: string) => (value && value.trim() !== '' ? value : null);

type PhaseDatesForm = Record<string, string>;

const FIELDS = [
  'goalSettingOpenDate', 'goalSettingDeadline',
  'q1ReviewOpenDate', 'q1ReviewDeadline',
  'midYearOpenDate', 'midYearDeadline',
  'q3ReviewOpenDate', 'q3ReviewDeadline',
  'peerNominationDeadline',
  'selfEvaluationOpenDate', 'selfEvaluationDeadline',
  'peerEvaluationOpenDate', 'peerEvaluationDeadline',
  'managerEvaluationOpenDate', 'managerEvaluationDeadline',
  'calibrationOpenDate', 'calibrationDeadline',
  'hrReviewOpenDate', 'hrReviewDeadline',
  'employeeAcknowledgeDeadline',
  'finalConversationDeadline',
] as const;

interface CyclePhaseDatesDialogProps {
  cycle: AppraisalCycle;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/**
 * Edits a cycle's phase dates from its own detail page.
 *
 * These were previously read-only here with a note to "edit these from the cycle list", which meant
 * leaving the record you are looking at to change a date on it — and the list dialog is the whole
 * cycle form, so a phase-date tweak went through every other field on the way.
 *
 * ⚠ The update endpoint takes the **whole cycle**, so this sends the unchanged identity fields back
 * alongside the dates. It does *not* send `status`: that belongs to open/close/reopen, and posting
 * it here is what used to revert an Open cycle to Draft.
 */
export function CyclePhaseDatesDialog({ cycle, open, onOpenChange }: CyclePhaseDatesDialogProps) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const form = useForm<PhaseDatesForm>({
    defaultValues: Object.fromEntries(FIELDS.map((f) => [f, ''])),
  });

  useEffect(() => {
    if (!open) return;
    form.reset(
      Object.fromEntries(
        FIELDS.map((f) => [f, dayInput(cycle[f as keyof AppraisalCycle] as string | null)]),
      ),
    );
  }, [open, cycle, form]);

  const save = useMutation({
    mutationFn: (values: PhaseDatesForm) =>
      appraisalCycleService.update(cycle.id, {
        id: cycle.id,
        cycleCode: cycle.cycleCode,
        cycleName: cycle.cycleName,
        year: cycle.year,
        appraisalType: cycle.appraisalType,
        startDate: cycle.startDate,
        endDate: cycle.endDate,
        appraisalSettingsId: cycle.appraisalSettingsId,
        ...(Object.fromEntries(
          FIELDS.map((f) => [f, nullable(values[f])]),
        ) as Record<(typeof FIELDS)[number], string | null>),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'appraisal-cycle', cycle.id] });
      queryClient.invalidateQueries({ queryKey: ['hr', 'appraisal-cycles'] });
      // The calendar and the deadline risks are both derived from these dates.
      queryClient.invalidateQueries({ queryKey: ['hr', 'cycle-progress', cycle.id] });
      onOpenChange(false);
      toast({ title: 'Phase dates updated' });
    },
    onError: (err: unknown) =>
      toast({
        variant: 'destructive',
        title: 'Could not save the phase dates',
        description: (err as Error)?.message ?? 'Please try again.',
      }),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[720px]">
        <DialogHeader>
          <DialogTitle>Phase dates</DialogTitle>
          <DialogDescription>
            Every date is optional. A phase with no deadline never appears on the calendar or in
            reminders.
          </DialogDescription>
        </DialogHeader>

        <Form {...form}>
          <div className="max-h-[60vh] overflow-y-auto pr-1">
            <CyclePhaseDateFields form={form} />
          </div>
        </Form>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={form.handleSubmit((v) => save.mutate(v))} disabled={save.isPending}>
            {save.isPending ? 'Saving…' : 'Save dates'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
