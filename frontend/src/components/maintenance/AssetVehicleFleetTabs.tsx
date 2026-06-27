'use client';

import * as React from 'react';
import { useRouter } from 'next/navigation';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';

import fleetService, {
  FleetDashboardSummaryDto,
  FleetTripDto,
  FleetVehicleAssignmentDto,
  FleetComplianceItemDto,
  FleetFuelTransactionDto,
  FleetDefectDto,
  FleetIncidentDto,
  FleetExternalRepairDto,
  FleetCostEntryDto,
  FleetTyreDto,
  FleetBatteryDto,
  PagedResult,
} from '@/services/fleetService';
import { formatFleetDate, formatFleetDateTime } from '@/lib/date-format';

type Loadable<T> = { loading: boolean; error: string | null; data: T | null };

function ErrorBox({ message }: { message: string }) {
  return <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">{message}</div>;
}

function EmptyRow({ colSpan, message }: { colSpan: number; message: string }) {
  return (
    <TableRow>
      <TableCell colSpan={colSpan} className="text-center text-sm text-muted-foreground py-6">
        {message}
      </TableCell>
    </TableRow>
  );
}

function getComplianceStatus(expiryDateIso?: string | null): 'Not set' | 'Overdue' | 'Due soon' | 'OK' {
  if (!expiryDateIso) return 'Not set';
  const expiry = new Date(expiryDateIso);
  if (Number.isNaN(expiry.getTime())) return 'Not set';
  const today = new Date();
  const todayUtc = Date.UTC(today.getFullYear(), today.getMonth(), today.getDate());
  const expiryUtc = Date.UTC(expiry.getFullYear(), expiry.getMonth(), expiry.getDate());
  const days = Math.floor((expiryUtc - todayUtc) / 86_400_000);
  if (days < 0) return 'Overdue';
  if (days <= 7) return 'Due soon';
  return 'OK';
}

function ComplianceStatusBadge({ expiryDate }: { expiryDate?: string | null }) {
  const status = getComplianceStatus(expiryDate);
  if (status === 'OK') return <Badge className="bg-emerald-100 text-emerald-800 hover:bg-emerald-100">OK</Badge>;
  if (status === 'Due soon') return <Badge className="bg-amber-100 text-amber-800 hover:bg-amber-100">Due soon</Badge>;
  if (status === 'Overdue') return <Badge variant="destructive">Overdue</Badge>;
  return <Badge variant="outline">Not set</Badge>;
}

