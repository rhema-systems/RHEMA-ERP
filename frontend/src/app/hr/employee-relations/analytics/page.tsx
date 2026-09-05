'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Loader2, AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { employeeRelationsAnalyticsService } from '@/services/hr/employee-relations-admin.service';
import {
  GRIEVANCE_LADDER,
  type ErCountSlice, type ErRate,
} from '@/types/hr/employee-relations';

const levelLabel = (v: string) => GRIEVANCE_LADDER.find((l) => l.value === v)?.label ?? v;

/**
 * ⚠ A rate with no denominator renders as "—", never as 0%.
 *
 * "Nobody complied" and "nobody was asked" are different facts, and area 7 shipped a compliance
 * figure that showed both as 0%. The server already refuses to send a bare percentage — `percent`
 * is null when the denominator is zero — so the only way to reintroduce the defect here would be to
 * coalesce it. Do not.
 */
function RateCard({ rate }: { rate: ErRate }) {
  return (
    <Card>
      <CardContent className="pt-6">
        <div className="text-xs uppercase tracking-wide text-muted-foreground">{rate.label}</div>
        <div className="mt-2 text-2xl font-semibold">
          {rate.noData || rate.percent === null || rate.percent === undefined
            ? <span className="text-muted-foreground">—</span>
            : `${rate.percent}%`}
        </div>
        <div className="mt-1 text-xs text-muted-foreground">
          {rate.noData
            ? 'nothing to take a share of yet'
            : `${rate.numerator} of ${rate.denominator}`}
        </div>
      </CardContent>
    </Card>
  );
}

