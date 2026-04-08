import { type Dispatch, type SetStateAction, useMemo, useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import type { BusinessPartnerDto } from '@/services/businessPartnerService';
import type {
  CreateProjectBuildingDto,
  CreateProjectFloorDto,
  CreateProjectUnitDto,
  CreateProjectUnitReleaseBatchDto,
  ProjectDetailDto,
  ProjectLinkOptionsDto,
  ProjectSalesAgreementLinkOptionDto,
  ProjectSalesOrderLinkOptionDto,
  ProjectUnitDto,
} from '@/services/projectService';

type ProjectUnitsTabProps = {
  project: ProjectDetailDto;
  activeBusinessPartners: BusinessPartnerDto[];
  linkOptions: ProjectLinkOptionsDto;
  unitDraft: CreateProjectUnitDto;
  setUnitDraft: Dispatch<SetStateAction<CreateProjectUnitDto>>;
  unitTypeOptions: string[];
  unitStatusOptions: string[];
  formatCatalogLabel: (value?: string | null) => string;
  formatDateLabel: (value?: string) => string;
  formatMoney: (value: number | undefined, currency?: string | null, maximumFractionDigits?: number) => string;
  unitActionBusyKey: string | null;
  onAddBuilding: (dto: CreateProjectBuildingDto) => Promise<void>;
  onDeleteBuilding: (buildingId: string) => Promise<void>;
  onAddFloor: (dto: CreateProjectFloorDto) => Promise<void>;
  onDeleteFloor: (floorId: string) => Promise<void>;
  onAddUnitReleaseBatch: (dto: CreateProjectUnitReleaseBatchDto) => Promise<void>;
  onDeleteUnitReleaseBatch: (unitReleaseBatchId: string) => Promise<void>;
  onAddUnit: () => void;
  onReleaseUnit: (unitId: string) => void;
  onWithdrawUnitRelease: (unitId: string) => void;
  onCreateSalesAgreement: (unitId: string) => void;
  onCreateLeaseAgreement: (unitId: string) => void;
  onCreateSalesOrder: (unitId: string) => void;
  onDeleteUnit: (unitId: string) => void;
};

const DEFAULT_RELEASE_BATCH_STATUSES = ['Draft', 'Planned', 'Released', 'Closed'];

const countUnitsByValue = (units: ProjectUnitDto[], selector: (unit: ProjectUnitDto) => string | undefined, status: string) =>
  units.filter((item) => (selector(item) || '').toLowerCase() === status.toLowerCase()).length;

const formatSalesAgreementLabel = (agreement: ProjectSalesAgreementLinkOptionDto) =>
  `${agreement.documentNumber}${agreement.agreementType ? ` [${agreement.agreementType}]` : ''}${agreement.agreementTitle ? ` - ${agreement.agreementTitle}` : ''}`;

const formatSalesOrderLabel = (order: ProjectSalesOrderLinkOptionDto) =>
  `${order.orderNumber}${order.customerName ? ` - ${order.customerName}` : ''}`;

export function ProjectUnitsTab({
  project,
  activeBusinessPartners,
  linkOptions,
  unitDraft,
  setUnitDraft,
  unitTypeOptions,
  unitStatusOptions,
  formatCatalogLabel,
  formatDateLabel,
  formatMoney,
  unitActionBusyKey,
  onAddBuilding,
  onDeleteBuilding,
  onAddFloor,
  onDeleteFloor,
  onAddUnitReleaseBatch,
  onDeleteUnitReleaseBatch,
  onAddUnit,
  onReleaseUnit,
  onWithdrawUnitRelease,
  onCreateSalesAgreement,
  onCreateLeaseAgreement,
  onCreateSalesOrder,
  onDeleteUnit,
}: ProjectUnitsTabProps) {
  const [buildingDraft, setBuildingDraft] = useState<CreateProjectBuildingDto>({ name: '' });
  const [floorDraft, setFloorDraft] = useState<CreateProjectFloorDto>({ name: '' });
  const [releaseBatchDraft, setReleaseBatchDraft] = useState<CreateProjectUnitReleaseBatchDto>({ name: '', status: DEFAULT_RELEASE_BATCH_STATUSES[0] });

  const totals = useMemo(() => ({
    area: project.units.reduce((sum, item) => sum + (item.areaSquareMeters || 0), 0),
    value: project.units.reduce((sum, item) => sum + (item.basePrice || 0), 0),
    available: countUnitsByValue(project.units, (item) => item.commercialStatus, 'Available'),
    reserved: countUnitsByValue(project.units, (item) => item.commercialStatus, 'Reserved'),
    soldOrLeased:
      countUnitsByValue(project.units, (item) => item.commercialStatus, 'Sold')
      + countUnitsByValue(project.units, (item) => item.commercialStatus, 'Leased'),
    released: project.units.filter((item) => item.isReleasedForMarket).length,
    completedHandover:
      countUnitsByValue(project.units, (item) => item.handoverStatus, 'HandedOver')
      + countUnitsByValue(project.units, (item) => item.handoverStatus, 'Occupied'),
  }), [project.units]);

  const availableSalesAgreements = useMemo(
    () => unitDraft.customerBusinessPartnerId
      ? linkOptions.salesAgreements.filter((item) => item.businessPartnerId === unitDraft.customerBusinessPartnerId)
      : linkOptions.salesAgreements,
    [linkOptions.salesAgreements, unitDraft.customerBusinessPartnerId],
  );
  const availableSalesOrders = useMemo(
    () => unitDraft.customerBusinessPartnerId
      ? linkOptions.salesOrders.filter((item) => item.businessPartnerId === unitDraft.customerBusinessPartnerId)
      : linkOptions.salesOrders,
    [linkOptions.salesOrders, unitDraft.customerBusinessPartnerId],
  );

  const filterFloors = (buildingId?: string) => project.floors.filter((item) => !buildingId || item.projectBuildingId === buildingId);
  const filterReleaseBatches = (buildingId?: string, floorId?: string) =>
    project.unitReleaseBatches.filter((item) => (!buildingId || !item.projectBuildingId || item.projectBuildingId === buildingId) && (!floorId || !item.projectFloorId || item.projectFloorId === floorId));

  const availableUnitFloors = useMemo(() => filterFloors(unitDraft.projectBuildingId), [project.floors, unitDraft.projectBuildingId]);
  const availableUnitReleaseBatches = useMemo(() => filterReleaseBatches(unitDraft.projectBuildingId, unitDraft.projectFloorId), [project.unitReleaseBatches, unitDraft.projectBuildingId, unitDraft.projectFloorId]);

  const handleAddBuilding = async () => {
    if (!buildingDraft.name?.trim()) return;
    await onAddBuilding({ ...buildingDraft, name: buildingDraft.name.trim(), code: buildingDraft.code?.trim() || undefined, notes: buildingDraft.notes?.trim() || undefined });
    setBuildingDraft({ name: '' });
  };

  const handleAddFloor = async () => {
    if (!floorDraft.name?.trim()) return;
    await onAddFloor({ ...floorDraft, name: floorDraft.name.trim(), code: floorDraft.code?.trim() || undefined, notes: floorDraft.notes?.trim() || undefined });
    setFloorDraft({ name: '', projectBuildingId: floorDraft.projectBuildingId });
  };

  const handleAddReleaseBatch = async () => {
    if (!releaseBatchDraft.name?.trim()) return;
    await onAddUnitReleaseBatch({ ...releaseBatchDraft, name: releaseBatchDraft.name.trim(), code: releaseBatchDraft.code?.trim() || undefined, notes: releaseBatchDraft.notes?.trim() || undefined });
    setReleaseBatchDraft({ name: '', status: DEFAULT_RELEASE_BATCH_STATUSES[0], projectBuildingId: releaseBatchDraft.projectBuildingId, projectFloorId: releaseBatchDraft.projectFloorId });
  };

  const handleBuildingChange = (value: string) => {
    setUnitDraft((current) => {
      const projectBuildingId = value === 'none' ? undefined : value;
      const nextFloors = filterFloors(projectBuildingId);
      const projectFloorId = current.projectFloorId && nextFloors.some((item) => item.id === current.projectFloorId) ? current.projectFloorId : undefined;
      const nextBatches = filterReleaseBatches(projectBuildingId, projectFloorId);
      const projectUnitReleaseBatchId = current.projectUnitReleaseBatchId && nextBatches.some((item) => item.id === current.projectUnitReleaseBatchId) ? current.projectUnitReleaseBatchId : undefined;
      return { ...current, projectBuildingId, projectFloorId, projectUnitReleaseBatchId };
    });
  };

  const handleFloorChange = (value: string) => {
    setUnitDraft((current) => {
      const projectFloorId = value === 'none' ? undefined : value;
      const nextBatches = filterReleaseBatches(current.projectBuildingId, projectFloorId);
      const projectUnitReleaseBatchId = current.projectUnitReleaseBatchId && nextBatches.some((item) => item.id === current.projectUnitReleaseBatchId) ? current.projectUnitReleaseBatchId : undefined;
      return { ...current, projectFloorId, projectUnitReleaseBatchId };
    });
  };

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Units</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-3 xl:grid-cols-6">
          <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Delivery Structure</div><div className="text-xl font-semibold">{formatCatalogLabel(project.developmentProfile?.deliveryStructure || 'WholeDevelopment')}</div></div>
          <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Units</div><div className="text-2xl font-semibold">{project.units.length}</div><div className="text-sm text-muted-foreground">{totals.released} released</div></div>
          <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Area</div><div className="text-2xl font-semibold">{totals.area.toFixed(2)} sqm</div></div>
          <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Valuation</div><div className="text-xl font-semibold">{formatMoney(totals.value, project.units[0]?.currency)}</div></div>
          <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Commercial</div><div className="text-2xl font-semibold">{totals.soldOrLeased}</div><div className="text-sm text-muted-foreground">{totals.reserved} reserved, {totals.available} available</div></div>
          <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Hierarchy</div><div className="text-xl font-semibold">{project.buildings.length} / {project.floors.length} / {project.unitReleaseBatches.length}</div><div className="text-sm text-muted-foreground">Buildings, floors, release batches</div></div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Hierarchy & Release Planning</CardTitle></CardHeader>
        <CardContent className="grid gap-6 xl:grid-cols-3">
          <div className="space-y-3 rounded-lg border p-4">
            <div className="font-medium">Buildings</div>
            <Input placeholder="Code" value={buildingDraft.code || ''} onChange={(event) => setBuildingDraft((current) => ({ ...current, code: event.target.value || undefined }))} />
            <Input placeholder="Building name" value={buildingDraft.name || ''} onChange={(event) => setBuildingDraft((current) => ({ ...current, name: event.target.value }))} />
            <Button disabled={!buildingDraft.name?.trim()} onClick={() => { void handleAddBuilding(); }}><Plus className="mr-2 h-4 w-4" />Add Building</Button>
            <div className="space-y-2">
              {project.buildings.length === 0 ? <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">No buildings yet.</div> : null}
              {project.buildings.map((building) => (
                <div key={building.id} className="flex items-center justify-between rounded-md border p-3">
                  <div>
                    <div className="font-medium">{building.name}</div>
                    <div className="text-sm text-muted-foreground">{building.code || 'No code'} · {project.units.filter((item) => item.projectBuildingId === building.id).length} units</div>
                  </div>
                  <Button variant="ghost" size="sm" onClick={() => { void onDeleteBuilding(building.id); }}><Trash2 className="h-4 w-4" /></Button>
                </div>
              ))}
            </div>
          </div>
          <div className="space-y-3 rounded-lg border p-4">
            <div className="font-medium">Floors</div>
            <Select value={floorDraft.projectBuildingId || 'none'} onValueChange={(value) => setFloorDraft((current) => ({ ...current, projectBuildingId: value === 'none' ? undefined : value }))}>
              <SelectTrigger><SelectValue placeholder="Optional building" /></SelectTrigger>
              <SelectContent><SelectItem value="none">No building</SelectItem>{project.buildings.map((building) => <SelectItem key={building.id} value={building.id}>{building.code ? `${building.code} · ${building.name}` : building.name}</SelectItem>)}</SelectContent>
            </Select>
            <Input placeholder="Code" value={floorDraft.code || ''} onChange={(event) => setFloorDraft((current) => ({ ...current, code: event.target.value || undefined }))} />
            <Input placeholder="Floor name" value={floorDraft.name || ''} onChange={(event) => setFloorDraft((current) => ({ ...current, name: event.target.value }))} />
            <Input placeholder="Level number" type="number" value={floorDraft.levelNumber ?? ''} onChange={(event) => setFloorDraft((current) => ({ ...current, levelNumber: event.target.value ? Number(event.target.value) : undefined }))} />
            <Button disabled={!floorDraft.name?.trim()} onClick={() => { void handleAddFloor(); }}><Plus className="mr-2 h-4 w-4" />Add Floor</Button>
            <div className="space-y-2">
              {project.floors.length === 0 ? <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">No floors yet.</div> : null}
              {project.floors.map((floor) => (
                <div key={floor.id} className="flex items-center justify-between rounded-md border p-3">
                  <div>
                    <div className="font-medium">{floor.name}</div>
                    <div className="text-sm text-muted-foreground">{floor.projectBuildingName || 'Whole project'} · {floor.code || 'No code'} · {project.units.filter((item) => item.projectFloorId === floor.id).length} units</div>
                  </div>
                  <Button variant="ghost" size="sm" onClick={() => { void onDeleteFloor(floor.id); }}><Trash2 className="h-4 w-4" /></Button>
                </div>
              ))}
            </div>
          </div>
          <div className="space-y-3 rounded-lg border p-4">
            <div className="font-medium">Release Batches</div>
            <Select value={releaseBatchDraft.projectBuildingId || 'none'} onValueChange={(value) => setReleaseBatchDraft((current) => ({ ...current, projectBuildingId: value === 'none' ? undefined : value, projectFloorId: undefined }))}>
              <SelectTrigger><SelectValue placeholder="Optional building" /></SelectTrigger>
              <SelectContent><SelectItem value="none">No building</SelectItem>{project.buildings.map((building) => <SelectItem key={building.id} value={building.id}>{building.code ? `${building.code} · ${building.name}` : building.name}</SelectItem>)}</SelectContent>
            </Select>
            <Select value={releaseBatchDraft.projectFloorId || 'none'} onValueChange={(value) => setReleaseBatchDraft((current) => ({ ...current, projectFloorId: value === 'none' ? undefined : value }))}>
              <SelectTrigger><SelectValue placeholder="Optional floor" /></SelectTrigger>
              <SelectContent><SelectItem value="none">No floor</SelectItem>{filterFloors(releaseBatchDraft.projectBuildingId).map((floor) => <SelectItem key={floor.id} value={floor.id}>{floor.code ? `${floor.code} · ${floor.name}` : floor.name}</SelectItem>)}</SelectContent>
            </Select>
            <Input placeholder="Code" value={releaseBatchDraft.code || ''} onChange={(event) => setReleaseBatchDraft((current) => ({ ...current, code: event.target.value || undefined }))} />
            <Input placeholder="Release batch name" value={releaseBatchDraft.name || ''} onChange={(event) => setReleaseBatchDraft((current) => ({ ...current, name: event.target.value }))} />
            <Select value={releaseBatchDraft.status || DEFAULT_RELEASE_BATCH_STATUSES[0]} onValueChange={(value) => setReleaseBatchDraft((current) => ({ ...current, status: value }))}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>{DEFAULT_RELEASE_BATCH_STATUSES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
            </Select>
            <Button disabled={!releaseBatchDraft.name?.trim()} onClick={() => { void handleAddReleaseBatch(); }}><Plus className="mr-2 h-4 w-4" />Add Release Batch</Button>
            <div className="space-y-2">
              {project.unitReleaseBatches.length === 0 ? <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">No release batches yet.</div> : null}
              {project.unitReleaseBatches.map((batch) => (
                <div key={batch.id} className="flex items-center justify-between rounded-md border p-3">
                  <div>
                    <div className="flex items-center gap-2"><div className="font-medium">{batch.name}</div><Badge variant="secondary">{formatCatalogLabel(batch.status)}</Badge></div>
                    <div className="text-sm text-muted-foreground">{batch.projectBuildingName || 'Whole project'}{batch.projectFloorName ? ` · ${batch.projectFloorName}` : ''} · {project.units.filter((item) => item.projectUnitReleaseBatchId === batch.id).length} units</div>
                  </div>
                  <Button variant="ghost" size="sm" onClick={() => { void onDeleteUnitReleaseBatch(batch.id); }}><Trash2 className="h-4 w-4" /></Button>
                </div>
              ))}
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Add Unit</CardTitle></CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-4">
          <div className="grid gap-2"><Label>Code</Label><Input value={unitDraft.code || ''} onChange={(event) => setUnitDraft((current) => ({ ...current, code: event.target.value || undefined }))} /></div>
          <div className="grid gap-2 md:col-span-2"><Label>Name</Label><Input value={unitDraft.name} onChange={(event) => setUnitDraft((current) => ({ ...current, name: event.target.value }))} /></div>
          <div className="grid gap-2"><Label>Status</Label><Select value={unitDraft.status || unitStatusOptions[0]} onValueChange={(value) => setUnitDraft((current) => ({ ...current, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{unitStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
          <div className="grid gap-2"><Label>Type</Label><Select value={unitDraft.unitType || unitTypeOptions[0]} onValueChange={(value) => setUnitDraft((current) => ({ ...current, unitType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{unitTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
          <div className="grid gap-2"><Label>Building</Label><Select value={unitDraft.projectBuildingId || 'none'} onValueChange={handleBuildingChange}><SelectTrigger><SelectValue placeholder="Optional building" /></SelectTrigger><SelectContent><SelectItem value="none">No building</SelectItem>{project.buildings.map((building) => <SelectItem key={building.id} value={building.id}>{building.code ? `${building.code} · ${building.name}` : building.name}</SelectItem>)}</SelectContent></Select></div>
          <div className="grid gap-2"><Label>Floor</Label><Select value={unitDraft.projectFloorId || 'none'} onValueChange={handleFloorChange}><SelectTrigger><SelectValue placeholder="Optional floor" /></SelectTrigger><SelectContent><SelectItem value="none">No floor</SelectItem>{availableUnitFloors.map((floor) => <SelectItem key={floor.id} value={floor.id}>{floor.code ? `${floor.code} · ${floor.name}` : floor.name}</SelectItem>)}</SelectContent></Select></div>
          <div className="grid gap-2"><Label>Release Batch</Label><Select value={unitDraft.projectUnitReleaseBatchId || 'none'} onValueChange={(value) => setUnitDraft((current) => ({ ...current, projectUnitReleaseBatchId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="Optional release batch" /></SelectTrigger><SelectContent><SelectItem value="none">No release batch</SelectItem>{availableUnitReleaseBatches.map((batch) => <SelectItem key={batch.id} value={batch.id}>{batch.code ? `${batch.code} · ${batch.name}` : batch.name}</SelectItem>)}</SelectContent></Select></div>
          <div className="grid gap-2"><Label>Customer</Label><Select value={unitDraft.customerBusinessPartnerId || 'none'} onValueChange={(value) => setUnitDraft((current) => ({ ...current, customerBusinessPartnerId: value === 'none' ? undefined : value, salesAgreementId: undefined, salesOrderId: undefined }))}><SelectTrigger><SelectValue placeholder="Optional customer" /></SelectTrigger><SelectContent><SelectItem value="none">No customer</SelectItem>{activeBusinessPartners.map((partner) => <SelectItem key={partner.id} value={partner.id}>{partner.partnerName}</SelectItem>)}</SelectContent></Select></div>
          <div className="grid gap-2"><Label>Block Label</Label><Input value={unitDraft.blockName || ''} onChange={(event) => setUnitDraft((current) => ({ ...current, blockName: event.target.value || undefined }))} /></div>
          <div className="grid gap-2"><Label>Floor Label</Label><Input value={unitDraft.floorLabel || ''} onChange={(event) => setUnitDraft((current) => ({ ...current, floorLabel: event.target.value || undefined }))} /></div>
          <div className="grid gap-2"><Label>Area (sqm)</Label><Input type="number" min="0" step="0.01" value={unitDraft.areaSquareMeters ?? ''} onChange={(event) => setUnitDraft((current) => ({ ...current, areaSquareMeters: event.target.value ? Number(event.target.value) : undefined }))} /></div>
          <div className="grid gap-2"><Label>Rate</Label><Input type="number" min="0" step="0.01" value={unitDraft.valuationRate ?? ''} onChange={(event) => setUnitDraft((current) => ({ ...current, valuationRate: event.target.value ? Number(event.target.value) : undefined }))} /></div>
          <div className="grid gap-2"><Label>Base Price</Label><Input type="number" min="0" step="0.01" value={unitDraft.basePrice ?? ''} onChange={(event) => setUnitDraft((current) => ({ ...current, basePrice: event.target.value ? Number(event.target.value) : undefined }))} /></div>
          <div className="grid gap-2"><Label>Handover</Label><Input type="date" value={unitDraft.handoverDate || ''} onChange={(event) => setUnitDraft((current) => ({ ...current, handoverDate: event.target.value || undefined }))} /></div>
          <div className="grid gap-2"><Label>Sales Agreement</Label><Select value={unitDraft.salesAgreementId || 'none'} onValueChange={(value) => setUnitDraft((current) => ({ ...current, salesAgreementId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="Optional agreement" /></SelectTrigger><SelectContent><SelectItem value="none">No agreement</SelectItem>{availableSalesAgreements.map((agreement) => <SelectItem key={agreement.id} value={agreement.id}>{formatSalesAgreementLabel(agreement)}</SelectItem>)}</SelectContent></Select></div>
          <div className="grid gap-2"><Label>Sales Order</Label><Select value={unitDraft.salesOrderId || 'none'} onValueChange={(value) => setUnitDraft((current) => ({ ...current, salesOrderId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="Optional sales order" /></SelectTrigger><SelectContent><SelectItem value="none">No sales order</SelectItem>{availableSalesOrders.map((order) => <SelectItem key={order.id} value={order.id}>{formatSalesOrderLabel(order)}</SelectItem>)}</SelectContent></Select></div>
          <div className="flex items-center justify-between rounded-lg border px-3 py-2"><div><Label className="text-sm">Released for market</Label><div className="text-xs text-muted-foreground">Required before sales allocation</div></div><Switch checked={Boolean(unitDraft.isReleasedForMarket)} onCheckedChange={(checked) => setUnitDraft((current) => ({ ...current, isReleasedForMarket: checked }))} /></div>
          <div className="grid gap-2 md:col-span-4"><Label>Notes</Label><Textarea rows={2} value={unitDraft.notes || ''} onChange={(event) => setUnitDraft((current) => ({ ...current, notes: event.target.value || undefined }))} /></div>
          <div className="md:col-span-4 flex justify-end"><Button disabled={!unitDraft.name?.trim()} onClick={onAddUnit}><Plus className="mr-2 h-4 w-4" />Add Unit</Button></div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Registered Units</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          {project.units.length === 0 ? <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">No units are recorded for this project yet.</div> : null}
          {project.units.map((unit) => (
            <div key={unit.id} className="rounded-lg border p-4 space-y-3">
              <div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
                <div className="space-y-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <div className="font-medium">{unit.name}</div>
                    {unit.code ? <Badge variant="outline">{unit.code}</Badge> : null}
                    <Badge>{formatCatalogLabel(unit.status)}</Badge>
                    {unit.commercialStatus && unit.commercialStatus !== unit.status ? <Badge variant="secondary">{formatCatalogLabel(unit.commercialStatus)}</Badge> : null}
                    {unit.commercialIntent ? <Badge variant="outline">{formatCatalogLabel(unit.commercialIntent)} Intent</Badge> : null}
                    {unit.projectUnitReleaseBatchName ? <Badge variant="secondary">{unit.projectUnitReleaseBatchCode ? `${unit.projectUnitReleaseBatchCode} · ` : ''}{unit.projectUnitReleaseBatchName}</Badge> : null}
                    {unit.isReleasedForMarket ? <Badge variant="outline">Released</Badge> : null}
                  </div>
                  <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
                    {unit.projectBuildingName ? <span>{unit.projectBuildingCode ? `${unit.projectBuildingCode} · ` : ''}{unit.projectBuildingName}</span> : null}
                    {unit.projectFloorName ? <span>{unit.projectFloorCode ? `${unit.projectFloorCode} · ` : ''}{unit.projectFloorName}</span> : null}
                    {unit.areaSquareMeters ? <span>{unit.areaSquareMeters.toFixed(2)} sqm</span> : null}
                    {unit.customerBusinessPartnerName ? <span>{unit.customerBusinessPartnerName}</span> : null}
                    {unit.salesAgreementNumber ? <span>{unit.salesAgreementType ? `${formatCatalogLabel(unit.salesAgreementType)} ` : 'Agreement '}{unit.salesAgreementNumber}{unit.salesAgreementStatus ? ` (${formatCatalogLabel(unit.salesAgreementStatus)})` : ''}</span> : null}
                    {unit.salesOrderNumber ? <span>Order {unit.salesOrderNumber}{unit.salesOrderStatus ? ` (${formatCatalogLabel(unit.salesOrderStatus)})` : ''}</span> : null}
                    {unit.handoverDate ? <span>Handover {formatDateLabel(unit.handoverDate)}</span> : null}
                  </div>
                  <div className="grid gap-3 text-sm md:grid-cols-3">
                    <div><div className="text-muted-foreground">Rate</div><div className="font-medium">{formatMoney(unit.valuationRate, unit.currency)}</div></div>
                    <div><div className="text-muted-foreground">Base Price</div><div className="font-medium">{formatMoney(unit.basePrice, unit.currency)}</div></div>
                    <div><div className="text-muted-foreground">Currency</div><div className="font-medium">{unit.currency}</div></div>
                  </div>
                  {unit.notes ? <div className="text-sm text-muted-foreground whitespace-pre-wrap">{unit.notes}</div> : null}
                </div>
                <div className="flex flex-wrap items-center gap-2 xl:max-w-sm xl:justify-end">
                  {!unit.salesAgreementId ? <Button variant="outline" size="sm" disabled={!unit.isReleasedForMarket || !unit.customerBusinessPartnerId || unitActionBusyKey === `unit-sales-agreement:${unit.id}`} onClick={() => onCreateSalesAgreement(unit.id)}>{unitActionBusyKey === `unit-sales-agreement:${unit.id}` ? 'Creating...' : 'Create Agreement'}</Button> : null}
                  {!unit.salesAgreementId ? <Button variant="outline" size="sm" disabled={!unit.isReleasedForMarket || !unit.customerBusinessPartnerId || Boolean(unit.salesOrderId) || unitActionBusyKey === `unit-lease-agreement:${unit.id}`} onClick={() => onCreateLeaseAgreement(unit.id)}>{unitActionBusyKey === `unit-lease-agreement:${unit.id}` ? 'Creating...' : 'Create Lease Agreement'}</Button> : null}
                  {!unit.salesOrderId ? <Button variant="outline" size="sm" disabled={!unit.isReleasedForMarket || !unit.customerBusinessPartnerId || unitActionBusyKey === `unit-sales-order:${unit.id}`} onClick={() => onCreateSalesOrder(unit.id)}>{unitActionBusyKey === `unit-sales-order:${unit.id}` ? 'Creating...' : 'Create Sales Order'}</Button> : null}
                  {unit.isReleasedForMarket ? <Button variant="outline" size="sm" disabled={Boolean(unit.salesAgreementId || unit.salesOrderId) || unitActionBusyKey === `unit-withdraw-release:${unit.id}`} onClick={() => onWithdrawUnitRelease(unit.id)}>{unitActionBusyKey === `unit-withdraw-release:${unit.id}` ? 'Withdrawing...' : 'Withdraw Release'}</Button> : <Button variant="outline" size="sm" disabled={unitActionBusyKey === `unit-release:${unit.id}`} onClick={() => onReleaseUnit(unit.id)}>{unitActionBusyKey === `unit-release:${unit.id}` ? 'Releasing...' : 'Release'}</Button>}
                  <Button variant="ghost" size="sm" onClick={() => onDeleteUnit(unit.id)}><Trash2 className="h-4 w-4" /></Button>
                </div>
              </div>
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
