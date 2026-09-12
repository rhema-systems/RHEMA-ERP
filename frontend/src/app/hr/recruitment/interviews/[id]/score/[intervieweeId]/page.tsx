'use client';

import { useEffect, useMemo, useState } from 'react';
import { useParams, useRouter, useSearchParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CheckCheck, Loader2, Save } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { HR_ROLES } from '@/components/hr/common/PermissionGate';
import { humanizeEnum } from '@/lib/hr/attendance-format';
import { jobInterviewService } from '@/services/hr/interviews.service';
import {
  INTERVIEW_RECOMMENDATIONS,
  RECOMMENDATION_ORDINALS,
  recommendationFromOrdinal,
  type JobInterviewRecommendation,
} from '@/types/hr/interviews';

type Entry = { rawScore: string; remarks: string };

/**
 * One panelist's scorecard for one candidate.
 *
 * ⚠ **The running total is computed here with the same arithmetic the server uses** —
 * `(mark ÷ top of band) × weight` — deliberately duplicated rather than waiting for the server's
 * number. Two independent computations make a disagreement visible instead of silent, which is the
 * lesson from the appraisal scoring model. If the total shown here ever differs from the one that
 * comes back on save, that is a bug worth chasing, not a rounding artefact.
 *
 * Marks are held as strings until submit so a half-typed value does not read as a real 0.
 */
