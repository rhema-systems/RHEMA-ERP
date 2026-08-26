'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, ClipboardCheck, Loader2, PenLine } from 'lucide-react';
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
 * A reviewer's queue — the reviews the caller has been asked to conduct.
 *
 * ⚠ **Token-derived, and it has to be.** The browser holds no employee id for the
 * signed-in user — the client `User` type carries roles and tenants but no employee link — so a
 * screen cannot call the by-id reviewer endpoint at all. Building this page is what surfaced that;
 * `reviews/to-conduct` exists because of it.
 *
 * ⚠ **No HR permission needed.** A probation review is conducted by the employee's line
 * manager, who holds none. Entitlement is read off the record by the API.
 *
 * Area 25 slice 7: the "About me" tab (the employee's own reviews + acknowledgement)
 * re-homed to the portal at /me/probation — this page is the reviewer's side only.
 */
export default function ProbationReviewsPage() {
  const qc = useQueryClient();
  const [submitting, setSubmitting] = useState<ProbationReview | null>(null);

  const { data: toConduct, isLoading: loadingQueue } = useQuery({
    queryKey: ['probation-reviews-to-conduct'],
    queryFn: () => probationService.getMyReviewerQueue(),
  });

  const refresh = () => {
    void qc.invalidateQueries({ queryKey: ['probation-reviews-to-conduct'] });
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
        description="Reviews you have been asked to conduct. Reviews of your own probation live in your self-service portal."
        backHref="/hr/probation"
      />

      <Tabs defaultValue="conduct">
        <TabsList>
          <TabsTrigger value="conduct">To conduct ({toConduct?.length ?? 0})</TabsTrigger>
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

      </Tabs>

      <SubmitReviewDialog review={submitting} onClose={() => setSubmitting(null)} onDone={refresh} />
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
