import { type Dispatch, type SetStateAction, useMemo } from 'react';
import { AlertTriangle, ArrowRightCircle, Pencil, Plus, Trash2, X } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import type { BusinessPartnerDto } from '@/services/businessPartnerService';
import type { ContractDto } from '@/services/contractService';
import type {
  CreateProjectBoqItemDto,
  CreateProjectPackageDto,
  ProjectBoqItemDto,
  ProjectDetailDto,
  ProjectPackageDto,
  ProjectPhaseDto,
  ProjectProcurementPlanItemLookupDto,
  ProjectPurchaseOrderLookupDto,
  ProjectPurchaseRequisitionLookupDto,
  ProjectTenderLookupDto,
} from '@/services/projectService';

type ProjectPackagesTabProps = {
  project: ProjectDetailDto;
  phases: ProjectPhaseDto[];
  packageDraft: CreateProjectPackageDto;
  editingPackageId: string | null;
  setPackageDraft: Dispatch<SetStateAction<CreateProjectPackageDto>>;
  boqDraft: CreateProjectBoqItemDto;
  editingBoqItemId: string | null;
  setBoqDraft: Dispatch<SetStateAction<CreateProjectBoqItemDto>>;
  activeBusinessPartners: BusinessPartnerDto[];
  activeContracts: ContractDto[];
  tenders: ProjectTenderLookupDto[];
  procurementPlanItems: ProjectProcurementPlanItemLookupDto[];
  purchaseRequisitions: ProjectPurchaseRequisitionLookupDto[];
  purchaseOrders: ProjectPurchaseOrderLookupDto[];
  packageTypeOptions: string[];
  packageStatusOptions: string[];
  boqItemTypeOptions: string[];
  formatMoney: (value: number | undefined, currency?: string | null, maximumFractionDigits?: number) => string;
  formatCatalogLabel: (value?: string | null) => string;
  onSavePackage: () => void;
  onEditPackage: (projectPackage: ProjectPackageDto) => void;
  onCancelPackageEdit: () => void;
  onDeletePackage: (packageId: string) => void;
  onSaveBoqItem: () => void;
  onEditBoqItem: (boqItem: ProjectBoqItemDto) => void;
  onCancelBoqItemEdit: () => void;
  onDeleteBoqItem: (boqItemId: string) => void;
};

const flattenPhases = (phases: ProjectPhaseDto[], depth = 0): Array<{ id: string; label: string }> =>
  phases.flatMap((phase) => [
    { id: phase.id, label: `${' '.repeat(depth * 2)}${phase.name}`.trimStart() },
    ...flattenPhases(phase.children || [], depth + 1),
  ]);

const formatReference = (...parts: Array<string | undefined>) => parts.filter(Boolean).join(' - ');

const getPhaseSyncTone = (status?: string) => {
  switch ((status || '').toLowerCase()) {
    case 'aligned':
    case 'stable':
      return 'border-emerald-200 bg-emerald-50 text-emerald-700';
    case 'lagging':
      return 'border-amber-200 bg-amber-50 text-amber-700';
    case 'unassigned':
      return 'border-slate-300 bg-slate-50 text-slate-700';
    default:
      return 'border-blue-200 bg-blue-50 text-blue-700';
  }
};

