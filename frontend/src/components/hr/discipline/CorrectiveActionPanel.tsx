'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { CheckCircle2, ClipboardList, Loader2, Lock, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import {
  DateField, FieldRow, SelectField, TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { useToast } from '@/hooks/use-toast';
import { disciplineCorrectiveActionService } from '@/services/hr/discipline.service';
import {
  CORRECTIVE_ACTION_STATUS_OPTIONS,
  LOCKED_CORRECTIVE_ACTION_STATUSES,
  type DisciplineCorrectiveAction,
  type DisciplineCorrectiveActionItem,
  type DisciplineCorrectiveActionStatus,
} from '@/types/hr/discipline';

const toInput = (v?: string | null) => (v ? v.slice(0, 10) : '');
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const orNull = (v?: string | null) => {
  const t = (v ?? '').trim();
  return t.length > 0 ? t : null;
};

const planSchema = z
  .object({
    supervisorId: z.string().min(1, 'Name the supervising manager'),
    objective: z.string().trim().min(1, 'State what the plan is for').max(1000),
    startDate: z.string().min(1, 'Start date is required'),
    reviewDate: z.string().min(1, 'Review date is required'),
    status: z.string().min(1),
    completedDate: z.string().optional(),
    notes: z.string().max(1000).optional(),
  })
  .refine((v) => v.reviewDate >= v.startDate, {
    message: 'The review cannot fall before the plan starts',
    path: ['reviewDate'],
  });

type PlanForm = z.infer<typeof planSchema>;

const itemSchema = z.object({
  description: z.string().trim().min(1, 'Describe what has to be done').max(1000),
  targetDate: z.string().min(1, 'Target date is required'),
});

type ItemForm = z.infer<typeof itemSchema>;

/**
 * The corrective action plan on a case, and the items that make it up.
 *
 * ⚠ **One plan per case.** `cases/{id}/corrective-action` is singular and returns the record or
 * null, so this is a form plus a child list rather than a collection — which is why it does not
 * use `ResourceCollectionTab` at the top level, only for the items.
 *
 * ⚠ **The lock is real and lives on the server.** `CorrectiveActionService.UpdateAsync` throws
 * "A completed or cancelled corrective action plan cannot be edited." Unusually for this codebase
 * the rule is not merely a screen convention, so the screen mirrors it rather than inventing it —
 * the form is disabled with the reason shown, instead of letting someone type into a box whose
 * save will be refused.
 *
 * ⚠ **`employeeId` is not derived from the case.** `CreateAsync` copies it from the request body,
 * so this panel passes the case's own subject explicitly. Nothing on the server checks the two
 * agree, which is why it is passed and not left to a default.
 */
