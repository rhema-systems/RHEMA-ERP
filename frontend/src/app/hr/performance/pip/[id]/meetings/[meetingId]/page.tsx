'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'next/navigation';
import { CheckCircle2, MessageSquarePlus, Save, TriangleAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { Progress } from '@/components/ui/progress';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Slider } from '@/components/ui/slider';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { formatDate } from '@/lib/hr/attendance-format';
import { pipMeetingService } from '@/services/hr/pip.service';
import { GOAL_PROGRESS_STATUS_OPTIONS, type GoalProgressStatus } from '@/types/hr/goals';
import type { PipMeetingForm } from '@/types/hr/pip';

/**
 * One PIP review meeting.
 *
 * The server hands back the whole form — the plan's context and a line per goal — and takes the
 * same object back, so this screen edits one object rather than juggling several calls. Goal
 * progress typed here is written onto the goals themselves when the form is saved: that is why
 * progress is recorded in a meeting rather than on the goals tab, so what changed and the
 * conversation that agreed it stay together.
 *
 * ⚠ A meeting has no stored status. "Completed" is derived from its date being in the past, so
 * the Complete button saves the notes and stamps nothing extra — it is a save with a fuller name.
 */
export default function PipMeetingPage() {
  const params = useParams<{ id: string; meetingId: string }>();
  const pipId = params.id;
  const meetingId = params.meetingId;
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [form, setForm] = useState<PipMeetingForm | null>(null);
  const [comment, setComment] = useState('');

  const meeting = useQuery({
    queryKey: ['hr', 'pip-meeting', meetingId, pipId],
    queryFn: () => pipMeetingService.getMeeting(meetingId, pipId),
    enabled: !!meetingId && !!pipId,
    retry: false,
  });

  // Seed the editable copy once the meeting arrives, and again after each save.
  useEffect(() => {
    if (meeting.data) setForm(meeting.data);
  }, [meeting.data]);

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'pip-meeting', meetingId] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'pip', pipId] });
  };

  const fail = (title: string) => (e: Error) =>
    toast({ title, description: e.message, variant: 'destructive' });

  const save = useMutation({
    mutationFn: (complete: boolean) => {
      if (!form) throw new Error('The meeting is still loading.');
      return complete
        ? pipMeetingService.complete(meetingId, form)
        : pipMeetingService.update(meetingId, form);
    },
    onSuccess: (_r, complete) => {
      toast({
        title: complete ? 'Meeting recorded' : 'Saved',
        description: complete ? 'Goal progress from this review has been applied.' : undefined,
      });
      refresh();
    },
    onError: fail('Could not save the meeting'),
  });

  const addComment = useMutation({
    mutationFn: () => pipMeetingService.addComment(meetingId, comment.trim()),
    onSuccess: () => {
      toast({ title: 'Comment added' });
      setComment('');
      refresh();
    },
    onError: fail('Could not add the comment'),
  });

  const patchGoal = (goalId: string, patch: { percent?: number; status?: GoalProgressStatus }) =>
    setForm((prev) =>
      prev
        ? {
            ...prev,
            goalUpdates: prev.goalUpdates.map((g) =>
              g.goalId === goalId
                ? {
                    ...g,
                    currentProgressPercent: patch.percent ?? g.currentProgressPercent,
                    currentStatus: patch.status ?? g.currentStatus,
                  }
                : g,
            ),
          }
        : prev,
    );

  if (meeting.isLoading || (!form && !meeting.isError)) {
    return (
      <div className="space-y-6 p-6">
        <Skeleton className="h-10 w-1/3" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (meeting.isError || !form) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="Review meeting" backHref={`/hr/performance/pip/${pipId}`} />
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={TriangleAlert}
              title="Could not load this meeting"
              description={(meeting.error as Error)?.message ?? 'It may have been removed.'}
            />
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`Review ${form.meetingNumber} of ${form.totalScheduledMeetings}`}
        description={`${form.pipNumber} — ${form.employeeName} · ${formatDate(form.meetingDate)}`}
        backHref={`/hr/performance/pip/${pipId}`}
        actions={
          <div className="flex items-center gap-2">
            <Button variant="outline" onClick={() => save.mutate(false)} disabled={save.isPending}>
              <Save className="mr-2 h-4 w-4" />
              Save
            </Button>
            <Button onClick={() => save.mutate(true)} disabled={save.isPending}>
              <CheckCircle2 className="mr-2 h-4 w-4" />
              {save.isPending ? 'Saving…' : 'Record meeting'}
            </Button>
          </div>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">What was discussed</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center gap-2">
            <Checkbox
              id="attended"
              checked={form.employeeAttended}
              onCheckedChange={(checked) =>
                setForm((p) => (p ? { ...p, employeeAttended: checked === true } : p))
              }
            />
            <Label htmlFor="attended">{form.employeeName} attended</Label>
          </div>

          <div className="space-y-2">
            <Label htmlFor="pm-progress">Progress since the last review</Label>
            <Textarea
              id="pm-progress"
              rows={4}
              maxLength={2000}
              value={form.progressNotes}
              onChange={(e) =>
                setForm((p) => (p ? { ...p, progressNotes: e.target.value } : p))
              }
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="pm-issues">Issues discussed</Label>
            <Textarea
              id="pm-issues"
              rows={3}
              maxLength={2000}
              value={form.issuesDiscussed ?? ''}
              onChange={(e) =>
                setForm((p) => (p ? { ...p, issuesDiscussed: e.target.value } : p))
              }
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="pm-actions">Actions agreed</Label>
            <Textarea
              id="pm-actions"
              rows={3}
              maxLength={2000}
              value={form.actionsAgreed ?? ''}
              onChange={(e) => setForm((p) => (p ? { ...p, actionsAgreed: e.target.value } : p))}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Goal progress agreed in this review</CardTitle>
        </CardHeader>
        <CardContent className="space-y-6">
          {form.goalUpdates.length === 0 ? (
            <EmptyState
              title="No goals on this plan"
              description="Goals are added while the plan is still a draft."
            />
          ) : (
            form.goalUpdates.map((goal) => (
              <div key={goal.goalId} className="space-y-3 rounded-md border p-4">
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div>
                    <p className="font-medium">{goal.goalTitle}</p>
                    {goal.successCriteria && (
                      <p className="text-xs text-muted-foreground">{goal.successCriteria}</p>
                    )}
                  </div>
                  <span className="text-xs text-muted-foreground">
                    Due {formatDate(goal.dueDate)}
                  </span>
                </div>

                <div className="grid gap-4 sm:grid-cols-[1fr_200px]">
                  <div className="space-y-2">
                    <Label>Progress — {Math.round(Number(goal.currentProgressPercent) || 0)}%</Label>
                    <div className="flex items-center gap-3">
                      <Slider
                        value={[Number(goal.currentProgressPercent) || 0]}
                        min={0}
                        max={100}
                        step={5}
                        onValueChange={([v]) => patchGoal(goal.goalId, { percent: v })}
                      />
                      <Progress
                        value={Number(goal.currentProgressPercent) || 0}
                        className="hidden h-2 w-24 sm:block"
                      />
                    </div>
                  </div>
                  <div className="space-y-2">
                    <Label>Status</Label>
                    <Select
                      value={goal.currentStatus}
                      onValueChange={(v) =>
                        patchGoal(goal.goalId, { status: v as GoalProgressStatus })
                      }
                    >
                      <SelectTrigger>
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
              </div>
            ))
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">The employee&apos;s comments</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {form.employeeComments ? (
            <p className="whitespace-pre-wrap rounded-md border bg-muted/40 p-3 text-sm">
              {form.employeeComments}
            </p>
          ) : (
            <p className="text-sm text-muted-foreground">
              Nothing recorded yet. The employee&apos;s right of reply is theirs to use — anyone on
              the plan can enter it here on their behalf if the meeting was verbal.
            </p>
          )}

          <div className="space-y-2">
            <Label htmlFor="pm-comment">Add a comment</Label>
            <Textarea
              id="pm-comment"
              rows={3}
              maxLength={2000}
              value={comment}
              onChange={(e) => setComment(e.target.value)}
              placeholder="Replaces whatever is above — this field holds one comment, not a thread."
            />
            <Button
              size="sm"
              variant="outline"
              onClick={() => addComment.mutate()}
              disabled={!comment.trim() || addComment.isPending}
            >
              <MessageSquarePlus className="mr-2 h-4 w-4" />
              {addComment.isPending ? 'Saving…' : 'Save comment'}
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
