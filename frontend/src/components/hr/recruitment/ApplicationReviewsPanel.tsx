'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCheck, Loader2, Plus, Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { useToast } from '@/hooks/use-toast';
import { formatDateTime } from '@/lib/hr/attendance-format';
import { jobApplicationService } from '@/services/hr/recruitment-pipeline.service';

/**
 * Panel scoring — several reviewers each score the application 0–100.
 *
 * ⚠ **Only finalized reviews count toward the aggregate**, and **only a review's own author may
 * finalize it** (anyone else gets 403). Finalizing is the reviewer committing their judgement, not a
 * clerical step, which is why the button is offered on every row but explained rather than hidden:
 * hiding it would make someone else's draft look like it had simply failed to save.
 */
export function ApplicationReviewsPanel({ applicationId }: { applicationId: string }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [adding, setAdding] = useState(false);
  const [score, setScore] = useState('');
  const [notes, setNotes] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'application-reviews', applicationId],
    queryFn: () => jobApplicationService.getAggregatedReview(applicationId),
    enabled: !!applicationId,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'application-reviews', applicationId] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'application', applicationId] });
  };

  const add = useMutation({
    mutationFn: () => jobApplicationService.addReview(applicationId, Number(score), notes.trim() || null),
    onSuccess: async () => {
      await refresh();
      setAdding(false);
      setScore('');
      setNotes('');
      toast({ title: 'Review added', description: 'Finalize it to count it toward the aggregate.' });
    },
    onError: (e: any) => toast({ title: 'Could not add', description: e?.message, variant: 'destructive' }),
  });

  const finalize = useMutation({
    mutationFn: (reviewId: string) => jobApplicationService.finalizeReview(reviewId),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Review finalized' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not finalize', description: e?.message, variant: 'destructive' }),
  });

  const reviews = data?.reviews ?? [];
  const numericScore = Number(score);
  const scoreValid = score !== '' && Number.isFinite(numericScore) && numericScore >= 0 && numericScore <= 100;

  return (
    <div className="space-y-4">
      <MetricTiles
        tiles={[
          {
            label: 'Aggregated score',
            value: data?.aggregatedScore != null ? data.aggregatedScore.toFixed(2) : '—',
            hint: 'Mean of finalized reviews only',
          },
          { label: 'Finalized reviews', value: data?.reviewerCount ?? 0 },
          { label: 'Reviews recorded', value: reviews.length },
        ]}
      />

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          ) : reviews.length === 0 ? (
            <EmptyState
              icon={Users}
              title="No panel reviews"
              description="Each reviewer scores independently; the aggregate is the mean of the finalized ones."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Reviewer</TableHead>
                  <TableHead className="w-24 text-right">Score</TableHead>
                  <TableHead>Notes</TableHead>
                  <TableHead className="w-40">Reviewed</TableHead>
                  <TableHead className="w-40 text-right">State</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {reviews.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell className="font-medium">{r.reviewerName || '—'}</TableCell>
                    <TableCell className="text-right tabular-nums">{r.score.toFixed(1)}</TableCell>
                    <TableCell className="text-sm text-muted-foreground">{r.notes ?? '—'}</TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {formatDateTime(r.reviewedAt)}
                    </TableCell>
                    <TableCell className="text-right">
                      {r.isFinalized ? (
                        <Badge variant="default" className="gap-1">
                          <CheckCheck className="h-3 w-3" />
                          Finalized
                        </Badge>
                      ) : (
                        <Button
                          variant="ghost"
                          size="sm"
                          disabled={finalize.isPending}
                          onClick={() => finalize.mutate(r.id)}
                          title="Only the reviewer who wrote this review can finalize it."
                        >
                          Finalize
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Button variant="outline" onClick={() => setAdding(true)}>
        <Plus className="mr-2 h-4 w-4" />
        Add my review
      </Button>

      <Dialog open={adding} onOpenChange={(o) => !o && setAdding(false)}>
        <DialogContent className="sm:max-w-[480px]">
          <DialogHeader>
            <DialogTitle>Add review</DialogTitle>
            <DialogDescription>
              Recorded against you as the reviewer. It does not count toward the aggregate until you
              finalize it.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="review-score">Score (0–100)</Label>
              <Input
                id="review-score"
                type="number"
                min={0}
                max={100}
                value={score}
                onChange={(e) => setScore(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="review-notes">Notes</Label>
              <Textarea id="review-notes" value={notes} onChange={(e) => setNotes(e.target.value)} rows={4} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAdding(false)}>
              Cancel
            </Button>
            <Button disabled={!scoreValid || add.isPending} onClick={() => add.mutate()}>
              {add.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Add
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
