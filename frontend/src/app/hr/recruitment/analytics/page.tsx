'use client';

import { useMemo, useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import {
  ResponsiveContainer,
  BarChart,
  Bar,
  Cell,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
} from 'recharts';
import {
  Loader2,
  Briefcase,
  FileText,
  HandCoins,
  UserCheck,
  Timer,
  Building2,
  Info,
} from 'lucide-react';
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
import { recruitmentDashboardService } from '@/services/hr/recruitment-dashboard.service';

/**
 * Recruitment Analytics — the consumer for `GET api/recruitment-dashboard/analytics?year=`.
 *
 * Built from the same parts as `/hr/training/analytics`, with three rules carried over from the
 * payload's own documentation, because each one is a way this screen could quietly lie:
 *
 * 1. **A null speed figure means "no sample", not "instant".** All four render an em dash, and the
 *    sample size behind them sits on the tile rather than being left implicit.
 * 2. **The acceptance rate's denominator is offers that got an answer**, not offers issued. With
 *    nothing answered it computes to 0, which would read as "everyone declined" — so the card says
 *    which of the two it is before quoting a rate.
 * 3. **Half this payload is point-in-time and ignores the year selector.** Open vacancies, ageing,
 *    empty seats and each recruiter's open count are as-of-today however far back the year goes.
 *    Every one of them says so where it is read, not in a single footnote nobody reaches.
 *
 * Colour: two validated ramps rather than the shadcn `--chart-*` defaults, whose light and dark
 * sets are different hues — a series would change identity with the theme. The slots come from the
 * data-viz reference palette and were re-validated against this app's own card surfaces
 * (`#ffffff` light, `#202020` dark): the three categorical hues clear the CVD, chroma, lightness
 * band and normal-vision gates in both modes, and the blue ordinal ramps clear monotonicity, step
 * separation and the light-end contrast floor. Aqua measures 2.82:1 on the light card, below the
 * 3:1 bar, which obliges the relief rule — hence a table view under every chart, which is also how
 * identity here is never carried by colour alone.
 */

/** Keyed to the entity, not to rank: a series keeps its hue in every chart on this page. */
const SERIES = {
  applications: 'var(--rx-applications)',
  offers: 'var(--rx-offers)',
  hires: 'var(--rx-hires)',
};

/**
 * The palette, as a stylesheet rather than Tailwind arbitrary-property classes, so the values are
 * in one readable block and nothing depends on the scanner finding a colour inside a string array.
 *
 * `.dark` is the only dark selector needed: the app drives theming through next-themes with
 * `attribute="class"`, which resolves even the "system" setting to a real class on `<html>`, and
 * Tailwind's own dark variant here is `&:is(.dark *)`. A `prefers-color-scheme` block would never
 * be the thing that fires.
 *
 * Both ordinal ramps are one hue, light to dark, and are used only for genuinely ordered
 * categories — funnel stages and age bands. The dark column is re-stepped for the dark surface
 * rather than being the same hex flipped.
 */
const VIZ_PALETTE_CSS = `
.rx-viz {
  --rx-applications: #2a78d6;
  --rx-offers: #eb6834;
  --rx-hires: #1baf7a;
  --rx-ord-1: #86b6ef;
  --rx-ord-2: #5598e7;
  --rx-ord-3: #2a78d6;
  --rx-ord-4: #1c5cab;
  --rx-ord-5: #104281;
  --rx-age-1: #86b6ef;
  --rx-age-2: #3987e5;
  --rx-age-3: #1c5cab;
  --rx-age-4: #104281;
}
.dark .rx-viz {
  --rx-applications: #3987e5;
  --rx-offers: #d95926;
  --rx-hires: #199e70;
  --rx-ord-1: #b7d3f6;
  --rx-ord-2: #86b6ef;
  --rx-ord-3: #3987e5;
  --rx-ord-4: #256abf;
  --rx-ord-5: #184f95;
  --rx-age-1: #b7d3f6;
  --rx-age-2: #86b6ef;
  --rx-age-3: #2a78d6;
  --rx-age-4: #184f95;
}
`;

const FUNNEL_RAMP = [
  'var(--rx-ord-1)',
  'var(--rx-ord-2)',
  'var(--rx-ord-3)',
  'var(--rx-ord-4)',
  'var(--rx-ord-5)',
];

const AGE_RAMP = ['var(--rx-age-1)', 'var(--rx-age-2)', 'var(--rx-age-3)', 'var(--rx-age-4)'];

/**
 * `var(--popover)`, not `hsl(var(--popover))`. This app's theme tokens hold whole colour values
 * (`oklch(...)`, `#202020`) rather than the bare `H S% L%` triplets the `hsl()` wrapper expects, so
 * wrapping them produces invalid CSS that is dropped without a warning — leaving recharts' default
 * white tooltip, which is unreadable on a dark card. Several older charts in this repo do exactly
 * that; this one deliberately does not.
 */
const TOOLTIP_STYLE = {
  borderRadius: 8,
  border: '1px solid var(--border)',
  background: 'var(--popover)',
  color: 'var(--popover-foreground)',
  fontSize: 12,
};

/**
 * The hover band, which is also the hit target: it spans the whole month column, so reading a
 * value never means landing on a two-pixel bar.
 */
const TOOLTIP_CURSOR = { fill: 'var(--muted-foreground)', opacity: 0.12 };

/** Server-side rates already arrive as 0-100 to one decimal — multiplying again gives 10,000%. */
const pct = (v: number) => `${Math.round(v * 10) / 10}%`;

const money = (v: number, ccy: string) =>
  `${ccy} ${v.toLocaleString(undefined, { maximumFractionDigits: 0 })}`;

/** Null is the absence of a measurement, and an em dash is the only honest way to draw that. */
const days = (v?: number | null) => (v == null ? '—' : `${v} days`);

export default function RecruitmentAnalyticsPage() {
  const thisYear = new Date().getFullYear();
  const [year, setYear] = useState(thisYear);

  const {
    data: analytics,
    isLoading,
    isFetching,
  } = useQuery({
    queryKey: ['hr', 'recruitment', 'analytics', year],
    queryFn: () => recruitmentDashboardService.getAnalytics(year),
    // Changing the year must not blank the page — hold the last render and dim it instead.
    placeholderData: keepPreviousData,
  });

  const monthly = useMemo(
    () =>
      (analytics?.monthlyTrend ?? []).map((m) => ({
        month: m.monthName,
        Applications: m.applications,
        Offers: m.offers,
        Hires: m.hires,
      })),
    [analytics],
  );

  const hasMonthlyData = monthly.some((m) => m.Applications + m.Offers + m.Hires > 0);

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
        <EmptyState title="No analytics" description="Recruitment analytics could not be loaded." />
      </div>
    );
  }

  const years = Array.from({ length: 6 }, (_, i) => thisYear - i);

  const { funnel, offerOutcomes: offers } = analytics;

  // Every figure on the speed row shares one sample, so it is stated once rather than four times.
  const speedHint =
    analytics.timedHiresSampleSize === 0
      ? `No confirmed starts in ${analytics.year}`
      : analytics.medianTimeToFillDays == null
        ? `${analytics.timedHiresSampleSize} hires timed`
        : `median ${analytics.medianTimeToFillDays} d · ${analytics.timedHiresSampleSize} hires timed`;

  // An offer nobody has answered is not a declined offer. Only accepted + declined carry a rate.
  const respondedOffers = offers.accepted + offers.declined;

  // Draft, pending-approval, on-hold and rejected offers are in the total and in none of the five
  // buckets, so the remainder is shown rather than letting the rows quietly fail to add up.
  const otherOffers = Math.max(
    0,
    offers.totalOffers -
      (offers.accepted + offers.declined + offers.expired + offers.withdrawn + offers.pending),
  );

  const ageing = analytics.vacancyAgeing;
  const hasAgeingData = ageing.some((b) => b.count > 0);

  const funnelStages = [
    { label: 'Applied', value: funnel.applied },
    { label: 'Shortlisted', value: funnel.shortlisted },
    { label: 'Interviewed', value: funnel.interviewed },
    { label: 'Offered', value: funnel.offered },
    { label: 'Hired', value: funnel.hired },
  ];

  return (
    <div
      className={`rx-viz space-y-6 p-6 ${isFetching ? 'opacity-60 transition-opacity' : ''}`}
    >
      <style>{VIZ_PALETTE_CSS}</style>

      <PageHeader
        title="Recruitment Analytics"
        description="How the year's hiring performed — speed, cost, conversion and where the load sits."
        backHref="/hr/recruitment"
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
          {
            label: 'Open vacancies',
            value: analytics.openVacanciesCount,
            icon: Briefcase,
            hint: 'Live campaigns, right now',
          },
          {
            label: 'Applications',
            value: analytics.applicationsYtd,
            icon: FileText,
            hint: `Received in ${analytics.year}`,
          },
          {
            label: 'Offers',
            value: analytics.offersYtd,
            icon: HandCoins,
            hint: `Issued in ${analytics.year}`,
          },
          {
            label: 'Hires',
            value: analytics.hiresYtd,
            icon: UserCheck,
            hint: `Started in ${analytics.year}`,
          },
        ]}
      />

      <MetricTiles
        tiles={[
          {
            label: 'Time to fill',
            value: days(analytics.avgTimeToFillDays),
            icon: Timer,
            hint: speedHint,
          },
          {
            label: 'Time to hire',
            value: days(analytics.avgTimeToHireDays),
            hint: 'Application received → start',
          },
          {
            label: 'Time to shortlist',
            value: days(analytics.avgTimeToShortlistDays),
            hint: 'Published → shortlist closed',
          },
          {
            label: 'Seats standing empty',
            value: analytics.openPositionVacanciesCount,
            icon: Building2,
            // G-14.3: this excludes Anticipated vacancies — seats where notice has been given but
            // the person has not left — while the establishment screen's own stats include them.
            // Both are right, for different questions; what was missing was either screen saying
            // so, which left the same question looking like it had two answers.
            hint:
              analytics.avgPositionVacancyAgeDays == null
                ? 'Empty now — excludes seats with notice served'
                : `${analytics.avgPositionVacancyAgeDays} days empty on average · excludes seats with notice served`,
          },
        ]}
      />

      <Card>
        <CardHeader>
          <CardTitle>By month</CardTitle>
          <CardDescription>
            Two plots rather than one with two scales: an offer or a hire is a fraction of the
            applications behind it, and a shared axis would flatten both onto the baseline.{' '}
            {analytics.year}.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {!hasMonthlyData ? (
            <EmptyState
              title="Nothing recorded"
              description={`No applications, offers or hires in ${analytics.year}.`}
            />
          ) : (
            <>
              <p className="mb-1 text-sm font-medium">Applications received</p>
              <div className="h-[200px] w-full">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={monthly} margin={{ top: 8, right: 8, bottom: 0, left: -20 }}>
                    <CartesianGrid vertical={false} className="stroke-muted" />
                    <XAxis dataKey="month" tickLine={false} axisLine={false} fontSize={12} />
                    <YAxis allowDecimals={false} tickLine={false} axisLine={false} fontSize={12} />
                    <Tooltip cursor={TOOLTIP_CURSOR} contentStyle={TOOLTIP_STYLE} />
                    {/* One series needs no legend — the heading above it is the label. */}
                    <Bar
                      dataKey="Applications"
                      fill={SERIES.applications}
                      radius={[4, 4, 0, 0]}
                      maxBarSize={28}
                    />
                  </BarChart>
                </ResponsiveContainer>
              </div>

              <p className="mb-1 mt-4 text-sm font-medium">Offers and hires</p>
              <div className="h-[220px] w-full">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart
                    data={monthly}
                    margin={{ top: 8, right: 8, bottom: 0, left: -20 }}
                    barGap={2}
                  >
                    <CartesianGrid vertical={false} className="stroke-muted" />
                    <XAxis dataKey="month" tickLine={false} axisLine={false} fontSize={12} />
                    <YAxis allowDecimals={false} tickLine={false} axisLine={false} fontSize={12} />
                    <Tooltip cursor={TOOLTIP_CURSOR} contentStyle={TOOLTIP_STYLE} />
                    <Legend />
                    {/* Grouped, not stacked: a hire in March may be answering an offer made in
                        January, so the two are neither a sum nor a subset of one another. */}
                    <Bar
                      dataKey="Offers"
                      fill={SERIES.offers}
                      radius={[4, 4, 0, 0]}
                      maxBarSize={18}
                    />
                    <Bar dataKey="Hires" fill={SERIES.hires} radius={[4, 4, 0, 0]} maxBarSize={18} />
                  </BarChart>
                </ResponsiveContainer>
              </div>

              <details className="mt-4">
                <summary className="cursor-pointer text-sm text-muted-foreground">
                  View as table
                </summary>
                <div className="mt-2 rounded-md border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Month</TableHead>
                        <TableHead className="text-right">Applications</TableHead>
                        <TableHead className="text-right">Offers</TableHead>
                        <TableHead className="text-right">Hires</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {monthly.map((m) => (
                        <TableRow key={m.month}>
                          <TableCell>{m.month}</TableCell>
                          <TableCell className="text-right tabular-nums">
                            {m.Applications}
                          </TableCell>
                          <TableCell className="text-right tabular-nums">{m.Offers}</TableCell>
                          <TableCell className="text-right tabular-nums">{m.Hires}</TableCell>
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
            {/* ⚠ G-14.4 (2026-09-15): this was titled **Funnel**. The caption was accurate and said
                so — a share of applications, not stage-to-stage conversion — but "Funnel", drawn as
                a descending ramp, is the visual grammar of conversion, and a reader who takes the
                picture at face value reads drop-off rates the numbers do not support. The text was
                right and the form contradicted it; of the two, the form is what gets believed.
                Renamed rather than redrawn: the bars themselves are a perfectly good answer to
                "how far did applications get", which is the question this data can answer. */}
            <CardTitle>How far applications got</CardTitle>
            <CardDescription>
              Applications received in {analytics.year}, each counted against every stage it
              reached — so the bars overlap rather than divide up a total. Not stage-to-stage
              conversion: an application can be interviewed without having been formally
              shortlisted, so a lower bar is not the drop-off from the one above it.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {funnel.applied === 0 ? (
              <EmptyState
                title="No applications"
                description={`Nothing was received in ${analytics.year}.`}
              />
            ) : (
              funnelStages.map((s, i) => (
                <div key={s.label}>
                  <div className="flex items-center justify-between text-sm">
                    <span>{s.label}</span>
                    <span className="font-medium tabular-nums">
                      {s.value}
                      <span className="ml-2 text-xs font-normal text-muted-foreground">
                        {pct((s.value / funnel.applied) * 100)}
                      </span>
                    </span>
                  </div>
                  <div className="mt-1 h-2 w-full overflow-hidden rounded-full bg-muted">
                    <div
                      className="h-full rounded-full"
                      style={{
                        width: `${(s.value / funnel.applied) * 100}%`,
                        backgroundColor: FUNNEL_RAMP[i],
                      }}
                    />
                  </div>
                </div>
              ))
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Offer outcomes</CardTitle>
            <CardDescription>
              {respondedOffers === 0
                ? 'No candidate has answered an offer yet, so there is no acceptance rate to quote.'
                : `${pct(offers.acceptanceRate)} accepted and ${pct(offers.declineRate)} declined — of the ${respondedOffers} offers that got an answer, not of all ${offers.totalOffers}.`}
            </CardDescription>
          </CardHeader>
          <CardContent>
            {offers.totalOffers === 0 ? (
              <EmptyState
                title="No offers"
                description={`No offer was issued in ${analytics.year}.`}
              />
            ) : (
              <div className="rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Outcome</TableHead>
                      <TableHead className="text-right">Offers</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {[
                      { label: 'Accepted', value: offers.accepted },
                      { label: 'Declined', value: offers.declined },
                      { label: 'Expired', value: offers.expired },
                      { label: 'Withdrawn', value: offers.withdrawn },
                      { label: 'Awaiting the candidate', value: offers.pending },
                      ...(otherOffers > 0
                        ? [{ label: 'Not yet sent (draft, approval, on hold)', value: otherOffers }]
                        : []),
                    ].map((r) => (
                      <TableRow key={r.label}>
                        <TableCell>{r.label}</TableCell>
                        <TableCell className="text-right tabular-nums">{r.value}</TableCell>
                      </TableRow>
                    ))}
                    <TableRow>
                      <TableCell className="font-medium">Total issued</TableCell>
                      <TableCell className="text-right font-medium tabular-nums">
                        {offers.totalOffers}
                      </TableCell>
                    </TableRow>
                  </TableBody>
                </Table>
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>What it cost</CardTitle>
          <CardDescription>
            Requisition costs recorded in {analytics.year}, each converted to {analytics.currency}{' '}
            at the rate held against its own cost line.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <p className="text-sm text-muted-foreground">Total spend</p>
              <p className="text-2xl font-bold">
                {money(analytics.totalRecruitmentCost, analytics.currency)}
              </p>
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Cost per hire</p>
              <p className="text-2xl font-bold">
                {analytics.costPerHire == null
                  ? '—'
                  : money(analytics.costPerHire, analytics.currency)}
              </p>
              <p className="mt-1 text-xs text-muted-foreground">
                {analytics.hiresYtd === 0
                  ? 'Nobody was hired, so the spend divides by nothing'
                  : `Across ${analytics.hiresYtd} hires`}
              </p>
            </div>
          </div>

          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Category</TableHead>
                  <TableHead className="text-right">Amount</TableHead>
                  <TableHead className="text-right">Share</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {analytics.costByCategory.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={3}>
                      <EmptyState
                        title="No costs recorded"
                        description={`No requisition cost was entered in ${analytics.year}.`}
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  analytics.costByCategory.map((c) => (
                    <TableRow key={c.category}>
                      <TableCell>{c.categoryName}</TableCell>
                      <TableCell className="text-right tabular-nums">
                        {money(c.amount, analytics.currency)}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {analytics.totalRecruitmentCost > 0
                          ? pct((c.amount / analytics.totalRecruitmentCost) * 100)
                          : '—'}
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Vacancy ageing</CardTitle>
            <CardDescription>
              How long the currently open vacancies have been open. Point-in-time — this reads as of
              today whichever year is selected above.
            </CardDescription>
          </CardHeader>
          <CardContent>
            {!hasAgeingData ? (
              <EmptyState title="No open vacancies" description="Nothing is currently open." />
            ) : (
              <>
                <div className="h-[240px] w-full">
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={ageing} margin={{ top: 8, right: 8, bottom: 0, left: -20 }}>
                      <CartesianGrid vertical={false} className="stroke-muted" />
                      <XAxis dataKey="label" tickLine={false} axisLine={false} fontSize={12} />
                      <YAxis allowDecimals={false} tickLine={false} axisLine={false} fontSize={12} />
                      <Tooltip cursor={TOOLTIP_CURSOR} contentStyle={TOOLTIP_STYLE} />
                      {/* Ordered age bands, so one hue deepening with age — not four identities. */}
                      <Bar dataKey="count" name="Vacancies" radius={[4, 4, 0, 0]} maxBarSize={48}>
                        {ageing.map((b, i) => (
                          <Cell key={b.label} fill={AGE_RAMP[i % AGE_RAMP.length]} />
                        ))}
                      </Bar>
                    </BarChart>
                  </ResponsiveContainer>
                </div>

                <details className="mt-4">
                  <summary className="cursor-pointer text-sm text-muted-foreground">
                    View as table
                  </summary>
                  <div className="mt-2 rounded-md border">
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Age</TableHead>
                          <TableHead className="text-right">Vacancies</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {ageing.map((b) => (
                          <TableRow key={b.label}>
                            <TableCell>{b.label}</TableCell>
                            <TableCell className="text-right tabular-nums">{b.count}</TableCell>
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

        <Card>
          <CardHeader>
            <CardTitle>Where candidates come from</CardTitle>
            <CardDescription>
              Applications in {analytics.year} by source. A hire rate is only as meaningful as the
              applications beside it — one hire out of two applications is 50%, and noise.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Source</TableHead>
                    <TableHead className="text-right">Applications</TableHead>
                    <TableHead className="text-right">Shortlisted</TableHead>
                    <TableHead className="text-right">Hires</TableHead>
                    <TableHead className="text-right">Hire rate</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {analytics.sourceEffectiveness.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={5}>
                        <EmptyState
                          title="No applications"
                          description={`Nothing was received in ${analytics.year}.`}
                        />
                      </TableCell>
                    </TableRow>
                  ) : (
                    analytics.sourceEffectiveness.map((s) => (
                      <TableRow key={s.source}>
                        <TableCell className="font-medium">{s.sourceName}</TableCell>
                        <TableCell className="text-right tabular-nums">{s.applications}</TableCell>
                        <TableCell className="text-right tabular-nums">{s.shortlisted}</TableCell>
                        <TableCell className="text-right tabular-nums">{s.hires}</TableCell>
                        <TableCell className="text-right tabular-nums">{pct(s.hireRate)}</TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </div>
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Longest open</CardTitle>
            <CardDescription>
              The eight oldest vacancies still open right now, whichever year is selected.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Vacancy</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead className="text-right">Applications</TableHead>
                    <TableHead className="text-right">Age</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {analytics.oldestOpenVacancies.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={4}>
                        <EmptyState
                          title="Nothing open"
                          description="No vacancy is currently open."
                        />
                      </TableCell>
                    </TableRow>
                  ) : (
                    analytics.oldestOpenVacancies.map((v) => (
                      <TableRow key={v.vacancyId}>
                        <TableCell>
                          <p className="font-medium">{v.jobTitle}</p>
                          <p className="text-xs text-muted-foreground">
                            {v.vacancyNumber}
                            {v.recruiterName ? ` · ${v.recruiterName}` : ''}
                          </p>
                        </TableCell>
                        <TableCell>
                          <Badge variant="outline">{v.statusName}</Badge>
                        </TableCell>
                        <TableCell className="text-right tabular-nums">
                          {v.applicationCount}
                        </TableCell>
                        <TableCell className="text-right tabular-nums">{v.ageDays} d</TableCell>
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
            <CardTitle>Recruiter load</CardTitle>
            <CardDescription>
              Open vacancies are what each recruiter is carrying now; applications and hires are
              their {analytics.year} totals.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Recruiter</TableHead>
                    <TableHead className="text-right">Open</TableHead>
                    <TableHead className="text-right">Applications</TableHead>
                    <TableHead className="text-right">Hires</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {analytics.recruiterLoad.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={4}>
                        <EmptyState
                          title="No recruiter assigned"
                          description="Load appears once open vacancies name a recruiter."
                        />
                      </TableCell>
                    </TableRow>
                  ) : (
                    analytics.recruiterLoad.map((r) => (
                      <TableRow key={r.recruiterId}>
                        <TableCell className="font-medium">{r.recruiterName}</TableCell>
                        <TableCell className="text-right tabular-nums">{r.openVacancies}</TableCell>
                        <TableCell className="text-right tabular-nums">{r.applications}</TableCell>
                        <TableCell className="text-right tabular-nums">{r.hiresYtd}</TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </div>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardContent className="flex items-start gap-3 p-4">
          <Info className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
          <p className="text-xs text-muted-foreground">
            Applications, offers, hires and costs are filtered to {analytics.year}. Open vacancies,
            vacancy ageing, empty seats and each recruiter&apos;s open count are point-in-time and
            read as of today — the year selector does not move them.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
