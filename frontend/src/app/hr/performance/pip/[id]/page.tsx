'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import {
  CalendarPlus,
  Download,
  Flag,
  Info,
  Paperclip,
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { DocumentUploadField } from '@/components/hr/common/DocumentUploadField';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
import { useWorkflowRecord } from '@/hooks/useWorkflowRecord';
import { useToast } from '@/hooks/use-toast';
import { dateOffset, formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { pipMeetingService, pipService } from '@/services/hr/pip.service';
import { GOAL_PROGRESS_STATUS_OPTIONS, type GoalProgressStatus } from '@/types/hr/goals';
import {
  PIP_MANUAL_STATUS_OPTIONS,
  PIP_OUTCOME_OPTIONS,
  type PipOutcome,
  type PipStatus,
} from '@/types/hr/pip';

/**
 * One improvement plan.
 *
 * **Three owners, three stages.** The plan text and its goals belong to whoever is writing it —
 * editable only while it is a draft. Approval belongs to the workflow engine: whoever the
 * published `PerformanceImprovementPlan` definition routes it to, which is why there is no
 * bespoke approve button here. The outcome belongs to HR and closes the record.
 *
 * ⚠ Submitting is refused until the plan has at least one goal — an approver cannot weigh a plan
 * with nothing measurable in it, so the banner says so rather than waiting for the 422.
 *
 * ⚠ `Extended` is not a closure: it needs a new end date and leaves the plan running.
 */
export default function PipDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [goalOpen, setGoalOpen] = useState(false);
  const [editingGoalId, setEditingGoalId] = useState<string | null>(null);
  const [goalForm, setGoalForm] = useState({
    title: '',
    description: '',
    successCriteria: '',
    dueDate: dateOffset(30),
    status: 'NotStarted' as GoalProgressStatus,
    progressPercent: '',
    progressNotes: '',
  });

  const [meetingOpen, setMeetingOpen] = useState(false);
  const [meetingDate, setMeetingDate] = useState(dateOffset(14));

  const [outcomeOpen, setOutcomeOpen] = useState(false);
  const [outcomeForm, setOutcomeForm] = useState({
    outcome: 'PerformanceImproved' as PipOutcome,
    notes: '',
    newEndDate: '',
  });

  const detail = useQuery({
    queryKey: ['hr', 'pip', id],
    queryFn: () => pipService.getDetail(id),
    enabled: !!id,
    retry: false,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'pip', id] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'pips'] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'pip-dashboard'] });
  };

  const fail = (title: string) => (e: Error) =>
    toast({ title, description: e.message, variant: 'destructive' });

  const data = detail.data;
  const isDraft = data?.status === 'Draft';
  const isLive = data?.status === 'Active' || data?.status === 'InProgress';
  const hasGoals = (data?.goals.length ?? 0) > 0;

  /**
   * Submit / approve / reject / recall all come from the engine. This page never sets a status:
   * the service drives the workflow and `PerformanceImprovementPlanWorkflowStatusAdapter` maps
   * the outcome onto the record, so we refetch and let it decide.
   */
  const workflow = useWorkflowRecord({
    entityType: 'PerformanceImprovementPlan',
    entityId: id,
    entityLabel: 'Improvement Plan',
    entityNumber: data?.pipNumber,
    status: data?.status ?? 'Draft',
    canSubmit: isDraft && hasGoals,
    canApproveReject: data?.status === 'PendingApproval',
    enabled: !!data,
    commands: {
      submit: () => pipService.submit(id),
      approve: () => pipService.approve(id),
      reject: (ctx) => pipService.reject(id, ctx.comments || null),
      afterAction: refresh,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  const saveGoal = useMutation({
    mutationFn: () => {
      const payload = {
        title: goalForm.title.trim(),
        description: goalForm.description.trim() || null,
        successCriteria: goalForm.successCriteria.trim() || null,
        dueDate: goalForm.dueDate,
        status: goalForm.status,
        progressPercent: goalForm.progressPercent.trim() ? Number(goalForm.progressPercent) : null,
        progressNotes: goalForm.progressNotes.trim() || null,
      };
      return editingGoalId
        ? pipService.updateGoal(editingGoalId, payload)
        : pipService.addGoal(id, payload);
    },
    onSuccess: () => {
      toast({ title: editingGoalId ? 'Goal updated' : 'Goal added' });
      setGoalOpen(false);
      setEditingGoalId(null);
      refresh();
    },
    onError: fail('Could not save the goal'),
  });

  const removeGoal = useMutation({
    mutationFn: (goalId: string) => pipService.deleteGoal(goalId),
    onSuccess: () => {
      toast({ title: 'Goal removed' });
      refresh();
    },
    onError: fail('Could not remove the goal'),
  });

  const scheduleMeeting = useMutation({
    mutationFn: () => pipMeetingService.schedule(id, new Date(meetingDate).toISOString()),
    onSuccess: (meetingId) => {
      toast({ title: 'Review scheduled', description: 'The employee has been notified.' });
      setMeetingOpen(false);
      refresh();
      router.push(`/hr/performance/pip/${id}/meetings/${meetingId}`);
    },
    onError: fail('Could not schedule the review'),
  });

  const setStatus = useMutation({
    mutationFn: (status: PipStatus) => pipService.updateStatus(id, status),
    onSuccess: () => {
      toast({ title: 'Status updated' });
      refresh();
    },
    onError: fail('Could not change the status'),
  });

  const recordOutcome = useMutation({
    mutationFn: () =>
      pipService.recordOutcome(
        id,
        outcomeForm.outcome,
        outcomeForm.notes.trim() || null,
        outcomeForm.outcome === 'Extended' ? outcomeForm.newEndDate : null,
      ),
    onSuccess: () => {
      toast({
        title: outcomeForm.outcome === 'Extended' ? 'Plan extended' : 'Outcome recorded',
        description:
          outcomeForm.outcome === 'Extended'
            ? 'The plan keeps running to the new end date.'
            : 'The plan is closed and everyone on it has been notified.',
      });
      setOutcomeOpen(false);
      refresh();
    },
    onError: fail('Could not record the outcome'),
  });

  const downloadAttachment = async (attachmentId: string, fileName: string) => {
    try {
      const blob = await pipService.downloadAttachment(attachmentId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = fileName;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      fail('Could not download the file')(e as Error);
    }
  };

  const openNewGoal = () => {
    setEditingGoalId(null);
    setGoalForm({
      title: '',
      description: '',
      successCriteria: '',
      dueDate: dateOffset(30),
      status: 'NotStarted',
      progressPercent: '',
      progressNotes: '',
    });
    setGoalOpen(true);
  };

  if (detail.isLoading) {
    return (
      <div className="space-y-6 p-6">
        <Skeleton className="h-10 w-1/3" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (detail.isError || !data) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="Improvement plan" backHref="/hr/performance/pip" />
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={TriangleAlert}
              title="Could not load this plan"
              description={
                (detail.error as Error)?.message ??
                'Improvement plans are readable only by HR, the employee, the supervisor and the HR owner.'
              }
            />
          </CardContent>
        </Card>
      </div>
    );
  }

  const overdue = isLive && new Date(data.endDate) < new Date();

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${data.pipNumber} — ${data.employeeName}`}
        description={
          data.appraisalCycleName
            ? `Raised from ${data.appraisalCycleName}${
                data.appraisalScore != null ? ` · score ${data.appraisalScore}` : ''
              }`
            : `Supervisor: ${data.supervisorName || '—'}`
        }
        backHref="/hr/performance/pip"
        actions={
          <div className="flex items-center gap-2">
            <WorkflowApprovalActions {...workflow.actionProps} />
            {isLive && (
              <>
                <Button variant="outline" onClick={() => setMeetingOpen(true)}>
                  <CalendarPlus className="mr-2 h-4 w-4" />
                  Schedule review
                </Button>
                <Button onClick={() => setOutcomeOpen(true)}>
                  <Flag className="mr-2 h-4 w-4" />
                  Record outcome
                </Button>
              </>
            )}
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-3">
        <StatusBadge status={humanizeEnum(data.status)} />
        {overdue && <StatusBadge status="Overdue" />}
        <span className="text-sm text-muted-foreground">
          {formatDate(data.startDate)} – {formatDate(data.endDate)}
        </span>
        {data.appraisalId && (
          <Button variant="link" size="sm" className="h-auto p-0" asChild>
            <Link href={`/hr/performance/appraisals/${data.appraisalId}`}>Open the appraisal</Link>
          </Button>
        )}
        {isLive && (
          <Select value={data.status} onValueChange={(v) => setStatus.mutate(v as PipStatus)}>
            <SelectTrigger className="ml-auto w-[160px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {PIP_MANUAL_STATUS_OPTIONS.map((o) => (
                <SelectItem key={o.value} value={o.value}>
                  {o.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
      </div>

      {isDraft && !hasGoals && (
        <Alert>
          <Info className="h-4 w-4" />
          <AlertTitle>Needs at least one goal before it can go for approval</AlertTitle>
          <AlertDescription>
            An approver is being asked to sign off something that will be served on{' '}
            {data.employeeName}. A plan with nothing measurable in it is not something anyone can
            weigh.
          </AlertDescription>
        </Alert>
      )}

      {data.status === 'PendingApproval' && (
        <Alert>
          <Info className="h-4 w-4" />
          <AlertTitle>Out for approval — not yet in force</AlertTitle>
          <AlertDescription>
            Nothing has been served on the employee. Recall it from the workflow to make changes.
          </AlertDescription>
        </Alert>
      )}

      {data.outcome && (
        <Alert>
          <Flag className="h-4 w-4" />
          <AlertTitle>
            Outcome: {humanizeEnum(data.outcome)}
            {data.completionDate ? ` · ${formatDate(data.completionDate)}` : ''}
          </AlertTitle>
          {data.outcomeNotes && <AlertDescription>{data.outcomeNotes}</AlertDescription>}
        </Alert>
      )}

      <Tabs defaultValue="plan">
        <TabsList>
          <TabsTrigger value="plan">The plan</TabsTrigger>
          <TabsTrigger value="goals">Goals ({data.goals.length})</TabsTrigger>
          <TabsTrigger value="meetings">Reviews ({data.reviewMeetings.length})</TabsTrigger>
          <TabsTrigger value="documents">Documents ({data.attachments.length})</TabsTrigger>
          <WorkflowTabTrigger value="workflow" />
        </TabsList>

        <TabsContent value="plan" className="space-y-4 pt-4">
          {[
            { title: 'Performance issues', body: data.performanceIssues },
            { title: 'Expected standards', body: data.expectedStandards },
            { title: 'Improvement actions', body: data.improvementActions },
            { title: 'Support provided', body: data.supportProvided },
            { title: 'Measurement criteria', body: data.measurementCriteria },
            { title: 'Review schedule and notes', body: data.reviewSchedule ?? '' },
          ].map((section) => (
            <Card key={section.title}>
              <CardHeader>
                <CardTitle className="text-base">{section.title}</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="whitespace-pre-wrap text-sm">
                  {section.body || (
                    <span className="text-muted-foreground">Nothing recorded.</span>
                  )}
                </p>
              </CardContent>
            </Card>
          ))}
        </TabsContent>

        <TabsContent value="goals" className="pt-4">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle className="text-base">Improvement goals</CardTitle>
              {isDraft && (
                <Button size="sm" onClick={openNewGoal}>
                  <Plus className="mr-2 h-4 w-4" />
                  Add goal
                </Button>
              )}
            </CardHeader>
            <CardContent className="p-0">
              {data.goals.length === 0 ? (
                <EmptyState
                  icon={Target}
                  title="No goals yet"
                  description="Each goal is one measurable thing that has to change, with a date and a way of telling."
                  action={
                    isDraft ? (
                      <Button size="sm" onClick={openNewGoal}>
                        Add the first goal
                      </Button>
                    ) : undefined
                  }
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Goal</TableHead>
                      <TableHead>Due</TableHead>
                      <TableHead className="w-40">Progress</TableHead>
                      <TableHead>Status</TableHead>
                      {isDraft && <TableHead className="w-16" />}
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {data.goals.map((goal) => (
                      <TableRow key={goal.goalId}>
                        <TableCell>
                          <div className="font-medium">{goal.title}</div>
                          {goal.successCriteria && (
                            <div className="max-w-md text-xs text-muted-foreground">
                              {goal.successCriteria}
                            </div>
                          )}
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {formatDate(goal.dueDate)}
                        </TableCell>
                        <TableCell>
                          <div className="flex items-center gap-2">
                            <Progress value={Number(goal.progressPercent) || 0} className="h-2" />
                            <span className="w-10 text-right text-xs tabular-nums text-muted-foreground">
                              {Math.round(Number(goal.progressPercent) || 0)}%
                            </span>
                          </div>
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={humanizeEnum(goal.status)} />
                        </TableCell>
                        {isDraft && (
                          <TableCell className="text-right">
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() => removeGoal.mutate(goal.goalId)}
                              disabled={removeGoal.isPending}
                              aria-label="Remove goal"
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </TableCell>
                        )}
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
          {!isDraft && (
            <p className="pt-2 text-xs text-muted-foreground">
              Goal progress is recorded in a review meeting, so that what changed and the
              conversation that agreed it stay together.
            </p>
          )}
        </TabsContent>

        <TabsContent value="meetings" className="pt-4">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle className="text-base">Review meetings</CardTitle>
              {isLive && (
                <Button size="sm" onClick={() => setMeetingOpen(true)}>
                  <CalendarPlus className="mr-2 h-4 w-4" />
                  Schedule
                </Button>
              )}
            </CardHeader>
            <CardContent className="p-0">
              {data.reviewMeetings.length === 0 ? (
                <EmptyState
                  title="No reviews yet"
                  description={
                    isLive
                      ? 'Schedule the first review so progress is recorded against the plan.'
                      : 'Reviews can be held once the plan is in force.'
                  }
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Date</TableHead>
                      <TableHead>Conducted by</TableHead>
                      <TableHead>Attended</TableHead>
                      <TableHead>Notes</TableHead>
                      <TableHead className="w-24" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {data.reviewMeetings.map((meeting) => (
                      <TableRow key={meeting.meetingId}>
                        <TableCell className="text-sm">
                          {formatDate(meeting.meetingDate)}
                        </TableCell>
                        <TableCell className="text-sm">{meeting.conductedByName || '—'}</TableCell>
                        <TableCell>
                          <StatusBadge
                            status={meeting.employeeAttended ? 'Attended' : 'Absent'}
                          />
                        </TableCell>
                        <TableCell className="max-w-sm text-xs text-muted-foreground">
                          {meeting.progressNotesPreview || '—'}
                        </TableCell>
                        <TableCell className="text-right">
                          <Button variant="ghost" size="sm" asChild>
                            <Link
                              href={`/hr/performance/pip/${id}/meetings/${meeting.meetingId}`}
                            >
                              Open
                            </Link>
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="documents" className="space-y-4 pt-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Evidence and correspondence</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <DocumentUploadField
                label="Attach a document"
                endpoint={`/Pip/${id}/attachments`}
                helpText="Stored privately: these files are served only through an endpoint that checks who is asking."
                onUploaded={() => {
                  toast({ title: 'Document attached' });
                  refresh();
                }}
              />

              {data.attachments.length === 0 ? (
                <EmptyState
                  icon={Paperclip}
                  title="Nothing attached"
                  description="Meeting records, written warnings and evidence of progress belong here."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>File</TableHead>
                      <TableHead>Uploaded</TableHead>
                      <TableHead>By</TableHead>
                      <TableHead className="w-28" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {data.attachments.map((file) => (
                      <TableRow key={file.attachmentId}>
                        <TableCell>
                          <div className="font-medium">{file.fileName}</div>
                          {file.description && (
                            <div className="text-xs text-muted-foreground">{file.description}</div>
                          )}
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {formatDate(file.uploadDate)}
                        </TableCell>
                        <TableCell className="text-sm">{file.uploadedByName || '—'}</TableCell>
                        <TableCell className="text-right">
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => downloadAttachment(file.attachmentId, file.fileName)}
                          >
                            <Download className="mr-2 h-4 w-4" />
                            Get
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <WorkflowTabContent
          value="workflow"
          entityType="PerformanceImprovementPlan"
          entityId={id}
          entityLabel="Improvement Plan"
          entityNumber={data.pipNumber}
          status={data.status}
          onAfterAction={async () => {
            await refresh();
            await workflow.refresh();
          }}
        />
      </Tabs>

      {/* Goal */}
      <Dialog open={goalOpen} onOpenChange={setGoalOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editingGoalId ? 'Edit goal' : 'New improvement goal'}</DialogTitle>
            <DialogDescription>
              One measurable change, with a date and a way of telling whether it happened.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="pg-title">Goal</Label>
              <Input
                id="pg-title"
                maxLength={300}
                value={goalForm.title}
                onChange={(e) => setGoalForm((p) => ({ ...p, title: e.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="pg-criteria">Success criteria</Label>
              <Textarea
                id="pg-criteria"
                rows={3}
                maxLength={1000}
                value={goalForm.successCriteria}
                onChange={(e) => setGoalForm((p) => ({ ...p, successCriteria: e.target.value }))}
                placeholder="What has to be true for this to count as met."
              />
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="pg-due">Due</Label>
                <Input
                  id="pg-due"
                  type="date"
                  value={goalForm.dueDate}
                  onChange={(e) => setGoalForm((p) => ({ ...p, dueDate: e.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="pg-status">Status</Label>
                <Select
                  value={goalForm.status}
                  onValueChange={(v) =>
                    setGoalForm((p) => ({ ...p, status: v as GoalProgressStatus }))
                  }
                >
                  <SelectTrigger id="pg-status">
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
          <DialogFooter>
            <Button variant="outline" onClick={() => setGoalOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => saveGoal.mutate()}
              disabled={!goalForm.title.trim() || saveGoal.isPending}
            >
              {saveGoal.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Schedule review */}
      <Dialog open={meetingOpen} onOpenChange={setMeetingOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Schedule a review</DialogTitle>
            <DialogDescription>
              You are recorded as holding it, and {data.employeeName} is notified. The form opens
              next so you can fill it in during or after the meeting.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="pm-date">Date</Label>
            <Input
              id="pm-date"
              type="date"
              value={meetingDate}
              onChange={(e) => setMeetingDate(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setMeetingOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => scheduleMeeting.mutate()} disabled={scheduleMeeting.isPending}>
              {scheduleMeeting.isPending ? 'Scheduling…' : 'Schedule'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Outcome */}
      <Dialog open={outcomeOpen} onOpenChange={setOutcomeOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record the outcome</DialogTitle>
            <DialogDescription>
              {PIP_OUTCOME_OPTIONS.find((o) => o.value === outcomeForm.outcome)?.description}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="po-outcome">Outcome</Label>
              <Select
                value={outcomeForm.outcome}
                onValueChange={(v) =>
                  setOutcomeForm((p) => ({
                    ...p,
                    outcome: v as PipOutcome,
                    newEndDate: v === 'Extended' && !p.newEndDate ? dateOffset(30) : p.newEndDate,
                  }))
                }
              >
                <SelectTrigger id="po-outcome">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {PIP_OUTCOME_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {outcomeForm.outcome === 'Extended' && (
              <div className="space-y-2">
                <Label htmlFor="po-end">New end date</Label>
                <Input
                  id="po-end"
                  type="date"
                  value={outcomeForm.newEndDate}
                  onChange={(e) => setOutcomeForm((p) => ({ ...p, newEndDate: e.target.value }))}
                />
                <p className="text-xs text-muted-foreground">
                  Must be later than {formatDate(data.endDate)}. The plan stays open.
                </p>
              </div>
            )}

            <div className="space-y-2">
              <Label htmlFor="po-notes">Notes</Label>
              <Textarea
                id="po-notes"
                rows={4}
                maxLength={2000}
                value={outcomeForm.notes}
                onChange={(e) => setOutcomeForm((p) => ({ ...p, notes: e.target.value }))}
                placeholder="What was achieved, what was not, and what happens next."
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOutcomeOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => recordOutcome.mutate()}
              disabled={
                recordOutcome.isPending ||
                (outcomeForm.outcome === 'Extended' && !outcomeForm.newEndDate)
              }
            >
              {recordOutcome.isPending ? 'Saving…' : 'Record'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
