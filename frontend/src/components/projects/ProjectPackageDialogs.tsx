import {
  useEffect,
  useMemo,
  useState,
  type Dispatch,
  type SetStateAction,
} from 'react';
import { Check, ChevronsUpDown, Plus, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import type { BusinessPartnerDto } from '@/services/businessPartnerService';
import type { ProjectContractLookupDto } from '@/services/projectService';
import type {
  InventoryItemDto,
  UnitOfMeasureDto,
} from '@/services/inventoryManagementService';
import { projectService } from '@/services/projectService';
import type {
  CreateProjectBoqItemDto,
  CreateProjectPackageDto,
  ProjectDetailDto,
  ProjectBoqClassificationOptionDto,
  ProjectBoqClassificationOptionsDto,
  ProjectPhaseDto,
  ProjectProcurementPlanItemLookupDto,
  ProjectPurchaseOrderLookupDto,
  ProjectPurchaseRequisitionLookupDto,
  ProjectTenderLookupDto,
} from '@/services/projectService';
import { cn } from '@/lib/utils';

const EMPTY_BOQ_CLASSIFICATIONS: ProjectBoqClassificationOptionsDto = {
  effectiveAtUtc: '',
  sections: [],
  trades: [],
  costCodes: [],
  measurementCodes: [],
};

type BoqClassificationSelectProps = {
  label: string;
  value?: string;
  options: ProjectBoqClassificationOptionDto[];
  placeholder: string;
  searchPlaceholder: string;
  emptyText: string;
  disabled?: boolean;
  onChange: (value?: string) => void;
};

function BoqClassificationSelect({
  label,
  value,
  options,
  placeholder,
  searchPlaceholder,
  emptyText,
  disabled,
  onChange,
}: BoqClassificationSelectProps) {
  const [open, setOpen] = useState(false);
  const selected = options.find((option) => option.id === value);

  return (
    <div className="grid gap-2">
      <Label>{label}</Label>
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button
            type="button"
            variant="outline"
            role="combobox"
            aria-expanded={open}
            disabled={disabled}
            className="w-full justify-between font-normal"
          >
            <span className="truncate">
              {selected ? `${selected.code} - ${selected.name}` : placeholder}
            </span>
            <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
          </Button>
        </PopoverTrigger>
        <PopoverContent
          className="w-[var(--radix-popover-trigger-width)] p-0"
          align="start"
        >
          <Command>
            <CommandInput placeholder={searchPlaceholder} />
            <CommandList>
              <CommandEmpty>{emptyText}</CommandEmpty>
              <CommandGroup>
                <CommandItem
                  value={`clear ${label}`}
                  onSelect={() => {
                    onChange(undefined);
                    setOpen(false);
                  }}
                >
                  <Check
                    className={cn(
                      'mr-2 h-4 w-4',
                      value ? 'opacity-0' : 'opacity-100'
                    )}
                  />
                  No selection
                </CommandItem>
                {options.map((option) => (
                  <CommandItem
                    key={option.id}
                    value={`${option.code} ${option.name} ${option.standardCode || ''}`}
                    onSelect={() => {
                      onChange(option.id);
                      setOpen(false);
                    }}
                  >
                    <Check
                      className={cn(
                        'mr-2 h-4 w-4',
                        value === option.id ? 'opacity-100' : 'opacity-0'
                      )}
                    />
                    <span className="truncate">
                      {option.code} - {option.name}
                    </span>
                  </CommandItem>
                ))}
              </CommandGroup>
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>
    </div>
  );
}

type ReadOnlyDialogConfig = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title?: string;
  description?: string;
  closeLabel?: string;
};

type PackagePhaseOption = Pick<
  ProjectPhaseDto,
  | 'id'
  | 'name'
  | 'plannedStartDate'
  | 'plannedEndDate'
  | 'completionWeightPercent'
> & {
  label: string;
  depth: number;
};

type ProjectPackageDialogsProps = {
  project: ProjectDetailDto;
  phaseOptions: PackagePhaseOption[];
  packageDraft: CreateProjectPackageDto;
  setPackageDraft: Dispatch<SetStateAction<CreateProjectPackageDto>>;
  boqDraft: CreateProjectBoqItemDto;
  setBoqDraft: Dispatch<SetStateAction<CreateProjectBoqItemDto>>;
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
  selectedPackageBoqItemsCount: number;
  isAddPackageDialogOpen: boolean;
  setIsAddPackageDialogOpen: Dispatch<SetStateAction<boolean>>;
  isAddBoqDialogOpen: boolean;
  setIsAddBoqDialogOpen: Dispatch<SetStateAction<boolean>>;
  editingPackageId: string | null;
  editingBoqItemId: string | null;
  onCancelPackageEdit: () => void;
  onCancelBoqItemEdit: () => void;
  onSavePackage: () => Promise<boolean> | boolean;
  onSaveBoqItem: () => Promise<boolean> | boolean;
  readOnlyPackageDialog?: ReadOnlyDialogConfig;
  readOnlyBoqDialog?: ReadOnlyDialogConfig;
};

