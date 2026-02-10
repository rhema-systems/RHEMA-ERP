'use client';

import * as React from 'react';
import {
  fleetService,
  FleetCostSummaryDto,
  FleetUtilizationSummaryDto,
  FleetVehicleListDto,
} from '@/services/fleetService';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';

function toIsoDayStart(dateYmd: string) {
  if (!dateYmd) return undefined;
  const d = new Date(`${dateYmd}T00:00:00.000Z`);
  if (Number.isNaN(d.getTime())) return undefined;
  return d.toISOString();
}

function toIsoDayEnd(dateYmd: string) {
  if (!dateYmd) return undefined;
  const d = new Date(`${dateYmd}T23:59:59.999Z`);
  if (Number.isNaN(d.getTime())) return undefined;
  return d.toISOString();
}

function formatNumber(value: number | null | undefined, digits = 2) {
  if (value === null || value === undefined) return '—';
  return value.toLocaleString(undefined, { maximumFractionDigits: digits });
}

function getThisMonthRange() {
  const now = new Date();
  const start = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1));
  const pad = (n: number) => String(n).padStart(2, '0');
  const ymd = (d: Date) => `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())}`;
  return { from: ymd(start), to: ymd(now) };
}

export default function FleetReportsPage() {
  const [{ from, to }, setRange] = React.useState(() => getThisMonthRange());
  const [vehicles, setVehicles] = React.useState<FleetVehicleListDto[]>([]);
  const [vehicleId, setVehicleId] = React.useState<string>('all');
  const [top, setTop] = React.useState<number>(10);

  const [costSummary, setCostSummary] = React.useState<FleetCostSummaryDto | null>(null);
  const [utilSummary, setUtilSummary] = React.useState<FleetUtilizationSummaryDto | null>(null);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const loadVehicles = React.useCallback(async () => {
    try {
      const result = await fleetService.getVehicles({ page: 1, pageSize: 100 });
      setVehicles(result.items ?? []);
    } catch (e) {
      console.error(e);
    }
  }, []);

  const load = React.useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const fromUtc = toIsoDayStart(from);
      const toUtc = toIsoDayEnd(to);
      const vehicleAssetId = vehicleId === 'all' ? undefined : vehicleId;

      const [c, u] = await Promise.all([
        fleetService.getCostSummary({ fromUtc, toUtc, top, vehicleAssetId }),
        fleetService.getUtilization({ fromUtc, toUtc, top, vehicleAssetId }),
      ]);

      setCostSummary(c);
      setUtilSummary(u);
    } catch (e: any) {
      setError(e?.message || 'Failed to load reports');
      setCostSummary(null);
      setUtilSummary(null);
    } finally {
      setLoading(false);
    }
  }, [from, to, top, vehicleId]);

  React.useEffect(() => {
    loadVehicles();
  }, [loadVehicles]);

  React.useEffect(() => {
    load();
  }, [load]);

  return (
    <div className="space-y-6 p-6">
      <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Fleet Reports</h1>
          <p className="text-muted-foreground">Cost and utilization rollups (by vehicle)</p>
        </div>

        <div className="flex flex-col gap-3 md:flex-row md:items-end">
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

          <div className="w-full md:w-[160px]">
            <Label>From</Label>
            <Input type="date" value={from} onChange={(e) => setRange((p) => ({ ...p, from: e.target.value }))} />
          </div>
          <div className="w-full md:w-[160px]">
            <Label>To</Label>
            <Input type="date" value={to} onChange={(e) => setRange((p) => ({ ...p, to: e.target.value }))} />
          </div>
          <div className="w-full md:w-[120px]">
            <Label>Top</Label>
            <Input
              type="number"
              min={1}
              max={200}
              value={String(top)}
              onChange={(e) => setTop(Math.max(1, Math.min(200, Number(e.target.value) || 10)))}
            />
          </div>

          <div className="flex gap-2">
            <Button
              variant="outline"
              onClick={() => {
                setRange(getThisMonthRange());
                setVehicleId('all');
                setTop(10);
              }}
            >
              This month
            </Button>
            <Button onClick={load} disabled={loading}>
              Refresh
            </Button>
          </div>
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

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Cost Summary</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="text-sm text-muted-foreground">
              Total: {loading ? '…' : formatNumber(costSummary?.totalAmount ?? null, 2)}
            </div>
            {!costSummary || costSummary.rows.length === 0 ? (
              <div className="text-sm text-muted-foreground">{loading ? 'Loading…' : 'No data.'}</div>
            ) : (
              <div className="overflow-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b text-left text-muted-foreground">
                      <th className="py-2">Vehicle</th>
                      <th className="py-2 text-right">Total</th>
                      <th className="py-2 text-right">Fuel</th>
                      <th className="py-2 text-right">External</th>
                      <th className="py-2 text-right">Internal</th>
                      <th className="py-2 text-right">Other</th>
                      <th className="py-2 text-right">Entries</th>
                    </tr>
                  </thead>
                  <tbody>
                    {costSummary.rows.map((r) => (
                      <tr key={r.vehicleAssetId} className="border-b">
                        <td className="py-2">{r.vehicleName}</td>
                        <td className="py-2 text-right">{formatNumber(r.totalAmount, 2)}</td>
                        <td className="py-2 text-right">{formatNumber(r.fuelAmount, 2)}</td>
                        <td className="py-2 text-right">{formatNumber(r.externalRepairAmount, 2)}</td>
                        <td className="py-2 text-right">{formatNumber(r.internalMaintenanceAmount, 2)}</td>
                        <td className="py-2 text-right">{formatNumber(r.otherAmount, 2)}</td>
                        <td className="py-2 text-right">{r.entryCount}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Utilization (Completed Trips)</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="text-sm text-muted-foreground">
              Trips: {loading ? '…' : utilSummary?.completedTrips ?? '—'} • Km: {loading ? '…' : formatNumber(utilSummary?.totalKm ?? null, 2)} • Hours:{' '}
              {loading ? '…' : formatNumber(utilSummary?.totalHours ?? null, 2)}
            </div>
            {!utilSummary || utilSummary.rows.length === 0 ? (
              <div className="text-sm text-muted-foreground">{loading ? 'Loading…' : 'No data.'}</div>
            ) : (
              <div className="overflow-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b text-left text-muted-foreground">
                      <th className="py-2">Vehicle</th>
                      <th className="py-2 text-right">Trips</th>
                      <th className="py-2 text-right">Km</th>
                      <th className="py-2 text-right">Hours</th>
                      <th className="py-2 text-right">Avg km/trip</th>
                      <th className="py-2 text-right">Avg hrs/trip</th>
                    </tr>
                  </thead>
                  <tbody>
                    {utilSummary.rows.map((r) => (
                      <tr key={r.vehicleAssetId} className="border-b">
                        <td className="py-2">{r.vehicleName}</td>
                        <td className="py-2 text-right">{r.completedTrips}</td>
                        <td className="py-2 text-right">{formatNumber(r.totalKm, 2)}</td>
                        <td className="py-2 text-right">{formatNumber(r.totalHours, 2)}</td>
                        <td className="py-2 text-right">{formatNumber(r.averageKmPerTrip ?? null, 2)}</td>
                        <td className="py-2 text-right">{formatNumber(r.averageHoursPerTrip ?? null, 2)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

