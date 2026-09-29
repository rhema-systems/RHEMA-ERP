'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, ClipboardCheck, HeartHandshake, Loader2, Rocket } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Progress } from '@/components/ui/progress';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { myOnboardingService } from '@/services/hr/my-onboarding.service';
import {
  ONBOARDING_STATUS_OPTIONS,
  ONBOARDING_TASK_CATEGORY_OPTIONS,
  ONBOARDING_TASK_STATUS_OPTIONS,
} from '@/types/hr/onboarding';
import type { MyOnboardingTask, OnboardingTaskStatus } from '@/types/hr/onboarding';

/** A DateOnly ("2026-10-05") as the reader's own calendar day — never shifted by a time zone. */
const fmtDay = (v?: string | null) =>
  v ? new Date(`${v}T00:00:00`).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' }) : '—';
const label = <T extends string>(options: { value: T; label: string }[], v: T) =>
  options.find((o) => o.value === v)?.label ?? v;

const TASK_TONE: Partial<Record<OnboardingTaskStatus, 'default' | 'secondary' | 'outline' | 'destructive'>> = {
  Completed: 'default',
  PendingVerification: 'secondary',
  Waived: 'secondary',
  Overdue: 'destructive',
  Blocked: 'destructive',
};

/**
 * Onboarding as the signed-in employee sees it (round 4, lane K-b2): their own plan as a new hire,
 * the tasks given to them on anybody's plan — with Mark done — and the new colleagues they are buddy
 * to. Every onboarding notice leads here; before this page a task's assignee could neither see nor
 * finish it unless they were HR.
 *
 * Reads the caller's own record from the token; there is no id to get wrong.
 */
