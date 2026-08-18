'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, ClipboardCheck, Loader2, PenLine, Send } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Input } from '@/components/ui/input';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { probationService } from '@/services/hr/probation.service';
import type {
  ProbationPerformanceRating,
  ProbationReview,
  ProbationReviewRecommendation,
} from '@/types/hr/probation';
import { toast } from 'sonner';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const RATINGS: ProbationPerformanceRating[] = [
  'Outstanding',
  'ExceedsExpectations',
  'MeetsExpectations',
  'BelowExpectations',
  'Unsatisfactory',
];

const RECOMMENDATIONS: ProbationReviewRecommendation[] = [
  'Confirm',
  'Extend',
  'Terminate',
  'ContinueMonitoring',
];

/**
 * A reviewer's queue, and the employee's own.
 *
 * ⚠ **Both tabs are token-derived, and they have to be.** The browser holds no employee id for the
 * signed-in user — the client `User` type carries roles and tenants but no employee link — so a
 * screen cannot call the by-id reviewer endpoint at all. Building this page is what surfaced that;
 * `reviews/to-conduct` exists because of it.
 *
 * ⚠ **Neither tab needs an HR permission.** A probation review is conducted by the employee's line
 * manager and acknowledged by the employee, and neither holds one. Entitlement is read off the
 * record by the API.
 */
