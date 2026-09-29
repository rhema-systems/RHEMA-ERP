'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CheckCheck, ClipboardPen, Loader2, Printer, Save } from 'lucide-react';
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

export interface InterviewScorecardFormProps {
  interviewId: string;
  intervieweeId: string;
  /**
   * The panelist seat to file as. HR passes one when opening somebody else's card; a panelist
   * filing their own can leave it null and the form finds their seat from the token's employee id.
   */
  panelistIdFromQuery?: string | null;
  /** Where the back arrow goes — HR's interview desk, or the panelist's own worklist. */
  backHref: string;
  /** Where to land after signing off. Defaults to `backHref`. */
  afterSignOffHref?: string;
}

/**
 * One panelist's scorecard for one candidate.
 *
 * ⚠ **This is the ONLY scorecard form**, rendered by two thin routes: HR's at
 * `/hr/recruitment/interviews/[id]/score/[intervieweeId]` and the panelist's own at
 * `/me/panel/[interviewId]/score/[intervieweeId]`. It was extracted into a component rather than
 * copied, and that is the whole point — the decision this replaces
 * (recorded on `/me/panel`) warned that a portal scorecard would mean *"two scorecard forms against
 * one upsert endpoint, which is how a scorecard gets silently replaced"*. Two **routes** onto one
 * form carry none of that risk; two **implementations** would. Do not fork this file.
 *
 * ⚠ **The running total is computed here with the same arithmetic the server uses** —
 * `(mark ÷ top of band) × weight` — deliberately duplicated rather than waiting for the server's
 * number. Two independent computations make a disagreement visible instead of silent, which is the
 * lesson from the appraisal scoring model. If the total shown here ever differs from the one that
 * comes back on save, that is a bug worth chasing, not a rounding artefact.
 *
 * Marks are held as strings until submit so a half-typed value does not read as a real 0.
 */
