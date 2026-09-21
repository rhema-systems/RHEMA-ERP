import * as XLSX from 'xlsx';
import { useEffect, useMemo, useState, type Dispatch, type SetStateAction } from 'react';
import { Download, Save } from 'lucide-react';
import type { BusinessPartnerDto } from '@/services/businessPartnerService';
import type { ProjectContractLookupDto } from '@/services/projectService';
import type { InventoryItemDto, UnitOfMeasureDto } from '@/services/inventoryManagementService';
import { ProjectPackageDialogs } from '@/components/projects/ProjectPackageDialogs';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import type {
  CreateProjectBoqItemDto,
  CreateProjectPackageDto,
  ProjectBoqItemDto,
  ProjectCommercialSummaryDto,
  ProjectDetailDto,
  ProjectPackageDto,
  ProjectProcurementPlanItemLookupDto,
  ProjectPurchaseOrderLookupDto,
  ProjectPurchaseRequisitionLookupDto,
  ProjectTenderLookupDto,
} from '@/services/projectService';

export type ProjectBoqBudgetWorksheetUpdate = {
  boqItemId: string;
  budgetQuantity?: number;
  budgetUnitRate?: number;
  budgetAmount?: number;
};

type ProjectBudgetingTabProps = {
  project: ProjectDetailDto;
  commercialSummary: ProjectCommercialSummaryDto | null;
  activeBusinessPartners: BusinessPartnerDto[];
  activeContracts: ProjectContractLookupDto[];
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
  formatCatalogLabel: (value?: string | null) => string;
  formatMoney: (value: number | undefined, currency?: string | null, maximumFractionDigits?: number) => string;
  onSaveBudgetWorksheet: (updates: ProjectBoqBudgetWorksheetUpdate[]) => Promise<boolean> | boolean;
};

type BudgetRowDraft = {
  budgetQuantity?: number;
  budgetUnitRate?: number;
};

const roundMoney = (value: number) => Math.round((value + Number.EPSILON) * 100) / 100;
const roundQuantity = (value: number) => Math.round((value + Number.EPSILON) * 10000) / 10000;
const approximatelyEqual = (left?: number, right?: number, precision: number = 0.0001) =>
  Math.abs((left ?? 0) - (right ?? 0)) < precision;

