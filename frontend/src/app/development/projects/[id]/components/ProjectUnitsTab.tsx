import Link from 'next/link';
import { type Dispatch, type SetStateAction, useMemo, useState } from 'react';
import { ArrowRight, Building2, Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
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
  ProjectUnitTypeTemplateDto,
} from '@/services/projectService';

type UnitActionType = 'salesAgreement' | 'leaseAgreement' | 'salesOrder' | 'release' | 'withdrawRelease' | 'publishEstate' | 'delete';
type RegisteredUnitSection = 'pendingRelease' | 'released' | 'allocated' | 'invoiced';

type PendingUnitAction = {
  action: UnitActionType;
  unitId: string;
  unitName: string;
};

type ProjectUnitsTabProps = {
  project: ProjectDetailDto;
  activeBusinessPartners: BusinessPartnerDto[];
  linkOptions: ProjectLinkOptionsDto;
  unitDraft: CreateProjectUnitDto;
  setUnitDraft: Dispatch<SetStateAction<CreateProjectUnitDto>>;
  editingUnitId: string | null;
  unitTypeTemplates: ProjectUnitTypeTemplateDto[];
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
  onSaveUnit: () => void;
  onEditUnit: (unit: ProjectUnitDto) => void;
  onCancelUnitEdit: () => void;
  onReleaseUnit: (unitId: string) => Promise<void> | void;
  onWithdrawUnitRelease: (unitId: string) => Promise<void> | void;
  onPublishUnitToEstate: (unitId: string) => Promise<void> | void;
  onCreateSalesAgreement: (unitId: string) => Promise<void> | void;
  onCreateLeaseAgreement: (unitId: string) => Promise<void> | void;
  onCreateSalesOrder: (unitId: string) => Promise<void> | void;
  onDeleteUnit: (unitId: string) => Promise<void> | void;
};

const DEFAULT_RELEASE_BATCH_STATUSES = ['Draft', 'Planned', 'Released', 'Closed'];

const countUnitsByValue = (units: ProjectUnitDto[], selector: (unit: ProjectUnitDto) => string | undefined, status: string) =>
  units.filter((item) => (selector(item) || '').toLowerCase() === status.toLowerCase()).length;

const formatSalesAgreementLabel = (agreement: ProjectSalesAgreementLinkOptionDto) =>
  `${agreement.documentNumber}${agreement.agreementType ? ` [${agreement.agreementType}]` : ''}${agreement.agreementTitle ? ` - ${agreement.agreementTitle}` : ''}`;

const formatSalesOrderLabel = (order: ProjectSalesOrderLinkOptionDto) =>
  `${order.orderNumber}${order.customerName ? ` - ${order.customerName}` : ''}`;

const isTerminalSalesHandoffStatus = (status?: string) => {
  const normalized = (status || '').toLowerCase();
  return normalized === 'occupied' || normalized === 'archived' || normalized === 'sold' || normalized === 'leased';
};

const canCreateSalesAgreement = (unit: ProjectUnitDto) =>
  Boolean(unit.customerBusinessPartnerId)
  && !unit.salesAgreementId
  && !unit.salesOrderId
  && !isTerminalSalesHandoffStatus(unit.status);

const canCreateSalesOrder = (unit: ProjectUnitDto) =>
  unit.isReleasedForMarket
  && Boolean(unit.customerBusinessPartnerId)
  && !unit.salesOrderId
  && !isTerminalSalesHandoffStatus(unit.status);

const canWithdrawRelease = (unit: ProjectUnitDto) =>
  unit.isReleasedForMarket
  && !unit.salesAgreementId
  && !unit.salesOrderId;

// Estate/Project integration: Project keeps unit creation; Estate only receives released or handed-over units.
const canPublishToEstate = (unit: ProjectUnitDto) =>
  unit.isReleasedForMarket
  || ['handedover', 'occupied'].includes((unit.status || '').toLowerCase());

const getUnitActionBusyKey = (action: UnitActionType, unitId: string) => {
  switch (action) {
    case 'salesAgreement':
      return `unit-sales-agreement:${unitId}`;
    case 'leaseAgreement':
      return `unit-lease-agreement:${unitId}`;
    case 'salesOrder':
      return `unit-sales-order:${unitId}`;
    case 'release':
      return `unit-release:${unitId}`;
    case 'withdrawRelease':
      return `unit-withdraw-release:${unitId}`;
    case 'publishEstate':
      return `unit-publish-estate:${unitId}`;
    default:
      return null;
  }
};

