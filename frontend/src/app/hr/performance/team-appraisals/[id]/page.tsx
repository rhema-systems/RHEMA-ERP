'use client';

import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'next/navigation';
import { MessagesSquare, Save, Send, TriangleAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { AppraisalPhaseRail } from '@/components/hr/performance/AppraisalPhaseRail';
import { EmployeeTrendPanel } from '@/components/hr/performance/EmployeeTrendPanel';
import {
  GoalAssessmentPanel,
  toGoalAssessments,
  type GoalAssessmentValues,
} from '@/components/hr/performance/GoalAssessmentPanel';
import {
  EvaluationScoreForm,
  type ScoreValues,
} from '@/components/hr/performance/EvaluationScoreForm';
import { PeerFeedbackPanel } from '@/components/hr/performance/PeerFeedbackPanel';
import { ConversationsPanel } from '@/components/hr/performance/ConversationsPanel';
import { PeerNominationPanel } from '@/components/hr/performance/PeerNominationPanel';
import { useToast } from '@/hooks/use-toast';
import { PerformanceAttachmentsPanel } from '@/components/hr/performance/PerformanceAttachmentsPanel';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import {
  appraisalWorkflowService,
  performanceAppraisalService,
} from '@/services/hr/appraisal-run.service';
import {
  isKpiItem,
  toItemScores,
  type EvaluationItem,
  type ManagerEvaluationItem,
} from '@/types/hr/appraisal-run';

/**
 * The manager's evaluation of one report.
 *
 * The employee's own scores sit beside each row rather than on a separate tab — the whole
 * point of a manager evaluation is the comparison, and putting the two a click apart makes it
 * an act of memory. Whether the self-score is visible at all is the cycle's decision
 * (`showSelfScoreToManager`), so the aside is dropped rather than shown blank when it is off.
 *
 * ⚠ **Submitting is one-way and does more than save.** It assigns the HR reviewer, moves the
 * appraisal to Governance, and locks every later write — a remand is the only route back.
 * That is why it is behind a confirm and why draft-saving is the prominent action.
 */
export default function ManagerEvaluationPage() {
  const params = useParams<{ id: string }>();
  const appraisalId = params.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [values, setValues] = useState<ScoreValues>({});
  const [narrative, setNarrative] = useState({
    overallComments: '',
    strengthsIdentified: '',
    areasForImprovement: '',
    trainingNeeds: '',
    careerAspirations: '',
    recommendationNotes: '',
  });
  const [recommendations, setRecommendations] = useState({
    recommendPromotion: false,
    recommendIncrement: false,
    recommendTraining: false,
    recommendPIP: false,
    recommendTermination: false,
  });
  const [goalValues, setGoalValues] = useState<GoalAssessmentValues>({});
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [tab, setTab] = useState('evaluation');

  const { data: context, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'manager-evaluation-context', appraisalId],
    queryFn: () => performanceAppraisalService.getManagerEvaluationContext(appraisalId),
    enabled: !!appraisalId,
    retry: false,
  });

  const { data: phase } = useQuery({
    queryKey: ['hr', 'appraisal-phase', appraisalId],
    queryFn: () => appraisalWorkflowService.getPhase(appraisalId),
    enabled: !!appraisalId,
  });

  useEffect(() => {
    if (!context) return;
    const seeded: ScoreValues = {};
    for (const section of context.sections) {
      for (const item of section.items) {
        seeded[item.templateItemId] = {
          numericScore: item.managerNumericScore ?? null,
          actualValue: item.managerActualValue ?? null,
          notes: item.managerNotes ?? null,
          evidenceLinks: item.managerEvidenceLinks ?? null,
        };
      }
    }
    setValues(seeded);
    setNarrative({
      overallComments: context.overallComments ?? '',
      strengthsIdentified: context.strengthsIdentified ?? '',
      areasForImprovement: context.areasForImprovement ?? '',
      trainingNeeds: context.trainingNeeds ?? '',
      careerAspirations: context.careerAspirations ?? '',
      recommendationNotes: context.recommendationNotes ?? '',
    });
    setRecommendations({
      recommendPromotion: context.recommendPromotion,
      recommendIncrement: context.recommendIncrement,
      recommendTraining: context.recommendTraining,
      recommendPIP: context.recommendPIP,
      recommendTermination: context.recommendTermination,
    });
  }, [appraisalId, context?.isManagerEvaluationSubmitted]);

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

  const itemsById = useMemo(() => {
    const map = new Map<string, ManagerEvaluationItem>();
    for (const section of context?.sections ?? []) {
      for (const item of section.items) map.set(item.templateItemId, item);
    }
    return map;
  }, [context]);

  const showSelfScores = context?.settings?.showSelfScoreToManager !== false;
  const readOnly = context?.isManagerEvaluationSubmitted === true || context?.isEditable === false;
  const remanded = context?.isRemandedAppeal === true;

  const save = useMutation({
    mutationFn: (isDraft: boolean) =>
      performanceAppraisalService.saveManagerEvaluation(appraisalId, {
        appraisalId,
        // Overwritten server-side from the token; sent to match the documented payload.
        managerId: '00000000-0000-0000-0000-000000000000',
        itemScores: toItemScores(values),
        overallNotes: narrative.overallComments || null,
        recommendation: narrative.recommendationNotes || null,
        overallComments: narrative.overallComments || null,
        strengthsIdentified: narrative.strengthsIdentified || null,
        areasForImprovement: narrative.areasForImprovement || null,
        trainingNeeds: narrative.trainingNeeds || null,
        careerAspirations: narrative.careerAspirations || null,
        recommendationNotes: narrative.recommendationNotes || null,
        ...recommendations,
        isDraft,
        // The manager's year-end verdict on each goal. Posted as an empty array before, so the
        // cycle never scored the goals it spent the year cascading.
        goalAssessments: toGoalAssessments(goalValues),
      }),
    onSuccess: (result, isDraft) => {
      if (!result.success) {
        // A rejected save answers 200 with success:false — surface the rule, don't claim success.
        toast({
          title: isDraft ? 'Draft not saved' : 'Could not submit',
          description: result.message ?? 'The server rejected this evaluation.',
          variant: 'destructive',
        });
        return;
      }

      toast({
        title: isDraft ? 'Draft saved' : 'Evaluation submitted',
        description: isDraft
          ? 'Nothing has been sent on yet.'
          : 'The appraisal has moved to HR review.',
      });
      setConfirmOpen(false);
      queryClient.invalidateQueries({ queryKey: ['hr', 'manager-evaluation-context', appraisalId] });
      queryClient.invalidateQueries({ queryKey: ['hr', 'appraisal-phase', appraisalId] });
      queryClient.invalidateQueries({ queryKey: ['hr', 'team-member-appraisals'] });
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

  if (isError || !context) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="Manager evaluation" backHref="/hr/performance/team-appraisals" />
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={TriangleAlert}
              title="Cannot open this evaluation"
              description={
                (error as Error)?.message ??
                'This employee does not report to you, or the appraisal no longer exists.'
              }
            />
          </CardContent>
        </Card>
      </div>
    );
  }

  const requiresManagerNomination =
    context.settings?.requirePeerReviews === true &&
    context.settings.peerNominationMode === 'Manager';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={context.employeeName}
        description={`${context.employeeNumber}${context.position ? ` · ${context.position}` : ''} · ${context.appraisalCycleName}`}
        backHref="/hr/performance/team-appraisals"
        actions={
          <div className="flex items-center gap-2">
            <StatusBadge status={humanizeEnum(context.status)} />
            {!readOnly && (
              <>
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
              </>
            )}
          </div>
        }
      />

      <Card>
        <CardContent className="p-4">
          <AppraisalPhaseRail phase={phase?.phase} settings={context.settings} />
        </CardContent>
      </Card>

      {remanded && (
        <Alert variant="destructive">
          <TriangleAlert className="h-4 w-4" />
          <AlertTitle>Re-evaluation after an appeal</AlertTitle>
          <AlertDescription>
            {context.appealedTemplateItemIds.length} item(s) are under appeal and are highlighted
            below.
            {context.appealRemandDeadline && (
              <>
                {' '}
                This must be resubmitted by{' '}
                <strong>{formatDate(context.appealRemandDeadline)}</strong>
                {context.isRemandDeadlineExceeded && ' — that deadline has passed, so the server will refuse the save. Contact HR.'}
              </>
            )}
          </AlertDescription>
        </Alert>
      )}

      {context.isManagerEvaluationSubmitted && !remanded && (
        <Alert>
          <Send className="h-4 w-4" />
          <AlertTitle>
            Submitted {formatDate(context.managerEvaluationSubmittedDate)}
          </AlertTitle>
          <AlertDescription>
            This evaluation is locked. HR can return it to you if it needs changing.
          </AlertDescription>
        </Alert>
      )}

      {!context.isManagerEvaluationSubmitted && !showSelfScores && (
        <p className="text-sm text-muted-foreground">
          This cycle does not show you the employee&apos;s self-scores while you evaluate.
        </p>
      )}

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="evaluation">Evaluation</TabsTrigger>
          <TabsTrigger value="goals">Goals</TabsTrigger>
          <TabsTrigger value="narrative">Assessment &amp; recommendations</TabsTrigger>
          {context.settings?.requirePeerReviews && (
            <TabsTrigger value="peers">Peer feedback</TabsTrigger>
          )}
          {requiresManagerNomination && <TabsTrigger value="nominations">Nominations</TabsTrigger>}
          <TabsTrigger value="history">History</TabsTrigger>
          <TabsTrigger value="conversations">Conversations</TabsTrigger>
          <TabsTrigger value="evidence">Evidence</TabsTrigger>
        </TabsList>

        {/* ⚠ The upload behind this tab was purpose-built for the controlled gate and had never
            been called by anything: an appraisal's evidence could not be attached at all. */}
        <TabsContent value="evidence" className="mt-4">
          <PerformanceAttachmentsPanel
            basePath="/PerformanceAppraisals"
            ownerId={appraisalId}
            canUpload={!readOnly}
            helpText="Evidence behind the ratings — reports, certificates, correspondence. Scanned on upload; max 10 MB."
          />
        </TabsContent>

        <TabsContent value="evaluation" className="mt-4">
          <EvaluationScoreForm
            sections={sections}
            values={values}
            disabled={readOnly}
            isItemHighlighted={(item) =>
              remanded && context.appealedTemplateItemIds.includes(item.templateItemId)
            }
            onChange={(id, patch) =>
              setValues((prev) => ({ ...prev, [id]: { ...prev[id], ...patch } }))
            }
            renderItemAside={
              showSelfScores
                ? (item: EvaluationItem) => {
                    const full = itemsById.get(item.templateItemId);
                    if (!full) return null;
                    const hasSelf =
                      full.employeeSelfNumericScore != null || full.employeeSelfActualValue != null;
                    if (!hasSelf) {
                      return (
                        <p className="text-muted-foreground">
                          {context.employeeName.split(' ')[0]} has not scored this yet.
                        </p>
                      );
                    }
                    return (
                      <div className="space-y-1">
                        <p className="text-xs font-medium uppercase text-muted-foreground">
                          Their self-assessment
                        </p>
                        <p className="text-lg font-semibold tabular-nums">
                          {isKpiItem(item)
                            ? `${full.employeeSelfActualValue ?? '—'}${item.kpiUnit ? ` ${item.kpiUnit}` : ''}`
                            : (full.employeeSelfNumericScore ?? '—')}
                          {full.employeeSelfAchievedGrade && (
                            <span className="ml-2 text-sm font-normal text-muted-foreground">
                              {full.employeeSelfAchievedGrade}
                            </span>
                          )}
                        </p>
                        {full.employeeSelfNotes && (
                          <p className="whitespace-pre-wrap text-muted-foreground">
                            {full.employeeSelfNotes}
                          </p>
                        )}
                      </div>
                    );
                  }
                : undefined
            }
          />
        </TabsContent>

        <TabsContent value="goals" className="mt-4">
          <GoalAssessmentPanel
            appraisalId={appraisalId}
            mode="manager"
            values={goalValues}
            onChange={setGoalValues}
            readOnly={readOnly}
          />
        </TabsContent>

        <TabsContent value="narrative" className="mt-4 space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Final assessment</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-4 md:grid-cols-2">
              <NarrativeField
                id="overallComments"
                label="Overall comments"
                value={narrative.overallComments}
                disabled={readOnly}
                onChange={(v) => setNarrative((p) => ({ ...p, overallComments: v }))}
              />
              <NarrativeField
                id="strengthsIdentified"
                label="Strengths"
                value={narrative.strengthsIdentified}
                disabled={readOnly}
                onChange={(v) => setNarrative((p) => ({ ...p, strengthsIdentified: v }))}
              />
              <NarrativeField
                id="areasForImprovement"
                label="Areas for improvement"
                value={narrative.areasForImprovement}
                disabled={readOnly}
                onChange={(v) => setNarrative((p) => ({ ...p, areasForImprovement: v }))}
              />
              <NarrativeField
                id="trainingNeeds"
                label="Training needs"
                value={narrative.trainingNeeds}
                disabled={readOnly}
                onChange={(v) => setNarrative((p) => ({ ...p, trainingNeeds: v }))}
              />
              <NarrativeField
                id="careerAspirations"
                label="Career aspirations"
                value={narrative.careerAspirations}
                disabled={readOnly}
                onChange={(v) => setNarrative((p) => ({ ...p, careerAspirations: v }))}
              />
              <NarrativeField
                id="recommendationNotes"
                label="Notes on your recommendations"
                value={narrative.recommendationNotes}
                disabled={readOnly}
                onChange={(v) => setNarrative((p) => ({ ...p, recommendationNotes: v }))}
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Recommendations</CardTitle>
              <p className="text-sm text-muted-foreground">
                These are recorded against the appraisal for HR to act on — ticking one does not
                itself start a promotion, increment or improvement plan.
              </p>
            </CardHeader>
            <CardContent className="grid gap-3 sm:grid-cols-2">
              {(
                [
                  ['recommendPromotion', 'Promotion'],
                  ['recommendIncrement', 'Salary increment'],
                  ['recommendTraining', 'Training'],
                  ['recommendPIP', 'Performance improvement plan'],
                  ['recommendTermination', 'Termination'],
                ] as const
              ).map(([key, label]) => (
                <label key={key} className="flex items-center gap-2 text-sm">
                  <Checkbox
                    checked={recommendations[key]}
                    disabled={readOnly}
                    onCheckedChange={(checked) =>
                      setRecommendations((p) => ({ ...p, [key]: checked === true }))
                    }
                  />
                  {label}
                </label>
              ))}
            </CardContent>
          </Card>
        </TabsContent>

        {context.settings?.requirePeerReviews && (
          <TabsContent value="peers" className="mt-4">
            <PeerFeedbackPanel appraisalId={appraisalId} />
          </TabsContent>
        )}

        {requiresManagerNomination && (
          <TabsContent value="nominations" className="mt-4">
            <PeerNominationPanel appraisalId={appraisalId} canManage canApprove />
          </TabsContent>
        )}

        {/*
          What this person has scored in previous cycles. A manager scoring someone for the first
          time is otherwise working blind — and a score that reads as a step change is the one
          worth a conversation before it is submitted.
        */}
        <TabsContent value="history" className="mt-4">
          <EmployeeTrendPanel
            employeeId={context.employeeId}
            employeeName={context.employeeName}
          />
        </TabsContent>

        <TabsContent value="conversations" className="mt-4">
          <ConversationsPanel appraisalId={appraisalId} canSchedule />
        </TabsContent>
      </Tabs>

      {!requiresManagerNomination && context.settings?.requirePeerReviews && (
        <Card>
          <CardHeader>
            <div className="flex items-center gap-2">
              <MessagesSquare className="h-4 w-4 text-muted-foreground" />
              <CardTitle className="text-base">Peer nominations awaiting your approval</CardTitle>
            </div>
          </CardHeader>
          <CardContent className="p-0">
            {/* The employee nominates in this cycle, but approving is still the manager's call. */}
            <PeerNominationPanel appraisalId={appraisalId} canApprove />
          </CardContent>
        </Card>
      )}

      <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Submit this evaluation?</DialogTitle>
            <DialogDescription>
              This hands the appraisal to HR and locks your scores — you will not be able to
              change them unless HR returns it to you.
              {context.settings?.requirePeerReviews &&
                ' Any peer feedback still outstanding will hold up HR sign-off, not this submission.'}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => save.mutate(false)} disabled={save.isPending}>
              Submit to HR
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function NarrativeField({
  id,
  label,
  value,
  disabled,
  onChange,
}: {
  id: string;
  label: string;
  value: string;
  disabled: boolean;
  onChange: (value: string) => void;
}) {
  return (
    <div className="space-y-1.5">
      <div className="flex items-baseline justify-between">
        <Label htmlFor={id}>{label}</Label>
        <Badge variant="outline" className="text-xs font-normal">
          {value.length}/2000
        </Badge>
      </div>
      <Textarea
        id={id}
        rows={4}
        maxLength={2000}
        disabled={disabled}
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
    </div>
  );
}
