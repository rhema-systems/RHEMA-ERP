'use client';

import Link from 'next/link';
import React, { useState, type CSSProperties, type ReactNode } from 'react';
import {
  ArrowRight,
  BarChart3,
  CircleDot,
  TrendingUp,
} from 'lucide-react';
import {
  Area,
  Bar,
  CartesianGrid,
  ComposedChart,
  Line,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { cn } from '@/lib/utils';

const DASHBOARD_COLORS = ['#2474ff', '#21bd87', '#8b5cf6', '#ff9f2e', '#ef4444', '#06b6d4'];

const compactNumber = (value: number) => new Intl.NumberFormat('en', {
  notation: 'compact',
  maximumFractionDigits: 1,
}).format(value);

const formatStageMoney = (amount: number, currency: string, compact = false) =>
  new Intl.NumberFormat('en-US', {
    style: 'currency', currency, currencyDisplay: 'code',
    maximumFractionDigits: compact ? 1 : 0,
    ...(compact ? { notation: 'compact' as const } : {}),
  }).format(amount);

const riskColor = (name: string) => {
  const band = name.trim().toLowerCase();
  if (band.includes('critical') || band.includes('high')) return '#ef4444';
  if (band.includes('medium') || band.includes('moderate')) return '#f97316';
  if (band.includes('low')) return '#3b82f6';
  if (band.includes('no risk') || band.includes('none')) return '#22c55e';
  return '#94a3b8';
};

export function ExecutiveWidget({
  title,
  description,
  href,
  actionLabel = 'View details',
  icon,
  children,
  className,
}: {
  title: string;
  description?: string;
  href?: string;
  actionLabel?: string;
  icon?: ReactNode;
  children: ReactNode;
  className?: string;
}) {
  return (
    <section
      className={cn(
        'overflow-hidden rounded-2xl border border-slate-200/75 bg-white shadow-[0_10px_32px_-24px_rgba(15,23,42,0.42)] dark:border-neutral-700/80 dark:bg-[#1d1d1d]',
        className,
      )}
    >
      <div className="flex items-start justify-between gap-3 px-4 pb-2 pt-4 sm:px-5">
        <div className="flex min-w-0 items-start gap-2.5">
          {icon ? (
            <span className="mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-lg bg-blue-50 text-blue-600 dark:bg-blue-950/60 dark:text-blue-300">
              {icon}
            </span>
          ) : null}
          <div className="min-w-0">
            <h2 className="text-[0.95rem] font-bold tracking-tight text-slate-950 dark:text-white">{title}</h2>
            {description ? <p className="mt-0.5 truncate text-[0.7rem] text-slate-500 dark:text-slate-400">{description}</p> : null}
          </div>
        </div>
        {href ? (
          <Link
            href={href}
            className="group inline-flex shrink-0 items-center gap-1 rounded-md px-1.5 py-1 text-[0.7rem] font-semibold text-blue-600 transition-colors hover:bg-blue-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:text-blue-300 dark:hover:bg-blue-950/40"
          >
            {actionLabel}
            <ArrowRight className="h-3 w-3 transition-transform group-hover:translate-x-0.5" aria-hidden="true" />
          </Link>
        ) : null}
      </div>
      {children}
    </section>
  );
}

export function DashboardEmptyState({
  title,
  description,
  href,
  actionLabel,
}: {
  title: string;
  description: string;
  href?: string;
  actionLabel?: string;
}) {
  return (
    <div className="mx-4 mb-4 flex min-h-24 items-center gap-3 rounded-xl border border-dashed border-slate-200 bg-slate-50/80 p-4 dark:border-neutral-700 dark:bg-neutral-900/50 sm:mx-5">
      <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-white text-slate-400 shadow-sm dark:bg-neutral-800">
        <CircleDot className="h-4 w-4" aria-hidden="true" />
      </span>
      <div className="min-w-0 flex-1">
        <p className="text-sm font-semibold text-slate-800 dark:text-slate-100">{title}</p>
        <p className="mt-0.5 text-xs text-slate-500 dark:text-slate-400">{description}</p>
      </div>
      {href && actionLabel ? (
        <Link href={href} className="shrink-0 text-xs font-semibold text-blue-600 hover:underline dark:text-blue-300">
          {actionLabel} →
        </Link>
      ) : null}
    </div>
  );
}

export function DashboardModuleUnavailableWidget({
  title,
  moduleName,
  accessRestricted = false,
  href,
  className,
}: {
  title: string;
  moduleName: string;
  accessRestricted?: boolean;
  href?: string;
  className?: string;
}) {
  const message = accessRestricted
    ? `${moduleName} dashboard access is restricted for this account.`
    : `${moduleName} dashboard data could not be loaded. Try refreshing the page.`;

  return (
    <ExecutiveWidget title={title} description={message} className={className}>
      <DashboardEmptyState
        title={accessRestricted ? `${moduleName} dashboard access restricted` : `${moduleName} dashboard unavailable`}
        description={message}
        href={accessRestricted ? undefined : href}
        actionLabel={href ? `Open ${moduleName}` : undefined}
      />
    </ExecutiveWidget>
  );
}

export function DashboardCoverageNotice({ modules }: { modules: string[] }) {
  if (modules.length === 0) return null;

  return (
    <p role="status" className="mx-4 mb-3 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-200 sm:mx-5">
      Partial view: {modules.join(', ')} dashboard data is restricted or unavailable.
    </p>
  );
}

function ChartTooltip({ active, payload, label, formatValue }: {
  active?: boolean;
  payload?: Array<{ color?: string; dataKey?: string | number; name?: string; value?: number }>;
  label?: string;
  formatValue: (value: number) => string;
}) {
  if (!active || !payload?.length) return null;
  return (
    <div className="rounded-xl border border-slate-200 bg-white/95 p-3 shadow-xl backdrop-blur dark:border-neutral-700 dark:bg-neutral-900/95">
      <p className="mb-2 text-xs font-semibold text-slate-700 dark:text-slate-200">{label}</p>
      {payload.map((entry, index) => (
        <p key={`${entry.dataKey ?? entry.name ?? 'series'}-${index}`} className="flex items-center gap-2 text-xs text-slate-600 dark:text-slate-300">
          <span className="h-2 w-2 rounded-full" style={{ backgroundColor: entry.color }} />
          <span>{entry.name}</span>
          <strong className="ml-auto pl-3 text-slate-900 dark:text-white">{formatValue(Number(entry.value ?? 0))}</strong>
        </p>
      ))}
    </div>
  );
}

export function FinancialPerformanceWidget({
  data,
  formatValue,
  href,
}: {
  data: Array<{ name: string; revenue: number; expenses: number }>;
  formatValue: (value: number) => string;
  href: string;
}) {
  const chartData = data.map((point) => ({ ...point, net: point.revenue - point.expenses }));
  return (
    <ExecutiveWidget
      title="Revenue vs Expenses"
      description="Posted general-ledger movement for the selected period"
      href={href}
      actionLabel="Open ledger"
      icon={<BarChart3 className="h-4 w-4" />}
      className="min-h-[285px]"
    >
      {chartData.length === 0 ? (
        <DashboardEmptyState title="No financial movement yet" description="Posted revenue and expense activity will appear here." href={href} actionLabel="Open ledger" />
      ) : (
        <div className="h-[225px] px-2 pb-3 pr-4 sm:px-3">
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart data={chartData} margin={{ top: 8, right: 4, left: -18, bottom: 0 }}>
              <defs>
                <linearGradient id="executiveNetFill" x1="0" x2="0" y1="0" y2="1">
                  <stop offset="0%" stopColor="#21bd87" stopOpacity={0.26} />
                  <stop offset="100%" stopColor="#21bd87" stopOpacity={0.02} />
                </linearGradient>
              </defs>
              <CartesianGrid vertical={false} stroke="#94a3b8" strokeOpacity={0.18} />
              <XAxis dataKey="name" axisLine={false} tickLine={false} tick={{ fill: '#64748b', fontSize: 10 }} />
              <YAxis axisLine={false} tickLine={false} tick={{ fill: '#64748b', fontSize: 10 }} tickFormatter={compactNumber} />
              <Tooltip content={<ChartTooltip formatValue={formatValue} />} cursor={{ fill: '#eff6ff', opacity: 0.45 }} />
              <Area type="monotone" dataKey="net" name="Net position" stroke="#21bd87" strokeWidth={2.5} fill="url(#executiveNetFill)" />
              <Bar dataKey="revenue" name="Revenue" fill="#2474ff" fillOpacity={0.78} radius={[5, 5, 0, 0]} barSize={14} />
              <Bar dataKey="expenses" name="Expenses" fill="#ff6574" fillOpacity={0.72} radius={[5, 5, 0, 0]} barSize={14} />
            </ComposedChart>
          </ResponsiveContainer>
        </div>
      )}
    </ExecutiveWidget>
  );
}

export function ExpenseAccountsWidget({
  data,
  formatValue,
  href,
}: {
  data: Array<{ name: string; value: number }>;
  formatValue: (value: number) => string;
  href: string;
}) {
  const visible = data.slice(0, 5);
  const maximum = Math.max(1, ...visible.map((item) => item.value));
  return (
    <ExecutiveWidget title="Top Expense Accounts" description="Largest posted balances" href={href} actionLabel="View all" icon={<TrendingUp className="h-4 w-4" />} className="min-h-[285px]">
      {visible.length === 0 ? (
        <DashboardEmptyState title="No expense balances yet" description="Posted expense accounts will appear here." href={href} actionLabel="Open ledger" />
      ) : (
        <div className="space-y-3 px-4 pb-4 pt-2 sm:px-5">
          {visible.map((item, index) => (
            <Link key={item.name} href={href} className="group grid grid-cols-[minmax(6.5rem,0.8fr)_minmax(5rem,1fr)_auto] items-center gap-3 text-xs">
              <span className="truncate font-medium text-slate-700 group-hover:text-blue-700 dark:text-slate-200 dark:group-hover:text-blue-300">{item.name}</span>
              <span className="h-2.5 overflow-hidden rounded-full bg-slate-100 dark:bg-neutral-800">
                <span
                  className="block h-full rounded-full transition-[width] duration-500"
                  style={{ width: `${Math.max(6, (item.value / maximum) * 100)}%`, backgroundColor: DASHBOARD_COLORS[(index + 4) % DASHBOARD_COLORS.length] }}
                />
              </span>
              <strong className="whitespace-nowrap text-[0.7rem] text-slate-900 dark:text-white">{formatValue(item.value)}</strong>
            </Link>
          ))}
        </div>
      )}
    </ExecutiveWidget>
  );
}

export function PipelineWidget({
  data,
  href,
  getHref,
  preferredCurrency,
  pipelineAsOf,
}: {
  data: Array<{
    stageId: string;
    stage: string;
    stageOrder: number;
    isClosed: boolean;
    isWon: boolean;
    opportunities: number;
    quotes: number;
    percentageOfActivePipeline: number;
    averageAgeDays: number;
    stalledOpportunityCount: number;
    overdueOpportunityCount: number;
    amountsByCurrency?: Array<{ currency: string; amount: number }>;
    weightedAmountsByCurrency?: Array<{ currency: string; amount: number }>;
    opportunitiesWithoutCurrencyCount?: number;
  }>;
  href: string;
  getHref: (stageId: string) => string;
  preferredCurrency?: string;
  pipelineAsOf?: string;
}) {
  const visible = [...data].sort((left, right) => left.stageOrder - right.stageOrder || left.stage.localeCompare(right.stage));
  const activeStages = visible.filter((item) => !item.isClosed);
  const total = activeStages.reduce((sum, item) => sum + item.opportunities, 0);
  const hasRecords = visible.some((item) => item.opportunities > 0);
  const currencies = Array.from(new Set(data.flatMap((item) => [
    ...(item.amountsByCurrency?.map((value) => value.currency) ?? []),
    ...(item.weightedAmountsByCurrency?.map((value) => value.currency) ?? []),
  ]))).sort();
  const [metric, setMetric] = useState<'value' | 'weighted' | 'count'>('value');
  const [currency, setCurrency] = useState(() => preferredCurrency && currencies.includes(preferredCurrency) ? preferredCurrency : currencies[0] ?? '');
  const selectedCurrency = currencies.includes(currency)
    ? currency
    : preferredCurrency && currencies.includes(preferredCurrency) ? preferredCurrency : currencies[0] ?? '';
  const metricValue = (item: typeof data[number]) => metric === 'count'
    ? item.opportunities
    : (metric === 'weighted' ? item.weightedAmountsByCurrency : item.amountsByCurrency)
        ?.find((value) => value.currency === selectedCurrency)?.amount ?? 0;
  const maximum = Math.max(1, ...visible.map(metricValue));
  const totalsByCurrency = currencies.map((code) => ({
    currency: code,
    amount: activeStages.reduce((sum, item) => sum + (
      (metric === 'weighted' ? item.weightedAmountsByCurrency : item.amountsByCurrency)
        ?.find((value) => value.currency === code)?.amount ?? 0
    ), 0),
  }));
  const missingCurrencyCount = data.reduce((sum, item) => sum + (item.opportunitiesWithoutCurrencyCount ?? 0), 0);
  return (
    <ExecutiveWidget title="CRM Pipeline" href={href} actionLabel="View opportunities" className="min-h-[255px]">
      {!hasRecords ? (
        <DashboardEmptyState title="No CRM data yet" description="Opportunities will appear here once created." href={href} actionLabel="Open CRM" />
      ) : (
        <div className="px-4 pb-4 sm:px-5">
          <div className="mb-3 flex items-start justify-between gap-2 border-b border-slate-100 pb-3 dark:border-neutral-800">
            <div className="min-w-0">
              <span className="block text-[0.68rem] font-medium text-slate-500 dark:text-slate-400">
                {metric === 'count' ? 'Active opportunities' : metric === 'weighted' ? 'Active weighted pipeline' : 'Active pipeline value'}
              </span>
              {metric !== 'count' ? (
                totalsByCurrency.length > 0 ? (
                  <div className="mt-1 flex flex-wrap gap-x-3 gap-y-1">
                    {totalsByCurrency.map((item) => (
                      <strong key={item.currency} className="block break-words text-lg font-bold leading-tight text-slate-950 dark:text-white">
                        {formatStageMoney(item.amount, item.currency)}
                      </strong>
                    ))}
                  </div>
                ) : <strong className="mt-1 block text-sm text-slate-500">No recorded value</strong>
              ) : <strong className="mt-1 block text-xl font-bold leading-tight text-slate-950 dark:text-white">{total.toLocaleString()}</strong>}
            </div>
            <select
              aria-label="CRM pipeline metric"
              value={metric}
              onChange={(event) => setMetric(event.target.value as 'value' | 'weighted' | 'count')}
              className="max-w-28 rounded-lg border border-slate-200 bg-white px-2 py-1 text-[0.68rem] font-medium text-slate-700 dark:border-neutral-700 dark:bg-neutral-900 dark:text-slate-200"
            >
              <option value="value">By Value</option>
              <option value="weighted">Weighted</option>
              <option value="count">By count</option>
            </select>
          </div>
          {metric !== 'count' && currencies.length > 1 ? (
            <label className="mb-3 flex items-center gap-2 text-[0.68rem] text-slate-500 dark:text-slate-400">
              Compare bars in
              <select value={selectedCurrency} onChange={(event) => setCurrency(event.target.value)} aria-label="CRM pipeline bar currency" className="rounded-lg border border-slate-200 bg-white px-2 py-1 font-medium text-slate-700 dark:border-neutral-700 dark:bg-neutral-900 dark:text-slate-200">
                {currencies.map((code) => <option key={code} value={code}>{code}</option>)}
              </select>
            </label>
          ) : null}
          <div className="space-y-2.5">
            {visible.map((item, index) => (
              <Link key={item.stageId} href={getHref(item.stageId)} title={`${item.opportunities} opportunities · ${item.averageAgeDays} average days in stage · ${item.stalledOpportunityCount} stalled · ${item.overdueOpportunityCount} overdue`} className="group grid grid-cols-[minmax(5.5rem,0.8fr)_minmax(3rem,1.1fr)_auto] items-center gap-2.5 text-[0.7rem]">
                <span className="truncate font-medium text-slate-600 group-hover:text-blue-700 dark:text-slate-300 dark:group-hover:text-blue-300">{item.stage}</span>
                <span className="h-2.5 overflow-hidden rounded-full bg-slate-100 dark:bg-neutral-800">
                  <span className="block h-full rounded-full bg-blue-600 transition-[width] duration-500" style={{ width: `${metricValue(item) > 0 ? Math.max(4, (metricValue(item) / maximum) * 100) : 0}%`, opacity: 1 - index * 0.1 }} />
                </span>
                {metric === 'count' ? <strong className="whitespace-nowrap text-right text-slate-900 dark:text-white">{item.opportunities}</strong> : (
                  <span className="flex flex-col items-end text-right font-semibold text-slate-900 dark:text-white">
                    {(metric === 'weighted' ? item.weightedAmountsByCurrency : item.amountsByCurrency)?.length
                      ? (metric === 'weighted' ? item.weightedAmountsByCurrency : item.amountsByCurrency)?.map((value) => <span key={value.currency} className="whitespace-nowrap">{formatStageMoney(value.amount, value.currency, true)}</span>)
                      : <span className="text-slate-400">—</span>}
                  </span>
                )}
              </Link>
            ))}
          </div>
          {missingCurrencyCount > 0 && metric !== 'count' ? (
            <p className="mt-3 text-[0.65rem] text-slate-500 dark:text-slate-400">{missingCurrencyCount} opportunities without a recorded currency are excluded from value totals.</p>
          ) : null}
          {pipelineAsOf ? <p className="mt-2 text-[0.62rem] text-slate-400">Current-state snapshot as of {new Date(pipelineAsOf).toLocaleString()}.</p> : null}
        </div>
      )}
    </ExecutiveWidget>
  );
}

export function ConversionFunnelWidget({
  data,
  href,
  getHref,
  preferredCurrency,
  rangeStart,
  rangeEnd,
  historyCoverageStart,
  lostOpportunityCount = 0,
  dataQualityIssues = [],
}: {
  data: Array<{
    stageId: string;
    stage: string;
    stageOrder: number;
    count: number;
    conversionRate: number | null;
    overallConversionRate: number | null;
    amountsByCurrency?: Array<{ currency: string; amount: number }>;
    opportunitiesWithoutCurrencyCount?: number;
  }>;
  href: string;
  getHref: (stageId: string) => string;
  preferredCurrency?: string;
  rangeStart?: string;
  rangeEnd?: string;
  historyCoverageStart?: string | null;
  lostOpportunityCount?: number;
  dataQualityIssues?: string[];
}) {
  const currencies = Array.from(new Set(data.flatMap((item) => item.amountsByCurrency?.map((value) => value.currency) ?? []))).sort();
  const [metric, setMetric] = useState(() => preferredCurrency && currencies.includes(preferredCurrency) ? preferredCurrency : currencies[0] ?? 'count');
  const selectedMetric = metric === 'count' || currencies.includes(metric)
    ? metric
    : preferredCurrency && currencies.includes(preferredCurrency) ? preferredCurrency : currencies[0] ?? 'count';
  const metricValue = (item: typeof data[number]) => selectedMetric === 'count'
    ? item.count
    : item.amountsByCurrency?.find((value) => value.currency === selectedMetric)?.amount ?? 0;
  const visible = [...data]
    .sort((left, right) => left.stageOrder - right.stageOrder || left.stage.localeCompare(right.stage));
  const total = visible[0] ? metricValue(visible[0]) : 0;
  const missingCurrencyCount = data.reduce((sum, item) => sum + (item.opportunitiesWithoutCurrencyCount ?? 0), 0);
  const funnelColors = ['#2563eb', '#3b82f6', '#8b5cf6', '#f59e0b', '#10b981'];
  const widths = visible.map((_, index) => Math.max(32, 100 - index * (68 / Math.max(visible.length - 1, 1))));
  const rangeLabel = rangeStart && rangeEnd
    ? `${new Date(rangeStart).toLocaleDateString()}–${new Date(rangeEnd).toLocaleDateString()}`
    : null;
  return (
    <ExecutiveWidget title="Sales Conversion Funnel" description={rangeLabel ? `Stage entries during ${rangeLabel}` : 'Historical opportunity stage entries'} href={href} actionLabel="View details" className="min-h-[255px]">
      {data.length === 0 ? (
        <DashboardEmptyState title="No funnel data yet" description="Opportunity stages will appear here once recorded." href={href} actionLabel="Open opportunities" />
      ) : (
        <div className="px-4 pb-4 pt-1 sm:px-5">
          <div className="mb-3 flex items-center justify-between gap-2">
            <span className="truncate text-[0.68rem] font-medium text-slate-500 dark:text-slate-400">
              {selectedMetric === 'count' ? `${total} opportunities` : `${formatStageMoney(total, selectedMetric)} in selected currency`}
            </span>
            <select
              aria-label="Conversion funnel metric"
              value={selectedMetric}
              onChange={(event) => setMetric(event.target.value)}
              className="max-w-28 rounded-lg border border-slate-200 bg-white px-2 py-1 text-[0.68rem] font-medium text-slate-700 dark:border-neutral-700 dark:bg-neutral-900 dark:text-slate-200"
            >
              <option value="count">By count</option>
              {currencies.map((currency) => <option key={currency} value={currency}>{currency} value</option>)}
            </select>
          </div>
          <div className="grid grid-cols-[minmax(6rem,1.05fr)_minmax(8rem,0.95fr)] items-center gap-3">
          <div className="mx-auto w-full max-w-[155px] space-y-[2px]" role="img" aria-label="Opportunity stage funnel">
            {visible.map((item, index) => {
              const amount = metricValue(item);
              return (
                <div key={item.stageId} className="mx-auto h-6 rounded-[7px] transition-[width] duration-500" style={{ width: `${widths[index]}%`, backgroundColor: funnelColors[index % funnelColors.length], clipPath: 'polygon(2% 0, 98% 0, 89% 100%, 11% 100%)' }} aria-label={`${item.stage}: ${selectedMetric === 'count' ? `${item.count} opportunities` : formatStageMoney(amount, selectedMetric)}`} />
              );
            })}
          </div>
          <div className="space-y-[2px]">
            {visible.map((item, index) => (
              <Link key={item.stageId} href={getHref(item.stageId)} title={item.conversionRate === null ? 'No prior-stage denominator' : `${item.conversionRate.toFixed(1)}% from prior stage`} className="grid h-6 grid-cols-[minmax(0,1fr)_auto] items-center gap-2 rounded-md px-1 text-[0.68rem] hover:bg-blue-50 hover:text-blue-700 dark:hover:bg-blue-950/30 dark:hover:text-blue-300">
                <span className="flex min-w-0 items-center gap-1.5"><span className="h-2 w-2 shrink-0 rounded-full" style={{ backgroundColor: funnelColors[index % funnelColors.length] }} /><span className="truncate text-slate-600 dark:text-slate-300">{item.stage}</span></span>
                <strong className="whitespace-nowrap text-slate-900 dark:text-white">
                  {selectedMetric === 'count' ? item.count : formatStageMoney(metricValue(item), selectedMetric, true)}
                  <span className="ml-1 font-normal text-slate-500">({item.overallConversionRate === null ? '—' : `${Math.round(item.overallConversionRate)}%`})</span>
                </strong>
              </Link>
            ))}
          </div>
          </div>
          {missingCurrencyCount > 0 && selectedMetric !== 'count' ? (
            <p className="mt-2 text-[0.65rem] text-slate-500 dark:text-slate-400">{missingCurrencyCount} opportunities without currency are excluded from value totals.</p>
          ) : null}
          {lostOpportunityCount > 0 ? <p className="mt-2 text-[0.65rem] text-slate-500 dark:text-slate-400">Lost during range: {lostOpportunityCount}.</p> : null}
          {historyCoverageStart ? <p className="mt-1 text-[0.62rem] text-slate-400">Tracked stage history starts {new Date(historyCoverageStart).toLocaleDateString()}.</p> : null}
          {dataQualityIssues.length > 0 ? <p className="mt-1 line-clamp-2 text-[0.62rem] text-amber-700 dark:text-amber-300">{dataQualityIssues[0]}</p> : null}
        </div>
      )}
    </ExecutiveWidget>
  );
}

export function RiskMixWidget({
  data,
  href,
}: {
  data: Array<{ name: string; value: number }>;
  href: string;
}) {
  const total = data.reduce((sum, item) => sum + item.value, 0);
  const ordered = [...data].sort((left, right) => {
    const rank = (name: string) => name.toLowerCase().includes('critical') ? 0 : name.toLowerCase().includes('high') ? 1 : name.toLowerCase().includes('medium') ? 2 : name.toLowerCase().includes('low') ? 3 : name.toLowerCase().includes('no risk') ? 4 : 5;
    return rank(left.name) - rank(right.name) || left.name.localeCompare(right.name);
  });
  let cursor = 0;
  const segments = ordered.map((item) => {
    const start = cursor;
    cursor += total ? (item.value / total) * 100 : 0;
    return `${riskColor(item.name)} ${start}% ${cursor}%`;
  });
  const donutStyle: CSSProperties = { background: total ? `conic-gradient(${segments.join(',')})` : '#e2e8f0' };
  return (
    <ExecutiveWidget title="Account Risk Mix" description="Active business-partner health" href={href} actionLabel="View accounts" className="min-h-[255px]">
      {data.length === 0 ? (
        <DashboardEmptyState title="No account risk data yet" description="Risk bands will appear after account assessments." href={href} actionLabel="Open accounts" />
      ) : (
        <div className="grid grid-cols-[7.5rem_1fr] items-center gap-5 px-4 pb-4 pt-1 sm:px-5">
          <div className="relative mx-auto h-28 w-28 rounded-full shadow-inner" style={donutStyle}>
            <div className="absolute inset-[18px] flex flex-col items-center justify-center rounded-full bg-white shadow-sm dark:bg-[#1d1d1d]">
              <strong className="text-xl text-slate-950 dark:text-white">{total}</strong>
              <span className="text-[0.6rem] text-slate-500">Accounts</span>
            </div>
          </div>
          <div className="space-y-2.5">
            {ordered.slice(0, 5).map((item) => (
              <Link key={item.name} href={`${href}?healthCategory=${encodeURIComponent(item.name)}`} className="grid grid-cols-[auto_1fr_auto] items-center gap-2 text-[0.68rem] hover:text-blue-700 dark:hover:text-blue-300">
                <span className="h-2.5 w-2.5 rounded-sm" style={{ backgroundColor: riskColor(item.name) }} />
                <span className="truncate text-slate-600 dark:text-slate-300">{item.name}</span>
                <strong>{item.value} <span className="font-normal text-slate-400">({total ? Math.round((item.value / total) * 100) : 0}%)</span></strong>
              </Link>
            ))}
          </div>
        </div>
      )}
    </ExecutiveWidget>
  );
}

export function CompactProgressWidget({
  title,
  description,
  data,
  href,
  actionLabel,
  emptyTitle,
  emptyDescription,
}: {
  title: string;
  description: string;
  data: Array<{ name: string; value: number }>;
  href: string;
  actionLabel: string;
  emptyTitle: string;
  emptyDescription: string;
}) {
  const maximum = Math.max(1, ...data.map((item) => item.value));
  return (
    <ExecutiveWidget title={title} description={description} href={href} actionLabel={actionLabel}>
      {data.length === 0 ? (
        <DashboardEmptyState title={emptyTitle} description={emptyDescription} href={href} actionLabel={actionLabel} />
      ) : (
        <div className="space-y-3 px-4 pb-4 pt-1 sm:px-5">
          {data.slice(0, 5).map((item, index) => (
            <Link href={href} key={item.name} className="group grid grid-cols-[minmax(6.5rem,0.9fr)_1fr_auto] items-center gap-3 text-[0.7rem]">
              <span className="truncate text-slate-600 group-hover:text-blue-700 dark:text-slate-300 dark:group-hover:text-blue-300">{item.name}</span>
              <span className="h-2 overflow-hidden rounded-full bg-slate-100 dark:bg-neutral-800">
                <span className="block h-full rounded-full" style={{ width: `${Math.max(5, (item.value / maximum) * 100)}%`, backgroundColor: DASHBOARD_COLORS[(index + 4) % DASHBOARD_COLORS.length] }} />
              </span>
              <strong>{item.value}</strong>
            </Link>
          ))}
        </div>
      )}
    </ExecutiveWidget>
  );
}

export function MaintenanceTrendWidget({
  data,
  href,
}: {
  data: Array<{ period: string; created: number; completed: number }>;
  href: string;
}) {
  return (
    <ExecutiveWidget title="Maintenance Trend" description="Created versus completed work orders" href={href} actionLabel="View work orders">
      {data.length === 0 ? (
        <DashboardEmptyState title="No maintenance trend yet" description="Work-order movement will appear here as activity is recorded." href={href} actionLabel="Open work orders" />
      ) : (
        <div className="h-[155px] px-2 pb-3 pr-4">
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart data={data} margin={{ top: 6, right: 0, left: -24, bottom: 0 }}>
              <CartesianGrid vertical={false} stroke="#94a3b8" strokeOpacity={0.16} />
              <XAxis dataKey="period" axisLine={false} tickLine={false} tick={{ fill: '#64748b', fontSize: 9 }} />
              <YAxis axisLine={false} tickLine={false} tick={{ fill: '#64748b', fontSize: 9 }} allowDecimals={false} />
              <Tooltip content={<ChartTooltip formatValue={(value) => String(value)} />} cursor={{ fill: '#eff6ff', opacity: 0.4 }} />
              <Bar dataKey="created" name="Created" fill="#2474ff" radius={[4, 4, 0, 0]} barSize={10} />
              <Bar dataKey="completed" name="Completed" fill="#46cc9a" radius={[4, 4, 0, 0]} barSize={10} />
              <Line type="monotone" dataKey="completed" name="Completion trend" stroke="#14a973" strokeWidth={2} dot={false} />
            </ComposedChart>
          </ResponsiveContainer>
        </div>
      )}
    </ExecutiveWidget>
  );
}

export function MiniTrend({ values, color = '#2474ff' }: { values: number[]; color?: string }) {
  const cleaned = values.filter(Number.isFinite);
  if (cleaned.length < 2) return <span className="block h-8 w-20 rounded-lg bg-slate-100 dark:bg-neutral-800" aria-hidden="true" />;
  const minimum = Math.min(...cleaned);
  const maximum = Math.max(...cleaned);
  const span = Math.max(1, maximum - minimum);
  const points = cleaned.map((value, index) => `${(index / (cleaned.length - 1)) * 78 + 1},${29 - ((value - minimum) / span) * 25}`).join(' ');
  return (
    <svg viewBox="0 0 80 32" className="h-8 w-20 overflow-visible" aria-hidden="true">
      <polyline points={points} fill="none" stroke={color} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}
