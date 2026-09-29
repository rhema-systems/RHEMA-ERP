'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import { CheckCircle2, Lock, RotateCcw, Scale, TriangleAlert, XCircle } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { formatDate } from '@/lib/hr/attendance-format';
import { appraisalAppealService } from '@/services/hr/appeals.service';
import type { AppraisalAppealStatus, CriterionScoreModification } from '@/types/hr/appeals';

/**
 * HR's adjudication of one appeal.
 *
 * **The screen shows one of two things**, depending on where the appeal is. Before a remand it
 * is the review: the contested items with all three evaluation legs side by side, and the three
 * decisions. After a remand it is the comparison: what the manager scored before against what
 * they scored on re-evaluation, and a final Uphold/Reject.
 *
 * **Remand is not a verdict.** It rolls the appraisal back to Active, freezes a snapshot of the
 * manager's evaluation for the comparison, sets a re-evaluation deadline and notifies the
 * manager. Nothing is decided until they re-submit and HR rules again.
 *
 * **Score changes depend on the cycle, not on HR's judgement.** `hrCanModifyScores` comes from
 * the settings profile the cycle runs on; when it is false the server refuses modifications, so
 * the fields are not offered.
 */
type Decision = Extract<AppraisalAppealStatus, 'Upheld' | 'Rejected' | 'Remanded'>;

