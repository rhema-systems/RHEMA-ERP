import { type Dispatch, type SetStateAction, useMemo } from 'react';
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
  CreateProjectUnitDto,
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
  onAddUnit: () => void;
  onReleaseUnit: (unitId: string) => void;
  onWithdrawUnitRelease: (unitId: string) => void;
  onCreateSalesAgreement: (unitId: string) => void;
  onCreateLeaseAgreement: (unitId: string) => void;
  onCreateSalesOrder: (unitId: string) => void;
  onDeleteUnit: (unitId: string) => void;
};

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
  onAddUnit,
  onReleaseUnit,
  onWithdrawUnitRelease,
  onCreateSalesAgreement,
  onCreateLeaseAgreement,
  onCreateSalesOrder,
  onDeleteUnit,
}: ProjectUnitsTabProps) {
  const totalArea = useMemo(
    () => project.units.reduce((sum, item) => sum + (item.areaSquareMeters || 0), 0),
    [project.units],
  );
  const inventoryValue = useMemo(
    () => project.units.reduce((sum, item) => sum + (item.basePrice || 0), 0),
    [project.units],
  );
  const availableCount = countUnitsByValue(project.units, (item) => item.commercialStatus, 'Available');
  const reservedCount = countUnitsByValue(project.units, (item) => item.commercialStatus, 'Reserved');
  const soldOrLeasedCount =
    countUnitsByValue(project.units, (item) => item.commercialStatus, 'Sold')
    + countUnitsByValue(project.units, (item) => item.commercialStatus, 'Leased');
  const releasedCount = project.units.filter((item) => item.isReleasedForMarket).length;
  const completedHandoverCount =
    countUnitsByValue(project.units, (item) => item.handoverStatus, 'HandedOver')
    + countUnitsByValue(project.units, (item) => item.handoverStatus, 'Occupied');
  const pendingHandoverCount =
    countUnitsByValue(project.units, (item) => item.handoverStatus, 'Pending')
    + countUnitsByValue(project.units, (item) => item.handoverStatus, 'Scheduled')
    + countUnitsByValue(project.units, (item) => item.handoverStatus, 'Due');
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

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Units</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-6">
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Delivery Structure</div>
              <div className="text-2xl font-semibold">{formatCatalogLabel(project.developmentProfile?.deliveryStructure || 'WholeDevelopment')}</div>
              <div className="text-sm text-muted-foreground">How this project is handed over</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Units</div>
              <div className="text-2xl font-semibold">{project.units.length}</div>
              <div className="text-sm text-muted-foreground">{releasedCount} released to market</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Area</div>
              <div className="text-2xl font-semibold">{totalArea.toFixed(2)} sqm</div>
              <div className="text-sm text-muted-foreground">Combined tracked area</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Valuation</div>
              <div className="text-2xl font-semibold">{formatMoney(inventoryValue, project.units[0]?.currency)}</div>
              <div className="text-sm text-muted-foreground">Base pricing across tracked units</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Commercial</div>
              <div className="text-2xl font-semibold">{soldOrLeasedCount}</div>
              <div className="text-sm text-muted-foreground">{reservedCount} reserved, {availableCount} available</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Handover</div>
              <div className="text-2xl font-semibold">{completedHandoverCount}</div>
              <div className="text-sm text-muted-foreground">{pendingHandoverCount} pending or scheduled</div>
            </div>
          </div>

          <div className="rounded-lg border p-4 space-y-4">
            <div className="font-medium">Add Unit</div>
            <div className="grid gap-4 md:grid-cols-4">
              <div className="grid gap-2">
                <Label>Code</Label>
                <Input value={unitDraft.code || ''} onChange={(event) => setUnitDraft((current) => ({ ...current, code: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2 md:col-span-2">
                <Label>Name</Label>
                <Input value={unitDraft.name} onChange={(event) => setUnitDraft((current) => ({ ...current, name: event.target.value }))} />
              </div>
              <div className="grid gap-2">
                <Label>Status</Label>
                <Select value={unitDraft.status || unitStatusOptions[0]} onValueChange={(value) => setUnitDraft((current) => ({ ...current, status: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{unitStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Type</Label>
                <Select value={unitDraft.unitType || unitTypeOptions[0]} onValueChange={(value) => setUnitDraft((current) => ({ ...current, unitType: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{unitTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Block</Label>
                <Input value={unitDraft.blockName || ''} onChange={(event) => setUnitDraft((current) => ({ ...current, blockName: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Floor</Label>
                <Input value={unitDraft.floorLabel || ''} onChange={(event) => setUnitDraft((current) => ({ ...current, floorLabel: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Customer</Label>
                <Select
                  value={unitDraft.customerBusinessPartnerId || 'none'}
                  onValueChange={(value) => setUnitDraft((current) => ({
                    ...current,
                    customerBusinessPartnerId: value === 'none' ? undefined : value,
                    salesAgreementId: undefined,
                    salesOrderId: undefined,
                  }))}
                >
                  <SelectTrigger><SelectValue placeholder="Optional customer" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No customer</SelectItem>
                    {activeBusinessPartners.map((partner) => <SelectItem key={partner.id} value={partner.id}>{partner.partnerName}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="flex items-center justify-between rounded-lg border px-3 py-2">
                <div className="space-y-1">
                  <Label className="text-sm">Released for market</Label>
                  <div className="text-xs text-muted-foreground">
                    Required before formal sales allocation
                  </div>
                </div>
                <Switch
                  checked={Boolean(unitDraft.isReleasedForMarket)}
                  onCheckedChange={(checked) => setUnitDraft((current) => ({ ...current, isReleasedForMarket: checked }))}
                />
              </div>
              <div className="grid gap-2">
                <Label>Sales Agreement</Label>
                <Select
                  value={unitDraft.salesAgreementId || 'none'}
                  onValueChange={(value) => setUnitDraft((current) => ({ ...current, salesAgreementId: value === 'none' ? undefined : value }))}
                >
                  <SelectTrigger><SelectValue placeholder="Optional agreement" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No agreement</SelectItem>
                    {availableSalesAgreements.map((agreement) => (
                      <SelectItem key={agreement.id} value={agreement.id}>
                        {formatSalesAgreementLabel(agreement)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Sales Order</Label>
                <Select
                  value={unitDraft.salesOrderId || 'none'}
                  onValueChange={(value) => setUnitDraft((current) => ({ ...current, salesOrderId: value === 'none' ? undefined : value }))}
                >
                  <SelectTrigger><SelectValue placeholder="Optional sales order" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No sales order</SelectItem>
                    {availableSalesOrders.map((order) => (
                      <SelectItem key={order.id} value={order.id}>
                        {formatSalesOrderLabel(order)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Area (sqm)</Label>
                <Input type="number" min="0" step="0.01" value={unitDraft.areaSquareMeters ?? ''} onChange={(event) => setUnitDraft((current) => ({ ...current, areaSquareMeters: event.target.value ? Number(event.target.value) : undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Rate</Label>
                <Input type="number" min="0" step="0.01" value={unitDraft.valuationRate ?? ''} onChange={(event) => setUnitDraft((current) => ({ ...current, valuationRate: event.target.value ? Number(event.target.value) : undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Base Price</Label>
                <Input type="number" min="0" step="0.01" value={unitDraft.basePrice ?? ''} onChange={(event) => setUnitDraft((current) => ({ ...current, basePrice: event.target.value ? Number(event.target.value) : undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Handover</Label>
                <Input type="date" value={unitDraft.handoverDate || ''} onChange={(event) => setUnitDraft((current) => ({ ...current, handoverDate: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2 md:col-span-4">
                <Label>Notes</Label>
                <Textarea rows={2} value={unitDraft.notes || ''} onChange={(event) => setUnitDraft((current) => ({ ...current, notes: event.target.value || undefined }))} />
              </div>
            </div>
            <div className="flex justify-end">
              <Button disabled={!unitDraft.name?.trim()} onClick={onAddUnit}>
                <Plus className="mr-2 h-4 w-4" />
                Add Unit
              </Button>
            </div>
          </div>

          <div className="space-y-3">
            {project.units.length === 0 ? (
              <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
                No units are recorded for this project yet.
              </div>
            ) : null}
            {project.units.map((unit) => (
              <div key={unit.id} className="rounded-lg border p-4 space-y-3">
                <div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
                  <div className="space-y-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <div className="font-medium">{unit.name}</div>
                      {unit.code ? <Badge variant="outline">{unit.code}</Badge> : null}
                      <Badge>{formatCatalogLabel(unit.status)}</Badge>
                      {unit.commercialStatus && unit.commercialStatus !== unit.status ? (
                        <Badge variant="secondary">{formatCatalogLabel(unit.commercialStatus)}</Badge>
                      ) : null}
                      {unit.commercialIntent ? (
                        <Badge variant="outline">{formatCatalogLabel(unit.commercialIntent)} Intent</Badge>
                      ) : null}
                      {unit.isReleasedForMarket ? <Badge variant="outline">Released</Badge> : null}
                      {unit.handoverStatus && unit.handoverStatus !== 'NotScheduled' ? (
                        <Badge variant="outline">{formatCatalogLabel(unit.handoverStatus)}</Badge>
                      ) : null}
                      <Badge variant="secondary">{formatCatalogLabel(unit.unitType)}</Badge>
                    </div>
                    <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
                      {unit.blockName ? <span>Block {unit.blockName}</span> : null}
                      {unit.floorLabel ? <span>{unit.floorLabel}</span> : null}
                      {unit.areaSquareMeters ? <span>{unit.areaSquareMeters.toFixed(2)} sqm</span> : null}
                      {unit.customerBusinessPartnerName ? <span>{unit.customerBusinessPartnerName}</span> : null}
                      {unit.salesAgreementNumber ? (
                        <span>
                          {unit.salesAgreementType ? `${formatCatalogLabel(unit.salesAgreementType)} ` : 'Agreement '}
                          {unit.salesAgreementNumber}
                          {unit.salesAgreementStatus ? ` (${formatCatalogLabel(unit.salesAgreementStatus)})` : ''}
                        </span>
                      ) : null}
                      {unit.salesOrderNumber ? (
                        <span>
                          Order {unit.salesOrderNumber}
                          {unit.salesOrderStatus ? ` (${formatCatalogLabel(unit.salesOrderStatus)})` : ''}
                        </span>
                      ) : null}
                      {unit.releasedAt ? (
                        <span>
                          Released {formatDateLabel(unit.releasedAt)}
                          {unit.releasedByDisplayName ? ` by ${unit.releasedByDisplayName}` : ''}
                        </span>
                      ) : null}
                      {unit.handoverDate ? <span>Handover {formatDateLabel(unit.handoverDate)}</span> : null}
                    </div>
                    <div className="grid gap-3 text-sm md:grid-cols-3">
                      <div>
                        <div className="text-muted-foreground">Rate</div>
                        <div className="font-medium">{formatMoney(unit.valuationRate, unit.currency)}</div>
                      </div>
                      <div>
                        <div className="text-muted-foreground">Base Price</div>
                        <div className="font-medium">{formatMoney(unit.basePrice, unit.currency)}</div>
                      </div>
                      <div>
                        <div className="text-muted-foreground">Currency</div>
                        <div className="font-medium">{unit.currency}</div>
                      </div>
                    </div>
                    {unit.notes ? <div className="text-sm text-muted-foreground whitespace-pre-wrap">{unit.notes}</div> : null}
                  </div>
                  <div className="flex flex-wrap items-center gap-2 xl:max-w-sm xl:justify-end">
                    {!unit.salesAgreementId ? (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={!unit.isReleasedForMarket || !unit.customerBusinessPartnerId || unitActionBusyKey === `unit-sales-agreement:${unit.id}`}
                        onClick={() => onCreateSalesAgreement(unit.id)}
                      >
                        {unitActionBusyKey === `unit-sales-agreement:${unit.id}` ? 'Creating...' : 'Create Agreement'}
                      </Button>
                    ) : null}
                    {!unit.salesAgreementId ? (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={!unit.isReleasedForMarket || !unit.customerBusinessPartnerId || Boolean(unit.salesOrderId) || unitActionBusyKey === `unit-lease-agreement:${unit.id}`}
                        onClick={() => onCreateLeaseAgreement(unit.id)}
                      >
                        {unitActionBusyKey === `unit-lease-agreement:${unit.id}` ? 'Creating...' : 'Create Lease Agreement'}
                      </Button>
                    ) : null}
                    {!unit.salesOrderId ? (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={!unit.isReleasedForMarket || !unit.customerBusinessPartnerId || unitActionBusyKey === `unit-sales-order:${unit.id}`}
                        onClick={() => onCreateSalesOrder(unit.id)}
                      >
                        {unitActionBusyKey === `unit-sales-order:${unit.id}` ? 'Creating...' : 'Create Sales Order'}
                      </Button>
                    ) : null}
                    {unit.isReleasedForMarket ? (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={Boolean(unit.salesAgreementId || unit.salesOrderId) || unitActionBusyKey === `unit-withdraw-release:${unit.id}`}
                        onClick={() => onWithdrawUnitRelease(unit.id)}
                      >
                        {unitActionBusyKey === `unit-withdraw-release:${unit.id}` ? 'Withdrawing...' : 'Withdraw Release'}
                      </Button>
                    ) : (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={unitActionBusyKey === `unit-release:${unit.id}`}
                        onClick={() => onReleaseUnit(unit.id)}
                      >
                        {unitActionBusyKey === `unit-release:${unit.id}` ? 'Releasing...' : 'Release'}
                      </Button>
                    )}
                    <Button variant="ghost" size="sm" onClick={() => onDeleteUnit(unit.id)}>
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
