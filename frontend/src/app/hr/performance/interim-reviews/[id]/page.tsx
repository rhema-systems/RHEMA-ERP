'use client';

import { useEffect, useMemo, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'next/navigation';
import { Download, Paperclip, Plus, Target, Trash2, Upload } from 'lucide-react';
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
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { InterimAppraisalScoreForm } from '@/components/hr/performance/InterimAppraisalScoreForm';
import { useToast } from '@/hooks/use-toast';
import { formatDate, formatDateTime, humanizeEnum } from '@/lib/hr/attendance-format';
import { interimReviewService } from '@/services/hr/interim-reviews.service';
import { employeeGoalService } from '@/services/hr/goals.service';
import { GOAL_PROGRESS_STATUS_OPTIONS, type GoalProgressStatus } from '@/types/hr/goals';
import type { FinalizeFullInterimAppraisal } from '@/types/hr/interim-reviews';

/**
 * One interim review: what the employee submitted, what each goal did over the period, the
 * evidence, and how the manager closed it.
 *
 * **Two shapes, one screen.** A light-touch event runs submit → complete. A full appraisal
 * (`isFullAppraisal`, set from the cycle's `interimReviewDepth`) runs submit → finalize, which
 * scores the period's goals and produces a weighted `overallPeriodScore`. The tabs adapt rather
 * than the route splitting in two, because everything else about the two is identical.
 *
 * ⚠ **Recording progress here moves the goal**, exactly as the goal screen and check-ins do —
 * percent and execution status are applied to the `EmployeeGoal`, so a goal flagged at risk at a
 * mid-year review turns up in the at-risk reports without re-entry. A goal that is not live
 * (draft, awaiting approval, locked, complete) keeps its status and only the entry is kept.
 *
 * ⚠ **Submit and complete can be refused with 422**, and the message is the point: the cycle's
 * settings may require a self-assessment before the manager can close, and may require a progress
 * update on *every* live goal before either side can move. The message names how many are missing.
 */
export default function InterimReviewDetailPage() {
  const params = useParams<{ id: string }>();
  const eventId = params.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [tab, setTab] = useState('overview');
  const [progressOpen, setProgressOpen] = useState(false);
  const [uploadOpen, setUploadOpen] = useState(false);
  const [selfForm, setSelfForm] = useState({ achievementsSummary: '', challengesSummary: '' });
  const [closeForm, setCloseForm] = useState({ notes: '', managerNotes: '' });
  const [progressForm, setProgressForm] = useState({
    employeeGoalId: '',
    progressPercent: '',
    actualValue: '',
    status: 'InProgress' as GoalProgressStatus,
    challenges: '',
    notes: '',
  });
  const [uploadFile, setUploadFile] = useState<File | null>(null);
  const [uploadDescription, setUploadDescription] = useState('');
  const fileInputRef = useRef<HTMLInputElement>(null);

  const { data: event, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'interim-review', eventId],
    queryFn: () => interimReviewService.getById(eventId),
    enabled: !!eventId,
    retry: false,
  });

  const { data: progressEntries } = useQuery({
    queryKey: ['hr', 'interim-review-progress', eventId],
    queryFn: () => interimReviewService.getProgressEntries(eventId),
    enabled: !!eventId,
  });

  const { data: attachments } = useQuery({
    queryKey: ['hr', 'interim-review-attachments', eventId],
    queryFn: () => interimReviewService.getAttachments(eventId),
    enabled: !!eventId,
  });

  // Only a full appraisal has a scoring context; asking for one on a light-touch event is a
  // pointless round trip.
  const { data: fullContext } = useQuery({
    queryKey: ['hr', 'interim-review-context', eventId],
    queryFn: () => interimReviewService.getFullAppraisalContext(eventId),
    enabled: !!eventId && !!event?.isFullAppraisal,
  });

  // The goals available to log progress against are the appraisee's, for this review's cycle —
  // the server refuses anything else.
  const { data: goals } = useQuery({
    queryKey: ['hr', 'employee-goals', event?.employeeId, event?.appraisalCycleId],
    queryFn: () =>
      employeeGoalService.getByEmployee(event?.employeeId ?? '', event?.appraisalCycleId ?? undefined),
    enabled: !!event?.employeeId && !!event?.appraisalCycleId,
  });

  useEffect(() => {
    if (!event) return;
    setSelfForm({
      achievementsSummary: event.achievementsSummary ?? '',
      challengesSummary: event.challengesSummary ?? '',
    });
    setCloseForm({ notes: event.notes ?? '', managerNotes: event.managerNotes ?? '' });
  }, [event]);

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['hr', 'interim-review', eventId] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'interim-review-progress', eventId] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'interim-review-context', eventId] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'interim-reviews'] });
    // Progress and finalize both move the goals themselves, so anything reading them is stale.
    queryClient.invalidateQueries({ queryKey: ['hr', 'employee-goals'] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'goals-at-risk'] });
  };

  /** 422 carries the rule's own message — showing anything else throws away the useful part. */
  const failed = (title: string) => (err: unknown) =>
    toast({
      variant: 'destructive',
      title,
      description: (err as Error)?.message ?? 'Please try again.',
    });

  const submit = useMutation({
    mutationFn: () =>
      interimReviewService.submit(eventId, {
        achievementsSummary: selfForm.achievementsSummary.trim() || null,
        challengesSummary: selfForm.challengesSummary.trim() || null,
      }),
    onSuccess: () => {
      refresh();
      toast({ title: 'Self-assessment submitted' });
    },
    onError: failed('Could not submit'),
  });

  const complete = useMutation({
    mutationFn: () =>
      interimReviewService.complete(eventId, {
        notes: closeForm.notes.trim() || null,
        managerNotes: closeForm.managerNotes.trim() || null,
      }),
    onSuccess: () => {
      refresh();
      toast({ title: 'Review closed' });
    },
    onError: failed('Could not close the review'),
  });

  const finalize = useMutation({
    mutationFn: (payload: FinalizeFullInterimAppraisal) =>
      interimReviewService.finalizeFullAppraisal(eventId, payload),
    onSuccess: (result) => {
      refresh();
      toast({
        title: 'Interim appraisal finalized',
        description:
          result.overallPeriodScore != null
            ? `Period score ${result.overallPeriodScore.toFixed(2)}.`
            : undefined,
      });
    },
    onError: failed('Could not finalize'),
  });

  const recordProgress = useMutation({
    mutationFn: () =>
      interimReviewService.recordProgressEntry(eventId, {
        employeeGoalId: progressForm.employeeGoalId,
        progressPercent:
          progressForm.progressPercent.trim() === '' ? null : Number(progressForm.progressPercent),
        actualValue:
          progressForm.actualValue.trim() === '' ? null : Number(progressForm.actualValue),
        status: progressForm.status,
        challenges: progressForm.challenges.trim() || null,
        notes: progressForm.notes.trim() || null,
      }),
    onSuccess: () => {
      refresh();
      setProgressOpen(false);
      setProgressForm({
        employeeGoalId: '',
        progressPercent: '',
        actualValue: '',
        status: 'InProgress',
        challenges: '',
        notes: '',
      });
      toast({ title: 'Progress recorded', description: 'The goal has been updated to match.' });
    },
    onError: failed('Could not record progress'),
  });

  const upload = useMutation({
    mutationFn: () => {
      if (!uploadFile) throw new Error('Choose a file first.');
      return interimReviewService.uploadAttachment(
        eventId,
        uploadFile,
        uploadDescription.trim() || null,
      );
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'interim-review-attachments', eventId] });
      setUploadOpen(false);
      setUploadFile(null);
      setUploadDescription('');
      if (fileInputRef.current) fileInputRef.current.value = '';
      toast({ title: 'Evidence attached' });
    },
    // The upload gate answers `{ code, message }`; the message is what the user can act on.
    onError: failed('Upload refused'),
  });

  const removeAttachment = useMutation({
    mutationFn: (attachmentId: string) =>
      interimReviewService.deleteAttachment(eventId, attachmentId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'interim-review-attachments', eventId] });
      toast({ title: 'Attachment removed' });
    },
    onError: failed('Could not remove the attachment'),
  });

  const goalsWithProgress = useMemo(
    () => new Set((progressEntries ?? []).map((p) => p.employeeGoalId)),
    [progressEntries],
  );

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-12 w-80" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (isError || !event) {
    return (
      <div className="space-y-6">
        <PageHeader title="Interim review" backHref="/hr/performance/interim-reviews" />
        <EmptyState
          title="Could not load this review"
          description={
            (error as Error)?.message ??
            'It may not exist, or it may belong to someone outside your team.'
          }
        />
      </div>
    );
  }

  const isClosed = event.status === 'Completed';
  const awaitingManager = event.status === 'EmployeeSubmitted';

  return (
    <div className="space-y-6">
      <PageHeader
        title={`${humanizeEnum(event.type)}${event.employeeName ? ` — ${event.employeeName}` : ''}`}
        description={`${event.cycleCode ?? 'Cycle'} · ${formatDate(event.eventDate)} · ${
          event.isFullAppraisal ? 'Full appraisal' : 'Light touch'
        }`}
        backHref="/hr/performance/interim-reviews"
        actions={<StatusBadge status={event.status} />}
      />

      {event.isFullAppraisal && event.overallPeriodScore != null && (
        <Card>
          <CardContent className="flex items-center justify-between py-4">
            <div>
              <p className="text-sm text-muted-foreground">Period score</p>
              <p className="text-3xl font-semibold tabular-nums">
                {event.overallPeriodScore.toFixed(2)}
              </p>
            </div>
            <p className="max-w-md text-sm text-muted-foreground">
              A weighted mean of the goals scored at this review — weights are relative to those
              goals, not to 100.
            </p>
          </CardContent>
        </Card>
      )}

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="progress">Goal progress</TabsTrigger>
          {event.isFullAppraisal && <TabsTrigger value="scoring">Scoring</TabsTrigger>}
          <TabsTrigger value="evidence">Evidence</TabsTrigger>
        </TabsList>

        {/* ── Overview: the two sides of the review ─────────────────────────── */}
        <TabsContent value="overview" className="space-y-4 pt-4">
          <Card>
            <CardHeader>
              <CardTitle>Employee self-assessment</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="achievements">What went well</Label>
                <Textarea
                  id="achievements"
                  rows={4}
                  disabled={isClosed || event.status === 'EmployeeSubmitted'}
                  value={selfForm.achievementsSummary}
                  onChange={(e) =>
                    setSelfForm((p) => ({ ...p, achievementsSummary: e.target.value }))
                  }
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="challenges">What got in the way</Label>
                <Textarea
                  id="challenges"
                  rows={4}
                  disabled={isClosed || event.status === 'EmployeeSubmitted'}
                  value={selfForm.challengesSummary}
                  onChange={(e) => setSelfForm((p) => ({ ...p, challengesSummary: e.target.value }))}
                />
              </div>
              {event.status === 'Pending' && (
                <div className="flex justify-end">
                  <Button onClick={() => submit.mutate()} disabled={submit.isPending}>
                    {submit.isPending ? 'Submitting…' : 'Submit self-assessment'}
                  </Button>
                </div>
              )}
              {awaitingManager && (
                <Alert>
                  <AlertTitle>Submitted</AlertTitle>
                  <AlertDescription>
                    Waiting for the manager to close this review.
                  </AlertDescription>
                </Alert>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Manager close-out</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="notes">Review notes</Label>
                <Textarea
                  id="notes"
                  rows={3}
                  disabled={isClosed}
                  value={closeForm.notes}
                  onChange={(e) => setCloseForm((p) => ({ ...p, notes: e.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="managerNotes">Manager notes</Label>
                <Textarea
                  id="managerNotes"
                  rows={3}
                  disabled={isClosed}
                  value={closeForm.managerNotes}
                  onChange={(e) => setCloseForm((p) => ({ ...p, managerNotes: e.target.value }))}
                />
              </div>
              {!isClosed && !event.isFullAppraisal && (
                <div className="flex justify-end">
                  <Button onClick={() => complete.mutate()} disabled={complete.isPending}>
                    {complete.isPending ? 'Closing…' : 'Close review'}
                  </Button>
                </div>
              )}
              {!isClosed && event.isFullAppraisal && (
                <Alert>
                  <AlertTitle>This is a full interim appraisal</AlertTitle>
                  <AlertDescription>
                    It closes from the Scoring tab, which records a score per goal and computes the
                    period score.
                  </AlertDescription>
                </Alert>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Goal progress ─────────────────────────────────────────────────── */}
        <TabsContent value="progress" className="space-y-4 pt-4">
          <div className="flex items-center justify-between">
            <p className="text-sm text-muted-foreground">
              Recording progress here updates the goal itself, not just this review.
            </p>
            <Button size="sm" onClick={() => setProgressOpen(true)} disabled={isClosed}>
              <Plus className="mr-2 h-4 w-4" />
              Record progress
            </Button>
          </div>

          {goals && goals.length > 0 && (
            <Card>
              <CardContent className="py-4">
                <p className="text-sm">
                  <span className="font-medium">
                    {goals.filter((g) => goalsWithProgress.has(g.id)).length} of {goals.length}
                  </span>{' '}
                  goals have an update at this review. If the cycle requires an update on every
                  live goal, submitting or closing is refused until they all do.
                </p>
              </CardContent>
            </Card>
          )}

          <Card>
            <CardContent className="p-0">
              {(progressEntries ?? []).length === 0 ? (
                <EmptyState
                  icon={Target}
                  title="No progress recorded yet"
                  description="Log where each goal stands at this checkpoint."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Goal</TableHead>
                      <TableHead className="w-28">Progress</TableHead>
                      <TableHead className="w-32">Status</TableHead>
                      <TableHead>Notes</TableHead>
                      <TableHead className="w-40">Recorded by</TableHead>
                      <TableHead className="w-40">When</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(progressEntries ?? []).map((entry) => (
                      <TableRow key={entry.id}>
                        <TableCell className="font-medium">{entry.goalTitle}</TableCell>
                        <TableCell className="tabular-nums">
                          {entry.progressPercent != null ? `${entry.progressPercent}%` : '—'}
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={entry.status} />
                        </TableCell>
                        <TableCell className="text-muted-foreground">
                          {entry.notes ?? entry.challenges ?? '—'}
                        </TableCell>
                        <TableCell>{entry.recordedByName || '—'}</TableCell>
                        <TableCell>{formatDateTime(entry.entryDate)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Scoring (full appraisal only) ─────────────────────────────────── */}
        {event.isFullAppraisal && (
          <TabsContent value="scoring" className="pt-4">
            {fullContext ? (
              <InterimAppraisalScoreForm
                goals={fullContext.goals}
                readOnly={isClosed}
                submitting={finalize.isPending}
                onSubmit={(payload) => finalize.mutate(payload)}
              />
            ) : (
              <Skeleton className="h-64 w-full" />
            )}
          </TabsContent>
        )}

        {/* ── Evidence ──────────────────────────────────────────────────────── */}
        <TabsContent value="evidence" className="space-y-4 pt-4">
          <div className="flex items-center justify-between">
            <p className="text-sm text-muted-foreground">
              Files are scanned and stored outside the web root — they download through an
              authorizing endpoint, never a link.
            </p>
            <Button size="sm" onClick={() => setUploadOpen(true)}>
              <Upload className="mr-2 h-4 w-4" />
              Attach evidence
            </Button>
          </div>

          <Card>
            <CardContent className="p-0">
              {(attachments ?? []).length === 0 ? (
                <EmptyState
                  icon={Paperclip}
                  title="No evidence attached"
                  description="Attach anything that backs up the period's progress."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>File</TableHead>
                      <TableHead>Description</TableHead>
                      <TableHead className="w-40">Uploaded by</TableHead>
                      <TableHead className="w-40">When</TableHead>
                      <TableHead className="w-28" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(attachments ?? []).map((a) => (
                      <TableRow key={a.id}>
                        <TableCell className="font-medium">{a.fileName}</TableCell>
                        <TableCell className="text-muted-foreground">
                          {a.description ?? '—'}
                        </TableCell>
                        <TableCell>{a.uploadedByName || '—'}</TableCell>
                        <TableCell>{formatDateTime(a.uploadDate)}</TableCell>
                        <TableCell>
                          <div className="flex items-center gap-1">
                            <Button
                              variant="ghost"
                              size="icon"
                              aria-label={`Download ${a.fileName}`}
                              onClick={() => interimReviewService.downloadAttachment(eventId, a)}
                            >
                              <Download className="h-4 w-4" />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon"
                              aria-label={`Remove ${a.fileName}`}
                              onClick={() => removeAttachment.mutate(a.id)}
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
      </Tabs>

      {/* ── Record progress ──────────────────────────────────────────────────── */}
      <Dialog open={progressOpen} onOpenChange={setProgressOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record goal progress</DialogTitle>
            <DialogDescription>
              This updates the goal itself — its percentage and execution status — as well as
              logging the entry against this review.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="goal">Goal</Label>
              <Select
                value={progressForm.employeeGoalId}
                onValueChange={(v) => setProgressForm((p) => ({ ...p, employeeGoalId: v }))}
              >
                <SelectTrigger id="goal">
                  <SelectValue placeholder="Select a goal…" />
                </SelectTrigger>
                <SelectContent>
                  {(goals ?? []).map((g) => (
                    <SelectItem key={g.id} value={g.id}>
                      {g.title}
                      {goalsWithProgress.has(g.id) ? ' — already updated' : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="progressPercent">Progress %</Label>
                <Input
                  id="progressPercent"
                  type="number"
                  min={0}
                  max={100}
                  value={progressForm.progressPercent}
                  onChange={(e) =>
                    setProgressForm((p) => ({ ...p, progressPercent: e.target.value }))
                  }
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="status">Status</Label>
                <Select
                  value={progressForm.status}
                  onValueChange={(v) =>
                    setProgressForm((p) => ({ ...p, status: v as GoalProgressStatus }))
                  }
                >
                  <SelectTrigger id="status">
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
            <div className="space-y-2">
              <Label htmlFor="progressNotes">Notes</Label>
              <Textarea
                id="progressNotes"
                rows={3}
                value={progressForm.notes}
                onChange={(e) => setProgressForm((p) => ({ ...p, notes: e.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="progressChallenges">Challenges</Label>
              <Input
                id="progressChallenges"
                value={progressForm.challenges}
                onChange={(e) => setProgressForm((p) => ({ ...p, challenges: e.target.value }))}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setProgressOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => recordProgress.mutate()}
              disabled={!progressForm.employeeGoalId || recordProgress.isPending}
            >
              {recordProgress.isPending ? 'Recording…' : 'Record'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Attach evidence ──────────────────────────────────────────────────── */}
      <Dialog open={uploadOpen} onOpenChange={setUploadOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Attach evidence</DialogTitle>
            <DialogDescription>
              The file is scanned before it is stored. A refusal comes back with a reason.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="file">File</Label>
              <Input
                id="file"
                type="file"
                ref={fileInputRef}
                onChange={(e) => setUploadFile(e.target.files?.[0] ?? null)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="uploadDescription">Description</Label>
              <Input
                id="uploadDescription"
                value={uploadDescription}
                onChange={(e) => setUploadDescription(e.target.value)}
                placeholder="Optional"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setUploadOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => upload.mutate()} disabled={!uploadFile || upload.isPending}>
              {upload.isPending ? 'Uploading…' : 'Upload'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
