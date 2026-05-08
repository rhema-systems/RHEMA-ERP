import { type Dispatch, type SetStateAction, useMemo, useState } from 'react';
import { AlertTriangle, ArrowRightCircle, Pencil, Plus, Trash2 } from 'lucide-react';
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from '@/components/ui/accordion';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ProjectPackageDialogs } from '@/components/projects/ProjectPackageDialogs';
import type { BusinessPartnerDto } from '@/services/businessPartnerService';
import type { ContractDto } from '@/services/contractService';
import type { InventoryItemDto, UnitOfMeasureDto } from '@/services/inventoryManagementService';
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
  inventoryItems: InventoryItemDto[];
  packageTypeOptions: string[];
  packageStatusOptions: string[];
  boqItemTypeOptions: string[];
  unitOfMeasures: UnitOfMeasureDto[];
  packageCurrencyOptions: string[];
  boqCurrencyOptions: string[];
  getCurrencyOptionLabel: (code: string) => string;
  formatMoney: (value: number | undefined, currency?: string | null, maximumFractionDigits?: number) => string;
  formatCatalogLabel: (value?: string | null) => string;
  onSavePackage: () => Promise<boolean> | boolean;
  onEditPackage: (projectPackage: ProjectPackageDto) => void;
  onCancelPackageEdit: () => void;
  onDeletePackage: (packageId: string) => void;
  onSaveBoqItem: () => Promise<boolean> | boolean;
  onEditBoqItem: (boqItem: ProjectBoqItemDto) => void;
  onCancelBoqItemEdit: () => void;
  onDeleteBoqItem: (boqItemId: string) => void;
};

type PhaseOption = Pick<ProjectPhaseDto, 'id' | 'name' | 'plannedStartDate' | 'plannedEndDate' | 'completionWeightPercent'> & {
  label: string;
  depth: number;
};

const flattenPhases = (phases: ProjectPhaseDto[], depth = 0): PhaseOption[] =>
  phases.flatMap((phase) => [
    {
      id: phase.id,
      label: `${' '.repeat(depth * 2)}${phase.name}`.trimStart(),
      depth,
      name: phase.name,
      plannedStartDate: phase.plannedStartDate,
      plannedEndDate: phase.plannedEndDate,
      completionWeightPercent: phase.completionWeightPercent,
    },
    ...flattenPhases(phase.children || [], depth + 1),
  ]);

const formatReference = (...parts: Array<string | undefined>) => parts.filter(Boolean).join(' - ');
const formatWeight = (value?: number) => `${Number(value ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 })}%`;
const formatDateWindow = (startDate?: string, endDate?: string) => {
  if (!startDate && !endDate) {
    return 'Not set';
  }

  return `${startDate ? startDate.slice(0, 10) : 'Open'} - ${endDate ? endDate.slice(0, 10) : 'Open'}`;
};

