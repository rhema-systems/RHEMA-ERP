'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Sparkles } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { useToast } from '@/hooks/use-toast';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { FieldRow, SelectField, TextareaField } from '@/components/hr/employee/tabs/fields';
import { talentReviewService } from '@/services/hr/succession.service';
import type { TalentRatingSuggestion, TalentReviewSession } from '@/types/hr/succession';

const PERFORMANCE = [
  'Outstanding',
  'ExceedsExpectations',
  'MeetsExpectations',
  'BelowExpectations',
  'Unsatisfactory',
] as const;
const POTENTIAL = ['HighPotential', 'MediumPotential', 'LowPotential'] as const;

const spaced = (v: string) => v.replace(/([a-z])([A-Z0-9])/g, '$1 $2');
const options = (values: readonly string[]) => values.map((v) => ({ value: v, label: spaced(v) }));
const orNull = (v?: string) => (v && v.trim() !== '' ? v : null);

const schema = z.object({
  employeeId: z.string().min(1, 'Choose an employee'),
  performance: z.enum(PERFORMANCE),
  potential: z.enum(POTENTIAL),
  justification: z.string().max(2000).optional(),
  keyStrengths: z.string().max(2000).optional(),
  developmentPriorities: z.string().max(2000).optional(),
  ratedById: z.string().optional(),
});

type FormValues = z.input<typeof schema>;

/**
 * Place someone in the grid — decision D-4 in practice.
 *
 * ⚠ **Performance is suggested, never imposed.** Picking an employee fetches their latest scored
 * appraisal and pre-fills Performance, showing which appraisal it came from. The rater can change
 * it: a calibration session exists precisely to disagree with what the paperwork says, and locking
 * the axis would defeat the meeting.
 *
 * ⚠ **Potential is never suggested.** `PotentialRating` does not exist anywhere in area 5 — it is
 * only ever a human judgement. That absence is what settled D-4: the grid could not have been a
 * projection of appraisal data even had we wanted it to be.
 */
export function TalentRatingDialog({
  open,
  onOpenChange,
  session,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  session: TalentReviewSession;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [suggestion, setSuggestion] = useState<TalentRatingSuggestion | null>(null);
  const [overridden, setOverridden] = useState(false);

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      employeeId: '',
      performance: 'MeetsExpectations',
      potential: 'MediumPotential',
      justification: '',
      keyStrengths: '',
      developmentPriorities: '',
      ratedById: '',
    },
  });

  const employeeId = form.watch('employeeId');
  const performance = form.watch('performance');

  // Fetch the suggestion when the employee changes, and pre-fill Performance from it.
  useEffect(() => {
    let cancelled = false;
    setSuggestion(null);
    setOverridden(false);
    if (!employeeId) return;

    talentReviewService
      .getRatingSuggestion(employeeId)
      .then((s) => {
        if (cancelled) return;
        setSuggestion(s);
        if (s.suggestedPerformance) form.setValue('performance', s.suggestedPerformance);
      })
      .catch(() => {
        // A missing suggestion is not an error — the rater simply starts from scratch.
      });

    return () => {
      cancelled = true;
    };
  }, [employeeId, form]);

  // Track whether the rater has moved away from what the appraisal said, so the screen can ask for
  // a justification rather than silently recording a disagreement.
  useEffect(() => {
    if (suggestion?.suggestedPerformance) {
      setOverridden(performance !== suggestion.suggestedPerformance);
    }
  }, [performance, suggestion]);

  const add = useMutation({
    mutationFn: (values: FormValues) =>
      talentReviewService.addRating(session.id, {
        sessionId: session.id,
        employeeId: values.employeeId,
        performance: values.performance,
        potential: values.potential,
        justification: orNull(values.justification),
        keyStrengths: orNull(values.keyStrengths),
        developmentPriorities: orNull(values.developmentPriorities),
        ratedById: orNull(values.ratedById),
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['talent-reviews'] });
      onOpenChange(false);
      form.reset();
      toast({ title: 'Rating added' });
    },
    onError: (error: any) => {
      const status = error?.response?.status;
      toast({
        variant: 'destructive',
        title:
          status === 409
            ? 'Already rated in this session'
            : status === 403
              ? 'That is not yours to do'
              : 'Could not add the rating',
        description: error?.response?.data?.detail ?? error?.message,
      });
    },
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] max-w-2xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Place someone in the grid</DialogTitle>
          <DialogDescription>
            Performance starts from their latest appraisal; potential is the session&apos;s
            judgement. Both can be changed here — that is what calibration is for.
          </DialogDescription>
        </DialogHeader>

        <form className="space-y-4" onSubmit={form.handleSubmit((v) => add.mutate(v))}>
          <EmployeePickerField form={form} name="employeeId" label="Employee" required />

          {suggestion && (
            <div className="rounded-md border bg-muted/40 p-3 text-sm">
              <p className="flex items-center gap-2 font-medium">
                <Sparkles className="h-3.5 w-3.5" />
                What the record already says
              </p>
              <ul className="mt-1 space-y-0.5 text-muted-foreground">
                <li>
                  {suggestion.suggestedPerformance ? (
                    <>
                      Performance <strong>{spaced(suggestion.suggestedPerformance)}</strong>, from
                      appraisal {suggestion.sourceAppraisalNumber ?? '—'}
                      {suggestion.sourceOverallScore != null &&
                        ` (score ${suggestion.sourceOverallScore})`}
                    </>
                  ) : (
                    'No scored appraisal on record — start from scratch.'
                  )}
                </li>
                {suggestion.previousPerformance && (
                  <li>
                    Last placed {spaced(suggestion.previousPerformance)} /{' '}
                    {spaced(suggestion.previousPotential ?? '')} in{' '}
                    {suggestion.previousSessionName}
                  </li>
                )}
                {/* Potential has no source outside this room, and the screen should say so. */}
                <li>Potential is not derived from anything — it is this session&apos;s call.</li>
              </ul>
            </div>
          )}

          <FieldRow>
            <SelectField
              form={form}
              name="performance"
              label="Performance"
              required
              options={options(PERFORMANCE)}
            />
            <SelectField
              form={form}
              name="potential"
              label="Potential"
              required
              options={options(POTENTIAL)}
            />
          </FieldRow>

          {overridden && (
            <p className="text-sm text-amber-700 dark:text-amber-400">
              This differs from the appraisal. Say why in the justification — the disagreement is
              the useful part of a calibration session, but it should be on the record.
            </p>
          )}

          <TextareaField form={form} name="justification" label="Justification" />
          <TextareaField form={form} name="keyStrengths" label="Key strengths" />
          <TextareaField form={form} name="developmentPriorities" label="Development priorities" />
          <EmployeePickerField form={form} name="ratedById" label="Rated by (the manager)" />

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={add.isPending}>
              {add.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Add rating
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
