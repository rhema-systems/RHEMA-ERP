'use client';

import { useEffect, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Info, Target } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { employeeGoalService } from '@/services/hr/goals.service';
import { GOAL_PROGRESS_STATUS_OPTIONS, type GoalProgressStatus } from '@/types/hr/goals';
import type { GoalAssessmentInput } from '@/types/hr/appraisal-run';

/** Radix Select refuses an empty-string item value. */
const UNSET = '__unset__';

export type GoalAssessmentValues = Record<string, GoalAssessmentInput>;

interface GoalAssessmentPanelProps {
  appraisalId: string;
  /** Which side of the assessment this form is filling in. */
  mode: 'self' | 'manager';
  values: GoalAssessmentValues;
  onChange: (values: GoalAssessmentValues) => void;
  readOnly?: boolean;
}

/**
 * The year-end assessment of the goals the cycle spent the year cascading.
 *
 * **Both sides are recorded separately and neither overwrites the other** — the employee's claim
 * and the manager's conclusion are stored in their own columns, because the year-end conversation
 * is precisely the comparison between them. In manager mode the employee's answers are shown
 * alongside, read-only, so the manager is assessing against a stated claim rather than from memory.
 *
 * ⚠ **For a KPI-backed goal the server recomputes the percentage** from the actual value against
 * the goal's target/min/max, and its number wins over anything typed here. That is deliberate — the
 * stored figure should reflect the goal's measurement semantics, not where a slider happened to
 * sit — but it means a typed percentage would silently vanish, so the field is disabled and says
 * why once an actual value is present.
 *
 * ⚠ Only goals the user has actually filled something in for are sent. Sending every goal would
 * write a row of nulls for each untouched one, which reads later as "assessed, with no answer".
 */