export default function MyOnboardingPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [doing, setDoing] = useState<MyOnboardingTask | null>(null);
  const [notes, setNotes] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'my-onboarding'],
    queryFn: () => myOnboardingService.getMine(),
  });

  const markDone = useMutation({
    mutationFn: (task: MyOnboardingTask) =>
      myOnboardingService.markDone(task.taskId, { completionNotes: notes.trim() || null }),
    onSuccess: (task) => {
      toast({
        title: task.status === 'PendingVerification' ? 'Marked done — waiting for sign-off' : 'Marked done',
        description:
          task.status === 'PendingVerification'
            ? `${task.coordinatorName ?? 'HR'} has been told it is ready to sign off.`
            : task.taskName,
      });
      setDoing(null);
      setNotes('');
      queryClient.invalidateQueries({ queryKey: ['hr', 'my-onboarding'] });
    },
    onError: (error: any) =>
      toast({ title: 'Could not mark it done', description: error?.message, variant: 'destructive' }),
  });

  const plan = data?.myPlan;
  const tasks = data?.tasksAssignedToMe ?? [];
  const buddyFor = data?.buddyFor ?? [];
  const open = tasks.filter((t) => t.canMarkDone);
  const nothing = !isLoading && !plan && tasks.length === 0 && buddyFor.length === 0;

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Onboarding"
        description="Your own onboarding, the onboarding tasks you have been given, and the new colleagues you are helping settle in."
        backHref="/me"
      />

      {isLoading ? (
        <p className="text-muted-foreground text-sm">Loading…</p>
      ) : nothing ? (
        <EmptyState
          icon={Rocket}
          title="Nothing here"
          description="When you start, when somebody's onboarding needs something from you, or when you are named a new colleague's buddy, it appears here."
        />
      ) : (
        <>
          {plan && (
            <Card>
              <CardHeader className="flex flex-row items-start justify-between gap-3 space-y-0">
                <div>
                  <CardTitle className="text-base">
                    <Rocket className="mr-2 inline h-4 w-4" />
                    Your onboarding
                  </CardTitle>
                  <p className="text-muted-foreground mt-1 text-sm">
                    From {fmtDay(plan.startDate)}
                    {plan.targetCompletionDate ? ` to ${fmtDay(plan.targetCompletionDate)}` : ''}
                    {plan.coordinatorName ? ` · coordinated by ${plan.coordinatorName}` : ''}
                    {plan.buddyName ? ` · your buddy is ${plan.buddyName}` : ''}
                  </p>
                </div>
                <Badge variant="outline">{label(ONBOARDING_STATUS_OPTIONS, plan.status)}</Badge>
              </CardHeader>
              <CardContent className="space-y-4">
                {plan.tasksTotal > 0 && (
                  <div className="flex items-center gap-3">
                    <Progress value={(plan.tasksDone / plan.tasksTotal) * 100} className="h-2 max-w-xs" />
                    <span className="text-muted-foreground text-sm">
                      {plan.tasksDone} of {plan.tasksTotal} done
                    </span>
                  </div>
                )}
                {plan.tasks.length === 0 ? (
                  <p className="text-muted-foreground text-sm">No tasks have been planned yet.</p>
                ) : (
                  <ul className="divide-y rounded-md border">
                    {plan.tasks.map((t) => (
                      <li key={t.taskId} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2 text-sm">
                        <div>
                          <span className="font-medium">{t.taskName}</span>
                          {t.isMine && (
                            <Badge variant="secondary" className="ml-2 text-[10px]">
                              Yours to do
                            </Badge>
                          )}
                          <div className="text-muted-foreground text-xs">
                            {label(ONBOARDING_TASK_CATEGORY_OPTIONS, t.category)} · due {fmtDay(t.dueDate)}
                            {t.who && !t.isMine ? ` · ${t.who}` : ''}
                          </div>
                        </div>
                        <Badge variant={TASK_TONE[t.status] ?? 'outline'}>
                          {label(ONBOARDING_TASK_STATUS_OPTIONS, t.status)}
                        </Badge>
                      </li>
                    ))}
                  </ul>
                )}
              </CardContent>
            </Card>
          )}

          <Card id="tasks">
            <CardHeader>
              <CardTitle className="text-base">
                <ClipboardCheck className="mr-2 inline h-4 w-4" />
                Tasks given to you
                {open.length > 0 && (
                  <Badge variant="default" className="ml-2">
                    {open.length} to do
                  </Badge>
                )}
              </CardTitle>
            </CardHeader>
            <CardContent>
              {tasks.length === 0 ? (
                <p className="text-muted-foreground text-sm">
                  No onboarding tasks have been given to you. Tasks you finish stay here for 30 days.
                </p>
              ) : (
                <ul className="space-y-3">
                  {tasks.map((t) => (
                    <li key={t.taskId} className="rounded-md border p-3">
                      <div className="flex flex-wrap items-start justify-between gap-3">
                        <div className="min-w-0 space-y-1">
                          <div className="font-medium">{t.taskName}</div>
                          <div className="text-muted-foreground text-xs">
                            {t.forMyOwnOnboarding
                              ? 'Part of your own onboarding'
                              : `For ${t.newHireName ?? 'a new colleague'}, starting ${fmtDay(t.newHireStartDate)}`}
                            {t.coordinatorName ? ` · coordinated by ${t.coordinatorName}` : ''}
                          </div>
                          {t.description && <p className="text-sm whitespace-pre-line">{t.description}</p>}
                          <div className="flex flex-wrap items-center gap-2 text-xs">
                            <span className={t.isOverdue ? 'font-medium text-red-600' : 'text-muted-foreground'}>
                              {t.isOverdue ? 'Overdue — was due ' : 'Due '}
                              {fmtDay(t.dueDate)}
                            </span>
                            {t.requiresVerification && (
                              <Badge variant="outline" className="text-[10px]">
                                Signed off by {t.coordinatorName ?? 'HR'}
                              </Badge>
                            )}
                          </div>
                          {t.completionNotes && (
                            <p className="text-muted-foreground text-xs">Your note: {t.completionNotes}</p>
                          )}
                        </div>
                        <div className="flex flex-col items-end gap-2">
                          <Badge variant={TASK_TONE[t.status] ?? 'outline'}>
                            {label(ONBOARDING_TASK_STATUS_OPTIONS, t.status)}
                          </Badge>
                          {t.canMarkDone ? (
                            <Button size="sm" onClick={() => { setDoing(t); setNotes(''); }}>
                              <CheckCircle2 className="mr-2 h-4 w-4" />
                              Mark done
                            </Button>
                          ) : (
                            t.cannotMarkDoneBecause && (
                              <span className="text-muted-foreground text-xs">{t.cannotMarkDoneBecause}</span>
                            )
                          )}
                        </div>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>

          {buddyFor.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">
                  <HeartHandshake className="mr-2 inline h-4 w-4" />
                  New colleagues you are buddy to
                </CardTitle>
              </CardHeader>
              <CardContent>
                <ul className="divide-y rounded-md border">
                  {buddyFor.map((b) => (
                    <li key={b.planId} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2 text-sm">
                      <div>
                        <span className="font-medium">{b.newHireName ?? 'A new colleague'}</span>
                        <div className="text-muted-foreground text-xs">
                          Starting {fmtDay(b.startDate)}
                          {b.coordinatorName ? ` · coordinated by ${b.coordinatorName}` : ''}
                        </div>
                      </div>
                      <Badge variant="outline">{label(ONBOARDING_STATUS_OPTIONS, b.status)}</Badge>
                    </li>
                  ))}
                </ul>
              </CardContent>
            </Card>
          )}
        </>
      )}

      <Dialog open={doing !== null} onOpenChange={(o) => !o && !markDone.isPending && setDoing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Mark “{doing?.taskName}” done</DialogTitle>
            <DialogDescription>
              {doing?.requiresVerification
                ? `${doing?.coordinatorName ?? 'HR'} signs it off after you do, and is told it is ready.`
                : 'It is recorded as done by you, today.'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="done-notes">A note (optional)</Label>
            <Textarea
              id="done-notes"
              rows={3}
              maxLength={2000}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="What was done — a serial number, who it was handed to…"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDoing(null)} disabled={markDone.isPending}>
              Cancel
            </Button>
            <Button onClick={() => doing && markDone.mutate(doing)} disabled={markDone.isPending}>
              {markDone.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Mark done
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
