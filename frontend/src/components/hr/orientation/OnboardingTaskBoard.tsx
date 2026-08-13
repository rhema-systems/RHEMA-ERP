'use client';

import { useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Loader2,
  MoreHorizontal,
  CheckCircle2,
  ShieldCheck,
  MessageSquare,
  AlertTriangle,
  Plus,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Input } from '@/components/ui/input';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useToast } from '@/components/ui/use-toast';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { onboardingPlanService } from '@/services/hr/onboarding.service';
import { ONBOARDING_TASK_CATEGORY_OPTIONS } from '@/types/hr/onboarding';
import type { OnboardingTask, OnboardingTaskStatus } from '@/types/hr/onboarding';

const categoryLabel = (v: string) =>
  ONBOARDING_TASK_CATEGORY_OPTIONS.find((o) => o.value === v)?.label ?? v;
const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * Board columns.
 *
 * `PendingVerification` gets a column of its own rather than sitting in "done": the task has been
 * carried out but somebody else still owes it a sign-off, and folding it into Done is exactly how
 * that second step gets forgotten. `Overdue` sits in To do — it is work still outstanding, marked
 * rather than filed away.
 */
const COLUMNS: { key: string; title: string; statuses: OnboardingTaskStatus[] }[] = [
  { key: 'todo', title: 'To do', statuses: ['Pending', 'Blocked', 'Overdue'] },
  { key: 'doing', title: 'In progress', statuses: ['InProgress'] },
  { key: 'verify', title: 'Awaiting verification', statuses: ['PendingVerification'] },
  { key: 'done', title: 'Done', statuses: ['Completed', 'Waived'] },
];

const STATUS_VARIANT: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Overdue: 'destructive',
  Blocked: 'destructive',
  PendingVerification: 'secondary',
  Completed: 'default',
  Waived: 'outline',
  InProgress: 'secondary',
  Pending: 'outline',
};

interface Props {
  planId: string;
  /** A completed or cancelled plan is a record, not a worklist. */
  readOnly?: boolean;
  onChanged?: () => void;
}

/**
 * The plan's tasks as a board.
 *
 * ⚠ Completing a task flagged `requiresVerification` moves it to `PendingVerification`, not
 * `Completed` — so the returned status is read back rather than assumed, and the message says which
 * of the two happened. Verification is refused for whoever completed the task, which is the point of
 * having it.
 */
