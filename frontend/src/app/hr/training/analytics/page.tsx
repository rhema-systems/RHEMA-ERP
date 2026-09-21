'use client';

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  ResponsiveContainer,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
} from 'recharts';
import { Loader2, TrendingUp, Award, ShieldAlert, Coins } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { trainingAnalyticsService } from '@/services/hr/training-analytics.service';

/**
 * Two-series categorical palette, validated for both modes against the CVD, chroma, lightness-band
 * and contrast checks rather than picked by eye. Light steps sit in L 0.43–0.77 on a light surface;
 * the dark steps are re-stepped for L 0.48–0.67, not an automatic flip of the same hex.
 *
 * ⚠ The dark steps were declared here from the first version and then never read — every fill took
 * the `.light` value, so dark mode drew the light palette onto a dark card. The values themselves
 * were right (both pairs still pass every gate against this app's own card surfaces, `#ffffff` and
 * `#202020`); only the wiring was missing. They travel as CSS custom properties now, which is how a
 * colour held in a JS constant can follow the theme without the chart re-rendering.
 *
 * `.dark` is the only selector needed: next-themes runs with `attribute="class"` and `enableSystem`,
 * so even the "system" setting resolves to a real class on `<html>`.
 */
const SERIES_PALETTE_CSS = `
.tr-viz {
  --tr-passed: #2563eb;
  --tr-not-passed: #d97706;
}
.dark .tr-viz {
  --tr-passed: #3b82f6;
  --tr-not-passed: #d97706;
}
`;

const SERIES = {
  passed: 'var(--tr-passed)',
  notPassed: 'var(--tr-not-passed)',
};

const pct = (v: number) => `${Math.round(v * 10) / 10}%`;
const money = (v: number, ccy: string) =>
  `${ccy} ${v.toLocaleString(undefined, { maximumFractionDigits: 0 })}`;

