'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'next/navigation';
import { CheckCircle2, Plus, Target, Trash2, TriangleAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { CheckInObjectivesPanel } from '@/components/hr/performance/CheckInObjectivesPanel';
import { useToast } from '@/hooks/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { PerformanceAttachmentsPanel } from '@/components/hr/performance/PerformanceAttachmentsPanel';
import { hasAnyPermissionAccess } from '@/lib/permissions';
import { formatDate, formatDateTime, humanizeEnum } from '@/lib/hr/attendance-format';
import { checkInService } from '@/services/hr/appraisal-run.service';
import { employeeGoalService } from '@/services/hr/goals.service';
import {
  GOAL_PROGRESS_STATUS_OPTIONS,
  type GoalProgressStatus,
} from '@/types/hr/appraisal-run';

/**
 * One check-in: what was discussed, and what it changed about the employee's goals.
 *
 * The goal updates are the substantive part. Recording one here **moves the goal itself** —
 * the percent, the reported status and the at-risk flag are applied to the `EmployeeGoal`, so
 * a goal flagged in a one-to-one turns up in the at-risk reports without anyone re-entering it.
 * A goal that is not live (draft, awaiting approval, locked, already complete) keeps its
 * status and only the note is kept.
 *
 * ⚠ **Private notes are the conductor's own record.** The server blanks them for everyone but
 * the conductor and the HR desk — and always for the check-in's subject (performance closure
 * P15) — and writes them only from the conductor (P6), so the field is offered to the conductor
 * alone. Closing the meeting is the conductor's or HR's act, never the subject's.
 */
export default function CheckInDetailPage() {
  const params = useParams<{ id: string }>();
  const checkInId = params.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { user } = useAuth();

  const [completeOpen, setCompleteOpen] = useState(false);
  const [goalOpen, setGoalOpen] = useState(false);
  const [notes, setNotes] = useState({ sharedNotes: '', privateNotes: '', actionItems: '' });
  const [goalForm, setGoalForm] = useState({
    employeeGoalId: '',
    updatedProgress: '' as string,
    updatedStatus: 'InProgress' as GoalProgressStatus,
    flaggedAtRisk: false,
    note: '',
  });

  const { data: checkIn, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'check-in', checkInId],
    queryFn: () => checkInService.getById(checkInId),
    enabled: !!checkInId,
    retry: false,
  });

  const { data: goalUpdates } = useQuery({
    queryKey: ['hr', 'check-in-goal-updates', checkInId],
    queryFn: () => checkInService.getGoalUpdates(checkInId),
    enabled: !!checkInId,
  });

  // The goals available to update are the employee's own, for this check-in's cycle.
  const { data: goals } = useQuery({
    queryKey: ['hr', 'employee-goals', checkIn?.employeeId, checkIn?.appraisalCycleId],
    queryFn: () =>
      employeeGoalService.getByEmployee(
        checkIn?.employeeId ?? '',
        checkIn?.appraisalCycleId ?? undefined,
      ),
    enabled: !!checkIn?.employeeId && !!checkIn?.appraisalCycleId,
  });

  useEffect(() => {
    if (!checkIn) return;
    setNotes({
      sharedNotes: checkIn.sharedNotes ?? '',
      privateNotes: checkIn.privateNotes ?? '',
      actionItems: checkIn.actionItems ?? '',
    });
  }, [checkIn]);

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['hr', 'check-in', checkInId] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'check-in-goal-updates', checkInId] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'check-ins'] });
    // The goal itself moved, so anything reading goals is now stale.
    queryClient.invalidateQueries({ queryKey: ['hr', 'employee-goals'] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'goals-at-risk'] });
  };

  const complete = useMutation({
    mutationFn: () =>
      checkInService.complete(checkInId, {
        sharedNotes: notes.sharedNotes.trim() || null,
        privateNotes: notes.privateNotes.trim() || null,
        actionItems: notes.actionItems.trim() || null,
      }),
    onSuccess: () => {
      toast({ title: 'Check-in recorded as held' });
      setCompleteOpen(false);
      refresh();
    },
    onError: (e: Error) =>
      toast({ title: 'Could not complete', description: e.message, variant: 'destructive' }),
  });

  const addGoalUpdate = useMutation({
    mutationFn: () =>
      checkInService.addGoalUpdate(checkInId, {
        checkInId,
        employeeGoalId: goalForm.employeeGoalId,
        updatedProgress:
          goalForm.updatedProgress === '' ? null : Number(goalForm.updatedProgress),
        updatedStatus: goalForm.updatedStatus,
        flaggedAtRisk: goalForm.flaggedAtRisk,
        note: goalForm.note.trim() || null,
      }),
    onSuccess: () => {
      toast({
        title: 'Goal updated',
        description: 'The goal itself has been moved, not just this check-in record.',
      });
      setGoalOpen(false);
      setGoalForm({
        employeeGoalId: '',
        updatedProgress: '',
        updatedStatus: 'InProgress',
        flaggedAtRisk: false,
        note: '',
      });
      refresh();
    },
    onError: (e: Error) =>
      toast({ title: 'Could not record update', description: e.message, variant: 'destructive' }),
  });

  const removeGoalUpdate = useMutation({
    mutationFn: (updateId: string) => checkInService.removeGoalUpdate(checkInId, updateId),
    onSuccess: () => {
      toast({
        title: 'Update removed',
        // Being straight about what deleting does and does not undo.
        description: 'The goal keeps the figures this update put on it.',
      });
      refresh();
    },
    onError: (e: Error) =>
      toast({ title: 'Could not remove', description: e.message, variant: 'destructive' }),
  });

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-10 w-1/3" />
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  if (isError || !checkIn) {
    return (
      <div className="space-y-6">
        <PageHeader title="Check-in" backHref="/me/performance/check-ins" />
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={TriangleAlert}
              title="Check-in not found"
              description={(error as Error)?.message ?? 'It may have been deleted.'}
            />
          </CardContent>
        </Card>
      </div>
    );
  }

  const held = Boolean(checkIn.conductedDate);
  const rows = goalUpdates ?? [];
  const availableGoals = goals ?? [];

  // The server's rules, so the page offers only what it will accept (P6, P9).
  const me = user?.employeeId ?? null;
  const isConductor = !!me && me === checkIn.conductedById;
  const isSubject = !!me && me === checkIn.employeeId;
  const isDeskWriter = hasAnyPermissionAccess(user, ['HR.Performance.Write', 'HR.Performance.Admin']);
  const canClose = isConductor || (!isSubject && isDeskWriter);

  return (
    <div className="space-y-6">
      <PageHeader
        title={checkIn.title}
        description={`${humanizeEnum(checkIn.checkInType)} with ${checkIn.employeeName} · held by ${checkIn.conductedByName}`}
        backHref="/me/performance/check-ins"
        actions={
          <div className="flex items-center gap-2">
            {held ? (
              <Badge variant="default">Held {formatDate(checkIn.conductedDate)}</Badge>
            ) : canClose ? (
              <Button onClick={() => setCompleteOpen(true)}>
                <CheckCircle2 className="mr-2 h-4 w-4" />
                Record as held
              </Button>
            ) : (
              <Badge variant="secondary">Not yet held</Badge>
            )}
          </div>
        }
      />

      <Card>
        <CardContent className="grid gap-4 p-4 text-sm sm:grid-cols-3">
          <Field label="Scheduled" value={formatDateTime(checkIn.scheduledDate)} />
          <Field label="Cycle" value={checkIn.cycleCode ?? '—'} />
          <Field
            label="Follow-up"
            value={checkIn.followUpDate ? formatDate(checkIn.followUpDate) : '—'}
          />
        </CardContent>
      </Card>

      <CheckInObjectivesPanel
        checkInId={checkInId}
        cycleId={checkIn.appraisalCycleId}
        readOnly={held}
      />

      {checkIn.agenda && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Agenda</CardTitle>
          </CardHeader>
          <CardContent className="whitespace-pre-wrap text-sm">{checkIn.agenda}</CardContent>
        </Card>
      )}

      {held && (
        <div className="grid gap-4 md:grid-cols-2">
          <NoteCard title="Shared notes" body={checkIn.sharedNotes} />
          <NoteCard title="Action items" body={checkIn.actionItems} />
          {checkIn.privateNotes && (
            <NoteCard
              title="Private notes"
              body={checkIn.privateNotes}
              hint="Only the person who held this check-in should see these."
            />
          )}
          {checkIn.employeeComments && (
            <NoteCard title="Employee comments" body={checkIn.employeeComments} />
          )}
        </div>
      )}

      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-start justify-between gap-2">
            <div>
              <CardTitle className="text-base">Goal updates</CardTitle>
              <p className="mt-1 text-sm text-muted-foreground">
                Recording an update here moves the goal itself, so it shows up in progress and
                at-risk reporting.
              </p>
            </div>
            <Button size="sm" variant="outline" onClick={() => setGoalOpen(true)}>
              <Plus className="mr-2 h-4 w-4" />
              Update a goal
            </Button>
          </div>
        </CardHeader>
        <CardContent className="p-0">
          {rows.length === 0 ? (
            <EmptyState
              icon={Target}
              title="No goals updated"
              description="Record where each goal stands so the employee's progress reflects this conversation."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Goal</TableHead>
                  <TableHead className="w-28 text-right">Progress</TableHead>
                  <TableHead className="w-32">Status</TableHead>
                  <TableHead className="w-16" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row) => (
                  <TableRow key={row.id}>
                    <TableCell>
                      <div className="font-medium">{row.goalTitle}</div>
                      {row.note && (
                        <div className="mt-1 whitespace-pre-wrap text-xs text-muted-foreground">
                          {row.note}
                        </div>
                      )}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {row.updatedProgress != null ? `${Number(row.updatedProgress).toFixed(0)}%` : '—'}
                    </TableCell>
                    <TableCell>
                      <StatusBadge
                        status={row.flaggedAtRisk ? 'At risk' : humanizeEnum(row.updatedStatus)}
                      />
                    </TableCell>
                    <TableCell className="text-right">
                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => removeGoalUpdate.mutate(row.id)}
                        aria-label={`Remove update for ${row.goalTitle}`}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={completeOpen} onOpenChange={setCompleteOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Record this check-in as held</DialogTitle>
            <DialogDescription>
              These fields are replaced wholesale each time, not merged — paste back anything
              you want to keep.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="shared">Shared notes</Label>
              <Textarea
                id="shared"
                rows={4}
                maxLength={4000}
                value={notes.sharedNotes}
                onChange={(e) => setNotes((p) => ({ ...p, sharedNotes: e.target.value }))}
                placeholder="What you both agreed. The employee can see this."
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="actions">Action items</Label>
              <Textarea
                id="actions"
                rows={3}
                maxLength={2000}
                value={notes.actionItems}
                onChange={(e) => setNotes((p) => ({ ...p, actionItems: e.target.value }))}
              />
            </div>
            {/* The conductor's own record — the server keeps anyone else's value out (P6). */}
            {isConductor && (
              <div className="space-y-2">
                <Label htmlFor="private">Private notes</Label>
                <Textarea
                  id="private"
                  rows={3}
                  maxLength={4000}
                  value={notes.privateNotes}
                  onChange={(e) => setNotes((p) => ({ ...p, privateNotes: e.target.value }))}
                  placeholder="Your own record."
                />
                <p className="text-xs text-muted-foreground">
                  Kept out of the employee&apos;s view of this check-in.
                </p>
              </div>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCompleteOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => complete.mutate()} disabled={complete.isPending}>
              Record as held
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={goalOpen} onOpenChange={setGoalOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Update a goal</DialogTitle>
            <DialogDescription>
              This is applied to the goal, not just recorded against the check-in.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="goal">Goal</Label>
              <Select
                value={goalForm.employeeGoalId}
                onValueChange={(v) => setGoalForm((p) => ({ ...p, employeeGoalId: v }))}
              >
                <SelectTrigger id="goal">
                  <SelectValue
                    placeholder={
                      availableGoals.length === 0
                        ? 'No goals in this cycle'
                        : 'Choose a goal'
                    }
                  />
                </SelectTrigger>
                <SelectContent>
                  {availableGoals.map((g) => (
                    <SelectItem key={g.id} value={g.id}>
                      {g.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="progress">Progress %</Label>
                <Input
                  id="progress"
                  type="number"
                  min={0}
                  max={100}
                  value={goalForm.updatedProgress}
                  onChange={(e) =>
                    setGoalForm((p) => ({ ...p, updatedProgress: e.target.value }))
                  }
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="gstatus">Status</Label>
                <Select
                  value={goalForm.updatedStatus}
                  onValueChange={(v) =>
                    setGoalForm((p) => ({ ...p, updatedStatus: v as GoalProgressStatus }))
                  }
                >
                  <SelectTrigger id="gstatus">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {GOAL_PROGRESS_STATUS_OPTIONS.map((o) => (
                      <SelectItem key={o.value} value={o.value}>
                        {o.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <label className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={goalForm.flaggedAtRisk}
                onCheckedChange={(c) =>
                  setGoalForm((p) => ({ ...p, flaggedAtRisk: c === true }))
                }
              />
              Flag this goal as at risk
            </label>
            <div className="space-y-2">
              <Label htmlFor="gnote">Note</Label>
              <Textarea
                id="gnote"
                rows={3}
                maxLength={2000}
                value={goalForm.note}
                onChange={(e) => setGoalForm((p) => ({ ...p, note: e.target.value }))}
              />
            </div>
            <Alert>
              <TriangleAlert className="h-4 w-4" />
              <AlertTitle>This changes the goal</AlertTitle>
              <AlertDescription>
                100% completes it. Flagging it at risk overrides the status you picked. A goal
                that is not yet approved keeps its status and only the note is recorded.
              </AlertDescription>
            </Alert>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setGoalOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => addGoalUpdate.mutate()}
              disabled={!goalForm.employeeGoalId || addGoalUpdate.isPending}
            >
              Record update
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ⚠ Goals and review events have had an attachment panel since the area shipped; check-ins
          did not, so anything agreed in a one-to-one could be described and never evidenced. */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Attachments</CardTitle>
          <p className="mt-1 text-sm text-muted-foreground">
            Anything referred to in this check-in — a plan, a report, a note.
          </p>
        </CardHeader>
        <CardContent>
          {/* P9: a file is removed by whoever attached it, or HR (never as the subject), and
              only until the check-in is held — after that it is part of the record. */}
          <PerformanceAttachmentsPanel
            basePath="/CheckIns"
            ownerId={checkInId}
            canDelete={!held}
            canDeleteItem={(a) =>
              (!!me && a.uploadedById === me) || (isDeskWriter && !isSubject)
            }
          />
        </CardContent>
      </Card>
    </div>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-muted-foreground">{label}</p>
      <p className="mt-0.5 font-medium">{value}</p>
    </div>
  );
}

function NoteCard({ title, body, hint }: { title: string; body?: string | null; hint?: string }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{title}</CardTitle>
        {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
      </CardHeader>
      <CardContent className="whitespace-pre-wrap text-sm">
        {body || <span className="text-muted-foreground">Nothing recorded.</span>}
      </CardContent>
    </Card>
  );
}