export default function ProbationReviewsPage() {
  const qc = useQueryClient();
  const [submitting, setSubmitting] = useState<ProbationReview | null>(null);
  const [acknowledging, setAcknowledging] = useState<ProbationReview | null>(null);

  const { data: toConduct, isLoading: loadingQueue } = useQuery({
    queryKey: ['probation-reviews-to-conduct'],
    queryFn: () => probationService.getMyReviewerQueue(),
  });

  const { data: mine, isLoading: loadingMine } = useQuery({
    queryKey: ['probation-reviews-mine'],
    queryFn: () => probationService.getMyReviews(),
  });

  const refresh = () => {
    void qc.invalidateQueries({ queryKey: ['probation-reviews-to-conduct'] });
    void qc.invalidateQueries({ queryKey: ['probation-reviews-mine'] });
  };

  const complete = useMutation({
    mutationFn: (reviewId: string) => probationService.completeReview(reviewId),
    onSuccess: () => {
      toast.success('Review completed');
      refresh();
    },
    onError: (e: unknown) => toast.error(e instanceof Error ? e.message : 'Refused'),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Probation reviews"
        description="Reviews you have been asked to conduct, and reviews of your own probation."
        backHref="/hr/probation"
      />

      <Tabs defaultValue="conduct">
        <TabsList>
          <TabsTrigger value="conduct">To conduct ({toConduct?.length ?? 0})</TabsTrigger>
          <TabsTrigger value="mine">About me ({mine?.length ?? 0})</TabsTrigger>
        </TabsList>

        <TabsContent value="conduct">
          <Card>
            <CardContent className="space-y-3 p-4">
              {loadingQueue ? (
                <div className="flex justify-center p-8">
                  <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                </div>
              ) : !toConduct || toConduct.length === 0 ? (
                <EmptyState
                  icon={ClipboardCheck}
                  title="Nothing to conduct"
                  description="You have not been named as the reviewer on any probation review."
                />
              ) : (
                toConduct.map((r) => (
                  <div key={r.id} className="flex flex-wrap items-center gap-3 rounded-md border p-3">
                    <div className="min-w-[14rem] flex-1">
                      <p className="font-medium">{r.employeeName}</p>
                      <p className="text-xs text-muted-foreground">
                        Review {r.reviewNumber} · due {fmtDate(r.scheduledDate)}
                        {r.employeeNumber ? ` · ${r.employeeNumber}` : ''}
                      </p>
                    </div>
                    <Badge variant="secondary">{r.statusName}</Badge>
                    {/* ⚠ A review with no recommendation has not been conducted, whatever its
                        status says — so "Record" and "Complete" are offered on that, not on the
                        status alone. The API refuses completing an empty review. */}
                    {r.recommendationName ? (
                      <Badge variant="outline">{r.recommendationName}</Badge>
                    ) : null}
                    <div className="flex gap-2">
                      <Button size="sm" variant="outline" onClick={() => setSubmitting(r)}>
                        <PenLine className="mr-2 h-4 w-4" />
                        {r.recommendationName ? 'Amend' : 'Record'}
                      </Button>
                      {r.recommendationName && r.statusName !== 'Completed' && (
                        <Button size="sm" onClick={() => complete.mutate(r.id)}>
                          <CheckCircle2 className="mr-2 h-4 w-4" />
                          Complete
                        </Button>
                      )}
                      <Button size="sm" variant="ghost" asChild>
                        <Link href={`/hr/probation/${r.probationPeriodId}`}>Open</Link>
                      </Button>
                    </div>
                  </div>
                ))
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="mine">
          <Card>
            <CardContent className="space-y-3 p-4">
              {loadingMine ? (
                <div className="flex justify-center p-8">
                  <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                </div>
              ) : !mine || mine.length === 0 ? (
                <EmptyState
                  icon={ClipboardCheck}
                  title="No reviews about you"
                  description="You are not currently on probation, or no review has been scheduled yet."
                />
              ) : (
                mine.map((r) => (
                  <div key={r.id} className="space-y-2 rounded-md border p-3">
                    <div className="flex flex-wrap items-center gap-3">
                      <div className="flex-1">
                        <p className="font-medium">Review {r.reviewNumber}</p>
                        <p className="text-xs text-muted-foreground">
                          {r.actualDate ? `Held ${fmtDate(r.actualDate)}` : `Due ${fmtDate(r.scheduledDate)}`}
                          {r.reviewedByName ? ` · by ${r.reviewedByName}` : ''}
                        </p>
                      </div>
                      {r.employeeAcknowledged ? (
                        <Badge variant="secondary">Acknowledged</Badge>
                      ) : r.recommendationName ? (
                        <Button size="sm" onClick={() => setAcknowledging(r)}>
                          <Send className="mr-2 h-4 w-4" />
                          Acknowledge
                        </Button>
                      ) : (
                        <Badge variant="outline">Not yet held</Badge>
                      )}
                    </div>
                    {r.reviewerComments && (
                      <p className="whitespace-pre-wrap text-sm">{r.reviewerComments}</p>
                    )}
                    {r.employeeResponse && (
                      <p className="whitespace-pre-wrap rounded bg-muted p-2 text-sm">
                        <span className="text-muted-foreground">Your response: </span>
                        {r.employeeResponse}
                      </p>
                    )}
                  </div>
                ))
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      <SubmitReviewDialog review={submitting} onClose={() => setSubmitting(null)} onDone={refresh} />
      <AcknowledgeDialog review={acknowledging} onClose={() => setAcknowledging(null)} onDone={refresh} />
    </div>
  );
}

function SubmitReviewDialog({
  review,
  onClose,
  onDone,
}: {
  review: ProbationReview | null;
  onClose: () => void;
  onDone: () => void;
}) {
  const [performance, setPerformance] = useState<ProbationPerformanceRating | ''>('');
  const [conduct, setConduct] = useState<ProbationPerformanceRating | ''>('');
  const [attitude, setAttitude] = useState<ProbationPerformanceRating | ''>('');
  const [strengths, setStrengths] = useState('');
  const [improvements, setImprovements] = useState('');
  const [comments, setComments] = useState('');
  const [recommendation, setRecommendation] = useState<ProbationReviewRecommendation | ''>('');
  const [months, setMonths] = useState('');

  const submit = useMutation({
    mutationFn: (r: ProbationReview) =>
      probationService.submitReview(r.id, {
        reviewId: r.id,
        actualDate: new Date().toISOString().slice(0, 10),
        performanceRating: performance || undefined,
        conductRating: conduct || undefined,
        attitudeRating: attitude || undefined,
        strengthsObserved: strengths.trim() || undefined,
        areasForImprovement: improvements.trim() || undefined,
        reviewerComments: comments.trim() || undefined,
        recommendation: recommendation as ProbationReviewRecommendation,
        // ⚠ Only sent for Extend. The API stores it for that recommendation alone, so sending it
        // otherwise would record a proposal nobody made.
        proposedExtensionMonths: recommendation === 'Extend' && months ? Number(months) : undefined,
      }),
    onSuccess: () => {
      toast.success('Assessment recorded');
      onDone();
      onClose();
    },
    onError: (e: unknown) => toast.error(e instanceof Error ? e.message : 'Refused'),
  });

  if (!review) return null;

  // The API requires a recommendation, and requires months when it is Extend. Mirroring both here
  // saves a round trip, but the API remains the authority — its refusal is shown if it disagrees.
  const canSubmit =
    !!recommendation && (recommendation !== 'Extend' || !!months) && !submit.isPending;

  return (
    <Dialog open onOpenChange={onClose}>
      <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>
            {review.employeeName} — review {review.reviewNumber}
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          <div className="grid gap-3 sm:grid-cols-3">
            {([
              ['Performance', performance, setPerformance],
              ['Conduct', conduct, setConduct],
              ['Attitude', attitude, setAttitude],
            ] as const).map(([label, value, set]) => (
              <div key={label} className="space-y-2">
                <Label>{label}</Label>
                <Select value={value} onValueChange={(v) => set(v as ProbationPerformanceRating)}>
                  <SelectTrigger>
                    <SelectValue placeholder="—" />
                  </SelectTrigger>
                  <SelectContent>
                    {RATINGS.map((r) => (
                      <SelectItem key={r} value={r}>
                        {r}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            ))}
          </div>

          <div className="space-y-2">
            <Label>Strengths observed</Label>
            <Textarea value={strengths} onChange={(e) => setStrengths(e.target.value)} rows={2} />
          </div>
          <div className="space-y-2">
            <Label>Areas for improvement</Label>
            <Textarea
              value={improvements}
              onChange={(e) => setImprovements(e.target.value)}
              rows={2}
            />
          </div>
          <div className="space-y-2">
            <Label>Comments</Label>
            <Textarea value={comments} onChange={(e) => setComments(e.target.value)} rows={3} />
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>Recommendation (required)</Label>
              <Select
                value={recommendation}
                onValueChange={(v) => setRecommendation(v as ProbationReviewRecommendation)}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Choose" />
                </SelectTrigger>
                <SelectContent>
                  {RECOMMENDATIONS.map((r) => (
                    <SelectItem key={r} value={r}>
                      {r}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {recommendation === 'Extend' && (
              <div className="space-y-2">
                <Label>Months proposed (required)</Label>
                <Input
                  type="number"
                  min={1}
                  max={24}
                  value={months}
                  onChange={(e) => setMonths(e.target.value)}
                />
              </div>
            )}
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button disabled={!canSubmit} onClick={() => submit.mutate(review)}>
            {submit.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Record assessment
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function AcknowledgeDialog({
  review,
  onClose,
  onDone,
}: {
  review: ProbationReview | null;
  onClose: () => void;
  onDone: () => void;
}) {
  const [response, setResponse] = useState('');

  const ack = useMutation({
    mutationFn: (r: ProbationReview) =>
      probationService.acknowledgeReview(r.id, {
        reviewId: r.id,
        employeeResponse: response.trim() || undefined,
      }),
    onSuccess: () => {
      toast.success('Acknowledged');
      onDone();
      onClose();
    },
    onError: (e: unknown) => toast.error(e instanceof Error ? e.message : 'Refused'),
  });

  if (!review) return null;

  return (
    <Dialog open onOpenChange={onClose}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Acknowledge review {review.reviewNumber}</DialogTitle>
        </DialogHeader>
        <div className="space-y-3">
          {/* Acknowledgement is testimony: it says the employee has SEEN the review, not that they
              agree with it. The wording says so, and the response box is where disagreement goes. */}
          <p className="text-sm text-muted-foreground">
            This records that you have seen this review. It does not record agreement — use the box
            below if you want your own comments on the record.
          </p>
          <div className="space-y-2">
            <Label>Your response (optional)</Label>
            <Textarea value={response} onChange={(e) => setResponse(e.target.value)} rows={4} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button disabled={ack.isPending} onClick={() => ack.mutate(review)}>
            {ack.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            I have seen this review
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
