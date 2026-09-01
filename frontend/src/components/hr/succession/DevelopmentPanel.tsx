'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  CalendarClock,
  Check,
  ExternalLink,
  Flag,
  GraduationCap,
  Loader2,
  Plus,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  DateField,
  FieldRow,
  NumberField,
  SelectField,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { hrCurrencyService } from '@/services/hr/hr-currency.service';
import { successionDevelopmentService } from '@/services/hr/succession.service';
import type { SuccessionCandidate } from '@/types/hr/succession';

const ACTIVITY_TYPES = [
  'Training',
  'Mentoring',
  'JobRotation',
  'ProjectAssignment',
  'Shadowing',
  'ActingRole',
  'ExternalExperience',
  'Certification',
  'Other',
] as const;

const ACTIVITY_STATUSES = ['Planned', 'InProgress', 'Completed', 'Deferred', 'Cancelled'] as const;

const spaced = (v?: string | null) => (v ? v.replace(/([a-z])([A-Z0-9])/g, '$1 $2') : '—');
const options = (values: readonly string[]) => values.map((v) => ({ value: v, label: spaced(v) }));
const orNull = (v?: string) => (v && v.trim() !== '' ? v : null);
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/** Money is rendered in the row's own currency — never a hardcoded symbol. */
const fmtMoney = (amount?: number | null, currency?: string | null) =>
  amount === null || amount === undefined
    ? '—'
    : new Intl.NumberFormat(undefined, {
        style: 'currency',
        currency: currency || 'GHS',
        currencyDisplay: 'code',
      }).format(amount);

const schema = z.object({
  activityName: z.string().min(1, 'Required').max(200),
  type: z.enum(ACTIVITY_TYPES),
  description: z.string().max(2000).optional(),
  plannedStartDate: z.string().min(1, 'Required'),
  plannedEndDate: z.string().optional(),
  status: z.enum(ACTIVITY_STATUSES),
  estimatedCost: z.coerce.number().min(0).optional(),
  currencyCode: z.string().optional(),
  supervisorId: z.string().optional(),
  externalProviderName: z.string().max(200).optional(),
  notes: z.string().max(2000).optional(),
}).refine((v) => !v.plannedEndDate || v.plannedEndDate >= v.plannedStartDate, {
  message: 'The end date cannot be before the start',
  path: ['plannedEndDate'],
}).refine((v) => v.estimatedCost === undefined || !!v.currencyCode, {
  // The server enforces this too; saying it here saves a round trip.
  message: 'A cost needs a currency',
  path: ['currencyCode'],
});

type FormValues = z.input<typeof schema>;

/**
 * What a candidate is being put through to become ready: training, secondments, acting roles, and
 * the milestones each one is tracked against.
 *
 * ⚠ **Mentoring is not built here.** A `Mentoring` activity records *that* a candidate is being
 * mentored as part of their succession development; the mentoring programme itself — pairs,
 * sessions, the whole engine — belongs to area 7 and already exists at `/hr/training/mentoring`.
 * The boundary call (§4 of the build plan) was to link by reference rather than duplicate it.
 */
