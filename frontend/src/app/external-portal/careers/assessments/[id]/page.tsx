'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, Clock, Loader2, Save, Send } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { candidateService } from '@/services/hr/careers.service';
import type {
  CandidateSitting,
  CandidateSittingResult,
  SubmitAnswerPayload,
} from '@/types/hr/recruitment-tests';

interface DraftAnswer {
  selectedOptionIds: string[];
  freeTextAnswer: string;
  numericAnswer: string;
}

const emptyAnswer = (): DraftAnswer => ({ selectedOptionIds: [], freeTextAnswer: '', numericAnswer: '' });

/** Whole seconds left, floored at zero. */
const secondsUntil = (iso?: string | null) => {
  if (!iso) return null;
  return Math.max(0, Math.floor((new Date(iso).getTime() - Date.now()) / 1000));
};

const formatClock = (totalSeconds: number) => {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
};

/**
 * The candidate sits the paper.
 *
 * ⚠ **The countdown here is a display, not the rule.** It counts down to `mustSubmitBy`, which the
 * server fixed when the attempt was opened and checks again at submit. Closing the page does not
 * pause it, and a browser clock that disagrees changes nothing.
 *
 * ⚠ **Answers are saved on the server**, not in this tab. A crash costs the remaining time, not the
 * work — and it is what makes a timed-out paper markable at all, because the server marks what was
 * saved before the deadline.
 *
 * ⚠ **One live window per attempt.** The access token returned when the attempt was opened travels
 * on every write; opening the paper again re-issues it and this window's saves are then refused.
 * That is deliberate: without it a stale second tab's autosave silently replaces fresh answers with
 * ten-minute-old ones.
 */
