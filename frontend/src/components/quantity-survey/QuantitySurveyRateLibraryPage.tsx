'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArchiveRestore,
  BookOpenCheck,
  Calculator,
  Edit3,
  History,
  LineChart,
  Plus,
  RefreshCw,
  ShieldCheck,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
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
import { ScrollArea } from '@/components/ui/scroll-area';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { QuantitySurveyRateBuildUpDialog } from '@/components/quantity-survey/QuantitySurveyRateBuildUpDialog';
import {
  QuantitySurveyRateItemCategory,
  QuantitySurveyRateLifecycleStatus,
  QuantitySurveyMarketSurveyPriceBasis,
  QuantitySurveyHistoricalRateSourceType,
  QuantitySurveyRateSourceType,
  quantitySurveyRateLibraryService,
  type QuantitySurveyLookupOption,
  type PrepareQuantitySurveyHistoricalRate,
  type PrepareQuantitySurveyMarketSurveyUpdate,
  type QuantitySurveyHistoricalRateSource,
  type QuantitySurveyRate,
  type QuantitySurveyRateLibraryItem,
  type SaveQuantitySurveyRate,
  type SaveQuantitySurveyRateLibraryItem,
} from '@/services/quantity-survey-rate-library.service';

const NONE = '__none__';
const today = () => new Date().toISOString().slice(0, 10);
const dateValue = (value?: string | null) => value?.slice(0, 10) ?? '';
const formatDate = (value?: string | null) =>
  value
    ? new Intl.DateTimeFormat('en-GB', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
      }).format(new Date(value))
    : 'Open-ended';

const CATEGORIES = [
  [QuantitySurveyRateItemCategory.StandardItem, 'Standard item'],
  [QuantitySurveyRateItemCategory.Material, 'Material'],
  [QuantitySurveyRateItemCategory.Labour, 'Labour'],
  [QuantitySurveyRateItemCategory.Plant, 'Plant'],
  [QuantitySurveyRateItemCategory.Equipment, 'Equipment'],
  [QuantitySurveyRateItemCategory.Subcontract, 'Subcontract'],
] as const;

const SOURCES = [
  [QuantitySurveyRateSourceType.Baseline, 'Approved baseline'],
  [
    QuantitySurveyRateSourceType.InventoryStandardCost,
    'Inventory standard cost',
  ],
  [QuantitySurveyRateSourceType.InventoryAverageCost, 'Inventory average cost'],
  [
    QuantitySurveyRateSourceType.InventoryLastPurchaseCost,
    'Inventory last purchase cost',
  ],
  [QuantitySurveyRateSourceType.PurchaseOrder, 'Purchase order'],
  [QuantitySurveyRateSourceType.SupplierQuotation, 'Supplier quotation'],
  [QuantitySurveyRateSourceType.ContractorQuotation, 'Contractor quotation'],
  [QuantitySurveyRateSourceType.FrameworkAgreement, 'Framework agreement'],
  [QuantitySurveyRateSourceType.HistoricalProject, 'Historical project'],
  [QuantitySurveyRateSourceType.LabourSchedule, 'Labour schedule'],
  [QuantitySurveyRateSourceType.PlantHire, 'Plant hire'],
  [QuantitySurveyRateSourceType.MarketSurvey, 'Market survey'],
  [QuantitySurveyRateSourceType.RateBuildUp, 'Rate build-up'],
] as const;
const DIRECT_RATE_SOURCES = SOURCES.filter(
  ([value]) =>
    value !== QuantitySurveyRateSourceType.MarketSurvey &&
    value !== QuantitySurveyRateSourceType.HistoricalProject &&
    value !== QuantitySurveyRateSourceType.PurchaseOrder &&
    value !== QuantitySurveyRateSourceType.RateBuildUp
);

const HISTORICAL_SOURCE_TYPES = [
  [
    QuantitySurveyHistoricalRateSourceType.CompletedBoqLine,
    'Completed BoQ line',
  ],
  [
    QuantitySurveyHistoricalRateSourceType.CertifiedValuation,
    'Certified valuation',
  ],
  [
    QuantitySurveyHistoricalRateSourceType.ProcurementPrice,
    'Procurement price',
  ],
  [
    QuantitySurveyHistoricalRateSourceType.ActualProjectCost,
    'Actual project cost',
  ],
] as const;

const MARKET_PRICE_BASES = [
  [
    QuantitySurveyMarketSurveyPriceBasis.CurrentMarketPrice,
    'Current market price',
  ],
  [QuantitySurveyMarketSurveyPriceBasis.AverageSurveyPrice, 'Survey average'],
  [
    QuantitySurveyMarketSurveyPriceBasis.LowestSurveyPrice,
    'Lowest survey price',
  ],
  [
    QuantitySurveyMarketSurveyPriceBasis.HighestSurveyPrice,
    'Highest survey price',
  ],
  [QuantitySurveyMarketSurveyPriceBasis.ForecastedPrice, 'Forecast price'],
] as const;

const categoryLabel = (value: number | string) => {
  const numeric = typeof value === 'number' ? value : Number(value);
  return CATEGORIES.find(([key]) => key === numeric)?.[1] ?? String(value);
};

const sourceLabel = (value: number | string) => {
  const numeric = typeof value === 'number' ? value : Number(value);
  return SOURCES.find(([key]) => key === numeric)?.[1] ?? String(value);
};

const statusLabel = (value: number | string) => {
  const numeric = typeof value === 'number' ? value : Number(value);
  return numeric === QuantitySurveyRateLifecycleStatus.Published
    ? 'Published'
    : numeric === QuantitySurveyRateLifecycleStatus.Retired
      ? 'Retired'
      : 'Draft';
};

const statusVariant = (value: number | string) =>
  Number(value) === QuantitySurveyRateLifecycleStatus.Published
    ? 'secondary'
    : Number(value) === QuantitySurveyRateLifecycleStatus.Retired
      ? 'outline'
      : 'default';

const emptyItem = (): SaveQuantitySurveyRateLibraryItem => ({
  code: '',
  name: '',
  description: '',
  category: QuantitySurveyRateItemCategory.StandardItem,
  unitOfMeasureId: '',
  projectCatalogEntryId: null,
  inventoryItemId: null,
  isActive: true,
  reason: '',
});

const emptyRate = (currencyId = ''): SaveQuantitySurveyRate => ({
  unitRate: 0,
  currencyId,
  effectiveFrom: today(),
  effectiveTo: null,
  projectTypeId: null,
  locationId: null,
  businessPartnerId: null,
  sourceType: QuantitySurveyRateSourceType.Baseline,
  sourceReference: '',
  sourceDate: today(),
  centralDocumentVersionId: null,
  changeReason: '',
});