export function CorrectiveActionPanel({
  caseId,
  employeeId,
  employeeName,
  canWrite,
  canDelete,
  onChanged,
}: {
  caseId: string;
  employeeId: string;
  employeeName?: string | null;
  canWrite: boolean;
  canDelete: boolean;
  onChanged?: () => void;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const planKey = ['hr', 'discipline', caseId, 'corrective-action'];
  const { data: plan, isLoading } = useQuery({
    queryKey: planKey,
    queryFn: () => disciplineCorrectiveActionService.getForCase(caseId),
    enabled: !!caseId,
  });

  const [creating, setCreating] = useState(false);
  const [completing, setCompleting] = useState<DisciplineCorrectiveActionItem | null>(null);
  const [completionNotes, setCompletionNotes] = useState('');
  const [confirmComplete, setConfirmComplete] = useState(false);

  const locked = !!plan && LOCKED_CORRECTIVE_ACTION_STATUSES.includes(plan.status);
  const editable = canWrite && !locked;

  const form = useForm<PlanForm>({
    resolver: zodResolver(planSchema) as any,
    defaultValues: {
      supervisorId: '',
      objective: '',
      startDate: new Date().toISOString().slice(0, 10),
      reviewDate: '',
      status: 'Pending',
      completedDate: '',
      notes: '',
    },
  });

  useEffect(() => {
    if (!plan) return;
    form.reset({
      supervisorId: plan.supervisorId,
      objective: plan.objective,
      startDate: toInput(plan.startDate),
      reviewDate: toInput(plan.reviewDate),
      status: plan.status,
      completedDate: toInput(plan.completedDate),
      notes: plan.notes ?? '',
    });
  }, [plan]);

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: planKey });
    onChanged?.();
  };

  const fail = (verb: string) => (e: any) =>
    toast({
      title: `Could not ${verb} the plan`,
      description: e?.response?.data?.detail ?? e?.message ?? 'Please try again.',
      variant: 'destructive',
    });

  const create = useMutation({
    mutationFn: (v: PlanForm) =>
      disciplineCorrectiveActionService.create(caseId, {
        // ⚠ The case's subject, passed explicitly — the server takes this verbatim.
        employeeId,
        supervisorId: v.supervisorId,
        objective: v.objective.trim(),
        startDate: v.startDate,
        reviewDate: v.reviewDate,
        notes: orNull(v.notes),
      }),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Corrective action plan created' });
      setCreating(false);
    },
    onError: fail('create'),
  });

  const save = useMutation({
    mutationFn: ({ planId, v }: { planId: string; v: PlanForm }) =>
      disciplineCorrectiveActionService.update(planId, {
        supervisorId: v.supervisorId,
        objective: v.objective.trim(),
        startDate: v.startDate,
        reviewDate: v.reviewDate,
        completedDate: orNull(v.completedDate),
        status: v.status as DisciplineCorrectiveActionStatus,
        notes: orNull(v.notes),
      }),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Plan updated' });
    },
    onError: fail('update'),
  });

  const completePlan = useMutation({
    mutationFn: (planId: string) => disciplineCorrectiveActionService.complete(planId),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Plan completed' });
      setConfirmComplete(false);
    },
    onError: fail('complete'),
  });

  const completeItem = useMutation({
    mutationFn: ({ itemId, notes }: { itemId: string; notes: string }) =>
      disciplineCorrectiveActionService.completeItem(itemId, notes),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [...planKey, 'items'] });
      await refresh();
      toast({ title: 'Item completed' });
      setCompleting(null);
      setCompletionNotes('');
    },
    onError: fail('complete the item on'),
  });

  if (isLoading) {
    return (
      <Card>
        <CardHeader><CardTitle>Corrective action plan</CardTitle></CardHeader>
        <CardContent className="flex justify-center py-10">
          <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
        </CardContent>
      </Card>
    );
  }

  // ── no plan yet ────────────────────────────────────────────────────────────
  if (!plan && !creating) {
    return (
      <Card>
        <CardHeader><CardTitle>Corrective action plan</CardTitle></CardHeader>
        <CardContent>
          <EmptyState
            icon={ClipboardList}
            title="No plan"
            description={
              canWrite
                ? `No corrective action plan has been agreed for ${employeeName ?? 'this employee'} on this case.`
                : 'No corrective action plan has been agreed on this case.'
            }
            action={
              canWrite ? (
                <Button size="sm" onClick={() => setCreating(true)}>
                  <ClipboardList className="mr-2 h-4 w-4" />
                  Create a plan
                </Button>
              ) : undefined
            }
          />
        </CardContent>
      </Card>
    );
  }

  const onSubmit = form.handleSubmit((v) => (plan ? save.mutate({ planId: plan.id, v }) : create.mutate(v)));
  const busy = create.isPending || save.isPending;

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="flex flex-row items-start justify-between space-y-0">
          <div>
            <CardTitle>Corrective action plan</CardTitle>
            {plan && (
              <div className="flex flex-wrap items-center gap-2 pt-2">
                <StatusBadge status={plan.statusName ?? plan.status} />
                {plan.isOverdue && <Badge variant="destructive">Overdue</Badge>}
                <span className="text-xs text-muted-foreground">
                  {plan.completedItemCount} of {plan.itemCount} items done
                </span>
              </div>
            )}
          </div>
          {plan && canWrite && !locked && (
            <Button variant="outline" size="sm" onClick={() => setConfirmComplete(true)}>
              <CheckCircle2 className="mr-2 h-4 w-4" />
              Complete plan
            </Button>
          )}
        </CardHeader>
        <CardContent className="space-y-4">
          {locked && (
            <div className="flex items-start gap-3 rounded-md border bg-muted/40 p-3 text-sm text-muted-foreground">
              <Lock className="mt-0.5 h-4 w-4 shrink-0" />
              <p>
                This plan is {(plan?.statusName ?? plan?.status ?? '').toLowerCase()} and can no
                longer be edited. The API refuses the change too — this is not only a screen rule.
              </p>
            </div>
          )}

          <form onSubmit={onSubmit} className="space-y-4">
            <FieldRow>
              <div className="space-y-2">
                <Label>Supervising manager<span className="ml-0.5 text-red-500">*</span></Label>
                <EmployeePicker
                  value={form.watch('supervisorId')}
                  onChange={(v) => form.setValue('supervisorId', v ?? '', { shouldValidate: true })}
                  initialLabel={plan?.supervisorName ?? null}
                  disabled={!editable}
                />
                {form.formState.errors.supervisorId && (
                  <p className="text-sm text-red-500">{form.formState.errors.supervisorId.message}</p>
                )}
              </div>
              {plan ? (
                <SelectField
                  form={form}
                  name="status"
                  label="Status"
                  options={CORRECTIVE_ACTION_STATUS_OPTIONS}
                />
              ) : (
                <div className="space-y-2">
                  <Label>Employee</Label>
                  <p className="pt-2 text-sm">{employeeName ?? 'The case subject'}</p>
                  <p className="text-xs text-muted-foreground">Taken from the case.</p>
                </div>
              )}
            </FieldRow>

            <TextareaField
              form={form}
              name="objective"
              label="Objective"
              rows={3}
              placeholder="What has to change, and what good looks like at the review."
            />

            <FieldRow>
              <DateField form={form} name="startDate" label="Starts" required />
              <DateField form={form} name="reviewDate" label="Review on" required />
            </FieldRow>

            {plan && <DateField form={form} name="completedDate" label="Completed" />}

            <TextareaField form={form} name="notes" label="Notes" rows={2} />

            {editable && (
              <div className="flex justify-end gap-2">
                {!plan && (
                  <Button type="button" variant="outline" onClick={() => setCreating(false)} disabled={busy}>
                    Cancel
                  </Button>
                )}
                <Button type="submit" disabled={busy}>
                  {busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                  {plan ? 'Save changes' : 'Create plan'}
                </Button>
              </div>
            )}
          </form>
        </CardContent>
      </Card>

      {plan && (
        <Card>
          <CardHeader><CardTitle className="text-base">What the employee has to do</CardTitle></CardHeader>
          <CardContent>
            <ResourceCollectionTab<DisciplineCorrectiveActionItem, ItemForm>
              parentId={plan.id}
              title="items"
              singular="item"
              queryKey={[...planKey, 'items']}
              invalidateKeys={[planKey]}
              readOnly={!editable}
              // The item update DTO can set status and completion, but doing so bypasses the
              // transition that stamps the actor — so completion is the action, not a field.
              dialogHint="One concrete thing to be done, with a date to do it by."
              emptyDescription="A plan with no items is an objective nobody can be measured against."
              // ⚠ A real fetch, not `plan.items`. Reading the parent's copy through a closure
              // means the table renders whatever `plan` held when the closure was made, and the
              // invalidation that refreshes the parent races the one that refreshes this list.
              list={async (planId) =>
                (await disciplineCorrectiveActionService.getById(planId))?.items ?? []
              }
              create={(planId, v) =>
                disciplineCorrectiveActionService.addItem(planId, {
                  description: v.description.trim(),
                  targetDate: v.targetDate,
                })
              }
              update={async (planId, itemId, v) => {
                // Re-read rather than trusting the closure: the update DTO requires status and
                // completion, and sending a stale pair would quietly reopen a completed item.
                const fresh = await disciplineCorrectiveActionService.getById(planId);
                const existing = (fresh?.items ?? []).find((i) => i.id === itemId);
                return disciplineCorrectiveActionService.updateItem(itemId, {
                  description: v.description.trim(),
                  targetDate: v.targetDate,
                  // Preserved, not edited here — completion runs through its own action.
                  completedDate: existing?.completedDate ?? null,
                  status: existing?.status ?? 'Pending',
                  completionNotes: existing?.completionNotes ?? null,
                });
              }}
              remove={
                canDelete
                  ? (_planId, itemId) => disciplineCorrectiveActionService.removeItem(itemId)
                  : undefined
              }
              actions={[
                {
                  label: 'Mark done',
                  visible: (i) => i.status !== 'Completed' && i.status !== 'Cancelled',
                  run: async (i) => { setCompleting(i); setCompletionNotes(''); },
                },
              ]}
              getId={(i) => i.id}
              columns={[
                { header: 'What', cell: (i) => i.description },
                {
                  header: 'By',
                  cell: (i) => (
                    <div className="flex items-center gap-2">
                      {fmtDate(i.targetDate)}
                      {i.isOverdue && <Badge variant="destructive">Overdue</Badge>}
                    </div>
                  ),
                },
                { header: 'Done', cell: (i) => fmtDate(i.completedDate) },
                { header: 'Status', cell: (i) => <StatusBadge status={i.statusName ?? i.status} /> },
                {
                  header: 'Notes',
                  cell: (i) => <span className="text-muted-foreground">{i.completionNotes || '—'}</span>,
                },
              ]}
              schema={itemSchema}
              emptyForm={{ description: '', targetDate: '' }}
              toForm={(i) => ({ description: i.description, targetDate: toInput(i.targetDate) })}
              renderFields={(itemForm) => (
                <>
                  <TextareaField
                    form={itemForm}
                    name="description"
                    label="What has to be done"
                    rows={3}
                    placeholder="e.g. Complete the refresher course on cash handling."
                  />
                  <DateField form={itemForm} name="targetDate" label="Target date" required />
                </>
              )}
            />
          </CardContent>
        </Card>
      )}

      {/* Completing an item takes a bare string body and stamps the date and actor. */}
      <Dialog open={completing !== null} onOpenChange={(o) => !o && setCompleting(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Mark this item done</DialogTitle>
            <DialogDescription>{completing?.description}</DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="item-notes">Completion notes</Label>
            <Textarea
              id="item-notes"
              rows={3}
              value={completionNotes}
              onChange={(e) => setCompletionNotes(e.target.value)}
              placeholder="Optional — evidence, or how it was verified."
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCompleting(null)} disabled={completeItem.isPending}>
              Cancel
            </Button>
            <Button
              onClick={() =>
                completing &&
                completeItem.mutate({ itemId: completing.id, notes: completionNotes.trim() })
              }
              disabled={completeItem.isPending || !completing}
            >
              {completeItem.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Mark done
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={confirmComplete}
        onOpenChange={setConfirmComplete}
        title="Complete this plan?"
        description="A completed plan can no longer be edited — the API enforces that, not just this screen. Finish any outstanding items first."
        confirmText="Complete plan"
        isLoading={completePlan.isPending}
        onConfirm={async () => { if (plan) await completePlan.mutateAsync(plan.id); }}
      />
    </div>
  );
}