const getUnitActionDialogCopy = (action: UnitActionType, unitName: string) => {
  switch (action) {
    case 'salesAgreement':
      return {
        title: 'Create Sales Agreement',
        confirmText: 'Create Sales Agreement',
        description: `Create a sales agreement draft for ${unitName}. The agreement will be linked back to this unit and will appear in Sales > Agreements.`,
      };
    case 'leaseAgreement':
      return {
        title: 'Create Lease Agreement',
        confirmText: 'Create Lease Agreement',
        description: `Create a lease or tenancy agreement draft for ${unitName}. The agreement will be linked back to this unit and will appear in Sales > Agreements.`,
      };
    case 'salesOrder':
      return {
        title: 'Create Sales Order',
        confirmText: 'Create Sales Order',
        description: `Create a sales order draft for ${unitName}. This will allocate the released unit in inventory and the order will appear in Sales > Orders.`,
      };
    case 'release':
      return {
        title: 'Release Unit',
        confirmText: 'Release Unit',
        description: `Release ${unitName} into inventory so it can be offered to Sales and allocated to a sales order.`,
      };
    case 'withdrawRelease':
      return {
        title: 'Withdraw Release',
        confirmText: 'Withdraw Release',
        description: `Withdraw ${unitName} from released inventory. This is only allowed while it is not linked to any sales agreement or sales order.`,
      };
    case 'publishEstate':
      return {
        title: 'Push to Estate',
        confirmText: 'Push to Estate',
        description: `Publish ${unitName} to Estate / Property Management receiving and notify Estate users that the Project unit is ready for property, leasing, occupancy, facilities, maintenance, and records operations.`,
      };
    case 'delete':
      return {
        title: 'Delete Saved Unit',
        confirmText: 'Delete Unit',
        description: `Delete ${unitName} from this project. This should only be used when the unit was created in error or is no longer needed.`,
      };
    default:
      return {
        title: 'Confirm Action',
        confirmText: 'Continue',
        description: `Proceed with the selected action for ${unitName}.`,
      };
  }
};

