'use client';

import { useMemo, useState } from 'react';
import { useParams } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Loader2,
  CheckCircle2,
  Circle,
  Lock,
  ExternalLink,
  Award,
  AlertTriangle,
  FileSignature,
  Star,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Checkbox } from '@/components/ui/checkbox';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { employeeOrientationService } from '@/services/hr/employee-orientation.service';
import { ORIENTATION_CONTENT_TYPE_OPTIONS } from '@/types/hr/orientation';
import type {
  OrientationContentItem,
  OrientationContentProgress,
  OrientationAssessmentResult,
} from '@/types/hr/orientation';

const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const contentTypeLabel = (v: string) =>
  ORIENTATION_CONTENT_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;

interface AnswerState {
  selectedOptionIds: string[];
  freeTextAnswer: string;
}

/**
 * One participant working through one enrollment.
 *
 * The structure comes from the participant-safe `content` read rather than the catalogue endpoints,
 * which are HR-only — and progress rows are layered on top of it rather than driving it, because a
 * row does not exist until its item has been touched. Building the list the other way round would
 * show a fresh enrollment nothing to do.
 *
 * ⚠ The assessment is read through `getAssessment`, never the authoring endpoint: the participant
 * read forces `isCorrect` false and withholds explanations until the attempt has been graded.
 */
