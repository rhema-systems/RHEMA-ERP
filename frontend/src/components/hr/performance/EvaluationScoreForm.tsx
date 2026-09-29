'use client';

import { useMemo } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { Separator } from '@/components/ui/separator';
import { cn } from '@/lib/utils';
import {
  countScored,
  isGoalItem,
  isMeasuredItem,
  kpiAchievementPercent,
  resolveGrade,
  scaleTop,
  type AppraisalSectionKind,
  type EvaluationGradeRange,
  type EvaluationItem,
} from '@/types/hr/appraisal-run';

/** The value the form holds for one scored line. */
export interface ScoreValue {
  numericScore?: number | null;
  actualValue?: number | null;
  notes?: string | null;
  evidenceLinks?: string | null;
}

export type ScoreValues = Record<string, ScoreValue>;

/**
 * One section as this form needs it. Deliberately structural rather than tied to
 * `SelfEvaluationSection` / `ManagerEvaluationSection` / `PeerEvaluationSection`, because all
 * three carry the same section shell and differ only in what hangs off each item.
 */
export interface ScoreFormSection {
  sectionId: string;
  sectionName: string;
  sectionDescription?: string | null;
  sectionWeight: number;
  /** A goals section's rows are the employee's own locked goals. */
  kind?: AppraisalSectionKind;
  items: EvaluationItem[];
}

interface EvaluationScoreFormProps {
  sections: ScoreFormSection[];
  /** Keyed by `criterionKey`. */
  values: ScoreValues;
  onChange: (criterionKey: string, patch: Partial<ScoreValue>) => void;
  /** Read-only once submitted, or when the phase has moved past this role. */
  disabled?: boolean;
  /**
   * Per-item override. A peer's KPI rows are shown read-only when the cycle does not let peers
   * score KPIs, and remanded appeals lock everything except the appealed items.
   */
  isItemDisabled?: (item: EvaluationItem) => boolean;
  /** Rendered under the item's own inputs — the comparison column on the manager's form. */
  renderItemAside?: (item: EvaluationItem) => React.ReactNode;
  /** Draws attention to a row, e.g. an item flagged in an appeal remand. */
  isItemHighlighted?: (item: EvaluationItem) => boolean;
}

/**
 * The scoring form every evaluation leg reuses — self, manager and peer.
 *
 * All three score the *same* frozen snapshot the cycle took when it generated the appraisal,
 * keyed by `criterionKey` — a template row's template item, a goal row's own snapshot row — so
 * there is one form rather than three that drift apart. What differs between legs is which
 * existing scores get loaded in and what sits beside each row, both of which the caller supplies.
 *
 * Two kinds of line, decided by the row's own scoring method:
 *   • **Measured rows** — a KPI, or a goal with a target — take an *actual value* against the
 *     target, and the achievement percentage is previewed live. The server recomputes and stores
 *     its own figure — the preview only saves a round trip while the user types.
 *   • **Rated rows** — a competency, or a goal with no target — take a score on the row's grade
 *     bands (for a goal, the tenant's overall scale).
 *
 * The parent owns `values`; this component never holds score state, because a draft save and a
 * submit post the same object and the parent is what decides which.
 */