export function ProjectUnitsTab({
  project,
  activeBusinessPartners,
  linkOptions,
  unitDraft,
  setUnitDraft,
  editingUnitId,
  unitTypeTemplates,
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
  onSaveUnit,
  onEditUnit,
  onCancelUnitEdit,
  onReleaseUnit,
  onWithdrawUnitRelease,
  onPublishUnitToEstate,
  onCreateSalesAgreement,
  onCreateLeaseAgreement,
  onCreateSalesOrder,
  onDeleteUnit,
}: ProjectUnitsTabProps) {
  const [buildingDraft, setBuildingDraft] = useState<CreateProjectBuildingDto>({ name: '' });
  const [floorDraft, setFloorDraft] = useState<CreateProjectFloorDto>({ name: '' });
  const [releaseBatchDraft, setReleaseBatchDraft] = useState<CreateProjectUnitReleaseBatchDto>({ name: '', status: DEFAULT_RELEASE_BATCH_STATUSES[0] });
  const [pendingUnitAction, setPendingUnitAction] = useState<PendingUnitAction | null>(null);

  const totals = useMemo(() => ({
    area: project.units.reduce((sum, item) => sum + (item.areaSquareMeters || 0), 0),
    value: project.units.reduce((sum, item) => sum + (item.basePrice || 0), 0),
    available: countUnitsByValue(project.units, (item) => item.commercialStatus, 'Available'),
    reserved: countUnitsByValue(project.units, (item) => item.commercialStatus, 'Reserved'),
    soldOrLeased:
      countUnitsByValue(project.units, (item) => item.commercialStatus, 'Sold')
      + countUnitsByValue(project.units, (item) => item.commercialStatus, 'Leased'),
    released: project.units.filter((item) => item.isReleasedForMarket).length,
  }), [project.units]);

  const selectedUnitTypeTemplate = useMemo(
    () => unitTypeTemplates.find((item) => item.id === unitDraft.projectUnitTypeTemplateId),
    [unitDraft.projectUnitTypeTemplateId, unitTypeTemplates],
  );
  const draftAmenities = useMemo(
    () => (unitDraft.amenities || []).map((amenity, index) => ({
      ...amenity,
      quantity: amenity.quantity ?? 1,
      unitCost: amenity.unitCost ?? 0,
      totalCost: (amenity.quantity ?? 1) * (amenity.unitCost ?? 0),
      sortOrder: amenity.sortOrder ?? index,
    })),
    [unitDraft.amenities],
  );
  const draftAmenityTotal = useMemo(
    () => draftAmenities.reduce((sum, amenity) => sum + amenity.totalCost, 0),
    [draftAmenities],
  );

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
  const releasedInventoryUnits = useMemo(() => project.units.filter((item) => item.inventoryStatus === 'Released'), [project.units]);
  const allocatedInventoryUnits = useMemo(() => project.units.filter((item) => item.inventoryStatus === 'Allocated'), [project.units]);
  const invoicedInventoryUnits = useMemo(() => project.units.filter((item) => item.inventoryStatus === 'Invoiced'), [project.units]);
  const pendingReleaseUnits = useMemo(() => project.units.filter((item) => item.inventoryStatus === 'PendingRelease'), [project.units]);

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

  const handleUnitTypeTemplateChange = (value: string) => {
    const projectUnitTypeTemplateId = value === 'none' ? undefined : value;
    const template = unitTypeTemplates.find((item) => item.id === projectUnitTypeTemplateId);

    setUnitDraft((current) => ({
      ...current,
      projectUnitTypeTemplateId,
      unitType: template?.defaultProjectUnitType || current.unitType,
      currency: template?.currency || current.currency,
      amenities: template
        ? template.amenities.map((amenity, index) => ({
            inventoryItemId: amenity.inventoryItemId,
            itemCode: amenity.itemCode,
            amenityName: amenity.amenityName,
            quantity: amenity.quantity,
            unitCost: amenity.unitCost,
            sortOrder: amenity.sortOrder ?? index,
          }))
        : [],
      basePrice: template ? template.totalCost : current.basePrice,
    }));
  };

  const openUnitActionDialog = (unit: ProjectUnitDto, action: UnitActionType) => {
    setPendingUnitAction({
      action,
      unitId: unit.id,
      unitName: unit.name,
    });
  };

  const handleConfirmUnitAction = async () => {
    if (!pendingUnitAction) {
      return;
    }

    switch (pendingUnitAction.action) {
      case 'salesAgreement':
        await onCreateSalesAgreement(pendingUnitAction.unitId);
        break;
      case 'leaseAgreement':
        await onCreateLeaseAgreement(pendingUnitAction.unitId);
        break;
      case 'salesOrder':
        await onCreateSalesOrder(pendingUnitAction.unitId);
        break;
      case 'release':
        await onReleaseUnit(pendingUnitAction.unitId);
        break;
      case 'withdrawRelease':
        await onWithdrawUnitRelease(pendingUnitAction.unitId);
        break;
      case 'publishEstate':
        await onPublishUnitToEstate(pendingUnitAction.unitId);
        break;
      case 'delete':
        await onDeleteUnit(pendingUnitAction.unitId);
        break;
      default:
        break;
    }
  };

  const renderUnitCard = (unit: ProjectUnitDto, section: RegisteredUnitSection) => {
    const showPendingReleaseActions = section === 'pendingRelease';
    const showReleasedActions = section === 'released';
    const missingCustomerForSalesOrder = showReleasedActions && !unit.customerBusinessPartnerId && !unit.salesOrderId;

    return (
      <div key={unit.id} className="rounded-lg border p-4 space-y-3">
        <div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
          <div className="space-y-2">
            <div className="flex flex-wrap items-center gap-2">
              <div className="font-medium">{unit.name}</div>
              {unit.code ? <Badge variant="outline">{unit.code}</Badge> : null}
              <Badge>{formatCatalogLabel(unit.status)}</Badge>
              {unit.projectUnitTypeTemplateName ? <Badge variant="secondary">{unit.projectUnitTypeTemplateName}</Badge> : null}
              {unit.commercialStatus && unit.commercialStatus !== unit.status ? <Badge variant="secondary">{formatCatalogLabel(unit.commercialStatus)}</Badge> : null}
              {unit.inventoryStatus ? <Badge variant="outline">{formatCatalogLabel(unit.inventoryStatus)}</Badge> : null}
              {unit.commercialIntent ? <Badge variant="outline">{formatCatalogLabel(unit.commercialIntent)} Intent</Badge> : null}
              {unit.projectUnitReleaseBatchName ? <Badge variant="secondary">{unit.projectUnitReleaseBatchCode ? `${unit.projectUnitReleaseBatchCode} · ` : ''}{unit.projectUnitReleaseBatchName}</Badge> : null}
              {unit.isReleasedForMarket ? <Badge variant="outline">Released</Badge> : null}
            </div>
            <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
              {unit.projectBuildingName ? <span>{unit.projectBuildingCode ? `${unit.projectBuildingCode} · ` : ''}{unit.projectBuildingName}</span> : null}
              {unit.projectFloorName ? <span>{unit.projectFloorCode ? `${unit.projectFloorCode} · ` : ''}{unit.projectFloorName}</span> : null}
              {unit.areaSquareMeters ? <span>{unit.areaSquareMeters.toFixed(2)} sqm</span> : null}
              {unit.customerBusinessPartnerName ? <span>{unit.customerBusinessPartnerName}</span> : null}
              {unit.salesAgreementId && unit.salesAgreementNumber ? (
                <span>
                  {unit.salesAgreementType ? `${formatCatalogLabel(unit.salesAgreementType)} ` : 'Agreement '}
                  <Link href={`/sales/agreements/${unit.salesAgreementId}`} className="text-sky-700 underline underline-offset-4">
                    {unit.salesAgreementNumber}
                  </Link>
                  {unit.salesAgreementStatus ? ` (${formatCatalogLabel(unit.salesAgreementStatus)})` : ''}
                </span>
              ) : null}
              {unit.salesOrderId && unit.salesOrderNumber ? (
                <span>
                  Order{' '}
                  <Link href={`/sales/orders/${unit.salesOrderId}`} className="text-sky-700 underline underline-offset-4">
                    {unit.salesOrderNumber}
                  </Link>
                  {unit.salesOrderStatus ? ` (${formatCatalogLabel(unit.salesOrderStatus)})` : ''}
                </span>
              ) : null}
              {unit.handoverDate ? <span>Handover {formatDateLabel(unit.handoverDate)}</span> : null}
            </div>
            <div className="grid gap-3 text-sm md:grid-cols-4">
              <div><div className="text-muted-foreground">Rate</div><div className="font-medium">{formatMoney(unit.valuationRate, unit.currency)}</div></div>
              <div><div className="text-muted-foreground">Cost</div><div className="font-medium">{formatMoney(unit.basePrice, unit.currency)}</div></div>
              <div><div className="text-muted-foreground">Amenities</div><div className="font-medium">{unit.amenities.length}</div></div>
              <div><div className="text-muted-foreground">Currency</div><div className="font-medium">{unit.currency}</div></div>
            </div>
            {missingCustomerForSalesOrder ? (
              <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
                Assign a customer before using the direct workspace sales-order action for this released unit. You can also start a draft manually from Sales and link the unit there.
              </div>
            ) : null}
            {unit.notes ? <div className="text-sm text-muted-foreground whitespace-pre-wrap">{unit.notes}</div> : null}
          </div>
          <div className="flex flex-wrap items-center gap-2 xl:max-w-sm xl:justify-end">
            {showPendingReleaseActions && !unit.salesAgreementId ? (
              <Button
                variant="outline"
                size="sm"
                disabled={!canCreateSalesAgreement(unit) || unitActionBusyKey === `unit-sales-agreement:${unit.id}`}
                onClick={() => openUnitActionDialog(unit, 'salesAgreement')}
              >
                {unitActionBusyKey === `unit-sales-agreement:${unit.id}` ? 'Creating...' : 'Create Sales Agreement'}
              </Button>
            ) : null}
            {showPendingReleaseActions && !unit.salesAgreementId ? (
              <Button
                variant="outline"
                size="sm"
                disabled={!canCreateSalesAgreement(unit) || unitActionBusyKey === `unit-lease-agreement:${unit.id}`}
                onClick={() => openUnitActionDialog(unit, 'leaseAgreement')}
              >
                {unitActionBusyKey === `unit-lease-agreement:${unit.id}` ? 'Creating...' : 'Create Lease Agreement'}
              </Button>
            ) : null}
            {showPendingReleaseActions && !unit.isReleasedForMarket ? (
              <Button
                variant="outline"
                size="sm"
                disabled={unitActionBusyKey === `unit-release:${unit.id}`}
                onClick={() => openUnitActionDialog(unit, 'release')}
              >
                {unitActionBusyKey === `unit-release:${unit.id}` ? 'Releasing...' : 'Release'}
              </Button>
            ) : null}
            {showReleasedActions && !unit.salesOrderId ? (
              <Button
                variant="outline"
                size="sm"
                disabled={!canCreateSalesOrder(unit) || unitActionBusyKey === `unit-sales-order:${unit.id}`}
                onClick={() => openUnitActionDialog(unit, 'salesOrder')}
              >
                {unitActionBusyKey === `unit-sales-order:${unit.id}` ? 'Creating...' : 'Create Sales Order'}
              </Button>
            ) : null}
            {showReleasedActions ? (
              <Button
                variant="outline"
                size="sm"
                disabled={!canPublishToEstate(unit) || unitActionBusyKey === `unit-publish-estate:${unit.id}`}
                onClick={() => openUnitActionDialog(unit, 'publishEstate')}
              >
                {unitActionBusyKey === `unit-publish-estate:${unit.id}` ? 'Pushing...' : 'Push to Estate'}
              </Button>
            ) : null}
            {showReleasedActions && unit.isReleasedForMarket ? (
              <Button
                variant="outline"
                size="sm"
                disabled={!canWithdrawRelease(unit) || unitActionBusyKey === `unit-withdraw-release:${unit.id}`}
                onClick={() => openUnitActionDialog(unit, 'withdrawRelease')}
              >
                {unitActionBusyKey === `unit-withdraw-release:${unit.id}` ? 'Withdrawing...' : 'Withdraw Release'}
              </Button>
            ) : null}
            <Button variant="outline" size="sm" onClick={() => onEditUnit(unit)}>
              Edit
            </Button>
            <Button variant="ghost" size="sm" onClick={() => openUnitActionDialog(unit, 'delete')}><Trash2 className="h-4 w-4" /></Button>
          </div>
        </div>
      </div>
    );
  };

  const costIsTemplateDriven = Boolean(unitDraft.projectUnitTypeTemplateId);
  const pendingActionCopy = pendingUnitAction ? getUnitActionDialogCopy(pendingUnitAction.action, pendingUnitAction.unitName) : null;
  const pendingActionBusyKey = pendingUnitAction ? getUnitActionBusyKey(pendingUnitAction.action, pendingUnitAction.unitId) : null;

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
          <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Cost</div><div className="text-xl font-semibold">{formatMoney(totals.value, project.units[0]?.currency)}</div></div>
          <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Commercial</div><div className="text-2xl font-semibold">{totals.soldOrLeased}</div><div className="text-sm text-muted-foreground">{totals.reserved} reserved, {totals.available} available</div></div>
          <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Hierarchy</div><div className="text-xl font-semibold">{project.buildings.length} / {project.floors.length} / {project.unitReleaseBatches.length}</div><div className="text-sm text-muted-foreground">Buildings, floors, release batches</div></div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
            <div className="flex items-start gap-3">
              <div className="flex h-10 w-10 items-center justify-center rounded-md border bg-muted">
                <Building2 className="h-5 w-5 text-primary" />
              </div>
              <div>
                <CardTitle>Estate Callback</CardTitle>
                <Badge variant="outline" className="mt-3 w-fit">
                  Source: Estate Land Bank - Project Management - Estate / Property Management
                </Badge>
              </div>
            </div>
            <div className="flex flex-wrap gap-2">
              <Button asChild variant="outline" size="sm">
                <Link href="/estate/land-management">
                  Estate Land Bank
                  <ArrowRight className="ml-2 h-4 w-4" />
                </Link>
              </Button>
              <Button asChild size="sm">
                <Link href="/estate/property-management/EstatePropertyManagementPropertyUnit">
                  Property Receiving
                  <ArrowRight className="ml-2 h-4 w-4" />
                </Link>
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent className="grid gap-3 md:grid-cols-4">
          <div className="rounded-md border p-4">
            <div className="text-sm text-muted-foreground">Ready to push</div>
            <div className="text-2xl font-semibold">{releasedInventoryUnits.length}</div>
          </div>
          <div className="rounded-md border p-4">
            <div className="text-sm text-muted-foreground">Pending release</div>
            <div className="text-2xl font-semibold">{pendingReleaseUnits.length}</div>
          </div>
          <div className="rounded-md border p-4">
            <div className="text-sm text-muted-foreground">Allocated</div>
            <div className="text-2xl font-semibold">{allocatedInventoryUnits.length}</div>
          </div>
          <div className="rounded-md border p-4">
            <div className="text-sm text-muted-foreground">Invoiced</div>
            <div className="text-2xl font-semibold">{invoicedInventoryUnits.length}</div>
          </div>
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
        <CardHeader><CardTitle>{editingUnitId ? 'Edit Unit' : 'Add Unit'}</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <Tabs defaultValue="details" className="space-y-4">
            <TabsList className="grid w-full grid-cols-2">
              <TabsTrigger value="details">Details</TabsTrigger>
              <TabsTrigger value="amenities">Amenities</TabsTrigger>
            </TabsList>
            <TabsContent value="details" className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2"><Label>Code</Label><Input value={unitDraft.code || ''} onChange={(event) => setUnitDraft((current) => ({ ...current, code: event.target.value || undefined }))} /></div>
                <div className="grid gap-2 md:col-span-2"><Label>Name</Label><Input value={unitDraft.name} onChange={(event) => setUnitDraft((current) => ({ ...current, name: event.target.value }))} /></div>
                <div className="grid gap-2">
                  <Label>Status</Label>
                  <Select value={unitDraft.status || unitStatusOptions[0]} onValueChange={(value) => setUnitDraft((current) => ({ ...current, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{unitStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select>
                  <div className="text-xs text-muted-foreground">
                    Status tracks the unit&apos;s commercial lifecycle. Release controls whether the unit sits in inventory, and direct workspace sales actions also need a customer on the unit.
                  </div>
                </div>
                <div className="grid gap-2 md:col-span-2">
                  <Label>Unit Type</Label>
                  <Select value={unitDraft.projectUnitTypeTemplateId || 'none'} onValueChange={handleUnitTypeTemplateChange}>
                    <SelectTrigger><SelectValue placeholder="Optional unit type preset" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No preset</SelectItem>
                      {unitTypeTemplates.map((item) => (
                        <SelectItem key={item.id} value={item.id}>
                          {item.code ? `${item.code} · ${item.name}` : item.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {selectedUnitTypeTemplate ? (
                    <div className="text-xs text-muted-foreground">
                      Preset cost {formatMoney(selectedUnitTypeTemplate.totalCost, selectedUnitTypeTemplate.currency || unitDraft.currency)} across {selectedUnitTypeTemplate.amenities.length} amenity item(s).
                    </div>
                  ) : null}
                </div>
                <div className="grid gap-2"><Label>Classification</Label><Select value={unitDraft.unitType || unitTypeOptions[0]} onValueChange={(value) => setUnitDraft((current) => ({ ...current, unitType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{unitTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Building</Label><Select value={unitDraft.projectBuildingId || 'none'} onValueChange={handleBuildingChange}><SelectTrigger><SelectValue placeholder="Optional building" /></SelectTrigger><SelectContent><SelectItem value="none">No building</SelectItem>{project.buildings.map((building) => <SelectItem key={building.id} value={building.id}>{building.code ? `${building.code} · ${building.name}` : building.name}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Floor</Label><Select value={unitDraft.projectFloorId || 'none'} onValueChange={handleFloorChange}><SelectTrigger><SelectValue placeholder="Optional floor" /></SelectTrigger><SelectContent><SelectItem value="none">No floor</SelectItem>{availableUnitFloors.map((floor) => <SelectItem key={floor.id} value={floor.id}>{floor.code ? `${floor.code} · ${floor.name}` : floor.name}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Release Batch</Label><Select value={unitDraft.projectUnitReleaseBatchId || 'none'} onValueChange={(value) => setUnitDraft((current) => ({ ...current, projectUnitReleaseBatchId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="Optional release batch" /></SelectTrigger><SelectContent><SelectItem value="none">No release batch</SelectItem>{availableUnitReleaseBatches.map((batch) => <SelectItem key={batch.id} value={batch.id}>{batch.code ? `${batch.code} · ${batch.name}` : batch.name}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Customer</Label><Select value={unitDraft.customerBusinessPartnerId || 'none'} onValueChange={(value) => setUnitDraft((current) => ({ ...current, customerBusinessPartnerId: value === 'none' ? undefined : value, salesAgreementId: undefined, salesOrderId: undefined }))}><SelectTrigger><SelectValue placeholder="Optional customer" /></SelectTrigger><SelectContent><SelectItem value="none">No customer</SelectItem>{activeBusinessPartners.map((partner) => <SelectItem key={partner.id} value={partner.id}>{partner.partnerName}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Block Label</Label><Input value={unitDraft.blockName || ''} onChange={(event) => setUnitDraft((current) => ({ ...current, blockName: event.target.value || undefined }))} /></div>
                <div className="grid gap-2"><Label>Floor Label</Label><Input value={unitDraft.floorLabel || ''} onChange={(event) => setUnitDraft((current) => ({ ...current, floorLabel: event.target.value || undefined }))} /></div>
                <div className="grid gap-2"><Label>Area (sqm)</Label><Input type="number" min="0" step="0.01" value={unitDraft.areaSquareMeters ?? ''} onChange={(event) => setUnitDraft((current) => ({ ...current, areaSquareMeters: event.target.value ? Number(event.target.value) : undefined }))} /></div>
                <div className="grid gap-2"><Label>Rate</Label><Input type="number" min="0" step="0.01" value={unitDraft.valuationRate ?? ''} onChange={(event) => setUnitDraft((current) => ({ ...current, valuationRate: event.target.value ? Number(event.target.value) : undefined }))} /></div>
                <div className="grid gap-2">
                  <Label>Cost</Label>
                  <Input type="number" min="0" step="0.01" value={unitDraft.basePrice ?? draftAmenityTotal ?? ''} readOnly={costIsTemplateDriven} className={costIsTemplateDriven ? 'bg-muted' : undefined} onChange={(event) => setUnitDraft((current) => ({ ...current, basePrice: event.target.value ? Number(event.target.value) : undefined }))} />
                  {costIsTemplateDriven ? <div className="text-xs text-muted-foreground">Derived from the selected unit type amenities.</div> : null}
                </div>
                <div className="grid gap-2"><Label>Handover</Label><Input type="date" value={unitDraft.handoverDate || ''} onChange={(event) => setUnitDraft((current) => ({ ...current, handoverDate: event.target.value || undefined }))} /></div>
                <div className="grid gap-2">
                  <Label>Sales Agreement</Label>
                  <Select value={unitDraft.salesAgreementId || 'none'} onValueChange={(value) => setUnitDraft((current) => ({ ...current, salesAgreementId: value === 'none' ? undefined : value }))} disabled={!unitDraft.customerBusinessPartnerId && availableSalesAgreements.length === 0}>
                    <SelectTrigger><SelectValue placeholder="Optional agreement" /></SelectTrigger>
                    <SelectContent><SelectItem value="none">No agreement</SelectItem>{availableSalesAgreements.map((agreement) => <SelectItem key={agreement.id} value={agreement.id}>{formatSalesAgreementLabel(agreement)}</SelectItem>)}</SelectContent>
                  </Select>
                  {!unitDraft.customerBusinessPartnerId ? <div className="text-xs text-muted-foreground">Select a customer first to narrow the agreement list.</div> : null}
                </div>
                <div className="grid gap-2">
                  <Label>Sales Order</Label>
                  <Select value={unitDraft.salesOrderId || 'none'} onValueChange={(value) => setUnitDraft((current) => ({ ...current, salesOrderId: value === 'none' ? undefined : value }))} disabled={!unitDraft.isReleasedForMarket}>
                    <SelectTrigger><SelectValue placeholder="Optional sales order" /></SelectTrigger>
                    <SelectContent><SelectItem value="none">No sales order</SelectItem>{availableSalesOrders.map((order) => <SelectItem key={order.id} value={order.id}>{formatSalesOrderLabel(order)}</SelectItem>)}</SelectContent>
                  </Select>
                  {!unitDraft.isReleasedForMarket ? <div className="text-xs text-muted-foreground">Released units move into inventory and can then be allocated to sales orders.</div> : null}
                </div>
                <div className="flex items-center justify-between rounded-lg border px-3 py-2">
                  <div><Label className="text-sm">Released for market</Label><div className="text-xs text-muted-foreground">Required before sales allocation</div></div>
                  <Switch checked={Boolean(unitDraft.isReleasedForMarket)} onCheckedChange={(checked) => setUnitDraft((current) => ({ ...current, isReleasedForMarket: checked, salesOrderId: checked ? current.salesOrderId : undefined }))} />
                </div>
                <div className="grid gap-2 md:col-span-4"><Label>Notes</Label><Textarea rows={2} value={unitDraft.notes || ''} onChange={(event) => setUnitDraft((current) => ({ ...current, notes: event.target.value || undefined }))} /></div>
              </div>
            </TabsContent>
            <TabsContent value="amenities" className="space-y-4">
              {draftAmenities.length === 0 ? (
                <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
                  Select a unit type preset to load the amenity cost breakdown for this unit.
                </div>
              ) : (
                <div className="rounded-lg border bg-card">
                  <div className="flex flex-wrap items-center justify-between gap-3 border-b bg-muted/30 px-4 py-3">
                    <div>
                      <div className="font-medium">Amenity Cost Breakdown</div>
                      <div className="text-xs text-muted-foreground">
                        The preset amenity lines roll up into the unit cost shown on the details tab.
                      </div>
                    </div>
                    <Badge variant="outline">{draftAmenities.length} line(s)</Badge>
                  </div>
                  <div className="overflow-x-auto">
                    <table className="w-full min-w-[820px] table-fixed border-collapse text-sm">
                      <colgroup>
                        <col className="w-14" />
                        <col className="w-[34%]" />
                        <col className="w-[20%]" />
                        <col className="w-20" />
                        <col className="w-[18%]" />
                        <col className="w-[18%]" />
                      </colgroup>
                      <thead className="bg-muted/40 text-left">
                        <tr className="border-b">
                          <th className="px-4 py-3 font-medium">#</th>
                          <th className="px-4 py-3 font-medium">Amenity</th>
                          <th className="px-4 py-3 font-medium">Item Code</th>
                          <th className="px-4 py-3 font-medium">Qty</th>
                          <th className="px-4 py-3 font-medium">Unit Cost</th>
                          <th className="px-4 py-3 font-medium">Total</th>
                        </tr>
                      </thead>
                      <tbody>
                        {draftAmenities.map((amenity, index) => (
                          <tr
                            key={`${amenity.itemCode || amenity.amenityName}-${amenity.sortOrder}`}
                            className="border-b align-top odd:bg-white even:bg-slate-50/40"
                          >
                            <td className="px-4 py-3 font-medium text-muted-foreground">{index + 1}</td>
                            <td className="px-4 py-3 font-medium">{amenity.amenityName}</td>
                            <td className="px-4 py-3 text-muted-foreground">{amenity.itemCode || '-'}</td>
                            <td className="px-4 py-3 whitespace-nowrap">{amenity.quantity}</td>
                            <td className="px-4 py-3 whitespace-nowrap">{formatMoney(amenity.unitCost, unitDraft.currency)}</td>
                            <td className="px-4 py-3 font-medium whitespace-nowrap">{formatMoney(amenity.totalCost, unitDraft.currency)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                  <div className="flex items-center justify-between border-t bg-slate-50 px-4 py-3 text-sm font-medium">
                    <span>Total Amenity Cost</span>
                    <span>{formatMoney(draftAmenityTotal, unitDraft.currency)}</span>
                  </div>
                </div>
              )}
            </TabsContent>
          </Tabs>
          <div className="flex justify-end gap-2">
            {editingUnitId ? (
              <Button variant="outline" onClick={onCancelUnitEdit}>
                Cancel
              </Button>
            ) : null}
            <Button disabled={!unitDraft.name?.trim()} onClick={onSaveUnit}><Plus className="mr-2 h-4 w-4" />{editingUnitId ? 'Save Unit' : 'Add Unit'}</Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Registered Units</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          {project.units.length === 0 ? <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">No units are recorded for this project yet.</div> : null}
          {pendingReleaseUnits.length > 0 ? (
            <div className="rounded-xl border border-dashed p-4">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div>
                  <div className="font-medium">Pending Release</div>
                  <div className="text-sm text-muted-foreground">These units are registered but not yet in released inventory.</div>
                </div>
                <Badge variant="outline">{pendingReleaseUnits.length}</Badge>
              </div>
              <div className="mt-3 space-y-3">
                {pendingReleaseUnits.map((unit) => renderUnitCard(unit, 'pendingRelease'))}
              </div>
            </div>
          ) : null}
          <Tabs defaultValue="released" className="space-y-4">
            <TabsList className="grid w-full grid-cols-3">
              <TabsTrigger value="released">Released ({releasedInventoryUnits.length})</TabsTrigger>
              <TabsTrigger value="allocated">Allocated ({allocatedInventoryUnits.length})</TabsTrigger>
              <TabsTrigger value="invoiced">Invoiced ({invoicedInventoryUnits.length})</TabsTrigger>
            </TabsList>
            <TabsContent value="released" className="space-y-3">
              {releasedInventoryUnits.length === 0 ? <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">No released units are currently sitting in inventory.</div> : releasedInventoryUnits.map((unit) => renderUnitCard(unit, 'released'))}
            </TabsContent>
            <TabsContent value="allocated" className="space-y-3">
              {allocatedInventoryUnits.length === 0 ? <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">No units are currently allocated to sales orders.</div> : allocatedInventoryUnits.map((unit) => renderUnitCard(unit, 'allocated'))}
            </TabsContent>
            <TabsContent value="invoiced" className="space-y-3">
              {invoicedInventoryUnits.length === 0 ? <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">No invoiced units are recorded yet.</div> : invoicedInventoryUnits.map((unit) => renderUnitCard(unit, 'invoiced'))}
            </TabsContent>
          </Tabs>
        </CardContent>
      </Card>

      {pendingUnitAction && pendingActionCopy ? (
        <ConfirmationDialog
          open={Boolean(pendingUnitAction)}
          onOpenChange={(open) => {
            if (!open) {
              setPendingUnitAction(null);
            }
          }}
          title={pendingActionCopy.title}
          description={pendingActionCopy.description}
          confirmText={pendingActionCopy.confirmText}
          variant={pendingUnitAction.action === 'delete' ? 'destructive' : 'default'}
          onConfirm={handleConfirmUnitAction}
          isLoading={Boolean(pendingActionBusyKey && pendingActionBusyKey === unitActionBusyKey)}
        />
      ) : null}
    </div>
  );
}
