'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ClipboardCheck, Info, Loader2, Star, Trophy } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { awardsService } from '@/services/hr/awards.service';
import type { AwardPendingReview } from '@/types/hr/awards';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * What a committee member owes a score on, and what they have already scored (AWD-12, AWD-13).
 *
 * ⚠ **Committee members hold no HR permission**, so for most of them this is the only awards screen
 * they will ever open. It is behind no permission gate: membership of the committee a nomination was
 * assigned to is the entitlement, and it is read off the record.
 *
 * ⚠ **A score, not a verdict.** Until slice 6 this carried `approved: bool?`, which cannot express
 * "the highest average wins". The desk has no route to score on a member's behalf, deliberately.
 *
 * Area 25 slice 9: re-homed from /hr/awards/me/reviews (D3). The committee-result read is now
 * cycle-scoped server-side — a review in one cycle no longer opens every other cycle's scores.
 */
export default function MyReviewsPage() {
  const queryClient = useQueryClient();
  const [scoring, setScoring] = useState<AwardPendingReview | null>(null);
  const [score, setScore] = useState('');
  const [comments, setComments] = useState('');
  const [resultCycleId, setResultCycleId] = useState('');

  const { data: pending, isLoading } = useQuery({
    queryKey: ['me', 'awards', 'pending-reviews'],
    queryFn: () => awardsService.getMyPendingReviews(),
  });

  const { data: given } = useQuery({
    queryKey: ['me', 'awards', 'reviews-given'],
    queryFn: () => awardsService.getMyReviews(),
  });

  // The outcome of what this member scored. ⚠ A member who scores and can never see the result is
  // asked to do the work and denied the point of it - the route existed and nothing called it.
  const { data: outcome } = useQuery({
    queryKey: ['me', 'awards', 'committee-result', resultCycleId],
    queryFn: () => awardsService.getMyCommitteeResult(resultCycleId),
    enabled: Boolean(resultCycleId),
    retry: false,
  });

  const owed = pending ?? [];
  const scored = given ?? [];

  const submit = useMutation({
    mutationFn: () => {
      if (!scoring) throw new Error('No nomination selected.');
      return awardsService.scoreNomination(scoring.id, {
        score: Number(score),
        comments: comments.trim() || null,
      });
    },
    onSuccess: () => {
      toast.success('Score recorded.');
      setScoring(null);
      setScore('');
      setComments('');
      queryClient.invalidateQueries({ queryKey: ['me', 'awards', 'pending-reviews'] });
      queryClient.invalidateQueries({ queryKey: ['me', 'awards', 'reviews-given'] });
    },
    onError: (e: any) =>
      toast.error(e?.body?.detail || e?.body?.message || e?.message || 'The score was refused.'),
  });

  const scoreValue = Number(score);
  const scoreValid = score !== '' && Number.isFinite(scoreValue) && scoreValue >= 0 && scoreValue <= 100;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Nominations to score"
        description="What your award committee still owes a score on, and what you have already given."
        backHref="/me/awards"
      />

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <ClipboardCheck className="h-4 w-4" />
            Awaiting your score
          </CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : owed.length === 0 ? (
            <EmptyState
              icon={ClipboardCheck}
              title="Nothing owed"
              description="Nominations appear here once the awards desk assigns them to a committee you sit on. Somebody on no committee sees nothing here, which is correct rather than a fault."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Nominee</TableHead>
                  <TableHead>Award</TableHead>
                  <TableHead>Raised</TableHead>
                  <TableHead className="text-right">Action</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {owed.map((n) => (
                  <TableRow key={n.id}>
                    <TableCell className="font-medium">{n.nominationNumber}</TableCell>
                    <TableCell>{n.nomineeName || n.teamName || '—'}</TableCell>
                    <TableCell>{n.awardTypeName}</TableCell>
                    <TableCell>{fmtDate(n.nominationDate)}</TableCell>
                    <TableCell className="text-right">
                      <Button size="sm" variant="outline" onClick={() => setScoring(n)}>
                        <Star className="mr-2 h-4 w-4" />
                        Score
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {scored.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Star className="h-4 w-4" />
              Scores you have given
            </CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Nomination</TableHead>
                  <TableHead className="text-right">Score</TableHead>
                  <TableHead>Given</TableHead>
                  <TableHead>Comments</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {scored.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell className="font-medium">{r.nominationNumber}</TableCell>
                    <TableCell className="text-right">{r.score}</TableCell>
                    <TableCell>{fmtDate(r.reviewDate)}</TableCell>
                    <TableCell className="max-w-md truncate">{r.comments ?? '—'}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {scored.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Trophy className="h-4 w-4" />
              What your committee decided
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex flex-wrap items-center gap-3">
              <span className="text-sm text-muted-foreground">Cycle</span>
              <Input
                className="w-96"
                placeholder="Paste a cycle id to see its committee result"
                value={resultCycleId}
                onChange={(e) => setResultCycleId(e.target.value.trim())}
              />
            </div>

            {!resultCycleId ? (
              <p className="text-sm text-muted-foreground">
                Your committee&apos;s averages for a cycle you scored in. Ties are reported, never
                broken — who wins is a decision for people.
              </p>
            ) : !outcome ? (
              <p className="text-sm text-muted-foreground">
                Nothing scored in that cycle, or it is not one of your committee&apos;s.
              </p>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Nominee</TableHead>
                    <TableHead className="text-right">Average</TableHead>
                    <TableHead className="text-right">Reviewers</TableHead>
                    <TableHead>Counts?</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {outcome.scores.map((sc) => (
                    <TableRow key={sc.nominationId}>
                      <TableCell
                        className={sc.nominationId === outcome.winningNominationId ? 'font-medium' : ''}
                      >
                        {sc.nomineeName}
                      </TableCell>
                      <TableCell className="text-right">{sc.averageScore.toFixed(1)}</TableCell>
                      <TableCell className="text-right">{sc.reviewerCount}</TableCell>
                      <TableCell>
                        {sc.meetsMinimumReviewers ? 'yes' : (
                          <span className="text-xs text-muted-foreground">too few reviewers</span>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      )}

      <Dialog open={Boolean(scoring)} onOpenChange={(o) => !o && setScoring(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Score {scoring?.nominationNumber}</DialogTitle>
            <DialogDescription>
              {scoring?.nomineeName || scoring?.teamName} — {scoring?.awardTypeName}
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <Alert>
              <Info className="h-4 w-4" />
              <AlertDescription>
                The winner is the nominee with the highest average score across the committee. Your
                score is yours alone — nobody can enter it on your behalf.
              </AlertDescription>
            </Alert>

            <div className="space-y-2">
              <Label htmlFor="score">Score (0–100)</Label>
              <Input
                id="score"
                type="number"
                min={0}
                max={100}
                value={score}
                onChange={(e) => setScore(e.target.value)}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="comments">Comments (optional)</Label>
              <Textarea
                id="comments"
                rows={4}
                value={comments}
                onChange={(e) => setComments(e.target.value)}
              />
            </div>

            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => setScoring(null)}>
                Cancel
              </Button>
              <Button disabled={!scoreValid || submit.isPending} onClick={() => submit.mutate()}>
                {submit.isPending ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Star className="mr-2 h-4 w-4" />
                )}
                Record score
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
