'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'next/navigation';
import {
  MessageSquarePlus,
  Pencil,
  Play,
  Plus,
  Target,
  Trash2,
  TriangleAlert,
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { formatDate, formatDateTime, humanizeEnum } from '@/lib/hr/attendance-format';
import {
  developmentPlanFeedbackService,
  developmentPlanService,
} from '@/services/hr/development.service';
import {
  DEVELOPMENT_FEEDBACK_TYPE_OPTIONS,
  DEVELOPMENT_OBJECTIVE_STATUS_OPTIONS,
  DEVELOPMENT_PLAN_STATUS_OPTIONS,
  type DevelopmentFeedbackType,
  type DevelopmentObjective,
  type DevelopmentObjectiveStatus,
  type DevelopmentPlanStatus,
} from '@/types/hr/development';

/**
 * One development plan: the objectives, the progress against them, and the manager's running
 * commentary.
 *
 * **Draft is a real state.** A draft plan is invisible to the employee in the sense that matters —
 * nothing has told them about it. Activating raises the notification, which is why the banner
 * pushes for it rather than leaving the status picker to do the job quietly.
 */
export default function DevelopmentPlanDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [objectiveOpen, setObjectiveOpen] = useState(false);
  const [editing, setEditing] = useState<DevelopmentObjective | null>(null);
  const [progressFor, setProgressFor] = useState<DevelopmentObjective | null>(null);
  const [feedbackOpen, setFeedbackOpen] = useState(false);

  const [objectiveForm, setObjectiveForm] = useState({
    title: '',
    description: '',
    actions: '',
    targetDate: '',
    objectiveStatus: 'NotStarted' as DevelopmentObjectiveStatus,
  });
  const [progressForm, setProgressForm] = useState({
    progressPercent: 0,
    notes: '',
    status: 'InProgress' as DevelopmentObjectiveStatus,
  });
  const [feedbackForm, setFeedbackForm] = useState({
    feedbackType: 'GeneralComment' as DevelopmentFeedbackType,
    comment: '',
  });

  const plan = useQuery({
    queryKey: ['hr', 'development-plan', id],
    queryFn: () => developmentPlanService.getById(id),
    enabled: !!id,
    retry: false,
  });

  const objectives = useQuery({
    queryKey: ['hr', 'development-plan', id, 'objectives'],
    queryFn: () => developmentPlanService.getObjectives(id),
    enabled: !!id && !plan.isError,
  });

  const feedback = useQuery({
    queryKey: ['hr', 'development-plan', id, 'feedback'],
    queryFn: () => developmentPlanFeedbackService.getByPlan(id),
    enabled: !!id && !plan.isError,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'development-plan', id] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'development-plans'] });
  };

  const fail = (title: string) => (e: Error) =>
    toast({ title, description: e.message, variant: 'destructive' });

  const setStatus = useMutation({
    mutationFn: (status: DevelopmentPlanStatus) => developmentPlanService.updateStatus(id, status),
    onSuccess: (_r, status) => {
      toast({
        title: `Plan ${humanizeEnum(status).toLowerCase()}`,
        description:
          status === 'Active' ? 'The employee has been notified that the plan is live.' : undefined,
      });
      refresh();
    },
    onError: fail('Could not change the status'),
  });

  const saveObjective = useMutation({
    mutationFn: () => {
      const payload = {
        developmentPlanId: id,
        title: objectiveForm.title.trim(),
        description: objectiveForm.description.trim() || null,
        actions: objectiveForm.actions.trim() || null,
        targetDate: objectiveForm.targetDate || null,
        objectiveStatus: objectiveForm.objectiveStatus,
      };
      return editing
        ? developmentPlanService.updateObjective(id, editing.id, {
            ...payload,
            id: editing.id,
            progressPercent: editing.progressPercent,
            progressNotes: editing.progressNotes ?? null,
          })
        : developmentPlanService.addObjective(id, payload);
    },
    onSuccess: () => {
      toast({ title: editing ? 'Objective updated' : 'Objective added' });
      setObjectiveOpen(false);
      setEditing(null);
      refresh();
    },
    onError: fail('Could not save the objective'),
  });

  const removeObjective = useMutation({
    mutationFn: (objectiveId: string) => developmentPlanService.deleteObjective(id, objectiveId),
    onSuccess: () => {
      toast({ title: 'Objective removed' });
      refresh();
    },
    onError: fail('Could not remove the objective'),
  });

  const saveProgress = useMutation({
    mutationFn: () => {
      if (!progressFor) throw new Error('No objective selected.');
      return developmentPlanService.updateObjectiveProgress(id, progressFor.id, {
        progressPercent: progressForm.progressPercent,
        notes: progressForm.notes.trim() || null,
        status: progressForm.status,
      });
    },
    onSuccess: () => {
      toast({ title: 'Progress recorded' });
      setProgressFor(null);
      refresh();
    },
    onError: fail('Could not record progress'),
  });

  const addFeedback = useMutation({
    mutationFn: () =>
      developmentPlanFeedbackService.add({
        developmentPlanId: id,
        feedbackType: feedbackForm.feedbackType,
        comment: feedbackForm.comment.trim(),
      }),
    onSuccess: () => {
      toast({ title: 'Feedback added', description: 'The employee has been notified.' });
      setFeedbackOpen(false);
      setFeedbackForm({ feedbackType: 'GeneralComment', comment: '' });
      refresh();
    },
    onError: fail('Could not add feedback'),
  });

  // Seed the progress dialog from whichever objective was opened.
  useEffect(() => {
    if (!progressFor) return;
    setProgressForm({
      progressPercent: Number(progressFor.progressPercent) || 0,
      notes: '',
      status: progressFor.objectiveStatus,
    });
  }, [progressFor]);

  const openNewObjective = () => {
    setEditing(null);
    setObjectiveForm({
      title: '',
      description: '',
      actions: '',
      targetDate: '',
      objectiveStatus: 'NotStarted',
    });
    setObjectiveOpen(true);
  };

  const openEditObjective = (objective: DevelopmentObjective) => {
    setEditing(objective);
    setObjectiveForm({
      title: objective.title,
      description: objective.description ?? '',
      actions: objective.actions ?? '',
      targetDate: objective.targetDate?.slice(0, 10) ?? '',
      objectiveStatus: objective.objectiveStatus,
    });
    setObjectiveOpen(true);
  };

  if (plan.isLoading) {
    return (
      <div className="space-y-6 p-6">
        <Skeleton className="h-10 w-1/3" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (plan.isError || !plan.data) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="Development plan" backHref="/hr/performance/development-plans" />
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={TriangleAlert}
              title="Could not load this plan"
              description={
                (plan.error as Error)?.message ??
                'It may have been removed, or it may belong to someone whose plans you cannot see.'
              }
            />
          </CardContent>
        </Card>
      </div>
    );
  }

  const data = plan.data;
  const rows = objectives.data ?? [];
  const isClosed = data.planStatus === 'Completed' || data.planStatus === 'Cancelled';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={data.title || 'Development plan'}
        description={`${data.employeeName}${data.cycleName ? ` · ${data.cycleName}` : ''}`}
        backHref="/hr/performance/development-plans"
        actions={
          <div className="flex items-center gap-2">
            <Select
              value={data.planStatus}
              onValueChange={(v) => setStatus.mutate(v as DevelopmentPlanStatus)}
            >
              <SelectTrigger className="w-[170px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {DEVELOPMENT_PLAN_STATUS_OPTIONS.map((o) => (
                  <SelectItem key={o.value} value={o.value}>
                    {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {!isClosed && (
              <Button onClick={openNewObjective}>
                <Plus className="mr-2 h-4 w-4" />
                Objective
              </Button>
            )}
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-3">
        <StatusBadge status={humanizeEnum(data.planStatus)} />
        <span className="text-sm text-muted-foreground">
          {formatDate(data.startDate)}
          {data.endDate ? ` – ${formatDate(data.endDate)}` : ' — no end date'}
        </span>
      </div>

      {data.planStatus === 'Draft' && (
        <Alert>
          <Play className="h-4 w-4" />
          <AlertTitle>Still a draft — the employee has not been told</AlertTitle>
          <AlertDescription className="flex flex-wrap items-center gap-3">
            <span>
              Add the objectives you have agreed, then activate the plan. Activating is what raises
              the notification.
            </span>
            <Button size="sm" onClick={() => setStatus.mutate('Active')} disabled={setStatus.isPending}>
              Activate
            </Button>
          </AlertDescription>
        </Alert>
      )}

      <MetricTiles
        tiles={[
          { label: 'Objectives', value: data.objectiveCount, icon: Target },
          { label: 'Completed', value: data.completedObjectiveCount, tone: 'success' },
          {
            label: 'Average progress',
            value: `${Math.round(Number(data.averageProgressPercent) || 0)}%`,
          },
          { label: 'Feedback entries', value: feedback.data?.length ?? 0 },
        ]}
      />

      <Tabs defaultValue="objectives">
        <TabsList>
          <TabsTrigger value="objectives">Objectives</TabsTrigger>
          <TabsTrigger value="feedback">Feedback</TabsTrigger>
          <TabsTrigger value="overview">Notes</TabsTrigger>
        </TabsList>

        <TabsContent value="objectives" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {objectives.isLoading ? (
                <div className="space-y-2 p-4">
                  <Skeleton className="h-12 w-full" />
                  <Skeleton className="h-12 w-full" />
                </div>
              ) : rows.length === 0 ? (
                <EmptyState
                  icon={Target}
                  title="No objectives yet"
                  description="An objective is one concrete thing to get better at, with a way of telling when it has happened."
                  action={
                    !isClosed ? (
                      <Button size="sm" onClick={openNewObjective}>
                        Add the first objective
                      </Button>
                    ) : undefined
                  }
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Objective</TableHead>
                      <TableHead>Target</TableHead>
                      <TableHead className="w-48">Progress</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="w-40" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {rows.map((objective) => (
                      <TableRow key={objective.id}>
                        <TableCell>
                          <div className="font-medium">{objective.title}</div>
                          {objective.description && (
                            <div className="max-w-md text-xs text-muted-foreground">
                              {objective.description}
                            </div>
                          )}
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {formatDate(objective.targetDate)}
                        </TableCell>
                        <TableCell>
                          <div className="flex items-center gap-2">
                            <Progress
                              value={Number(objective.progressPercent) || 0}
                              className="h-2"
                            />
                            <span className="w-10 text-right text-xs tabular-nums text-muted-foreground">
                              {Math.round(Number(objective.progressPercent) || 0)}%
                            </span>
                          </div>
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={humanizeEnum(objective.objectiveStatus)} />
                        </TableCell>
                        <TableCell className="text-right">
                          <div className="flex justify-end gap-1">
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => setProgressFor(objective)}
                              disabled={isClosed}
                            >
                              Progress
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() => openEditObjective(objective)}
                              disabled={isClosed}
                              aria-label="Edit objective"
                            >
                              <Pencil className="h-4 w-4" />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() => removeObjective.mutate(objective.id)}
                              disabled={isClosed || removeObjective.isPending}
                              aria-label="Remove objective"
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="feedback" className="pt-4">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle className="text-base">Manager feedback</CardTitle>
              <Button size="sm" onClick={() => setFeedbackOpen(true)}>
                <MessageSquarePlus className="mr-2 h-4 w-4" />
                Add feedback
              </Button>
            </CardHeader>
            <CardContent className="p-0">
              {feedback.isLoading ? (
                <div className="space-y-2 p-4">
                  <Skeleton className="h-12 w-full" />
                </div>
              ) : (feedback.data?.length ?? 0) === 0 ? (
                <EmptyState
                  title="No feedback yet"
                  description="Observations recorded here build a timeline the year-end review can draw on."
                />
              ) : (
                <div className="divide-y">
                  {(feedback.data ?? []).map((entry) => (
                    <div key={entry.id} className="space-y-1 p-4">
                      <div className="flex items-center gap-2">
                        <StatusBadge status={humanizeEnum(entry.feedbackType)} />
                        <span className="text-xs text-muted-foreground">
                          {formatDateTime(entry.createdAt)}
                          {entry.createdBy ? ` · ${entry.createdBy}` : ''}
                        </span>
                      </div>
                      <p className="whitespace-pre-wrap text-sm">{entry.comment}</p>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="overview" className="pt-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Plan notes</CardTitle>
            </CardHeader>
            <CardContent>
              {data.overallNotes ? (
                <p className="whitespace-pre-wrap text-sm">{data.overallNotes}</p>
              ) : (
                <p className="text-sm text-muted-foreground">
                  No notes were recorded when this plan was created.
                </p>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Objective add/edit */}
      <Dialog open={objectiveOpen} onOpenChange={setObjectiveOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit objective' : 'New objective'}</DialogTitle>
            <DialogDescription>
              One thing to get better at, with the actions agreed to get there.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="obj-title">Title</Label>
              <Input
                id="obj-title"
                maxLength={300}
                value={objectiveForm.title}
                onChange={(e) => setObjectiveForm((p) => ({ ...p, title: e.target.value }))}
                placeholder="e.g. Lead a project end to end"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="obj-desc">What good looks like</Label>
              <Textarea
                id="obj-desc"
                rows={3}
                maxLength={2000}
                value={objectiveForm.description}
                onChange={(e) => setObjectiveForm((p) => ({ ...p, description: e.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="obj-actions">Actions agreed</Label>
              <Textarea
                id="obj-actions"
                rows={3}
                maxLength={2000}
                value={objectiveForm.actions}
                onChange={(e) => setObjectiveForm((p) => ({ ...p, actions: e.target.value }))}
                placeholder="Training, mentoring, stretch work — whatever was agreed."
              />
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="obj-target">Target date</Label>
                <Input
                  id="obj-target"
                  type="date"
                  value={objectiveForm.targetDate}
                  onChange={(e) => setObjectiveForm((p) => ({ ...p, targetDate: e.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="obj-status">Status</Label>
                <Select
                  value={objectiveForm.objectiveStatus}
                  onValueChange={(v) =>
                    setObjectiveForm((p) => ({
                      ...p,
                      objectiveStatus: v as DevelopmentObjectiveStatus,
                    }))
                  }
                >
                  <SelectTrigger id="obj-status">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {DEVELOPMENT_OBJECTIVE_STATUS_OPTIONS.map((o) => (
                      <SelectItem key={o.value} value={o.value}>
                        {o.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setObjectiveOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => saveObjective.mutate()}
              disabled={!objectiveForm.title.trim() || saveObjective.isPending}
            >
              {saveObjective.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Progress */}
      <Dialog open={!!progressFor} onOpenChange={(open) => !open && setProgressFor(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record progress</DialogTitle>
            <DialogDescription>{progressFor?.title}</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Progress — {progressForm.progressPercent}%</Label>
              <Slider
                value={[progressForm.progressPercent]}
                min={0}
                max={100}
                step={5}
                onValueChange={([v]) => setProgressForm((p) => ({ ...p, progressPercent: v }))}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="prog-status">Status</Label>
              <Select
                value={progressForm.status}
                onValueChange={(v) =>
                  setProgressForm((p) => ({ ...p, status: v as DevelopmentObjectiveStatus }))
                }
              >
                <SelectTrigger id="prog-status">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {DEVELOPMENT_OBJECTIVE_STATUS_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="prog-notes">Notes</Label>
              <Textarea
                id="prog-notes"
                rows={3}
                maxLength={2000}
                value={progressForm.notes}
                onChange={(e) => setProgressForm((p) => ({ ...p, notes: e.target.value }))}
                placeholder="Left blank, the previous note stands."
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setProgressFor(null)}>
              Cancel
            </Button>
            <Button onClick={() => saveProgress.mutate()} disabled={saveProgress.isPending}>
              {saveProgress.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Feedback */}
      <Dialog open={feedbackOpen} onOpenChange={setFeedbackOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add feedback</DialogTitle>
            <DialogDescription>
              Recorded under your name and shown to {data.employeeName}, who is notified.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="fb-type">Type</Label>
              <Select
                value={feedbackForm.feedbackType}
                onValueChange={(v) =>
                  setFeedbackForm((p) => ({ ...p, feedbackType: v as DevelopmentFeedbackType }))
                }
              >
                <SelectTrigger id="fb-type">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {DEVELOPMENT_FEEDBACK_TYPE_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="fb-comment">Comment</Label>
              <Textarea
                id="fb-comment"
                rows={4}
                maxLength={3000}
                value={feedbackForm.comment}
                onChange={(e) => setFeedbackForm((p) => ({ ...p, comment: e.target.value }))}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setFeedbackOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => addFeedback.mutate()}
              disabled={!feedbackForm.comment.trim() || addFeedback.isPending}
            >
              {addFeedback.isPending ? 'Saving…' : 'Add feedback'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
