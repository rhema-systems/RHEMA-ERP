'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, Save, AlertTriangle, CalendarSearch, CheckCircle2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import {
  TextField,
  NumberField,
  DateField,
  TimeField,
  TextareaField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { trainingProgramService } from '@/services/hr/training-program.service';
import { trainerService } from '@/services/hr/trainer.service';
import { trainingVendorService } from '@/services/hr/training-vendor.service';
import { trainingBudgetService } from '@/services/hr/training-budget.service';
import { trainingScheduleService } from '@/services/hr/training-schedule.service';
import { TRAINING_PRIORITY_OPTIONS } from '@/types/hr/training';
import type { TrainingScheduleRequest } from '@/types/hr/training-delivery';

/**
 * A schedule must name a trainer or a vendor. The API enforces this with a 422; the same rule is
 * mirrored here as a form-level refinement so the user is told before they submit rather than after.
 */
export const trainingScheduleSchema = z
  .object({
    programId: z.string().min(1, 'Programme is required'),
    startDate: z.string().min(1, 'Required'),
    endDate: z.string().min(1, 'Required'),
    startTime: z.string().optional().or(z.literal('')),
    endTime: z.string().optional().or(z.literal('')),
    venue: z.string().max(200).optional().or(z.literal('')),
    venueAddress: z.string().max(500).optional().or(z.literal('')),
    onlineLink: z.string().max(500).optional().or(z.literal('')),
    trainerProfileId: z.string().optional().or(z.literal('')),
    vendorId: z.string().optional().or(z.literal('')),
    maxParticipants: z.coerce.number().min(1, 'At least one seat').max(10000),
    priority: z.enum(['Critical', 'High', 'Medium', 'Low']),
    registrationOpenDate: z.string().min(1, 'Required'),
    registrationCloseDate: z.string().min(1, 'Required'),
    actualCost: z.coerce.number().min(0),
    budgetNotes: z.string().max(1000).optional().or(z.literal('')),
    trainingBudgetId: z.string().optional().or(z.literal('')),
  })
  .refine((v) => !!v.trainerProfileId || !!v.vendorId, {
    message: 'Pick a trainer or a vendor — a schedule needs someone to deliver it.',
    path: ['trainerProfileId'],
  })
  .refine((v) => !v.endDate || !v.startDate || new Date(v.endDate) >= new Date(v.startDate), {
    message: 'End date cannot be before the start date.',
    path: ['endDate'],
  })
  .refine(
    (v) =>
      !v.registrationCloseDate ||
      !v.registrationOpenDate ||
      new Date(v.registrationCloseDate) >= new Date(v.registrationOpenDate),
    { message: 'Registration cannot close before it opens.', path: ['registrationCloseDate'] },
  );

export type TrainingScheduleFormValues = z.infer<typeof trainingScheduleSchema>;

export const emptyTrainingSchedule: TrainingScheduleFormValues = {
  programId: '',
  startDate: '',
  endDate: '',
  startTime: '',
  endTime: '',
  venue: '',
  venueAddress: '',
  onlineLink: '',
  trainerProfileId: '',
  vendorId: '',
  maxParticipants: 20,
  priority: 'Medium',
  registrationOpenDate: '',
  registrationCloseDate: '',
  actualCost: 0,
  budgetNotes: '',
  trainingBudgetId: '',
};

export function toScheduleRequest(v: TrainingScheduleFormValues): TrainingScheduleRequest {
  return {
    programId: v.programId,
    startDate: new Date(v.startDate).toISOString(),
    endDate: new Date(v.endDate).toISOString(),
    startTime: v.startTime || null,
    endTime: v.endTime || null,
    venue: v.venue || null,
    venueAddress: v.venueAddress || null,
    onlineLink: v.onlineLink || null,
    trainerProfileId: v.trainerProfileId || null,
    vendorId: v.vendorId || null,
    maxParticipants: v.maxParticipants,
    priority: v.priority,
    registrationOpenDate: new Date(v.registrationOpenDate).toISOString(),
    registrationCloseDate: new Date(v.registrationCloseDate).toISOString(),
    actualCost: v.actualCost,
    budgetNotes: v.budgetNotes || null,
    trainingBudgetId: v.trainingBudgetId || null,
  };
}