export default function TrainingAnalyticsPage() {
  const thisYear = new Date().getFullYear();
  const [year, setYear] = useState(thisYear);

  const { data: analytics, isLoading } = useQuery({
    queryKey: ['hr', 'training', 'analytics', year],
    queryFn: () => trainingAnalyticsService.getAnalytics(year),
  });
  const { data: dashboard } = useQuery({
    queryKey: ['hr', 'training', 'dashboard', year],
    queryFn: () => trainingAnalyticsService.getDashboard(year),
  });

  // Passed is a subset of completed, so plotting both as peers would double-count the same event.
  // Stacking passed + not-passed sums to completions, which is what the axis then means.
  const monthly = useMemo(
    () =>
      (analytics?.monthlyCompletions ?? []).map((m) => ({
        month: m.monthName,
        Passed: m.passed,
        'Not passed': Math.max(0, m.completed - m.passed),
      })),
    [analytics],
  );

  const hasMonthlyData = monthly.some((m) => m.Passed > 0 || m['Not passed'] > 0);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!analytics) {
    return (
      <div className="p-6">
        <EmptyState title="No analytics" description="The dashboard could not be loaded." />
      </div>
    );
  }

  const years = Array.from({ length: 6 }, (_, i) => thisYear - i);

  // A rate with no denominator is not a low score, it is an unstarted programme — so the tile says
  // which one it is rather than showing a confident 0%.
  const complianceTile =
    analytics.complianceRecordsCount === 0
      ? 'Nobody assigned'
      : pct(analytics.complianceRate);

  return (
    <div className="tr-viz space-y-6 p-6">
      <style>{SERIES_PALETTE_CSS}</style>

      <PageHeader
        title="Training Analytics"
        description="Operations and effectiveness across the organisation."
        backHref="/hr/training"
        actions={
          <Select value={String(year)} onValueChange={(v) => setYear(Number(v))}>
            <SelectTrigger className="w-32">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {years.map((y) => (
                <SelectItem key={y} value={String(y)}>
                  {y}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        }
      />

      <MetricTiles
        tiles={[
          { label: 'Completions', value: analytics.completionsYtd, icon: TrendingUp },
          { label: 'Pass rate', value: pct(analytics.passRate) },
          { label: 'Certificates issued', value: analytics.certificatesIssuedYtd, icon: Award },
          { label: 'Compliance', value: complianceTile, icon: ShieldAlert },
        ]}
      />

      <MetricTiles
        tiles={[
          { label: 'Active programmes', value: analytics.activeProgramsCount },
          {
            label: 'Budget allocated',
            value: money(analytics.budgetAllocated, analytics.currency),
            icon: Coins,
          },
          { label: 'Budget used', value: pct(analytics.budgetUtilizationRate) },
          {
            label: 'Cost per completion',
            value:
              analytics.costPerCompletion == null
                ? '—'
                : money(analytics.costPerCompletion, analytics.currency),
          },
        ]}
      />

      <Card>
        <CardHeader>
          <CardTitle>Completions by month</CardTitle>
          <CardDescription>
            Stacked, so the column height is the month&apos;s completions and the split is the
            outcome. {analytics.year}.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {!hasMonthlyData ? (
            <EmptyState
              title="No completions recorded"
              description={`Nothing was completed in ${analytics.year}.`}
            />
          ) : (
            <>
              <div className="h-[320px] w-full">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={monthly} margin={{ top: 8, right: 8, bottom: 0, left: -20 }}>
                    <CartesianGrid strokeDasharray="3 3" vertical={false} className="stroke-muted" />
                    <XAxis dataKey="month" tickLine={false} axisLine={false} fontSize={12} />
                    <YAxis allowDecimals={false} tickLine={false} axisLine={false} fontSize={12} />
                    <Tooltip
                      contentStyle={{
                        borderRadius: 8,
                        border: '1px solid var(--border)',
                        background: 'var(--popover)',
                        color: 'var(--popover-foreground)',
                        fontSize: 12,
                      }}
                    />
                    <Legend />
                    {/* 2px gap between stacked segments, rounded data-end on the top segment only. */}
                    <Bar
                      dataKey="Passed"
                      stackId="a"
                      fill={SERIES.passed}
                      maxBarSize={28}
                    />
                    <Bar
                      dataKey="Not passed"
                      stackId="a"
                      fill={SERIES.notPassed}
                      radius={[4, 4, 0, 0]}
                      maxBarSize={28}
                    />
                  </BarChart>
                </ResponsiveContainer>
              </div>

              {/* Identity is never colour-alone: the same numbers are readable as a table. */}
              <details className="mt-4">
                <summary className="cursor-pointer text-sm text-muted-foreground">
                  View as table
                </summary>
                <div className="mt-2 rounded-md border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Month</TableHead>
                        <TableHead className="text-right">Completed</TableHead>
                        <TableHead className="text-right">Passed</TableHead>
                        <TableHead className="text-right">Not passed</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {monthly.map((m) => (
                        <TableRow key={m.month}>
                          <TableCell>{m.month}</TableCell>
                          <TableCell className="text-right">
                            {m.Passed + m['Not passed']}
                          </TableCell>
                          <TableCell className="text-right">{m.Passed}</TableCell>
                          <TableCell className="text-right">{m['Not passed']}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </details>
            </>
          )}
        </CardContent>
      </Card>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Effectiveness</CardTitle>
            <CardDescription>
              Kirkpatrick stages. These are counts of different things, not a strict funnel —
              feedback comes from whoever attended, so it can exceed completions.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {[
              { label: 'Nominated (approved/confirmed)', value: analytics.funnel.nominated },
              { label: 'Attended', value: analytics.funnel.attended },
              { label: 'Completed', value: analytics.funnel.completed },
              { label: 'Gave feedback (L1)', value: analytics.funnel.feedbackL1 },
              { label: 'Follow-up assessed (L2/3)', value: analytics.funnel.followUpL23 },
            ].map((s) => {
              const max = Math.max(analytics.funnel.nominated, analytics.funnel.feedbackL1, 1);
              return (
                <div key={s.label}>
                  <div className="flex items-center justify-between text-sm">
                    <span>{s.label}</span>
                    <span className="font-medium">{s.value}</span>
                  </div>
                  <div className="mt-1 h-2 w-full overflow-hidden rounded-full bg-muted">
                    <div
                      className="h-full rounded-full"
                      style={{
                        width: `${(s.value / max) * 100}%`,
                        backgroundColor: SERIES.passed,
                      }}
                    />
                  </div>
                </div>
              );
            })}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Reaction and learning</CardTitle>
            <CardDescription>
              Null means nobody answered — not zero. {analytics.feedbackResponses} responses.
            </CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-2">
            {[
              { label: 'Avg satisfaction', value: analytics.avgSatisfaction, suffix: '/5' },
              { label: 'Would recommend', value: analytics.wouldRecommendRate, suffix: '%' },
              { label: 'Likelihood to apply', value: analytics.avgLikelihoodToApply, suffix: '/5' },
              { label: 'Follow-ups completed', value: analytics.followUpsCompleted, suffix: '' },
              { label: 'Avg pre-score', value: analytics.avgPreScore, suffix: '' },
              { label: 'Avg post-score', value: analytics.avgPostScore, suffix: '' },
              { label: 'Avg gain', value: analytics.avgScoreGain, suffix: '' },
            ].map((m) => (
              <div key={m.label}>
                <p className="text-sm text-muted-foreground">{m.label}</p>
                <p className="text-lg font-medium">
                  {m.value == null ? '—' : `${m.value}${m.suffix}`}
                </p>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>By category</CardTitle>
            <CardDescription>
              Each category keeps the colour it was given in setup, so this is a table with swatches
              rather than a colour-coded chart.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Category</TableHead>
                    <TableHead className="text-right">Programmes</TableHead>
                    <TableHead className="text-right">Completions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {analytics.categoryBreakdown.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={3}>
                        <EmptyState title="No categories" description="Nothing to break down yet." />
                      </TableCell>
                    </TableRow>
                  ) : (
                    analytics.categoryBreakdown.map((c) => (
                      <TableRow key={c.categoryOptionId ?? c.categoryName}>
                        <TableCell>
                          <span className="flex items-center gap-2">
                            <span
                              aria-hidden
                              className="inline-block h-3 w-3 shrink-0 rounded-sm border"
                              style={{ backgroundColor: c.categoryColor ?? 'transparent' }}
                            />
                            {c.categoryName}
                          </span>
                        </TableCell>
                        <TableCell className="text-right">{c.programsCount}</TableCell>
                        <TableCell className="text-right">{c.completionsCount}</TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Trainer utilisation</CardTitle>
            <CardDescription>Busiest trainers by sessions delivered.</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Trainer</TableHead>
                    <TableHead className="text-right">Sessions</TableHead>
                    <TableHead className="text-right">Hours</TableHead>
                    <TableHead className="text-right">Rating</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {analytics.topTrainers.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={4}>
                        <EmptyState
                          title="No trainers yet"
                          description="Utilisation appears once sessions are delivered."
                        />
                      </TableCell>
                    </TableRow>
                  ) : (
                    analytics.topTrainers.map((t) => (
                      <TableRow key={t.trainerId}>
                        <TableCell className="font-medium">{t.name}</TableCell>
                        <TableCell className="text-right">{t.sessionsDelivered}</TableCell>
                        <TableCell className="text-right">{t.hoursDelivered}</TableCell>
                        <TableCell className="text-right">
                          {t.averageRating == null ? (
                            '—'
                          ) : (
                            <span>
                              {t.averageRating}
                              <span className="ml-1 text-xs text-muted-foreground">
                                ({t.ratingsCount})
                              </span>
                            </span>
                          )}
                        </TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </div>
          </CardContent>
        </Card>
      </div>

      {dashboard && (
        <Card>
          <CardHeader>
            <CardTitle>Right now</CardTitle>
            <CardDescription>Current state, independent of the selected year.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-3">
            <Badge variant="outline">{dashboard.pendingNominationsCount} nominations pending</Badge>
            <Badge variant="outline">{dashboard.upcomingSchedulesCount} upcoming runs</Badge>
            <Badge variant="outline">
              {dashboard.expiringCertificatesIn30Days} certificates expiring in 30 days
            </Badge>
            <Badge variant="outline">
              {dashboard.activeMentoringPairsCount} active mentoring pairs
            </Badge>
            <Badge variant="outline">
              {dashboard.activeLearningPathEnrollmentsCount} learning paths in progress
            </Badge>
            <Badge variant={dashboard.nonCompliantEmployeesCount > 0 ? 'destructive' : 'outline'}>
              {dashboard.nonCompliantEmployeesCount} non-compliant
            </Badge>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
