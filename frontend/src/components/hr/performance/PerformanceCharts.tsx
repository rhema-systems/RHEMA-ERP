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
 * name on the axis is the identity, and the value is written next to the mark. Colour comes from
 * the design system's `--chart-1`, which is already stepped for light and dark, rather than from
 * a per-row colour — the API sends one for grade rows, but it is a rotating decorative palette
 * that would encode nothing here.
 *
 * ⚠ Every one of these reads is derived from `overallScore`, which is not set until HR signs an
 * appraisal off. Empty is the normal state for most of a cycle, so each chart says so in words
 * rather than drawing an empty axis.
 */

const SERIES = 'var(--chart-1)';
const AXIS_TICK = { fontSize: 12, fill: 'hsl(var(--muted-foreground))' };

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
    <ResponsiveContainer width="100%" height={Math.max(height, data.length * 34 + 40)}>
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
        <Tooltip content={<ChartTooltip />} cursor={{ fill: 'hsl(var(--muted))', opacity: 0.4 }} />
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
    <ResponsiveContainer width="100%" height={height}>
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
  );
}