interface Props {
  defaultValues: TrainingScheduleFormValues;
  onSubmit: (values: TrainingScheduleFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel?: string;
  onCancel: () => void;
  /** Excluded from the trainer conflict check when editing, so a schedule never clashes with itself. */
  editingScheduleId?: string;
}

export function TrainingScheduleForm({
  defaultValues,
  onSubmit,
  submitting,
  submitLabel = 'Save schedule',
  onCancel,
  editingScheduleId,
}: Props) {
  const form = useForm<TrainingScheduleFormValues>({
    resolver: zodResolver(trainingScheduleSchema) as any,
    defaultValues,
  });

  const { data: programs } = useQuery({
    queryKey: ['hr', 'training', 'programs', 'active'],
    queryFn: () => trainingProgramService.getActive(),
  });
  const { data: trainers } = useQuery({
    queryKey: ['hr', 'training', 'trainers', 'active'],
    queryFn: () => trainerService.getActive(),
  });
  const { data: vendors } = useQuery({
    queryKey: ['hr', 'training', 'vendors', 'active'],
    queryFn: () => trainingVendorService.getActive(),
  });
  const { data: budgets } = useQuery({
    queryKey: ['hr', 'training', 'budgets', 'approved'],
    queryFn: () => trainingBudgetService.getApproved(),
  });

  const programOptions = (programs ?? []).map((p) => ({
    value: p.id,
    label: `${p.programCode} — ${p.programName}`,
  }));
  const trainerOptions = (trainers ?? []).map((t) => ({ value: t.id, label: t.name }));
  const vendorOptions = (vendors ?? []).map((v) => ({ value: v.id, label: v.name }));
  const budgetOptions = (budgets ?? []).map((b) => ({
    value: b.id,
    label: `${b.budgetCode} — ${b.periodDescription}`,
  }));

  // Trainer conflict check is explicit rather than automatic: it costs a round trip and only makes
  // sense once a trainer and both dates are chosen.
  const [checking, setChecking] = useState(false);
  const [checkResult, setCheckResult] = useState<Awaited<
    ReturnType<typeof trainingScheduleService.checkTrainerAvailability>
  > | null>(null);

  const trainerId = form.watch('trainerProfileId');
  const startDate = form.watch('startDate');
  const endDate = form.watch('endDate');
  const canCheck = !!trainerId && !!startDate && !!endDate;

  const runCheck = async () => {
    if (!canCheck) return;
    setChecking(true);
    setCheckResult(null);
    try {
      const result = await trainingScheduleService.checkTrainerAvailability(
        trainerId as string,
        new Date(startDate).toISOString(),
        new Date(endDate).toISOString(),
        editingScheduleId,
      );
      setCheckResult(result);
    } finally {
      setChecking(false);
    }
  };

  return (
    <form onSubmit={form.handleSubmit(onSubmit)}>
      <Card>
        <CardHeader>
          <CardTitle>Schedule details</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <SelectField form={form} name="programId" label="Programme" required options={programOptions} />

          <FieldRow>
            <DateField form={form} name="startDate" label="Start date" required />
            <DateField form={form} name="endDate" label="End date" required />
          </FieldRow>
          <FieldRow>
            <TimeField form={form} name="startTime" label="Start time" />
            <TimeField form={form} name="endTime" label="End time" />
          </FieldRow>

          <FieldRow>
            <SelectField
              form={form}
              name="trainerProfileId"
              label="Trainer"
              options={trainerOptions}
              allowEmpty
              emptyLabel="No individual trainer"
            />
            <SelectField
              form={form}
              name="vendorId"
              label="Vendor"
              options={vendorOptions}
              allowEmpty
              emptyLabel="No vendor"
            />
          </FieldRow>
          <p className="-mt-2 text-xs text-muted-foreground">
            Set at least one. Both is fine — a vendor supplying a named trainer.
          </p>

          {canCheck && (
            <div className="space-y-2">
              <Button type="button" variant="outline" size="sm" onClick={runCheck} disabled={checking}>
                {checking ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <CalendarSearch className="mr-2 h-4 w-4" />
                )}
                Check trainer availability
              </Button>

              {checkResult && !checkResult.hasConflicts && (
                <Alert>
                  <CheckCircle2 className="h-4 w-4" />
                  <AlertTitle>No conflicts</AlertTitle>
                  <AlertDescription>
                    This trainer has nothing else booked and no blocked time over those dates.
                  </AlertDescription>
                </Alert>
              )}

              {checkResult?.hasConflicts && (
                <Alert variant="destructive">
                  <AlertTriangle className="h-4 w-4" />
                  <AlertTitle>Trainer has a clash</AlertTitle>
                  <AlertDescription className="space-y-2">
                    {checkResult.conflictingSchedules.length > 0 && (
                      <div>
                        <p className="font-medium">Already scheduled:</p>
                        <ul className="ml-4 list-disc">
                          {checkResult.conflictingSchedules.map((c) => (
                            <li key={c.scheduleId}>
                              {c.scheduleNumber} — {c.programName} (
                              {new Date(c.startDate).toLocaleDateString()} –{' '}
                              {new Date(c.endDate).toLocaleDateString()})
                            </li>
                          ))}
                        </ul>
                      </div>
                    )}
                    {checkResult.blockedPeriods.length > 0 && (
                      <div>
                        <p className="font-medium">Blocked time:</p>
                        <ul className="ml-4 list-disc">
                          {checkResult.blockedPeriods.map((b, i) => (
                            <li key={i}>
                              {new Date(b.fromDate).toLocaleDateString()} –{' '}
                              {new Date(b.toDate).toLocaleDateString()}
                              {b.notes ? ` — ${b.notes}` : ''}
                            </li>
                          ))}
                        </ul>
                      </div>
                    )}
                    {/* Advisory, not a block: HR may knowingly double-book a higher-priority training. */}
                    <p className="text-xs">
                      This is a warning, not a block — you can still save if this training takes priority.
                    </p>
                  </AlertDescription>
                </Alert>
              )}
            </div>
          )}

          <FieldRow>
            <TextField form={form} name="venue" label="Venue" placeholder="Training Room A" />
            <TextField form={form} name="onlineLink" label="Online link" placeholder="https://…" />
          </FieldRow>
          <TextareaField form={form} name="venueAddress" label="Venue address" rows={2} />

          <FieldRow>
            <NumberField form={form} name="maxParticipants" label="Max participants" required />
            <SelectField
              form={form}
              name="priority"
              label="Priority"
              required
              options={TRAINING_PRIORITY_OPTIONS}
            />
          </FieldRow>

          <FieldRow>
            <DateField form={form} name="registrationOpenDate" label="Registration opens" required />
            <DateField form={form} name="registrationCloseDate" label="Registration closes" required />
          </FieldRow>

          <FieldRow>
            <NumberField form={form} name="actualCost" label="Cost" step="0.01" />
            <SelectField
              form={form}
              name="trainingBudgetId"
              label="Charged to budget"
              options={budgetOptions}
              allowEmpty
              emptyLabel="Not budgeted"
            />
          </FieldRow>
          <TextareaField form={form} name="budgetNotes" label="Budget notes" rows={2} />
        </CardContent>
        <CardFooter className="flex justify-end gap-2">
          <Button variant="outline" type="button" onClick={onCancel} disabled={submitting}>
            Cancel
          </Button>
          <Button type="submit" disabled={submitting}>
            {submitting ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
            {submitLabel}
          </Button>
        </CardFooter>
      </Card>
    </form>
  );
}