export default function AssetVehicleFleetTabs({ vehicleAssetId }: { vehicleAssetId: string }) {
  const router = useRouter();
  const pageSize = 10;

  const [tab, setTab] = React.useState<
    | 'summary'
    | 'trips'
    | 'assignments'
    | 'compliance'
    | 'fuel'
    | 'defects'
    | 'incidents'
    | 'external-repairs'
    | 'costs'
    | 'tyres'
    | 'batteries'
  >('summary');

  const [summary, setSummary] = React.useState<Loadable<FleetDashboardSummaryDto>>({ loading: false, error: null, data: null });
  const [trips, setTrips] = React.useState<Loadable<PagedResult<FleetTripDto>>>({ loading: false, error: null, data: null });
  const [assignments, setAssignments] = React.useState<Loadable<FleetVehicleAssignmentDto[]>>({ loading: false, error: null, data: null });
  const [compliance, setCompliance] = React.useState<Loadable<PagedResult<FleetComplianceItemDto>>>({ loading: false, error: null, data: null });
  const [fuel, setFuel] = React.useState<Loadable<PagedResult<FleetFuelTransactionDto>>>({ loading: false, error: null, data: null });
  const [defects, setDefects] = React.useState<Loadable<PagedResult<FleetDefectDto>>>({ loading: false, error: null, data: null });
  const [incidents, setIncidents] = React.useState<Loadable<PagedResult<FleetIncidentDto>>>({ loading: false, error: null, data: null });
  const [externalRepairs, setExternalRepairs] = React.useState<Loadable<PagedResult<FleetExternalRepairDto>>>({ loading: false, error: null, data: null });
  const [costs, setCosts] = React.useState<Loadable<PagedResult<FleetCostEntryDto>>>({ loading: false, error: null, data: null });
  const [tyres, setTyres] = React.useState<Loadable<PagedResult<FleetTyreDto>>>({ loading: false, error: null, data: null });
  const [batteries, setBatteries] = React.useState<Loadable<PagedResult<FleetBatteryDto>>>({ loading: false, error: null, data: null });

  React.useEffect(() => {
    setTab('summary');
    setSummary({ loading: false, error: null, data: null });
    setTrips({ loading: false, error: null, data: null });
    setAssignments({ loading: false, error: null, data: null });
    setCompliance({ loading: false, error: null, data: null });
    setFuel({ loading: false, error: null, data: null });
    setDefects({ loading: false, error: null, data: null });
    setIncidents({ loading: false, error: null, data: null });
    setExternalRepairs({ loading: false, error: null, data: null });
    setCosts({ loading: false, error: null, data: null });
    setTyres({ loading: false, error: null, data: null });
    setBatteries({ loading: false, error: null, data: null });
  }, [vehicleAssetId]);

  React.useEffect(() => {
    const ensure = async () => {
      try {
        if (tab === 'summary' && !summary.data && !summary.loading) {
          setSummary((p) => ({ ...p, loading: true, error: null }));
          const res = await fleetService.getDashboardSummary(vehicleAssetId);
          setSummary({ loading: false, error: null, data: res });
        }

        if (tab === 'trips' && !trips.data && !trips.loading) {
          setTrips((p) => ({ ...p, loading: true, error: null }));
          const res = await fleetService.getTrips({ page: 1, pageSize, vehicleAssetId });
          setTrips({ loading: false, error: null, data: res });
        }

        if (tab === 'assignments' && !assignments.data && !assignments.loading) {
          setAssignments((p) => ({ ...p, loading: true, error: null }));
          const res = await fleetService.getAssignments(vehicleAssetId);
          setAssignments({ loading: false, error: null, data: res });
        }

        if (tab === 'compliance' && !compliance.data && !compliance.loading) {
          setCompliance((p) => ({ ...p, loading: true, error: null }));
          const res = await fleetService.getCompliance(vehicleAssetId, 1, pageSize);
          setCompliance({ loading: false, error: null, data: res });
        }

        if (tab === 'fuel' && !fuel.data && !fuel.loading) {
          setFuel((p) => ({ ...p, loading: true, error: null }));
          const res = await fleetService.getFuel(vehicleAssetId, 1, pageSize);
          setFuel({ loading: false, error: null, data: res });
        }

        if (tab === 'defects' && !defects.data && !defects.loading) {
          setDefects((p) => ({ ...p, loading: true, error: null }));
          const res = await fleetService.getDefects({ page: 1, pageSize, vehicleAssetId });
          setDefects({ loading: false, error: null, data: res });
        }

        if (tab === 'incidents' && !incidents.data && !incidents.loading) {
          setIncidents((p) => ({ ...p, loading: true, error: null }));
          const res = await fleetService.getIncidents({ page: 1, pageSize, vehicleAssetId });
          setIncidents({ loading: false, error: null, data: res });
        }

        if (tab === 'external-repairs' && !externalRepairs.data && !externalRepairs.loading) {
          setExternalRepairs((p) => ({ ...p, loading: true, error: null }));
          const res = await fleetService.getExternalRepairs({ page: 1, pageSize, vehicleAssetId });
          setExternalRepairs({ loading: false, error: null, data: res });
        }

        if (tab === 'costs' && !costs.data && !costs.loading) {
          setCosts((p) => ({ ...p, loading: true, error: null }));
          const res = await fleetService.getCosts(vehicleAssetId, 1, pageSize);
          setCosts({ loading: false, error: null, data: res });
        }

        if (tab === 'tyres' && !tyres.data && !tyres.loading) {
          setTyres((p) => ({ ...p, loading: true, error: null }));
          const res = await fleetService.getTyres(vehicleAssetId, 1, pageSize);
          setTyres({ loading: false, error: null, data: res });
        }

        if (tab === 'batteries' && !batteries.data && !batteries.loading) {
          setBatteries((p) => ({ ...p, loading: true, error: null }));
          const res = await fleetService.getBatteries(vehicleAssetId, 1, pageSize);
          setBatteries({ loading: false, error: null, data: res });
        }
      } catch (e: any) {
        const msg = e?.message || String(e);
        if (tab === 'summary') setSummary({ loading: false, error: msg, data: null });
        if (tab === 'trips') setTrips({ loading: false, error: msg, data: null });
        if (tab === 'assignments') setAssignments({ loading: false, error: msg, data: null });
        if (tab === 'compliance') setCompliance({ loading: false, error: msg, data: null });
        if (tab === 'fuel') setFuel({ loading: false, error: msg, data: null });
        if (tab === 'defects') setDefects({ loading: false, error: msg, data: null });
        if (tab === 'incidents') setIncidents({ loading: false, error: msg, data: null });
        if (tab === 'external-repairs') setExternalRepairs({ loading: false, error: msg, data: null });
        if (tab === 'costs') setCosts({ loading: false, error: msg, data: null });
        if (tab === 'tyres') setTyres({ loading: false, error: msg, data: null });
        if (tab === 'batteries') setBatteries({ loading: false, error: msg, data: null });
      }
    };

    ensure();
  }, [tab, vehicleAssetId]);

  return (
    <Tabs value={tab} onValueChange={(v) => setTab(v as any)} className="w-full space-y-3">
      <TabsList className="flex flex-wrap justify-start gap-1 h-auto">
        <TabsTrigger value="summary">Summary</TabsTrigger>
        <TabsTrigger value="trips">Trips</TabsTrigger>
        <TabsTrigger value="assignments">Assignments</TabsTrigger>
        <TabsTrigger value="compliance">Compliance</TabsTrigger>
        <TabsTrigger value="fuel">Fuel</TabsTrigger>
        <TabsTrigger value="defects">Defects</TabsTrigger>
        <TabsTrigger value="incidents">Incidents</TabsTrigger>
        <TabsTrigger value="external-repairs">External Repairs</TabsTrigger>
        <TabsTrigger value="costs">Costs</TabsTrigger>
        <TabsTrigger value="tyres">Tyres</TabsTrigger>
        <TabsTrigger value="batteries">Batteries</TabsTrigger>
      </TabsList>

      <TabsContent value="summary" className="space-y-4">
        <div className="flex items-center justify-between">
          <div>
            <div className="text-sm font-medium">Fleet Summary</div>
            <div className="text-xs text-muted-foreground">Recent activity and KPIs for this vehicle.</div>
          </div>
          <Button size="sm" variant="outline" onClick={() => router.push('/maintenance/fleet/dashboard')}>
            Open Fleet Dashboard
          </Button>
        </div>

        {summary.loading && <p className="text-sm text-muted-foreground">Loading summary...</p>}
        {summary.error && <ErrorBox message={summary.error} />}
        {summary.data && (
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-4">
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium">Completed Trips (This Month)</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">{summary.data.tripsThisMonth}</div>
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium">Open Defects</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">{summary.data.openDefects}</div>
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium">Compliance Due Soon</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">{summary.data.complianceDueSoon}</div>
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium">Compliance Overdue</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">{summary.data.complianceOverdue}</div>
              </CardContent>
            </Card>

            <Card className="md:col-span-2">
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium">Costs (This Month)</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="grid grid-cols-2 gap-3 text-sm">
                  <div className="flex items-center justify-between rounded-md bg-muted/30 p-2">
                    <span className="text-muted-foreground">Fuel</span>
                    <span className="font-medium">{summary.data.fuelCostThisMonth.toLocaleString()}</span>
                  </div>
                  <div className="flex items-center justify-between rounded-md bg-muted/30 p-2">
                    <span className="text-muted-foreground">External Repairs</span>
                    <span className="font-medium">{summary.data.externalRepairCostThisMonth.toLocaleString()}</span>
                  </div>
                  <div className="flex items-center justify-between rounded-md bg-muted/30 p-2">
                    <span className="text-muted-foreground">Internal Maintenance</span>
                    <span className="font-medium">{summary.data.internalMaintenanceCostThisMonth.toLocaleString()}</span>
                  </div>
                  <div className="flex items-center justify-between rounded-md bg-muted/30 p-2">
                    <span className="text-muted-foreground">Total</span>
                    <span className="font-medium">{summary.data.totalCostThisMonth.toLocaleString()}</span>
                  </div>
                </div>
              </CardContent>
            </Card>

            <Card className="md:col-span-2">
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium">Quick Links</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="flex flex-wrap gap-2">
                  <Button size="sm" variant="outline" onClick={() => router.push(`/maintenance/fleet/trips?vehicleAssetId=${vehicleAssetId}`)}>
                    Trips
                  </Button>
                  <Button size="sm" variant="outline" onClick={() => router.push(`/maintenance/fleet/compliance?vehicleAssetId=${vehicleAssetId}`)}>
                    Compliance
                  </Button>
                  <Button size="sm" variant="outline" onClick={() => router.push(`/maintenance/fleet/fuel?vehicleAssetId=${vehicleAssetId}`)}>
                    Fuel
                  </Button>
                  <Button size="sm" variant="outline" onClick={() => router.push(`/maintenance/fleet/defects?vehicleAssetId=${vehicleAssetId}`)}>
                    Defects
                  </Button>
                  <Button size="sm" variant="outline" onClick={() => router.push(`/maintenance/fleet/incidents?vehicleAssetId=${vehicleAssetId}`)}>
                    Incidents
                  </Button>
                  <Button size="sm" variant="outline" onClick={() => router.push(`/maintenance/fleet/costs?vehicleAssetId=${vehicleAssetId}`)}>
                    Costs
                  </Button>
                </div>
              </CardContent>
            </Card>
          </div>
        )}
      </TabsContent>

      <TabsContent value="trips" className="space-y-3">
        <div className="flex items-center justify-between">
          <div className="text-sm font-medium">Recent Trips</div>
          <Button size="sm" variant="outline" onClick={() => router.push(`/maintenance/fleet/trips?vehicleAssetId=${vehicleAssetId}`)}>
            Open Trips
          </Button>
        </div>
        {trips.loading && <p className="text-sm text-muted-foreground">Loading trips...</p>}
        {trips.error && <ErrorBox message={trips.error} />}
        {trips.data && (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Status</TableHead>
                <TableHead>Planned Start</TableHead>
                <TableHead>Purpose</TableHead>
                <TableHead>Driver</TableHead>
                <TableHead className="text-right">Action</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(trips.data.items || []).map((t) => (
                <TableRow key={t.id}>
                  <TableCell><Badge variant="outline">{t.status}</Badge></TableCell>
                  <TableCell className="text-sm">{t.plannedStartAt ? formatFleetDateTime(t.plannedStartAt) : '-'}</TableCell>
                  <TableCell className="text-sm">{t.purpose || '-'}</TableCell>
                  <TableCell className="text-sm">{t.driverEmployeeName || '-'}</TableCell>
                  <TableCell className="text-right">
                    <Button size="sm" variant="outline" onClick={() => router.push(`/maintenance/fleet/trips?id=${t.id}`)}>
                      View
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
              {(trips.data.items || []).length === 0 && <EmptyRow colSpan={5} message="No trips found for this vehicle." />}
            </TableBody>
          </Table>
        )}
      </TabsContent>

      <TabsContent value="assignments" className="space-y-3">
        <div className="flex items-center justify-between">
          <div className="text-sm font-medium">Driver Assignment History</div>
          <Button size="sm" variant="outline" onClick={() => router.push('/maintenance/fleet/vehicles')}>
            Open Vehicles
          </Button>
        </div>
        {assignments.loading && <p className="text-sm text-muted-foreground">Loading assignments...</p>}
        {assignments.error && <ErrorBox message={assignments.error} />}
        {assignments.data && (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Employee</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>From</TableHead>
                <TableHead>To</TableHead>
                <TableHead>Status</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(assignments.data || []).slice(0, pageSize).map((a) => (
                <TableRow key={a.id}>
                  <TableCell className="text-sm">{a.employeeName}</TableCell>
                  <TableCell className="text-sm">{a.assignmentType}</TableCell>
                  <TableCell className="text-sm">{formatFleetDateTime(a.assignedFromUtc)}</TableCell>
                  <TableCell className="text-sm">{a.assignedToUtc ? formatFleetDateTime(a.assignedToUtc) : '-'}</TableCell>
                  <TableCell>{a.isActive ? <Badge className="bg-green-100 text-green-800">Active</Badge> : <Badge variant="outline">Ended</Badge>}</TableCell>
                </TableRow>
              ))}
              {(assignments.data || []).length === 0 && <EmptyRow colSpan={5} message="No assignments found for this vehicle." />}
            </TableBody>
          </Table>
        )}
      </TabsContent>

      <TabsContent value="compliance" className="space-y-3">
        <div className="flex items-center justify-between">
          <div className="text-sm font-medium">Compliance Records</div>
          <Button size="sm" variant="outline" onClick={() => router.push(`/maintenance/fleet/compliance?vehicleAssetId=${vehicleAssetId}`)}>
            Open Compliance
          </Button>
        </div>
        {compliance.loading && <p className="text-sm text-muted-foreground">Loading compliance...</p>}
        {compliance.error && <ErrorBox message={compliance.error} />}
        {compliance.data && (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Type</TableHead>
                <TableHead>Issue Date</TableHead>
                <TableHead>Expiry Date</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Critical</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(compliance.data.items || []).map((c) => (
                <TableRow key={c.id}>
                  <TableCell className="text-sm">{c.complianceType}</TableCell>
                  <TableCell className="text-sm">{c.issueDate ? formatFleetDate(c.issueDate) : '-'}</TableCell>
                  <TableCell className="text-sm">{formatFleetDate(c.expiryDate)}</TableCell>
                  <TableCell><ComplianceStatusBadge expiryDate={c.expiryDate} /></TableCell>
                  <TableCell>{c.isCritical ? <Badge className="bg-red-100 text-red-800">Yes</Badge> : <Badge variant="outline">No</Badge>}</TableCell>
                </TableRow>
              ))}
              {(compliance.data.items || []).length === 0 && <EmptyRow colSpan={5} message="No compliance records found for this vehicle." />}
            </TableBody>
          </Table>
        )}
      </TabsContent>
      <TabsContent value="fuel" className="space-y-3">
        <div className="flex items-center justify-between">
          <div className="text-sm font-medium">Fuel Transactions</div>
          <Button size="sm" variant="outline" onClick={() => router.push(`/maintenance/fleet/fuel?vehicleAssetId=${vehicleAssetId}`)}>
            Open Fuel
          </Button>
        </div>
        {fuel.loading && <p className="text-sm text-muted-foreground">Loading fuel transactions...</p>}
        {fuel.error && <ErrorBox message={fuel.error} />}
        {fuel.data && (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Date</TableHead>
                <TableHead>Qty</TableHead>
                <TableHead>Total Cost</TableHead>
                <TableHead>Vendor</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(fuel.data.items || []).map((f) => (
                <TableRow key={f.id}>
                  <TableCell className="text-sm">{formatFleetDateTime(f.fuelledAt)}</TableCell>
                  <TableCell className="text-sm">{f.quantity} {f.unit}</TableCell>
                  <TableCell className="text-sm">{f.totalCost != null ? f.totalCost.toLocaleString() : '-'}</TableCell>
                  <TableCell className="text-sm">{f.vendorName || '-'}</TableCell>
                </TableRow>
              ))}
              {(fuel.data.items || []).length === 0 && <EmptyRow colSpan={4} message="No fuel transactions found for this vehicle." />}
            </TableBody>
          </Table>
        )}
      </TabsContent>

      <TabsContent value="defects" className="space-y-3">
        <div className="flex items-center justify-between">
          <div className="text-sm font-medium">Defects</div>
          <Button size="sm" variant="outline" onClick={() => router.push(`/maintenance/fleet/defects?vehicleAssetId=${vehicleAssetId}`)}>
            Open Defects
          </Button>
        </div>
        {defects.loading && <p className="text-sm text-muted-foreground">Loading defects...</p>}
        {defects.error && <ErrorBox message={defects.error} />}
        {defects.data && (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Title</TableHead>
                <TableHead>Severity</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Reported</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(defects.data.items || []).map((d) => (
                <TableRow key={d.id}>
                  <TableCell className="text-sm">{d.title}</TableCell>
                  <TableCell><Badge variant="outline">{d.severity}</Badge></TableCell>
                  <TableCell><Badge variant="outline">{d.status}</Badge></TableCell>
                  <TableCell className="text-sm">{formatFleetDateTime(d.reportedAtUtc)}</TableCell>
                </TableRow>
              ))}
              {(defects.data.items || []).length === 0 && <EmptyRow colSpan={4} message="No defects found for this vehicle." />}
            </TableBody>
          </Table>
        )}
      </TabsContent>

      <TabsContent value="incidents" className="space-y-3">
        <div className="flex items-center justify-between">
          <div className="text-sm font-medium">Incidents</div>
          <Button size="sm" variant="outline" onClick={() => router.push(`/maintenance/fleet/incidents?vehicleAssetId=${vehicleAssetId}`)}>
            Open Incidents
          </Button>
        </div>
        {incidents.loading && <p className="text-sm text-muted-foreground">Loading incidents...</p>}
        {incidents.error && <ErrorBox message={incidents.error} />}
        {incidents.data && (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Title</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Occurred</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(incidents.data.items || []).map((i) => (
                <TableRow key={i.id}>
                  <TableCell className="text-sm">{i.title}</TableCell>
                  <TableCell className="text-sm">{i.incidentType}</TableCell>
                  <TableCell><Badge variant="outline">{i.status}</Badge></TableCell>
                  <TableCell className="text-sm">{formatFleetDateTime(i.occurredAtUtc)}</TableCell>
                </TableRow>
              ))}
              {(incidents.data.items || []).length === 0 && <EmptyRow colSpan={4} message="No incidents found for this vehicle." />}
            </TableBody>
          </Table>
        )}
      </TabsContent>
      <TabsContent value="external-repairs" className="space-y-3">
        <div className="flex items-center justify-between">
          <div className="text-sm font-medium">External Repairs</div>
          <Button size="sm" variant="outline" onClick={() => router.push(`/maintenance/fleet/external-repairs?vehicleAssetId=${vehicleAssetId}`)}>
            Open External Repairs
          </Button>
        </div>
        {externalRepairs.loading && <p className="text-sm text-muted-foreground">Loading external repairs...</p>}
        {externalRepairs.error && <ErrorBox message={externalRepairs.error} />}
        {externalRepairs.data && (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Title</TableHead>
                <TableHead>Vendor</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Requested</TableHead>
                <TableHead className="text-right">Actual Cost</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(externalRepairs.data.items || []).map((r) => (
                <TableRow key={r.id}>
                  <TableCell className="text-sm">{r.title}</TableCell>
                  <TableCell className="text-sm">{r.vendorBusinessPartnerName || '-'}</TableCell>
                  <TableCell><Badge variant="outline">{r.status}</Badge></TableCell>
                  <TableCell className="text-sm">{formatFleetDateTime(r.requestedAtUtc)}</TableCell>
                  <TableCell className="text-right text-sm">{r.actualCost != null ? r.actualCost.toLocaleString() : '-'}</TableCell>
                </TableRow>
              ))}
              {(externalRepairs.data.items || []).length === 0 && <EmptyRow colSpan={5} message="No external repairs found for this vehicle." />}
            </TableBody>
          </Table>
        )}
      </TabsContent>

      <TabsContent value="costs" className="space-y-3">
        <div className="flex items-center justify-between">
          <div className="text-sm font-medium">Cost Entries</div>
          <Button size="sm" variant="outline" onClick={() => router.push(`/maintenance/fleet/costs?vehicleAssetId=${vehicleAssetId}`)}>
            Open Costs
          </Button>
        </div>
        {costs.loading && <p className="text-sm text-muted-foreground">Loading costs...</p>}
        {costs.error && <ErrorBox message={costs.error} />}
        {costs.data && (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Date</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Source</TableHead>
                <TableHead className="text-right">Amount</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(costs.data.items || []).map((c) => (
                <TableRow key={c.id}>
                  <TableCell className="text-sm">{formatFleetDateTime(c.costDateUtc)}</TableCell>
                  <TableCell className="text-sm">{c.costType}</TableCell>
                  <TableCell className="text-sm">{c.source}</TableCell>
                  <TableCell className="text-right text-sm">
                    {c.amount.toLocaleString()} {c.currencyCode || ''}
                  </TableCell>
                </TableRow>
              ))}
              {(costs.data.items || []).length === 0 && <EmptyRow colSpan={4} message="No cost entries found for this vehicle." />}
            </TableBody>
          </Table>
        )}
      </TabsContent>

      <TabsContent value="tyres" className="space-y-3">
        <div className="flex items-center justify-between">
          <div className="text-sm font-medium">Tyres</div>
          <Button size="sm" variant="outline" onClick={() => router.push(`/maintenance/fleet/tyres?vehicleAssetId=${vehicleAssetId}`)}>
            Open Tyres
          </Button>
        </div>
        {tyres.loading && <p className="text-sm text-muted-foreground">Loading tyres...</p>}
        {tyres.error && <ErrorBox message={tyres.error} />}
        {tyres.data && (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Serial</TableHead>
                <TableHead>Brand</TableHead>
                <TableHead>Position</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Installed</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(tyres.data.items || []).map((t) => (
                <TableRow key={t.id}>
                  <TableCell className="text-sm font-mono">{t.serialNumber}</TableCell>
                  <TableCell className="text-sm">{t.brand || '-'}</TableCell>
                  <TableCell className="text-sm">{t.position || '-'}</TableCell>
                  <TableCell><Badge variant="outline">{t.status}</Badge></TableCell>
                  <TableCell className="text-sm">{formatFleetDateTime(t.installedAtUtc)}</TableCell>
                </TableRow>
              ))}
              {(tyres.data.items || []).length === 0 && <EmptyRow colSpan={5} message="No tyres found for this vehicle." />}
            </TableBody>
          </Table>
        )}
      </TabsContent>

      <TabsContent value="batteries" className="space-y-3">
        <div className="flex items-center justify-between">
          <div className="text-sm font-medium">Batteries</div>
          <Button size="sm" variant="outline" onClick={() => router.push(`/maintenance/fleet/batteries?vehicleAssetId=${vehicleAssetId}`)}>
            Open Batteries
          </Button>
        </div>
        {batteries.loading && <p className="text-sm text-muted-foreground">Loading batteries...</p>}
        {batteries.error && <ErrorBox message={batteries.error} />}
        {batteries.data && (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Serial</TableHead>
                <TableHead>Brand</TableHead>
                <TableHead>Position</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Installed</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(batteries.data.items || []).map((b) => (
                <TableRow key={b.id}>
                  <TableCell className="text-sm font-mono">{b.serialNumber}</TableCell>
                  <TableCell className="text-sm">{b.brand || '-'}</TableCell>
                  <TableCell className="text-sm">{b.position || '-'}</TableCell>
                  <TableCell><Badge variant="outline">{b.status}</Badge></TableCell>
                  <TableCell className="text-sm">{formatFleetDateTime(b.installedAtUtc)}</TableCell>
                </TableRow>
              ))}
              {(batteries.data.items || []).length === 0 && <EmptyRow colSpan={5} message="No batteries found for this vehicle." />}
            </TableBody>
          </Table>
        )}
      </TabsContent>
    </Tabs>
  );
}
