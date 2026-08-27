'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, MessageSquareQuote, Star } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { useToast } from '@/hooks/use-toast';
import { formatDate } from '@/lib/hr/attendance-format';
import { trainingNominationService } from '@/services/hr/training-nomination.service';
import type { TrainingFeedback, TrainingNominationSummary } from '@/types/hr/training-delivery';
import { cn } from '@/lib/utils';

/**
 * Course feedback (area 25 slice 14) — the last of the area's owed residuals.
 *
 * Parked since slice 6 for a reason that was never about the screen: there was no self read. The
 * only feedback read was HR's aggregate over a whole course, which answers 403 for a trainee, so
 * a form could neither show what you said nor tell whether you had already said it. `feedback/mine`
 * closed that in slice 14, alongside the dedupe that matters more — the trainer's rating average
 * is credited on every submission, so a repeatable form was a repeatable vote.
 *
 * ⚠ **One feedback per course, and the server is the one enforcing it.** This screen hides the
 * form for a course already answered, but that is courtesy, not the rule: a second POST is a 422.
 *
 * Feedback is keyed by SCHEDULE, and completions do not carry a `scheduleId` — nominations do.
 * So the list of "courses I could give feedback on" is built from confirmed nominations, left
 * joined against what has already been said.
 */

/** Ratings are 1–5. Rendered as buttons rather than a select: it is a survey, not a form field. */
const SCALE = [1, 2, 3, 4, 5];

const QUESTIONS = [
  { key: 'contentRelevanceRating', label: 'How relevant was the content to your work?' },
  { key: 'trainerKnowledgeRating', label: 'How knowledgeable was the trainer?' },
  { key: 'deliveryMethodRating', label: 'How well was it delivered?' },
  { key: 'materialQualityRating', label: 'How good were the materials?' },
  { key: 'venueFacilitiesRating', label: 'How were the venue and facilities?' },
  { key: 'overallSatisfactionRating', label: 'Overall, how satisfied are you?' },
] as const;

type RatingKey = (typeof QUESTIONS)[number]['key'];
type Form = Partial<Record<RatingKey, number>> & {
  strengthsOfTraining: string;
  areasForImprovement: string;
  additionalComments: string;
  wouldRecommend: boolean;
};

const BLANK: Form = {
  strengthsOfTraining: '',
  areasForImprovement: '',
  additionalComments: '',
  wouldRecommend: false,
};

function Stars({ value }: { value?: number | null }) {
  if (value == null) return <span className="text-sm text-muted-foreground">—</span>;
  return (
    <span className="flex items-center gap-0.5" aria-label={`${value} out of 5`}>
      {SCALE.map((n) => (
        <Star
          key={n}
          className={cn('h-3.5 w-3.5', n <= value ? 'fill-amber-400 text-amber-400' : 'text-muted-foreground/30')}
        />
      ))}
    </span>
  );
}

