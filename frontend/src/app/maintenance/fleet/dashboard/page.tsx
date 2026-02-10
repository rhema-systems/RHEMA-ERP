'use client';

import * as React from 'react';
import Link from 'next/link';
import { format } from 'date-fns';
import { AlertTriangle, Car, DollarSign, Fuel, MapPin, ShieldCheck, TrendingUp } from 'lucide-react';
import { Bar, BarChart, CartesianGrid, Cell, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';

import {
  fleetService,
  FleetCostSummaryDto,
  FleetDashboardSummaryDto,
  FleetHealthDto,
  FleetUtilizationSummaryDto,
  FleetVehicleListDto,
} from '@/services/fleetService';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';

function formatNumber(value: number | null | undefined, digits = 2) {
  if (value === null || value === undefined) return '—';
  return value.toLocaleString(undefined, { maximumFractionDigits: digits });
}

function startOfUtcMonth(d: Date) {
  return new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), 1, 0, 0, 0, 0));
}

type DashboardRange = 'month' | '30d' | '90d';

export default function FleetDashboardPage() {
  const [vehicles, setVehicles] = React.useState<FleetVehicleListDto[]>([]);
  const [vehicleId, setVehicleId] = React.useState<string>('all');
  const [summary, setSummary] = React.useState<FleetDashboardSummaryDto | null>(null);
  const [health, setHealth] = React.useState<FleetHealthDto | null>(null);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [range, setRange] = React.useState<DashboardRange>('30d');

  const [reportsLoading, setReportsLoading] = React.useState(false);
  const [costSummary, setCostSummary] = React.useState<FleetCostSummaryDto | null>(null);
  const [utilization, setUtilization] = React.useState<FleetUtilizationSummaryDto | null>(null);

  const loadVehicles = React.useCallback(async () => {
    try {
      const result = await fleetService.getVehicles({ page: 1, pageSize: 100 });
      setVehicles(result.items ?? []);
    } catch (e: any) {
      console.error(e);
    }
  }, []);

  const loadSummary = React.useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fleetService.getDashboardSummary(vehicleId === 'all' ? undefined : vehicleId);
      setSummary(res);
    } catch (e: any) {
      setError(e?.message || 'Failed to load dashboard');
      setSummary(null);
    } finally {
      setLoading(false);
    }
  }, [vehicleId]);

  React.useEffect(() => {
    loadVehicles();
  }, [loadVehicles]);

  React.useEffect(() => {
    (async () => {
      try {
        setHealth(await fleetService.getHealth());
      } catch (e) {
        console.error(e);
      }
    })();
  }, []);

  React.useEffect(() => {
    loadSummary();
  }, [loadSummary]);

  const rangeWindow = React.useMemo(() => {
    const now = new Date();
    const toUtc = now.toISOString();
    if (range === 'month') {
      const from = startOfUtcMonth(now);
      return { fromUtc: from.toISOString(), toUtc, label: `This month (${format(from, 'dd MMM')} → ${format(now, 'dd MMM')})` };
    }
    const days = range === '90d' ? 90 : 30;
    const from = new Date(now.getTime() - days * 24 * 60 * 60 * 1000);
    return { fromUtc: from.toISOString(), toUtc, label: `Last ${days} days (${format(from, 'dd MMM')} → ${format(now, 'dd MMM')})` };
  }, [range]);

  const loadReports = React.useCallback(async () => {
    setReportsLoading(true);
    try {
      const vehicleAssetId = vehicleId === 'all' ? undefined : vehicleId;
      const [cs, us] = await Promise.all([
        fleetService.getCostSummary({ ...rangeWindow, top: 7, vehicleAssetId }),
        fleetService.getUtilization({ ...rangeWindow, top: 7, vehicleAssetId }),
      ]);
      setCostSummary(cs);
      setUtilization(us);
    } catch (e) {
      // Best-effort: keep KPI summary working even if reports fail.
      console.error(e);
      setCostSummary(null);
      setUtilization(null);
    } finally {
      setReportsLoading(false);
    }
  }, [rangeWindow, vehicleId]);

  React.useEffect(() => {
    loadReports();
  }, [loadReports]);

  const kpiCards = React.useMemo(
    () => [
      {
        title: 'Active vehicles',
        value: loading ? '…' : summary?.activeVehicles ?? '—',
        icon: Car,
        className: 'border-l-4 border-l-green-500',
      },
      {
        title: 'Trips (this month)',
        value: loading ? '…' : summary?.tripsThisMonth ?? '—',
        icon: MapPin,
        className: 'border-l-4 border-l-blue-500',
      },
      {
        title: 'Compliance due soon',
        value: loading ? '…' : summary?.complianceDueSoon ?? '—',
        icon: ShieldCheck,
        className: 'border-l-4 border-l-amber-500',
      },
      {
        title: 'Compliance overdue',
        value: loading ? '…' : summary?.complianceOverdue ?? '—',
        icon: AlertTriangle,
        className: 'border-l-4 border-l-red-500',
      },
      {
        title: 'Open defects',
        value: loading ? '…' : summary?.openDefects ?? '—',
        icon: AlertTriangle,
        className: 'border-l-4 border-l-orange-500',
      },
      {
        title: 'Total cost (this month)',
        value: loading ? '…' : formatNumber(summary?.totalCostThisMonth ?? null, 2),
        icon: DollarSign,
        className: 'border-l-4 border-l-purple-500',
      },
    ],
    [loading, summary]
  );

  const costBreakdown = React.useMemo(() => {
    if (!summary) return [];
    const fuel = summary.fuelCostThisMonth ?? 0;
    const external = summary.externalRepairCostThisMonth ?? 0;
    const internal = summary.internalMaintenanceCostThisMonth ?? 0;
    const total = summary.totalCostThisMonth ?? 0;
    const other = Math.max(0, total - (fuel + external + internal));

    const rows = [
      { name: 'Fuel', value: fuel, color: '#22c55e' },
      { name: 'External repairs', value: external, color: '#f97316' },
      { name: 'Internal maint.', value: internal, color: '#3b82f6' },
      { name: 'Other', value: other, color: '#a855f7' },
    ];

    return rows.filter((r) => r.value > 0);
  }, [summary]);

  const topCostRows = React.useMemo(() => (costSummary?.rows || []).slice(0, 7), [costSummary]);
  const topUtilRows = React.useMemo(() => (utilization?.rows || []).slice(0, 7), [utilization]);

  return (
    <div className="space-y-6 p-6">
      <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Fleet Dashboard</h1>
          <div className="flex flex-wrap items-center gap-2 pt-1">
            <p className="text-muted-foreground">Fleet KPIs, compliance and performance at a glance</p>
            <Badge variant="outline" className="ml-0 md:ml-2">
              {rangeWindow.label}
            </Badge>
          </div>
        </div>

        <div className="grid w-full gap-3 md:w-auto md:grid-cols-2">
          <div className="w-full md:w-[320px]">
            <Label>Vehicle (optional)</Label>
            <Select value={vehicleId} onValueChange={setVehicleId}>
              <SelectTrigger>
                <SelectValue placeholder="All vehicles" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All vehicles</SelectItem>
                {vehicles.map((v) => (
                  <SelectItem key={v.id} value={v.id}>
                    {v.name} {v.licensePlate ? `(${v.licensePlate})` : ''}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="w-full md:w-[220px]">
            <Label>Period</Label>
            <Select value={range} onValueChange={(v) => setRange(v as DashboardRange)}>
              <SelectTrigger>
                <SelectValue placeholder="Select period" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="month">This month</SelectItem>
                <SelectItem value="30d">Last 30 days</SelectItem>
                <SelectItem value="90d">Last 90 days</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>
      </div>

      <div className="flex flex-wrap gap-2">
        <Button asChild variant="outline" size="sm">
          <Link href="/maintenance/fleet/vehicles">Vehicles</Link>
        </Button>
        <Button asChild variant="outline" size="sm">
          <Link href="/maintenance/fleet/trips">Trips</Link>
        </Button>
        <Button asChild variant="outline" size="sm">
          <Link href="/maintenance/fleet/compliance">Compliance</Link>
        </Button>
        <Button asChild variant="outline" size="sm">
          <Link href="/maintenance/fleet/defects">Defects</Link>
        </Button>
        <Button asChild variant="outline" size="sm">
          <Link href="/maintenance/fleet/costs">Costs</Link>
        </Button>
      </div>

      {error && (
        <Card className="border-destructive">
          <CardHeader>
            <CardTitle className="text-destructive">Error</CardTitle>
          </CardHeader>
          <CardContent>{error}</CardContent>
        </Card>
      )}

      {health?.warnings?.length ? (
        <Card className="border-amber-300 bg-amber-50/30">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <AlertTriangle className="h-5 w-5 text-amber-600" />
              Setup warnings
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            <ul className="list-disc space-y-1 pl-5">
              {health.warnings.map((w, i) => (
                <li key={i}>{w}</li>
              ))}
            </ul>
          </CardContent>
        </Card>
      ) : null}

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
        {kpiCards.map((k) => {
          const Icon = k.icon;
          return (
            <Card key={k.title} className={cn('relative overflow-hidden', k.className)}>
              <CardHeader className="pb-2">
                <CardTitle className="flex items-center justify-between text-sm font-medium text-muted-foreground">
                  <span>{k.title}</span>
                  <Icon className="h-4 w-4 text-muted-foreground" />
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-3xl font-semibold tracking-tight">{k.value}</div>
              </CardContent>
            </Card>
          );
        })}
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card className="xl:col-span-1">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Fuel className="h-5 w-5 text-green-600" />
              Cost breakdown (this month)
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {!summary ? (
              <div className="py-10 text-center text-sm text-muted-foreground">No data</div>
            ) : costBreakdown.length === 0 ? (
              <div className="py-10 text-center text-sm text-muted-foreground">No costs recorded for this month</div>
            ) : (
              <div className="h-[240px]">
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie data={costBreakdown} dataKey="value" nameKey="name" innerRadius={55} outerRadius={90} paddingAngle={2}>
                      {costBreakdown.map((entry) => (
                        <Cell key={entry.name} fill={entry.color} />
                      ))}
                    </Pie>
                    <Tooltip formatter={(v: any) => formatNumber(Number(v), 2)} />
                  </PieChart>
                </ResponsiveContainer>
              </div>
            )}

            {summary ? (
              <div className="grid grid-cols-2 gap-3 text-sm">
                <div className="rounded-md border p-3">
                  <div className="text-muted-foreground">Fuel cost / km</div>
                  <div className="mt-1 font-semibold">{formatNumber(summary.averageFuelCostPerKm ?? null, 4)}</div>
                </div>
                <div className="rounded-md border p-3">
                  <div className="text-muted-foreground">Km / liter</div>
                  <div className="mt-1 font-semibold">{formatNumber(summary.averageKmPerLiter ?? null, 3)}</div>
                </div>
              </div>
            ) : null}
          </CardContent>
        </Card>

        <Card className="xl:col-span-2">
          <CardHeader className="flex flex-row items-center justify-between gap-2">
            <CardTitle className="flex items-center gap-2">
              <DollarSign className="h-5 w-5 text-purple-600" />
              Top vehicles by cost
            </CardTitle>
            <Badge variant="outline">{rangeWindow.label}</Badge>
          </CardHeader>
          <CardContent className="space-y-4">
            {reportsLoading ? (
              <div className="py-10 text-center text-sm text-muted-foreground">Loading…</div>
            ) : !costSummary || topCostRows.length === 0 ? (
              <div className="py-10 text-center text-sm text-muted-foreground">No cost data for the selected period</div>
            ) : (
              <div className="grid grid-cols-1 gap-4 lg:grid-cols-5">
                <div className="h-[280px] lg:col-span-3">
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={topCostRows.map((r) => ({ name: r.vehicleName, total: r.totalAmount }))} margin={{ left: 8, right: 8 }}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="name" tick={{ fontSize: 11 }} interval={0} height={60} angle={-20} textAnchor="end" />
                      <YAxis tick={{ fontSize: 11 }} />
                      <Tooltip formatter={(v: any) => formatNumber(Number(v), 2)} />
                      <Bar dataKey="total" fill="#a855f7" radius={[6, 6, 0, 0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </div>

                <div className="lg:col-span-2">
                  <div className="space-y-2">
                    {topCostRows.map((r) => (
                      <div key={r.vehicleAssetId} className="flex items-center justify-between gap-3 rounded-md border p-3">
                        <div className="min-w-0">
                          <div className="truncate text-sm font-medium">{r.vehicleName}</div>
                          <div className="text-xs text-muted-foreground">
                            Trips: {r.completedTrips} • Km: {formatNumber(r.totalKm, 0)}
                            {r.costPerKm != null ? ` • Cost/km: ${formatNumber(r.costPerKm, 4)}` : ''}
                          </div>
                        </div>
                        <div className="text-right text-sm font-semibold">{formatNumber(r.totalAmount, 2)}</div>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-2">
          <CardTitle className="flex items-center gap-2">
            <TrendingUp className="h-5 w-5 text-blue-600" />
            Utilization (top vehicles)
          </CardTitle>
          <Badge variant="outline">{rangeWindow.label}</Badge>
        </CardHeader>
        <CardContent className="space-y-4">
          {reportsLoading ? (
            <div className="py-10 text-center text-sm text-muted-foreground">Loading…</div>
          ) : !utilization || topUtilRows.length === 0 ? (
            <div className="py-10 text-center text-sm text-muted-foreground">No utilization data for the selected period</div>
          ) : (
            <>
              <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
                <div className="rounded-md border p-3">
                  <div className="text-sm text-muted-foreground">Completed trips</div>
                  <div className="mt-1 text-2xl font-semibold">{formatNumber(utilization.completedTrips, 0)}</div>
                </div>
                <div className="rounded-md border p-3">
                  <div className="text-sm text-muted-foreground">Total km</div>
                  <div className="mt-1 text-2xl font-semibold">{formatNumber(utilization.totalKm, 0)}</div>
                </div>
                <div className="rounded-md border p-3">
                  <div className="text-sm text-muted-foreground">Avg km / trip</div>
                  <div className="mt-1 text-2xl font-semibold">
                    {utilization.completedTrips > 0 ? formatNumber(utilization.totalKm / utilization.completedTrips, 2) : '—'}
                  </div>
                </div>
              </div>

              <div className="h-[280px]">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={topUtilRows.map((r) => ({ name: r.vehicleName, km: r.totalKm }))} margin={{ left: 8, right: 8 }}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="name" tick={{ fontSize: 11 }} interval={0} height={60} angle={-20} textAnchor="end" />
                    <YAxis tick={{ fontSize: 11 }} />
                    <Tooltip formatter={(v: any) => formatNumber(Number(v), 0)} />
                    <Bar dataKey="km" fill="#3b82f6" radius={[6, 6, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