export function DevelopmentPanel({ candidate }: { candidate: SuccessionCandidate }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [creating, setCreating] = useState(false);
  const [milestoneFor, setMilestoneFor] = useState<string | null>(null);
  const [milestoneName, setMilestoneName] = useState('');
  const [milestoneDate, setMilestoneDate] = useState('');

  const { data: activities, isLoading } = useQuery({
    queryKey: ['succession-development', candidate.id],
    queryFn: () => successionDevelopmentService.getByCandidateFull(candidate.id),
  });

  // Only currencies Finance actually holds — the server rejects anything else, so offering a free
  // text box would be offering a way to fail.
  const { data: currencies } = useQuery({
    queryKey: ['finance', 'currencies'],
    queryFn: () => hrCurrencyService.getActive(),
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['succession-development', candidate.id] });
    await queryClient.invalidateQueries({ queryKey: ['succession-candidates'] });
  };

  const fail = (fallback: string) => (error: any) => {
    const status = error?.response?.status;
    toast({
      variant: 'destructive',
      title: status === 403 ? 'That is not yours to do' : fallback,
      description: error?.response?.data?.detail ?? error?.message,
    });
  };

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      activityName: '',
      type: 'Training',
      description: '',
      plannedStartDate: '',
      plannedEndDate: '',
      status: 'Planned',
      estimatedCost: undefined,
      currencyCode: '',
      supervisorId: '',
      externalProviderName: '',
      notes: '',
    },
  });

  const create = useMutation({
    mutationFn: (values: FormValues) =>
      successionDevelopmentService.create({
        candidateId: candidate.id,
        activityName: values.activityName,
        type: values.type,
        description: orNull(values.description),
        plannedStartDate: values.plannedStartDate,
        plannedEndDate: orNull(values.plannedEndDate),
        status: values.status,
        estimatedCost:
          values.estimatedCost === undefined || values.estimatedCost === null
            ? null
            : Number(values.estimatedCost),
        currencyCode: orNull(values.currencyCode),
        supervisorId: orNull(values.supervisorId),
        externalProviderName: orNull(values.externalProviderName),
        notes: orNull(values.notes),
      }),
    onSuccess: async () => {
      await refresh();
      setCreating(false);
      form.reset();
      toast({ title: 'Development activity added' });
    },
    onError: fail('Could not add the activity'),
  });

  const addMilestone = useMutation({
    mutationFn: (activityId: string) =>
      successionDevelopmentService.addMilestone(activityId, {
        activityId,
        milestoneName,
        targetDate: milestoneDate,
      }),
    onSuccess: async () => {
      await refresh();
      setMilestoneFor(null);
      setMilestoneName('');
      setMilestoneDate('');
      toast({ title: 'Milestone added' });
    },
    onError: fail('Could not add the milestone'),
  });

  const completeMilestone = useMutation({
    mutationFn: (milestoneId: string) =>
      successionDevelopmentService.completeMilestone(milestoneId),
    onSuccess: async () => {
      await refresh();
      toast({
        title: 'Milestone completed',
        // Stated because the absence of a status change looks like a bug otherwise.
        description: "The activity's own status is unchanged — whether it is finished is the supervisor's call.",
      });
    },
    onError: fail('Could not complete the milestone'),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const rows = activities ?? [];
  const today = new Date().toISOString().slice(0, 10);

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <Button size="sm" onClick={() => setCreating(true)}>
          <Plus className="mr-2 h-4 w-4" />
          Add activity
        </Button>
      </div>

      {rows.length === 0 ? (
        <EmptyState
          icon={GraduationCap}
          title="Nothing planned yet"
          description="Readiness does not improve on its own. Record the training, secondment or acting role that will close the gap."
        />
      ) : (
        <div className="space-y-3">
          {rows.map((a) => (
            <Card key={a.id}>
              <CardContent className="space-y-3 p-4">
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div>
                    <div className="flex items-center gap-2 font-medium">
                      {a.activityName}
                      <Badge variant="outline">{spaced(a.type)}</Badge>
                      <StatusBadge status={a.status} />
                    </div>
                    <div className="text-xs text-muted-foreground">
                      {fmtDate(a.plannedStartDate)} → {fmtDate(a.plannedEndDate)}
                      {a.supervisorName && ` · supervised by ${a.supervisorName}`}
                      {a.externalProviderName && ` · ${a.externalProviderName}`}
                    </div>
                  </div>
                  <div className="text-right text-sm">
                    <div>{fmtMoney(a.estimatedCost, a.currencyCode)}</div>
                    {a.actualCost != null && (
                      <div className="text-xs text-muted-foreground">
                        actual {fmtMoney(a.actualCost, a.currencyCode)}
                      </div>
                    )}
                  </div>
                </div>

                {a.type === 'Mentoring' && (
                  // The boundary call made visible: this records that mentoring is part of the
                  // development plan; the programme itself lives in area 7.
                  <p className="text-xs text-muted-foreground">
                    Mentoring programmes, pairs and sessions are managed under{' '}
                    <Link href="/hr/training/mentoring" className="underline">
                      Training → Mentoring
                      <ExternalLink className="ml-0.5 inline h-3 w-3" />
                    </Link>
                    . This entry records it as part of the succession plan.
                  </p>
                )}

                {a.description && <p className="text-sm">{a.description}</p>}

                <div className="rounded-md border">
                  <div className="flex items-center justify-between border-b px-3 py-2">
                    <span className="flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                      <Flag className="h-3.5 w-3.5" />
                      Milestones
                    </span>
                    <Button variant="ghost" size="sm" onClick={() => setMilestoneFor(a.id)}>
                      <Plus className="mr-1 h-3 w-3" />
                      Add
                    </Button>
                  </div>
                  {a.milestones.length === 0 ? (
                    <p className="px-3 py-2 text-sm text-muted-foreground">None recorded.</p>
                  ) : (
                    <ul className="divide-y">
                      {a.milestones.map((m) => {
                        const overdue = !m.isCompleted && m.targetDate.slice(0, 10) < today;
                        return (
                          <li key={m.id} className="flex items-center justify-between px-3 py-2">
                            <div className="text-sm">
                              <span className={m.isCompleted ? 'line-through opacity-60' : ''}>
                                {m.milestoneName}
                              </span>
                              <span
                                className={`ml-2 text-xs ${overdue ? 'text-red-600 dark:text-red-400' : 'text-muted-foreground'}`}
                              >
                                <CalendarClock className="mr-1 inline h-3 w-3" />
                                {fmtDate(m.targetDate)}
                                {overdue && ' · overdue'}
                                {m.isCompleted && ` · done ${fmtDate(m.completedDate)}`}
                              </span>
                            </div>
                            {!m.isCompleted && (
                              <Button
                                variant="ghost"
                                size="sm"
                                disabled={completeMilestone.isPending}
                                onClick={() => completeMilestone.mutate(m.id)}
                              >
                                <Check className="mr-1 h-3.5 w-3.5" />
                                Complete
                              </Button>
                            )}
                          </li>
                        );
                      })}
                    </ul>
                  )}
                </div>

                {a.addressedGaps.length > 0 && (
                  <p className="text-xs text-muted-foreground">
                    Closes {a.addressedGaps.length} competency gap
                    {a.addressedGaps.length === 1 ? '' : 's'}:{' '}
                    {a.addressedGaps.map((g) => g.competencyName).join(', ')}
                  </p>
                )}
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {/* ── Add activity ─────────────────────────────────────────────────────── */}
      <Dialog open={creating} onOpenChange={setCreating}>
        <DialogContent className="max-h-[85vh] max-w-2xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Add a development activity</DialogTitle>
            <DialogDescription>
              What {candidate.employeeName} will be put through to close the gap to readiness.
            </DialogDescription>
          </DialogHeader>

          <form className="space-y-4" onSubmit={form.handleSubmit((v) => create.mutate(v))}>
            <FieldRow>
              <TextField form={form} name="activityName" label="Activity" required />
              <SelectField form={form} name="type" label="Type" required options={options(ACTIVITY_TYPES)} />
            </FieldRow>
            <FieldRow>
              <DateField form={form} name="plannedStartDate" label="Planned start" required />
              <DateField form={form} name="plannedEndDate" label="Planned end" />
            </FieldRow>
            <FieldRow>
              <SelectField form={form} name="status" label="Status" required options={options(ACTIVITY_STATUSES)} />
              <EmployeePickerField form={form} name="supervisorId" label="Supervisor" />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="estimatedCost" label="Estimated cost" />
              <SelectField
                form={form}
                name="currencyCode"
                label="Currency"
                // Finance owns this list. An unknown code is refused by the server.
                // ⚠ `currencyCode`/`currencyName`, NOT `code`/`name`. This read `c.code` behind an
                // `any` cast, so every option rendered "undefined — undefined" with an undefined
                // value and TypeScript never objected. Found 2026-09-01 while copying this block
                // for the guarantor surety, where a TYPED prop refused to compile.
                options={(currencies ?? []).map((c) => ({
                  value: c.code,
                  label: `${c.code} — ${c.name}`,
                }))}
                allowEmpty
                emptyLabel="No cost recorded"
              />
            </FieldRow>
            <TextField form={form} name="externalProviderName" label="External provider" />
            <TextareaField form={form} name="description" label="Description" />
            <TextareaField form={form} name="notes" label="Notes" />

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setCreating(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={create.isPending}>
                {create.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Add activity
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Add milestone ────────────────────────────────────────────────────── */}
      <Dialog open={!!milestoneFor} onOpenChange={(open) => !open && setMilestoneFor(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add a milestone</DialogTitle>
            <DialogDescription>
              A checkpoint on the way through this activity.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <input
              className="w-full rounded-md border px-3 py-2 text-sm"
              value={milestoneName}
              onChange={(e) => setMilestoneName(e.target.value)}
              placeholder="What has to be reached"
            />
            <input
              type="date"
              className="w-full rounded-md border px-3 py-2 text-sm"
              value={milestoneDate}
              onChange={(e) => setMilestoneDate(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setMilestoneFor(null)}>
              Cancel
            </Button>
            <Button
              disabled={
                addMilestone.isPending || milestoneName.trim() === '' || milestoneDate === ''
              }
              onClick={() => milestoneFor && addMilestone.mutate(milestoneFor)}
            >
              {addMilestone.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Add milestone
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