export function GoalAssessmentPanel({
  appraisalId,
  mode,
  values,
  onChange,
  readOnly = false,
}: GoalAssessmentPanelProps) {
  const { data: goals, isLoading } = useQuery({
    queryKey: ['hr', 'appraisal-goals', appraisalId],
    queryFn: () => employeeGoalService.getByAppraisal(appraisalId),
    enabled: !!appraisalId,
  });

  // Seed from whatever was saved before, so reopening a draft shows the previous answers rather
  // than an empty form. Only runs while the form is still untouched.
  useEffect(() => {
    if (!goals || Object.keys(values).length > 0) return;

    const seeded: GoalAssessmentValues = {};
    for (const g of goals) {
      const percent = mode === 'self' ? g.selfFinalProgressPercent : g.managerFinalProgressPercent;
      const status = mode === 'self' ? g.selfFinalStatus : g.managerFinalStatus;
      const actual = mode === 'self' ? g.selfFinalActualValue : g.managerFinalActualValue;
      const notes = mode === 'self' ? g.selfAssessmentNotes : g.managerAssessmentNotes;
      const evidence = mode === 'self' ? g.selfEvidenceLinks : g.managerEvidenceLinks;

      if (percent == null && status == null && actual == null && !notes && !evidence) continue;

      seeded[g.id] = {
        goalId: g.id,
        finalProgressPercent: percent ?? null,
        finalStatus: status ?? null,
        finalActualValue: actual ?? null,
        assessmentNotes: notes ?? null,
        evidenceLinks: evidence ?? null,
      };
    }

    if (Object.keys(seeded).length > 0) onChange(seeded);
  }, [goals, mode, values, onChange]);

  const set = (goalId: string, patch: Partial<GoalAssessmentInput>) => {
    const current = values[goalId] ?? { goalId };
    onChange({ ...values, [goalId]: { ...current, ...patch } });
  };

  const assessedCount = useMemo(
    () =>
      Object.values(values).filter(
        (v) =>
          v.finalProgressPercent != null ||
          v.finalStatus != null ||
          v.finalActualValue != null ||
          !!v.assessmentNotes ||
          !!v.evidenceLinks,
      ).length,
    [values],
  );

  if (isLoading) return <Skeleton className="h-48 w-full" />;

  if (!goals || goals.length === 0) {
    return (
      <Card>
        <CardContent className="p-0">
          <EmptyState
            icon={Target}
            title="No goals in this cycle"
            description="There is nothing to assess — this appraisal's employee had no goals set for the cycle."
          />
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-4">
        <CardTitle>Goal assessment</CardTitle>
        <p className="text-sm text-muted-foreground">
          {assessedCount} of {goals.length} assessed
        </p>
      </CardHeader>
      <CardContent className="space-y-6">
        {assessedCount < goals.length && !readOnly && (
          <Alert>
            <Info className="h-4 w-4" />
            <AlertTitle>
              {goals.length - assessedCount} goal{goals.length - assessedCount === 1 ? '' : 's'} not
              yet assessed
            </AlertTitle>
            <AlertDescription>
              Goals you leave blank are not recorded either way — they are simply absent from the
              year-end record.
            </AlertDescription>
          </Alert>
        )}

        {goals.map((goal) => {
          const v = values[goal.id] ?? { goalId: goal.id };
          // Matches the server: a KPI-backed, non-Boolean goal has its percentage recomputed from
          // the actual value, so typing one here would be discarded.
          const derivesPercent =
            !!goal.kpiDefinitionId &&
            goal.measurementType !== 'Boolean' &&
            v.finalActualValue != null;

          return (
            <div key={goal.id} className="space-y-3 rounded-lg border p-4">
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div className="space-y-1">
                  <p className="font-medium leading-snug">{goal.title}</p>
                  <p className="flex flex-wrap items-center gap-x-2 text-xs text-muted-foreground">
                    <span>Weight {goal.weight}</span>
                    <span>·</span>
                    <span>Tracked at {goal.progressPercent}%</span>
                    {goal.targetValue != null && (
                      <>
                        <span>·</span>
                        <span>
                          Target {goal.targetValue}
                          {goal.unit ? ` ${goal.unit}` : ''}
                        </span>
                      </>
                    )}
                  </p>
                </div>
                <StatusBadge status={goal.status} />
              </div>

              {/* In manager mode, what the employee said — the thing being assessed against. */}
              {mode === 'manager' &&
                (goal.selfFinalProgressPercent != null ||
                  goal.selfFinalStatus != null ||
                  goal.selfAssessmentNotes) && (
                  <div className="rounded-md bg-muted/50 p-3 text-sm">
                    <p className="mb-1 flex items-center gap-2 text-xs font-medium text-muted-foreground">
                      <Badge variant="outline">Employee&apos;s assessment</Badge>
                    </p>
                    <p className="text-muted-foreground">
                      {goal.selfFinalProgressPercent != null && (
                        <span className="tabular-nums">{goal.selfFinalProgressPercent}% </span>
                      )}
                      {goal.selfFinalStatus && <span>· {goal.selfFinalStatus} </span>}
                      {goal.selfFinalActualValue != null && (
                        <span>· actual {goal.selfFinalActualValue} </span>
                      )}
                    </p>
                    {goal.selfAssessmentNotes && (
                      <p className="mt-1 whitespace-pre-wrap">{goal.selfAssessmentNotes}</p>
                    )}
                  </div>
                )}

              <div className="grid gap-3 sm:grid-cols-3">
                <div className="space-y-2">
                  <Label htmlFor={`actual-${goal.id}`}>
                    Final actual{goal.unit ? ` (${goal.unit})` : ''}
                  </Label>
                  <Input
                    id={`actual-${goal.id}`}
                    type="number"
                    step="0.01"
                    disabled={readOnly}
                    value={v.finalActualValue ?? ''}
                    onChange={(e) =>
                      set(goal.id, {
                        finalActualValue: e.target.value === '' ? null : Number(e.target.value),
                      })
                    }
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor={`percent-${goal.id}`}>Final progress %</Label>
                  <Input
                    id={`percent-${goal.id}`}
                    type="number"
                    min={0}
                    max={100}
                    disabled={readOnly || derivesPercent}
                    value={v.finalProgressPercent ?? ''}
                    onChange={(e) =>
                      set(goal.id, {
                        finalProgressPercent: e.target.value === '' ? null : Number(e.target.value),
                      })
                    }
                  />
                  {derivesPercent && (
                    <p className="text-xs text-muted-foreground">
                      Calculated from the actual value against this KPI&apos;s target.
                    </p>
                  )}
                </div>

                <div className="space-y-2">
                  <Label htmlFor={`status-${goal.id}`}>Final status</Label>
                  <Select
                    value={v.finalStatus ?? UNSET}
                    disabled={readOnly}
                    onValueChange={(val) =>
                      set(goal.id, {
                        finalStatus: val === UNSET ? null : (val as GoalProgressStatus),
                      })
                    }
                  >
                    <SelectTrigger id={`status-${goal.id}`}>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={UNSET}>Not assessed</SelectItem>
                      {GOAL_PROGRESS_STATUS_OPTIONS.map((o) => (
                        <SelectItem key={o.value} value={o.value}>
                          {o.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor={`notes-${goal.id}`}>Assessment notes</Label>
                <Textarea
                  id={`notes-${goal.id}`}
                  rows={2}
                  disabled={readOnly}
                  value={v.assessmentNotes ?? ''}
                  onChange={(e) => set(goal.id, { assessmentNotes: e.target.value || null })}
                  placeholder={
                    mode === 'self'
                      ? 'What you delivered against this goal, and what got in the way.'
                      : 'Your conclusion on this goal, and how it compares with what was claimed.'
                  }
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor={`evidence-${goal.id}`}>Evidence</Label>
                <Input
                  id={`evidence-${goal.id}`}
                  disabled={readOnly}
                  value={v.evidenceLinks ?? ''}
                  onChange={(e) => set(goal.id, { evidenceLinks: e.target.value || null })}
                  placeholder="Links or references that back this up"
                />
              </div>
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
}

/**
 * Drops untouched goals, so an unassessed goal is absent from the payload rather than stored as a
 * row of nulls.
 */
export function toGoalAssessments(values: GoalAssessmentValues): GoalAssessmentInput[] {
  return Object.values(values).filter(
    (v) =>
      v.finalProgressPercent != null ||
      v.finalStatus != null ||
      v.finalActualValue != null ||
      !!v.assessmentNotes ||
      !!v.evidenceLinks,
  );
}