export default function InterviewScorecardPage() {
  const params = useParams();
  const router = useRouter();
  const searchParams = useSearchParams();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const { hasAnyRole, user } = useAuth();

  const interviewId = params.id as string;
  const intervieweeId = params.intervieweeId as string;
  const panelistIdFromQuery = searchParams.get('panelistId');

  const [entries, setEntries] = useState<Record<string, Entry>>({});
  const [comments, setComments] = useState('');
  const [recommendation, setRecommendation] = useState<JobInterviewRecommendation | ''>('');
  const [hydrated, setHydrated] = useState(false);

  const canManage = hasAnyRole(HR_ROLES);

  const interview = useQuery({
    queryKey: ['hr', 'interview', interviewId],
    queryFn: () => jobInterviewService.getDetails(interviewId),
  });

  const data = interview.data;
  const candidate = data?.interviewees.find((ie) => ie.id === intervieweeId);

  // Whose card this is: the query string when HR arrived from the candidate list, otherwise the
  // caller's own panel seat. The server refuses anything else, so this only decides what to load.
  const myPanelist =
    data?.panelists.find((p) => p.id === panelistIdFromQuery) ??
    data?.panelists.find((p) => p.employeeId === (user as any)?.employeeId);

  const questions = useMemo(
    () =>
      (data?.questions ?? []).flatMap((plan) =>
        plan.selectedQuestions.map((q) => ({ ...q, planName: plan.questionTypeName, planId: plan.id })),
      ),
    [data],
  );

  const existingCards = useQuery({
    queryKey: ['hr', 'interview-scores', intervieweeId],
    queryFn: () => jobInterviewService.getScoreSummaries(intervieweeId),
    enabled: !!intervieweeId,
  });

  const myCard = existingCards.data?.find((c) => c.internalPanelistId === myPanelist?.id);

  // Held as plain values so the queries and mutations below never need a non-null assertion — the
  // screen refuses to render its form at all without a panel seat.
  const myPanelistId = myPanelist?.id ?? null;
  const myCardId = myCard?.id ?? null;

  const draft = useQuery({
    queryKey: ['hr', 'interview-draft', interviewId, intervieweeId, myPanelistId],
    queryFn: () =>
      jobInterviewService.getScoreDraft(interviewId, intervieweeId, { internalPanelistId: myPanelistId }),
    enabled: !!myPanelistId && !myCard?.isFinalized,
  });

  const submitted = useQuery({
    queryKey: ['hr', 'interview-score-detail', myCardId],
    queryFn: () => (myCardId ? jobInterviewService.getScoreSummary(myCardId) : Promise.resolve(null)),
    enabled: !!myCardId,
  });

  // Seed the form once: a submitted card wins over a draft, because the draft is deleted when a card
  // is signed off and a lingering one would otherwise overwrite the real marks.
  useEffect(() => {
    if (hydrated || !questions.length) return;
    if (myCard && submitted.isLoading) return;
    if (!myCard && draft.isLoading) return;

    const seeded: Record<string, Entry> = {};
    for (const q of questions) seeded[q.questionDetailId] = { rawScore: '', remarks: '' };

    if (submitted.data) {
      for (const e of submitted.data.scoreEntries) {
        seeded[e.questionDetailId] = { rawScore: String(e.rawScore), remarks: e.remarks ?? '' };
      }
      setComments(submitted.data.comments ?? '');
      setRecommendation(submitted.data.recommendation);
    } else if (draft.data) {
      for (const e of draft.data.scoreEntries) {
        seeded[e.questionDetailId] = {
          rawScore: e.rawScore === null || e.rawScore === undefined ? '' : String(e.rawScore),
          remarks: e.remarks ?? '',
        };
      }
      setComments(draft.data.comments ?? '');
      setRecommendation(recommendationFromOrdinal(draft.data.recommendation) ?? '');
    }

    setEntries(seeded);
    setHydrated(true);
  }, [hydrated, questions, myCard, submitted.data, submitted.isLoading, draft.data, draft.isLoading]);

  const scored = questions.filter((q) => entries[q.questionDetailId]?.rawScore !== '');
  const runningTotals = scored.reduce(
    (acc, q) => {
      const raw = Number(entries[q.questionDetailId].rawScore);
      if (Number.isNaN(raw)) return acc;
      const weighted = q.maxScore > 0 ? (raw / q.maxScore) * q.weight : 0;
      return { raw: acc.raw + raw, weighted: acc.weighted + weighted };
    },
    { raw: 0, weighted: 0 },
  );

  const outOfBand = questions.filter((q) => {
    const value = entries[q.questionDetailId]?.rawScore;
    if (!value) return false;
    const raw = Number(value);
    return Number.isNaN(raw) || raw < q.minScore || raw > q.maxScore;
  });

  // Mirrors the server's finalisation gate so the panelist knows before they press it.
  const shortSections = (data?.questions ?? [])
    .map((plan) => {
      const required = Math.min(plan.requiredQuestionCount, plan.selectedQuestions.length);
      const answered = plan.selectedQuestions.filter(
        (q) => entries[q.questionDetailId]?.rawScore !== '' && entries[q.questionDetailId] !== undefined,
      ).length;
      return { name: plan.questionTypeName, required, answered };
    })
    .filter((s) => s.answered < s.required);

  const payloadEntries = () =>
    scored.map((q) => ({
      questionDetailId: q.questionDetailId,
      rawScore: Number(entries[q.questionDetailId].rawScore),
      remarks: entries[q.questionDetailId].remarks || null,
    }));

  const saveDraft = useMutation({
    mutationFn: () =>
      jobInterviewService.saveScoreDraft(interviewId, intervieweeId, {
        jobIntervieweeId: intervieweeId,
        internalPanelistId: myPanelistId,
        scoreEntries: questions.map((q) => ({
          questionDetailId: q.questionDetailId,
          rawScore: entries[q.questionDetailId]?.rawScore ? Number(entries[q.questionDetailId].rawScore) : null,
          remarks: entries[q.questionDetailId]?.remarks || null,
        })),
        comments: comments || null,
        recommendation: recommendation ? RECOMMENDATION_ORDINALS[recommendation] : null,
      }),
    onSuccess: () => toast({ title: 'Draft saved', description: 'Only you can see it.' }),
    onError: (error: any) =>
      toast({ title: 'Could not save the draft', description: error?.message, variant: 'destructive' }),
  });

  const submit = useMutation({
    mutationFn: () =>
      jobInterviewService.saveScoreSummary(intervieweeId, {
        jobIntervieweeId: intervieweeId,
        internalPanelistId: myPanelistId,
        recommendation: recommendation as JobInterviewRecommendation,
        comments: comments || null,
        evaluationDate: new Date().toISOString(),
        scoreEntries: payloadEntries(),
      }),
    onSuccess: (saved) => {
      // The two totals are computed independently — if they disagree, say so rather than quietly
      // showing the server's number.
      const mine = Math.round(runningTotals.weighted * 10000) / 10000;
      if (Math.abs(mine - saved.totalWeightedScore) > 0.01) {
        toast({
          title: 'Scores saved, but the totals disagree',
          description: `This screen computed ${mine}; the server recorded ${saved.totalWeightedScore}. Please report this.`,
          variant: 'destructive',
        });
      } else {
        toast({ title: 'Scorecard saved', description: 'Sign it off when you are happy with it.' });
      }
      queryClient.invalidateQueries({ queryKey: ['hr', 'interview-scores', intervieweeId] });
    },
    onError: (error: any) =>
      toast({ title: 'Could not save the scorecard', description: error?.message, variant: 'destructive' }),
  });

  const finalize = useMutation({
    mutationFn: async () => {
      const saved = await jobInterviewService.saveScoreSummary(intervieweeId, {
        jobIntervieweeId: intervieweeId,
        internalPanelistId: myPanelistId,
        recommendation: recommendation as JobInterviewRecommendation,
        comments: comments || null,
        evaluationDate: new Date().toISOString(),
        scoreEntries: payloadEntries(),
      });
      await jobInterviewService.finalizeScore(saved.id);
    },
    onSuccess: () => {
      toast({ title: 'Scorecard signed off', description: 'It can no longer be changed.' });
      queryClient.invalidateQueries({ queryKey: ['hr', 'interview-scores', intervieweeId] });
      router.push(`/hr/recruitment/interviews/${interviewId}`);
    },
    onError: (error: any) =>
      toast({ title: 'Could not sign off', description: error?.message, variant: 'destructive' }),
  });

  if (interview.isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!data || !candidate) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="Scorecard" backHref={`/hr/recruitment/interviews/${interviewId}`} />
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>This candidate is not part of this interview.</AlertDescription>
        </Alert>
      </div>
    );
  }

  if (!myPanelist) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="Scorecard" backHref={`/hr/recruitment/interviews/${interviewId}`} />
        <Alert>
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>No panel seat</AlertTitle>
          <AlertDescription>
            {canManage
              ? 'Open a scorecard from the candidate list so it is attributed to the right panelist. HR can record on an external assessor’s behalf, but a scorecard always belongs to one named panelist.'
              : 'You are not on this interview’s panel, so you have no scorecard to fill in.'}
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  const locked = !!myCard?.isFinalized;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`Scorecard — ${candidate.candidateName}`}
        description={`${data.interviewNumber} · ${data.jobTitle} · scoring as ${myPanelist.employeeName}`}
        backHref={`/hr/recruitment/interviews/${interviewId}`}
      />

      {locked && (
        <Alert>
          <CheckCheck className="h-4 w-4" />
          <AlertTitle>Signed off</AlertTitle>
          <AlertDescription>
            This scorecard is final and can no longer be changed. It is shown here read-only.
          </AlertDescription>
        </Alert>
      )}

      {questions.length === 0 && (
        <Alert>
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>No question plan</AlertTitle>
          <AlertDescription>
            This interview has no drawn questions, so there is nothing to score against. Add a question
            plan to the interview first.
          </AlertDescription>
        </Alert>
      )}

      {(data.questions ?? []).map((plan) => (
        <Card key={plan.id}>
          <CardHeader>
            <CardTitle className="text-base">{plan.questionTypeName}</CardTitle>
            <CardDescription>
              Score at least {Math.min(plan.requiredQuestionCount, plan.selectedQuestions.length)} of these
              {plan.selectedQuestions.length > 0 ? ` ${plan.selectedQuestions.length}` : ''} to sign off.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-5">
            {plan.selectedQuestions.map((question, index) => {
              const entry = entries[question.questionDetailId] ?? { rawScore: '', remarks: '' };
              const raw = Number(entry.rawScore);
              const bad =
                entry.rawScore !== '' && (Number.isNaN(raw) || raw < question.minScore || raw > question.maxScore);

              return (
                <div key={question.id} className="space-y-2">
                  <div className="flex gap-3">
                    <span className="w-5 shrink-0 pt-2 text-sm text-muted-foreground">{index + 1}.</span>
                    <div className="flex-1 space-y-2">
                      <p className="text-sm">{question.questionText}</p>
                      <div className="flex flex-wrap items-center gap-3">
                        <div className="flex items-center gap-2">
                          <Label htmlFor={`score-${question.id}`} className="text-xs text-muted-foreground">
                            Mark ({question.minScore}–{question.maxScore})
                          </Label>
                          <Input
                            id={`score-${question.id}`}
                            type="number"
                            min={question.minScore}
                            max={question.maxScore}
                            disabled={locked}
                            className={`h-9 w-24 ${bad ? 'border-destructive' : ''}`}
                            value={entry.rawScore}
                            onChange={(e) =>
                              setEntries((prev) => ({
                                ...prev,
                                [question.questionDetailId]: { ...entry, rawScore: e.target.value },
                              }))
                            }
                          />
                        </div>
                        <span className="text-xs text-muted-foreground">weight {question.weight}</span>
                        {entry.rawScore !== '' && !bad && (
                          <span className="text-xs text-muted-foreground">
                            contributes {Math.round((raw / question.maxScore) * question.weight * 100) / 100}
                          </span>
                        )}
                        {bad && (
                          <span className="text-xs text-destructive">
                            Outside the {question.minScore}–{question.maxScore} band — this would be refused.
                          </span>
                        )}
                      </div>
                      <Textarea
                        rows={2}
                        disabled={locked}
                        placeholder="Notes on the answer (optional)"
                        value={entry.remarks}
                        onChange={(e) =>
                          setEntries((prev) => ({
                            ...prev,
                            [question.questionDetailId]: { ...entry, remarks: e.target.value },
                          }))
                        }
                      />
                    </div>
                  </div>
                </div>
              );
            })}
          </CardContent>
        </Card>
      ))}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Your verdict</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-3">
            <div className="rounded-md border p-3">
              <p className="text-sm text-muted-foreground">Questions scored</p>
              <p className="mt-1 text-2xl font-semibold">
                {scored.length}
                <span className="text-base font-normal text-muted-foreground">/{questions.length}</span>
              </p>
            </div>
            <div className="rounded-md border p-3">
              <p className="text-sm text-muted-foreground">Raw total</p>
              <p className="mt-1 text-2xl font-semibold">{runningTotals.raw}</p>
            </div>
            <div className="rounded-md border p-3">
              <p className="text-sm text-muted-foreground">Weighted total</p>
              <p className="mt-1 text-2xl font-semibold">
                {Math.round(runningTotals.weighted * 100) / 100}
              </p>
            </div>
          </div>

          <div className="space-y-2">
            <Label>
              Recommendation<span className="ml-0.5 text-red-500">*</span>
            </Label>
            <Select
              value={recommendation}
              disabled={locked}
              onValueChange={(v) => setRecommendation(v as JobInterviewRecommendation)}
            >
              <SelectTrigger className="sm:w-[280px]">
                <SelectValue placeholder="Choose a recommendation" />
              </SelectTrigger>
              <SelectContent>
                {INTERVIEW_RECOMMENDATIONS.map((r) => (
                  <SelectItem key={r} value={r}>
                    {humanizeEnum(r)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="cardComments">Comments</Label>
            <Textarea
              id="cardComments"
              rows={4}
              disabled={locked}
              placeholder="Your overall read on this candidate."
              value={comments}
              onChange={(e) => setComments(e.target.value)}
            />
          </div>

          {outOfBand.length > 0 && (
            <Alert variant="destructive">
              <AlertTriangle className="h-4 w-4" />
              <AlertDescription>
                {outOfBand.length} mark{outOfBand.length === 1 ? ' is' : 's are'} outside the question&rsquo;s
                band. The whole scorecard would be refused.
              </AlertDescription>
            </Alert>
          )}

          {shortSections.length > 0 && (
            <Alert>
              <AlertTriangle className="h-4 w-4" />
              <AlertDescription>
                Not enough scored to sign off yet:{' '}
                {shortSections.map((s) => `${s.name} (${s.answered} of ${s.required})`).join(', ')}. You can
                still save a draft.
              </AlertDescription>
            </Alert>
          )}

          {myCard && !locked && (
            <p className="text-sm text-muted-foreground">
              You already have a saved scorecard for this candidate —{' '}
              <StatusBadge status="Draft" /> saving again replaces it rather than adding another.
            </p>
          )}
        </CardContent>
      </Card>

      {!locked && (
        <div className="flex flex-wrap justify-end gap-3">
          <Button
            variant="outline"
            disabled={saveDraft.isPending}
            onClick={() => saveDraft.mutate()}
          >
            {saveDraft.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-2 h-4 w-4" />
            )}
            Save draft
          </Button>
          <Button
            variant="outline"
            disabled={submit.isPending || !recommendation || outOfBand.length > 0}
            onClick={() => submit.mutate()}
          >
            {submit.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Save scorecard
          </Button>
          <Button
            disabled={
              finalize.isPending || !recommendation || outOfBand.length > 0 || shortSections.length > 0
            }
            onClick={() => finalize.mutate()}
          >
            {finalize.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <CheckCheck className="mr-2 h-4 w-4" />
            )}
            Sign off
          </Button>
        </div>
      )}
    </div>
  );
}
