'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, ListChecks, Loader2, Pencil, SkipForward } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { disciplineActionStepService } from '@/services/hr/discipline.service';
import {
  ACTION_STEP_EDITABLE_STATUSES,
  type DisciplineActionStep,
  type DisciplinaryActionStepStatus,
} from '@/types/hr/discipline';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const toInput = (v?: string | null) => (v ? v.slice(0, 10) : '');
const orNull = (v: string) => (v.trim().length > 0 ? v.trim() : null);

/** A step nobody can act on any more. */
const isSettled = (s: DisciplineActionStep) =>
  s.status === 'Completed' || s.status === 'Skipped' || s.status === 'Cancelled';

/**
 * The offence's procedure, instantiated against this case.
 *
 * This panel exists because the read-only version had an empty state — "the procedure has not been
 * initialised for this case" — describing an action with **no button behind it anywhere**. The
 * initialise endpoint had never been called by any client.
 *
 * ⚠ **Complete and skip are transitions, not status values, and the difference is the audit
 * trail.** Both stamp `actionedById` from the caller's token, and complete also stamps
 * `completedDate` — skip deliberately does not, because a skipped step was not completed and the
 * entity has no separate skipped-on column. Setting `status: 'Completed'` through the plain update
 * writes the word and stamps nothing, so the step reads as done while the record says nobody did
 * it. That is why the edit dialog offers only Pending / In progress / Cancelled and the other two
 * are buttons.
 *
 * ⚠ Building this panel is what surfaced the fact that neither transition stamped the actor at all
 * — both set only the `UpdatedBy` string audit column, leaving the "By" column here permanently
 * blank. Fixed in `StaffDisciplineActionStepService`; see ledger D-07.
 *
 * ⚠ **A skip is a departure from the disciplinary procedure**, which is exactly the sort of thing a
 * case turns on later. The reason is required by the screen even though the API would accept an
 * empty string.
 */
