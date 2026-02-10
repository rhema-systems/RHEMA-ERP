'use client';

import * as React from 'react';
import { fleetService, FleetDashboardSummaryDto, FleetHealthDto, FleetVehicleListDto } from '@/services/fleetService';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Label } from '@/components/ui/label';

function formatNumber(value: number | null | undefined, digits = 2) {
  if (value === null || value === undefined) return '—';
  return value.toLocaleString(undefined, { maximumFractionDigits: digits });
}

export default function FleetDashboardPage() {
  const [vehicles, setVehicles] = React.useState<FleetVehicleListDto[]>([]);
  const [vehicleId, setVehicleId] = React.useState<string>('all');
  const [summary, setSummary] = React.useState<FleetDashboardSummaryDto | null>(null);
  const [health, setHealth] = React.useState<FleetHealthDto | null>(null);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

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

  return (
    <div className="space-y-6 p-6">
      <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Fleet Dashboard</h1>
          <p className="text-muted-foreground">KPIs across vehicles, trips, compliance, defects, and costs</p>
        </div>

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
        <Card className="border-amber-300">
          <CardHeader>
            <CardTitle>Setup Warnings</CardTitle>
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

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
        <Card>
          <CardHeader>
            <CardTitle>Active Vehicles</CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold">{loading ? '…' : summary?.activeVehicles ?? '—'}</CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Trips (This Month)</CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold">{loading ? '…' : summary?.tripsThisMonth ?? '—'}</CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Open Defects</CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold">{loading ? '…' : summary?.openDefects ?? '—'}</CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Compliance Due Soon</CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold">{loading ? '…' : summary?.complianceDueSoon ?? '—'}</CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Compliance Overdue</CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold">{loading ? '…' : summary?.complianceOverdue ?? '—'}</CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Fuel Cost (This Month)</CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold">{loading ? '…' : formatNumber(summary?.fuelCostThisMonth ?? null, 2)}</CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>External Repair Cost (This Month)</CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold">
            {loading ? '…' : formatNumber(summary?.externalRepairCostThisMonth ?? null, 2)}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Internal Maintenance (This Month)</CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold">
            {loading ? '…' : formatNumber(summary?.internalMaintenanceCostThisMonth ?? null, 2)}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Total Cost (This Month)</CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold">{loading ? '…' : formatNumber(summary?.totalCostThisMonth ?? null, 2)}</CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>KPIs (This Month)</CardTitle>
          </CardHeader>
          <CardContent className="space-y-1 text-sm">
            <div className="flex items-center justify-between">
              <span className="text-muted-foreground">Fuel cost / km</span>
              <span className="font-medium">{formatNumber(summary?.averageFuelCostPerKm ?? null, 4)}</span>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-muted-foreground">Km / liter</span>
              <span className="font-medium">{formatNumber(summary?.averageKmPerLiter ?? null, 3)}</span>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