export default function MyOrientationPlayerPage() {
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [answers, setAnswers] = useState<Record<string, AnswerState>>({});
  const [submitting, setSubmitting] = useState(false);
  const [confirmSubmit, setConfirmSubmit] = useState(false);
  const [result, setResult] = useState<OrientationAssessmentResult | null>(null);
  const [busyItemId, setBusyItemId] = useState<string | null>(null);
  const [openedAt, setOpenedAt] = useState<Record<string, number>>({});

  const [signTarget, setSignTarget] = useState<{ id: string; accept: boolean } | null>(null);
  const [declineReason, setDeclineReason] = useState('');
  const [signing, setSigning] = useState(false);

  const [feedback, setFeedback] = useState({
    overallRating: '',
    contentRating: '',
    facilitatorRating: '',
    relevanceRating: '',
    comments: '',
  });
  const [sendingFeedback, setSendingFeedback] = useState(false);

  const enrollmentKey = ['hr', 'orientation-enrollments', id];

  const { data: enrollment, isLoading, isError } = useQuery({
    queryKey: enrollmentKey,
    queryFn: () => employeeOrientationService.getById(id),
    enabled: !!id,
  });

  const { data: modules = [], isLoading: loadingContent } = useQuery({
    queryKey: ['hr', 'orientation-enrollments', id, 'content'],
    queryFn: () => employeeOrientationService.getProgramContent(id),
    enabled: !!id,
  });

  const { data: progress = [] } = useQuery({
    queryKey: ['hr', 'orientation-enrollments', id, 'content-progress'],
    queryFn: () => employeeOrientationService.getContentProgress(id),
    enabled: !!id,
  });

  const { data: questions = [] } = useQuery({
    queryKey: ['hr', 'orientation-enrollments', id, 'assessment'],
    queryFn: () => employeeOrientationService.getAssessment(id),
    enabled: !!id,
  });

  const { data: acknowledgements = [] } = useQuery({
    queryKey: ['hr', 'orientation-enrollments', id, 'acknowledgements'],
    queryFn: () => employeeOrientationService.getAcknowledgements(id),
    enabled: !!id,
  });

  const { data: certificates = [] } = useQuery({
    queryKey: ['hr', 'orientation-enrollments', id, 'certificates'],
    queryFn: () => employeeOrientationService.getCertificatesForEnrollment(id),
    enabled: !!id,
  });

  const { data: myFeedback = [] } = useQuery({
    queryKey: ['hr', 'orientation-enrollments', id, 'feedback'],
    queryFn: () => employeeOrientationService.getFeedback(id),
    enabled: !!id,
  });

  const progressByItem = useMemo(
    () => new Map(progress.map((p) => [p.contentItemId, p])),
    [progress],
  );

  const allItems = useMemo(() => modules.flatMap((m) => m.contentItems), [modules]);
  const requiredItems = allItems.filter((i) => i.isRequired);
  const doneRequired = requiredItems.filter(
    (i) => progressByItem.get(i.id)?.status === 'Completed',
  ).length;

  const refreshAfterTrack = () =>
    Promise.all([
      queryClient.invalidateQueries({
        queryKey: ['hr', 'orientation-enrollments', id, 'content-progress'],
      }),
      // Completing the last required item can finish the enrollment outright, so the header has to
      // be refetched rather than assumed still in progress.
      queryClient.invalidateQueries({ queryKey: enrollmentKey }),
      queryClient.invalidateQueries({ queryKey: ['hr', 'orientation-enrollments', 'mine'] }),
    ]);

  const track = async (
    item: OrientationContentItem,
    opts: { markCompleted: boolean; acknowledge?: boolean },
  ) => {
    setBusyItemId(item.id);
    try {
      // Time is credited from when the item was opened in this sitting; nothing is invented for an
      // item marked done without ever being opened.
      const started = openedAt[item.id];
      const delta = started ? Math.max(0, Math.round((Date.now() - started) / 1000)) : 0;

      await employeeOrientationService.trackContentProgress(id, {
        employeeOrientationId: id,
        contentItemId: item.id,
        timeSpentSecondsDelta: delta,
        markCompleted: opts.markCompleted,
        acknowledge: opts.acknowledge ?? false,
      });
      if (started) setOpenedAt((prev) => ({ ...prev, [item.id]: Date.now() }));
      await refreshAfterTrack();
      if (opts.markCompleted) toast({ title: 'Marked done', description: item.title });
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to record progress.',
        variant: 'destructive',
      });
    } finally {
      setBusyItemId(null);
    }
  };

  const open = (item: OrientationContentItem) => {
    setOpenedAt((prev) => ({ ...prev, [item.id]: Date.now() }));
    if (item.resourceUrl) window.open(item.resourceUrl, '_blank', 'noopener,noreferrer');
    // Records the access even when there is nothing to open — a text item still counts as started.
    void track(item, { markCompleted: false });
  };

  const setAnswer = (questionId: string, patch: Partial<AnswerState>) =>
    setAnswers((prev) => ({
      ...prev,
      [questionId]: {
        selectedOptionIds: prev[questionId]?.selectedOptionIds ?? [],
        freeTextAnswer: prev[questionId]?.freeTextAnswer ?? '',
        ...patch,
      },
    }));

  const answeredCount = questions.filter((q) => {
    const a = answers[q.id];
    if (!a) return false;
    return q.questionType === 'FreeText'
      ? a.freeTextAnswer.trim().length > 0
      : a.selectedOptionIds.length > 0;
  }).length;

  const submitAssessment = async () => {
    setSubmitting(true);
    try {
      const payload = await employeeOrientationService.submitAssessment(id, {
        employeeOrientationId: id,
        answers: questions.map((q) => ({
          questionId: q.id,
          selectedOptionIds: answers[q.id]?.selectedOptionIds ?? [],
          freeTextAnswer: answers[q.id]?.freeTextAnswer || null,
        })),
      });
      setResult(payload);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: enrollmentKey }),
        queryClient.invalidateQueries({
          queryKey: ['hr', 'orientation-enrollments', id, 'assessment'],
        }),
        queryClient.invalidateQueries({ queryKey: ['hr', 'orientation-enrollments', 'mine'] }),
      ]);
      toast({
        title: payload.passed ? 'Passed' : 'Not passed',
        description: `You scored ${payload.scorePercent}%.`,
        variant: payload.passed ? undefined : 'destructive',
      });
      setConfirmSubmit(false);
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to submit the assessment.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setSubmitting(false);
    }
  };

  const sign = async () => {
    if (!signTarget) return false;
    if (!signTarget.accept && !declineReason.trim()) {
      toast({ title: 'Give a reason for declining', variant: 'destructive' });
      return false;
    }
    setSigning(true);
    try {
      await employeeOrientationService.signAcknowledgement({
        acknowledgementId: signTarget.id,
        accept: signTarget.accept,
        declineReason: signTarget.accept ? null : declineReason.trim(),
      });
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: ['hr', 'orientation-enrollments', id, 'acknowledgements'],
        }),
        queryClient.invalidateQueries({ queryKey: enrollmentKey }),
      ]);
      toast({ title: signTarget.accept ? 'Signed' : 'Declined' });
      setSignTarget(null);
      setDeclineReason('');
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to record your response.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setSigning(false);
    }
  };

  const sendFeedback = async () => {
    setSendingFeedback(true);
    try {
      const num = (v: string) => (v ? Number(v) : null);
      await employeeOrientationService.submitFeedback(id, {
        employeeOrientationId: id,
        overallRating: num(feedback.overallRating),
        contentRating: num(feedback.contentRating),
        facilitatorRating: num(feedback.facilitatorRating),
        relevanceRating: num(feedback.relevanceRating),
        comments: feedback.comments || null,
        isAnonymous: false,
      });
      await queryClient.invalidateQueries({
        queryKey: ['hr', 'orientation-enrollments', id, 'feedback'],
      });
      toast({ title: 'Thank you', description: 'Your feedback has been recorded.' });
      setFeedback({
        overallRating: '',
        contentRating: '',
        facilitatorRating: '',
        relevanceRating: '',
        comments: '',
      });
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to submit feedback.',
        variant: 'destructive',
      });
    } finally {
      setSendingFeedback(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }

  if (isError || !enrollment) {
    return (
      <div className="p-6">
        <EmptyState
          title="Orientation not found"
          description="It may have been removed, or it may not be yours to open."
        />
      </div>
    );
  }

  const pendingAcks = acknowledgements.filter(
    (a) => a.status === 'Pending' || a.status === 'Presented',
  );
  const graded = enrollment.attemptCount > 0;
  const activeCertificate = certificates.find((c) => c.status === 'Active');

  return (
    <div className="space-y-6">
      <PageHeader
        title={enrollment.programTitle ?? 'Orientation'}
        description={`${enrollment.programCode ?? ''}${
          enrollment.sessionTitle ? ` · ${enrollment.sessionTitle}` : ''
        }`}
        backHref="/me/orientation"
        actions={<StatusBadge status={enrollment.completionStatus} />}
      />

      <MetricTiles
        tiles={[
          { label: 'Progress', value: `${enrollment.progressPercentage}%` },
          {
            label: 'Content done',
            value: `${doneRequired} of ${requiredItems.length}`,
            hint: requiredItems.length === 0 ? 'Nothing to work through' : 'Required items only',
          },
          {
            label: 'Assessment',
            value: graded
              ? `${enrollment.finalScore ?? 0}%`
              : questions.length > 0
                ? 'Not attempted'
                : 'None',
            hint: graded ? (enrollment.isPassed ? 'Passed' : 'Not passed') : undefined,
            tone: graded ? (enrollment.isPassed ? 'success' : 'danger') : 'default',
          },
          {
            label: 'Due',
            value: fmt(enrollment.nextDueDate),
            tone: enrollment.completionStatus === 'Overdue' ? 'danger' : 'default',
          },
        ]}
      />

      <Card>
        <CardContent className="py-4">
          <Progress value={enrollment.progressPercentage} className="h-2" />
          {enrollment.enrollmentStatus === 'Waitlisted' && (
            <p className="mt-3 text-sm text-amber-600">
              You are on the waitlist
              {enrollment.waitlistPosition ? ` at position ${enrollment.waitlistPosition}` : ''} —
              a seat will be released to you if one frees up.
            </p>
          )}
          {pendingAcks.length > 0 && (
            <p className="mt-3 text-sm text-amber-600">
              {pendingAcks.length} declaration{pendingAcks.length === 1 ? '' : 's'} still need
              signing before this can be completed.
            </p>
          )}
        </CardContent>
      </Card>

      <Tabs defaultValue="content">
        <TabsList className="flex-wrap">
          <TabsTrigger value="content">Content ({allItems.length})</TabsTrigger>
          {questions.length > 0 && (
            <TabsTrigger value="assessment">Assessment ({questions.length})</TabsTrigger>
          )}
          {acknowledgements.length > 0 && (
            <TabsTrigger value="acknowledgements">
              Declarations ({acknowledgements.length})
            </TabsTrigger>
          )}
          <TabsTrigger value="feedback">Feedback</TabsTrigger>
          {certificates.length > 0 && (
            <TabsTrigger value="certificate">Certificate</TabsTrigger>
          )}
        </TabsList>

        {/* ── Content ─────────────────────────────────────────────────────── */}
        <TabsContent value="content" className="space-y-4 pt-4">
          {loadingContent ? (
            <p className="text-muted-foreground text-sm">Loading content…</p>
          ) : modules.length === 0 ? (
            <EmptyState
              title="Nothing to work through"
              description="This programme has no content modules. If it has an assessment, go straight to it."
            />
          ) : (
            modules.map((module) => {
              // Within a module marked sequential, everything after the first unfinished item is
              // locked — the order is the point of the flag, and the server does not enforce it.
              const firstUnfinished = module.contentItems.find(
                (i) => progressByItem.get(i.id)?.status !== 'Completed',
              );

              return (
                <Card key={module.id}>
                  <CardHeader>
                    <div className="flex flex-wrap items-center gap-2">
                      <CardTitle className="text-base">{module.title}</CardTitle>
                      {module.isOptional && <Badge variant="outline">Optional</Badge>}
                      {module.isSequentiallyRequired && (
                        <Badge variant="secondary">In order</Badge>
                      )}
                      {module.estimatedDurationMinutes ? (
                        <span className="text-muted-foreground text-xs">
                          ~{module.estimatedDurationMinutes} min
                        </span>
                      ) : null}
                    </div>
                    {module.description && (
                      <CardDescription>{module.description}</CardDescription>
                    )}
                  </CardHeader>
                  <CardContent className="space-y-2">
                    {module.contentItems.length === 0 ? (
                      <p className="text-muted-foreground text-sm">
                        This module has no content yet.
                      </p>
                    ) : (
                      module.contentItems.map((item) => {
                        const p: OrientationContentProgress | undefined = progressByItem.get(
                          item.id,
                        );
                        const done = p?.status === 'Completed';
                        const locked =
                          module.isSequentiallyRequired &&
                          !done &&
                          !!firstUnfinished &&
                          firstUnfinished.id !== item.id;

                        return (
                          <div
                            key={item.id}
                            className={`flex flex-wrap items-center gap-3 rounded-md border p-3 ${
                              locked ? 'opacity-60' : ''
                            }`}
                          >
                            {done ? (
                              <CheckCircle2 className="h-5 w-5 shrink-0 text-green-600" />
                            ) : locked ? (
                              <Lock className="text-muted-foreground h-5 w-5 shrink-0" />
                            ) : (
                              <Circle className="text-muted-foreground h-5 w-5 shrink-0" />
                            )}

                            <div className="min-w-0 flex-1">
                              <p className="font-medium">{item.title}</p>
                              <p className="text-muted-foreground text-xs">
                                {contentTypeLabel(item.contentType)}
                                {!item.isRequired && ' · optional'}
                                {item.mediaDurationSeconds
                                  ? ` · ${Math.round(item.mediaDurationSeconds / 60)} min`
                                  : ''}
                                {locked && ' · finish the item above first'}
                                {p?.completedAt && ` · done ${fmt(p.completedAt)}`}
                              </p>
                            </div>

                            <div className="flex shrink-0 gap-2">
                              <Button
                                variant="outline"
                                size="sm"
                                disabled={locked || busyItemId === item.id}
                                onClick={() => open(item)}
                              >
                                <ExternalLink className="mr-2 h-4 w-4" />
                                {item.resourceUrl ? 'Open' : 'Start'}
                              </Button>
                              {!done && (
                                <Button
                                  size="sm"
                                  disabled={locked || busyItemId === item.id}
                                  onClick={() => track(item, { markCompleted: true })}
                                >
                                  {busyItemId === item.id && (
                                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                  )}
                                  Mark done
                                </Button>
                              )}
                            </div>
                          </div>
                        );
                      })
                    )}
                  </CardContent>
                </Card>
              );
            })
          )}
        </TabsContent>

        {/* ── Assessment ──────────────────────────────────────────────────── */}
        {questions.length > 0 && (
          <TabsContent value="assessment" className="space-y-4 pt-4">
            {(result || graded) && (
              <Alert>
                {(result?.passed ?? enrollment.isPassed) ? (
                  <CheckCircle2 className="h-4 w-4" />
                ) : (
                  <AlertTriangle className="h-4 w-4" />
                )}
                <AlertTitle>
                  {result
                    ? `You scored ${result.scorePercent}% — ${result.passed ? 'passed' : 'not passed'}`
                    : `Last attempt: ${enrollment.finalScore ?? 0}% — ${
                        enrollment.isPassed ? 'passed' : 'not passed'
                      }`}
                </AlertTitle>
                <AlertDescription>
                  {result
                    ? `${result.correctCount} of ${result.totalQuestions} correct, ${result.pointsAwarded} of ${result.totalPoints} points. This was attempt ${result.attemptNumber}.`
                    : `Attempt ${enrollment.attemptCount}. Submitting again replaces this result.`}
                </AlertDescription>
              </Alert>
            )}

            <Card>
              <CardHeader>
                <CardTitle className="text-base">Questions</CardTitle>
                <CardDescription>
                  Every question counts towards the score, so leaving one blank costs its marks.
                  {answeredCount < questions.length &&
                    ` You have answered ${answeredCount} of ${questions.length}.`}
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-6">
                {questions.map((q, index) => {
                  const a = answers[q.id];
                  return (
                    <div key={q.id} className="space-y-3">
                      <div className="flex items-start gap-2">
                        <span className="text-muted-foreground font-mono text-sm">
                          {index + 1}.
                        </span>
                        <div className="flex-1">
                          <p className="font-medium">{q.questionText}</p>
                          <p className="text-muted-foreground text-xs">
                            {q.questionType === 'FreeText'
                              ? 'Free text — recorded but not auto-marked'
                              : q.questionType === 'MultiSelect'
                                ? `Choose all that apply · ${q.points} points`
                                : `Choose one · ${q.points} points`}
                          </p>
                        </div>
                      </div>

                      <div className="pl-6">
                        {q.questionType === 'FreeText' ? (
                          <Textarea
                            rows={3}
                            value={a?.freeTextAnswer ?? ''}
                            onChange={(e) =>
                              setAnswer(q.id, { freeTextAnswer: e.target.value })
                            }
                            placeholder="Your answer"
                          />
                        ) : q.questionType === 'MultiSelect' ? (
                          <div className="space-y-2">
                            {q.options.map((o) => {
                              const checked = a?.selectedOptionIds.includes(o.id) ?? false;
                              return (
                                <label
                                  key={o.id}
                                  className="flex items-center gap-2 text-sm"
                                  htmlFor={`${q.id}-${o.id}`}
                                >
                                  <Checkbox
                                    id={`${q.id}-${o.id}`}
                                    checked={checked}
                                    onCheckedChange={(v) =>
                                      setAnswer(q.id, {
                                        selectedOptionIds: v
                                          ? [...(a?.selectedOptionIds ?? []), o.id]
                                          : (a?.selectedOptionIds ?? []).filter(
                                              (x) => x !== o.id,
                                            ),
                                      })
                                    }
                                  />
                                  {o.optionText}
                                </label>
                              );
                            })}
                          </div>
                        ) : (
                          <RadioGroup
                            value={a?.selectedOptionIds[0] ?? ''}
                            onValueChange={(v) => setAnswer(q.id, { selectedOptionIds: [v] })}
                            className="space-y-2"
                          >
                            {q.options.map((o) => (
                              <label
                                key={o.id}
                                className="flex items-center gap-2 text-sm"
                                htmlFor={`${q.id}-${o.id}`}
                              >
                                <RadioGroupItem id={`${q.id}-${o.id}`} value={o.id} />
                                {o.optionText}
                              </label>
                            ))}
                          </RadioGroup>
                        )}

                        {/* Only present once graded — the participant read withholds it until then. */}
                        {q.explanation && (
                          <p className="text-muted-foreground mt-2 text-xs italic">
                            {q.explanation}
                          </p>
                        )}
                      </div>
                    </div>
                  );
                })}

                <div className="flex justify-end">
                  <Button onClick={() => setConfirmSubmit(true)} disabled={submitting}>
                    {submitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                    Submit assessment
                  </Button>
                </div>
              </CardContent>
            </Card>
          </TabsContent>
        )}

        {/* ── Acknowledgements ────────────────────────────────────────────── */}
        {acknowledgements.length > 0 && (
          <TabsContent value="acknowledgements" className="space-y-4 pt-4">
            {acknowledgements.map((ack) => (
              <Card key={ack.id}>
                <CardHeader>
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <CardTitle className="text-base">{ack.title}</CardTitle>
                    <StatusBadge status={ack.status} />
                  </div>
                  <CardDescription>
                    {ack.status === 'Signed'
                      ? `Signed ${fmt(ack.signedAt)}`
                      : ack.status === 'Declined'
                        ? `Declined ${fmt(ack.declinedAt)}${ack.declineReason ? ` — ${ack.declineReason}` : ''}`
                        : 'Signing this is recorded with your IP address and a tamper hash.'}
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  <p className="whitespace-pre-wrap text-sm">{ack.acknowledgementText}</p>
                  {(ack.status === 'Pending' || ack.status === 'Presented') && (
                    <div className="flex gap-2">
                      <Button
                        size="sm"
                        onClick={() => setSignTarget({ id: ack.id, accept: true })}
                      >
                        <FileSignature className="mr-2 h-4 w-4" />
                        I agree
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => setSignTarget({ id: ack.id, accept: false })}
                      >
                        Decline
                      </Button>
                    </div>
                  )}
                </CardContent>
              </Card>
            ))}
          </TabsContent>
        )}

        {/* ── Feedback ────────────────────────────────────────────────────── */}
        <TabsContent value="feedback" className="pt-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">How was it?</CardTitle>
              <CardDescription>
                {myFeedback.length > 0
                  ? 'You have already given feedback. Sending again adds another response.'
                  : 'Rated out of 5. Everything here is optional.'}
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 sm:grid-cols-2">
                {(
                  [
                    ['overallRating', 'Overall'],
                    ['contentRating', 'Content'],
                    ['facilitatorRating', 'Facilitator'],
                    ['relevanceRating', 'Relevance to your job'],
                  ] as const
                ).map(([key, label]) => (
                  <div key={key} className="space-y-2">
                    <Label>{label}</Label>
                    <Select
                      value={feedback[key]}
                      onValueChange={(v) => setFeedback((f) => ({ ...f, [key]: v }))}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Not rated" />
                      </SelectTrigger>
                      <SelectContent>
                        {[1, 2, 3, 4, 5].map((n) => (
                          <SelectItem key={n} value={String(n)}>
                            {n} {n === 1 ? 'star' : 'stars'}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                ))}
              </div>
              <div className="space-y-2">
                <Label htmlFor="feedback-comments">Comments</Label>
                <Textarea
                  id="feedback-comments"
                  rows={4}
                  value={feedback.comments}
                  onChange={(e) => setFeedback((f) => ({ ...f, comments: e.target.value }))}
                  placeholder="What worked, what did not."
                />
              </div>
              <div className="flex justify-end">
                <Button onClick={sendFeedback} disabled={sendingFeedback}>
                  {sendingFeedback ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Star className="mr-2 h-4 w-4" />
                  )}
                  Send feedback
                </Button>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Certificate ─────────────────────────────────────────────────── */}
        {certificates.length > 0 && (
          <TabsContent value="certificate" className="space-y-4 pt-4">
            {activeCertificate && (
              <Alert>
                <Award className="h-4 w-4" />
                <AlertTitle>Certificate {activeCertificate.certificateNumber}</AlertTitle>
                <AlertDescription>
                  Issued {fmt(activeCertificate.issuedAt)}
                  {activeCertificate.expiresAt
                    ? `, expires ${fmt(activeCertificate.expiresAt)}`
                    : ' — it does not expire'}
                  .
                </AlertDescription>
              </Alert>
            )}
            {certificates.map((c) => (
              <Card key={c.id}>
                <CardContent className="flex flex-wrap items-center justify-between gap-3 py-4">
                  <div>
                    <p className="font-mono font-medium">{c.certificateNumber}</p>
                    <p className="text-muted-foreground text-xs">
                      Issued {fmt(c.issuedAt)}
                      {c.issuedByName ? ` by ${c.issuedByName}` : ''}
                      {c.revokedAt ? ` · revoked ${fmt(c.revokedAt)}` : ''}
                      {c.revocationReason ? ` — ${c.revocationReason}` : ''}
                    </p>
                  </div>
                  <div className="flex items-center gap-2">
                    <StatusBadge status={c.status} />
                    {c.verificationUrl && (
                      <Button asChild size="sm" variant="outline">
                        <a href={c.verificationUrl} target="_blank" rel="noreferrer">
                          <ExternalLink className="mr-2 h-4 w-4" />
                          Verify
                        </a>
                      </Button>
                    )}
                  </div>
                </CardContent>
              </Card>
            ))}
          </TabsContent>
        )}
      </Tabs>

      <ConfirmationDialog
        open={confirmSubmit}
        onOpenChange={setConfirmSubmit}
        title="Submit your assessment?"
        description={
          answeredCount < questions.length
            ? `You have answered ${answeredCount} of ${questions.length}. Unanswered questions score zero, and submitting replaces any previous attempt.`
            : 'Submitting replaces any previous attempt.'
        }
        confirmText="Submit"
        isLoading={submitting}
        onConfirm={submitAssessment}
      />

      <ConfirmationDialog
        open={signTarget !== null && signTarget.accept}
        onOpenChange={(o) => !o && setSignTarget(null)}
        title="Sign this declaration?"
        description="Your name, the time, and your IP address are recorded against it. This cannot be undone."
        confirmText="I agree"
        isLoading={signing}
        onConfirm={sign}
      />

      <ConfirmationDialog
        open={signTarget !== null && !signTarget.accept}
        onOpenChange={(o) => {
          if (!o) {
            setSignTarget(null);
            setDeclineReason('');
          }
        }}
        title="Decline this declaration?"
        description="Declining is recorded and HR will be able to see your reason."
        confirmText="Decline"
        variant="destructive"
        isLoading={signing}
        onConfirm={sign}
      >
        <div className="space-y-2">
          <Label htmlFor="decline-reason">Reason</Label>
          <Textarea
            id="decline-reason"
            rows={3}
            value={declineReason}
            onChange={(e) => setDeclineReason(e.target.value)}
            placeholder="Why are you declining?"
          />
        </div>
      </ConfirmationDialog>
    </div>
  );
}