export function ActionStepsPanel({
  caseId,
  canWrite,
  onChanged,
}: {
  caseId: string;
  canWrite: boolean;
  onChanged?: () => void;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const queryKey = ['hr', 'discipline', caseId, 'action-steps'];
  const { data: steps, isLoading } = useQuery({
    queryKey,
    queryFn: () => disciplineActionStepService.getForCase(caseId),
    enabled: !!caseId,
  });

  const [editing, setEditing] = useState<DisciplineActionStep | null>(null);
  const [form, setForm] = useState({
    dueDate: '',
    startedDate: '',
    status: 'Pending' as DisciplinaryActionStepStatus,
    notes: '',
  });
  const [transition, setTransition] = useState<{ step: DisciplineActionStep; kind: 'complete' | 'skip' } | null>(null);
  const [transitionText, setTransitionText] = useState('');

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey });
    onChanged?.();
  };

  const fail = (verb: string) => (e: any) =>
    toast({
      title: `Could not ${verb} the step`,
      description: e?.response?.data?.detail ?? e?.message ?? 'Please try again.',
      variant: 'destructive',
    });

  const initialise = useMutation({
    mutationFn: () => disciplineActionStepService.initialise(caseId),
    onSuccess: async (created) => {
      await refresh();
      toast({
        title: 'Procedure initialised',
        description: `${created?.length ?? 0} step${created?.length === 1 ? '' : 's'} created from the offence's procedure.`,
      });
    },
    onError: fail('initialise'),
  });

  const save = useMutation({
    mutationFn: (step: DisciplineActionStep) =>
      disciplineActionStepService.update(step.id, {
        dueDate: orNull(form.dueDate),
        startedDate: orNull(form.startedDate),
        // Carried through untouched — only the transitions may set these two, because only they
        // stamp the actor alongside the date.
        completedDate: step.completedDate ?? null,
        status: form.status,
        actionedById: step.actionedById ?? null,
        notes: orNull(form.notes),
      }),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Step updated' });
      setEditing(null);
    },
    onError: fail('update'),
  });

  const runTransition = useMutation({
    mutationFn: ({ step, kind, text }: { step: DisciplineActionStep; kind: 'complete' | 'skip'; text: string }) =>
      kind === 'complete'
        ? disciplineActionStepService.complete(step.id, text)
        : disciplineActionStepService.skip(step.id, text),
    onSuccess: async (_result, variables) => {
      await refresh();
      toast({ title: variables.kind === 'complete' ? 'Step completed' : 'Step skipped' });
      setTransition(null);
      setTransitionText('');
    },
    onError: (e: any, variables) => fail(variables.kind === 'complete' ? 'complete' : 'skip')(e),
  });

  const openEdit = (s: DisciplineActionStep) => {
    setEditing(s);
    setForm({
      dueDate: toInput(s.dueDate),
      startedDate: toInput(s.startedDate),
      // A settled step's status is not in the editable list; fall back so the Select is not blank.
      status: ACTION_STEP_EDITABLE_STATUSES.some((o) => o.value === s.status) ? s.status : 'Pending',
      notes: s.notes ?? '',
    });
  };

  const rows = steps ?? [];
  const skipNeedsReason = transition?.kind === 'skip' && transitionText.trim().length === 0;

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0">
        <CardTitle>Procedure steps</CardTitle>
        {canWrite && rows.length === 0 && !isLoading && (
          <Button size="sm" onClick={() => initialise.mutate()} disabled={initialise.isPending}>
            {initialise.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <ListChecks className="mr-2 h-4 w-4" />
            )}
            Initialise procedure
          </Button>
        )}
      </CardHeader>
      <CardContent className="p-0">
        {isLoading ? (
          <div className="flex justify-center py-10">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : rows.length === 0 ? (
          <div className="px-6 pb-6">
            <EmptyState
              title="No steps"
              description={
                canWrite
                  ? "The offence's procedure has not been copied onto this case yet. Initialising it creates one step per procedure stage, with a due date from each stage's expected duration."
                  : "The offence's procedure has not been initialised for this case."
              }
            />
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-[52px]">#</TableHead>
                <TableHead>Step</TableHead>
                <TableHead>Due</TableHead>
                <TableHead>Completed</TableHead>
                <TableHead>By</TableHead>
                <TableHead>Status</TableHead>
                {canWrite && <TableHead className="text-right">Actions</TableHead>}
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows
                .slice()
                .sort((a, b) => a.sequence - b.sequence)
                .map((s) => (
                  <TableRow key={s.id}>
                    <TableCell className="text-muted-foreground">{s.sequence}</TableCell>
                    <TableCell>
                      <div>{s.stepName ?? '—'}</div>
                      {s.stepDescription && (
                        <div className="text-xs text-muted-foreground">{s.stepDescription}</div>
                      )}
                      {s.notes && <div className="mt-1 text-xs italic text-muted-foreground">{s.notes}</div>}
                    </TableCell>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        {fmtDate(s.dueDate)}
                        {s.isOverdue && <Badge variant="destructive">Overdue</Badge>}
                      </div>
                    </TableCell>
                    <TableCell>{fmtDate(s.completedDate)}</TableCell>
                    <TableCell>{s.actionedByName ?? '—'}</TableCell>
                    <TableCell><StatusBadge status={s.statusName} /></TableCell>
                    {canWrite && (
                      <TableCell className="text-right">
                        <div className="flex justify-end gap-1">
                          <Button variant="ghost" size="sm" onClick={() => openEdit(s)}>
                            <Pencil className="h-4 w-4" />
                            <span className="sr-only">Edit</span>
                          </Button>
                          {!isSettled(s) && (
                            <>
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => { setTransition({ step: s, kind: 'complete' }); setTransitionText(''); }}
                              >
                                <CheckCircle2 className="mr-1 h-4 w-4" />
                                Complete
                              </Button>
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => { setTransition({ step: s, kind: 'skip' }); setTransitionText(''); }}
                              >
                                <SkipForward className="mr-1 h-4 w-4" />
                                Skip
                              </Button>
                            </>
                          )}
                        </div>
                      </TableCell>
                    )}
                  </TableRow>
                ))}
            </TableBody>
          </Table>
        )}
      </CardContent>

      {/* Edit — dates, status and notes only. Completion is a transition, not a field. */}
      <Dialog open={editing !== null} onOpenChange={(o) => !o && setEditing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editing?.stepName ?? 'Step'}</DialogTitle>
            <DialogDescription>
              Adjust the schedule or add a note. Use Complete or Skip to close the step — those
              record who acted and when.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="step-due">Due</Label>
                <Input
                  id="step-due"
                  type="date"
                  value={form.dueDate}
                  onChange={(e) => setForm({ ...form, dueDate: e.target.value })}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="step-started">Started</Label>
                <Input
                  id="step-started"
                  type="date"
                  value={form.startedDate}
                  onChange={(e) => setForm({ ...form, startedDate: e.target.value })}
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="step-status">Status</Label>
              <Select
                value={form.status}
                onValueChange={(v) => setForm({ ...form, status: v as DisciplinaryActionStepStatus })}
              >
                <SelectTrigger id="step-status"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {ACTION_STEP_EDITABLE_STATUSES.map((o) => (
                    <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="step-notes">Notes</Label>
              <Textarea
                id="step-notes"
                rows={3}
                value={form.notes}
                onChange={(e) => setForm({ ...form, notes: e.target.value })}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditing(null)} disabled={save.isPending}>Cancel</Button>
            <Button
              onClick={() => editing && save.mutate(editing)}
              disabled={save.isPending || !editing}
            >
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save changes
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Complete / skip — both take a bare string body and stamp the actor from the token. */}
      <Dialog open={transition !== null} onOpenChange={(o) => !o && setTransition(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {transition?.kind === 'complete' ? 'Complete this step' : 'Skip this step'}
            </DialogTitle>
            <DialogDescription>
              {transition?.kind === 'complete'
                ? 'This records you as the person who completed it, with today’s date.'
                : 'Skipping departs from the offence’s procedure. The reason is kept on the case and may be read back later.'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="transition-text">
              {transition?.kind === 'complete' ? 'Notes' : 'Reason'}
              {transition?.kind === 'skip' && <span className="ml-0.5 text-red-500">*</span>}
            </Label>
            <Textarea
              id="transition-text"
              rows={3}
              value={transitionText}
              onChange={(e) => setTransitionText(e.target.value)}
              placeholder={
                transition?.kind === 'complete'
                  ? 'Optional — what was done.'
                  : 'e.g. The employee waived the right to a hearing in writing.'
              }
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setTransition(null)} disabled={runTransition.isPending}>
              Cancel
            </Button>
            <Button
              onClick={() =>
                transition &&
                runTransition.mutate({ ...transition, text: transitionText.trim() })
              }
              disabled={runTransition.isPending || skipNeedsReason || !transition}
            >
              {runTransition.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {transition?.kind === 'complete' ? 'Complete step' : 'Skip step'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