export function ProjectPackageDialogs({
  project,
  phaseOptions,
  packageDraft,
  setPackageDraft,
  boqDraft,
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
  formatCatalogLabel,
  selectedPackageBoqItemsCount,
  isAddPackageDialogOpen,
  setIsAddPackageDialogOpen,
  isAddBoqDialogOpen,
  setIsAddBoqDialogOpen,
  editingPackageId,
  editingBoqItemId,
  onCancelPackageEdit,
  onCancelBoqItemEdit,
  onSavePackage,
  onSaveBoqItem,
  readOnlyPackageDialog,
  readOnlyBoqDialog,
}: ProjectPackageDialogsProps) {
  const [inventorySearch, setInventorySearch] = useState('');
  const [boqClassifications, setBoqClassifications] =
    useState<ProjectBoqClassificationOptionsDto>(EMPTY_BOQ_CLASSIFICATIONS);
  const [boqClassificationsLoading, setBoqClassificationsLoading] =
    useState(false);
  const [boqClassificationsError, setBoqClassificationsError] = useState<
    string | null
  >(null);
  const trimmedInventorySearch = inventorySearch.trim();
  const shouldShowInventoryResults = trimmedInventorySearch.length >= 2;
  const currentBoqUnit = boqDraft.unitOfMeasure?.trim();
  const sortedUnitOfMeasures = [...unitOfMeasures].sort(
    (left, right) =>
      left.sortOrder - right.sortOrder || left.code.localeCompare(right.code)
  );
  const sortedInventoryItems = useMemo(
    () =>
      [...inventoryItems].sort(
        (left, right) =>
          left.itemCode.localeCompare(right.itemCode) ||
          left.name.localeCompare(right.name)
      ),
    [inventoryItems]
  );
  const selectedInventoryItem = useMemo(
    () =>
      sortedInventoryItems.find(
        (item) => item.id === boqDraft.inventoryItemId
      ) ?? null,
    [boqDraft.inventoryItemId, sortedInventoryItems]
  );
  const selectedPhase = useMemo(
    () =>
      phaseOptions.find((phase) => phase.id === packageDraft.projectPhaseId) ??
      null,
    [packageDraft.projectPhaseId, phaseOptions]
  );
  const selectedPhaseStartDate = selectedPhase?.plannedStartDate?.slice(0, 10);
  const selectedPhaseEndDate = selectedPhase?.plannedEndDate?.slice(0, 10);
  const currentBoqItem = useMemo(
    () => project.boqItems.find((item) => item.id === editingBoqItemId) ?? null,
    [editingBoqItemId, project.boqItems]
  );
  const withCurrentClassification = (
    options: ProjectBoqClassificationOptionDto[],
    id: string | undefined,
    code: string | undefined,
    name: string | undefined,
    metadata?: Partial<ProjectBoqClassificationOptionDto>
  ) => {
    if (!id || options.some((option) => option.id === id)) {
      return options;
    }

    return [
      ...options,
      {
        id,
        catalogType: '',
        code: code || 'CURRENT',
        name: name || 'Current saved value',
        sortOrder: Number.MAX_SAFE_INTEGER,
        ...metadata,
      },
    ];
  };
  const sectionOptions = withCurrentClassification(
    boqClassifications.sections,
    boqDraft.sectionCatalogEntryId,
    currentBoqItem?.sectionCode,
    currentBoqItem?.sectionName
  );
  const tradeOptions = withCurrentClassification(
    boqClassifications.trades,
    boqDraft.tradeCatalogEntryId,
    currentBoqItem?.tradeCode,
    currentBoqItem?.tradeName
  );
  const costCodeOptions = withCurrentClassification(
    boqClassifications.costCodes,
    boqDraft.costCodeCatalogEntryId,
    currentBoqItem?.costCode,
    currentBoqItem?.costCodeName
  );
  const measurementCodeOptions = withCurrentClassification(
    boqClassifications.measurementCodes,
    boqDraft.measurementCodeCatalogEntryId,
    currentBoqItem?.measurementCode,
    currentBoqItem?.description,
    {
      standardCode: currentBoqItem?.measurementStandard,
      measurementRule: currentBoqItem?.measurementRule,
      defaultUnitOfMeasure: currentBoqItem?.unitOfMeasure,
    }
  );
  const selectedMeasurementCode =
    measurementCodeOptions.find(
      (option) => option.id === boqDraft.measurementCodeCatalogEntryId
    ) ?? null;
  const hasCurrentBoqUnit = currentBoqUnit
    ? sortedUnitOfMeasures.some((unit) => unit.code === currentBoqUnit)
    : false;
  const boqUnitOptions = [
    ...(currentBoqUnit && !hasCurrentBoqUnit
      ? [
          {
            key: `legacy-${currentBoqUnit}`,
            value: currentBoqUnit,
            label: `${currentBoqUnit} (current value)`,
          },
        ]
      : []),
    ...sortedUnitOfMeasures.map((unit) => ({
      key: unit.id,
      value: unit.code,
      label: `${unit.code}${unit.name ? ` - ${unit.name}` : ''}`,
    })),
  ];
  const filteredInventoryItems = useMemo(() => {
    if (!shouldShowInventoryResults) {
      return [];
    }

    const query = trimmedInventorySearch.toLowerCase();
    return sortedInventoryItems
      .filter((item) =>
        [
          item.itemCode,
          item.name,
          item.description,
          item.shortDescription,
        ].some(
          (value) =>
            typeof value === 'string' && value.toLowerCase().includes(query)
        )
      )
      .slice(0, 12);
  }, [
    shouldShowInventoryResults,
    trimmedInventorySearch,
    sortedInventoryItems,
  ]);
  const boqBudgetVariance =
    typeof boqDraft.budgetAmount === 'number' &&
    typeof boqDraft.forecastAmount === 'number'
      ? Number((boqDraft.budgetAmount - boqDraft.forecastAmount).toFixed(2))
      : undefined;

  useEffect(() => {
    let cancelled = false;
    setBoqClassificationsLoading(true);
    setBoqClassificationsError(null);
    projectService
      .getProjectBoqClassifications(project.id)
      .then((result) => {
        if (!cancelled) {
          setBoqClassifications(result);
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setBoqClassifications(EMPTY_BOQ_CLASSIFICATIONS);
          setBoqClassificationsError(
            error instanceof Error
              ? error.message
              : 'BOQ classifications could not be loaded.'
          );
        }
      })
      .finally(() => {
        if (!cancelled) {
          setBoqClassificationsLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [project.id]);

  const handleMeasurementCodeChange = (value?: string) => {
    const option = measurementCodeOptions.find(
      (candidate) => candidate.id === value
    );
    setBoqDraft((current) => ({
      ...current,
      measurementCodeCatalogEntryId: value,
      itemCode: option?.code || current.itemCode,
      description: current.description?.trim()
        ? current.description
        : option?.description || option?.name || '',
      unitOfMeasure: option?.defaultUnitOfMeasure || current.unitOfMeasure,
    }));
  };

  const handleSavePackage = async () => {
    const saved = await Promise.resolve(onSavePackage());
    if (saved) {
      setIsAddPackageDialogOpen(false);
    }
  };

  const handleSaveBoqItem = async () => {
    const saved = await Promise.resolve(onSaveBoqItem());
    if (saved) {
      setIsAddBoqDialogOpen(false);
    }
  };

  const resolveInventoryItemRate = (item: InventoryItemDto) => {
    const candidates = [
      item.currentCost,
      item.lastPurchaseCost,
      item.standardCost,
      item.averageCost,
    ];
    const preferred = candidates.find(
      (value) =>
        typeof value === 'number' && Number.isFinite(value) && value > 0
    );
    if (preferred !== undefined) {
      return preferred;
    }

    return candidates.find(
      (value) => typeof value === 'number' && Number.isFinite(value)
    );
  };
  const selectedInventoryRate = useMemo(
    () =>
      selectedInventoryItem
        ? resolveInventoryItemRate(selectedInventoryItem)
        : undefined,
    [selectedInventoryItem]
  );

  const handleInventoryItemSelect = (item: InventoryItemDto) => {
    setBoqDraft((current) => ({
      ...current,
      inventoryItemId: item.id,
      itemCode: item.itemCode || current.itemCode,
      description:
        item.description?.trim() ||
        item.shortDescription?.trim() ||
        item.name ||
        current.description,
      unitOfMeasure: item.unitOfMeasure || current.unitOfMeasure,
      unitRate: resolveInventoryItemRate(item) ?? current.unitRate,
    }));
    setInventorySearch('');
  };

  const clearSelectedInventoryItem = () => {
    setBoqDraft((current) => ({
      ...current,
      inventoryItemId: undefined,
    }));
    setInventorySearch('');
  };

  const handlePackagePhaseChange = (value: string) => {
    setPackageDraft((current) => {
      const projectPhaseId = value === 'none' ? undefined : value;
      const phase = phaseOptions.find((item) => item.id === projectPhaseId);
      const phaseStartDate = phase?.plannedStartDate?.slice(0, 10);
      const phaseEndDate = phase?.plannedEndDate?.slice(0, 10);
      const plannedStartDate =
        phaseStartDate &&
        current.plannedStartDate &&
        current.plannedStartDate < phaseStartDate
          ? phaseStartDate
          : current.plannedStartDate;
      const plannedEndDate =
        phaseEndDate &&
        current.plannedEndDate &&
        current.plannedEndDate > phaseEndDate
          ? phaseEndDate
          : current.plannedEndDate;

      return {
        ...current,
        projectPhaseId,
        completionWeightPercent: projectPhaseId
          ? current.completionWeightPercent
          : 0,
        plannedStartDate,
        plannedEndDate,
      };
    });
  };

  const formatWeight = (value?: number) =>
    `${Number(value ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 })}%`;
  const formatDateWindow = (startDate?: string, endDate?: string) => {
    if (!startDate && !endDate) {
      return 'Not set';
    }

    return `${startDate ? startDate.slice(0, 10) : 'Open'} - ${endDate ? endDate.slice(0, 10) : 'Open'}`;
  };

  const renderPackageForm = (readOnly: boolean = false) => (
    <div className="grid gap-4 md:grid-cols-2">
      <div className="grid gap-2">
        <Label>Name</Label>
        <Input
          value={packageDraft.name}
          onChange={(event) =>
            setPackageDraft((current) => ({
              ...current,
              name: event.target.value,
            }))
          }
          readOnly={readOnly}
          className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
        />
      </div>
      <div className="grid gap-2">
        <Label>Code</Label>
        <Input
          value={packageDraft.code || ''}
          onChange={(event) =>
            setPackageDraft((current) => ({
              ...current,
              code: event.target.value || undefined,
            }))
          }
          readOnly={readOnly}
          className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
        />
      </div>
      <div className="grid gap-2">
        <Label>Phase</Label>
        <Select
          value={packageDraft.projectPhaseId || 'none'}
          onValueChange={handlePackagePhaseChange}
          disabled={readOnly}
        >
          <SelectTrigger
            className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
          >
            <SelectValue placeholder="Select phase" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="none">No phase</SelectItem>
            {phaseOptions.map((phase) => (
              <SelectItem key={phase.id} value={phase.id}>
                {phase.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <div className="grid gap-2">
        <Label>Work Component Type</Label>
        <Select
          value={packageDraft.packageType || packageTypeOptions[0]}
          onValueChange={(value) =>
            setPackageDraft((current) => ({ ...current, packageType: value }))
          }
          disabled={readOnly}
        >
          <SelectTrigger
            className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
          >
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {packageTypeOptions.map((item) => (
              <SelectItem key={item} value={item}>
                {formatCatalogLabel(item)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <div className="grid gap-2">
        <Label>Status</Label>
        <Select
          value={packageDraft.status || packageStatusOptions[0]}
          onValueChange={(value) =>
            setPackageDraft((current) => ({ ...current, status: value }))
          }
          disabled={readOnly}
        >
          <SelectTrigger
            className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
          >
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {packageStatusOptions.map((item) => (
              <SelectItem key={item} value={item}>
                {formatCatalogLabel(item)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <div className="grid gap-2">
        <Label>Completion Weight (%)</Label>
        <Input
          type="number"
          min={0}
          max={100}
          step="0.01"
          value={packageDraft.completionWeightPercent ?? ''}
          onChange={(event) =>
            setPackageDraft((current) => ({
              ...current,
              completionWeightPercent: event.target.value
                ? Number(event.target.value)
                : undefined,
            }))
          }
          readOnly={readOnly}
          className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
        />
      </div>
      <div className="grid gap-2">
        <Label>Currency</Label>
        <Select
          value={packageDraft.currency || 'none'}
          onValueChange={(value) =>
            setPackageDraft((current) => ({
              ...current,
              currency: value === 'none' ? undefined : value,
            }))
          }
          disabled={readOnly}
        >
          <SelectTrigger
            className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
          >
            <SelectValue placeholder="Select currency" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="none">Select currency</SelectItem>
            {packageCurrencyOptions.map((item) => (
              <SelectItem key={item} value={item}>
                {getCurrencyOptionLabel(item)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <div className="grid gap-2">
        <Label>Planned Start</Label>
        <Input
          type="date"
          value={packageDraft.plannedStartDate?.slice(0, 10) ?? ''}
          min={selectedPhaseStartDate}
          max={selectedPhaseEndDate}
          onChange={(event) =>
            setPackageDraft((current) => ({
              ...current,
              plannedStartDate: event.target.value || undefined,
            }))
          }
          readOnly={readOnly}
          className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
        />
      </div>
      <div className="grid gap-2">
        <Label>Planned End</Label>
        <Input
          type="date"
          value={packageDraft.plannedEndDate?.slice(0, 10) ?? ''}
          min={selectedPhaseStartDate}
          max={selectedPhaseEndDate}
          onChange={(event) =>
            setPackageDraft((current) => ({
              ...current,
              plannedEndDate: event.target.value || undefined,
            }))
          }
          readOnly={readOnly}
          className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
        />
      </div>
      <div className="rounded-lg border border-blue-200 bg-blue-50/70 px-3 py-3 text-sm text-blue-950 md:col-span-2">
        <div className="font-medium">
          {selectedPhase ? `${selectedPhase.name} phase guide` : 'Phase guide'}
        </div>
        <div className="mt-1 flex flex-wrap gap-3 text-xs text-blue-900">
          <span>
            Phase weight{' '}
            {selectedPhase
              ? formatWeight(selectedPhase.completionWeightPercent)
              : 'Not available'}
          </span>
          <span>
            Phase window{' '}
            {formatDateWindow(
              selectedPhase?.plannedStartDate,
              selectedPhase?.plannedEndDate
            )}
          </span>
        </div>
        <div className="mt-1 text-xs text-blue-800">
          Work component dates should stay inside the selected phase window, and
          work component weights within that phase must total 100%.
        </div>
      </div>
      <div className="grid gap-2">
        <Label>Business Partner</Label>
        <Select
          value={packageDraft.businessPartnerId || 'none'}
          onValueChange={(value) =>
            setPackageDraft((current) => ({
              ...current,
              businessPartnerId: value === 'none' ? undefined : value,
            }))
          }
          disabled={readOnly}
        >
          <SelectTrigger
            className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
          >
            <SelectValue placeholder="Select partner" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="none">No partner</SelectItem>
            {activeBusinessPartners.map((partner) => (
              <SelectItem key={partner.id} value={partner.id}>
                {partner.partnerName}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <div className="grid gap-2">
        <Label>Contract</Label>
        <Select
          value={packageDraft.contractId || 'none'}
          onValueChange={(value) =>
            setPackageDraft((current) => ({
              ...current,
              contractId: value === 'none' ? undefined : value,
            }))
          }
          disabled={readOnly}
        >
          <SelectTrigger
            className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
          >
            <SelectValue placeholder="Select contract" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="none">No contract</SelectItem>
            {activeContracts.map((contract) => (
              <SelectItem key={contract.id} value={contract.id}>
                {contract.contractNumber} - {contract.contractTitle}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <div className="grid gap-2">
        <Label>Tender</Label>
        <Select
          value={packageDraft.tenderId || 'none'}
          onValueChange={(value) =>
            setPackageDraft((current) => ({
              ...current,
              tenderId: value === 'none' ? undefined : value,
            }))
          }
          disabled={readOnly}
        >
          <SelectTrigger
            className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
          >
            <SelectValue placeholder="Select tender" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="none">No tender</SelectItem>
            {tenders.map((tender) => (
              <SelectItem key={tender.id} value={tender.id}>
                {tender.tenderNumber} - {tender.title}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <div className="grid gap-2">
        <Label>Procurement Plan Item</Label>
        <Select
          value={packageDraft.procurementPlanItemId || 'none'}
          onValueChange={(value) =>
            setPackageDraft((current) => ({
              ...current,
              procurementPlanItemId: value === 'none' ? undefined : value,
            }))
          }
          disabled={readOnly}
        >
          <SelectTrigger
            className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
          >
            <SelectValue placeholder="Select plan item" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="none">No plan item</SelectItem>
            {procurementPlanItems.map((item) => (
              <SelectItem key={item.id} value={item.id}>
                {item.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <div className="grid gap-2">
        <Label>Purchase Requisition</Label>
        <Select
          value={packageDraft.purchaseRequisitionId || 'none'}
          onValueChange={(value) =>
            setPackageDraft((current) => ({
              ...current,
              purchaseRequisitionId: value === 'none' ? undefined : value,
            }))
          }
          disabled={readOnly}
        >
          <SelectTrigger
            className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
          >
            <SelectValue placeholder="Select requisition" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="none">No requisition</SelectItem>
            {purchaseRequisitions.map((item) => (
              <SelectItem key={item.id} value={item.id}>
                {item.requisitionNumber}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <div className="grid gap-2">
        <Label>Purchase Order</Label>
        <Select
          value={packageDraft.purchaseOrderId || 'none'}
          onValueChange={(value) =>
            setPackageDraft((current) => ({
              ...current,
              purchaseOrderId: value === 'none' ? undefined : value,
            }))
          }
          disabled={readOnly}
        >
          <SelectTrigger
            className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
          >
            <SelectValue placeholder="Select purchase order" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="none">No purchase order</SelectItem>
            {purchaseOrders.map((item) => (
              <SelectItem key={item.id} value={item.id}>
                {item.orderNumber}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <div className="grid gap-2">
        <Label>Budget Baseline</Label>
        <Input
          type="number"
          value={packageDraft.budgetAmount ?? ''}
          onChange={(event) =>
            setPackageDraft((current) => ({
              ...current,
              budgetAmount: event.target.value
                ? Number(event.target.value)
                : undefined,
            }))
          }
          readOnly={readOnly}
          className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
        />
      </div>
      <div className="grid gap-2">
        <Label>Latest Forecast</Label>
        <Input
          type="number"
          value={packageDraft.forecastAmount ?? ''}
          onChange={(event) =>
            setPackageDraft((current) => ({
              ...current,
              forecastAmount: event.target.value
                ? Number(event.target.value)
                : undefined,
            }))
          }
          readOnly={readOnly}
          className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
        />
      </div>
      <div className="grid gap-2 md:col-span-2">
        <Label>Description</Label>
        <Input
          value={packageDraft.description || ''}
          onChange={(event) =>
            setPackageDraft((current) => ({
              ...current,
              description: event.target.value || undefined,
            }))
          }
          readOnly={readOnly}
          className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
        />
      </div>
    </div>
  );

  const renderBoqForm = (readOnly: boolean = false) => (
    <div className="space-y-4">
      <div className="grid gap-4 md:grid-cols-2">
        <div className="grid gap-2">
          <Label>Work Component</Label>
          <Select
            value={boqDraft.projectPackageId || 'none'}
            onValueChange={(value) =>
              setBoqDraft((current) => ({
                ...current,
                projectPackageId: value === 'none' ? '' : value,
              }))
            }
            disabled={readOnly}
          >
            <SelectTrigger
              className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
            >
              <SelectValue placeholder="Select work component" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="none">Select work component</SelectItem>
              {project.packages.map((item) => (
                <SelectItem key={item.id} value={item.id}>
                  {item.code ? `${item.code} - ${item.name}` : item.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="grid gap-2">
          <Label>Item Type</Label>
          <Select
            value={boqDraft.itemType || boqItemTypeOptions[0]}
            onValueChange={(value) =>
              setBoqDraft((current) => ({ ...current, itemType: value }))
            }
            disabled={readOnly}
          >
            <SelectTrigger
              className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
            >
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {boqItemTypeOptions.map((item) => (
                <SelectItem key={item} value={item}>
                  {formatCatalogLabel(item)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <BoqClassificationSelect
          label="Section"
          value={boqDraft.sectionCatalogEntryId}
          options={sectionOptions}
          placeholder={
            boqClassificationsLoading
              ? 'Loading sections...'
              : 'Select QS section'
          }
          searchPlaceholder="Search sections..."
          emptyText="No effective QS sections are configured."
          disabled={readOnly || boqClassificationsLoading}
          onChange={(value) =>
            setBoqDraft((current) => ({
              ...current,
              sectionCatalogEntryId: value,
            }))
          }
        />
        <BoqClassificationSelect
          label="Trade"
          value={boqDraft.tradeCatalogEntryId}
          options={tradeOptions}
          placeholder={
            boqClassificationsLoading ? 'Loading trades...' : 'Select QS trade'
          }
          searchPlaceholder="Search trades..."
          emptyText="No effective QS trades are configured."
          disabled={readOnly || boqClassificationsLoading}
          onChange={(value) =>
            setBoqDraft((current) => ({
              ...current,
              tradeCatalogEntryId: value,
            }))
          }
        />
        <BoqClassificationSelect
          label="Cost Code"
          value={boqDraft.costCodeCatalogEntryId}
          options={costCodeOptions}
          placeholder={
            boqClassificationsLoading
              ? 'Loading cost codes...'
              : 'Select QS cost code'
          }
          searchPlaceholder="Search cost codes..."
          emptyText="No effective QS cost codes are configured."
          disabled={readOnly || boqClassificationsLoading}
          onChange={(value) =>
            setBoqDraft((current) => ({
              ...current,
              costCodeCatalogEntryId: value,
            }))
          }
        />
        <BoqClassificationSelect
          label="Measurement Code"
          value={boqDraft.measurementCodeCatalogEntryId}
          options={measurementCodeOptions}
          placeholder={
            boqClassificationsLoading
              ? 'Loading measurement codes...'
              : 'Select SMM/CESMM/TDC code'
          }
          searchPlaceholder="Search measurement codes..."
          emptyText="No effective QS measurement codes are configured."
          disabled={readOnly || boqClassificationsLoading}
          onChange={handleMeasurementCodeChange}
        />
        {selectedMeasurementCode ? (
          <div className="rounded-lg border border-blue-200 bg-blue-50/70 px-3 py-2 text-sm md:col-span-2">
            <div className="flex flex-wrap gap-x-4 gap-y-1 font-medium text-blue-950">
              <span>
                Standard: {selectedMeasurementCode.standardCode || 'Not set'}
              </span>
              <span>
                Default UOM:{' '}
                {selectedMeasurementCode.defaultUnitOfMeasure || 'Not set'}
              </span>
            </div>
            <div className="mt-1 text-xs text-blue-800">
              {selectedMeasurementCode.measurementRule ||
                'No measurement rule has been configured for this code.'}
            </div>
          </div>
        ) : null}
        {boqClassificationsError ? (
          <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800 md:col-span-2">
            {boqClassificationsError}
          </div>
        ) : null}
        <div className="grid gap-2 md:col-span-2">
          <div className="flex items-center justify-between gap-3">
            <Label>Inventory Item</Label>
            {!readOnly && selectedInventoryItem ? (
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={clearSelectedInventoryItem}
              >
                Clear Link
              </Button>
            ) : null}
          </div>
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-3.5 h-4 w-4 text-muted-foreground" />
            <Input
              className={`pl-10 ${readOnly ? 'bg-slate-50 text-slate-700' : ''}`}
              placeholder={
                readOnly
                  ? 'Inventory search is disabled in read-only mode'
                  : inventoryItems.length > 0
                    ? 'Type at least 2 characters to search inventory items'
                    : 'No active inventory items available'
              }
              value={inventorySearch}
              onChange={(event) => setInventorySearch(event.target.value)}
              readOnly={readOnly}
              disabled={readOnly || inventoryItems.length === 0}
            />
          </div>
          {selectedInventoryItem ? (
            <div className="rounded-lg border border-blue-200 bg-blue-50/70 p-3">
              <div className="text-sm font-medium text-slate-900">
                Linked item: {selectedInventoryItem.itemCode} -{' '}
                {selectedInventoryItem.name}
              </div>
              <div className="mt-1 text-xs text-slate-600">
                {selectedInventoryItem.description ||
                  selectedInventoryItem.shortDescription ||
                  'No description'}
              </div>
              <div className="mt-1 text-xs text-slate-500">
                UOM {selectedInventoryItem.unitOfMeasure || 'Not set'}
                {selectedInventoryRate !== undefined
                  ? ` • Cost ${selectedInventoryRate.toFixed(2)}`
                  : ''}
              </div>
            </div>
          ) : null}
          {readOnly ? (
            <div className="rounded-lg border border-dashed bg-slate-50/40 p-3 text-sm text-muted-foreground">
              Inventory search is available from the editable BOQ dialog on the
              Work Components tab.
            </div>
          ) : inventoryItems.length === 0 ? (
            <div className="rounded-lg border bg-slate-50/60 p-3 text-sm text-muted-foreground">
              No active inventory items were loaded for selection.
            </div>
          ) : shouldShowInventoryResults ? (
            <div className="rounded-lg border bg-slate-50/60">
              {filteredInventoryItems.length === 0 ? (
                <div className="p-3 text-sm text-muted-foreground">
                  No inventory items match this search.
                </div>
              ) : (
                <div className="max-h-52 divide-y overflow-y-auto">
                  {filteredInventoryItems.map((item) => {
                    const derivedRate = resolveInventoryItemRate(item);
                    const isSelected = selectedInventoryItem?.id === item.id;
                    return (
                      <button
                        key={item.id}
                        type="button"
                        className={`flex w-full flex-col gap-1 px-3 py-3 text-left transition-colors hover:bg-white ${isSelected ? 'bg-blue-50' : ''}`}
                        onClick={() => handleInventoryItemSelect(item)}
                      >
                        <span className="text-sm font-medium text-slate-900">
                          {item.itemCode} - {item.name}
                        </span>
                        <span className="text-xs text-slate-600">
                          {item.description ||
                            item.shortDescription ||
                            'No description'}
                        </span>
                        <span className="text-xs text-slate-500">
                          UOM {item.unitOfMeasure || 'Not set'}
                          {derivedRate !== undefined
                            ? ` • Cost ${derivedRate.toFixed(2)}`
                            : ''}
                          {isSelected ? ' • Linked' : ''}
                        </span>
                      </button>
                    );
                  })}
                </div>
              )}
            </div>
          ) : (
            <div className="rounded-lg border border-dashed bg-slate-50/40 p-3 text-sm text-muted-foreground">
              Start typing to search inventory items and select one for
              auto-fill.
            </div>
          )}
          <div className="text-xs text-muted-foreground">
            {readOnly
              ? 'Inventory linkage is shown here for reference.'
              : 'Selecting an inventory item fills the item code, description, unit of measure, and unit rate automatically.'}
          </div>
        </div>
        <div className="grid gap-2 md:col-span-2">
          <Label>Description</Label>
          <Input
            value={boqDraft.description}
            onChange={(event) =>
              setBoqDraft((current) => ({
                ...current,
                description: event.target.value,
              }))
            }
            readOnly={readOnly}
            className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
          />
        </div>
        <div className="grid gap-2">
          <Label>Line Number</Label>
          <Input
            value={boqDraft.lineNumber || ''}
            onChange={(event) =>
              setBoqDraft((current) => ({
                ...current,
                lineNumber: event.target.value || undefined,
              }))
            }
            readOnly={readOnly}
            className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
          />
        </div>
        <div className="grid gap-2">
          <Label>Item Code</Label>
          <Input
            value={boqDraft.itemCode || ''}
            onChange={(event) =>
              setBoqDraft((current) => ({
                ...current,
                itemCode: event.target.value || undefined,
              }))
            }
            readOnly={readOnly}
            className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
          />
        </div>
        <div className="grid gap-2">
          <Label>Quantity</Label>
          <Input
            type="number"
            value={boqDraft.quantity ?? ''}
            onChange={(event) =>
              setBoqDraft((current) => ({
                ...current,
                quantity: event.target.value
                  ? Number(event.target.value)
                  : undefined,
              }))
            }
            readOnly={readOnly}
            className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
          />
        </div>
        <div className="grid gap-2">
          <Label>Unit of Measure</Label>
          <Select
            value={boqDraft.unitOfMeasure || 'none'}
            onValueChange={(value) =>
              setBoqDraft((current) => ({
                ...current,
                unitOfMeasure: value === 'none' ? undefined : value,
              }))
            }
            disabled={readOnly}
          >
            <SelectTrigger
              className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
            >
              <SelectValue placeholder="Select unit of measure" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="none">No unit selected</SelectItem>
              {boqUnitOptions.map((item) => (
                <SelectItem key={item.key} value={item.value}>
                  {item.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          {currentBoqUnit && !hasCurrentBoqUnit ? (
            <div className="text-xs text-amber-700">
              This BOQ line is using a unit that is not in the active system UOM
              list. Choose a system unit to standardize it.
            </div>
          ) : null}
        </div>
        <div className="grid gap-2">
          <Label>Unit Rate</Label>
          <Input
            type="number"
            value={boqDraft.unitRate ?? ''}
            onChange={(event) =>
              setBoqDraft((current) => ({
                ...current,
                unitRate: event.target.value
                  ? Number(event.target.value)
                  : undefined,
              }))
            }
            readOnly={readOnly}
            className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
          />
        </div>
        <div className="grid gap-2">
          <Label>Budget</Label>
          <Input
            type="number"
            value={boqDraft.budgetAmount ?? ''}
            readOnly
            className="bg-slate-50"
          />
          <div className="text-xs text-muted-foreground">
            This is maintained from the Budgeting tab.
          </div>
        </div>
        <div className="grid gap-2">
          <Label>Forecast</Label>
          <Input
            type="number"
            value={boqDraft.forecastAmount ?? ''}
            onChange={(event) =>
              setBoqDraft((current) => ({
                ...current,
                forecastAmount: event.target.value
                  ? Number(event.target.value)
                  : undefined,
              }))
            }
            readOnly={readOnly}
            className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
          />
        </div>
        <div className="grid gap-2">
          <Label>Variance</Label>
          <Input
            type="number"
            value={boqBudgetVariance ?? ''}
            readOnly
            className="bg-slate-50"
          />
        </div>
        <div className="grid gap-2">
          <Label>Currency</Label>
          <Select
            value={boqDraft.currency || 'none'}
            onValueChange={(value) =>
              setBoqDraft((current) => ({
                ...current,
                currency: value === 'none' ? undefined : value,
              }))
            }
            disabled={readOnly}
          >
            <SelectTrigger
              className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
            >
              <SelectValue placeholder="Select currency" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="none">Select currency</SelectItem>
              {boqCurrencyOptions.map((item) => (
                <SelectItem key={item} value={item}>
                  {getCurrencyOptionLabel(item)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="grid gap-2">
          <Label>Procurement Plan Item</Label>
          <Select
            value={boqDraft.procurementPlanItemId || 'none'}
            onValueChange={(value) =>
              setBoqDraft((current) => ({
                ...current,
                procurementPlanItemId: value === 'none' ? undefined : value,
              }))
            }
            disabled={readOnly}
          >
            <SelectTrigger
              className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
            >
              <SelectValue placeholder="Select plan item" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="none">No plan item</SelectItem>
              {procurementPlanItems.map((item) => (
                <SelectItem key={item.id} value={item.id}>
                  {item.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="grid gap-2 md:col-span-2">
          <Label>Notes</Label>
          <Input
            value={boqDraft.notes || ''}
            onChange={(event) =>
              setBoqDraft((current) => ({
                ...current,
                notes: event.target.value || undefined,
              }))
            }
            readOnly={readOnly}
            className={readOnly ? 'bg-slate-50 text-slate-700' : undefined}
          />
        </div>
      </div>
      <div className="text-sm text-muted-foreground">
        {selectedPackageBoqItemsCount} existing BOQ lines in the selected work
        component
      </div>
    </div>
  );

  return (
    <>
      <Dialog
        open={isAddPackageDialogOpen}
        onOpenChange={(open) => {
          setIsAddPackageDialogOpen(open);
          if (!open) {
            onCancelPackageEdit();
          }
        }}
      >
        <DialogContent className="max-h-[90vh] max-w-5xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Add Work Component</DialogTitle>
            <DialogDescription>
              Capture a construction, trade, or supply work component without
              taking space away from the live list.
            </DialogDescription>
          </DialogHeader>
          {renderPackageForm()}
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => {
                setIsAddPackageDialogOpen(false);
                onCancelPackageEdit();
              }}
            >
              Cancel
            </Button>
            <Button
              disabled={!packageDraft.name?.trim()}
              onClick={handleSavePackage}
            >
              <Plus className="mr-2 h-4 w-4" />
              Add Work Component
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(editingPackageId)}
        onOpenChange={(open) => !open && onCancelPackageEdit()}
      >
        <DialogContent className="max-h-[90vh] max-w-5xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit Work Component</DialogTitle>
            <DialogDescription>
              Update work component linkage, currency, and commercial values in
              a focused dialog.
            </DialogDescription>
          </DialogHeader>
          {renderPackageForm()}
          <DialogFooter>
            <Button variant="outline" onClick={onCancelPackageEdit}>
              Cancel
            </Button>
            <Button
              disabled={!packageDraft.name?.trim()}
              onClick={onSavePackage}
            >
              Save Work Component
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={isAddBoqDialogOpen}
        onOpenChange={(open) => {
          setIsAddBoqDialogOpen(open);
          if (!open) {
            onCancelBoqItemEdit();
          }
        }}
      >
        <DialogContent className="max-h-[90vh] max-w-5xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Add BOQ Item</DialogTitle>
            <DialogDescription>
              Add a BOQ line in a dialog so the work component list remains the
              main workspace surface.
            </DialogDescription>
          </DialogHeader>
          {renderBoqForm()}
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => {
                setIsAddBoqDialogOpen(false);
                onCancelBoqItemEdit();
              }}
            >
              Cancel
            </Button>
            <Button
              disabled={
                !boqDraft.projectPackageId || !boqDraft.description?.trim()
              }
              onClick={handleSaveBoqItem}
            >
              <Plus className="mr-2 h-4 w-4" />
              Add BOQ Item
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(editingBoqItemId)}
        onOpenChange={(open) => !open && onCancelBoqItemEdit()}
      >
        <DialogContent className="max-h-[90vh] max-w-5xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit BOQ Item</DialogTitle>
            <DialogDescription>
              Update BOQ detail in a focused modal instead of pushing the work
              component page layout downward.
            </DialogDescription>
          </DialogHeader>
          {renderBoqForm()}
          <DialogFooter>
            <Button variant="outline" onClick={onCancelBoqItemEdit}>
              Cancel
            </Button>
            <Button
              disabled={
                !boqDraft.projectPackageId || !boqDraft.description?.trim()
              }
              onClick={onSaveBoqItem}
            >
              Save BOQ Item
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {readOnlyPackageDialog ? (
        <Dialog
          open={readOnlyPackageDialog.open}
          onOpenChange={readOnlyPackageDialog.onOpenChange}
        >
          <DialogContent className="max-h-[90vh] max-w-5xl overflow-y-auto">
            <DialogHeader>
              <DialogTitle>
                {readOnlyPackageDialog.title || 'Edit Work Component'}
              </DialogTitle>
              <DialogDescription>
                {readOnlyPackageDialog.description ||
                  'Reviewing the work component dialog in read-only mode from the budgeting worksheet.'}
              </DialogDescription>
            </DialogHeader>
            {renderPackageForm(true)}
            <DialogFooter>
              <Button
                variant="outline"
                onClick={() => readOnlyPackageDialog.onOpenChange(false)}
              >
                {readOnlyPackageDialog.closeLabel || 'Close'}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      ) : null}

      {readOnlyBoqDialog ? (
        <Dialog
          open={readOnlyBoqDialog.open}
          onOpenChange={readOnlyBoqDialog.onOpenChange}
        >
          <DialogContent className="max-h-[90vh] max-w-5xl overflow-y-auto">
            <DialogHeader>
              <DialogTitle>
                {readOnlyBoqDialog.title || 'Edit BOQ Item'}
              </DialogTitle>
              <DialogDescription>
                {readOnlyBoqDialog.description ||
                  'Reviewing the BOQ dialog in read-only mode from the budgeting worksheet.'}
              </DialogDescription>
            </DialogHeader>
            {renderBoqForm(true)}
            <DialogFooter>
              <Button
                variant="outline"
                onClick={() => readOnlyBoqDialog.onOpenChange(false)}
              >
                {readOnlyBoqDialog.closeLabel || 'Close'}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      ) : null}
    </>
  );
}
