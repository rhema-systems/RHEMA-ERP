'use client';

import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams, useRouter } from 'next/navigation';
import { EyeOff, Save, Send, TriangleAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
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
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  EvaluationScoreForm,
  type ScoreValues,
} from '@/components/hr/performance/EvaluationScoreForm';
import { useToast } from '@/hooks/use-toast';
import { formatDate } from '@/lib/hr/attendance-format';
import { peerEvaluationService } from '@/services/hr/appraisal-run.service';
import { toItemScores, type EvaluationItem } from '@/types/hr/appraisal-run';

/**
 * Giving peer feedback on a colleague's appraisal.
 *
 * Same scoring form as the self and manager legs — the snapshot is shared, so the criteria and
 * their weights are identical to what everyone else is scoring against.
 *
 * Two things are peer-specific:
 *   • **KPI rows may be read-only.** When the cycle's settings do not let peers score KPIs,
 *     those items still appear (so the criterion is visible in context) but cannot be scored;
 *     the server enforces the same rule when validating the submission.
 *   • **Anonymity is about the appraisee, not you.** `isAnonymous` means *they* will not see
 *     who said what. Their manager always sees your name, and it is worth saying so plainly
 *     rather than letting the word "anonymous" imply more than it means.
 *
 * The due date is the nomination's, else the cycle's peer deadline, and the nominator's
 * instructions are shown above the form (performance closure D5): they were collected on the
 * nomination and shown nowhere.
 */