export function OnboardingTaskBoard({ planId, readOnly, onChanged }: Props) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [completeTarget, setCompleteTarget] = useState<OnboardingTask | null>(null);
  const [completionNotes, setCompletionNotes] = useState('');
  const [evidencePath, setEvidencePath] = useState('');
  const [commentTarget, setCommentTarget] = useState<OnboardingTask | null>(null);
  const [newComment, setNewComment] = useState('');
  const [busy, setBusy] = useState(false);

  const tasksKey = ['hr', 'onboarding-plans', planId, 'tasks'];
  const { data: tasks = [], isLoading } = useQuery({
    queryKey: tasksKey,
    queryFn: () => onboardingPlanService.getTasks(planId),
    enabled: !!planId,
  });

  const commentTaskId = commentTarget?.id;
  const { data: comments = [], isLoading: loadingComments } = useQuery({
    queryKey: ['hr', 'onboarding-tasks', commentTaskId, 'comments'],
    queryFn: () => onboardingPlanService.getTaskComments(commentTaskId ?? ''),
    enabled: !!commentTaskId,
  });

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: tasksKey }),
      queryClient.invalidateQueries({ queryKey: ['hr', 'onboarding-plans', planId] }),
      queryClient.invalidateQueries({ queryKey: ['hr', 'onboarding-plans'] }),
      // The queues screen counts the same rows.
      queryClient.invalidateQueries({ queryKey: ['hr', 'onboarding-queues'] }),
    ]);
    onChanged?.();
  };

  const grouped = useMemo(() => {
    const sorted = [...tasks].sort(
      (a, b) => a.displayOrder - b.displayOrder || a.dueDate.localeCompare(b.dueDate),
    );
    return COLUMNS.map((c) => ({
      ...c,
      items: sorted.filter((t) => c.statuses.includes(t.status)),
    }));
  }, [tasks]);

  const runComplete = async () => {
    if (!completeTarget) return;
    setBusy(true);
    try {
      const updated = await onboardingPlanService.completeTask(completeTarget.id, {
        taskId: completeTarget.id,
        completionNotes: completionNotes.trim() || null,
        evidenceFilePath: evidencePath.trim() || null,
      });
      await refresh();
      // Read the status back rather than announcing it done — a task requiring verification is not.
      toast(
        updated.status === 'PendingVerification'
          ? {
              title: 'Sent for verification',
              description: 'It is done, but someone else has to sign it off before it closes.',
            }
          : { title: 'Task completed', description: updated.taskName },
      );
      setCompleteTarget(null);
      setCompletionNotes('');
      setEvidencePath('');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to complete the task.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  const runVerify = async (task: OnboardingTask) => {
    try {
      await onboardingPlanService.verifyTask(task.id, { taskId: task.id });
      await refresh();
      toast({ title: 'Verified', description: task.taskName });
    } catch (error: any) {
      // The server refuses a self-verification with its own message; it is surfaced as-is because
      // "you completed this yourself" is the whole explanation.
      toast({
        title: 'Could not verify',
        description: error?.message || 'Failed to verify the task.',
        variant: 'destructive',
      });
    }
  };

  const addComment = async () => {
    if (!commentTarget || !newComment.trim()) return;
    setBusy(true);
    try {
      await onboardingPlanService.addTaskComment(commentTarget.id, {
        taskId: commentTarget.id,
        comment: newComment.trim(),
      });
      await queryClient.invalidateQueries({
        queryKey: ['hr', 'onboarding-tasks', commentTarget.id, 'comments'],
      });
      setNewComment('');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to add the comment.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }

  if (tasks.length === 0) {
    return (
      <EmptyState
        title="No tasks on this plan"
        description="It was created without a template, or the template had no tasks. Add them below."
      />
    );
  }

  return (
    <>
      <div className="grid gap-4 lg:grid-cols-4">
        {grouped.map((column) => (
          <div key={column.key} className="space-y-2">
            <div className="flex items-center justify-between px-1">
              <h3 className="text-sm font-medium">{column.title}</h3>
              <Badge variant="outline">{column.items.length}</Badge>
            </div>

            <div className="space-y-2">
              {column.items.length === 0 ? (
                <p className="text-muted-foreground rounded-md border border-dashed p-3 text-center text-xs">
                  Nothing here
                </p>
              ) : (
                column.items.map((task) => (
                  <Card key={task.id}>
                    <CardContent className="space-y-2 p-3">
                      <div className="flex items-start justify-between gap-2">
                        <p className="text-sm font-medium leading-tight">{task.taskName}</p>
                        {!readOnly && (
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button variant="ghost" size="icon" className="-mr-1 h-6 w-6 shrink-0">
                                <MoreHorizontal className="h-4 w-4" />
                                <span className="sr-only">Actions</span>
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              {task.status !== 'Completed' &&
                                task.status !== 'Waived' &&
                                task.status !== 'PendingVerification' && (
                                  <DropdownMenuItem onClick={() => setCompleteTarget(task)}>
                                    <CheckCircle2 className="mr-2 h-4 w-4" />
                                    Complete
                                  </DropdownMenuItem>
                                )}
                              {task.status === 'PendingVerification' && (
                                <DropdownMenuItem onClick={() => runVerify(task)}>
                                  <ShieldCheck className="mr-2 h-4 w-4" />
                                  Verify
                                </DropdownMenuItem>
                              )}
                              <DropdownMenuItem onClick={() => setCommentTarget(task)}>
                                <MessageSquare className="mr-2 h-4 w-4" />
                                Comments
                              </DropdownMenuItem>
                            </DropdownMenuContent>
                          </DropdownMenu>
                        )}
                      </div>

                      <div className="flex flex-wrap items-center gap-1">
                        <Badge variant={STATUS_VARIANT[task.status] ?? 'outline'} className="text-[10px]">
                          {task.statusName ?? task.status}
                        </Badge>
                        {task.isMandatory && (
                          <Badge variant="outline" className="text-[10px]">
                            Mandatory
                          </Badge>
                        )}
                        <Badge variant="outline" className="text-[10px]">
                          {categoryLabel(task.category)}
                        </Badge>
                      </div>

                      <div className="text-muted-foreground space-y-0.5 text-xs">
                        <p className={task.isOverdue ? 'font-medium text-red-600' : undefined}>
                          Due {fmt(task.dueDate)}
                          {task.isOverdue && ' · overdue'}
                        </p>
                        <p>
                          {task.assignedToName
                            ? `Assigned to ${task.assignedToName}`
                            : task.assignedOrganizationUnitName
                              ? `Assigned to ${task.assignedOrganizationUnitName}`
                              : task.ownerPositionTitle
                                ? `Owned by ${task.ownerPositionTitle}`
                                : 'Unassigned'}
                        </p>
                        {task.awaitingVerification && (
                          <p className="text-amber-600">
                            Completed by {task.completedByName ?? 'someone'} — needs a second
                            signature
                          </p>
                        )}
                        {task.verifiedByName && (
                          <p>
                            Verified by {task.verifiedByName} {fmt(task.verifiedDate)}
                          </p>
                        )}
                      </div>
                    </CardContent>
                  </Card>
                ))
              )}
            </div>
          </div>
        ))}
      </div>

      <Dialog
        open={completeTarget !== null}
        onOpenChange={(o) => {
          if (!o) {
            setCompleteTarget(null);
            setCompletionNotes('');
            setEvidencePath('');
          }
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Complete “{completeTarget?.taskName}”</DialogTitle>
            <DialogDescription>
              {completeTarget?.requiresVerification
                ? 'This task needs verifying, so it will move to awaiting verification rather than closing — and you cannot verify it yourself.'
                : 'This closes the task.'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-2">
              <Label htmlFor="completion-notes">Notes</Label>
              <Textarea
                id="completion-notes"
                rows={3}
                value={completionNotes}
                onChange={(e) => setCompletionNotes(e.target.value)}
                placeholder="What was done."
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="evidence-path">Evidence file path</Label>
              <Input
                id="evidence-path"
                value={evidencePath}
                onChange={(e) => setEvidencePath(e.target.value)}
                placeholder="Optional — a path or reference to the supporting document"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCompleteTarget(null)} disabled={busy}>
              Cancel
            </Button>
            <Button onClick={runComplete} disabled={busy}>
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {completeTarget?.requiresVerification ? 'Complete & send for verification' : 'Complete'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={commentTarget !== null} onOpenChange={(o) => !o && setCommentTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{commentTarget?.taskName}</DialogTitle>
            <DialogDescription>
              {commentTarget?.description || 'Comments on this task.'}
            </DialogDescription>
          </DialogHeader>

          <div className="max-h-[40vh] space-y-3 overflow-y-auto py-2">
            {loadingComments ? (
              <p className="text-muted-foreground text-sm">Loading…</p>
            ) : comments.length === 0 ? (
              <p className="text-muted-foreground text-sm">No comments yet.</p>
            ) : (
              comments.map((c) => (
                <div key={c.id} className="rounded-md border p-3">
                  <p className="text-sm">{c.comment}</p>
                  <p className="text-muted-foreground mt-1 text-xs">
                    {c.authorName} · {new Date(c.commentDate).toLocaleString()}
                  </p>
                </div>
              ))
            )}
          </div>

          {!readOnly && (
            <div className="space-y-2">
              <Label htmlFor="new-comment">Add a comment</Label>
              <Textarea
                id="new-comment"
                rows={3}
                value={newComment}
                onChange={(e) => setNewComment(e.target.value)}
              />
            </div>
          )}

          <DialogFooter>
            <Button variant="outline" onClick={() => setCommentTarget(null)} disabled={busy}>
              Close
            </Button>
            {!readOnly && (
              <Button onClick={addComment} disabled={busy || !newComment.trim()}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                <Plus className="mr-2 h-4 w-4" />
                Comment
              </Button>
            )}
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

/** Shown on the plan header when mandatory work is still outstanding. */
export function MandatoryOutstandingNote({ tasks }: { tasks: OnboardingTask[] }) {
  const outstanding = tasks.filter(
    (t) => t.isMandatory && t.status !== 'Completed' && t.status !== 'Waived',
  );
  if (outstanding.length === 0) return null;
  return (
    <p className="flex items-center gap-2 text-sm text-amber-600">
      <AlertTriangle className="h-4 w-4" />
      {outstanding.length} mandatory task{outstanding.length === 1 ? '' : 's'} still outstanding.
    </p>
  );
}