/** A breakdown, each row carrying the denominator it is a share OF — which is not always the total. */
function Breakdown({ title, note, slices }: { title: string; note?: string; slices: ErCountSlice[] }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{title}</CardTitle>
        {note && <p className="mt-1 text-xs text-muted-foreground">{note}</p>}
      </CardHeader>
      <CardContent className="p-0">
        {slices.length === 0 ? (
          <div className="px-6 pb-6 text-sm text-muted-foreground">Nothing in this window.</div>
        ) : (
          <Table>
            <TableBody>
              {slices.map((s) => (
                <TableRow key={s.key}>
                  <TableCell>{s.label}</TableCell>
                  <TableCell className="w-24 text-right font-medium">{s.count}</TableCell>
                  <TableCell className="w-40 text-right text-muted-foreground">
                    {s.percent === null || s.percent === undefined ? '—' : `${s.percent}% of ${s.total}`}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}

function Metric({ label, value, hint }: { label: string; value: React.ReactNode; hint?: string }) {
  return (
    <Card>
      <CardContent className="pt-6">
        <div className="text-xs uppercase tracking-wide text-muted-foreground">{label}</div>
        <div className="mt-2 text-2xl font-semibold">{value}</div>
        {hint && <div className="mt-1 text-xs text-muted-foreground">{hint}</div>}
      </CardContent>
    </Card>
  );
}

export default function EmployeeRelationsAnalyticsPage() {
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [range, setRange] = useState<{ from?: string; to?: string }>({});

  const { data: a, isLoading } = useQuery({
    queryKey: ['hr', 'employee-relations', 'analytics', range],
    queryFn: () => employeeRelationsAnalyticsService.get(range),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Employee-relations analytics"
        description="Every figure on this page is computed from one set of cases over one window, so nothing here can disagree with anything else here."
        backHref="/hr/employee-relations"
      />

      <div className="flex flex-wrap items-end gap-2">
        <div>
          <Label htmlFor="an-from" className="text-xs text-muted-foreground">Filed on or after</Label>
          <Input id="an-from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="w-44" />
        </div>
        <div>
          <Label htmlFor="an-to" className="text-xs text-muted-foreground">Filed on or before</Label>
          <Input id="an-to" type="date" value={to} onChange={(e) => setTo(e.target.value)} className="w-44" />
        </div>
        <Button
          variant="outline"
          onClick={() => setRange({ ...(from ? { from } : {}), ...(to ? { to } : {}) })}
        >
          Apply
        </Button>
        {(range.from || range.to) && (
          <Button variant="ghost" onClick={() => { setFrom(''); setTo(''); setRange({}); }}>
            All time
          </Button>
        )}
      </div>

      {isLoading || !a ? (
        <div className="flex items-center justify-center p-16">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : (
        <>
          <div className="grid gap-4 md:grid-cols-5">
            <Metric label="Cases" value={a.totalCases} />
            <Metric label="Open" value={a.openCases} />
            <Metric label="Resolved" value={a.resolvedCases} />
            <Metric label="Withdrawn" value={a.withdrawnCases} hint="taken back, not settled" />
            <Metric label="Closed unresolved" value={a.closedUnresolvedCases} hint="ladder exhausted" />
          </div>

          <div className="grid gap-4 md:grid-cols-3">
            <RateCard rate={a.escalationRate} />
            <RateCard rate={a.outcomeNotRecordedRate} />
            <RateCard rate={a.agreementMissingRate} />
          </div>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Time to resolution</CardTitle>
              {/* Stated on the page, because a reader who assumes otherwise will misread it. */}
              <p className="mt-1 text-xs text-muted-foreground">
                Resolved cases only. A withdrawal is not a resolution, and counting one would shorten
                the average every time somebody gave up — the figure would improve as the process got
                worse.
              </p>
            </CardHeader>
            <CardContent>
              <div className="grid gap-4 md:grid-cols-4">
                <Metric label="Resolved in window" value={a.resolutionTime.resolvedCount} />
                <Metric
                  label="Average days"
                  value={a.resolutionTime.averageDays ?? <span className="text-muted-foreground">—</span>}
                />
                <Metric
                  label="Median days"
                  value={a.resolutionTime.medianDays ?? <span className="text-muted-foreground">—</span>}
                />
                <Metric
                  label="Longest"
                  value={a.resolutionTime.longestDays ?? <span className="text-muted-foreground">—</span>}
                />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Where the ladder is stuck</CardTitle>
              <p className="mt-1 text-xs text-muted-foreground">
                Open cases whose current rung owes an answer. &ldquo;Nobody named&rdquo; counts the
                ones the responder matrix does not cover — the org-authority gap, in cases rather
                than in coverage percentages.
              </p>
            </CardHeader>
            <CardContent className="p-0">
              {a.stuckAtRung.length === 0 ? (
                <EmptyState title="Nothing stuck" description="Every open case has been answered at the rung it sits at." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Rung</TableHead>
                      <TableHead className="text-right">Waiting</TableHead>
                      <TableHead className="text-right">Longest wait</TableHead>
                      <TableHead className="text-right">Nobody named</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {a.stuckAtRung.map((r) => (
                      <TableRow key={r.level}>
                        <TableCell className="font-medium">{levelLabel(r.level)}</TableCell>
                        <TableCell className="text-right">{r.count}</TableCell>
                        <TableCell className="text-right">{r.oldestWaitingDays} days</TableCell>
                        <TableCell className="text-right">
                          {r.unassigned > 0 ? (
                            <span className="inline-flex items-center gap-1 text-amber-600">
                              <AlertTriangle className="h-3 w-3" />{r.unassigned}
                            </span>
                          ) : '0'}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>

          <div className="grid gap-4 lg:grid-cols-2">
            <Breakdown title="By case type" slices={a.byCaseType} />
            <Breakdown title="By status" slices={a.byStatus} />
            <Breakdown
              title="Where open cases are sitting"
              note="A share of the OPEN cases, not of every case — where a case sits means nothing once it has ended."
              slices={a.byCurrentLevel}
            />
            <Breakdown
              title="By outcome"
              note="A share of the RESOLVED cases. Cases with no resolution record predate the resolve path and are counted, not dropped, so the breakdown adds up."
              slices={a.byOutcome}
            />
            <Breakdown title="By organisation unit" note="Top 20 units by case count." slices={a.byOrganizationUnit} />
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Anonymous concerns</CardTitle>
                {/* The reason there is no unit breakdown here, said out loud. */}
                <p className="mt-1 text-xs text-muted-foreground">
                  Counted, never cross-tabbed. There is deliberately no breakdown by unit or
                  reporter: in a unit of four, &ldquo;one fraud concern this quarter&rdquo; is an
                  identification rather than a statistic.
                </p>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="grid gap-4 sm:grid-cols-3">
                  <Metric label="Reported" value={a.concernsReported} />
                  <Metric label="Untriaged" value={a.concernsUntriaged} hint="nobody has looked yet" />
                  <Metric label="Became a case" value={a.concernsConverted} />
                </div>
                {a.concernsByCategory.length > 0 && (
                  <Table>
                    <TableBody>
                      {a.concernsByCategory.map((s) => (
                        <TableRow key={s.key}>
                          <TableCell>{s.label}</TableCell>
                          <TableCell className="w-20 text-right font-medium">{s.count}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </CardContent>
            </Card>
          </div>
        </>
      )}
    </div>
  );
}