export default function PeerEvaluationPage() {
  const params = useParams<{ id: string }>();
  const evaluationId = params.id;
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [values, setValues] = useState<ScoreValues>({});
  const [confirmOpen, setConfirmOpen] = useState(false);

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'peer-evaluation', evaluationId],
    queryFn: () => peerEvaluationService.getDetail(evaluationId),
    enabled: !!evaluationId,
    retry: false,
  });

  useEffect(() => {
    if (!data) return;
    const seeded: ScoreValues = {};
    for (const section of data.sections) {
      for (const item of section.items) {
        seeded[item.criterionKey] = {
          numericScore: item.existingNumericScore ?? null,
          actualValue: item.existingActualValue ?? null,
          notes: item.existingNotes ?? null,
          evidenceLinks: item.existingEvidenceLinks ?? null,
        };
      }
    }
    setValues(seeded);
  }, [evaluationId, data?.isSubmitted]);

  const sections = useMemo(
    () =>
      (data?.sections ?? []).map((s) => ({
        sectionId: s.sectionId,
        sectionName: s.sectionName,
        sectionDescription: s.sectionDescription,
        sectionWeight: s.sectionWeight,
        kind: s.kind,
        items: s.items,
      })),
    [data],
  );

  /**
   * The server's own `isScoreable`: KPI rows and the employee's goal rows are locked unless the
   * cycle lets peers score measured work. It used to be re-derived here from the KPI id, which no
   * goal row carries.
   */
  const lockedKeys = useMemo(
    () =>
      new Set(
        (data?.sections ?? [])
          .flatMap((s) => s.items)
          .filter((i) => i.isScoreable === false)
          .map((i) => i.criterionKey),
      ),
    [data],
  );
  const notScoreable = (item: EvaluationItem) => lockedKeys.has(item.criterionKey);
  // Only what this peer may score is sent: a goal row they may not score is refused outright.
  const scoreableItems = useMemo(
    () => sections.flatMap((s) => s.items).filter((i) => !lockedKeys.has(i.criterionKey)),
    [sections, lockedKeys],
  );

  const saveDraft = useMutation({
    mutationFn: () =>
      peerEvaluationService.saveDraft(evaluationId, {
        evaluationId,
        itemScores: toItemScores(scoreableItems, values),
      }),
    onSuccess: () => {
      toast({ title: 'Draft saved', description: 'Your feedback has not been sent yet.' });
      queryClient.invalidateQueries({ queryKey: ['hr', 'peer-evaluation', evaluationId] });
    },
    onError: (e: Error) =>
      toast({ title: 'Could not save draft', description: e.message, variant: 'destructive' }),
  });

  /**
   * Submit is score-then-submit: the draft carries the scores, the submit validates
   * completeness. Doing them in one click keeps the user from having to remember to save first,
   * and the 422 from either half names exactly what is outstanding.
   */
  const submit = useMutation({
    mutationFn: async () => {
      await peerEvaluationService.saveDraft(evaluationId, {
        evaluationId,
        itemScores: toItemScores(scoreableItems, values),
      });
      return peerEvaluationService.submit(evaluationId);
    },
    onSuccess: () => {
      toast({
        title: 'Peer feedback submitted',
        description: 'Their manager has been notified. You cannot change it now.',
      });
      setConfirmOpen(false);
      queryClient.invalidateQueries({ queryKey: ['hr', 'peer-evaluation', evaluationId] });
      queryClient.invalidateQueries({ queryKey: ['hr', 'peer-evaluation-assignments'] });
      router.push('/me/performance/peer-reviews');
    },
    onError: (e: Error) =>
      toast({ title: 'Could not submit', description: e.message, variant: 'destructive' }),
  });

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-10 w-1/3" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className="space-y-6">
        <PageHeader title="Peer feedback" backHref="/me/performance/peer-reviews" />
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={TriangleAlert}
              title="Peer evaluation not found"
              description={
                (error as Error)?.message ??
                'This form is not assigned to you, or the nomination has been withdrawn.'
              }
            />
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={`Feedback on ${data.appraiseeName}`}
        description={`${data.appraiseePosition} · ${data.appraiseeOrganizationUnit} · ${data.appraisalCycleName}`}
        backHref="/me/performance/peer-reviews"
        actions={
          !data.isSubmitted ? (
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                onClick={() => saveDraft.mutate()}
                disabled={saveDraft.isPending || submit.isPending}
              >
                <Save className="mr-2 h-4 w-4" />
                Save draft
              </Button>
              <Button onClick={() => setConfirmOpen(true)} disabled={submit.isPending}>
                <Send className="mr-2 h-4 w-4" />
                Submit
              </Button>
            </div>
          ) : undefined
        }
      />

      {data.isSubmitted ? (
        <Alert>
          <Send className="h-4 w-4" />
          <AlertTitle>Submitted</AlertTitle>
          <AlertDescription>
            Your feedback is in and cannot be changed.
          </AlertDescription>
        </Alert>
      ) : (
        <Alert>
          <EyeOff className="h-4 w-4" />
          <AlertTitle>
            {data.isAnonymous
              ? `${data.appraiseeName} will not see who gave which feedback`
              : `${data.appraiseeName} will be able to see your feedback attributed to you`}
          </AlertTitle>
          <AlertDescription>
            Their manager sees your name either way. Peer feedback carries{' '}
            {Math.round((Number(data.peerEvaluationWeight) || 0) * 100)}% of the final score.
            {data.dueDate && ` Due ${formatDate(data.dueDate)}.`}
          </AlertDescription>
        </Alert>
      )}

      {data.instructionsToPeer && (
        <Card>
          <CardContent className="p-4 text-sm">
            <p className="font-medium">What you were asked to comment on</p>
            <p className="mt-1 whitespace-pre-wrap text-muted-foreground">
              {data.instructionsToPeer}
            </p>
          </CardContent>
        </Card>
      )}

      {!data.allowPeerKpiEvaluation && (
        <p className="text-sm text-muted-foreground">
          This cycle does not ask peers to score KPI targets or the employee&apos;s goals — those rows
          are shown for context but cannot be scored.
        </p>
      )}

      <EvaluationScoreForm
        sections={sections}
        values={values}
        disabled={data.isSubmitted}
        isItemDisabled={notScoreable}
        onChange={(id, patch) =>
          setValues((prev) => ({ ...prev, [id]: { ...prev[id], ...patch } }))
        }
      />

      <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Submit your feedback?</DialogTitle>
            <DialogDescription>
              Everything required must be scored, and you will not be able to change your
              answers afterwards.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => submit.mutate()} disabled={submit.isPending}>
              Submit
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
