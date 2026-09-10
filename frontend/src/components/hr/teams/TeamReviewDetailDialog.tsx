'use client';

/**
 * One review, opened: the objective-by-objective rating that makes it a review rather than a note.
 *
 * ⚠ **A line snapshots the objective's progress AT THE TIME.** The server takes that figure itself —
 * the payload has no field for it — so a review cannot claim a percentage the objective never held,
 * and an old review still means something when the objective has since moved on.
 *
 * ⚠ **Once submitted, nothing here is editable, and the screen says so** rather than offering
 * controls the server would refuse. A review is a record of what somebody found; the lead
 * ACKNOWLEDGES it, which says it was read and nothing about whether they agreed.
 *
 * Round 2, lane F2 (plan § 6.6).
 */

import { useEffect, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Progress } from '@/components/ui/progress';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import { teamActivityService } from '@/services/hr/team-activity.service';
import { teamMeetingService } from '@/services/hr/team-meeting.service';
import { TEAM_REVIEW_STATUS_LABELS } from '@/types/hr/team-meeting';

/** Local, editable copy of one line. */
interface Draft {
  rating: string;
  comment: string;
}

export function TeamReviewDetailDialog({
  reviewId,
  teamId,
  onOpenChange,
  onChanged,
}: {
  reviewId: string | null;
  teamId: string;
  onOpenChange: (open: boolean) => void;
  onChanged: () => void;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [drafts, setDrafts] = useState<Record<string, Draft>>({});
  const [busy, setBusy] = useState<string | null>(null);

  const { data: review, isLoading } = useQuery({
    queryKey: ['hr', 'team-review', reviewId],
    queryFn: () => teamMeetingService.getReview(reviewId!),
    enabled: !!reviewId,
  });

  /**
   * ⚠ Every objective is listed, not only the ones already rated. A review that silently omits the
   * objective nobody wanted to talk about is the failure mode this screen exists to prevent, so the
   * unrated ones stay visible and are marked as such.
   */
  const { data: objectives = [] } = useQuery({
    queryKey: ['hr', 'teams', teamId, 'objectives'],
    queryFn: () => teamActivityService.getObjectives(teamId),
    enabled: !!reviewId,
  });

  useEffect(() => {
    if (!review) return;
    setDrafts(
      Object.fromEntries(
        review.lines.map((l) => [
          l.objectiveId,
          { rating: l.rating != null ? String(l.rating) : '', comment: l.comment ?? '' },
        ]),
      ),
    );
  }, [review]);

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['hr', 'team-review', reviewId] });
    onChanged();
  };

  const run = async (key: string, what: string, fn: () => Promise<unknown>) => {
    setBusy(key);
    try {
      await fn();
      refresh();
    } catch (e: unknown) {
      const err = e as { body?: { message?: string }; message?: string };
      toast({
        variant: 'destructive',
        title: `Could not ${what}`,
        description: err?.body?.message ?? err?.message ?? 'The server refused that.',
      });
    } finally {
      setBusy(null);
    }
  };

  const editable = review?.status === 'Draft';
  const lineFor = (objectiveId: string) => review?.lines.find((l) => l.objectiveId === objectiveId);

  return (
    <Dialog open={!!reviewId} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-[720px]">
        <DialogHeader>
          <DialogTitle>
            {review
              ? `Review ${review.periodStart.slice(0, 10)} → ${review.periodEnd.slice(0, 10)}`
              : 'Review'}
          </DialogTitle>
          <DialogDescription>
            {review?.reviewedByName ? `Reviewed by ${review.reviewedByName}` : 'Team review'}
          </DialogDescription>
        </DialogHeader>

        {isLoading || !review ? (
          <div className="flex justify-center py-8">
            <Loader2 className="h-5 w-5 animate-spin" />
          </div>
        ) : (
          <div className="space-y-5">
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant={review.status === 'Draft' ? 'outline' : 'default'}>
                {TEAM_REVIEW_STATUS_LABELS[review.status]}
              </Badge>
              {review.overallRating != null && (
                <span className="text-sm">Overall {review.overallRating}/5</span>
              )}
              {review.acknowledgedByName && (
                <span className="text-muted-foreground text-xs">
                  Acknowledged by {review.acknowledgedByName}
                  {review.acknowledgedOn ? ` on ${review.acknowledgedOn.slice(0, 10)}` : ''}
                </span>
              )}
            </div>

            {!editable && (
              <p className="text-muted-foreground text-sm">
                This review has been submitted, so it is now the record and cannot be changed.
              </p>
            )}

            {review.summary && (
              <div className="space-y-1">
                <Label className="text-xs uppercase tracking-wide">Summary</Label>
                <p className="whitespace-pre-wrap text-sm">{review.summary}</p>
              </div>
            )}

            {review.recommendations && (
              <div className="space-y-1">
                <Label className="text-xs uppercase tracking-wide">Recommendations</Label>
                <p className="whitespace-pre-wrap text-sm">{review.recommendations}</p>
              </div>
            )}

            {/* ── the objectives ────────────────────────────────────────── */}
            <div className="space-y-3 border-t pt-4">
              <Label className="text-xs uppercase tracking-wide">
                Objectives ({review.lines.length} of {objectives.length} rated)
              </Label>

              {objectives.length === 0 && (
                <p className="text-muted-foreground text-sm">
                  This team has no objectives, so there is nothing to rate. The summary carries the
                  review on its own.
                </p>
              )}

              {objectives.map((o) => {
                const line = lineFor(o.id);
                const draft = drafts[o.id] ?? { rating: '', comment: '' };
                return (
                  <div key={o.id} className="space-y-2 rounded-md border p-3">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="flex-1 text-sm font-medium">
                        {o.code ? `${o.code} · ` : ''}
                        {o.title}
                      </span>
                      {line ? (
                        <Badge variant="secondary">
                          {line.rating != null ? `${line.rating}/5` : 'Commented'}
                        </Badge>
                      ) : (
                        <Badge variant="outline">Not rated</Badge>
                      )}
                    </div>

                    <div className="flex items-center gap-3">
                      <Progress
                        className="h-1.5 flex-1"
                        value={line ? line.progressAtReview : o.progressPercent}
                      />
                      {/*
                        ⚠ Two different numbers, deliberately distinguished. A written line shows
                        what the objective stood at when it was written; an unwritten one shows
                        where the objective is now, which is what would be snapshotted.
                      */}
                      <span className="text-muted-foreground w-40 text-xs">
                        {line
                          ? `${line.progressAtReview}% at review`
                          : `${o.progressPercent}% now`}
                      </span>
                    </div>

                    {editable ? (
                      <div className="flex flex-wrap items-end gap-2">
                        <div className="space-y-1">
                          <Label htmlFor={`rating-${o.id}`} className="text-xs">
                            Rating (1–5)
                          </Label>
                          <Input
                            id={`rating-${o.id}`}
                            type="number"
                            min={1}
                            max={5}
                            className="h-8 w-24"
                            value={draft.rating}
                            onChange={(e) =>
                              setDrafts((d) => ({
                                ...d,
                                [o.id]: { ...draft, rating: e.target.value },
                              }))
                            }
                          />
                        </div>
                        <div className="min-w-[16rem] flex-1 space-y-1">
                          <Label htmlFor={`comment-${o.id}`} className="text-xs">
                            Comment
                          </Label>
                          <Textarea
                            id={`comment-${o.id}`}
                            rows={2}
                            value={draft.comment}
                            onChange={(e) =>
                              setDrafts((d) => ({
                                ...d,
                                [o.id]: { ...draft, comment: e.target.value },
                              }))
                            }
                          />
                        </div>
                        <Button
                          type="button"
                          size="sm"
                          disabled={busy === o.id || (!draft.rating && !draft.comment.trim())}
                          onClick={() =>
                            run(o.id, 'save that line', () =>
                              teamMeetingService.upsertReviewLine(review.id, {
                                objectiveId: o.id,
                                rating: draft.rating ? Number(draft.rating) : null,
                                comment: draft.comment || null,
                              }),
                            )
                          }
                        >
                          {busy === o.id && <Loader2 className="mr-2 h-3.5 w-3.5 animate-spin" />}
                          Save
                        </Button>
                        {line && (
                          <Button
                            type="button"
                            variant="ghost"
                            size="sm"
                            disabled={busy === o.id}
                            onClick={() =>
                              run(o.id, 'remove that line', async () => {
                                await teamMeetingService.deleteReviewLine(line.id);
                                setDrafts((d) => ({ ...d, [o.id]: { rating: '', comment: '' } }));
                              })
                            }
                          >
                            Remove
                          </Button>
                        )}
                      </div>
                    ) : (
                      line?.comment && <p className="text-sm">{line.comment}</p>
                    )}
                  </div>
                );
              })}
            </div>

            {editable && (
              <p className="text-muted-foreground border-t pt-4 text-xs">
                Lines save one at a time, so a long review does not have to be finished in one
                sitting. Submit it from the reviews list when it is ready.
              </p>
            )}
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
