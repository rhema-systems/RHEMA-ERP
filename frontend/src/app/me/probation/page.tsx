'use client';

/**
 * Area 25 slice 7 — My Probation: the reviews of the caller's own probation, and the
 * acknowledgement act (re-homed from the desk reviews page's "About me" tab, D3).
 *
 * Token-derived (`reviews/mine`) and permission-free by design: a probation review is
 * conducted by the line manager and acknowledged by the employee, and neither holds an HR
 * permission — the reviewer's queue stays on the desk at /hr/probation/reviews.
 * Acknowledgement is testimony (you have SEEN it, not that you agree); the response box is
 * where disagreement goes, and the server admits only the employee the review is about.
 */

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ClipboardCheck, Loader2, Send } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { probationService } from '@/services/hr/probation.service';
import type { ProbationReview } from '@/types/hr/probation';
import { toast } from 'sonner';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function MyProbationPage() {
  const qc = useQueryClient();
  const [acknowledging, setAcknowledging] = useState<ProbationReview | null>(null);

  const { data: mine, isLoading } = useQuery({
    queryKey: ['me', 'probation', 'reviews', 'mine'],
    queryFn: () => probationService.getMyReviews(),
  });

  const rows = mine ?? [];
  const awaiting = rows.filter((r) => !r.employeeAcknowledged && r.recommendationName);

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Probation"
        description="Reviews of your own probation, and your acknowledgement of each."
        backHref="/me"
      />

      <Card>
        <CardHeader>
          <CardTitle>Reviews about you</CardTitle>
          <CardDescription>
            {awaiting.length > 0
              ? `${awaiting.length} review${awaiting.length === 1 ? '' : 's'} waiting for your acknowledgement.`
              : 'A review appears here once it has been scheduled on your probation.'}
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {isLoading ? (
            <div className="flex justify-center p-8">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={ClipboardCheck}
              title="No reviews about you"
              description="You are not currently on probation, or no review has been scheduled yet."
            />
          ) : (
            rows.map((r) => (
              <div key={r.id} className="space-y-2 rounded-md border p-3">
                <div className="flex flex-wrap items-center gap-3">
                  <div className="flex-1">
                    <p className="font-medium">Review {r.reviewNumber}</p>
                    <p className="text-xs text-muted-foreground">
                      {r.actualDate ? `Held ${fmtDate(r.actualDate)}` : `Due ${fmtDate(r.scheduledDate)}`}
                      {r.reviewedByName ? ` · by ${r.reviewedByName}` : ''}
                    </p>
                  </div>
                  {r.recommendationName && <Badge variant="outline">{r.recommendationName}</Badge>}
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

                {(r.performanceRatingName || r.conductRatingName || r.attitudeRatingName) && (
                  <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
                    {r.performanceRatingName && <span>Performance: {r.performanceRatingName}</span>}
                    {r.conductRatingName && <span>Conduct: {r.conductRatingName}</span>}
                    {r.attitudeRatingName && <span>Attitude: {r.attitudeRatingName}</span>}
                  </div>
                )}
                {r.strengthsObserved && (
                  <p className="text-sm">
                    <span className="text-muted-foreground">Strengths: </span>
                    {r.strengthsObserved}
                  </p>
                )}
                {r.areasForImprovement && (
                  <p className="text-sm">
                    <span className="text-muted-foreground">To improve: </span>
                    {r.areasForImprovement}
                  </p>
                )}
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

      <AcknowledgeDialog
        review={acknowledging}
        onClose={() => setAcknowledging(null)}
        onDone={() => void qc.invalidateQueries({ queryKey: ['me', 'probation'] })}
      />
    </div>
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
      setResponse('');
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