export function InterviewScorecardForm({
  interviewId,
  intervieweeId,
  panelistIdFromQuery = null,
  backHref,
  afterSignOffHref,
}: InterviewScorecardFormProps) {
  const router = useRouter();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const { hasAnyRole, user } = useAuth();

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

  // Every seat on the panel, internal and external, as one list. External assessors have no login,
  // so their card can only ever be filed by HR — and until lane F4 this screen could not reach one
  // at all, although the server has always permitted it and the empty-state text below promised it.
  const seats = useMemo(
    () => [
      ...(data?.panelists ?? []).map((p) => ({
        id: p.id,
        kind: 'internal' as const,
        name: p.employeeName,
        role: humanizeEnum(p.role),
        employeeId: p.employeeId as string | null,
      })),
      ...(data?.externalPanelists ?? []).map((p) => ({
        id: p.id,
        kind: 'external' as const,
        name: p.associateName,
        role: p.associateOrganization
          ? `${humanizeEnum(p.role)} · ${p.associateOrganization}`
          : humanizeEnum(p.role),
        employeeId: null,
      })),
    ],
    [data],
  );

  const myEmployeeId = (user as any)?.employeeId as string | undefined;
  const ownSeat = seats.find((s) => s.kind === 'internal' && s.employeeId === myEmployeeId);

  // HR arriving from the candidate list without a seat in the query string picks one here rather
  // than hitting the dead end this screen used to be.
  const [pickedSeatId, setPickedSeatId] = useState<string>('');

  // Whose card this is: the query string when HR arrived from the candidate list, then an explicit
  // pick, then the caller's own panel seat. The server refuses anything else, so this only decides
  // what to load.
  const seat =
    seats.find((s) => s.id === panelistIdFromQuery) ??
    seats.find((s) => s.id === pickedSeatId) ??
    ownSeat;

  // Kept under the old name so the rest of the screen reads unchanged; it is now whichever seat was
  // resolved, which may be an external assessor.
  const myPanelist = seat;

  // The two ids the API takes. Exactly one is ever non-null — the server refuses both.
  const internalPanelistId = seat?.kind === 'internal' ? seat.id : null;
  const externalPanelistId = seat?.kind === 'external' ? seat.id : null;

  // Filing somebody else's card. The server records this on the row whatever the screen says; the
  // banner exists so the person doing it knows before they start, not after.
  const filingOnBehalf = !!seat && seat.id !== ownSeat?.id;

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

  // ⚠ Matched on the seat's OWN side. Comparing only `internalPanelistId` — as this did before
  // external seats were reachable — makes an external assessor's existing card invisible, and the
  // screen would then post a second one against a row the server upserts by panelist.
  const myCard = existingCards.data?.find((c) =>
    seat?.kind === 'external' ? c.externalPanelistId === seat.id : c.internalPanelistId === seat?.id,
  );

  // Held as plain values so the queries and mutations below never need a non-null assertion — the
  // screen refuses to render its form at all without a panel seat.
  const myPanelistId = myPanelist?.id ?? null;
  const myCardId = myCard?.id ?? null;

  const draft = useQuery({
    queryKey: ['hr', 'interview-draft', interviewId, intervieweeId, myPanelistId],
    queryFn: () =>
      jobInterviewService.getScoreDraft(interviewId, intervieweeId, {
        internalPanelistId,
        externalPanelistId,
      }),
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

  // ⚠ Switching seats must re-seed. The form is hydrated exactly once, so without this an HR user
  // who picks a second panelist keeps the first one's marks on screen — and pressing Save would
  // file them as the second panelist's verdict. Clearing here is not enough on its own: the effect
  // above also re-runs because `hydrated` went false, and it re-seeds from the new seat's card.
  useEffect(() => {
    setHydrated(false);
    setEntries({});
    setComments('');
    setRecommendation('');
  }, [myPanelistId]);

  // A question the seeding effect has not reached yet has no entry at all, so read the mark through
  // a default: entries[id]?.rawScore alone yields undefined, which is not the empty string, and every
  // unscored question would then fall into scored for the reduce below to dereference.
  const scored = questions.filter((q) => (entries[q.questionDetailId]?.rawScore ?? '') !== '');
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
        (q) => (entries[q.questionDetailId]?.rawScore ?? '') !== '',
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
        internalPanelistId,
        externalPanelistId,
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
        internalPanelistId,
        externalPanelistId,
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
        internalPanelistId,
        externalPanelistId,
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
      router.push(afterSignOffHref ?? backHref);
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
        <PageHeader title="Scorecard" backHref={backHref} />
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
        <PageHeader
          title={`Scorecard — ${candidate.candidateName}`}
          description={`${data.interviewNumber} · ${data.jobTitle}`}
          backHref={backHref}
        />
        {canManage && seats.length > 0 ? (
          // ⚠ This used to be a dead end: HR who arrived without `?panelistId=` was told to go back
          // and open the card from somewhere else, and an external assessor's card — which only HR
          // can ever file — was unreachable from any screen at all. A scorecard still belongs to
          // one named panelist; it is just named here instead of in a URL.
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Whose scorecard is this?</CardTitle>
              <CardDescription>
                A scorecard always belongs to one named panelist. Filing one for somebody else is
                recorded on the card, so the audit trail never claims they typed it themselves.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-2">
              <Label htmlFor="seatPicker">Panelist</Label>
              <Select value={pickedSeatId} onValueChange={setPickedSeatId}>
                <SelectTrigger id="seatPicker" className="sm:w-[380px]">
                  <SelectValue placeholder="Choose the panelist whose sheet you are entering" />
                </SelectTrigger>
                <SelectContent>
                  {seats.map((s) => (
                    <SelectItem key={s.id} value={s.id}>
                      {s.name} — {s.role}
                      {s.kind === 'external' ? ' (external)' : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </CardContent>
          </Card>
        ) : (
          <Alert>
            <AlertTriangle className="h-4 w-4" />
            <AlertTitle>No panel seat</AlertTitle>
            <AlertDescription>
              {canManage
                ? 'This interview has no panel yet, so there is no scorecard to attribute. Add panelists to the interview first.'
                : 'You are not on this interview’s panel, so you have no scorecard to fill in.'}
            </AlertDescription>
          </Alert>
        )}
      </div>
    );
  }

  const locked = !!myCard?.isFinalized;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`Scorecard — ${candidate.candidateName}`}
        description={`${data.interviewNumber} · ${data.jobTitle} · scoring as ${myPanelist.name}`}
        backHref={backHref}
        actions={
          // The paper counterpart of this screen, narrowed to this seat and this candidate — which
          // is the sheet whoever is typing here most likely has in front of them.
          <Button variant="outline" asChild>
            <Link
              href={`/hr/recruitment/interviews/${interviewId}/paper?variant=ScoreSheet&panelistId=${myPanelist.id}&intervieweeIds=${intervieweeId}`}
            >
              <Printer className="mr-1.5 h-4 w-4" />
              Print this sheet
            </Link>
          </Button>
        }
      />

      {filingOnBehalf && (
        <Alert>
          <ClipboardPen className="h-4 w-4" />
          <AlertTitle>Filing {myPanelist.name}&rsquo;s scorecard</AlertTitle>
          <AlertDescription>
            <p>
              This is how a sheet the panel marked on paper gets into the system: enter the marks and
              the recommendation exactly as {myPanelist.name} wrote them, and keep the signed sheet.
            </p>
            <p className="mt-1">
              The card will be recorded as filed by you on their behalf — it stays their verdict, but
              the trail will not claim they typed it.
            </p>
            {canManage && seats.length > 1 && !panelistIdFromQuery && (
              <div className="mt-3 max-w-sm">
                <Select value={pickedSeatId || myPanelist.id} onValueChange={setPickedSeatId}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {seats.map((s) => (
                      <SelectItem key={s.id} value={s.id}>
                        {s.name} — {s.role}
                        {s.kind === 'external' ? ' (external)' : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
          </AlertDescription>
        </Alert>
      )}

      {myCard?.filedOnBehalfNote && (
        <Alert>
          <ClipboardPen className="h-4 w-4" />
          <AlertDescription>{myCard.filedOnBehalfNote}.</AlertDescription>
        </Alert>
      )}

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
                      {question.scoringGuide && (
                        // Shown here as well as on the printed sheet. The guide exists because a
                        // panelist who did not write the question cannot otherwise tell what the top
                        // of the band is worth — that is as true on screen as it is on paper.
                        <p className="rounded-md bg-muted/50 px-3 py-2 text-xs text-muted-foreground">
                          <span className="font-medium">What a good answer sounds like:</span>{' '}
                          {question.scoringGuide}
                        </p>
                      )}
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
