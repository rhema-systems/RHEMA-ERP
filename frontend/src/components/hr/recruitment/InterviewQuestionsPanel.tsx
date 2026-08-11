'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Check, Dices, HelpCircle, Loader2, Save } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { interviewQuestionBankService, jobInterviewService } from '@/services/hr/interviews.service';
import type { JobInterviewDetail, QuestionPlanPreview } from '@/types/hr/interviews';

/**
 * The question plan: which sections the panel covers and, within each, which questions were drawn.
 *
 * Two ways to set the questions, and they are genuinely different jobs:
 * **Re-draw** replaces every section's selection with a fresh random draw — the fair-sampling route,
 * used when the same role is interviewed repeatedly and you do not want the same set twice.
 * **Choose by hand** ticks specific questions, for a panel that wants to probe something particular.
 *
 * ⚠ Order matters and is preserved: the sequence saved here is the order the panel asks in and the
 * order the scorecard renders. A refused save leaves the existing selection untouched.
 */
export function InterviewQuestionsPanel({
  interview,
  canManage,
}: {
  interview: JobInterviewDetail;
  canManage: boolean;
}) {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [editingPlanId, setEditingPlanId] = useState<string | null>(null);
  const [picked, setPicked] = useState<string[]>([]);
  const [preview, setPreview] = useState<QuestionPlanPreview[] | null>(null);

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['hr', 'interview', interview.id] });

  const editingPlan = interview.questions.find((p) => p.id === editingPlanId) ?? null;

  // The whole bank for the section being edited — a hand-pick chooses from every active question of
  // that type, not just the ones already drawn.
  const bankForPlan = useQuery({
    queryKey: ['hr', 'interview-questions', 'active', editingPlan?.questionTypeId],
    queryFn: () => interviewQuestionBankService.getActiveQuestions(editingPlan?.questionTypeId),
    enabled: !!editingPlan,
  });

  const runPreview = useMutation({
    mutationFn: () => jobInterviewService.previewQuestions(interview.id),
    onSuccess: setPreview,
    onError: (error: any) =>
      toast({ title: 'Could not preview a draw', description: error?.message, variant: 'destructive' }),
  });

  const redraw = useMutation({
    mutationFn: () => jobInterviewService.redrawQuestions(interview.id),
    onSuccess: () => {
      toast({ title: 'Questions re-drawn', description: 'Every section now has a fresh selection.' });
      setPreview(null);
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Could not re-draw', description: error?.message, variant: 'destructive' }),
  });

  const commit = useMutation({
    mutationFn: (payload: { planId: string; questionDetailIds: string[] }) =>
      jobInterviewService.commitQuestions(interview.id, { plans: [payload] }),
    onSuccess: () => {
      toast({ title: 'Questions saved' });
      setEditingPlanId(null);
      setPicked([]);
      invalidate();
    },
    onError: (error: any) =>
      toast({
        title: 'Could not save the questions',
        description: error?.message ?? 'The existing selection has been left as it was.',
        variant: 'destructive',
      }),
  });

  const startEditing = (planId: string) => {
    const plan = interview.questions.find((p) => p.id === planId);
    setEditingPlanId(planId);
    setPicked(plan?.selectedQuestions.map((q) => q.questionDetailId) ?? []);
  };

  return (
    <div className="space-y-4">
      {canManage && (
        <div className="flex flex-wrap items-center gap-2">
          <Button variant="outline" size="sm" disabled={runPreview.isPending} onClick={() => runPreview.mutate()}>
            {runPreview.isPending ? (
              <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
            ) : (
              <Dices className="mr-1.5 h-4 w-4" />
            )}
            Preview a fresh draw
          </Button>
          {preview && (
            <Button size="sm" disabled={redraw.isPending} onClick={() => redraw.mutate()}>
              {redraw.isPending && <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />}
              Apply the draw
            </Button>
          )}
          {preview && (
            <Button variant="ghost" size="sm" onClick={() => setPreview(null)}>
              Discard
            </Button>
          )}
        </div>
      )}

      {preview?.some((p) => !p.meetsRequiredCount) && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>The bank is too thin for this plan</AlertTitle>
          <AlertDescription>
            {preview
              .filter((p) => !p.meetsRequiredCount)
              .map((p) => (
                <div key={p.planId}>
                  {p.questionTypeName} asks for {p.requiredQuestionCount} but only{' '}
                  {p.availableQuestionCount} active question
                  {p.availableQuestionCount === 1 ? ' is' : 's are'} available. The panel will not be able
                  to sign off their scorecards until more are added to the bank.
                </div>
              ))}
          </AlertDescription>
        </Alert>
      )}

      {interview.questions.length === 0 ? (
        <Card>
          <CardContent className="py-10">
            <EmptyState
              icon={HelpCircle}
              title="No question plan"
              description="This interview was scheduled without a preset. Apply one, or the panel scores on their own notes."
            />
          </CardContent>
        </Card>
      ) : (
        interview.questions.map((plan) => {
          const planPreview = preview?.find((p) => p.planId === plan.id);
          const isEditing = editingPlanId === plan.id;

          return (
            <Card key={plan.id}>
              <CardHeader className="flex flex-row items-start justify-between gap-4 space-y-0">
                <div>
                  <CardTitle className="text-base">{plan.questionTypeName}</CardTitle>
                  <CardDescription>
                    The panel must score at least {plan.requiredQuestionCount} of these before their
                    scorecard can be signed off.
                  </CardDescription>
                </div>
                {canManage && !isEditing && (
                  <Button variant="outline" size="sm" onClick={() => startEditing(plan.id)}>
                    Choose by hand
                  </Button>
                )}
              </CardHeader>
              <CardContent className="space-y-3">
                {isEditing ? (
                  <>
                    {bankForPlan.isLoading ? (
                      <div className="flex items-center gap-2 text-sm text-muted-foreground">
                        <Loader2 className="h-4 w-4 animate-spin" /> Loading the bank…
                      </div>
                    ) : (
                      <div className="max-h-80 space-y-1 overflow-y-auto rounded-md border p-2">
                        {(bankForPlan.data ?? []).map((question) => {
                          const order = picked.indexOf(question.id);
                          const checked = order >= 0;
                          return (
                            <label
                              key={question.id}
                              className="flex cursor-pointer items-start gap-3 rounded-md px-2 py-2 hover:bg-muted"
                            >
                              <Checkbox
                                checked={checked}
                                onCheckedChange={(value) =>
                                  setPicked((prev) =>
                                    value ? [...prev, question.id] : prev.filter((id) => id !== question.id),
                                  )
                                }
                              />
                              <span className="flex-1">
                                <span className="text-sm">{question.questionText}</span>
                                <span className="ml-2 text-xs text-muted-foreground">
                                  weight {question.weight} · marked {question.minScore}–{question.maxScore}
                                </span>
                              </span>
                              {checked && <Badge variant="secondary">#{order + 1}</Badge>}
                            </label>
                          );
                        })}
                      </div>
                    )}
                    <p className="text-sm text-muted-foreground">
                      Ticks are numbered in the order you add them — that is the order the panel will ask
                      and score in.
                    </p>
                    <div className="flex gap-2">
                      <Button
                        size="sm"
                        disabled={commit.isPending}
                        onClick={() => commit.mutate({ planId: plan.id, questionDetailIds: picked })}
                      >
                        {commit.isPending ? (
                          <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
                        ) : (
                          <Save className="mr-1.5 h-4 w-4" />
                        )}
                        Save selection
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => {
                          setEditingPlanId(null);
                          setPicked([]);
                        }}
                      >
                        Cancel
                      </Button>
                    </div>
                  </>
                ) : (
                  <>
                    {plan.selectedQuestions.length === 0 ? (
                      <p className="text-sm text-muted-foreground">
                        No questions drawn for this section yet.
                      </p>
                    ) : (
                      <ol className="space-y-2">
                        {plan.selectedQuestions.map((question, index) => (
                          <li key={question.id} className="flex gap-3 text-sm">
                            <span className="w-5 shrink-0 text-muted-foreground">{index + 1}.</span>
                            <span className="flex-1">
                              {question.questionText}
                              <span className="ml-2 text-xs text-muted-foreground">
                                weight {question.weight} · marked {question.minScore}–{question.maxScore}
                              </span>
                            </span>
                          </li>
                        ))}
                      </ol>
                    )}

                    {planPreview && (
                      <div className="rounded-md border border-dashed p-3">
                        <p className="mb-2 flex items-center gap-1.5 text-sm font-medium">
                          <Dices className="h-4 w-4" />
                          Proposed draw
                        </p>
                        <ol className="space-y-1.5">
                          {planPreview.questions.map((question, index) => (
                            <li key={question.questionDetailId} className="flex gap-3 text-sm">
                              <span className="w-5 shrink-0 text-muted-foreground">{index + 1}.</span>
                              <span className="flex-1">{question.questionText}</span>
                              {plan.selectedQuestions.some(
                                (q) => q.questionDetailId === question.questionDetailId,
                              ) && <Check className="h-4 w-4 shrink-0 text-muted-foreground" />}
                            </li>
                          ))}
                        </ol>
                      </div>
                    )}
                  </>
                )}
              </CardContent>
            </Card>
          );
        })
      )}
    </div>
  );
}