export function EvaluationScoreForm({
  sections,
  values,
  onChange,
  disabled = false,
  isItemDisabled,
  renderItemAside,
  isItemHighlighted,
}: EvaluationScoreFormProps) {
  const allItems = useMemo(() => sections.flatMap((s) => s.items), [sections]);
  const scoreable = (item: EvaluationItem) => !(isItemDisabled?.(item) ?? false);
  const { total, scored } = countScored(allItems, values, scoreable);

  if (sections.length === 0) {
    return (
      <Card>
        <CardContent className="p-6 text-sm text-muted-foreground">
          This appraisal has no scoreable criteria. That means the cycle generated it without a
          criterion snapshot — HR needs to check the template assigned to this employee.
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-6">
      <Card>
        <CardContent className="flex flex-wrap items-center gap-4 p-4">
          <div className="min-w-[12rem] flex-1">
            <div className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">Scored</span>
              <span className="font-medium tabular-nums">
                {scored} of {total}
              </span>
            </div>
            <Progress value={total ? (scored / total) * 100 : 0} className="mt-2" />
          </div>
          <p className="text-xs text-muted-foreground">
            Only items you have scored are sent. Everything required must be scored before you
            can submit.
          </p>
        </CardContent>
      </Card>

      {sections.map((section) => (
        <Card key={section.sectionId}>
          <CardHeader>
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div>
                <CardTitle className="text-base">{section.sectionName}</CardTitle>
                {section.sectionDescription && (
                  <p className="mt-1 text-sm text-muted-foreground">{section.sectionDescription}</p>
                )}
              </div>
              <Badge variant="outline">Weight {section.sectionWeight}%</Badge>
            </div>
            {section.kind === 'EmployeeGoals' && (
              <p className="text-sm text-muted-foreground">
                The goals agreed and locked for this cycle, each weighted within the section by its own
                weight. A goal with a target is scored on what was achieved against it; one without is
                rated on the grade scale.
              </p>
            )}
          </CardHeader>
          <CardContent className="space-y-6">
            {section.items.map((item, index) => (
              <div key={item.criterionKey}>
                {index > 0 && <Separator className="mb-6" />}
                <ScoreRow
                  item={item}
                  value={values[item.criterionKey] ?? {}}
                  onChange={(patch) => onChange(item.criterionKey, patch)}
                  disabled={disabled || (isItemDisabled?.(item) ?? false)}
                  highlighted={isItemHighlighted?.(item) ?? false}
                  aside={renderItemAside?.(item)}
                />
              </div>
            ))}
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

function ScoreRow({
  item,
  value,
  onChange,
  disabled,
  highlighted,
  aside,
}: {
  item: EvaluationItem;
  value: ScoreValue;
  onChange: (patch: Partial<ScoreValue>) => void;
  disabled: boolean;
  highlighted: boolean;
  aside?: React.ReactNode;
}) {
  const measured = isMeasuredItem(item);
  const goal = isGoalItem(item);
  const grade = measured ? null : resolveGrade(value.numericScore, item.gradeRanges);
  const achievement = measured
    ? kpiAchievementPercent(value.actualValue, item.kpiTargetValue, item.kpiMinValue, item.kpiMaxValue)
    : null;
  const top = scaleTop(item.gradeRanges);
  const key = item.criterionKey;

  return (
    <div className={cn('space-y-3 rounded-md', highlighted && 'bg-amber-50 p-3 dark:bg-amber-950/30')}>
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="font-medium">{item.customQuestion || item.itemName}</p>
          {item.itemDescription && (
            <p className="mt-0.5 text-sm text-muted-foreground">{item.itemDescription}</p>
          )}
        </div>
        <div className="flex shrink-0 items-center gap-2">
          {highlighted && <Badge variant="destructive">Under appeal</Badge>}
          {goal ? (
            <Badge variant="secondary">Goal</Badge>
          ) : (
            measured && <Badge variant="secondary">KPI</Badge>
          )}
          <Badge variant="outline">Weight {item.itemWeight}%</Badge>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <div className="space-y-3">
          {measured ? (
            <div className="space-y-1.5">
              <Label htmlFor={`v-${key}`}>
                Actual achieved{item.kpiUnit ? ` (${item.kpiUnit})` : ''}
              </Label>
              <Input
                id={`v-${key}`}
                type="number"
                step="any"
                min={0}
                inputMode="decimal"
                disabled={disabled}
                value={value.actualValue ?? ''}
                onChange={(e) =>
                  onChange({ actualValue: e.target.value === '' ? null : Number(e.target.value) })
                }
              />
              <p className="text-xs text-muted-foreground">
                {item.kpiTargetValue !== null && item.kpiTargetValue !== undefined
                  ? `Target ${item.kpiTargetValue}${item.kpiUnit ? ` ${item.kpiUnit}` : ''}`
                  : `No target set on this ${goal ? 'goal' : 'KPI'}`}
                {achievement !== null && ` · ${achievement}% of target`}
              </p>
            </div>
          ) : (
            <div className="space-y-1.5">
              <Label htmlFor={`s-${key}`}>Score (0–{top})</Label>
              <Input
                id={`s-${key}`}
                type="number"
                min={0}
                max={top}
                step={1}
                inputMode="numeric"
                disabled={disabled}
                value={value.numericScore ?? ''}
                onChange={(e) =>
                  onChange({ numericScore: e.target.value === '' ? null : Number(e.target.value) })
                }
              />
              {grade ? (
                <p className="text-xs text-muted-foreground">
                  Grade: <span className="font-medium text-foreground">{grade}</span>
                </p>
              ) : (
                <GradeBandHint ranges={item.gradeRanges} />
              )}
            </div>
          )}

          <div className="space-y-1.5">
            <Label htmlFor={`n-${key}`}>Comments</Label>
            <Textarea
              id={`n-${key}`}
              rows={2}
              disabled={disabled}
              value={value.notes ?? ''}
              onChange={(e) => onChange({ notes: e.target.value })}
              placeholder="What supports this score?"
            />
          </div>

          {item.requireEvidence && (
            <div className="space-y-1.5">
              <Label htmlFor={`e-${key}`}>
                Evidence link <span className="text-muted-foreground">(required for this item)</span>
              </Label>
              <Input
                id={`e-${key}`}
                disabled={disabled}
                value={value.evidenceLinks ?? ''}
                onChange={(e) => onChange({ evidenceLinks: e.target.value })}
                placeholder="https://…"
              />
            </div>
          )}
        </div>

        {aside && <div className="rounded-md border bg-muted/40 p-3 text-sm">{aside}</div>}
      </div>
    </div>
  );
}

/** Shown until a score is entered, so the bands are visible while deciding rather than after. */
function GradeBandHint({ ranges }: { ranges: EvaluationGradeRange[] }) {
  if (ranges.length === 0) {
    return (
      <p className="text-xs text-muted-foreground">
        No grade bands on this item — the score is recorded as-is.
      </p>
    );
  }

  return (
    <p className="text-xs text-muted-foreground">
      {ranges
        .slice()
        .sort((a, b) => b.lowScore - a.lowScore)
        .map((r) => `${r.gradeName} ${r.lowScore}–${r.highScore}`)
        .join(' · ')}
    </p>
  );
}
