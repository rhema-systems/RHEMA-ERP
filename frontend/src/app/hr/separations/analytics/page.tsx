'use client';

/**
 * Exit analytics — what the separations register adds up to.
 *
 * Two editorial decisions here are worth stating, because a dashboard that gets them wrong reads
 * confidently and is wrong:
 *
 * - **Coverage before averages.** The exit-interview panel leads with how many exits were actually
 *   interviewed. An average score over four interviews out of ninety exits describes those four
 *   people; showing it as a headline figure invites it to be read as the organisation's temperature.
 * - **Unvalued is not zero.** Where a settlement carries lines nobody could value, that count is
 *   shown next to the money rather than folded into it, because the money total is then an
 *   understatement of a known size rather than a fact.
 */

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, ArrowRight, Loader2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { separationService } from '@/services/hr/separation.service';
import type { SeparationBreakdownRow } from '@/types/hr/separation';

const pct = (v?: number | null) => (v == null ? '—' : `${v.toFixed(1)}%`);
const score = (v?: number | null) => (v == null ? 'Not asked' : v.toFixed(2));

function Stat({ label, value, hint, tone }: {
  label: string;
  value: string | number;
  hint?: string;
  tone?: 'default' | 'warning';
}) {
  return (
    <Card className={tone === 'warning' ? 'border-amber-300' : undefined}>
      <CardContent className="pt-6">
        <p className="text-sm text-muted-foreground">{label}</p>
        <p className={`mt-1 text-2xl font-semibold ${tone === 'warning' ? 'text-amber-700' : ''}`}>
          {value}
        </p>
        {hint && <p className="mt-1 text-xs text-muted-foreground">{hint}</p>}
      </CardContent>
    </Card>
  );
}

/** A share bar. Percentages come from the server so the parts always sum to the whole. */
function Breakdown({ rows, empty }: { rows: SeparationBreakdownRow[]; empty: string }) {
  if (rows.length === 0) {
    return <p className="text-sm text-muted-foreground">{empty}</p>;
  }
  return (
    <div className="space-y-3">
      {rows.map((r) => (
        <div key={r.key} className="space-y-1">
          <div className="flex items-baseline justify-between text-sm">
            <span>{r.label}</span>
            <span className="text-muted-foreground">
              {r.count} · {r.percentage.toFixed(1)}%
            </span>
          </div>
          <div className="h-2 w-full rounded bg-muted">
            <div
              className="h-2 rounded bg-primary"
              style={{ width: `${Math.min(r.percentage, 100)}%` }}
            />
          </div>
        </div>
      ))}
    </div>
  );
}

