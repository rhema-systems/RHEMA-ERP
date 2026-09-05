'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Loader2, Pencil, Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { successionService } from '@/services/hr/succession.service';
import type {
  ActionPriority,
  ActionStatus,
  ActionType,
  SuccessionAction,
  SuccessionCandidateSummary,
} from '@/types/hr/succession';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
/** `<input type="date">` wants yyyy-mm-dd; the API returns a full ISO timestamp. */
const toDateInput = (v?: string | null) => (v ? v.slice(0, 10) : '');
const fromDateInput = (v: string) => (v ? new Date(`${v}T00:00:00`).toISOString() : null);

const TYPES: { value: ActionType; label: string }[] = [
  { value: 'Development', label: 'Development' },
  { value: 'Recruitment', label: 'Recruitment' },
  { value: 'Retention', label: 'Retention' },
  { value: 'Assessment', label: 'Assessment' },
  { value: 'Other', label: 'Other' },
];

const PRIORITIES: { value: ActionPriority; label: string }[] = [
  { value: 'Critical', label: 'Critical' },
  { value: 'High', label: 'High' },
  { value: 'Medium', label: 'Medium' },
  { value: 'Low', label: 'Low' },
];

const STATUSES: { value: ActionStatus; label: string }[] = [
  { value: 'NotStarted', label: 'Not started' },
  { value: 'InProgress', label: 'In progress' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Overdue', label: 'Overdue' },
  { value: 'Cancelled', label: 'Cancelled' },
];

const PRIORITY_TONE: Record<ActionPriority, string> = {
  Critical: 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200',
  High: 'bg-orange-100 text-orange-800 dark:bg-orange-900/40 dark:text-orange-200',
  Medium: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200',
  Low: 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200',
};

interface FormState {
  candidateId: string;
  actionDescription: string;
  type: ActionType;
  priority: ActionPriority;
  responsiblePersonId: string;
  responsiblePersonName: string | null;
  dueDate: string;
  startedDate: string;
  status: ActionStatus;
  completionDate: string;
  completionNotes: string;
  dependsOnActionId: string;
  wasSuccessful: boolean;
  outcomeNotes: string;
}

const BLANK: FormState = {
  candidateId: '',
  actionDescription: '',
  type: 'Development',
  priority: 'Medium',
  responsiblePersonId: '',
  responsiblePersonName: null,
  dueDate: '',
  startedDate: '',
  status: 'NotStarted',
  completionDate: '',
  completionNotes: '',
  dependsOnActionId: '',
  wasSuccessful: false,
  outcomeNotes: '',
};

/** The sentinel a Radix `Select` needs, because an empty string is not a valid item value. */
const NONE = '__none__';

/**
 * The work the plan commits to — recruit, develop, retain, assess.
 *
 * ⚠ **This panel reads `GET succession-plans/{id}/actions`, not the plan's nested `actions`.**
 * The nested list is `SuccessionActionSummary` — seven fields — and the update payload has
 * thirteen. Editing from the summary would have wiped the candidate, the assigner, both dates,
 * both note fields, the dependency and the success flag on every save. The dedicated read was
 * itself a summary until this slice; it now returns the record.
 *
 * ⚠ **Nothing here sets the assigner.** Who raised an action is a fact about the signed-in user,
 * so the server stamps it from the token and neither payload carries the field. It is displayed,
 * never edited — and an edit deliberately leaves it alone, so the original assigner survives.
 *
 * ⚠ **`dependsOnActionId` is checked for cycles server-side** and answers 400, so the picker
 * offers the plan's other actions rather than trying to compute reachability here.
 */
export function PlanActionsPanel({
  planId,
  candidates,
  canWrite,
  canDelete,
}: {
  planId: string;
  candidates: SuccessionCandidateSummary[];
  canWrite: boolean;
  canDelete: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const queryKey = ['succession-plans', planId, 'actions'];
  const { data: actions, isLoading } = useQuery({
    queryKey,
    queryFn: () => successionService.getActions(planId),
    enabled: !!planId,
  });

  const [adding, setAdding] = useState(false);
  const [editing, setEditing] = useState<SuccessionAction | null>(null);
  const [pendingDelete, setPendingDelete] = useState<SuccessionAction | null>(null);
  const [form, setForm] = useState<FormState>(BLANK);

  const rows = actions ?? [];
  const set = <K extends keyof FormState>(key: K, value: FormState[K]) =>
    setForm((f) => ({ ...f, [key]: value }));

  const fail = (title: string) => (e: any) =>
    toast({
      title,
      description:
        e?.response?.data?.message ?? e?.response?.data ?? e?.message ?? 'Please try again.',
      variant: 'destructive',
    });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey });
    // The plan detail nests the summary list and drives the tab count.
    await queryClient.invalidateQueries({ queryKey: ['succession-plans', planId] });
  };

  const add = useMutation({
    mutationFn: () =>
      successionService.addAction(planId, {
        candidateId: form.candidateId || null,
        actionDescription: form.actionDescription.trim(),
        type: form.type,
        priority: form.priority,
        responsiblePersonId: form.responsiblePersonId || null,
        dueDate: fromDateInput(form.dueDate),
        dependsOnActionId: form.dependsOnActionId || null,
      }),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Action added' });
      setAdding(false);
    },
    onError: fail('Could not add the action'),
  });

  const update = useMutation({
    mutationFn: ({ id }: { id: string }) =>
      successionService.updateAction(id, {
        id,
        actionDescription: form.actionDescription.trim(),
        type: form.type,
        priority: form.priority,
        responsiblePersonId: form.responsiblePersonId || null,
        dueDate: fromDateInput(form.dueDate),
        startedDate: fromDateInput(form.startedDate),
        status: form.status,
        completionDate: fromDateInput(form.completionDate),
        completionNotes: form.completionNotes.trim() || null,
        dependsOnActionId: form.dependsOnActionId || null,
        wasSuccessful: form.wasSuccessful,
        outcomeNotes: form.outcomeNotes.trim() || null,
      }),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Action updated' });
      setEditing(null);
    },
    onError: fail('Could not update the action'),
  });

  const remove = useMutation({
    mutationFn: (row: SuccessionAction) => successionService.removeAction(row.id),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Action removed' });
      setPendingDelete(null);
    },
    onError: fail('Could not remove the action'),
  });

  const openAdd = () => {
    setForm(BLANK);
    setAdding(true);
  };

  const openEdit = (row: SuccessionAction) => {
    setForm({
      candidateId: row.candidateId ?? '',
      actionDescription: row.actionDescription,
      type: row.type,
      priority: row.priority,
      responsiblePersonId: row.responsiblePersonId ?? '',
      responsiblePersonName: row.responsiblePersonName ?? null,
      dueDate: toDateInput(row.dueDate),
      startedDate: toDateInput(row.startedDate),
      status: row.status,
      completionDate: toDateInput(row.completionDate),
      completionNotes: row.completionNotes ?? '',
      dependsOnActionId: row.dependsOnActionId ?? '',
      wasSuccessful: row.wasSuccessful,
      outcomeNotes: row.outcomeNotes ?? '',
    });
    setEditing(row);
  };

  const descriptionValid = form.actionDescription.trim().length > 0;
  /** An action cannot depend on itself; everything else is the server's cycle check. */
  const dependencyOptions = rows.filter((a) => a.id !== editing?.id);

  const commonFields = (
    <>
      <div className="space-y-2">
        <Label htmlFor="ac-description">
          What has to happen<span className="ml-0.5 text-red-500">*</span>
        </Label>
        <Textarea
          id="ac-description"
          rows={3}
          maxLength={2000}
          value={form.actionDescription}
          onChange={(e) => set('actionDescription', e.target.value)}
          placeholder="Enrol in the leadership programme; run an external search; agree a retention package…"
        />
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor="ac-type">Type</Label>
          <Select value={form.type} onValueChange={(v) => set('type', v as ActionType)}>
            <SelectTrigger id="ac-type"><SelectValue /></SelectTrigger>
            <SelectContent>
              {TYPES.map((o) => (
                <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-2">
          <Label htmlFor="ac-priority">Priority</Label>
          <Select
            value={form.priority}
            onValueChange={(v) => set('priority', v as ActionPriority)}
          >
            <SelectTrigger id="ac-priority"><SelectValue /></SelectTrigger>
            <SelectContent>
              {PRIORITIES.map((o) => (
                <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      <div className="space-y-2">
        <Label>Owner</Label>
        <EmployeePicker
          value={form.responsiblePersonId || null}
          initialLabel={form.responsiblePersonName}
          placeholder="Who is accountable for this?"
          onChange={(employeeId, label) =>
            setForm((f) => ({
              ...f,
              responsiblePersonId: employeeId ?? '',
              responsiblePersonName: label,
            }))
          }
        />
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor="ac-due">Due</Label>
          <Input
            id="ac-due"
            type="date"
            value={form.dueDate}
            onChange={(e) => set('dueDate', e.target.value)}
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor="ac-depends">Depends on</Label>
          <Select
            value={form.dependsOnActionId || NONE}
            onValueChange={(v) => set('dependsOnActionId', v === NONE ? '' : v)}
          >
            <SelectTrigger id="ac-depends">
              <SelectValue placeholder="Nothing" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={NONE}>Nothing</SelectItem>
              {dependencyOptions.map((a) => (
                <SelectItem key={a.id} value={a.id}>
                  {a.actionDescription.slice(0, 60)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <p className="text-xs text-muted-foreground">
            A prerequisite. Circular chains are refused by the server.
          </p>
        </div>
      </div>
    </>
  );

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0">
        <CardTitle className="text-base">Actions</CardTitle>
        {canWrite && (
          <Button size="sm" onClick={openAdd}>
            <Plus className="mr-2 h-4 w-4" />
            Add an action
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
              icon={CheckCircle2}
              title="No actions recorded"
              description="Actions are the work the plan commits to — recruit, develop, retain, assess. A plan with none is an intention with no follow-through."
            />
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Action</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Priority</TableHead>
                <TableHead>Owner</TableHead>
                <TableHead>Due</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="w-24 text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((a) => (
                <TableRow key={a.id}>
                  <TableCell className="max-w-md">
                    <div>{a.actionDescription}</div>
                    <div className="mt-0.5 space-x-2 text-xs text-muted-foreground">
                      {a.candidateEmployeeName && <span>For {a.candidateEmployeeName}</span>}
                      {a.dependsOnActionDescription && (
                        <span>After “{a.dependsOnActionDescription.slice(0, 40)}”</span>
                      )}
                      {a.assignedByName && <span>Assigned by {a.assignedByName}</span>}
                    </div>
                  </TableCell>
                  <TableCell>{a.typeName ?? a.type}</TableCell>
                  <TableCell>
                    <Badge className={PRIORITY_TONE[a.priority]}>{a.priority}</Badge>
                  </TableCell>
                  <TableCell>{a.responsiblePersonName ?? '—'}</TableCell>
                  <TableCell>{fmtDate(a.dueDate)}</TableCell>
                  <TableCell>
                    <StatusBadge status={a.status} />
                  </TableCell>
                  <TableCell className="text-right">
                    <div className="flex justify-end gap-1">
                      {canWrite && (
                        <Button variant="ghost" size="sm" onClick={() => openEdit(a)}>
                          <Pencil className="h-4 w-4" />
                          <span className="sr-only">Edit</span>
                        </Button>
                      )}
                      {canDelete && (
                        <Button
                          variant="ghost"
                          size="sm"
                          className="text-red-600"
                          onClick={() => setPendingDelete(a)}
                        >
                          <Trash2 className="h-4 w-4" />
                          <span className="sr-only">Remove</span>
                        </Button>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>

      <Dialog open={adding} onOpenChange={setAdding}>
        <DialogContent className="max-h-[85vh] max-w-2xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Add an action</DialogTitle>
            <DialogDescription>
              A new action starts at “Not started” — its progress is recorded by editing it.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="ac-candidate">About a candidate</Label>
              <Select
                value={form.candidateId || NONE}
                onValueChange={(v) => set('candidateId', v === NONE ? '' : v)}
              >
                <SelectTrigger id="ac-candidate">
                  <SelectValue placeholder="The plan as a whole" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>The plan as a whole</SelectItem>
                  {candidates.map((c) => (
                    <SelectItem key={c.id} value={c.id}>
                      {c.employeeName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                Fixed once the action exists — an action about a different person is a different
                action.
              </p>
            </div>
            {commonFields}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setAdding(false)} disabled={add.isPending}>
              Cancel
            </Button>
            <Button onClick={() => add.mutate()} disabled={!descriptionValid || add.isPending}>
              {add.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Add
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={editing !== null} onOpenChange={(o) => !o && setEditing(null)}>
        <DialogContent className="max-h-[85vh] max-w-2xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit action</DialogTitle>
            <DialogDescription>
              {editing?.assignedByName
                ? `Raised by ${editing.assignedByName}. That does not change when the action is edited.`
                : 'The assigner is recorded from whoever raised the action and is not editable.'}
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            {commonFields}

            <div className="grid gap-4 sm:grid-cols-3">
              <div className="space-y-2">
                <Label htmlFor="ac-status">Status</Label>
                <Select
                  value={form.status}
                  onValueChange={(v) => set('status', v as ActionStatus)}
                >
                  <SelectTrigger id="ac-status"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {STATUSES.map((o) => (
                      <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="ac-started">Started</Label>
                <Input
                  id="ac-started"
                  type="date"
                  value={form.startedDate}
                  onChange={(e) => set('startedDate', e.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="ac-completed">Completed</Label>
                <Input
                  id="ac-completed"
                  type="date"
                  value={form.completionDate}
                  onChange={(e) => set('completionDate', e.target.value)}
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="ac-completion-notes">Completion notes</Label>
              <Textarea
                id="ac-completion-notes"
                rows={2}
                maxLength={2000}
                value={form.completionNotes}
                onChange={(e) => set('completionNotes', e.target.value)}
                placeholder="What was actually done"
              />
            </div>

            <div className="flex items-start gap-2 rounded-md border p-3">
              <Checkbox
                id="ac-successful"
                checked={form.wasSuccessful}
                onCheckedChange={(v) => set('wasSuccessful', v === true)}
              />
              <div className="space-y-1">
                <Label htmlFor="ac-successful" className="cursor-pointer">
                  The action achieved what it set out to
                </Label>
                <p className="text-xs text-muted-foreground">
                  Separate from the status: an action can be completed and still not have worked.
                </p>
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="ac-outcome-notes">Outcome notes</Label>
              <Textarea
                id="ac-outcome-notes"
                rows={2}
                maxLength={2000}
                value={form.outcomeNotes}
                onChange={(e) => set('outcomeNotes', e.target.value)}
                placeholder="What changed as a result — or why it did not"
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setEditing(null)} disabled={update.isPending}>
              Cancel
            </Button>
            <Button
              onClick={() => editing && update.mutate({ id: editing.id })}
              disabled={!descriptionValid || update.isPending}
            >
              {update.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={pendingDelete !== null}
        onOpenChange={(o) => !o && setPendingDelete(null)}
        title="Remove this action?"
        description={`"${pendingDelete?.actionDescription.slice(0, 80) ?? ''}" will be removed from the plan.`}
        confirmText="Remove"
        variant="destructive"
        isLoading={remove.isPending}
        onConfirm={async () => {
          if (pendingDelete) await remove.mutateAsync(pendingDelete);
        }}
      />
    </Card>
  );
}