const sortPackages = (items: ProjectPackageDto[]) => [...items].sort((left, right) =>
  (left.plannedStartDate || left.plannedEndDate || '').localeCompare(right.plannedStartDate || right.plannedEndDate || '')
  || (left.sortOrder ?? 0) - (right.sortOrder ?? 0)
  || (left.code || left.name).localeCompare(right.code || right.name),
);

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
  inventoryItems,
  packageTypeOptions,
  packageStatusOptions,
  boqItemTypeOptions,
  unitOfMeasures,
  packageCurrencyOptions,
  boqCurrencyOptions,
  getCurrencyOptionLabel,
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
  const [isAddPackageDialogOpen, setIsAddPackageDialogOpen] = useState(false);
  const [isAddBoqDialogOpen, setIsAddBoqDialogOpen] = useState(false);
  const [expandedPhaseIds, setExpandedPhaseIds] = useState<string[]>([]);
  const [expandedPackageIds, setExpandedPackageIds] = useState<string[]>([]);
  const phaseOptions = useMemo(() => flattenPhases(phases), [phases]);
  const totalBoqLines = useMemo(() => project.boqItems.length, [project.boqItems]);
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
  const packagesByPhase = useMemo(() => {
    const grouped = new Map<string, ProjectPackageDto[]>();
    phaseOptions.forEach((phase) => grouped.set(phase.id, []));

    const unassigned: ProjectPackageDto[] = [];
    project.packages.forEach((item) => {
      if (item.projectPhaseId && grouped.has(item.projectPhaseId)) {
        grouped.get(item.projectPhaseId)?.push(item);
        return;
      }
      unassigned.push(item);
    });

    return {
      groupedPhases: phaseOptions
        .map((phase) => ({
          phase,
          packages: sortPackages(grouped.get(phase.id) ?? []),
        }))
        .filter((entry) => entry.packages.length > 0),
      unassignedPackages: sortPackages(unassigned),
    };
  }, [phaseOptions, project.packages]);
  const phasesWithWorkComponentsCount = useMemo(
    () => packagesByPhase.groupedPhases.length,
    [packagesByPhase.groupedPhases],
  );
  const weightReadyPhaseCount = useMemo(
    () => packagesByPhase.groupedPhases.filter(({ packages }) => {
      const totalWeight = packages.reduce((sum, item) => sum + (item.completionWeightPercent ?? 0), 0);
      return Math.abs(totalWeight - 100) < 0.01;
    }).length,
    [packagesByPhase.groupedPhases],
  );

  const openAddPackageDialogForPhase = (projectPhaseId?: string) => {
    onCancelPackageEdit();
    setPackageDraft((current) => ({ ...current, projectPhaseId }));
    setIsAddPackageDialogOpen(true);
  };

  const openAddBoqDialogForPackage = (packageId: string) => {
    onCancelBoqItemEdit();
    setBoqDraft((current) => ({ ...current, projectPackageId: packageId }));
    setIsAddBoqDialogOpen(true);
  };

  const renderPackageAccordionItem = (item: ProjectPackageDto) => (
    <AccordionItem key={item.id} value={item.id} className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
      <div className="flex flex-col gap-3 p-4 xl:flex-row xl:items-start xl:justify-between">
        <AccordionTrigger className="flex-1 py-0 text-left hover:no-underline">
          <div className="min-w-0 flex-1 space-y-4">
            <div className="space-y-2">
              <div className="flex flex-wrap items-center gap-2">
                <div className="text-base font-semibold text-slate-900">{item.code ? `${item.code} - ${item.name}` : item.name}</div>
                <Badge variant="outline">{formatCatalogLabel(item.packageType)}</Badge>
                <Badge>{formatCatalogLabel(item.status)}</Badge>
                {item.projectPhaseName ? <Badge variant="secondary">{item.projectPhaseName}</Badge> : null}
                <Badge variant="outline" className="border-blue-200 bg-blue-50 text-blue-700">
                  Weight {formatWeight(item.completionWeightPercent)}
                </Badge>
                <Badge variant="outline" className={getPhaseSyncTone(item.phaseCommercialSyncStatus)}>
                  {item.phaseCommercialSyncStatus}
                </Badge>
                <Badge variant="outline">{item.boqItems.length} BOQ</Badge>
              </div>
              {item.description ? <div className="text-sm leading-6 text-muted-foreground">{item.description}</div> : null}
              <div className="flex flex-wrap gap-2 text-xs text-slate-500">
                <span className="rounded-full border bg-slate-50 px-3 py-1">Window {formatDateWindow(item.plannedStartDate, item.plannedEndDate)}</span>
                <span className="rounded-full border bg-slate-50 px-3 py-1">Weight {formatWeight(item.completionWeightPercent)}</span>
              </div>
              <div className="text-xs font-medium text-slate-500">Open this work component to see its BOQ lines and cost detail.</div>
            </div>

            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <div className="rounded-lg border bg-slate-50/80 px-3 py-2">
                <div className="text-[11px] font-semibold uppercase tracking-[0.16em] text-slate-500">Budget</div>
                <div className="mt-1 text-sm font-semibold text-slate-900">{formatMoney(item.budgetAmount, item.currency)}</div>
              </div>
              <div className="rounded-lg border bg-slate-50/80 px-3 py-2">
                <div className="text-[11px] font-semibold uppercase tracking-[0.16em] text-slate-500">Committed</div>
                <div className="mt-1 text-sm font-semibold text-slate-900">{formatMoney(item.committedAmount, item.currency)}</div>
              </div>
              <div className="rounded-lg border bg-slate-50/80 px-3 py-2">
                <div className="text-[11px] font-semibold uppercase tracking-[0.16em] text-slate-500">Actual</div>
                <div className="mt-1 text-sm font-semibold text-slate-900">{formatMoney(item.actualAmount, item.currency)}</div>
              </div>
              <div className="rounded-lg border bg-slate-50/80 px-3 py-2">
                <div className="text-[11px] font-semibold uppercase tracking-[0.16em] text-slate-500">Latest Forecast</div>
                <div className="mt-1 text-sm font-semibold text-slate-900">{formatMoney(item.forecastAmount, item.currency)}</div>
              </div>
            </div>

            {(item.businessPartnerName ||
              item.contractNumber ||
              item.tenderNumber ||
              item.procurementPlanItemLabel ||
              item.purchaseRequisitionNumber ||
              item.purchaseOrderNumber) ? (
              <div className="flex flex-wrap gap-2">
                {item.businessPartnerName ? (
                  <span className="rounded-full border bg-slate-50 px-3 py-1 text-xs text-slate-600">{formatReference('Partner', item.businessPartnerName)}</span>
                ) : null}
                {item.contractNumber ? (
                  <span className="rounded-full border bg-slate-50 px-3 py-1 text-xs text-slate-600">
                    {formatReference('Contract', item.contractNumber, item.contractTitle)}
                  </span>
                ) : null}
                {item.tenderNumber ? (
                  <span className="rounded-full border bg-slate-50 px-3 py-1 text-xs text-slate-600">
                    {formatReference('Tender', item.tenderNumber, item.tenderTitle)}
                  </span>
                ) : null}
                {item.procurementPlanItemLabel ? (
                  <span className="rounded-full border bg-slate-50 px-3 py-1 text-xs text-slate-600">
                    {formatReference('Plan', item.procurementPlanItemLabel)}
                  </span>
                ) : null}
                {item.purchaseRequisitionNumber ? (
                  <span className="rounded-full border bg-slate-50 px-3 py-1 text-xs text-slate-600">
                    {formatReference('PR', item.purchaseRequisitionNumber)}
                  </span>
                ) : null}
                {item.purchaseOrderNumber ? (
                  <span className="rounded-full border bg-slate-50 px-3 py-1 text-xs text-slate-600">
                    {formatReference('PO', item.purchaseOrderNumber)}
                  </span>
                ) : null}
              </div>
            ) : null}

            {item.phaseCommercialSyncMessage ? (
              <div className={`rounded-lg border px-3 py-3 text-sm ${getPhaseSyncTone(item.phaseCommercialSyncStatus)}`}>
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
          </div>
        </AccordionTrigger>

        <div className="flex shrink-0 flex-wrap items-center gap-2 xl:justify-end">
          <Button variant="outline" size="sm" className="gap-2" onClick={() => openAddBoqDialogForPackage(item.id)}>
            <Plus className="h-4 w-4" />
            Add BOQ
          </Button>
          <Button variant="outline" size="sm" className="gap-2" onClick={() => onEditPackage(item)}>
            <Pencil className="h-4 w-4" />
            Edit
          </Button>
          <Button variant="ghost" size="sm" className="h-9 w-9 p-0" onClick={() => onDeletePackage(item.id)}>
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      </div>

      <AccordionContent className="border-t bg-slate-50/60 px-4 pb-4 pt-4 data-[state=open]:max-h-[9999px]">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
          <div>
            <div className="text-sm font-semibold text-slate-900">BOQ Lines</div>
            <div className="text-xs text-slate-500">{item.boqItems.length} line(s) linked to this work component</div>
          </div>
          <Button variant="outline" size="sm" className="gap-2" onClick={() => openAddBoqDialogForPackage(item.id)}>
            <Plus className="h-4 w-4" />
            Add BOQ Line
          </Button>
        </div>
        {item.boqItems.length === 0 ? (
          <div className="rounded-lg border border-dashed bg-white p-5 text-sm text-muted-foreground">
            No BOQ lines have been added to this work component yet.
          </div>
        ) : (
          <div className="grid gap-3 xl:grid-cols-2">
            {item.boqItems.map((boqItem: ProjectBoqItemDto) => {
              const boqVariance = (boqItem.budgetAmount ?? 0) - (boqItem.forecastAmount ?? 0);

              return (
                <div key={boqItem.id} className="rounded-xl border bg-white p-4 shadow-sm">
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                    <div className="min-w-0 space-y-2">
                      <div className="flex flex-wrap items-center gap-2">
                        <div className="font-medium text-slate-900">
                          {[boqItem.lineNumber, boqItem.itemCode, boqItem.description].filter(Boolean).join(' | ')}
                        </div>
                        <Badge variant="outline">{formatCatalogLabel(boqItem.itemType)}</Badge>
                      </div>
                      <div className="text-sm text-muted-foreground">
                        Qty {boqItem.quantity} {boqItem.unitOfMeasure || ''} at {formatMoney(boqItem.unitRate, boqItem.currency, 2)}
                      </div>
                      <div className="grid gap-2 text-sm text-muted-foreground sm:grid-cols-4">
                        <div>Budget {formatMoney(boqItem.budgetAmount, boqItem.currency)}</div>
                        <div>Forecast {formatMoney(boqItem.forecastAmount, boqItem.currency)}</div>
                        <div>Actual {formatMoney(boqItem.actualAmount, boqItem.currency)}</div>
                        <div>Variance {formatMoney(boqVariance, boqItem.currency)}</div>
                      </div>
                      {boqItem.notes ? <div className="text-sm text-muted-foreground">{boqItem.notes}</div> : null}
                    </div>
                    <div className="flex shrink-0 items-center gap-2">
                      <Button variant="outline" size="sm" className="gap-2" onClick={() => onEditBoqItem(boqItem)}>
                        <Pencil className="h-4 w-4" />
                        Edit
                      </Button>
                      <Button variant="ghost" size="sm" className="h-9 w-9 p-0" onClick={() => onDeleteBoqItem(boqItem.id)}>
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </AccordionContent>
    </AccordionItem>
  );

  return (
    <div className="space-y-6">
      <Card className="border-slate-200/80 shadow-sm">
        <CardHeader className="space-y-4">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div className="space-y-1">
              <CardTitle>Work Components & BOQ</CardTitle>
              <p className="text-sm text-muted-foreground">
                Track work components, linked procurement records, and BOQ lines without crowding the page.
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <Button
                variant="outline"
                className="gap-2"
                onClick={() => {
                  onCancelBoqItemEdit();
                  setIsAddBoqDialogOpen(true);
                }}
              >
                <Plus className="h-4 w-4" />
                Add BOQ
              </Button>
              <Button
                className="gap-2"
                onClick={() => {
                  onCancelPackageEdit();
                  setIsAddPackageDialogOpen(true);
                }}
              >
                <Plus className="h-4 w-4" />
                Add Work Component
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
            <div className="rounded-xl border bg-slate-50/70 p-4">
              <div className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Work Components</div>
              <div className="mt-2 text-2xl font-semibold text-slate-900">{project.packages.length}</div>
              <div className="mt-1 text-xs text-slate-500">Scoped construction work areas</div>
            </div>
            <div className="rounded-xl border bg-slate-50/70 p-4">
              <div className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">BOQ Lines</div>
              <div className="mt-2 text-2xl font-semibold text-slate-900">{totalBoqLines}</div>
              <div className="mt-1 text-xs text-slate-500">Measured items across work components</div>
            </div>
            <div className="rounded-xl border bg-slate-50/70 p-4">
              <div className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Phase Coverage</div>
              <div className="mt-2 text-2xl font-semibold text-slate-900">{phasesWithWorkComponentsCount}</div>
              <div className="mt-1 text-xs text-slate-500">{unassignedPackageCount} unassigned work component(s)</div>
            </div>
            <div className="rounded-xl border bg-slate-50/70 p-4">
              <div className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Weight Readiness</div>
              <div className="mt-2 text-2xl font-semibold text-slate-900">{weightReadyPhaseCount}</div>
              <div className="mt-1 text-xs text-slate-500">Phase(s) with work component weights summing to 100%</div>
            </div>
            <div className={`rounded-xl border p-4 ${laggingPackageCount > 0 ? 'border-amber-200 bg-amber-50/70' : 'bg-emerald-50/70 border-emerald-200'}`}>
              <div className={`text-xs font-semibold uppercase tracking-[0.16em] ${laggingPackageCount > 0 ? 'text-amber-700' : 'text-emerald-700'}`}>
                Sync Posture
              </div>
              <div className={`mt-2 text-2xl font-semibold ${laggingPackageCount > 0 ? 'text-amber-900' : 'text-emerald-900'}`}>
                {alignedPackageCount} / {project.packages.length}
              </div>
              <div className={`mt-1 text-xs ${laggingPackageCount > 0 ? 'text-amber-700' : 'text-emerald-700'}`}>
                {laggingPackageCount > 0 ? `${laggingPackageCount} lagging, ${unassignedPackageCount} unassigned` : 'All work components are aligned or ready'}
              </div>
            </div>
          </div>

          {laggingPackageCount > 0 ? (
            <div className="flex items-start gap-3 rounded-xl border border-amber-200 bg-amber-50/70 p-4 text-sm text-amber-900">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
              <div>
                {laggingPackageCount} work component(s) are behind their linked phase. Review the sync badges below and complete the recommended procurement or commercial step.
              </div>
            </div>
          ) : null}

          <div className="space-y-3">
            {project.packages.length === 0 ? (
              <div className="rounded-xl border border-dashed p-8 text-sm text-muted-foreground">
                No work components have been created yet. Start with a major trade or construction work component, then attach its BOQ lines from the same tab.
              </div>
            ) : null}
            {packagesByPhase.groupedPhases.length > 0 ? (
              <Accordion type="multiple" value={expandedPhaseIds} onValueChange={setExpandedPhaseIds} className="space-y-3">
                {packagesByPhase.groupedPhases.map(({ phase, packages }) => {
                  const phaseBoqCount = packages.reduce((sum, item) => sum + item.boqItems.length, 0);
                  const phaseLaggingCount = packages.filter((item) => item.phaseCommercialSyncStatus === 'Lagging').length;
                  const phasePackageWeightTotal = packages.reduce((sum, item) => sum + (item.completionWeightPercent ?? 0), 0);
                  return (
                    <AccordionItem key={phase.id} value={phase.id} className="overflow-hidden rounded-xl border border-slate-300 bg-slate-50/60 shadow-sm">
                      <div className="flex flex-col gap-3 p-4 xl:flex-row xl:items-start xl:justify-between">
                        <AccordionTrigger className="flex-1 py-0 text-left hover:no-underline">
                          <div className="min-w-0 flex-1 space-y-3">
                            <div className="flex flex-wrap items-center gap-2">
                              <div className="text-base font-semibold text-slate-900" style={{ paddingLeft: `${phase.depth * 0.75}rem` }}>
                                {phase.name}
                              </div>
                              <Badge variant="outline">{packages.length} Work Components</Badge>
                              <Badge variant="outline">{phaseBoqCount} BOQ</Badge>
                              <Badge variant="outline" className="border-blue-200 bg-blue-50 text-blue-700">
                                Phase Weight {formatWeight(phase.completionWeightPercent)}
                              </Badge>
                              <Badge
                                variant="outline"
                                className={phasePackageWeightTotal > 100
                                  ? 'border-rose-200 bg-rose-50 text-rose-700'
                                  : phasePackageWeightTotal === 100
                                    ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
                                    : 'border-amber-200 bg-amber-50 text-amber-700'}
                              >
                                Component Weight {formatWeight(phasePackageWeightTotal)}
                              </Badge>
                              {phaseLaggingCount > 0 ? <Badge className="bg-amber-100 text-amber-800 hover:bg-amber-100">{phaseLaggingCount} Lagging</Badge> : null}
                            </div>
                            <div className="text-sm text-slate-500">
                              Window {formatDateWindow(phase.plannedStartDate, phase.plannedEndDate)}. Open this phase to review its work components and then drill into each one for BOQ detail.
                            </div>
                          </div>
                        </AccordionTrigger>

                        <div className="flex shrink-0 items-center gap-2">
                          <Button variant="outline" size="sm" className="gap-2" onClick={() => openAddPackageDialogForPhase(phase.id)}>
                            <Plus className="h-4 w-4" />
                            Add Work Component
                          </Button>
                        </div>
                      </div>

                      <AccordionContent className="border-t bg-white px-4 pb-4 pt-4 data-[state=open]:max-h-[9999px]">
                        <Accordion type="multiple" value={expandedPackageIds} onValueChange={setExpandedPackageIds} className="space-y-3">
                          {packages.map((item) => renderPackageAccordionItem(item))}
                        </Accordion>
                      </AccordionContent>
                    </AccordionItem>
                  );
                })}
              </Accordion>
            ) : null}

            {packagesByPhase.unassignedPackages.length > 0 ? (
              <div className="space-y-3 rounded-xl border border-dashed border-slate-300 bg-white/80 p-4">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div className="space-y-1">
                    <div className="text-sm font-semibold text-slate-900">No Phase Assigned</div>
                    <div className="text-sm text-slate-500">
                      These work components will not follow phase-based workflow nudges until they are linked to a phase.
                    </div>
                  </div>
                  <Button variant="outline" size="sm" className="gap-2" onClick={() => openAddPackageDialogForPhase(undefined)}>
                    <Plus className="h-4 w-4" />
                    Add Unassigned Work Component
                  </Button>
                </div>
                <Accordion type="multiple" value={expandedPackageIds} onValueChange={setExpandedPackageIds} className="space-y-3">
                  {packagesByPhase.unassignedPackages.map((item) => renderPackageAccordionItem(item))}
                </Accordion>
              </div>
            ) : null}
          </div>
        </CardContent>
      </Card>

      <ProjectPackageDialogs
        project={project}
        phaseOptions={phaseOptions}
        packageDraft={packageDraft}
        setPackageDraft={setPackageDraft}
        boqDraft={boqDraft}
        setBoqDraft={setBoqDraft}
        activeBusinessPartners={activeBusinessPartners}
        activeContracts={activeContracts}
        tenders={tenders}
        procurementPlanItems={procurementPlanItems}
        purchaseRequisitions={purchaseRequisitions}
        purchaseOrders={purchaseOrders}
        inventoryItems={inventoryItems}
        packageTypeOptions={packageTypeOptions}
        packageStatusOptions={packageStatusOptions}
        boqItemTypeOptions={boqItemTypeOptions}
        unitOfMeasures={unitOfMeasures}
        packageCurrencyOptions={packageCurrencyOptions}
        boqCurrencyOptions={boqCurrencyOptions}
        getCurrencyOptionLabel={getCurrencyOptionLabel}
        formatCatalogLabel={formatCatalogLabel}
        selectedPackageBoqItemsCount={selectedPackageBoqItems.length}
        isAddPackageDialogOpen={isAddPackageDialogOpen}
        setIsAddPackageDialogOpen={setIsAddPackageDialogOpen}
        isAddBoqDialogOpen={isAddBoqDialogOpen}
        setIsAddBoqDialogOpen={setIsAddBoqDialogOpen}
        editingPackageId={editingPackageId}
        editingBoqItemId={editingBoqItemId}
        onCancelPackageEdit={onCancelPackageEdit}
        onCancelBoqItemEdit={onCancelBoqItemEdit}
        onSavePackage={onSavePackage}
        onSaveBoqItem={onSaveBoqItem}
      />
    </div>
  );
}