export default function AppealReviewPage() {
  const params = useParams<{ id: string }>();
  const appraisalId = params.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [decision, setDecision] = useState<Decision | null>(null);
  const [notes, setNotes] = useState('');
  const [scoreEdits, setScoreEdits] = useState<Record<string, string>>({});
  const [justifications, setJustifications] = useState<Record<string, string>>({});
  const [finalOpen, setFinalOpen] = useState(false);
  const [finalDecision, setFinalDecision] = useState<'Upheld' | 'Rejected'>('Upheld');
  const [finalNotes, setFinalNotes] = useState('');

  const review = useQuery({
    queryKey: ['hr', 'appeal-review', appraisalId],
    queryFn: () => appraisalAppealService.getAppealReview(appraisalId),
    enabled: !!appraisalId,
    retry: false,
  });

  const isRemanded = review.data?.status === 'Remanded';

  const postRemand = useQuery({
    queryKey: ['hr', 'post-remand-review', appraisalId],
    queryFn: () => appraisalAppealService.getPostRemandReview(appraisalId),
    enabled: !!appraisalId && isRemanded,
    retry: false,
  });

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['hr', 'appeal-review', appraisalId] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'post-remand-review', appraisalId] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'appeals'] });
  };

  const fail = (title: string) => (e: Error) =>
    toast({ title, description: e.message, variant: 'destructive' });

  const pickUp = useMutation({
    mutationFn: () => appraisalAppealService.beginReview(appraisalId),
    onSuccess: () => {
      toast({ title: 'Appeal picked up', description: 'It now shows as in review.' });
      refresh();
    },
    onError: fail('Could not pick up the appeal'),
  });

  // Keyed by criterion; a restatement names its row by both ids it has — a goal row has no
  // template item, and was keyed under an empty one.
  const modifications = useMemo<CriterionScoreModification[]>(() => {
    return (review.data?.appealedCriteria ?? [])
      .filter((c) => (scoreEdits[c.criterionKey] ?? '').trim() !== '')
      .map((c) => ({
        templateItemId: c.templateItemId ?? null,
        criterionConfigId: c.criterionConfigId ?? null,
        newScore: Number(scoreEdits[c.criterionKey]),
        justification: justifications[c.criterionKey]?.trim() || 'Adjusted on appeal',
      }));
  }, [review.data, scoreEdits, justifications]);

  const resolve = useMutation({
    // The decision is passed in rather than read off state, so the call cannot be made
    // without one.
    mutationFn: (resolutionDecision: Decision) =>
      appraisalAppealService.resolveAppeal(appraisalId, {
        resolutionDecision,
        resolutionNotes: notes.trim(),
        criteriaModifications: modifications.length ? modifications : null,
      }),
    onSuccess: () => {
      toast({
        title:
          decision === 'Remanded'
            ? 'Sent back to the manager'
            : decision === 'Upheld'
              ? 'Appeal upheld'
              : 'Appeal rejected',
        description:
          decision === 'Remanded'
            ? 'The manager has been asked to re-evaluate, with a deadline.'
            : 'The employee has been notified and can see the outcome.',
      });
      setDecision(null);
      setNotes('');
      setScoreEdits({});
      setJustifications({});
      refresh();
    },
    onError: fail('Could not record the decision'),
  });

  const finalize = useMutation({
    mutationFn: () =>
      appraisalAppealService.finalizePostRemand(appraisalId, {
        finalDecision,
        hrFinalNotes: finalNotes.trim(),
      }),
    onSuccess: () => {
      toast({
        title: 'Appeal finalised',
        description: 'The employee has been notified and the appraisal is complete.',
      });
      setFinalOpen(false);
      setFinalNotes('');
      refresh();
    },
    onError: fail('Could not finalise the appeal'),
  });

  if (review.isLoading) {
    return (
      <div className="space-y-6 p-6">
        <Skeleton className="h-10 w-1/3" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (review.isError || !review.data) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="Appeal" backHref="/hr/performance/appeals" />
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={TriangleAlert}
              title="Could not load this appeal"
              description={
                (review.error as Error)?.message ?? 'It may not exist, or you may not have access.'
              }
            />
          </CardContent>
        </Card>
      </div>
    );
  }

  const data = review.data;
  const isFinal = data.status === 'Upheld' || data.status === 'Rejected';
  const canDecide = data.status === 'Submitted' || data.status === 'UnderReview';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`Appeal — ${data.employeeName}`}
        description={`${data.appraisalNumber} · ${data.cycleName} (${formatDate(data.cycleStartDate)} – ${formatDate(data.cycleEndDate)})`}
        backHref="/hr/performance/appeals"
        actions={
          <div className="flex items-center gap-2">
            {data.status === 'Submitted' && (
              <Button variant="outline" onClick={() => pickUp.mutate()} disabled={pickUp.isPending}>
                <Scale className="mr-2 h-4 w-4" />
                {pickUp.isPending ? 'Picking up…' : 'Pick up'}
              </Button>
            )}
            <Button variant="ghost" asChild>
              <Link href={`/hr/performance/hr-review/${appraisalId}`}>Open appraisal</Link>
            </Button>
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-3">
        <StatusBadge status={data.status} />
        <span className="text-sm text-muted-foreground">
          {data.employeeNumber}
          {data.positionTitle ? ` · ${data.positionTitle}` : ''}
          {data.organizationUnitName ? ` · ${data.organizationUnitName}` : ''}
        </span>
        <span className="text-sm text-muted-foreground">
          Submitted {formatDate(data.submittedDate)}
        </span>
      </div>

      <MetricTiles
        tiles={[
          { label: 'Self', value: fmt(data.selfEvaluationScore) },
          { label: 'Peers', value: fmt(data.peerEvaluationScore) },
          { label: 'Manager', value: fmt(data.managerEvaluationScore) },
          { label: 'Overall', value: fmt(data.overallScore) },
        ]}
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">What the employee says</CardTitle>
        </CardHeader>
        <CardContent className="whitespace-pre-wrap text-sm">
          {data.overallAppealReason || 'No overall reason was given; see the items below.'}
        </CardContent>
      </Card>

      {isFinal && (
        <Alert>
          <Lock className="h-4 w-4" />
          <AlertTitle>Decided</AlertTitle>
          <AlertDescription>
            This appeal is final and the scores are locked. It is kept here for the record; the
            employee can see the outcome on their own appraisal.
          </AlertDescription>
        </Alert>
      )}

      {isRemanded && (
        <Alert>
          <RotateCcw className="h-4 w-4" />
          <AlertTitle>With the manager</AlertTitle>
          <AlertDescription>
            {postRemand.data?.managerReevaluationDate
              ? 'The manager has re-submitted. Compare the two evaluations below and make the final decision.'
              : `Waiting on the manager's re-evaluation${
                  postRemand.data?.appealRemandDeadline
                    ? `, due ${formatDate(postRemand.data.appealRemandDeadline)}`
                    : ''
                }. The final decision opens once they submit.`}
          </AlertDescription>
        </Alert>
      )}

      {/* ── Post-remand comparison ───────────────────────────────────────────── */}
      {isRemanded && postRemand.data && (
        <>
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Before and after the remand</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="flex flex-wrap gap-8 text-sm">
                <div>
                  <div className="text-muted-foreground">Pre-remand overall</div>
                  <div className="text-lg tabular-nums">
                    {fmt(postRemand.data.preRemandOverallScore)}
                  </div>
                </div>
                <div>
                  <div className="text-muted-foreground">Post-remand overall</div>
                  <div className="text-lg font-medium tabular-nums">
                    {fmt(postRemand.data.postRemandOverallScore)}
                  </div>
                </div>
              </div>

              <div className="overflow-x-auto">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Criterion</TableHead>
                      <TableHead className="text-right">Weight</TableHead>
                      <TableHead className="text-right">Before</TableHead>
                      <TableHead className="text-right">After</TableHead>
                      <TableHead className="text-right">Δ</TableHead>
                      <TableHead>Appealed</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {postRemand.data.criteriaComparisons.map((c) => (
                      <TableRow key={c.criterionKey}>
                        <TableCell>
                          <div className="font-medium">{c.itemName}</div>
                          {c.appealReason && (
                            <div className="text-xs text-muted-foreground">{c.appealReason}</div>
                          )}
                        </TableCell>
                        <TableCell className="text-right tabular-nums">{c.weight}</TableCell>
                        <TableCell className="text-right tabular-nums">
                          {fmt(c.preRemandScore)}
                        </TableCell>
                        <TableCell className="text-right tabular-nums font-medium">
                          {fmt(c.postRemandScore)}
                        </TableCell>
                        <TableCell className="text-right tabular-nums">
                          {c.scoreDifference != null && c.scoreDifference !== 0 ? (
                            <span
                              className={
                                c.scoreDifference > 0
                                  ? 'text-emerald-600 dark:text-emerald-500'
                                  : 'text-red-600 dark:text-red-500'
                              }
                            >
                              {c.scoreDifference > 0 ? '+' : ''}
                              {c.scoreDifference}
                            </span>
                          ) : (
                            <span className="text-muted-foreground">—</span>
                          )}
                        </TableCell>
                        <TableCell>
                          {c.wasAppealed ? (
                            <StatusBadge status="Appealed" />
                          ) : (
                            <span className="text-xs text-muted-foreground">—</span>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            </CardContent>
          </Card>

          {postRemand.data.managerReevaluationDate && (
            <div className="flex justify-end">
              <Button onClick={() => setFinalOpen(true)}>
                <CheckCircle2 className="mr-2 h-4 w-4" />
                Make the final decision
              </Button>
            </div>
          )}
        </>
      )}

      {/* ── The contested items ──────────────────────────────────────────────── */}
      {!isRemanded && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              Contested criteria ({data.appealedCriteria.length})
            </CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            {data.appealedCriteria.length === 0 ? (
              <EmptyState
                icon={Scale}
                title="No criteria listed"
                description="The appeal was filed against the overall outcome rather than named items."
              />
            ) : (
              <div className="overflow-x-auto">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Criterion</TableHead>
                      <TableHead className="text-right">Weight</TableHead>
                      <TableHead className="text-right">Self</TableHead>
                      <TableHead className="text-right">Peers</TableHead>
                      <TableHead className="text-right">Manager</TableHead>
                      <TableHead className="text-right">Weighted</TableHead>
                      {data.hrCanModifyScores && canDecide && (
                        <TableHead className="w-64">New score</TableHead>
                      )}
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {data.appealedCriteria.map((c) => (
                      <TableRow key={c.appealItemId}>
                        <TableCell className="max-w-xs">
                          <div className="font-medium">{c.itemName}</div>
                          <div className="mt-1 text-xs text-muted-foreground">
                            <span className="font-medium">Their reason:</span> {c.appealReason}
                          </div>
                          {c.managerComments && (
                            <div className="mt-1 text-xs text-muted-foreground">
                              <span className="font-medium">Manager:</span> {c.managerComments}
                            </div>
                          )}
                        </TableCell>
                        <TableCell className="text-right tabular-nums">{c.weight}</TableCell>
                        <TableCell className="text-right tabular-nums">{fmt(c.selfScore)}</TableCell>
                        <TableCell className="text-right tabular-nums">
                          {fmt(c.peerAverageScore)}
                        </TableCell>
                        <TableCell className="text-right tabular-nums font-medium">
                          {fmt(c.managerScore)}
                        </TableCell>
                        <TableCell className="text-right tabular-nums">
                          {fmt(c.finalWeightedScore)}
                        </TableCell>
                        {data.hrCanModifyScores && canDecide && (
                          <TableCell>
                            <div className="space-y-2">
                              <Input
                                type="number"
                                min={0}
                                max={100}
                                placeholder="Leave blank to keep"
                                value={scoreEdits[c.criterionKey] ?? ''}
                                onChange={(e) =>
                                  setScoreEdits({
                                    ...scoreEdits,
                                    [c.criterionKey]: e.target.value,
                                  })
                                }
                                aria-label={`New score for ${c.itemName}`}
                              />
                              {scoreEdits[c.criterionKey]?.trim() && (
                                <Input
                                  placeholder="Justification (required)"
                                  value={justifications[c.criterionKey] ?? ''}
                                  onChange={(e) =>
                                    setJustifications({
                                      ...justifications,
                                      [c.criterionKey]: e.target.value,
                                    })
                                  }
                                  aria-label={`Justification for ${c.itemName}`}
                                />
                              )}
                            </div>
                          </TableCell>
                        )}
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {!data.hrCanModifyScores && canDecide && (
        <Alert>
          <Lock className="h-4 w-4" />
          <AlertTitle>Scores are locked on this cycle</AlertTitle>
          <AlertDescription>
            The settings profile <strong>{data.appraisalSettingsName}</strong> does not let HR
            change scores while resolving an appeal. To change a score, remand the appeal and let
            the manager re-evaluate.
          </AlertDescription>
        </Alert>
      )}

      {canDecide && (
        <div className="flex flex-wrap justify-end gap-2">
          <Button variant="outline" onClick={() => setDecision('Remanded')}>
            <RotateCcw className="mr-2 h-4 w-4" />
            Send back to the manager
          </Button>
          <Button variant="destructive" onClick={() => setDecision('Rejected')}>
            <XCircle className="mr-2 h-4 w-4" />
            Reject the appeal
          </Button>
          <Button onClick={() => setDecision('Upheld')}>
            <CheckCircle2 className="mr-2 h-4 w-4" />
            Uphold the appeal
          </Button>
        </div>
      )}

      {/* ── Decision dialog ──────────────────────────────────────────────────── */}
      <Dialog open={!!decision} onOpenChange={(o) => !o && setDecision(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {decision === 'Remanded'
                ? 'Send back for re-evaluation'
                : decision === 'Upheld'
                  ? 'Uphold the appeal'
                  : 'Reject the appeal'}
            </DialogTitle>
            <DialogDescription>
              {decision === 'Remanded'
                ? "The appraisal returns to the manager, a snapshot of their current evaluation is frozen for comparison, and a deadline is set. Nothing is decided until they re-submit."
                : 'This is final. The employee is notified and can read your notes on their outcome page.'}
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-2">
            <Label htmlFor="resolutionNotes">
              {decision === 'Remanded' ? 'What the manager should reconsider' : 'Your reasoning'}
            </Label>
            <Textarea
              id="resolutionNotes"
              rows={5}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="The employee reads this. Be specific about what was and was not accepted."
            />
            {modifications.length > 0 && decision !== 'Remanded' && (
              <p className="text-xs text-muted-foreground">
                {modifications.length} score change(s) will be applied and the overall score
                recalculated.
              </p>
            )}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setDecision(null)}>
              Cancel
            </Button>
            <Button
              onClick={() => decision && resolve.mutate(decision)}
              disabled={!decision || !notes.trim() || resolve.isPending}
            >
              {resolve.isPending ? 'Recording…' : 'Confirm'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Final post-remand decision ───────────────────────────────────────── */}
      <Dialog open={finalOpen} onOpenChange={setFinalOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Final decision</DialogTitle>
            <DialogDescription>
              The manager has re-evaluated. Upholding accepts the appeal as having had merit;
              rejecting confirms the scores as they now stand. Either way the appraisal is
              complete and the scores lock.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="flex gap-2">
              <Button
                variant={finalDecision === 'Upheld' ? 'default' : 'outline'}
                onClick={() => setFinalDecision('Upheld')}
                className="flex-1"
              >
                Uphold
              </Button>
              <Button
                variant={finalDecision === 'Rejected' ? 'destructive' : 'outline'}
                onClick={() => setFinalDecision('Rejected')}
                className="flex-1"
              >
                Reject
              </Button>
            </div>

            <div className="space-y-2">
              <Label htmlFor="hrFinalNotes">Final notes</Label>
              <Textarea
                id="hrFinalNotes"
                rows={5}
                value={finalNotes}
                onChange={(e) => setFinalNotes(e.target.value)}
                placeholder="The employee reads this on their outcome page."
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setFinalOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => finalize.mutate()}
              disabled={!finalNotes.trim() || finalize.isPending}
            >
              {finalize.isPending ? 'Finalising…' : 'Finalise'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function fmt(value?: number | null): string {
  return value != null ? Number(value).toFixed(1) : '—';
}