export default function CandidateSittingPage() {
  const params = useParams();
  const router = useRouter();
  const sittingId = params?.id as string;
  const { toast } = useToast();

  const [answers, setAnswers] = useState<Record<string, DraftAnswer>>({});
  const [token, setToken] = useState<string | null>(null);
  const [secondsLeft, setSecondsLeft] = useState<number | null>(null);
  const [result, setResult] = useState<CandidateSittingResult | null>(null);
  const [dirty, setDirty] = useState(false);
  const [staleWindow, setStaleWindow] = useState(false);

  const dirtyRef = useRef(false);
  dirtyRef.current = dirty;

  const sittingQuery = useQuery({
    queryKey: ['candidate', 'sitting', sittingId],
    queryFn: () => candidateService.getSitting(sittingId),
    enabled: !!sittingId,
    // ⚠ Never refetched on its own. A refetch re-issues the access token and would invalidate the
    // window the candidate is actually typing in.
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    staleTime: Infinity,
  });

  const sitting: CandidateSitting | undefined = sittingQuery.data;

  useEffect(() => {
    if (!sitting) return;
    const seeded: Record<string, DraftAnswer> = {};
    for (const question of sitting.questions) seeded[question.id] = emptyAnswer();
    for (const saved of sitting.savedAnswers) {
      seeded[saved.questionId] = {
        selectedOptionIds: saved.selectedOptionIds ?? [],
        freeTextAnswer: saved.freeTextAnswer ?? '',
        numericAnswer: saved.numericAnswer ?? '',
      };
    }
    setAnswers(seeded);
    setToken(sitting.accessToken ?? null);
    setSecondsLeft(secondsUntil(sitting.mustSubmitBy));
  }, [sitting]);

  const buildPayload = useCallback((): SubmitAnswerPayload[] => {
    return Object.entries(answers)
      .map(([questionId, draft]) => ({
        questionId,
        selectedOptionIds: draft.selectedOptionIds,
        freeTextAnswer: draft.freeTextAnswer.trim() || null,
        numericAnswer: draft.numericAnswer.trim() || null,
      }))
      .filter(
        (answer) =>
          answer.selectedOptionIds.length > 0 || !!answer.freeTextAnswer || !!answer.numericAnswer,
      );
  }, [answers]);

  const saveProgress = useMutation({
    mutationFn: () =>
      candidateService.saveAssessmentProgress({
        sittingId,
        accessToken: token ?? '',
        answers: buildPayload(),
      }),
    onSuccess: () => setDirty(false),
    onError: (error: any) => {
      // The one error worth interrupting for: another window has taken the attempt over, and every
      // further keystroke here would be thrown away.
      setStaleWindow(true);
      toast({
        title: 'This window is no longer the live one',
        description: error?.message ?? 'Reload the page to continue.',
        variant: 'destructive',
      });
    },
  });

  const submit = useMutation({
    mutationFn: () =>
      candidateService.submitAssessment({
        sittingId,
        accessToken: token ?? '',
        answers: buildPayload(),
      }),
    onSuccess: (submitted) => {
      setDirty(false);
      setResult(submitted);
    },
    onError: (error: any) =>
      toast({
        title: 'Could not submit',
        description: error?.message ?? 'Please try again.',
        variant: 'destructive',
      }),
  });

  // ── the countdown ────────────────────────────────────────────────────────
  useEffect(() => {
    if (secondsLeft === null || result) return;
    const timer = window.setInterval(() => {
      setSecondsLeft((current) => (current === null ? null : Math.max(0, current - 1)));
    }, 1000);
    return () => window.clearInterval(timer);
  }, [secondsLeft === null, result]); // eslint-disable-line react-hooks/exhaustive-deps

  // ── autosave ─────────────────────────────────────────────────────────────
  useEffect(() => {
    if (!token || result || staleWindow) return;
    const timer = window.setInterval(() => {
      if (dirtyRef.current && !saveProgress.isPending) saveProgress.mutate();
    }, 20_000);
    return () => window.clearInterval(timer);
  }, [token, result, staleWindow]); // eslint-disable-line react-hooks/exhaustive-deps

  // ⚠ When the clock reaches zero the paper is submitted automatically. The server marks it on what
  // it has either way; submitting means the candidate is TOLD, rather than finding out from a
  // sweep hours later.
  const timeUp = secondsLeft === 0;
  useEffect(() => {
    if (timeUp && !result && !submit.isPending && token) submit.mutate();
  }, [timeUp]); // eslint-disable-line react-hooks/exhaustive-deps

  const answeredCount = useMemo(() => buildPayload().length, [buildPayload]);

  if (sittingQuery.isLoading || !sitting) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (result) {
    return (
      <div className="mx-auto max-w-2xl space-y-6 p-6">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              {result.timedOut ? (
                <AlertTriangle className="h-5 w-5 text-amber-600" />
              ) : (
                <CheckCircle2 className="h-5 w-5 text-green-600" />
              )}
              {result.timedOut ? 'Time expired' : 'Submitted'}
            </CardTitle>
            <CardDescription>{sitting.testName}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-sm">{result.message}</p>
            {result.scorePercent != null && (
              <p className="text-3xl font-semibold">{result.scorePercent}%</p>
            )}
            <Button asChild>
              <Link href="/external-portal/careers/assessments">Back to my assessments</Link>
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  const alreadySubmitted = sitting.status !== 'InProgress';

  return (
    <div className="mx-auto max-w-3xl space-y-6 p-6 pb-32">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">{sitting.testName}</h1>
          <p className="text-muted-foreground mt-1 text-sm">
            {sitting.questions.length} question{sitting.questions.length === 1 ? '' : 's'} &middot;
            attempt {sitting.attemptNumber}
          </p>
        </div>
        {secondsLeft !== null && (
          <Badge
            variant={secondsLeft < 300 ? 'destructive' : 'secondary'}
            className="gap-1 px-3 py-2 text-base"
          >
            <Clock className="h-4 w-4" />
            {formatClock(secondsLeft)}
          </Badge>
        )}
      </div>

      {staleWindow && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>This assessment was opened in another window</AlertTitle>
          <AlertDescription>
            That window is now the one being marked, and nothing typed here will be saved. Reload
            this page to carry on.
          </AlertDescription>
        </Alert>
      )}

      {alreadySubmitted && (
        <Alert>
          <AlertTitle>Already submitted</AlertTitle>
          <AlertDescription>
            This attempt has been submitted and cannot be changed.
          </AlertDescription>
        </Alert>
      )}

      {sitting.instructions && (
        <Card>
          <CardContent className="p-4 text-sm whitespace-pre-wrap">{sitting.instructions}</CardContent>
        </Card>
      )}

      <div className="space-y-4">
        {sitting.questions.map((question, index) => {
          const draft = answers[question.id] ?? emptyAnswer();
          const disabled = alreadySubmitted || staleWindow || submit.isPending;

          const update = (next: Partial<DraftAnswer>) => {
            setAnswers((current) => ({
              ...current,
              [question.id]: { ...(current[question.id] ?? emptyAnswer()), ...next },
            }));
            setDirty(true);
          };

          return (
            <Card key={question.id}>
              <CardHeader className="pb-3">
                <CardTitle className="text-base font-normal">
                  <span className="font-semibold">{index + 1}.</span> {question.questionText}
                </CardTitle>
                <CardDescription>
                  {question.points} mark{question.points === 1 ? '' : 's'}
                  {question.sectionName && ` · ${question.sectionName}`}
                  {question.questionType === 'MultiSelect' && ' · choose all that apply'}
                </CardDescription>
              </CardHeader>
              <CardContent>
                {question.questionType === 'MultiSelect' ? (
                  <div className="space-y-2">
                    {question.options.map((option) => (
                      <label key={option.id} className="flex items-center gap-3 text-sm">
                        <Checkbox
                          disabled={disabled}
                          checked={draft.selectedOptionIds.includes(option.id)}
                          onCheckedChange={(checked) =>
                            update({
                              selectedOptionIds:
                                checked === true
                                  ? [...draft.selectedOptionIds, option.id]
                                  : draft.selectedOptionIds.filter((id) => id !== option.id),
                            })
                          }
                        />
                        {option.optionText}
                      </label>
                    ))}
                  </div>
                ) : question.options.length > 0 ? (
                  <RadioGroup
                    disabled={disabled}
                    value={draft.selectedOptionIds[0] ?? ''}
                    onValueChange={(value) => update({ selectedOptionIds: [value] })}
                    className="space-y-2"
                  >
                    {question.options.map((option) => (
                      <label key={option.id} className="flex items-center gap-3 text-sm">
                        <RadioGroupItem value={option.id} id={option.id} />
                        {option.optionText}
                      </label>
                    ))}
                  </RadioGroup>
                ) : question.questionType === 'Numeric' ? (
                  <div className="space-y-2">
                    <Label htmlFor={`numeric-${question.id}`} className="sr-only">
                      Your answer
                    </Label>
                    <Input
                      id={`numeric-${question.id}`}
                      inputMode="decimal"
                      disabled={disabled}
                      className="max-w-[200px]"
                      value={draft.numericAnswer}
                      onChange={(event) => update({ numericAnswer: event.target.value })}
                    />
                  </div>
                ) : (
                  <Textarea
                    rows={5}
                    disabled={disabled}
                    value={draft.freeTextAnswer}
                    onChange={(event) => update({ freeTextAnswer: event.target.value })}
                    placeholder="Your answer"
                  />
                )}
              </CardContent>
            </Card>
          );
        })}
      </div>

      <div className="fixed inset-x-0 bottom-0 border-t bg-background/95 p-4 backdrop-blur">
        <div className="mx-auto flex max-w-3xl flex-wrap items-center justify-between gap-3">
          <p className="text-sm text-muted-foreground">
            {answeredCount} of {sitting.questions.length} answered
            {saveProgress.isPending
              ? ' · saving…'
              : dirty
                ? ' · unsaved changes'
                : ' · saved'}
          </p>
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              disabled={alreadySubmitted || staleWindow || saveProgress.isPending}
              onClick={() => saveProgress.mutate()}
            >
              {saveProgress.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Save className="mr-2 h-4 w-4" />
              )}
              Save
            </Button>
            <Button
              disabled={alreadySubmitted || staleWindow || submit.isPending}
              onClick={() => {
                if (
                  window.confirm(
                    answeredCount < sitting.questions.length
                      ? `You have answered ${answeredCount} of ${sitting.questions.length}. Submit anyway? You cannot come back to it.`
                      : 'Submit your answers? You cannot come back to it.',
                  )
                ) {
                  submit.mutate();
                }
              }}
            >
              {submit.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Send className="mr-2 h-4 w-4" />
              )}
              Submit
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