const normalizeNumberInput = (value: string): number | undefined => {
  if (!value.trim()) {
    return undefined;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : undefined;
};

const compareText = (left?: string | null, right?: string | null) => (left || '').localeCompare(right || '');

const flattenPhasesInOrder = (phases: ProjectDetailDto['phases']): ProjectDetailDto['phases'] =>
  [...phases]
    .sort((left, right) =>
      left.sortOrder - right.sortOrder
      || compareText(left.code, right.code)
      || compareText(left.name, right.name),
    )
    .flatMap((phase) => [phase, ...flattenPhasesInOrder(phase.children || [])]);

const sortBoqItems = (items: ProjectBoqItemDto[], packages: ProjectPackageDto[], phases: ProjectDetailDto['phases']) => {
  const phaseOrderLookup = new Map(flattenPhasesInOrder(phases).map((phase, index) => [phase.id, index]));
  const orderedPackages = [...packages].sort((left, right) =>
    (phaseOrderLookup.get(left.projectPhaseId || '') ?? Number.MAX_SAFE_INTEGER)
      - (phaseOrderLookup.get(right.projectPhaseId || '') ?? Number.MAX_SAFE_INTEGER)
    || left.sortOrder - right.sortOrder
    || compareText(left.code, right.code)
    || compareText(left.name, right.name),
  );
  const packageOrderLookup = new Map(orderedPackages.map((projectPackage, index) => [projectPackage.id, index]));

  return [...items].sort((left, right) =>
    (packageOrderLookup.get(left.projectPackageId) ?? Number.MAX_SAFE_INTEGER)
      - (packageOrderLookup.get(right.projectPackageId) ?? Number.MAX_SAFE_INTEGER)
    || left.sortOrder - right.sortOrder
    || compareText(left.lineNumber, right.lineNumber)
    || compareText(left.itemCode, right.itemCode)
    || compareText(left.description, right.description),
  );
};

const resolveSavedBudgetQuantity = (item: ProjectBoqItemDto) => item.budgetQuantity ?? item.quantity;

const resolveSavedBudgetUnitRate = (item: ProjectBoqItemDto) => {
  if (typeof item.budgetUnitRate === 'number') {
    return item.budgetUnitRate;
  }

  const savedBudgetQuantity = resolveSavedBudgetQuantity(item);
  if (typeof item.budgetAmount === 'number' && savedBudgetQuantity) {
    return roundQuantity(item.budgetAmount / savedBudgetQuantity);
  }

  return item.unitRate ?? 0;
};

const resolveForecastReferenceTotal = (item: ProjectBoqItemDto) =>
  typeof item.forecastAmount === 'number'
    ? roundMoney(item.forecastAmount)
    : roundMoney((item.quantity || 0) * (item.unitRate || 0));

const flattenPhases = (
  phases: ProjectDetailDto['phases'],
  depth = 0,
): Array<{ id: string; label: string; depth: number; name: string; plannedStartDate?: string; plannedEndDate?: string; completionWeightPercent: number }> =>
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

const mapPackageToDraft = (projectPackage: ProjectPackageDto, baseCurrencyCode?: string): CreateProjectPackageDto => ({
  projectPhaseId: projectPackage.projectPhaseId || undefined,
  code: projectPackage.code || undefined,
  name: projectPackage.name,
  description: projectPackage.description || undefined,
  packageType: projectPackage.packageType,
  status: projectPackage.status,
  sortOrder: projectPackage.sortOrder,
  completionWeightPercent: projectPackage.completionWeightPercent ?? 0,
  plannedStartDate: projectPackage.plannedStartDate || undefined,
  plannedEndDate: projectPackage.plannedEndDate || undefined,
  procurementRoute: projectPackage.procurementRoute || undefined,
  contractStrategy: projectPackage.contractStrategy || undefined,
  businessPartnerId: projectPackage.businessPartnerId || undefined,
  tenderId: projectPackage.tenderId || undefined,
  contractId: projectPackage.contractId || undefined,
  procurementPlanItemId: projectPackage.procurementPlanItemId || undefined,
  purchaseRequisitionId: projectPackage.purchaseRequisitionId || undefined,
  purchaseOrderId: projectPackage.purchaseOrderId || undefined,
  budgetAmount: projectPackage.budgetAmount ?? undefined,
  committedAmount: projectPackage.committedAmount ?? undefined,
  actualAmount: projectPackage.actualAmount ?? undefined,
  forecastAmount: projectPackage.forecastAmount ?? undefined,
  currency: projectPackage.currency || baseCurrencyCode,
  notes: projectPackage.notes || undefined,
});

const mapBoqItemToDraft = (boqItem: ProjectBoqItemDto, baseCurrencyCode?: string): CreateProjectBoqItemDto => ({
  projectPackageId: boqItem.projectPackageId,
  sectionCatalogEntryId: boqItem.sectionCatalogEntryId || undefined,
  tradeCatalogEntryId: boqItem.tradeCatalogEntryId || undefined,
  costCodeCatalogEntryId: boqItem.costCodeCatalogEntryId || undefined,
  measurementCodeCatalogEntryId: boqItem.measurementCodeCatalogEntryId || undefined,
  lineNumber: boqItem.lineNumber || undefined,
  itemCode: boqItem.itemCode || undefined,
  itemType: boqItem.itemType,
  description: boqItem.description,
  quantity: boqItem.quantity ?? undefined,
  unitOfMeasure: boqItem.unitOfMeasure || undefined,
  unitRate: boqItem.unitRate ?? undefined,
  budgetQuantity: boqItem.budgetQuantity ?? undefined,
  budgetUnitRate: boqItem.budgetUnitRate ?? undefined,
  budgetAmount: boqItem.budgetAmount ?? undefined,
  committedAmount: boqItem.committedAmount ?? undefined,
  actualAmount: boqItem.actualAmount ?? undefined,
  forecastAmount: boqItem.forecastAmount ?? undefined,
  currency: boqItem.currency || baseCurrencyCode,
  inventoryItemId: boqItem.inventoryItemId || undefined,
  tenderItemId: boqItem.tenderItemId || undefined,
  procurementPlanItemId: boqItem.procurementPlanItemId || undefined,
  purchaseRequisitionItemId: boqItem.purchaseRequisitionItemId || undefined,
  purchaseOrderItemId: boqItem.purchaseOrderItemId || undefined,
  notes: boqItem.notes || undefined,
  sortOrder: boqItem.sortOrder,
});

const createEmptyPackageDraft = (baseCurrencyCode?: string): CreateProjectPackageDto => ({
  name: '',
  currency: baseCurrencyCode,
});

const createEmptyBoqDraft = (project: ProjectDetailDto): CreateProjectBoqItemDto => ({
  projectPackageId: project.packages[0]?.id || '',
  description: '',
  currency: project.baseCurrencyCode,
});

const noopSetBoolean: Dispatch<SetStateAction<boolean>> = () => undefined;
const noopSetPackageDraft: Dispatch<SetStateAction<CreateProjectPackageDto>> = () => undefined;
const noopSetBoqDraft: Dispatch<SetStateAction<CreateProjectBoqItemDto>> = () => undefined;
const noopSave = () => false;

const readOnlyHeaderClass = 'bg-slate-200/85 text-slate-700';
const editableHeaderClass = 'bg-white text-slate-700';

const getReadOnlyCellClass = (index: number, isDirty: boolean) =>
  isDirty
    ? 'bg-slate-200/85'
    : index % 2 === 0
      ? 'bg-slate-100/85'
      : 'bg-slate-200/65';

const getEditableCellClass = (isDirty: boolean) =>
  isDirty ? 'bg-amber-50/85' : 'bg-white';

export function ProjectBudgetingTab({
  project,
  commercialSummary,
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
  formatCatalogLabel,
  formatMoney,
  onSaveBudgetWorksheet,
}: ProjectBudgetingTabProps) {
  const [drafts, setDrafts] = useState<Record<string, BudgetRowDraft>>({});
  const [saving, setSaving] = useState(false);
  const [selectedPackageId, setSelectedPackageId] = useState<string | null>(null);
  const [selectedBoqItemId, setSelectedBoqItemId] = useState<string | null>(null);

  useEffect(() => {
    setDrafts({});
    setSelectedPackageId(null);
    setSelectedBoqItemId(null);
  }, [project.id, project.boqItems]);

  const boqItems = useMemo(() => sortBoqItems(project.boqItems, project.packages, project.phases), [project.boqItems, project.packages, project.phases]);
  const phaseOptions = useMemo(() => flattenPhases(project.phases), [project.phases]);
  const phaseLookup = useMemo(
    () => new Map(flattenPhasesInOrder(project.phases).map((phase) => [phase.id, phase.code ? `${phase.code} - ${phase.name}` : phase.name])),
    [project.phases],
  );
  const packageLookup = useMemo(
    () => new Map<string, ProjectPackageDto>(project.packages.map((item) => [item.id, item])),
    [project.packages],
  );
  const selectedPackage = selectedPackageId ? packageLookup.get(selectedPackageId) ?? null : null;
  const selectedBoqItem = selectedBoqItemId ? boqItems.find((item) => item.id === selectedBoqItemId) ?? null : null;
  const readonlyPackageDraft = useMemo(
    () => selectedPackage ? mapPackageToDraft(selectedPackage, project.baseCurrencyCode) : createEmptyPackageDraft(project.baseCurrencyCode),
    [project.baseCurrencyCode, selectedPackage],
  );
  const readonlyBoqDraft = useMemo(
    () => selectedBoqItem ? mapBoqItemToDraft(selectedBoqItem, project.baseCurrencyCode) : createEmptyBoqDraft(project),
    [project, project.baseCurrencyCode, selectedBoqItem],
  );
  const selectedPackageBoqItemsCount = useMemo(
    () => (readonlyBoqDraft.projectPackageId ? project.boqItems.filter((item) => item.projectPackageId === readonlyBoqDraft.projectPackageId).length : 0),
    [project.boqItems, readonlyBoqDraft.projectPackageId],
  );

  const getEffectiveBudgetQuantity = (item: ProjectBoqItemDto) =>
    drafts[item.id]?.budgetQuantity ?? resolveSavedBudgetQuantity(item);

  const getEffectiveBudgetUnitRate = (item: ProjectBoqItemDto) =>
    drafts[item.id]?.budgetUnitRate ?? resolveSavedBudgetUnitRate(item);

  const getEffectiveBudgetTotal = (item: ProjectBoqItemDto) =>
    roundMoney(getEffectiveBudgetQuantity(item) * getEffectiveBudgetUnitRate(item));

  const dirtyBoqItemIds = useMemo(
    () =>
      boqItems
        .filter((item) => {
          const currentBudgetQuantity = getEffectiveBudgetQuantity(item);
          const currentBudgetUnitRate = getEffectiveBudgetUnitRate(item);
          return !approximatelyEqual(currentBudgetQuantity, resolveSavedBudgetQuantity(item))
            || !approximatelyEqual(currentBudgetUnitRate, resolveSavedBudgetUnitRate(item));
        })
        .map((item) => item.id),
    [boqItems, drafts],
  );

  const dirtyBoqItemIdSet = useMemo(() => new Set(dirtyBoqItemIds), [dirtyBoqItemIds]);
  const budgetedLineCount = useMemo(
    () => boqItems.filter((item) => (item.budgetAmount ?? 0) > 0 || item.budgetQuantity !== undefined || item.budgetUnitRate !== undefined).length,
    [boqItems],
  );

  const updateDraft = (boqItemId: string, changes: BudgetRowDraft) => {
    setDrafts((current) => ({
      ...current,
      [boqItemId]: {
        ...current[boqItemId],
        ...changes,
      },
    }));
  };

  const saveBudgetWorksheet = async () => {
    if (dirtyBoqItemIds.length === 0) {
      return;
    }

    setSaving(true);
    try {
      const updates = boqItems
        .filter((item) => dirtyBoqItemIdSet.has(item.id))
        .map((item) => ({
          boqItemId: item.id,
          budgetQuantity: getEffectiveBudgetQuantity(item),
          budgetUnitRate: getEffectiveBudgetUnitRate(item),
          budgetAmount: getEffectiveBudgetTotal(item),
        }));

      const saved = await Promise.resolve(onSaveBudgetWorksheet(updates));
      if (saved) {
        setDrafts({});
      }
    } finally {
      setSaving(false);
    }
  };

  const exportBudgetWorksheet = () => {
    const rows = boqItems.map((item) => {
      const linkedPackage = packageLookup.get(item.projectPackageId);
      const forecastReferenceTotal = resolveForecastReferenceTotal(item);
      const effectiveBudgetQuantity = getEffectiveBudgetQuantity(item);
      const effectiveBudgetUnitRate = getEffectiveBudgetUnitRate(item);
      const effectiveBudgetTotal = getEffectiveBudgetTotal(item);

      return {
        Phase: linkedPackage?.projectPhaseId ? phaseLookup.get(linkedPackage.projectPhaseId) || linkedPackage.projectPhaseName || 'Unassigned phase' : linkedPackage?.projectPhaseName || 'Unassigned phase',
        'Work Component': item.packageCode ? `${item.packageCode} - ${item.packageName}` : item.packageName || 'Unassigned work component',
        Section: item.sectionCode ? `${item.sectionCode} - ${item.sectionName || ''}`.trim() : '',
        Trade: item.tradeCode ? `${item.tradeCode} - ${item.tradeName || ''}`.trim() : '',
        'Cost Code': item.costCode ? `${item.costCode} - ${item.costCodeName || ''}`.trim() : '',
        'Measurement Standard': item.measurementStandard || '',
        'Measurement Code': item.measurementCode || '',
        'Measurement Rule': item.measurementRule || '',
        'Item Code': item.itemCode || item.lineNumber || '',
        Description: item.description,
        Qty: item.quantity,
        UOM: item.unitOfMeasure || '',
        'Forecast Cost': item.unitRate ?? 0,
        'Forecast Total': forecastReferenceTotal,
        Currency: item.currency,
        'Budget Qty': effectiveBudgetQuantity,
        'Budget Cost': effectiveBudgetUnitRate,
        'Budget Total': effectiveBudgetTotal,
        Variation: roundMoney(effectiveBudgetTotal - forecastReferenceTotal),
      };
    });

    const worksheet = XLSX.utils.json_to_sheet(rows);
    worksheet['!cols'] = [
      { wch: 24 },
      { wch: 28 },
      { wch: 24 },
      { wch: 24 },
      { wch: 24 },
      { wch: 20 },
      { wch: 20 },
      { wch: 48 },
      { wch: 18 },
      { wch: 40 },
      { wch: 10 },
      { wch: 10 },
      { wch: 14 },
      { wch: 16 },
      { wch: 10 },
      { wch: 12 },
      { wch: 14 },
      { wch: 16 },
      { wch: 14 },
    ];
    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, worksheet, 'Budget Worksheet');
    XLSX.writeFile(workbook, `${project.projectCode}_BudgetWorksheet.xlsx`);
  };

  if (boqItems.length === 0) {
    return (
      <Card>
        <CardContent className="py-10 text-sm text-muted-foreground">
          Add BOQ lines under Work Components first. They will appear here automatically for budget preparation.
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader className="space-y-4">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <CardTitle>Budget Worksheet</CardTitle>
            <div className="flex flex-wrap gap-2">
              <Button type="button" variant="outline" onClick={exportBudgetWorksheet} className="gap-2">
                <Download className="h-4 w-4" />
                Export Excel
              </Button>
              <Button onClick={saveBudgetWorksheet} disabled={saving || dirtyBoqItemIds.length === 0} className="gap-2">
                <Save className="h-4 w-4" />
                {saving ? 'Saving...' : `Save Budgeting${dirtyBoqItemIds.length > 0 ? ` (${dirtyBoqItemIds.length})` : ''}`}
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <div className="rounded-xl border bg-slate-50/70 p-4">
              <div className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">BOQ Lines</div>
              <div className="mt-2 text-2xl font-semibold text-slate-900">{boqItems.length}</div>
              <div className="mt-1 text-xs text-slate-500">Measured items available for budgeting</div>
            </div>
            <div className="rounded-xl border bg-slate-50/70 p-4">
              <div className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Budgeted Lines</div>
              <div className="mt-2 text-2xl font-semibold text-slate-900">{budgetedLineCount}</div>
              <div className="mt-1 text-xs text-slate-500">Rows already carrying budget values</div>
            </div>
            <div className={`rounded-xl border p-4 ${dirtyBoqItemIds.length > 0 ? 'border-amber-200 bg-amber-50/70' : 'border-emerald-200 bg-emerald-50/70'}`}>
              <div className={`text-xs font-semibold uppercase tracking-[0.16em] ${dirtyBoqItemIds.length > 0 ? 'text-amber-700' : 'text-emerald-700'}`}>Unsaved Changes</div>
              <div className={`mt-2 text-2xl font-semibold ${dirtyBoqItemIds.length > 0 ? 'text-amber-900' : 'text-emerald-900'}`}>{dirtyBoqItemIds.length}</div>
              <div className={`mt-1 text-xs ${dirtyBoqItemIds.length > 0 ? 'text-amber-700' : 'text-emerald-700'}`}>
                {dirtyBoqItemIds.length > 0 ? 'Budget inputs changed and ready to save' : 'Worksheet is in sync'}
              </div>
            </div>
            <div className="rounded-xl border bg-slate-50/70 p-4">
              <div className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Commercial Budget Total</div>
              <div className="mt-2 text-2xl font-semibold text-slate-900">
                {formatMoney(commercialSummary?.packageBudgetAmount, commercialSummary?.currency || project.baseCurrencyCode)}
              </div>
              <div className="mt-1 text-xs text-slate-500">
                Converted commercial rollup in {commercialSummary?.currency || project.baseCurrencyCode || 'project currency'}
              </div>
            </div>
          </div>

          <div className="overflow-x-auto rounded-xl border">
            <table className="min-w-[1460px] w-full text-sm">
              <thead className="text-slate-700">
                <tr className="border-b">
                  <th className={`px-3 py-3 text-left font-semibold ${readOnlyHeaderClass}`}>Phase</th>
                  <th className={`px-3 py-3 text-left font-semibold ${readOnlyHeaderClass}`}>Work Component</th>
                  <th className={`px-3 py-3 text-left font-semibold ${readOnlyHeaderClass}`}>Item Code</th>
                  <th className={`px-3 py-3 text-left font-semibold ${readOnlyHeaderClass}`}>Description</th>
                  <th className={`px-3 py-3 text-right font-semibold ${readOnlyHeaderClass}`}>Qty</th>
                  <th className={`px-3 py-3 text-left font-semibold ${readOnlyHeaderClass}`}>UOM</th>
                  <th className={`px-3 py-3 text-right font-semibold ${readOnlyHeaderClass}`}>Forecast Cost</th>
                  <th className={`px-3 py-3 text-right font-semibold ${readOnlyHeaderClass}`}>Forecast Total</th>
                  <th className={`px-3 py-3 text-left font-semibold ${readOnlyHeaderClass}`}>Currency</th>
                  <th className={`px-3 py-3 text-right font-semibold ${editableHeaderClass}`}>Budget Qty</th>
                  <th className={`px-3 py-3 text-right font-semibold ${editableHeaderClass}`}>Budget Cost</th>
                  <th className={`px-3 py-3 text-right font-semibold ${readOnlyHeaderClass}`}>Budget Total</th>
                  <th className={`px-3 py-3 text-right font-semibold ${readOnlyHeaderClass}`}>Variation</th>
                </tr>
              </thead>
              <tbody>
                {boqItems.map((item, index) => {
                  const effectiveBudgetQuantity = getEffectiveBudgetQuantity(item);
                  const effectiveBudgetUnitRate = getEffectiveBudgetUnitRate(item);
                  const effectiveBudgetTotal = getEffectiveBudgetTotal(item);
                  const forecastReferenceTotal = resolveForecastReferenceTotal(item);
                  const variation = roundMoney(effectiveBudgetTotal - forecastReferenceTotal);
                  const isDirty = dirtyBoqItemIdSet.has(item.id);
                  const linkedPackage = packageLookup.get(item.projectPackageId);
                  const phaseName = linkedPackage?.projectPhaseId ? phaseLookup.get(linkedPackage.projectPhaseId) || linkedPackage.projectPhaseName || 'Unassigned phase' : linkedPackage?.projectPhaseName || 'Unassigned phase';

                  return (
                    <tr key={item.id} className="border-b align-top">
                      <td className={`px-3 py-3 text-slate-700 ${getReadOnlyCellClass(index, isDirty)}`}>
                        <div className="min-w-[180px]">{phaseName}</div>
                      </td>
                      <td className={`px-3 py-3 ${getReadOnlyCellClass(index, isDirty)}`}>
                        <button
                          type="button"
                          className="text-left font-medium text-sky-700 transition hover:text-sky-800 hover:underline disabled:text-slate-400 disabled:no-underline"
                          onClick={() => linkedPackage && setSelectedPackageId(linkedPackage.id)}
                          disabled={!linkedPackage}
                        >
                          {item.packageCode ? `${item.packageCode} - ${item.packageName}` : item.packageName || 'Unassigned work component'}
                        </button>
                        {item.lineNumber ? <div className="mt-1 text-xs text-slate-500">Line {item.lineNumber}</div> : null}
                      </td>
                      <td className={`px-3 py-3 text-slate-700 ${getReadOnlyCellClass(index, isDirty)}`}>
                        <button
                          type="button"
                          className="whitespace-nowrap text-left font-medium text-sky-700 transition hover:text-sky-800 hover:underline"
                          onClick={() => setSelectedBoqItemId(item.id)}
                        >
                          {item.itemCode || item.lineNumber || 'View item'}
                        </button>
                      </td>
                      <td className={`px-3 py-3 ${getReadOnlyCellClass(index, isDirty)}`}>
                        <div className="max-w-[320px] text-slate-900">{item.description}</div>
                      </td>
                      <td className={`px-3 py-3 text-right tabular-nums text-slate-700 ${getReadOnlyCellClass(index, isDirty)}`}>{item.quantity}</td>
                      <td className={`px-3 py-3 text-slate-700 ${getReadOnlyCellClass(index, isDirty)}`}>{item.unitOfMeasure || '-'}</td>
                      <td className={`px-3 py-3 text-right tabular-nums text-slate-700 ${getReadOnlyCellClass(index, isDirty)}`}>{formatMoney(item.unitRate, item.currency, 4)}</td>
                      <td className={`px-3 py-3 text-right tabular-nums text-slate-700 ${getReadOnlyCellClass(index, isDirty)}`}>{formatMoney(forecastReferenceTotal, item.currency)}</td>
                      <td className={`px-3 py-3 text-slate-700 ${getReadOnlyCellClass(index, isDirty)}`}>{item.currency}</td>
                      <td className={`px-3 py-3 ${getEditableCellClass(isDirty)}`}>
                        <Input
                          type="number"
                          step="0.0001"
                          value={effectiveBudgetQuantity}
                          onChange={(event) => updateDraft(item.id, { budgetQuantity: normalizeNumberInput(event.target.value) })}
                          className="w-[76px] min-w-[76px] border-slate-300 bg-white text-right shadow-sm"
                        />
                      </td>
                      <td className={`px-3 py-3 ${getEditableCellClass(isDirty)}`}>
                        <Input
                          type="number"
                          step="0.0001"
                          value={effectiveBudgetUnitRate}
                          onChange={(event) => updateDraft(item.id, { budgetUnitRate: normalizeNumberInput(event.target.value) })}
                          className="min-w-[120px] border-slate-300 bg-white text-right shadow-sm"
                        />
                      </td>
                      <td className={`px-3 py-3 text-right font-semibold tabular-nums whitespace-nowrap text-slate-900 ${getReadOnlyCellClass(index, isDirty)}`}>{formatMoney(effectiveBudgetTotal, item.currency)}</td>
                      <td className={`px-3 py-3 text-right font-semibold tabular-nums whitespace-nowrap ${variation > 0 ? 'text-red-700' : variation < 0 ? 'text-emerald-700' : 'text-slate-700'} ${getReadOnlyCellClass(index, isDirty)}`}>
                        {formatMoney(variation, item.currency)}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>

      <ProjectPackageDialogs
        project={project}
        phaseOptions={phaseOptions}
        packageDraft={readonlyPackageDraft}
        setPackageDraft={noopSetPackageDraft}
        boqDraft={readonlyBoqDraft}
        setBoqDraft={noopSetBoqDraft}
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
        selectedPackageBoqItemsCount={selectedPackageBoqItemsCount}
        isAddPackageDialogOpen={false}
        setIsAddPackageDialogOpen={noopSetBoolean}
        isAddBoqDialogOpen={false}
        setIsAddBoqDialogOpen={noopSetBoolean}
        editingPackageId={null}
        editingBoqItemId={null}
        onCancelPackageEdit={() => setSelectedPackageId(null)}
        onCancelBoqItemEdit={() => setSelectedBoqItemId(null)}
        onSavePackage={noopSave}
        onSaveBoqItem={noopSave}
        readOnlyPackageDialog={{
          open: Boolean(selectedPackage),
          onOpenChange: (open) => !open && setSelectedPackageId(null),
          title: 'Edit Work Component',
          description: 'Update work component linkage, currency, and commercial values in a focused dialog. Read-only from the budgeting worksheet.',
        }}
        readOnlyBoqDialog={{
          open: Boolean(selectedBoqItem),
          onOpenChange: (open) => !open && setSelectedBoqItemId(null),
          title: 'Edit BOQ Item',
          description: 'Update BOQ detail in a focused modal instead of pushing the work component page layout downward. Read-only from the budgeting worksheet.',
        }}
      />
    </div>
  );
}