export default function TrainingFeedbackPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [answering, setAnswering] = useState<TrainingNominationSummary | null>(null);
  const [form, setForm] = useState<Form>(BLANK);

  const nominations = useQuery({
    queryKey: ['me', 'training', 'nominations', 'mine'],
    queryFn: () => trainingNominationService.getMine(),
  });

  const given = useQuery({
    queryKey: ['me', 'training', 'feedback', 'mine'],
    queryFn: () => trainingNominationService.getMyFeedback(),
  });

  const answeredScheduleIds = useMemo(
    () => new Set((given.data ?? []).map((f) => f.scheduleId)),
    [given.data],
  );

  // Only courses you were actually placed on, and only those not already answered.
  const awaiting = useMemo(
    () =>
      (nominations.data ?? []).filter(
        (n) => n.status === 'Confirmed' && !answeredScheduleIds.has(n.scheduleId),
      ),
    [nominations.data, answeredScheduleIds],
  );

  const submit = useMutation({
    mutationFn: () => {
      if (!answering) throw new Error('No course selected.');
      return trainingNominationService.submitFeedback({
        scheduleId: answering.scheduleId,
        employeeId: answering.employeeId,
        nominationId: answering.id,
        contentRelevanceRating: form.contentRelevanceRating ?? null,
        trainerKnowledgeRating: form.trainerKnowledgeRating ?? null,
        deliveryMethodRating: form.deliveryMethodRating ?? null,
        materialQualityRating: form.materialQualityRating ?? null,
        venueFacilitiesRating: form.venueFacilitiesRating ?? null,
        overallSatisfactionRating: form.overallSatisfactionRating ?? null,
        strengthsOfTraining: form.strengthsOfTraining.trim() || null,
        areasForImprovement: form.areasForImprovement.trim() || null,
        additionalComments: form.additionalComments.trim() || null,
        wouldRecommend: form.wouldRecommend,
      });
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['me', 'training', 'feedback', 'mine'] });
      setAnswering(null);
      setForm(BLANK);
      toast({ title: 'Thank you', description: 'Your feedback has been recorded.' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not submit', description: e?.message, variant: 'destructive' }),
  });

  const loading = nominations.isLoading || given.isLoading;
  const answered = given.data ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="Course feedback"
        description="Tell us how your training went — and see what you have already said."
        backHref="/me/training"
      />

      {loading ? (
        <div className="space-y-3">
          <Skeleton className="h-28" />
          <Skeleton className="h-28" />
        </div>
      ) : (
        <>
          {/* ── Awaiting your feedback ─────────────────────────────────────── */}
          <section className="space-y-3">
            <h2 className="text-sm font-medium">Awaiting your feedback</h2>
            {awaiting.length === 0 ? (
              <Card>
                <CardContent className="py-8">
                  <EmptyState
                    icon={CheckCircle2}
                    title="Nothing to review"
                    description="When you are confirmed on a course, it appears here for your feedback once you have attended."
                  />
                </CardContent>
              </Card>
            ) : (
              <div className="space-y-2">
                {awaiting.map((n) => (
                  <Card key={n.id}>
                    <CardContent className="flex flex-wrap items-center gap-3 p-4">
                      <div className="min-w-0 flex-1">
                        <p className="truncate font-medium">{n.programName}</p>
                        <p className="text-xs text-muted-foreground">
                          {n.nominationNumber}
                          {n.trainingStartDate ? ` · ${formatDate(n.trainingStartDate)}` : ''}
                        </p>
                      </div>
                      <Button size="sm" onClick={() => { setAnswering(n); setForm(BLANK); }}>
                        Give feedback
                      </Button>
                    </CardContent>
                  </Card>
                ))}
              </div>
            )}
          </section>

          {/* ── What you have already said ─────────────────────────────────── */}
          <section className="space-y-3">
            <h2 className="text-sm font-medium">Feedback you have given</h2>
            {answered.length === 0 ? (
              <p className="text-sm text-muted-foreground">Nothing yet.</p>
            ) : (
              <div className="space-y-3">
                {answered.map((f: TrainingFeedback) => (
                  <Card key={f.id}>
                    <CardContent className="space-y-3 p-4">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-medium">{f.programName || f.scheduleNumber}</span>
                        {f.averageRating != null && (
                          <Badge variant="secondary">{f.averageRating.toFixed(2)} average</Badge>
                        )}
                        {f.wouldRecommend && <Badge variant="outline">Would recommend</Badge>}
                        <span className="ml-auto text-xs text-muted-foreground">
                          {formatDate(f.feedbackDate)}
                        </span>
                      </div>

                      <div className="grid gap-2 sm:grid-cols-2">
                        {QUESTIONS.map((q) => (
                          <div key={q.key} className="flex items-center justify-between gap-3">
                            <span className="text-xs text-muted-foreground">{q.label}</span>
                            <Stars value={f[q.key]} />
                          </div>
                        ))}
                      </div>

                      {(f.strengthsOfTraining || f.areasForImprovement || f.additionalComments) && (
                        <div className="space-y-1.5 border-t pt-3 text-sm">
                          {f.strengthsOfTraining && (
                            <p><span className="text-muted-foreground">Strengths: </span>{f.strengthsOfTraining}</p>
                          )}
                          {f.areasForImprovement && (
                            <p><span className="text-muted-foreground">To improve: </span>{f.areasForImprovement}</p>
                          )}
                          {f.additionalComments && (
                            <p className="whitespace-pre-line">{f.additionalComments}</p>
                          )}
                        </div>
                      )}
                    </CardContent>
                  </Card>
                ))}
              </div>
            )}
          </section>

          <p className="text-sm text-muted-foreground">
            Looking for your courses?{' '}
            <Link href="/me/training" className="text-primary hover:underline">
              My training
            </Link>
            .
          </p>
        </>
      )}

      {/* ── The form ───────────────────────────────────────────────────────── */}
      <Dialog open={!!answering} onOpenChange={(o) => !o && setAnswering(null)}>
        <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{answering?.programName}</DialogTitle>
            <DialogDescription>
              Every question is optional, but you can only submit this once — so say what you want
              to say now.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-5">
            {QUESTIONS.map((q) => (
              <div key={q.key} className="space-y-2">
                <Label>{q.label}</Label>
                <div className="flex gap-1.5">
                  {SCALE.map((n) => (
                    <Button
                      key={n}
                      type="button"
                      variant={form[q.key] === n ? 'default' : 'outline'}
                      size="sm"
                      className="h-9 w-9 p-0"
                      onClick={() =>
                        setForm((f) => ({ ...f, [q.key]: f[q.key] === n ? undefined : n }))
                      }
                    >
                      {n}
                    </Button>
                  ))}
                </div>
              </div>
            ))}

            <div className="space-y-1.5">
              <Label htmlFor="strengths">What worked well?</Label>
              <Textarea
                id="strengths"
                rows={2}
                value={form.strengthsOfTraining}
                onChange={(e) => setForm((f) => ({ ...f, strengthsOfTraining: e.target.value }))}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="improve">What could be better?</Label>
              <Textarea
                id="improve"
                rows={2}
                value={form.areasForImprovement}
                onChange={(e) => setForm((f) => ({ ...f, areasForImprovement: e.target.value }))}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="more">Anything else</Label>
              <Textarea
                id="more"
                rows={2}
                value={form.additionalComments}
                onChange={(e) => setForm((f) => ({ ...f, additionalComments: e.target.value }))}
              />
            </div>

            <div className="flex items-center gap-2">
              <Checkbox
                id="recommend"
                checked={form.wouldRecommend}
                onCheckedChange={(v) => setForm((f) => ({ ...f, wouldRecommend: v === true }))}
              />
              <Label htmlFor="recommend" className="font-normal">
                I would recommend this course to a colleague
              </Label>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setAnswering(null)} disabled={submit.isPending}>
              Cancel
            </Button>
            <Button onClick={() => submit.mutate()} disabled={submit.isPending}>
              <MessageSquareQuote className="mr-2 h-4 w-4" /> Submit feedback
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
