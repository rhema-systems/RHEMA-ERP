'use client';

import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  LabelList,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { EmptyState } from '@/components/hr/common/EmptyState';

/**
 * The three charts the performance analytics screens draw.
 *
 * **Each is a single series**, so none of them carries a legend or a colour key: the category
 * name on the axis is the identity, and the value is written next to the mark. One colour for
 * every mark, rather than a per-row one — the API sends a colour on grade rows, but it is a
 * rotating decorative palette that would encode nothing here.
 *
 * ⚠ Every one of these reads is derived from `overallScore`, which is not set until HR signs an
 * appraisal off. Empty is the normal state for most of a cycle, so each chart says so in words
 * rather than drawing an empty axis.
 */

/**
 * The series colour, formerly `var(--chart-1)`.
 *
 * Despite the name, `--chart-1` is not a light/dark step of one hue: globals.css defines it as
 * `oklch(0.646 0.222 41.116)` (orange) in light and `oklch(0.488 0.243 264.376)` (violet-blue) in
 * dark, so these charts changed hue with the theme. It is slot 1 of the validated categorical
 * palette now — the same blue the recruitment and training analytics screens use — checked against
 * this app's own card surfaces (`#ffffff` light, `#202020` dark) rather than the reference
 * palette's: both steps clear the lightness band, the chroma floor and 3:1 contrast.
 *
 * It travels as a CSS custom property because a colour held in a JS constant cannot follow the
 * theme; `.dark` is the only selector needed, since next-themes runs with `attribute="class"` and
 * `enableSystem` and so resolves even the "system" setting to a real class on `<html>`.
 */
const PALETTE_CSS = `
.pf-viz { --pf-series: #2a78d6; }
.dark .pf-viz { --pf-series: #3987e5; }
`;

const SERIES = 'var(--pf-series)';

/**
 * `var(--muted-foreground)`, not `hsl(var(--muted-foreground))`.
 *
 * This app's theme tokens hold whole colour values (`oklch(0.556 0 0)`, `#a3a3a3`), not the bare
 * `H S% L%` triplets that the `hsl()` wrapper expects. Wrapping one produces a declaration the
 * browser discards without a warning, so these ticks were falling back to SVG's default black fill
 * — invisible against a dark card, and never the muted grey they were written to be.
 */
const AXIS_TICK = { fontSize: 12, fill: 'var(--muted-foreground)' };

/**
 * The hover band behind a bar, which is also its hit target. Drawn from `--muted-foreground` at low
 * alpha rather than `--muted`: the latter is a near-white grey, so on a light card it would be
 * invisible at any opacity, which is how this started as `hsl(var(--muted))` and went unnoticed.
 */
const BAR_CURSOR = { fill: 'var(--muted-foreground)', opacity: 0.12 };

interface TooltipEntry {
  name?: string;
  value?: number | string;
  payload?: { label?: string; hint?: string };
}

/** Recharts' default tooltip ignores the theme; this one wears the app's card tokens. */
function ChartTooltip({
  active,
  payload,
  label,
}: {
  active?: boolean;
  payload?: TooltipEntry[];
  label?: string | number;
}) {
  if (!active || !payload?.length) return null;
  const hint = payload[0]?.payload?.hint;
  return (
    <div className="rounded-md border bg-popover px-3 py-2 text-xs shadow-md">
      <p className="font-medium text-popover-foreground">{label}</p>
      {payload.map((entry, i) => (
        <p key={i} className="text-muted-foreground">
          {entry.name}: <span className="tabular-nums text-popover-foreground">{entry.value}</span>
        </p>
      ))}
      {hint && <p className="mt-1 text-muted-foreground">{hint}</p>}
    </div>
  );
}

export interface DistributionDatum {
  label: string;
  count: number;
  /** Shown under the count in the tooltip — a share, usually. */
  hint?: string;
}

/**
 * A count per band, drawn as horizontal bars so the band names stay readable
 * ("Exceeds Expectations" does not fit under a vertical bar).
 *
 * Rows arrive in the order they should be read — grades and ratings are both ordered scales, so
 * the caller's order is preserved rather than sorted by size.
 */
export function DistributionBars({
  data,
  valueName = 'Appraisals',
  emptyTitle,
  emptyDescription,
  height = 220,
}: {
  data: DistributionDatum[];
  valueName?: string;
  emptyTitle: string;
  emptyDescription: string;
  height?: number;
}) {
  const total = data.reduce((sum, d) => sum + d.count, 0);

  if (total === 0) {
    return <EmptyState title={emptyTitle} description={emptyDescription} />;
  }

  return (
    <>
      <style>{PALETTE_CSS}</style>
      <ResponsiveContainer
        className="pf-viz"
        width="100%"
        height={Math.max(height, data.length * 34 + 40)}
      >
        <BarChart data={data} layout="vertical" margin={{ top: 4, right: 32, bottom: 4, left: 8 }}>
          <CartesianGrid horizontal={false} strokeDasharray="3 3" className="stroke-muted" />
          <XAxis type="number" tick={AXIS_TICK} tickLine={false} axisLine={false} allowDecimals={false} />
          <YAxis
            type="category"
            dataKey="label"
            width={150}
            tick={AXIS_TICK}
            tickLine={false}
            axisLine={false}
          />
          <Tooltip content={<ChartTooltip />} cursor={BAR_CURSOR} />
          <Bar dataKey="count" name={valueName} fill={SERIES} radius={[0, 4, 4, 0]} barSize={18}>
            {data.map((d) => (
              <Cell key={d.label} />
            ))}
            <LabelList
              dataKey="count"
              position="right"
              className="fill-muted-foreground"
              fontSize={12}
            />
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </>
  );
}

export interface TrendDatum {
  /** X label — the cycle year, or the cycle name where two cycles share a year. */
  label: string;
  score: number;
  hint?: string;
}

/**
 * One employee's overall score across cycles. Fixed 0–100 so two people's charts, or the same
 * person's in two years, can be compared by eye — an auto-scaled axis would make a flat run of
 * scores look like a rollercoaster.
 */
export function PerformanceTrendChart({
  data,
  height = 240,
}: {
  data: TrendDatum[];
  height?: number;
}) {
  if (data.length === 0) {
    return (
      <EmptyState
        title="No scored appraisals yet"
        description="A point appears here once an appraisal has been signed off with an overall score."
      />
    );
  }

  return (
    <>
      <style>{PALETTE_CSS}</style>
      <ResponsiveContainer className="pf-viz" width="100%" height={height}>
        <LineChart data={data} margin={{ top: 12, right: 24, bottom: 4, left: 0 }}>
          <CartesianGrid strokeDasharray="3 3" className="stroke-muted" />
          <XAxis dataKey="label" tick={AXIS_TICK} tickLine={false} axisLine={false} />
          <YAxis domain={[0, 100]} tick={AXIS_TICK} tickLine={false} axisLine={false} width={40} />
          <Tooltip content={<ChartTooltip />} />
          <Line
            type="monotone"
            dataKey="score"
            name="Overall score"
            stroke={SERIES}
            strokeWidth={2}
            dot={{ r: 4, strokeWidth: 2 }}
            activeDot={{ r: 6 }}
          >
            <LabelList
              dataKey="score"
              position="top"
              className="fill-muted-foreground"
              fontSize={12}
            />
          </Line>
        </LineChart>
      </ResponsiveContainer>
    </>
  );
}
