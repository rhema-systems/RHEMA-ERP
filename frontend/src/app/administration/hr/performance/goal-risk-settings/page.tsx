'use client';

import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, RotateCcw, TriangleAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NumberField, FieldRow } from '@/components/hr/employee/tabs/fields';
import { goalRiskSettingsService } from '@/services/hr/goals.service';
import { formatDateTime } from '@/lib/hr/attendance-format';
import { useState } from 'react';

/**
 * What "at risk" means for this tenant.
 *
 * Two rules use these numbers, and both fire on goals that are still in flight:
 *   1. Close to the deadline with too little done — inside `daysRemainingThreshold` of the due
 *      date and below `minimumProgressPercent`.
 *   2. Behind the run rate — actual progress plus `expectedProgressTolerancePercent` is still
 *      under the progress a straight line from start to due date would predict by now.
 *
 * Nothing seeds these, so until someone saves here the evaluator runs on the built-in
 * defaults. Saving is what creates the tenant's own row; resetting drops it again.
 */
const settingsSchema = z.object({
  daysRemainingThreshold: z.coerce.number().int().min(1).max(365),
  minimumProgressPercent: z.coerce.number().int().min(0).max(100),
  expectedProgressTolerancePercent: z.coerce.number().int().min(0).max(100),
});

type SettingsForm = z.input<typeof settingsSchema>;

export default function GoalRiskSettingsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [confirmReset, setConfirmReset] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'goal-risk-settings'],
    queryFn: () => goalRiskSettingsService.get(),
  });

  const form = useForm<SettingsForm>({
    resolver: zodResolver(settingsSchema) as any,
    defaultValues: {
      daysRemainingThreshold: 14,
      minimumProgressPercent: 60,
      expectedProgressTolerancePercent: 20,
    },
  });

  // Seed the form from whatever is in force — the tenant's own row, or the defaults.
  useEffect(() => {
    if (!data) return;
    form.reset({
      daysRemainingThreshold: data.daysRemainingThreshold,
      minimumProgressPercent: data.minimumProgressPercent,
      expectedProgressTolerancePercent: data.expectedProgressTolerancePercent,
    });
  }, [data, form]);

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'goal-risk-settings'] });
    // Every at-risk read is computed against these, so the cached ones are now wrong.
    await queryClient.invalidateQueries({ queryKey: ['hr', 'team-goals'] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'goals-at-risk'] });
  };

  const saveMutation = useMutation({
    mutationFn: (values: SettingsForm) => goalRiskSettingsService.save(settingsSchema.parse(values)),
    onSuccess: async () => {
      await invalidate();
      toast({ title: 'Saved', description: 'Goal risk thresholds updated.' });
    },
    onError: (e: any) =>
      toast({
        title: 'Error',
        description: e?.message || 'Failed to save the thresholds.',
        variant: 'destructive',
      }),
  });

  const resetMutation = useMutation({
    mutationFn: () => goalRiskSettingsService.reset(),
    onSuccess: async () => {
      await invalidate();
      setConfirmReset(false);
      toast({ title: 'Reset', description: 'Back to the built-in defaults (14 days / 60% / 20%).' });
    },
    onError: (e: any) => {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to reset the thresholds.',
        variant: 'destructive',
      });
      setConfirmReset(false);
    },
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Goal Risk Thresholds"
        description="When a goal is flagged at risk, across the manager workspace and the org-wide report."
        backHref="/administration/hr/performance"
        actions={
          data?.isConfigured ? (
            <Button variant="outline" onClick={() => setConfirmReset(true)}>
              <RotateCcw className="mr-2 h-4 w-4" />
              Reset to defaults
            </Button>
          ) : undefined
        }
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : (
        <>
          {!data?.isConfigured && (
            <Alert>
              <TriangleAlert className="h-4 w-4" />
              <AlertTitle>Running on the defaults</AlertTitle>
              <AlertDescription>
                No thresholds have been saved for this tenant, so goal risk is evaluated with the
                built-in values below. Saving stores them and makes the choice explicit.
              </AlertDescription>
            </Alert>
          )}

          <form onSubmit={form.handleSubmit((values) => saveMutation.mutate(values))}>
            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="text-base">Thresholds</CardTitle>
              </CardHeader>
              <CardContent className="space-y-6">
                <div className="space-y-3">
                  <p className="text-sm font-medium">Rule 1 — close to the deadline</p>
                  <p className="text-sm text-muted-foreground">
                    A goal within this many days of its due date, that has not reached this much
                    progress, is flagged.
                  </p>
                  <FieldRow>
                    <NumberField
                      form={form}
                      name="daysRemainingThreshold"
                      label="Days remaining"
                      required
                    />
                    <NumberField
                      form={form}
                      name="minimumProgressPercent"
                      label="Minimum progress (%)"
                      required
                    />
                  </FieldRow>
                </div>

                <div className="space-y-3 border-t pt-6">
                  <p className="text-sm font-medium">Rule 2 — behind the run rate</p>
                  <p className="text-sm text-muted-foreground">
                    Progress is compared against a straight line from start date to due date. This
                    is the slack allowed before a goal counts as behind — a larger number is more
                    lenient.
                  </p>
                  <FieldRow>
                    <NumberField
                      form={form}
                      name="expectedProgressTolerancePercent"
                      label="Tolerance (%)"
                      required
                    />
                    <div />
                  </FieldRow>
                </div>

                <div className="flex items-center justify-between border-t pt-6">
                  <p className="text-xs text-muted-foreground">
                    {data?.isConfigured && data.updatedAt
                      ? `Last changed ${formatDateTime(data.updatedAt)}${
                          data.updatedBy ? ` by ${data.updatedBy}` : ''
                        }.`
                      : 'Applies to every at-risk view as soon as it is saved.'}
                  </p>
                  <Button type="submit" disabled={saveMutation.isPending}>
                    {saveMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                    Save thresholds
                  </Button>
                </div>
              </CardContent>
            </Card>
          </form>
        </>
      )}

      <ConfirmationDialog
        open={confirmReset}
        onOpenChange={setConfirmReset}
        title="Reset to defaults?"
        description="The tenant's stored thresholds are dropped and goal risk goes back to 14 days / 60% / 20%."
        confirmText="Reset"
        isLoading={resetMutation.isPending}
        onConfirm={async () => {
          await resetMutation.mutateAsync();
        }}
      />
    </div>
  );
}
