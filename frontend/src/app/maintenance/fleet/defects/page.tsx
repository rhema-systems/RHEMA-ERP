'use client';

import * as React from 'react';
import Link from 'next/link';
import { fleetService, FleetDefectDto, FleetVehicleListDto } from '@/services/fleetService';
import { maintenanceApiService } from '@/services/maintenanceApiService';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { formatFleetDateTime } from '@/lib/date-format';
import maintenanceSettingsService from '@/services/maintenanceSettingsService';

export default function FleetDefectsPage() {
  type BillingType = 'Maintenance' | 'Repairs';

  const [vehicles, setVehicles] = React.useState<FleetVehicleListDto[]>([]);
  const [vehicleId, setVehicleId] = React.useState<string>('all');
  const [status, setStatus] = React.useState<string>('all');
  const [searchTerm, setSearchTerm] = React.useState<string>('');

  const [items, setItems] = React.useState<FleetDefectDto[]>([]);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const [openCreate, setOpenCreate] = React.useState(false);
  const [createForm, setCreateForm] = React.useState({
    title: '',
    description: '',
    severity: 'Medium',
  });

  const [openWo, setOpenWo] = React.useState(false);
  const [selectedDefect, setSelectedDefect] = React.useState<FleetDefectDto | null>(null);
  const [workOrderTypes, setWorkOrderTypes] = React.useState<any[]>([]);
  const [maintenanceTypes, setMaintenanceTypes] = React.useState<any[]>([]);
  const [priorityLevels, setPriorityLevels] = React.useState<any[]>([]);
  const [woForm, setWoForm] = React.useState({
    workOrderTypeId: '',
    maintenanceTypeId: '',
    priorityLevelId: '',
    billingType: 'Repairs' as BillingType,
  });

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
      const res = await fleetService.getDefects({
        page: 1,
        pageSize: 50,
        vehicleAssetId: vehicleId === 'all' ? undefined : vehicleId,
        status: status === 'all' ? undefined : status,
        searchTerm: searchTerm || undefined,
      });
      setItems(res.items ?? []);
    } catch (e: any) {
      setError(e?.message || 'Failed to load defects');
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [vehicleId, status, searchTerm]);

  React.useEffect(() => {
    loadVehicles();
  }, [loadVehicles]);

  React.useEffect(() => {
    load();
  }, [load]);

  const openWorkOrder = async (d: FleetDefectDto) => {
    setSelectedDefect(d);
    setWoForm({ workOrderTypeId: '', maintenanceTypeId: '', priorityLevelId: '', billingType: 'Repairs' });
    setOpenWo(true);
    try {
      const [types, mtypes, pri, settings] = await Promise.all([
        maintenanceApiService.getWorkOrderTypes(),
        maintenanceApiService.getMaintenanceTypes(),
        maintenanceApiService.getPriorityLevels(),
        maintenanceSettingsService.getSettings(),
      ]);
      setWorkOrderTypes(types ?? []);
      setMaintenanceTypes(mtypes ?? []);
      setPriorityLevels(pri ?? []);
      setWoForm({
        workOrderTypeId: settings.defaultFleetDefectWorkOrderTypeId || '',
        maintenanceTypeId: settings.defaultFleetDefectMaintenanceTypeId || '',
        priorityLevelId: settings.defaultFleetDefectPriorityLevelId || '',
        billingType: settings.defaultFleetDefectBillingType === 'Maintenance' ? 'Maintenance' : 'Repairs',
      });
    } catch (e) {
      console.error(e);
    }
  };

  const create = async () => {
    if (!createForm.title.trim()) return;
    try {
      await fleetService.createDefect({
        vehicleAssetId: vehicleId === 'all' ? (vehicles[0]?.id ?? '') : vehicleId,
        title: createForm.title,
        description: createForm.description || undefined,
        severity: createForm.severity,
      });
      setOpenCreate(false);
      setCreateForm({ title: '', description: '', severity: 'Medium' });
      await load();
    } catch (e: any) {
      alert(e?.message || 'Failed to create defect');
    }
  };

  const createWorkOrder = async () => {
    if (!selectedDefect) return;
    if (!woForm.workOrderTypeId || !woForm.maintenanceTypeId || !woForm.priorityLevelId) return;
    try {
      const res = await fleetService.createWorkOrderFromDefect({
        defectId: selectedDefect.id,
        workOrderTypeId: woForm.workOrderTypeId,
        maintenanceTypeId: woForm.maintenanceTypeId,
        priorityLevelId: woForm.priorityLevelId,
        billingType: woForm.billingType,
      });
      setOpenWo(false);
      await load();
      if (res?.workOrderId) {
        window.open(`/maintenance/work-orders?id=${res.workOrderId}`, '_blank');
      }
    } catch (e: any) {
      alert(e?.message || 'Failed to create work order');
    }
  };

  return (
    <div className="space-y-6 p-6">
      <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Fleet Defects</h1>
          <p className="text-muted-foreground">Log defects and convert them to Work Orders/Job Cards</p>
        </div>

        <div className="flex flex-col gap-3 md:flex-row">
          <div className="w-full md:w-[280px]">
            <Label>Vehicle</Label>
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
            <Label>Status</Label>
            <Select value={status} onValueChange={setStatus}>
              <SelectTrigger>
                <SelectValue placeholder="All" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All</SelectItem>
                <SelectItem value="Open">Open</SelectItem>
                <SelectItem value="InProgress">In Progress</SelectItem>
                <SelectItem value="Resolved">Resolved</SelectItem>
                <SelectItem value="Closed">Closed</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <div className="w-full md:w-[260px]">
            <Label>Search</Label>
            <Input value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} placeholder="Title/description..." />
          </div>

          <Button onClick={() => setOpenCreate(true)}>New Defect</Button>
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

      <Card>
        <CardHeader>
          <CardTitle>Defects</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {loading ? (
            <div className="text-sm text-muted-foreground">Loading…</div>
          ) : items.length === 0 ? (
            <div className="text-sm text-muted-foreground">No defects found.</div>
          ) : (
            <div className="overflow-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b bg-muted/40 text-left text-muted-foreground">
                    <th className="py-2">Reported</th>
                    <th className="py-2">Asset</th>
                    <th className="py-2">Title</th>
                    <th className="py-2">Severity</th>
                    <th className="py-2">Status</th>
                    <th className="py-2">Work Order</th>
                    <th className="py-2" />
                  </tr>
                </thead>
                <tbody>
                  {items.map((d, idx) => (
                    <tr key={d.id} className={idx % 2 === 1 ? 'border-b bg-muted/10 hover:bg-muted/30' : 'border-b hover:bg-muted/30'}>
                      <td className="py-2">{formatFleetDateTime(d.reportedAtUtc)}</td>
                      <td className="py-2">{d.vehicleName}</td>
                      <td className="py-2">{d.title}</td>
                      <td className="py-2">{d.severity}</td>
                      <td className="py-2">{d.status}</td>
                      <td className="py-2">
                        {d.workOrderId ? (
                          <Link className="text-primary underline" href={`/maintenance/work-orders?id=${d.workOrderId}`}>
                            View
                          </Link>
                        ) : (
                          <span className="text-muted-foreground">—</span>
                        )}
                      </td>
                      <td className="py-2 text-right">
                        <div className="flex justify-end gap-2">
                          {!d.workOrderId && (
                            <Button variant="outline" size="sm" onClick={() => openWorkOrder(d)}>
                              Create WO
                            </Button>
                          )}
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={async () => {
                              const newStatus = d.status === 'Open' ? 'InProgress' : d.status === 'InProgress' ? 'Resolved' : 'Closed';
                              await fleetService.updateDefectStatus(d.id, { status: newStatus });
                              await load();
                            }}
                          >
                            Next Status
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog open={openCreate} onOpenChange={setOpenCreate}>
        <DialogContent className="sm:max-w-[720px]">
          <DialogHeader>
            <DialogTitle>New Defect</DialogTitle>
          </DialogHeader>

          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2 md:col-span-2">
              <Label>Title</Label>
              <Input value={createForm.title} onChange={(e) => setCreateForm((p) => ({ ...p, title: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Severity</Label>
              <Select value={createForm.severity} onValueChange={(v) => setCreateForm((p) => ({ ...p, severity: v }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Severity" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Low">Low</SelectItem>
                  <SelectItem value="Medium">Medium</SelectItem>
                  <SelectItem value="High">High</SelectItem>
                  <SelectItem value="Critical">Critical</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Description</Label>
              <Textarea
                value={createForm.description}
                onChange={(e) => setCreateForm((p) => ({ ...p, description: e.target.value }))}
                rows={5}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpenCreate(false)}>
              Cancel
            </Button>
            <Button onClick={create} disabled={!createForm.title.trim() || (vehicleId === 'all' && vehicles.length === 0)}>
              Create
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={openWo} onOpenChange={setOpenWo}>
        <DialogContent className="sm:max-w-[720px]">
          <DialogHeader>
            <DialogTitle>Create Work Order</DialogTitle>
          </DialogHeader>

          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>Work Order Type</Label>
              <Select value={woForm.workOrderTypeId} onValueChange={(v) => setWoForm((p) => ({ ...p, workOrderTypeId: v }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Select type" />
                </SelectTrigger>
                <SelectContent>
                  {workOrderTypes.map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Maintenance Type</Label>
              <Select value={woForm.maintenanceTypeId} onValueChange={(v) => setWoForm((p) => ({ ...p, maintenanceTypeId: v }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Select type" />
                </SelectTrigger>
                <SelectContent>
                  {maintenanceTypes.map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Priority</Label>
              <Select value={woForm.priorityLevelId} onValueChange={(v) => setWoForm((p) => ({ ...p, priorityLevelId: v }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Select priority" />
                </SelectTrigger>
                <SelectContent>
                  {priorityLevels.map((p) => (
                    <SelectItem key={p.id} value={p.id}>
                      {p.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Billing Type</Label>
              <p className="text-xs text-muted-foreground">Defaults to Repairs for defects (itemized costs).</p>
              <Select value={woForm.billingType} onValueChange={(v) => setWoForm((p) => ({ ...p, billingType: v as BillingType }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Select billing type" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Repairs">Repairs</SelectItem>
                  <SelectItem value="Maintenance">Maintenance</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpenWo(false)}>
              Cancel
            </Button>
            <Button onClick={createWorkOrder} disabled={!selectedDefect || !woForm.workOrderTypeId || !woForm.maintenanceTypeId || !woForm.priorityLevelId}>
              Create Work Order
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