export function ProjectPackagesTab({
  project,
  phases,
  packageDraft,
  editingPackageId,
  setPackageDraft,
  boqDraft,
  editingBoqItemId,
  setBoqDraft,
  activeBusinessPartners,
  activeContracts,
  tenders,
  procurementPlanItems,
  purchaseRequisitions,
  purchaseOrders,
  packageTypeOptions,
  packageStatusOptions,
  boqItemTypeOptions,
  formatMoney,
  formatCatalogLabel,
  onSavePackage,
  onEditPackage,
  onCancelPackageEdit,
  onDeletePackage,
  onSaveBoqItem,
  onEditBoqItem,
  onCancelBoqItemEdit,
  onDeleteBoqItem,
}: ProjectPackagesTabProps) {
  const phaseOptions = useMemo(() => flattenPhases(phases), [phases]);
  const totalBudget = useMemo(() => project.packages.reduce((sum, item) => sum + (item.budgetAmount ?? 0), 0), [project.packages]);
  const totalCommitted = useMemo(() => project.packages.reduce((sum, item) => sum + (item.committedAmount ?? 0), 0), [project.packages]);
  const totalActual = useMemo(() => project.packages.reduce((sum, item) => sum + (item.actualAmount ?? 0), 0), [project.packages]);
  const totalForecast = useMemo(() => project.packages.reduce((sum, item) => sum + (item.forecastAmount ?? 0), 0), [project.packages]);
  const alignedPackageCount = useMemo(() => project.packages.filter((item) => item.isPhaseCommerciallyAligned).length, [project.packages]);
  const laggingPackageCount = useMemo(
    () => project.packages.filter((item) => item.phaseCommercialSyncStatus === 'Lagging').length,
    [project.packages],
  );
  const unassignedPackageCount = useMemo(
    () => project.packages.filter((item) => item.phaseCommercialSyncStatus === 'Unassigned').length,
    [project.packages],
  );
  const selectedPackageBoqItems = useMemo(
    () => (boqDraft.projectPackageId ? project.boqItems.filter((item) => item.projectPackageId === boqDraft.projectPackageId) : []),
    [boqDraft.projectPackageId, project.boqItems],
  );

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Packages & BOQ</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-4">
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Packages</div>
              <div className="text-2xl font-semibold">{project.packages.length}</div>
              <div className="text-sm text-muted-foreground">Construction, trade, and supply groupings</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Budget</div>
              <div className="text-2xl font-semibold">{formatMoney(totalBudget)}</div>
              <div className="text-sm text-muted-foreground">Package-level baseline</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Committed</div>
              <div className="text-2xl font-semibold">{formatMoney(totalCommitted)}</div>
              <div className="text-sm text-muted-foreground">Linked procurement exposure</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Actual / Forecast</div>
              <div className="text-2xl font-semibold">{formatMoney(totalActual)}</div>
              <div className="text-sm text-muted-foreground">Forecast {formatMoney(totalForecast)}</div>
            </div>
          </div>

          <div className="grid gap-4 md:grid-cols-3">
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Phase-Aligned</div>
              <div className="text-2xl font-semibold">{alignedPackageCount}</div>
              <div className="text-sm text-muted-foreground">Packages already matching the current phase posture</div>
            </div>
            <div className="rounded-lg border border-amber-200 bg-amber-50/60 p-4">
              <div className="text-sm text-amber-700">Need Commercial Sync</div>
              <div className="text-2xl font-semibold text-amber-900">{laggingPackageCount}</div>
              <div className="text-sm text-amber-700">Packages lagging procurement or construction follow-through</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Unassigned to Phase</div>
              <div className="text-2xl font-semibold">{unassignedPackageCount}</div>
              <div className="text-sm text-muted-foreground">Packages that still need a phase before workflow nudges apply</div>
            </div>
          </div>

          {laggingPackageCount > 0 ? (
            <div className="flex items-start gap-3 rounded-lg border border-amber-200 bg-amber-50/70 p-4 text-sm text-amber-900">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
              <div>
                {laggingPackageCount} package(s) are behind their linked phase. Review the sync badges below and complete the recommended procurement or commercial step.
              </div>
            </div>
          ) : null}

          <div className="grid gap-4 xl:grid-cols-2">
            <div className="rounded-lg border p-4 space-y-4">
              <div className="font-medium">{editingPackageId ? 'Edit Package' : 'New Package'}</div>
              <div className="grid gap-4 md:grid-cols-2">
                <div className="grid gap-2">
                  <Label>Name</Label>
                  <Input value={packageDraft.name} onChange={(event) => setPackageDraft((current) => ({ ...current, name: event.target.value }))} />
                </div>
                <div className="grid gap-2">
                  <Label>Code</Label>
                  <Input value={packageDraft.code || ''} onChange={(event) => setPackageDraft((current) => ({ ...current, code: event.target.value || undefined }))} />
                </div>
                <div className="grid gap-2">
                  <Label>Phase</Label>
                  <Select value={packageDraft.projectPhaseId || 'none'} onValueChange={(value) => setPackageDraft((current) => ({ ...current, projectPhaseId: value === 'none' ? undefined : value }))}>
                    <SelectTrigger><SelectValue placeholder="Select phase" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No phase</SelectItem>
                      {phaseOptions.map((phase) => <SelectItem key={phase.id} value={phase.id}>{phase.label}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Package Type</Label>
                  <Select value={packageDraft.packageType || packageTypeOptions[0]} onValueChange={(value) => setPackageDraft((current) => ({ ...current, packageType: value }))}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>{packageTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Status</Label>
                  <Select value={packageDraft.status || packageStatusOptions[0]} onValueChange={(value) => setPackageDraft((current) => ({ ...current, status: value }))}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>{packageStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Currency</Label>
                  <Input value={packageDraft.currency || ''} onChange={(event) => setPackageDraft((current) => ({ ...current, currency: event.target.value || undefined }))} />
                </div>
                <div className="grid gap-2">
                  <Label>Business Partner</Label>
                  <Select value={packageDraft.businessPartnerId || 'none'} onValueChange={(value) => setPackageDraft((current) => ({ ...current, businessPartnerId: value === 'none' ? undefined : value }))}>
                    <SelectTrigger><SelectValue placeholder="Select partner" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No partner</SelectItem>
                      {activeBusinessPartners.map((partner) => <SelectItem key={partner.id} value={partner.id}>{partner.partnerName}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Contract</Label>
                  <Select value={packageDraft.contractId || 'none'} onValueChange={(value) => setPackageDraft((current) => ({ ...current, contractId: value === 'none' ? undefined : value }))}>
                    <SelectTrigger><SelectValue placeholder="Select contract" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No contract</SelectItem>
                      {activeContracts.map((contract) => <SelectItem key={contract.id} value={contract.id}>{contract.contractNumber} - {contract.contractTitle}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Tender</Label>
                  <Select value={packageDraft.tenderId || 'none'} onValueChange={(value) => setPackageDraft((current) => ({ ...current, tenderId: value === 'none' ? undefined : value }))}>
                    <SelectTrigger><SelectValue placeholder="Select tender" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No tender</SelectItem>
                      {tenders.map((tender) => <SelectItem key={tender.id} value={tender.id}>{tender.tenderNumber} - {tender.title}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Procurement Plan Item</Label>
                  <Select value={packageDraft.procurementPlanItemId || 'none'} onValueChange={(value) => setPackageDraft((current) => ({ ...current, procurementPlanItemId: value === 'none' ? undefined : value }))}>
                    <SelectTrigger><SelectValue placeholder="Select plan item" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No plan item</SelectItem>
                      {procurementPlanItems.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Purchase Requisition</Label>
                  <Select value={packageDraft.purchaseRequisitionId || 'none'} onValueChange={(value) => setPackageDraft((current) => ({ ...current, purchaseRequisitionId: value === 'none' ? undefined : value }))}>
                    <SelectTrigger><SelectValue placeholder="Select requisition" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No requisition</SelectItem>
                      {purchaseRequisitions.map((item) => <SelectItem key={item.id} value={item.id}>{item.requisitionNumber}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Purchase Order</Label>
                  <Select value={packageDraft.purchaseOrderId || 'none'} onValueChange={(value) => setPackageDraft((current) => ({ ...current, purchaseOrderId: value === 'none' ? undefined : value }))}>
                    <SelectTrigger><SelectValue placeholder="Select purchase order" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No purchase order</SelectItem>
                      {purchaseOrders.map((item) => <SelectItem key={item.id} value={item.id}>{item.orderNumber}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Budget</Label>
                  <Input type="number" value={packageDraft.budgetAmount ?? ''} onChange={(event) => setPackageDraft((current) => ({ ...current, budgetAmount: event.target.value ? Number(event.target.value) : undefined }))} />
                </div>
                <div className="grid gap-2">
                  <Label>Forecast</Label>
                  <Input type="number" value={packageDraft.forecastAmount ?? ''} onChange={(event) => setPackageDraft((current) => ({ ...current, forecastAmount: event.target.value ? Number(event.target.value) : undefined }))} />
                </div>
                <div className="grid gap-2 md:col-span-2">
                  <Label>Description</Label>
                  <Input value={packageDraft.description || ''} onChange={(event) => setPackageDraft((current) => ({ ...current, description: event.target.value || undefined }))} />
                </div>
              </div>
              <div className="flex justify-end gap-2">
                {editingPackageId ? (
                  <Button variant="outline" onClick={onCancelPackageEdit}>
                    <X className="mr-2 h-4 w-4" />
                    Cancel
                  </Button>
                ) : null}
                <Button disabled={!packageDraft.name?.trim()} onClick={onSavePackage}>
                  <Plus className="mr-2 h-4 w-4" />
                  {editingPackageId ? 'Save Package' : 'Add Package'}
                </Button>
              </div>
            </div>

            <div className="rounded-lg border p-4 space-y-4">
              <div className="font-medium">{editingBoqItemId ? 'Edit BOQ Item' : 'New BOQ Item'}</div>
              <div className="grid gap-4 md:grid-cols-2">
                <div className="grid gap-2">
                  <Label>Package</Label>
                  <Select value={boqDraft.projectPackageId || 'none'} onValueChange={(value) => setBoqDraft((current) => ({ ...current, projectPackageId: value === 'none' ? '' : value }))}>
                    <SelectTrigger><SelectValue placeholder="Select package" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">Select package</SelectItem>
                      {project.packages.map((item) => <SelectItem key={item.id} value={item.id}>{item.code ? `${item.code} - ${item.name}` : item.name}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Item Type</Label>
                  <Select value={boqDraft.itemType || boqItemTypeOptions[0]} onValueChange={(value) => setBoqDraft((current) => ({ ...current, itemType: value }))}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>{boqItemTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2 md:col-span-2">
                  <Label>Description</Label>
                  <Input value={boqDraft.description} onChange={(event) => setBoqDraft((current) => ({ ...current, description: event.target.value }))} />
                </div>
                <div className="grid gap-2">
                  <Label>Line Number</Label>
                  <Input value={boqDraft.lineNumber || ''} onChange={(event) => setBoqDraft((current) => ({ ...current, lineNumber: event.target.value || undefined }))} />
                </div>
                <div className="grid gap-2">
                  <Label>Item Code</Label>
                  <Input value={boqDraft.itemCode || ''} onChange={(event) => setBoqDraft((current) => ({ ...current, itemCode: event.target.value || undefined }))} />
                </div>
                <div className="grid gap-2">
                  <Label>Quantity</Label>
                  <Input type="number" value={boqDraft.quantity ?? ''} onChange={(event) => setBoqDraft((current) => ({ ...current, quantity: event.target.value ? Number(event.target.value) : undefined }))} />
                </div>
                <div className="grid gap-2">
                  <Label>Unit</Label>
                  <Input value={boqDraft.unitOfMeasure || ''} onChange={(event) => setBoqDraft((current) => ({ ...current, unitOfMeasure: event.target.value || undefined }))} />
                </div>
                <div className="grid gap-2">
                  <Label>Unit Rate</Label>
                  <Input type="number" value={boqDraft.unitRate ?? ''} onChange={(event) => setBoqDraft((current) => ({ ...current, unitRate: event.target.value ? Number(event.target.value) : undefined }))} />
                </div>
                <div className="grid gap-2">
                  <Label>Budget</Label>
                  <Input type="number" value={boqDraft.budgetAmount ?? ''} onChange={(event) => setBoqDraft((current) => ({ ...current, budgetAmount: event.target.value ? Number(event.target.value) : undefined }))} />
                </div>
                <div className="grid gap-2">
                  <Label>Currency</Label>
                  <Input value={boqDraft.currency || ''} onChange={(event) => setBoqDraft((current) => ({ ...current, currency: event.target.value || undefined }))} />
                </div>
                <div className="grid gap-2">
                  <Label>Procurement Plan Item</Label>
                  <Select value={boqDraft.procurementPlanItemId || 'none'} onValueChange={(value) => setBoqDraft((current) => ({ ...current, procurementPlanItemId: value === 'none' ? undefined : value }))}>
                    <SelectTrigger><SelectValue placeholder="Select plan item" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No plan item</SelectItem>
                      {procurementPlanItems.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2 md:col-span-2">
                  <Label>Notes</Label>
                  <Input value={boqDraft.notes || ''} onChange={(event) => setBoqDraft((current) => ({ ...current, notes: event.target.value || undefined }))} />
                </div>
              </div>
              <div className="flex items-center justify-between gap-3 text-sm text-muted-foreground">
                <span>{selectedPackageBoqItems.length} existing BOQ lines in the selected package</span>
                <div className="flex items-center gap-2">
                  {editingBoqItemId ? (
                    <Button variant="outline" onClick={onCancelBoqItemEdit}>
                      <X className="mr-2 h-4 w-4" />
                      Cancel
                    </Button>
                  ) : null}
                  <Button disabled={!boqDraft.projectPackageId || !boqDraft.description?.trim()} onClick={onSaveBoqItem}>
                  <Plus className="mr-2 h-4 w-4" />
                    {editingBoqItemId ? 'Save BOQ Item' : 'Add BOQ Item'}
                  </Button>
                </div>
              </div>
            </div>
          </div>

          <div className="space-y-3">
            {project.packages.length === 0 ? (
              <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
                No project packages have been created yet.
              </div>
            ) : null}
            {project.packages.map((item) => (
              <div key={item.id} className="rounded-lg border p-4 space-y-4">
                <div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
                  <div className="space-y-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <div className="font-medium">{item.code ? `${item.code} - ${item.name}` : item.name}</div>
                      <Badge variant="outline">{formatCatalogLabel(item.packageType)}</Badge>
                      <Badge>{formatCatalogLabel(item.status)}</Badge>
                      {item.projectPhaseName ? <Badge variant="secondary">{item.projectPhaseName}</Badge> : null}
                      <Badge variant="outline" className={getPhaseSyncTone(item.phaseCommercialSyncStatus)}>
                        {item.phaseCommercialSyncStatus}
                      </Badge>
                    </div>
                    {item.description ? <div className="text-sm text-muted-foreground">{item.description}</div> : null}
                    {item.phaseCommercialSyncMessage ? (
                      <div className={`rounded-md border px-3 py-2 text-sm ${getPhaseSyncTone(item.phaseCommercialSyncStatus)}`}>
                        <div>{item.phaseCommercialSyncMessage}</div>
                        {item.recommendedNextAction ? (
                          <div className="mt-2 flex flex-wrap items-center gap-2 text-xs font-medium">
                            <ArrowRightCircle className="h-3.5 w-3.5" />
                            <span>{item.recommendedNextAction}</span>
                            {item.recommendedNextStatus ? <Badge variant="outline">{formatCatalogLabel(item.recommendedNextStatus)}</Badge> : null}
                          </div>
                        ) : null}
                      </div>
                    ) : null}
                    <div className="grid gap-2 text-sm text-muted-foreground md:grid-cols-2 xl:grid-cols-4">
                      <div>Budget {formatMoney(item.budgetAmount, item.currency)}</div>
                      <div>Committed {formatMoney(item.committedAmount, item.currency)}</div>
                      <div>Actual {formatMoney(item.actualAmount, item.currency)}</div>
                      <div>Forecast {formatMoney(item.forecastAmount, item.currency)}</div>
                    </div>
                    <div className="flex flex-wrap gap-2 text-xs text-muted-foreground">
                      {item.businessPartnerName ? <span>{formatReference('Partner', item.businessPartnerName)}</span> : null}
                      {item.contractNumber ? <span>{formatReference('Contract', item.contractNumber, item.contractTitle)}</span> : null}
                      {item.tenderNumber ? <span>{formatReference('Tender', item.tenderNumber, item.tenderTitle)}</span> : null}
                      {item.procurementPlanItemLabel ? <span>{formatReference('Plan', item.procurementPlanItemLabel)}</span> : null}
                      {item.purchaseRequisitionNumber ? <span>{formatReference('PR', item.purchaseRequisitionNumber)}</span> : null}
                      {item.purchaseOrderNumber ? <span>{formatReference('PO', item.purchaseOrderNumber)}</span> : null}
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    <Button variant="outline" size="sm" onClick={() => onEditPackage(item)}>
                      <Pencil className="mr-2 h-4 w-4" />
                      Edit
                    </Button>
                    <Button variant="ghost" size="sm" onClick={() => onDeletePackage(item.id)}>
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </div>

                <div className="space-y-2">
                  <div className="text-sm font-medium">BOQ Lines</div>
                  {item.boqItems.length === 0 ? (
                    <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
                      No BOQ lines have been added to this package yet.
                    </div>
                  ) : (
                    item.boqItems.map((boqItem: ProjectBoqItemDto) => (
                      <div key={boqItem.id} className="flex flex-col gap-3 rounded-md border p-3 xl:flex-row xl:items-start xl:justify-between">
                        <div className="space-y-1">
                          <div className="font-medium">
                            {[boqItem.lineNumber, boqItem.itemCode, boqItem.description].filter(Boolean).join(' | ')}
                          </div>
                          <div className="text-sm text-muted-foreground">
                            {formatCatalogLabel(boqItem.itemType)} | Qty {boqItem.quantity} {boqItem.unitOfMeasure || ''} | Rate {formatMoney(boqItem.unitRate, boqItem.currency, 2)}
                          </div>
                          <div className="text-sm text-muted-foreground">
                            Budget {formatMoney(boqItem.budgetAmount, boqItem.currency)} | Actual {formatMoney(boqItem.actualAmount, boqItem.currency)} | Forecast {formatMoney(boqItem.forecastAmount, boqItem.currency)}
                          </div>
                          {boqItem.notes ? <div className="text-sm text-muted-foreground">{boqItem.notes}</div> : null}
                        </div>
                        <div className="flex items-center gap-2">
                          <Button variant="outline" size="sm" onClick={() => onEditBoqItem(boqItem)}>
                            <Pencil className="mr-2 h-4 w-4" />
                            Edit
                          </Button>
                          <Button variant="ghost" size="sm" onClick={() => onDeleteBoqItem(boqItem.id)}>
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
