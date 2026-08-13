'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Loader2,
  Lock,
  CheckCircle2,
  FileText,
  ExternalLink,
  CalendarClock,
  Users,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useAuth } from '@/hooks/use-auth';
import { learningPathService } from '@/services/hr/learning-path.service';
import { LEARNING_PATH_STEP_STATUS } from '@/types/hr/learning-paths';
import { MATERIAL_TYPE_OPTIONS, TRAINING_LEVEL_OPTIONS } from '@/types/hr/training';
import { NOMINATION_STATUS_OPTIONS } from '@/types/hr/training-delivery';

const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const materialLabel = (v: string) => MATERIAL_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;
const levelLabel = (v: string) => TRAINING_LEVEL_OPTIONS.find((o) => o.value === v)?.label ?? v;
const nomLabel = (v: string) => NOMINATION_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;

/**
 * One step of a learning path, from the learner's side.
 *
 * Built on the server's purpose-built read model, which already resolves whether the step is locked
 * and whether it can be marked complete — `canMarkComplete` is true only when there is attendance or
 * a completion record to justify it, so completion stays evidenced rather than self-asserted.
 */
export default function StepDetailPage() {
  const router = useRouter();
  const params = useParams();
  const enrollmentId = (params?.id as string) ?? '';
  const stepId = (params?.stepId as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasRole } = useAuth();
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [reason, setReason] = useState('');

  // HR may complete a step nothing evidences; everyone else may only confirm what the record supports.
  const isHr = hasRole('HR') || hasRole('SuperAdmin') || hasRole('TenantAdmin');

  const queryKey = ['hr', 'training', 'learning-paths', 'steps', stepId];
  const { data: step, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () => learningPathService.getStepDetail(stepId),
    enabled: !!stepId,
  });

  const historyKey = ['hr', 'training', 'learning-paths', 'steps', stepId, 'history'];
  const { data: history } = useQuery({
    queryKey: historyKey,
    queryFn: () => learningPathService.getStepHistory(stepId),
    enabled: !!stepId,
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !step) {
    return (
      <div className="p-6">
        <EmptyState title="Step not found" description="It may have been removed." />
      </div>
    );
  }

  // An HR completion with nothing behind it is an override, and the server requires a reason for it.
  const isOverride = !step.canMarkComplete && isHr;

  // Returns false on failure so the dialog stays open with the error toast visible.
  const markComplete = async (): Promise<boolean> => {
    if (isOverride && !reason.trim()) {
      toast({
        title: 'A reason is required',
        description: 'Nothing on the record evidences this step, so say why you are recording it.',
        variant: 'destructive',
      });
      return false;
    }

    setBusy(true);
    try {
      await learningPathService.updateStep(stepId, {
        isCompleted: true,
        completedDate: new Date().toISOString(),
        nominationId: step.myNomination?.id ?? null,
        reason: isOverride ? reason.trim() : null,
      });
      const updated = await learningPathService.recalculateProgress(enrollmentId);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey }),
        queryClient.invalidateQueries({ queryKey: historyKey }),
        queryClient.invalidateQueries({
          queryKey: ['hr', 'training', 'learning-paths', 'enrollments'],
        }),
      ]);
      setReason('');
      toast({
        title: 'Step completed',
        description: `Path progress is now ${updated.progressPercentage}%.`,
      });
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to mark the step complete.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={step.programName}
        description={`${step.learningPathName} · step ${step.stepSequence} of ${step.totalSteps}${
          step.isOwnStep ? '' : ` · ${step.learnerName}`
        }`}
        backHref={`/hr/training/my-learning/${enrollmentId}`}
        actions={
          <div className="flex items-center gap-2">
            {!step.isMandatory && <Badge variant="outline">Optional</Badge>}
            {step.isCompleted ? (
              <Badge variant="default">
                <CheckCircle2 className="mr-1 h-3 w-3" /> Completed {fmt(step.completedDate)}
              </Badge>
            ) : (
              <Button
                size="sm"
                variant={isOverride ? 'outline' : 'default'}
                disabled={step.isLocked || (!step.canMarkComplete && !isHr)}
                onClick={() => setConfirmOpen(true)}
              >
                <CheckCircle2 className="mr-2 h-4 w-4" />
                {isOverride ? 'Record completion' : 'Mark complete'}
              </Button>
            )}
          </div>
        }
      />

      {step.isLocked && (
        <Alert>
          <Lock className="h-4 w-4" />
          <AlertTitle>This step is locked</AlertTitle>
          <AlertDescription>
            Finish &quot;{step.prerequisiteProgramName}&quot; first — the path is sequenced.
          </AlertDescription>
        </Alert>
      )}

      {!step.isLocked && !step.isCompleted && !step.canMarkComplete && (
        <Alert>
          <CalendarClock className="h-4 w-4" />
          <AlertTitle>Nothing to evidence this yet</AlertTitle>
          <AlertDescription>
            {isHr ? (
              <>
                No attendance or completion record sits behind this step. You can still record it —
                prior learning and self-paced study are ordinary reasons — but the reason you give is
                kept on the record alongside your name.
              </>
            ) : (
              <>
                {step.isOwnStep
                  ? 'Attend a scheduled run'
                  : 'The learner must attend a scheduled run'}
                , or have a completion recorded against the nomination. Completion is evidenced rather
                than self-declared, so the button stays disabled until then.
              </>
            )}
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader>
          <CardTitle>About this programme</CardTitle>
          <CardDescription>
            {step.programCode} · {step.durationDays}d / {step.durationHours}h ·{' '}
            {levelLabel(step.level)}
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          <div className="sm:col-span-2">
            <p className="text-sm text-muted-foreground">Description</p>
            <p className="font-medium">{step.description || '—'}</p>
          </div>
          {step.learningObjectives && (
            <div className="sm:col-span-2">
              <p className="text-sm text-muted-foreground">What you should come away with</p>
              <p className="font-medium">{step.learningObjectives}</p>
            </div>
          )}
          {step.prerequisites && (
            <div className="sm:col-span-2">
              <p className="text-sm text-muted-foreground">Assumed knowledge</p>
              <p className="font-medium">{step.prerequisites}</p>
            </div>
          )}
          {step.providesCertificate && (
            <div className="sm:col-span-2">
              <Badge variant="secondary">
                Awards: {step.certificateName || 'a certificate'}
              </Badge>
            </div>
          )}
        </CardContent>
      </Card>

      {step.materials.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Materials</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {step.materials.map((m) => (
              <div key={m.id} className="flex items-center justify-between rounded-md border p-3">
                <div className="flex items-center gap-2">
                  <FileText className="h-4 w-4 text-muted-foreground" />
                  <span className="font-medium">{m.materialName}</span>
                  <Badge variant="outline" className="text-[10px]">
                    {materialLabel(m.type)}
                  </Badge>
                </div>
                {m.externalUrl && (
                  <a
                    href={m.externalUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="inline-flex items-center gap-1 text-sm text-primary hover:underline"
                  >
                    Open <ExternalLink className="h-3.5 w-3.5" />
                  </a>
                )}
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle>
            {step.isOwnStep ? 'Your place on this step' : `${step.learnerName} on this step`}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {step.myNomination ? (
            <div className="rounded-md border p-3">
              <div className="flex items-center justify-between">
                <span className="font-medium">{step.myNomination.scheduleNumber}</span>
                <StatusBadge status={nomLabel(step.myNomination.status)} />
              </div>
              <p className="mt-1 text-sm text-muted-foreground">
                {fmt(step.myNomination.trainingStartDate)} – {fmt(step.myNomination.trainingEndDate)}
                {' · '}
                {step.myNomination.nominationNumber}
              </p>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">
              {step.isOwnStep
                ? 'You have not been nominated onto a run of this programme yet.'
                : 'Not yet nominated onto a run of this programme.'}
            </p>
          )}

          {step.myAttendance.length > 0 && (
            <div>
              <p className="mb-2 text-sm font-medium">Attendance</p>
              <div className="space-y-1">
                {step.myAttendance.map((a) => (
                  <div key={a.id} className="flex items-center gap-2 text-sm">
                    <span className="text-muted-foreground">{fmt(a.attendanceDate)}</span>
                    <Badge variant={a.isPresent ? 'default' : 'destructive'} className="text-[10px]">
                      {a.isPresent ? 'Present' : 'Absent'}
                    </Badge>
                    {a.absenceReason && (
                      <span className="text-xs text-muted-foreground">{a.absenceReason}</span>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}

          {step.myCompletion && (
            <div className="rounded-md border p-3">
              <p className="text-sm font-medium">Completion recorded</p>
              <p className="mt-1 text-sm text-muted-foreground">
                {fmt(step.myCompletion.completionDate)}
                {typeof step.myCompletion.finalScore === 'number'
                  ? ` · score ${step.myCompletion.finalScore}`
                  : ''}
              </p>
              <div className="mt-2 flex items-center gap-2">
                <Badge variant={step.myCompletion.isPassed ? 'default' : 'destructive'}>
                  {step.myCompletion.isPassed ? 'Passed' : 'Not passed'}
                </Badge>
                {step.myCompletion.isVerifiedByManager && (
                  <Badge variant="secondary">Verified</Badge>
                )}
              </div>
            </div>
          )}

          {step.myFeedback && (
            <div className="rounded-md border p-3">
              <div className="flex items-center justify-between">
                <p className="text-sm font-medium">
                  {step.isOwnStep ? 'Your feedback' : 'Their feedback'}
                </p>
                <span className="text-xs text-muted-foreground">
                  {fmt(step.myFeedback.feedbackDate)}
                </span>
              </div>
              <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
                <span>Relevance {step.myFeedback.contentRelevanceRating ?? '—'}/5</span>
                <span>Trainer {step.myFeedback.trainerKnowledgeRating ?? '—'}/5</span>
                <span>Delivery {step.myFeedback.deliveryMethodRating ?? '—'}/5</span>
                <span>Materials {step.myFeedback.materialQualityRating ?? '—'}/5</span>
                <span>Overall {step.myFeedback.overallSatisfactionRating ?? '—'}/5</span>
              </div>
              {step.myFeedback.wouldRecommend && (
                <Badge variant="secondary" className="mt-2 text-[10px]">
                  Would recommend
                </Badge>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {step.availableSchedules.length > 0 && !step.isCompleted && (
        <Card>
          <CardHeader>
            <CardTitle>Scheduled runs</CardTitle>
            <CardDescription>Upcoming deliveries of this programme you could join.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            {step.availableSchedules.map((s) => (
              <div
                key={s.id}
                className="flex cursor-pointer items-center justify-between rounded-md border p-3 hover:bg-muted/50"
                onClick={() => router.push(`/hr/training/schedules/${s.id}`)}
              >
                <div>
                  <div className="font-medium">
                    {fmt(s.startDate)} – {fmt(s.endDate)}
                  </div>
                  <div className="text-xs text-muted-foreground">
                    {s.scheduleNumber}
                    {s.venue ? ` · ${s.venue}` : ''}
                    {s.trainerName || s.vendorName ? ` · ${s.trainerName ?? s.vendorName}` : ''}
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <span className="inline-flex items-center gap-1 text-xs text-muted-foreground">
                    <Users className="h-3.5 w-3.5" />
                    {s.confirmedParticipantsCount} / {s.maxParticipants}
                  </span>
                  {s.isRegistrationOpen ? (
                    <Badge variant="secondary" className="text-[10px]">
                      Open
                    </Badge>
                  ) : (
                    <Badge variant="outline" className="text-[10px]">
                      Closed
                    </Badge>
                  )}
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      {!!history?.length && (
        <Card>
          <CardHeader>
            <CardTitle>How this was recorded</CardTitle>
            <CardDescription>
              Whether a completion rested on evidence or on someone&apos;s authority, and who.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            {history.map((h) => (
              <div key={h.id} className="rounded-md border p-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <Badge
                      variant={
                        h.toStatus === LEARNING_PATH_STEP_STATUS.CompletedByOverride
                          ? 'secondary'
                          : h.toStatus === LEARNING_PATH_STEP_STATUS.Completed
                            ? 'default'
                            : 'outline'
                      }
                    >
                      {h.toStatusName}
                    </Badge>
                    <span className="text-sm text-muted-foreground">{h.changedByName ?? '—'}</span>
                  </div>
                  <span className="text-xs text-muted-foreground">
                    {h.changedAt ? new Date(h.changedAt).toLocaleString() : '—'}
                  </span>
                </div>
                {h.reason && <p className="mt-2 text-sm">{h.reason}</p>}
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      <ConfirmationDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title={isOverride ? 'Record this step as complete?' : 'Mark this step complete?'}
        description={
          isOverride
            ? 'Nothing on the record evidences this step, so this is recorded as an HR completion against your name and the reason below.'
            : step.myNomination
              ? `This will be recorded against nomination ${step.myNomination.nominationNumber}, and the path's progress recalculated.`
              : "The path's progress will be recalculated."
        }
        confirmText={isOverride ? 'Record completion' : 'Mark complete'}
        isLoading={busy}
        confirmDisabled={isOverride && !reason.trim()}
        onConfirm={markComplete}
      >
        {isOverride && (
          <div className="space-y-2">
            <Label htmlFor="override-reason">Reason</Label>
            <Textarea
              id="override-reason"
              rows={3}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="For example: completed externally in 2024, certificate on file."
              maxLength={1000}
            />
            <p className="text-xs text-muted-foreground">
              Kept on the step&apos;s record. This is what an auditor reads when asking why a
              completion had no attendance behind it.
            </p>
          </div>
        )}
      </ConfirmationDialog>
    </div>
  );
}