export default function SeparationAnalyticsPage() {
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');

  const range = { from: from || undefined, to: to || undefined };

  const { data: analytics, isLoading, error } = useQuery({
    queryKey: ['separation-analytics', from, to],
    queryFn: () => separationService.getAnalytics(range.from, range.to),
  });

  const { data: themes } = useQuery({
    queryKey: ['separation-exit-themes', from, to],
    queryFn: () => separationService.getExitInterviewThemes(range.from, range.to),
  });

  const money = (v: number) =>
    `${analytics?.currencyCode ?? ''} ${v.toLocaleString(undefined, {
      minimumFractionDigits: 2, maximumFractionDigits: 2,
    })}`.trim();

  return (
    <div className="space-y-6">
      <PageHeader
        title="Exit analytics"
        description="Who left, by which route, what it cost, and where the pipeline is stuck."
        backHref="/hr/separations"
        actions={
          <Button variant="outline" asChild>
            <Link href="/hr/separations">
              Register
              <ArrowRight className="ml-2 h-4 w-4" />
            </Link>
          </Button>
        }
      />

      <Card>
        <CardContent className="flex flex-wrap items-end gap-4 pt-6">
          <div className="space-y-1.5">
            <Label htmlFor="from">From</Label>
            <Input id="from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="to">To</Label>
            <Input id="to" type="date" value={to} onChange={(e) => setTo(e.target.value)} />
          </div>
          {(from || to) && (
            <Button variant="ghost" onClick={() => { setFrom(''); setTo(''); }}>
              Clear
            </Button>
          )}
          <p className="text-xs text-muted-foreground">
            Defaults to the twelve months ending today. Exits are dated by their effective date, so
            a separation that completes today but takes effect next month counts next month.
          </p>
        </CardContent>
      </Card>

      {error && (
        <Alert variant="destructive">
          <AlertDescription>{(error as Error).message}</AlertDescription>
        </Alert>
      )}

      {isLoading && (
        <div className="flex items-center justify-center py-16 text-muted-foreground">
          <Loader2 className="mr-2 h-5 w-5 animate-spin" />
          Loading…
        </div>
      )}

      {analytics && (
        <>
          {/* ⚠ This area's founding defect, surfaced as a number: an exit that completed and never
              reached the employee record leaves somebody dismissed still counted as on strength. */}
          {analytics.completedButNotApplied > 0 && (
            <Alert variant="destructive">
              <AlertTriangle className="h-4 w-4" />
              <AlertDescription>
                <span className="font-medium">
                  {analytics.completedButNotApplied} completed separation
                  {analytics.completedButNotApplied === 1 ? '' : 's'} never reached the employee
                  record.
                </span>{' '}
                Those people are still counted as on strength. Run the repair from the register.
              </AlertDescription>
            </Alert>
          )}

          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Stat
              label="Exits in the period"
              value={analytics.completedInPeriod}
              hint={`${analytics.raisedInPeriod} raised in the same period`}
            />
            <Stat
              label="Exit rate"
              value={pct(analytics.exitRatePercent)}
              hint={`Against ${analytics.activeHeadcount.toLocaleString()} on strength`}
            />
            <Stat
              label="In flight"
              value={analytics.inFlight}
              hint="Neither completed, cancelled nor rejected"
            />
            <Stat
              label="Never applied"
              value={analytics.completedButNotApplied}
              hint="Completed exits not reflected on the employee record"
              tone={analytics.completedButNotApplied > 0 ? 'warning' : 'default'}
            />
          </div>

          <div className="grid gap-4 lg:grid-cols-2">
            <Card>
              <CardHeader><CardTitle className="text-base">By route</CardTitle></CardHeader>
              <CardContent>
                <Breakdown rows={analytics.byRoute} empty="No completed exits in this period." />
              </CardContent>
            </Card>

            <Card>
              <CardHeader><CardTitle className="text-base">By reason</CardTitle></CardHeader>
              <CardContent>
                <Breakdown rows={analytics.byReason} empty="No completed exits in this period." />
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Where things are sitting</CardTitle>
              <p className="text-sm text-muted-foreground">
                Everything in flight, whenever it was raised — a separation stuck since last year is
                exactly what this is for, so the date filter does not apply here.
              </p>
            </CardHeader>
            <CardContent>
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                {analytics.pipeline.map((stage) => (
                  <div key={stage.status} className="rounded-md border p-3">
                    <p className="text-sm font-medium">{stage.label}</p>
                    <p className="text-2xl font-semibold">{stage.count}</p>
                    {/* Null oldestDays means the stage is empty — not "arrived today". */}
                    {stage.count > 0 && stage.oldestDays != null && (
                      <p className="mt-1 text-xs text-muted-foreground">
                        Oldest waiting {stage.oldestDays} day{stage.oldestDays === 1 ? '' : 's'}
                      </p>
                    )}
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Final settlements</CardTitle>
              <p className="text-sm text-muted-foreground">
                Counted only from settlements Internal Audit has passed.
              </p>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 sm:grid-cols-3">
                <div>
                  <p className="text-sm text-muted-foreground">Earnings</p>
                  <p className="text-xl font-semibold">{money(analytics.settledEarnings)}</p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Recoveries</p>
                  <p className="text-xl font-semibold">{money(analytics.settledRecoveries)}</p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Net payable</p>
                  <p className="text-xl font-semibold">{money(analytics.settledNetPayable)}</p>
                </div>
              </div>

              {/* ⚠ Shown beside the money, never folded into it. */}
              {analytics.settlementsWithUnvaluedLines > 0 && (
                <Alert>
                  <AlertTriangle className="h-4 w-4" />
                  <AlertDescription>
                    {analytics.settlementsWithUnvaluedLines} settlement
                    {analytics.settlementsWithUnvaluedLines === 1 ? ' carries' : 's carry'} lines
                    that could not be valued. The totals above are understated by an unknown amount.
                  </AlertDescription>
                </Alert>
              )}
            </CardContent>
          </Card>

          {themes && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">What leavers said</CardTitle>
                <p className="text-sm text-muted-foreground">
                  Averages run over the answers actually given. A question that was not asked is
                  left out rather than counted as a low score.
                </p>
              </CardHeader>
              <CardContent className="space-y-5">
                {/* Coverage first, deliberately: it is what makes the averages readable. */}
                <div className="flex flex-wrap items-center gap-3">
                  <Badge variant={themes.coveragePercent >= 50 ? 'secondary' : 'outline'}>
                    {pct(themes.coveragePercent)} of exits interviewed
                  </Badge>
                  <span className="text-sm text-muted-foreground">
                    {themes.interviewsConducted} conducted, {themes.interviewsDeclined} declined, of
                    {' '}{themes.separationsInPeriod} exits
                  </span>
                  {themes.interviewsRecorded > 0 && (
                    <span className="text-sm text-muted-foreground">
                      · {pct(themes.declineRatePercent)} declined
                    </span>
                  )}
                </div>

                {themes.interviewsConducted === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    No interviews have been conducted in this period, so there is nothing to average.
                  </p>
                ) : (
                  <>
                    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                      <Stat label="Overall experience" value={score(themes.averageOverallExperience)} hint="out of 5" />
                      <Stat label="Management" value={score(themes.averageManagement)} hint="out of 5" />
                      <Stat label="Pay and benefits" value={score(themes.averagePayAndBenefits)} hint="out of 5" />
                      <Stat label="Career development" value={score(themes.averageCareerDevelopment)} hint="out of 5" />
                    </div>

                    <div className="grid gap-4 sm:grid-cols-2">
                      <Stat
                        label="Would recommend TDC as an employer"
                        value={pct(themes.wouldRecommendPercent)}
                        hint="Of those asked"
                      />
                      <Stat
                        label="Would consider returning"
                        value={pct(themes.wouldReturnPercent)}
                        hint="Of those asked"
                      />
                    </div>

                    <div>
                      <p className="mb-3 text-sm font-medium">
                        Why they said they were leaving
                      </p>
                      <Breakdown
                        rows={themes.byPrimaryReason}
                        empty="No interview recorded a reason."
                      />
                    </div>
                  </>
                )}
              </CardContent>
            </Card>
          )}
        </>
      )}
    </div>
  );
}