const emptyMarketUpdate = (): PrepareQuantitySurveyMarketSurveyUpdate => ({
  marketAnalysisId: '',
  priceBasis: QuantitySurveyMarketSurveyPriceBasis.AverageSurveyPrice,
  effectiveFrom: today(),
  effectiveTo: null,
  projectTypeId: null,
  locationId: null,
  centralDocumentVersionId: '',
  changeReason: '',
});

const emptyHistoricalRate = (): PrepareQuantitySurveyHistoricalRate => ({
  sourceType: QuantitySurveyHistoricalRateSourceType.CompletedBoqLine,
  sourceId: '',
  sourceIntegrityHash: '',
  effectiveFrom: today(),
  effectiveTo: null,
  projectTypeId: null,
  locationId: null,
  centralDocumentVersionId: null,
  changeReason: '',
});

type RowCell<T> = { row: { original: T } };

function LookupSelect({
  value,
  onChange,
  options,
  placeholder,
  allowNone = true,
  disabled = false,
}: {
  value?: string | null;
  onChange: (value: string | null) => void;
  options: QuantitySurveyLookupOption[];
  placeholder: string;
  allowNone?: boolean;
  disabled?: boolean;
}) {
  return (
    <Select
      disabled={disabled}
      value={value || (allowNone ? NONE : undefined)}
      onValueChange={(next) => onChange(next === NONE ? null : next)}
    >
      <SelectTrigger>
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        {allowNone ? (
          <SelectItem value={NONE}>Not applicable</SelectItem>
        ) : null}
        {options.map((option) => (
          <SelectItem key={option.value} value={option.value}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

export default function QuantitySurveyRateLibraryPage() {
  const client = useQueryClient();
  const { toast } = useToast();
  const { hasPermission } = useAuth();
  const canManage = hasPermission('quantity-survey.rates.manage');
  const canApprove = hasPermission('quantity-survey.transactions.approve');
  const canAudit = hasPermission('quantity-survey.audit.read');

  const [search, setSearch] = useState('');
  const [category, setCategory] = useState('all');
  const [effectiveAt, setEffectiveAt] = useState(today());
  const [includeInactive, setIncludeInactive] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [itemOpen, setItemOpen] = useState(false);
  const [editingItem, setEditingItem] =
    useState<QuantitySurveyRateLibraryItem | null>(null);
  const [itemForm, setItemForm] =
    useState<SaveQuantitySurveyRateLibraryItem>(emptyItem);
  const [rateOpen, setRateOpen] = useState(false);
  const [editingRate, setEditingRate] = useState<QuantitySurveyRate | null>(
    null
  );
  const [rateForm, setRateForm] = useState<SaveQuantitySurveyRate>(() =>
    emptyRate()
  );
  const [marketOpen, setMarketOpen] = useState(false);
  const [marketForm, setMarketForm] =
    useState<PrepareQuantitySurveyMarketSurveyUpdate>(emptyMarketUpdate);
  const [historicalOpen, setHistoricalOpen] = useState(false);
  const [buildUpOpen, setBuildUpOpen] = useState(false);
  const [historicalSourceType, setHistoricalSourceType] = useState('all');
  const [historicalSearch, setHistoricalSearch] = useState('');
  const [historicalForm, setHistoricalForm] =
    useState<PrepareQuantitySurveyHistoricalRate>(emptyHistoricalRate);
  const [lifecycle, setLifecycle] = useState<{
    action: 'publish' | 'retire';
    rate: QuantitySurveyRate;
  } | null>(null);
  const [lifecycleReason, setLifecycleReason] = useState('');
  const [historyOpen, setHistoryOpen] = useState(false);

  const lookups = useQuery({
    queryKey: ['quantity-survey-rate-library-lookups'],
    queryFn: quantitySurveyRateLibraryService.lookups,
  });
  const marketSources = useQuery({
    queryKey: ['quantity-survey-market-survey-sources'],
    queryFn: quantitySurveyRateLibraryService.marketSurveySources,
    enabled: marketOpen && canManage,
  });
  const list = useQuery({
    queryKey: [
      'quantity-survey-rate-library',
      search,
      category,
      effectiveAt,
      includeInactive,
    ],
    queryFn: () =>
      quantitySurveyRateLibraryService.list({
        search: search.trim() || undefined,
        category:
          category === 'all'
            ? undefined
            : (Number(category) as QuantitySurveyRateItemCategory),
        effectiveAt: effectiveAt || undefined,
        includeInactive,
        page: 1,
        pageSize: 250,
      }),
  });
  const detail = useQuery({
    queryKey: ['quantity-survey-rate-library-item', selectedId],
    queryFn: () => {
      if (!selectedId) throw new Error('A rate-library item is required.');
      return quantitySurveyRateLibraryService.get(selectedId);
    },
    enabled: Boolean(selectedId),
  });
  const history = useQuery({
    queryKey: ['quantity-survey-rate-library-history', selectedId],
    queryFn: () => {
      if (!selectedId) throw new Error('A rate-library item is required.');
      return quantitySurveyRateLibraryService.history(selectedId);
    },
    enabled: Boolean(selectedId && historyOpen && canAudit),
  });
  const historicalSources = useQuery({
    queryKey: [
      'quantity-survey-historical-rate-sources',
      selectedId,
      historicalSourceType,
      historicalSearch,
    ],
    queryFn: () => {
      if (!selectedId) throw new Error('A rate-library item is required.');
      return quantitySurveyRateLibraryService.historicalSources(
        selectedId,
        historicalSourceType === 'all'
          ? undefined
          : (Number(
              historicalSourceType
            ) as QuantitySurveyHistoricalRateSourceType),
        historicalSearch
      );
    },
    enabled: Boolean(selectedId && historicalOpen && canManage),
  });

  const options = (key: string) => lookups.data?.sources?.[key] ?? [];
  const invalidate = async (id?: string | null) => {
    await client.invalidateQueries({
      queryKey: ['quantity-survey-rate-library'],
    });
    if (id) {
      await client.invalidateQueries({
        queryKey: ['quantity-survey-rate-library-item', id],
      });
      await client.invalidateQueries({
        queryKey: ['quantity-survey-rate-library-history', id],
      });
      await client.invalidateQueries({
        queryKey: ['quantity-survey-historical-rate-sources', id],
      });
      await client.invalidateQueries({
        queryKey: ['quantity-survey-rate-build-ups', id],
      });
    }
  };

  const saveItem = useMutation({
    mutationFn: () =>
      editingItem
        ? quantitySurveyRateLibraryService.updateItem(editingItem.id, {
            ...itemForm,
            rowVersion: editingItem.rowVersion,
          })
        : quantitySurveyRateLibraryService.createItem(itemForm),
    onSuccess: async (value) => {
      setItemOpen(false);
      setEditingItem(null);
      setSelectedId(value.id);
      await invalidate(value.id);
      toast({ title: 'Rate-library item saved', variant: 'success' });
    },
    onError: (error: Error) =>
      toast({
        title: 'Unable to save rate-library item',
        description: error.message,
        variant: 'destructive',
      }),
  });

  const saveRate = useMutation({
    mutationFn: () => {
      if (!selectedId) throw new Error('Select a rate-library item first.');
      return editingRate
        ? quantitySurveyRateLibraryService.updateRate(
            selectedId,
            editingRate.id,
            {
              ...rateForm,
              rowVersion: editingRate.rowVersion,
            }
          )
        : quantitySurveyRateLibraryService.createRate(selectedId, rateForm);
    },
    onSuccess: async () => {
      setRateOpen(false);
      setEditingRate(null);
      await invalidate(selectedId);
      toast({ title: 'Rate draft saved', variant: 'success' });
    },
    onError: (error: Error) =>
      toast({
        title: 'Unable to save rate draft',
        description: error.message,
        variant: 'destructive',
      }),
  });

  const prepareMarketUpdate = useMutation({
    mutationFn: () => {
      if (!selectedId) throw new Error('Select a rate-library item first.');
      return quantitySurveyRateLibraryService.prepareMarketSurveyUpdate(
        selectedId,
        marketForm
      );
    },
    onSuccess: async () => {
      setMarketOpen(false);
      setMarketForm(emptyMarketUpdate());
      await invalidate(selectedId);
      toast({
        title: 'Market-survey update prepared',
        description: 'An independent reviewer must publish the draft.',
        variant: 'success',
      });
    },
    onError: (error: Error) =>
      toast({
        title: 'Unable to prepare market-survey update',
        description: error.message,
        variant: 'destructive',
      }),
  });

  const prepareHistoricalRate = useMutation({
    mutationFn: () => {
      if (!selectedId) throw new Error('Select a rate-library item first.');
      return quantitySurveyRateLibraryService.prepareHistoricalRate(
        selectedId,
        historicalForm
      );
    },
    onSuccess: async () => {
      setHistoricalOpen(false);
      setHistoricalForm(emptyHistoricalRate());
      setHistoricalSearch('');
      setHistoricalSourceType('all');
      await invalidate(selectedId);
      toast({
        title: 'Historical rate draft prepared',
        description: 'An independent reviewer must publish the draft.',
        variant: 'success',
      });
    },
    onError: (error: Error) =>
      toast({
        title: 'Unable to prepare historical rate',
        description: error.message,
        variant: 'destructive',
      }),
  });

  const changeLifecycle = useMutation({
    mutationFn: () => {
      if (!selectedId || !lifecycle)
        throw new Error('Select a rate version first.');
      return lifecycle.action === 'publish'
        ? quantitySurveyRateLibraryService.publishRate(
            selectedId,
            lifecycle.rate.id,
            lifecycle.rate.rowVersion,
            lifecycleReason
          )
        : quantitySurveyRateLibraryService.retireRate(
            selectedId,
            lifecycle.rate.id,
            lifecycle.rate.rowVersion,
            lifecycleReason
          );
    },
    onSuccess: async () => {
      const action = lifecycle?.action === 'publish' ? 'published' : 'retired';
      setLifecycle(null);
      setLifecycleReason('');
      await invalidate(selectedId);
      toast({ title: `Rate ${action}`, variant: 'success' });
    },
    onError: (error: Error) =>
      toast({
        title: 'Unable to change rate lifecycle',
        description: error.message,
        variant: 'destructive',
      }),
  });

  const beginCreateItem = () => {
    setEditingItem(null);
    setItemForm(emptyItem());
    setItemOpen(true);
  };
  const beginEditItem = (item: QuantitySurveyRateLibraryItem) => {
    setEditingItem(item);
    setItemForm({
      code: item.code,
      name: item.name,
      description: item.description ?? '',
      category: Number(item.category) as QuantitySurveyRateItemCategory,
      unitOfMeasureId: item.unitOfMeasureId,
      projectCatalogEntryId: item.projectCatalogEntryId,
      inventoryItemId: item.inventoryItemId,
      isActive: item.isActive,
      reason: '',
    });
    setItemOpen(true);
  };
  const beginCreateRate = () => {
    setEditingRate(null);
    setRateForm(emptyRate(options('currencies')[0]?.value ?? ''));
    setRateOpen(true);
  };
  const beginMarketUpdate = () => {
    setMarketForm(emptyMarketUpdate());
    setMarketOpen(true);
  };
  const beginHistoricalRate = () => {
    setHistoricalForm(emptyHistoricalRate());
    setHistoricalSearch('');
    setHistoricalSourceType('all');
    setHistoricalOpen(true);
  };
  const beginEditRate = (rate: QuantitySurveyRate) => {
    setEditingRate(rate);
    setRateForm({
      unitRate: rate.unitRate,
      currencyId: rate.currencyId,
      effectiveFrom: dateValue(rate.effectiveFrom),
      effectiveTo: dateValue(rate.effectiveTo) || null,
      projectTypeId: rate.projectTypeId,
      locationId: rate.locationId,
      businessPartnerId: rate.businessPartnerId,
      sourceType: Number(rate.sourceType) as QuantitySurveyRateSourceType,
      sourceReference: rate.sourceReference ?? '',
      sourceDate: dateValue(rate.sourceDate),
      centralDocumentVersionId: rate.centralDocumentVersionId,
      changeReason: '',
    });
    setRateOpen(true);
  };

  const columns = useMemo<
    Array<DataTableColumn<QuantitySurveyRateLibraryItem>>
  >(
    () => [
      {
        id: 'code',
        header: 'Code',
        accessorKey: 'code',
        cell: ({ row }: RowCell<QuantitySurveyRateLibraryItem>) => (
          <button
            type="button"
            className="font-medium text-primary hover:underline"
            onClick={() => setSelectedId(row.original.id)}
          >
            {row.original.code}
          </button>
        ),
      },
      { id: 'name', header: 'Rate item', accessorKey: 'name' },
      {
        id: 'category',
        header: 'Category',
        cell: ({ row }: RowCell<QuantitySurveyRateLibraryItem>) =>
          categoryLabel(row.original.category),
      },
      {
        id: 'unit',
        header: 'UOM',
        cell: ({ row }: RowCell<QuantitySurveyRateLibraryItem>) =>
          `${row.original.unitOfMeasureCode} · ${row.original.unitOfMeasureName}`,
      },
      {
        id: 'currentRate',
        header: 'Effective rate',
        cell: ({ row }: RowCell<QuantitySurveyRateLibraryItem>) =>
          row.original.currentRate
            ? `${row.original.currentRate.currencyCode} ${row.original.currentRate.unitRate.toLocaleString()}`
            : 'No published rate',
      },
      {
        id: 'versions',
        header: 'Versions',
        accessorKey: 'rateCount',
      },
      {
        id: 'status',
        header: 'Status',
        cell: ({ row }: RowCell<QuantitySurveyRateLibraryItem>) => (
          <Badge variant={row.original.isActive ? 'secondary' : 'outline'}>
            {row.original.isActive ? 'Active' : 'Inactive'}
          </Badge>
        ),
      },
    ],
    []
  );

  const itemSaveDisabled =
    saveItem.isPending ||
    !itemForm.code.trim() ||
    !itemForm.name.trim() ||
    !itemForm.unitOfMeasureId ||
    Boolean(editingItem && !itemForm.reason?.trim());
  const rateSaveDisabled =
    saveRate.isPending ||
    !rateForm.currencyId ||
    !rateForm.effectiveFrom ||
    !rateForm.sourceDate ||
    !rateForm.changeReason.trim() ||
    rateForm.unitRate < 0;
  const selectedMarketSource = marketSources.data?.find(
    (source) => source.id === marketForm.marketAnalysisId
  );
  const marketProposedRate = selectedMarketSource
    ? marketForm.priceBasis ===
      QuantitySurveyMarketSurveyPriceBasis.CurrentMarketPrice
      ? selectedMarketSource.currentMarketPrice
      : marketForm.priceBasis ===
          QuantitySurveyMarketSurveyPriceBasis.LowestSurveyPrice
        ? selectedMarketSource.lowestSurveyPrice
        : marketForm.priceBasis ===
            QuantitySurveyMarketSurveyPriceBasis.HighestSurveyPrice
          ? selectedMarketSource.highestSurveyPrice
          : marketForm.priceBasis ===
              QuantitySurveyMarketSurveyPriceBasis.ForecastedPrice
            ? selectedMarketSource.forecastedPrice
            : selectedMarketSource.averageSurveyPrice
    : null;
  const marketSaveDisabled =
    prepareMarketUpdate.isPending ||
    !marketForm.marketAnalysisId ||
    !marketForm.effectiveFrom ||
    !marketForm.centralDocumentVersionId ||
    !marketForm.changeReason.trim() ||
    !marketProposedRate ||
    marketProposedRate <= 0;
  const selectedHistoricalSource = historicalSources.data?.find(
    (source) => source.sourceId === historicalForm.sourceId
  );
  const historicalSaveDisabled =
    prepareHistoricalRate.isPending ||
    !selectedHistoricalSource?.canPromote ||
    !historicalForm.sourceId ||
    !historicalForm.sourceIntegrityHash ||
    !historicalForm.effectiveFrom ||
    !historicalForm.changeReason.trim();
  const selected = detail.data;

  return (
    <div className="space-y-4">
      <div className="flex flex-col justify-between gap-3 md:flex-row md:items-center">
        <div>
          <h1 className="text-2xl font-bold">Quantity survey rate library</h1>
          <p className="text-sm text-muted-foreground">
            Controlled, effective-dated rates linked to existing TDC master data
            and central DMS evidence.
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => list.refetch()}>
            <RefreshCw
              className={`mr-2 h-4 w-4 ${list.isFetching ? 'animate-spin' : ''}`}
            />
            Refresh
          </Button>
          {canManage ? (
            <Button onClick={beginCreateItem}>
              <Plus className="mr-2 h-4 w-4" /> Add rate item
            </Button>
          ) : null}
        </div>
      </div>

      <div className="grid gap-3 rounded-lg border bg-card p-3 md:grid-cols-[minmax(220px,2fr)_minmax(180px,1fr)_180px_auto] md:items-end">
        <div className="space-y-1">
          <Label>Search</Label>
          <Input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Code, name, UOM or linked master…"
          />
        </div>
        <div className="space-y-1">
          <Label>Category</Label>
          <Select value={category} onValueChange={setCategory}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All categories</SelectItem>
              {CATEGORIES.map(([value, label]) => (
                <SelectItem key={value} value={String(value)}>
                  {label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1">
          <Label>Effective at</Label>
          <Input
            type="date"
            value={effectiveAt}
            onChange={(event) => setEffectiveAt(event.target.value)}
          />
        </div>
        <div className="flex h-10 items-center gap-2 rounded-md border px-3">
          <Switch
            checked={includeInactive}
            onCheckedChange={setIncludeInactive}
          />
          <Label>Include inactive</Label>
        </div>
      </div>

      <DataTable
        compact
        title="Rate items"
        description={`${list.data?.totalCount ?? 0} tenant-controlled item(s)`}
        data={list.data?.items ?? []}
        columns={columns}
        loading={list.isLoading}
        error={list.error ? 'Failed to load the QS rate library.' : null}
        enableExport
        exportFormats={['csv', 'excel']}
        exportFileName="quantity-survey-rate-library"
        enablePagination
        pageSize={20}
        emptyStateMessage="No rate-library items match the selected filters."
        rowActions={[
          {
            id: 'open',
            label: 'Open rate history',
            icon: BookOpenCheck,
            onClick: (row) => setSelectedId(row.original.id),
          },
          ...(canManage
            ? [
                {
                  id: 'edit',
                  label: 'Edit rate item',
                  icon: Edit3,
                  onClick: (row: { original: QuantitySurveyRateLibraryItem }) =>
                    beginEditItem(row.original),
                },
              ]
            : []),
        ]}
      />

      <Dialog
        open={Boolean(selectedId)}
        onOpenChange={(open) => !open && setSelectedId(null)}
      >
        <DialogContent className="max-w-6xl">
          <DialogHeader>
            <DialogTitle>
              {selected
                ? `${selected.code} · ${selected.name}`
                : 'Rate-library item'}
            </DialogTitle>
            <DialogDescription>
              {selected
                ? `${categoryLabel(selected.category)} · ${selected.unitOfMeasureCode} · ${selected.rateCount} version(s)`
                : 'Loading the controlled rate history…'}
            </DialogDescription>
          </DialogHeader>
          <div className="flex flex-wrap justify-end gap-2">
            {canAudit ? (
              <Button
                variant="outline"
                onClick={() => setHistoryOpen(true)}
                disabled={!selected}
              >
                <History className="mr-2 h-4 w-4" /> Audit history
              </Button>
            ) : null}
            {canManage ? (
              <>
                <Button
                  variant="outline"
                  onClick={beginMarketUpdate}
                  disabled={!selected}
                >
                  <LineChart className="mr-2 h-4 w-4" /> Market survey update
                </Button>
                <Button
                  variant="outline"
                  onClick={beginHistoricalRate}
                  disabled={!selected}
                >
                  <ArchiveRestore className="mr-2 h-4 w-4" /> Historical cost
                </Button>
                <Button
                  variant="outline"
                  onClick={() => setBuildUpOpen(true)}
                  disabled={!selected}
                >
                  <Calculator className="mr-2 h-4 w-4" /> Rate build-up
                </Button>
                <Button onClick={beginCreateRate} disabled={!selected}>
                  <Plus className="mr-2 h-4 w-4" /> New rate draft
                </Button>
              </>
            ) : null}
          </div>
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Version</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Rate</TableHead>
                  <TableHead>Effective period</TableHead>
                  <TableHead>Dimensions</TableHead>
                  <TableHead>Source and evidence</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(selected?.rates ?? []).map((rate) => (
                  <TableRow key={rate.id}>
                    <TableCell className="font-medium">
                      v{rate.version}
                    </TableCell>
                    <TableCell>
                      <Badge variant={statusVariant(rate.lifecycleStatus)}>
                        {statusLabel(rate.lifecycleStatus)}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      {rate.currencyCode} {rate.unitRate.toLocaleString()}
                    </TableCell>
                    <TableCell>
                      {formatDate(rate.effectiveFrom)} –{' '}
                      {formatDate(rate.effectiveTo)}
                    </TableCell>
                    <TableCell className="max-w-64 text-xs">
                      {[
                        rate.projectTypeLabel,
                        rate.locationLabel,
                        rate.businessPartnerLabel,
                      ]
                        .filter(Boolean)
                        .join(' · ') || 'Tenant-wide'}
                    </TableCell>
                    <TableCell className="max-w-72 text-xs">
                      <div>
                        {sourceLabel(rate.sourceType)}
                        {rate.sourceReference
                          ? ` · ${rate.sourceReference}`
                          : ''}
                      </div>
                      <div className="text-muted-foreground">
                        {rate.evidenceLabel ?? 'No DMS evidence linked'}
                      </div>
                      {rate.marketAnalysisCode ? (
                        <div className="mt-1 text-muted-foreground">
                          {rate.marketAnalysisCode} ·{' '}
                          {rate.marketSurveyQuoteCount ?? 0} quote(s) · previous{' '}
                          {rate.previousUnitRate == null
                            ? 'baseline unavailable'
                            : `${rate.previousCurrencyCode} ${rate.previousUnitRate.toLocaleString()}`}
                          {rate.variancePercent == null
                            ? ''
                            : ` · ${rate.variancePercent >= 0 ? '+' : ''}${rate.variancePercent}%`}
                        </div>
                      ) : null}
                      {rate.historicalSourceId ? (
                        <div className="mt-1 text-muted-foreground">
                          {rate.historicalProjectCode} ·{' '}
                          {rate.historicalSourceLabel} ·{' '}
                          {rate.historicalQuantity?.toLocaleString()}{' '}
                          {rate.historicalUnitOfMeasure} · total{' '}
                          {rate.currencyCode}{' '}
                          {rate.historicalTotalAmount?.toLocaleString()}
                        </div>
                      ) : null}
                      {rate.rateBuildUpId ? (
                        <div className="mt-1 text-muted-foreground">
                          Governed component calculation · immutable build-up
                          lineage
                        </div>
                      ) : null}
                    </TableCell>
                    <TableCell>
                      <div className="flex justify-end gap-1">
                        {canManage &&
                        Number(rate.lifecycleStatus) ===
                          QuantitySurveyRateLifecycleStatus.Draft &&
                        Number(rate.sourceType) !==
                          QuantitySurveyRateSourceType.MarketSurvey &&
                        Number(rate.sourceType) !==
                          QuantitySurveyRateSourceType.HistoricalProject &&
                        Number(rate.sourceType) !==
                          QuantitySurveyRateSourceType.RateBuildUp ? (
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => beginEditRate(rate)}
                          >
                            Edit
                          </Button>
                        ) : null}
                        {canApprove &&
                        Number(rate.lifecycleStatus) ===
                          QuantitySurveyRateLifecycleStatus.Draft ? (
                          <Button
                            size="sm"
                            onClick={() => {
                              setLifecycle({ action: 'publish', rate });
                              setLifecycleReason('');
                            }}
                          >
                            Publish
                          </Button>
                        ) : null}
                        {canApprove &&
                        Number(rate.lifecycleStatus) ===
                          QuantitySurveyRateLifecycleStatus.Published ? (
                          <Button
                            size="sm"
                            variant="destructive"
                            onClick={() => {
                              setLifecycle({ action: 'retire', rate });
                              setLifecycleReason('');
                            }}
                          >
                            Retire
                          </Button>
                        ) : null}
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
                {!detail.isLoading && (selected?.rates.length ?? 0) === 0 ? (
                  <TableRow>
                    <TableCell
                      colSpan={7}
                      className="py-8 text-center text-muted-foreground"
                    >
                      No rate versions have been prepared.
                    </TableCell>
                  </TableRow>
                ) : null}
              </TableBody>
            </Table>
          </div>
        </DialogContent>
      </Dialog>

      <QuantitySurveyRateBuildUpDialog
        open={buildUpOpen}
        onOpenChange={setBuildUpOpen}
        itemId={selectedId}
        itemLabel={
          selected ? `${selected.code} · ${selected.name}` : 'Rate item'
        }
        lookups={{
          currencies: options('currencies'),
          projectTypes: options('projectTypes'),
          locations: options('locations'),
          businessPartners: options('businessPartners'),
          evidenceDocuments: options('evidenceDocuments'),
        }}
        onPrepared={() => invalidate(selectedId)}
      />

      <Dialog open={itemOpen} onOpenChange={setItemOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {editingItem ? 'Edit rate-library item' : 'Add rate-library item'}
            </DialogTitle>
            <DialogDescription>
              Select existing masters; do not duplicate inventory, UOM or QS
              catalogue data here.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>Code</Label>
              <Input
                value={itemForm.code}
                onChange={(event) =>
                  setItemForm((value) => ({
                    ...value,
                    code: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Category</Label>
              <Select
                value={String(itemForm.category)}
                onValueChange={(value) =>
                  setItemForm((form) => ({
                    ...form,
                    category: Number(value) as QuantitySurveyRateItemCategory,
                  }))
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {CATEGORIES.map(([value, label]) => (
                    <SelectItem key={value} value={String(value)}>
                      {label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Name</Label>
              <Input
                value={itemForm.name}
                onChange={(event) =>
                  setItemForm((value) => ({
                    ...value,
                    name: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Description</Label>
              <Textarea
                value={itemForm.description ?? ''}
                onChange={(event) =>
                  setItemForm((value) => ({
                    ...value,
                    description: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Unit of measure</Label>
              <LookupSelect
                value={itemForm.unitOfMeasureId}
                onChange={(value) =>
                  setItemForm((form) => ({
                    ...form,
                    unitOfMeasureId: value ?? '',
                  }))
                }
                options={options('unitsOfMeasure')}
                placeholder="Select UOM"
                allowNone={false}
              />
            </div>
            <div className="space-y-2">
              <Label>QS catalogue entry</Label>
              <LookupSelect
                value={itemForm.projectCatalogEntryId}
                onChange={(value) =>
                  setItemForm((form) => ({
                    ...form,
                    projectCatalogEntryId: value,
                  }))
                }
                options={options('catalogueEntries')}
                placeholder="Select catalogue entry"
              />
            </div>
            <div className="space-y-2">
              <Label>Inventory item</Label>
              <LookupSelect
                value={itemForm.inventoryItemId}
                onChange={(value) =>
                  setItemForm((form) => ({ ...form, inventoryItemId: value }))
                }
                options={options('inventoryItems')}
                placeholder="Select inventory item"
              />
            </div>
            {editingItem ? (
              <div className="flex items-center gap-2 pt-7">
                <Switch
                  checked={Boolean(itemForm.isActive)}
                  onCheckedChange={(checked) =>
                    setItemForm((form) => ({ ...form, isActive: checked }))
                  }
                />
                <Label>Active</Label>
              </div>
            ) : null}
            {editingItem ? (
              <div className="space-y-2 md:col-span-2">
                <Label>Change reason</Label>
                <Textarea
                  value={itemForm.reason ?? ''}
                  onChange={(event) =>
                    setItemForm((form) => ({
                      ...form,
                      reason: event.target.value,
                    }))
                  }
                />
              </div>
            ) : null}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setItemOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => saveItem.mutate()}
              disabled={itemSaveDisabled}
            >
              {saveItem.isPending ? 'Saving…' : 'Save item'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={marketOpen} onOpenChange={setMarketOpen}>
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>Prepare market-survey rate update</DialogTitle>
            <DialogDescription>
              Select a published Procurement analysis and current central-DMS
              evidence. The resulting draft retains the previous value and
              requires independent publication.
            </DialogDescription>
          </DialogHeader>
          <ScrollArea className="max-h-[68vh] pr-3">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2 md:col-span-2">
                <Label>Published market analysis</Label>
                <Select
                  value={marketForm.marketAnalysisId || undefined}
                  onValueChange={(value) =>
                    setMarketForm((form) => ({
                      ...form,
                      marketAnalysisId: value,
                    }))
                  }
                >
                  <SelectTrigger>
                    <SelectValue
                      placeholder={
                        marketSources.isLoading
                          ? 'Loading published analyses…'
                          : 'Select market analysis'
                      }
                    />
                  </SelectTrigger>
                  <SelectContent>
                    {(marketSources.data ?? []).map((source) => (
                      <SelectItem key={source.id} value={source.id}>
                        {source.analysisCode} · {source.title} ·{' '}
                        {source.quoteCount} quote(s)
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              {selectedMarketSource ? (
                <div className="rounded-md border bg-muted/30 p-3 text-sm md:col-span-2">
                  <div className="font-medium">
                    {selectedMarketSource.itemDescription ||
                      selectedMarketSource.itemCategory ||
                      selectedMarketSource.title}
                  </div>
                  <div className="mt-1 text-muted-foreground">
                    Survey ended{' '}
                    {formatDate(selectedMarketSource.analysisPeriodEnd)} ·{' '}
                    review due{' '}
                    {formatDate(selectedMarketSource.nextReviewDueAt)} ·{' '}
                    {selectedMarketSource.currencyCode}{' '}
                    {selectedMarketSource.averageSurveyPrice.toLocaleString()}{' '}
                    average
                  </div>
                  {selectedMarketSource.isOverdue ? (
                    <div className="mt-1 text-destructive">
                      This source is overdue under the configured cadence and
                      cannot support a current effective rate.
                    </div>
                  ) : null}
                </div>
              ) : null}
              <div className="space-y-2">
                <Label>Price basis</Label>
                <Select
                  value={String(marketForm.priceBasis)}
                  onValueChange={(value) =>
                    setMarketForm((form) => ({
                      ...form,
                      priceBasis: Number(
                        value
                      ) as QuantitySurveyMarketSurveyPriceBasis,
                    }))
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {MARKET_PRICE_BASES.map(([value, label]) => (
                      <SelectItem key={value} value={String(value)}>
                        {label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Proposed unit rate</Label>
                <Input
                  readOnly
                  value={
                    selectedMarketSource && marketProposedRate != null
                      ? `${selectedMarketSource.currencyCode} ${marketProposedRate.toLocaleString()}`
                      : 'Select a survey and basis'
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>Effective from</Label>
                <Input
                  type="date"
                  value={marketForm.effectiveFrom}
                  onChange={(event) =>
                    setMarketForm((form) => ({
                      ...form,
                      effectiveFrom: event.target.value,
                    }))
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>Effective to</Label>
                <Input
                  type="date"
                  value={marketForm.effectiveTo ?? ''}
                  onChange={(event) =>
                    setMarketForm((form) => ({
                      ...form,
                      effectiveTo: event.target.value || null,
                    }))
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>Project type</Label>
                <LookupSelect
                  value={marketForm.projectTypeId}
                  onChange={(value) =>
                    setMarketForm((form) => ({
                      ...form,
                      projectTypeId: value,
                    }))
                  }
                  options={options('projectTypes')}
                  placeholder="Select project type"
                />
              </div>
              <div className="space-y-2">
                <Label>Region or location</Label>
                <LookupSelect
                  value={marketForm.locationId}
                  onChange={(value) =>
                    setMarketForm((form) => ({ ...form, locationId: value }))
                  }
                  options={options('locations')}
                  placeholder="Select location"
                />
              </div>
              <div className="space-y-2 md:col-span-2">
                <Label>Published central-DMS survey evidence</Label>
                <LookupSelect
                  value={marketForm.centralDocumentVersionId}
                  onChange={(value) =>
                    setMarketForm((form) => ({
                      ...form,
                      centralDocumentVersionId: value ?? '',
                    }))
                  }
                  options={options('evidenceDocuments')}
                  placeholder="Select published evidence"
                  allowNone={false}
                />
              </div>
              <div className="space-y-2 md:col-span-2">
                <Label>Update reason</Label>
                <Textarea
                  value={marketForm.changeReason}
                  onChange={(event) =>
                    setMarketForm((form) => ({
                      ...form,
                      changeReason: event.target.value,
                    }))
                  }
                  placeholder="Explain why this survey should replace the prior rate"
                />
              </div>
            </div>
          </ScrollArea>
          <DialogFooter>
            <Button variant="outline" onClick={() => setMarketOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => prepareMarketUpdate.mutate()}
              disabled={marketSaveDisabled}
            >
              {prepareMarketUpdate.isPending ? 'Preparing…' : 'Prepare update'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={historicalOpen} onOpenChange={setHistoricalOpen}>
        <DialogContent className="max-w-4xl">
          <DialogHeader>
            <DialogTitle>Promote a historical project cost</DialogTitle>
            <DialogDescription>
              Select an eligible completed-project source. The server locks its
              project dimensions, recalculates its value and verifies its
              integrity before preparing an independently publishable draft.
            </DialogDescription>
          </DialogHeader>
          <ScrollArea className="max-h-[68vh] pr-3">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Historical source family</Label>
                <Select
                  value={historicalSourceType}
                  onValueChange={(value) => {
                    setHistoricalSourceType(value);
                    setHistoricalForm(emptyHistoricalRate());
                  }}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All eligible sources</SelectItem>
                    {HISTORICAL_SOURCE_TYPES.map(([value, label]) => (
                      <SelectItem key={value} value={String(value)}>
                        {label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Search source</Label>
                <Input
                  value={historicalSearch}
                  onChange={(event) => setHistoricalSearch(event.target.value)}
                  placeholder="Project, source or reference"
                />
              </div>
              <div className="space-y-2 md:col-span-2">
                <Label>Verified project cost source</Label>
                <Select
                  value={
                    historicalForm.sourceId
                      ? `${historicalForm.sourceType}:${historicalForm.sourceId}`
                      : undefined
                  }
                  onValueChange={(value) => {
                    const source = historicalSources.data?.find(
                      (candidate) =>
                        `${candidate.sourceType}:${candidate.sourceId}` ===
                        value
                    );
                    if (!source) return;
                    setHistoricalForm((form) => ({
                      ...form,
                      sourceType: source.sourceType,
                      sourceId: source.sourceId,
                      sourceIntegrityHash: source.integrityHash,
                      projectTypeId: source.projectTypeId ?? null,
                      locationId: source.locationId ?? null,
                    }));
                  }}
                >
                  <SelectTrigger>
                    <SelectValue
                      placeholder={
                        historicalSources.isLoading
                          ? 'Loading governed historical sources…'
                          : 'Select an eligible project cost'
                      }
                    />
                  </SelectTrigger>
                  <SelectContent>
                    {(historicalSources.data ?? []).map((source) => (
                      <SelectItem
                        key={`${source.sourceType}-${source.sourceId}`}
                        value={`${source.sourceType}:${source.sourceId}`}
                        disabled={!source.canPromote}
                      >
                        {source.projectCode} · {source.sourceReference} ·{' '}
                        {source.currencyCode} {source.unitRate.toLocaleString()}
                        /{source.unitOfMeasure}
                        {source.canPromote ? '' : ' · already promoted'}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {!historicalSources.isLoading &&
                (historicalSources.data?.length ?? 0) === 0 ? (
                  <p className="text-xs text-muted-foreground">
                    No accessible completed-project source matches this rate
                    item and unit of measure.
                  </p>
                ) : null}
                {historicalSources.isError ? (
                  <p className="text-xs text-destructive">
                    Historical project costs could not be loaded. Refresh and
                    try again.
                  </p>
                ) : null}
              </div>
              {selectedHistoricalSource ? (
                <div className="rounded-md border bg-muted/30 p-3 text-sm md:col-span-2">
                  <div className="font-medium">
                    {selectedHistoricalSource.projectCode} ·{' '}
                    {selectedHistoricalSource.projectTitle}
                  </div>
                  <div className="mt-1 text-muted-foreground">
                    {selectedHistoricalSource.sourceLabel} ·{' '}
                    {formatDate(selectedHistoricalSource.sourceDate)} ·{' '}
                    {selectedHistoricalSource.quantity.toLocaleString()}{' '}
                    {selectedHistoricalSource.unitOfMeasure} · unit rate{' '}
                    {selectedHistoricalSource.currencyCode}{' '}
                    {selectedHistoricalSource.unitRate.toLocaleString()} · total{' '}
                    {selectedHistoricalSource.currencyCode}{' '}
                    {selectedHistoricalSource.totalAmount.toLocaleString()}
                  </div>
                </div>
              ) : null}
              <div className="space-y-2">
                <Label>Effective from</Label>
                <Input
                  type="date"
                  value={historicalForm.effectiveFrom}
                  onChange={(event) =>
                    setHistoricalForm((form) => ({
                      ...form,
                      effectiveFrom: event.target.value,
                    }))
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>Effective to</Label>
                <Input
                  type="date"
                  value={historicalForm.effectiveTo ?? ''}
                  onChange={(event) =>
                    setHistoricalForm((form) => ({
                      ...form,
                      effectiveTo: event.target.value || null,
                    }))
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>Source project type</Label>
                <LookupSelect
                  value={historicalForm.projectTypeId}
                  onChange={(value) =>
                    setHistoricalForm((form) => ({
                      ...form,
                      projectTypeId: value,
                    }))
                  }
                  options={options('projectTypes')}
                  placeholder="No project type"
                  disabled={Boolean(selectedHistoricalSource?.projectTypeId)}
                />
              </div>
              <div className="space-y-2">
                <Label>Source region or location</Label>
                <LookupSelect
                  value={historicalForm.locationId}
                  onChange={(value) =>
                    setHistoricalForm((form) => ({
                      ...form,
                      locationId: value,
                    }))
                  }
                  options={options('locations')}
                  placeholder="No project location"
                  disabled={Boolean(selectedHistoricalSource?.locationId)}
                />
              </div>
              <div className="space-y-2 md:col-span-2">
                <Label>Published central-DMS evidence (optional)</Label>
                <LookupSelect
                  value={historicalForm.centralDocumentVersionId}
                  onChange={(value) =>
                    setHistoricalForm((form) => ({
                      ...form,
                      centralDocumentVersionId: value,
                    }))
                  }
                  options={options('evidenceDocuments')}
                  placeholder="Select supporting evidence"
                />
              </div>
              <div className="space-y-2 md:col-span-2">
                <Label>Promotion reason</Label>
                <Textarea
                  value={historicalForm.changeReason}
                  onChange={(event) =>
                    setHistoricalForm((form) => ({
                      ...form,
                      changeReason: event.target.value,
                    }))
                  }
                  placeholder="Explain why this verified historical cost should enter the reusable library"
                />
              </div>
            </div>
          </ScrollArea>
          <DialogFooter>
            <Button variant="outline" onClick={() => setHistoricalOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => prepareHistoricalRate.mutate()}
              disabled={historicalSaveDisabled}
            >
              {prepareHistoricalRate.isPending
                ? 'Preparing…'
                : 'Prepare historical draft'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={rateOpen} onOpenChange={setRateOpen}>
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>
              {editingRate
                ? `Edit rate draft v${editingRate.version}`
                : 'Prepare rate draft'}
            </DialogTitle>
            <DialogDescription>
              Controlled dimensions follow the effective QS-DEC-005 policy.
              Market evidence must reference a published central-DMS version.
            </DialogDescription>
          </DialogHeader>
          <ScrollArea className="max-h-[68vh] pr-3">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Unit rate</Label>
                <Input
                  type="number"
                  min="0"
                  step="0.0001"
                  value={rateForm.unitRate}
                  onChange={(event) =>
                    setRateForm((form) => ({
                      ...form,
                      unitRate: Number(event.target.value),
                    }))
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>Currency</Label>
                <LookupSelect
                  value={rateForm.currencyId}
                  onChange={(value) =>
                    setRateForm((form) => ({
                      ...form,
                      currencyId: value ?? '',
                    }))
                  }
                  options={options('currencies')}
                  placeholder="Select currency"
                  allowNone={false}
                />
              </div>
              <div className="space-y-2">
                <Label>Effective from</Label>
                <Input
                  type="date"
                  value={rateForm.effectiveFrom}
                  onChange={(event) =>
                    setRateForm((form) => ({
                      ...form,
                      effectiveFrom: event.target.value,
                    }))
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>Effective to</Label>
                <Input
                  type="date"
                  value={rateForm.effectiveTo ?? ''}
                  onChange={(event) =>
                    setRateForm((form) => ({
                      ...form,
                      effectiveTo: event.target.value || null,
                    }))
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>Project type</Label>
                <LookupSelect
                  value={rateForm.projectTypeId}
                  onChange={(value) =>
                    setRateForm((form) => ({ ...form, projectTypeId: value }))
                  }
                  options={options('projectTypes')}
                  placeholder="Select project type"
                />
              </div>
              <div className="space-y-2">
                <Label>Region or location</Label>
                <LookupSelect
                  value={rateForm.locationId}
                  onChange={(value) =>
                    setRateForm((form) => ({ ...form, locationId: value }))
                  }
                  options={options('locations')}
                  placeholder="Select location"
                />
              </div>
              <div className="space-y-2">
                <Label>Supplier or contractor</Label>
                <LookupSelect
                  value={rateForm.businessPartnerId}
                  onChange={(value) =>
                    setRateForm((form) => ({
                      ...form,
                      businessPartnerId: value,
                    }))
                  }
                  options={options('businessPartners')}
                  placeholder="Select partner"
                />
              </div>
              <div className="space-y-2">
                <Label>Rate source</Label>
                <Select
                  value={String(rateForm.sourceType)}
                  onValueChange={(value) =>
                    setRateForm((form) => ({
                      ...form,
                      sourceType: Number(value) as QuantitySurveyRateSourceType,
                    }))
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {DIRECT_RATE_SOURCES.map(([value, label]) => (
                      <SelectItem key={value} value={String(value)}>
                        {label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Source date</Label>
                <Input
                  type="date"
                  value={rateForm.sourceDate}
                  onChange={(event) =>
                    setRateForm((form) => ({
                      ...form,
                      sourceDate: event.target.value,
                    }))
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>Source business reference</Label>
                <Input
                  value={rateForm.sourceReference ?? ''}
                  onChange={(event) =>
                    setRateForm((form) => ({
                      ...form,
                      sourceReference: event.target.value,
                    }))
                  }
                  placeholder="Quotation, PO or agreement reference"
                />
              </div>
              <div className="space-y-2 md:col-span-2">
                <Label>Central DMS evidence</Label>
                <LookupSelect
                  value={rateForm.centralDocumentVersionId}
                  onChange={(value) =>
                    setRateForm((form) => ({
                      ...form,
                      centralDocumentVersionId: value,
                    }))
                  }
                  options={options('evidenceDocuments')}
                  placeholder="Select published evidence"
                />
              </div>
              <div className="space-y-2 md:col-span-2">
                <Label>Change reason</Label>
                <Textarea
                  value={rateForm.changeReason}
                  onChange={(event) =>
                    setRateForm((form) => ({
                      ...form,
                      changeReason: event.target.value,
                    }))
                  }
                  placeholder="Explain the source and reason for this rate version"
                />
              </div>
            </div>
          </ScrollArea>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRateOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => saveRate.mutate()}
              disabled={rateSaveDisabled}
            >
              {saveRate.isPending ? 'Saving…' : 'Save draft'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(lifecycle)}
        onOpenChange={(open) => !open && setLifecycle(null)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {lifecycle?.action === 'publish'
                ? 'Publish rate version'
                : 'Retire published rate'}
            </DialogTitle>
            <DialogDescription>
              {lifecycle?.action === 'publish'
                ? 'Publication is an independent checker action. Future rates do not retire the current rate before their effective date.'
                : 'Retirement removes this version from effective-rate selection.'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Decision reason</Label>
            <Textarea
              value={lifecycleReason}
              onChange={(event) => setLifecycleReason(event.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setLifecycle(null)}>
              Cancel
            </Button>
            <Button
              variant={
                lifecycle?.action === 'retire' ? 'destructive' : 'default'
              }
              onClick={() => changeLifecycle.mutate()}
              disabled={changeLifecycle.isPending || !lifecycleReason.trim()}
            >
              <ShieldCheck className="mr-2 h-4 w-4" />
              {changeLifecycle.isPending
                ? 'Saving…'
                : lifecycle?.action === 'publish'
                  ? 'Publish'
                  : 'Retire'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={historyOpen} onOpenChange={setHistoryOpen}>
        <DialogContent className="max-w-5xl">
          <DialogHeader>
            <DialogTitle>Immutable audit history</DialogTitle>
            <DialogDescription>
              Actor, role, reason and correlation evidence for every
              rate-library change.
            </DialogDescription>
          </DialogHeader>
          <ScrollArea className="max-h-[65vh]">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Time</TableHead>
                  <TableHead>Action</TableHead>
                  <TableHead>Actor</TableHead>
                  <TableHead>Reason</TableHead>
                  <TableHead>Correlation</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(history.data ?? []).map((entry) => (
                  <TableRow key={entry.id}>
                    <TableCell className="whitespace-nowrap">
                      {new Date(entry.createdAt).toLocaleString()}
                    </TableCell>
                    <TableCell>
                      <Badge variant="outline">{entry.action}</Badge>
                    </TableCell>
                    <TableCell>
                      <div>{entry.actorName}</div>
                      <div className="text-xs text-muted-foreground">
                        {entry.actorRoles || 'No role snapshot'}
                      </div>
                    </TableCell>
                    <TableCell className="max-w-80 whitespace-normal">
                      {entry.reason || '—'}
                    </TableCell>
                    <TableCell
                      className="max-w-48 truncate font-mono text-xs"
                      title={entry.correlationId}
                    >
                      {entry.correlationId}
                    </TableCell>
                  </TableRow>
                ))}
                {!history.isLoading && (history.data?.length ?? 0) === 0 ? (
                  <TableRow>
                    <TableCell
                      colSpan={5}
                      className="py-8 text-center text-muted-foreground"
                    >
                      No audit events are available.
                    </TableCell>
                  </TableRow>
                ) : null}
              </TableBody>
            </Table>
          </ScrollArea>
        </DialogContent>
      </Dialog>
    </div>
  );
}
