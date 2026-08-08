'use client';

import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams, useRouter } from 'next/navigation';
import { Save, Send, TriangleAlert } from 'lucide-react';
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
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  EvaluationScoreForm,
  type ScoreValues,
} from '@/components/hr/performance/EvaluationScoreForm';
import { useToast } from '@/hooks/use-toast';
import { formatDate } from '@/lib/hr/attendance-format';
import { performanceAppraisalService } from '@/services/hr/appraisal-run.service';
import { toItemScores, type CustomQuestionResponseInput } from '@/types/hr/appraisal-run';

/**
 * The employee's own scoring form.
 *
 * Draft and submit are the same endpoint with `isDraft` flipped, so the form holds one set of
 * values and the two buttons differ only in that flag. Submitting is one-way: the server
 * refuses every later write once `submittedDate` is set, which is why it is behind a confirm.
 *
 * ⚠ This endpoint answers **200 with `success: false`** for a rejected save — a failed peer
 * nomination count, an unscored competency — as well as 400. Both paths are handled; treating
 * a 2xx as success would silently lose the user's work.
 */
export default function SelfEvaluationPage() {
  const params = useParams<{ id: string }>();
  const appraisalId = params.id;
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [values, setValues] = useState<ScoreValues>({});
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [confirmOpen, setConfirmOpen] = useState(false);

  const { data: context, isLoading } = useQuery({
    queryKey: ['hr', 'self-evaluation-context', appraisalId],
    queryFn: () => performanceAppraisalService.getSelfEvaluationContext(appraisalId),
    enabled: !!appraisalId,
  });

  // Seed from whatever was saved before. Keyed on the appraisal so switching records reloads,
  // but not on every context refetch — that would discard edits in progress after a save.
  useEffect(() => {
    if (!context) return;
    const seeded: ScoreValues = {};
    const seededAnswers: Record<string, string> = {};
    for (const section of context.sections) {
      for (const item of section.items) {
        seeded[item.templateItemId] = {
          numericScore: item.existingNumericScore ?? null,
          actualValue: item.existingActualValue ?? null,
          notes: item.existingNotes ?? null,
          evidenceLinks: item.existingEvidenceLinks ?? null,
        };
      }
      for (const q of section.customQuestions) {
        seededAnswers[q.templateItemId] = q.existingResponse ?? '';
      }
    }
    setValues(seeded);
    setAnswers(seededAnswers);
  }, [appraisalId, context?.isSelfEvaluationSubmitted]);

  const sections = useMemo(
    () =>
      (context?.sections ?? []).map((s) => ({
        sectionId: s.sectionId,
        sectionName: s.sectionName,
        sectionDescription: s.sectionDescription,
        sectionWeight: s.sectionWeight,
        items: s.items,
      })),
    [context],
  );

  const customQuestions = useMemo(
    () => (context?.sections ?? []).flatMap((s) => s.customQuestions),
    [context],
  );

  const save = useMutation({
    mutationFn: (isDraft: boolean) => {
      const customQuestionResponses: CustomQuestionResponseInput[] = customQuestions
        .filter((q) => (answers[q.templateItemId] ?? '').trim().length > 0)
        .map((q) => ({
          templateItemId: q.templateItemId,
          responseText: answers[q.templateItemId],
        }));

      return performanceAppraisalService.saveSelfEvaluation(appraisalId, {
        appraisalId,
        // Overwritten server-side from the token; sent to match the documented payload.
        employeeId: context?.employeeId ?? '',
        itemScores: toItemScores(values),
        isDraft,
        customQuestionResponses,
        goalAssessments: [],
      });
    },
    onSuccess: (result, isDraft) => {
      if (!result.success) {
        // A rejected save comes back 200 — surface the rule rather than claiming it saved.
        toast({
          title: isDraft ? 'Draft not saved' : 'Could not submit',
          description: result.message ?? 'The server rejected this evaluation.',
          variant: 'destructive',
        });
        return;
      }

      toast({
        title: isDraft ? 'Draft saved' : 'Self-evaluation submitted',
        description: isDraft
          ? 'You can come back and finish this later.'
          : 'Your manager has been notified.',
      });
      setConfirmOpen(false);
      queryClient.invalidateQueries({ queryKey: ['hr', 'self-evaluation-context', appraisalId] });
      queryClient.invalidateQueries({ queryKey: ['hr', 'appraisal-phase', appraisalId] });
      queryClient.invalidateQueries({ queryKey: ['hr', 'my-appraisals'] });
      if (!isDraft) router.push(`/hr/performance/appraisals/${appraisalId}`);
    },
    onError: (e: Error) =>
      toast({ title: 'Could not save', description: e.message, variant: 'destructive' }),
  });

  if (isLoading) {
    return (
      <div className="space-y-6 p-6">
        <Skeleton className="h-10 w-1/3" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (!context) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="Self-evaluation" backHref="/hr/performance/appraisals" />
        <Card>
          <CardContent className="p-0">
            <EmptyState icon={TriangleAlert} title="Appraisal not found" />
          </CardContent>
        </Card>
      </div>
    );
  }

  const readOnly = context.isSelfEvaluationSubmitted || !context.isEditable;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="My self-evaluation"
        description={`${context.appraisalCycleName} · ${formatDate(context.periodStart)} – ${formatDate(context.periodEnd)}`}
        backHref={`/hr/performance/appraisals/${appraisalId}`}
        actions={
          !readOnly ? (
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                onClick={() => save.mutate(true)}
                disabled={save.isPending}
              >
                <Save className="mr-2 h-4 w-4" />
                Save draft
              </Button>
              <Button onClick={() => setConfirmOpen(true)} disabled={save.isPending}>
                <Send className="mr-2 h-4 w-4" />
                Submit
              </Button>
            </div>
          ) : undefined
        }
      />

      {context.isSelfEvaluationSubmitted && (
        <Alert>
          <Send className="h-4 w-4" />
          <AlertTitle>
            Submitted {formatDate(context.selfEvaluationSubmittedDate)}
          </AlertTitle>
          <AlertDescription>
            A submitted self-evaluation cannot be changed. Speak to HR if something needs
            correcting.
          </AlertDescription>
        </Alert>
      )}

      {!context.isSelfEvaluationSubmitted && !context.isEditable && (
        <Alert variant="destructive">
          <TriangleAlert className="h-4 w-4" />
          <AlertTitle>This appraisal is not open for your self-evaluation</AlertTitle>
          <AlertDescription>
            Either the cycle has moved past the self-evaluation step, or goal setting is still
            outstanding.
          </AlertDescription>
        </Alert>
      )}

      {!context.isSelfEvaluationSubmitted && context.selfEvaluationDeadline && (
        <p className="text-sm text-muted-foreground">
          Due {formatDate(context.selfEvaluationDeadline)}.
        </p>
      )}

      <EvaluationScoreForm
        sections={sections}
        values={values}
        disabled={readOnly}
        onChange={(id, patch) =>
          setValues((prev) => ({ ...prev, [id]: { ...prev[id], ...patch } }))
        }
      />

      {customQuestions.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Questions</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {customQuestions
              .slice()
              .sort((a, b) => a.displayOrder - b.displayOrder)
              .map((q) => (
                <div key={q.templateItemId} className="space-y-1.5">
                  <Label htmlFor={`q-${q.templateItemId}`}>{q.questionText}</Label>
                  <Textarea
                    id={`q-${q.templateItemId}`}
                    rows={3}
                    disabled={readOnly}
                    value={answers[q.templateItemId] ?? ''}
                    onChange={(e) =>
                      setAnswers((prev) => ({ ...prev, [q.templateItemId]: e.target.value }))
                    }
                  />
                </div>
              ))}
          </CardContent>
        </Card>
      )}

      <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Submit your self-evaluation?</DialogTitle>
            <DialogDescription>
              You will not be able to change it afterwards, and your manager will be able to see
              your scores alongside theirs.
              {context.settings?.requirePeerReviews &&
                context.settings.peerNominationMode === 'Employee' &&
                ' Your peer nominations must also be within the required range, or this will be refused.'}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => save.mutate(false)} disabled={save.isPending}>
              Submit
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
