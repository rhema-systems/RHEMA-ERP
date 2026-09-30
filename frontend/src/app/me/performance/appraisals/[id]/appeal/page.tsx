'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams, useRouter } from 'next/navigation';
import { Gavel, Info, Send, TriangleAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { useToast } from '@/hooks/use-toast';
import { appraisalAppealService } from '@/services/hr/appeals.service';
import {
  formatActualAgainstTarget,
  formatCriterionScore,
  type AppealableCriterion,
} from '@/types/hr/appeals';

/**
 * The employee's appeal form.
 *
 * An appeal is per-criterion, not per-appraisal: each contested item carries its own reason, and
 * at least one is required. The overall reason is context on top of that, not a substitute.
 *
 * **Every criterion the manager scored can be contested** (closure C6) — a competency, a KPI, or
 * one of the employee's own goals — listed by section as the forms show them. A measured row shows
 * the manager's actual against the target and the achievement it gave; a rated row the score on its
 * own scale. The page offered competencies only, though the demo's one appeal was against a KPI.
 *
 * ⚠ There is **one appeal per appraisal, ever**. The server refuses a second one, so this is a
 * single shot — the copy says so before the submit rather than after the refusal.
 *
 * `canAppeal` is decided server-side (the appraisal has to be complete and unappealed) and
 * `cannotAppealReason` is written for the employee to read, so it is shown verbatim rather than
 * being re-derived here.
 */
export default function FileAppealPage() {
  const params = useParams<{ id: string }>();
  const appraisalId = params.id;
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  // Keyed by criterion — a goal row has no template item.
  const [selected, setSelected] = useState<Record<string, boolean>>({});
  const [reasons, setReasons] = useState<Record<string, string>>({});
  const [overallReason, setOverallReason] = useState('');

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'appeal-page-data', appraisalId],
    queryFn: () => appraisalAppealService.getAppealPageData(appraisalId),
    enabled: !!appraisalId,
    retry: false,
  });

  const criteria = useMemo(() => data?.appealableCriteria ?? [], [data]);

  // The rows by section, in the order the server sends them (the forms' order).
  const sections = useMemo(() => {
    const groups: { name: string; weight?: number | null; rows: AppealableCriterion[] }[] = [];
    for (const row of criteria) {
      const name = row.sectionName ?? 'Other criteria';
      const last = groups[groups.length - 1];
      if (last && last.name === name) last.rows.push(row);
      else groups.push({ name, weight: row.sectionWeight, rows: [row] });
    }
    return groups;
  }, [criteria]);

  // Each row by both ids it has, as the forms send theirs: a template row by its template item and
  // snapshot row, a goal row by its snapshot row alone.
  const appealedItems = useMemo(
    () =>
      criteria
        .filter((row) => selected[row.criterionKey])
        .map((row) => ({
          templateItemId: row.templateItemId ?? null,
          criterionConfigId: row.criterionConfigId ?? null,
          reason: reasons[row.criterionKey]?.trim() ?? '',
        })),
    [criteria, selected, reasons],
  );

  const incomplete = appealedItems.some((item) => !item.reason);

  const submit = useMutation({
    mutationFn: () =>
      appraisalAppealService.submitAppeal(appraisalId, {
        appraisalId,
        overallReason: overallReason.trim() || null,
        appealedItems,
      }),
    onSuccess: () => {
      toast({
        title: 'Appeal submitted',
        description: 'HR and your manager have been notified. You can follow it from here.',
      });
      queryClient.invalidateQueries({ queryKey: ['hr', 'appeal-page-data', appraisalId] });
      router.push(`/me/performance/appraisals/${appraisalId}/appeal-status`);
    },
    onError: (e: Error) =>
      toast({ title: 'Could not submit the appeal', description: e.message, variant: 'destructive' }),
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
        <PageHeader title="Appeal" backHref={`/me/performance/appraisals/${appraisalId}`} />
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={TriangleAlert}
              title="Could not load the appeal form"
              description={(error as Error)?.message ?? 'This appraisal may not be yours.'}
            />
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Appeal your appraisal"
        description={`${data.appraisalNumber} · ${data.cycleName}`}
        backHref={`/me/performance/appraisals/${appraisalId}`}
      />

      <MetricTiles
        tiles={[
          { label: 'Final score', value: data.finalScore != null ? data.finalScore.toFixed(1) : '—' },
          { label: 'Grade', value: data.finalGrade ?? '—' },
          { label: 'Items you can contest', value: criteria.length },
        ]}
      />

      {!data.canAppeal ? (
        <Alert>
          <Info className="h-4 w-4" />
          <AlertTitle>You cannot appeal this appraisal</AlertTitle>
          <AlertDescription>
            {data.cannotAppealReason ?? 'Appeals are not open on this appraisal.'}
          </AlertDescription>
        </Alert>
      ) : (
        <>
          <Alert>
            <Gavel className="h-4 w-4" />
            <AlertTitle>One appeal per appraisal</AlertTitle>
            <AlertDescription>
              You can file this once. Include every item you want reconsidered — there is no way
              to add to it afterwards. HR may uphold it, reject it, or send it back to your
              manager to look at again.
            </AlertDescription>
          </Alert>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">What are you contesting?</CardTitle>
            </CardHeader>
            <CardContent className="space-y-6">
              {!data.scoreBreakdownShown && (
                <p className="text-sm text-muted-foreground">
                  This cycle shows you the final result, not each criterion&apos;s score. You can
                  still name the criteria you want reconsidered and say why.
                </p>
              )}
              {criteria.length === 0 ? (
                <EmptyState
                  icon={Gavel}
                  title="No scored items to contest"
                  description="Your manager's evaluation has no scored criteria on it, so there is nothing itemised to appeal."
                />
              ) : (
                sections.map((section) => (
                  <div key={section.name} className="space-y-3">
                    <div className="flex items-center gap-2">
                      <h3 className="text-sm font-semibold">{section.name}</h3>
                      {section.weight != null && (
                        <Badge variant="outline">Weight {section.weight}%</Badge>
                      )}
                    </div>
                    {section.rows.map((item) => {
                      const checked = !!selected[item.criterionKey];
                      const measured = item.scoringMethod === 'Measured';
                      const against = measured
                        ? formatActualAgainstTarget(item.actualValue, item.targetValue, item.unit)
                        : null;
                      return (
                        <div key={item.criterionKey} className="space-y-3 rounded-lg border p-4">
                          <div className="flex items-start gap-3">
                            <Checkbox
                              id={`item-${item.criterionKey}`}
                              checked={checked}
                              onCheckedChange={(v) =>
                                setSelected({ ...selected, [item.criterionKey]: v === true })
                              }
                              className="mt-1"
                            />
                            <div className="flex-1 space-y-1">
                              <div className="flex flex-wrap items-center gap-2">
                                <Label
                                  htmlFor={`item-${item.criterionKey}`}
                                  className="text-sm font-medium"
                                >
                                  {item.itemName}
                                </Label>
                                <Badge variant="secondary">{item.itemType}</Badge>
                                <Badge variant="outline">Weight {item.weight}%</Badge>
                              </div>
                              {item.description && (
                                <p className="text-xs text-muted-foreground">{item.description}</p>
                              )}
                              {/* The cycle may show the overall only (closure B2): then the server
                                  sends the item without the manager's score. */}
                              {data.scoreBreakdownShown ? (
                                <p className="text-xs text-muted-foreground tabular-nums">
                                  {measured
                                    ? `Measured: ${against ?? 'no actual recorded'} — achievement ${formatCriterionScore(item.score, item.scoringMethod)}`
                                    : `Your manager scored ${formatCriterionScore(item.score, item.scoringMethod)} of ${item.scaleTop}`}
                                  {item.weightedScore != null
                                    ? ` · contributes ${Number(item.weightedScore).toFixed(1)}`
                                    : ''}
                                </p>
                              ) : (
                                measured &&
                                item.targetValue != null && (
                                  <p className="text-xs text-muted-foreground tabular-nums">
                                    {formatActualAgainstTarget(null, item.targetValue, item.unit)}
                                  </p>
                                )
                              )}
                              {data.scoreBreakdownShown && item.achievementOverridden && (
                                <p className="text-xs text-amber-700 dark:text-amber-400">
                                  The achievement was restated by calibration, not read from the
                                  actual.
                                </p>
                              )}
                            </div>
                          </div>

                          {checked && (
                            <div className="space-y-2 pl-7">
                              <Label htmlFor={`reason-${item.criterionKey}`}>
                                {measured ? 'Why this result is wrong' : 'Why this score is wrong'}
                              </Label>
                              <Textarea
                                id={`reason-${item.criterionKey}`}
                                rows={3}
                                value={reasons[item.criterionKey] ?? ''}
                                onChange={(e) =>
                                  setReasons({ ...reasons, [item.criterionKey]: e.target.value })
                                }
                                placeholder={
                                  measured
                                    ? 'What the figure should be, and where the record of it is. Required.'
                                    : 'Point to what you did and where the evidence is. Required.'
                                }
                              />
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                ))
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Anything else HR should know</CardTitle>
            </CardHeader>
            <CardContent>
              <Textarea
                rows={4}
                value={overallReason}
                onChange={(e) => setOverallReason(e.target.value)}
                placeholder="Optional context that applies to the appraisal as a whole."
              />
            </CardContent>
          </Card>

          <div className="flex items-center justify-end gap-3">
            {appealedItems.length === 0 && (
              <p className="text-sm text-muted-foreground">Select at least one item to appeal.</p>
            )}
            {incomplete && (
              <p className="text-sm text-muted-foreground">
                Every selected item needs a reason.
              </p>
            )}
            <Button
              onClick={() => submit.mutate()}
              disabled={appealedItems.length === 0 || incomplete || submit.isPending}
            >
              <Send className="mr-2 h-4 w-4" />
              {submit.isPending ? 'Submitting…' : 'Submit appeal'}
            </Button>
          </div>
        </>
      )}
    </div>
  );
}
