'use client';

import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Building2, FileText, Loader2, Package, Search, ShoppingCart } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useToast } from '@/hooks/use-toast';
import {
  salesSetupService,
  type SalesSaleableItemDto,
  type SalesSaleableSourceDto,
} from '@/services/salesSetupService';
import { hasSaleableNumber } from '@/lib/sales/saleableItemDisplay';

export interface SalesLinkedSourceContext {
  sourceId?: string;
  sourceCode?: string;
  sourceType?: string;
  sourceDisplayName?: string;
  adapterKey?: string;
  sourceItemId?: string;
  itemCode?: string;
  itemName?: string;
  itemType?: string;
  customerId?: string;
  customerName?: string;
  estimatedValue?: number | null;
  currency?: string;
  areaSquareMeters?: number | null;
  propertyReference?: string;
  projectId?: string;
  projectCode?: string;
  projectTitle?: string;
  projectUnitId?: string;
  projectUnitCode?: string;
  projectUnitName?: string;
  suggestedOrderType?: string;
  suggestedAgreementType?: string;
  suggestedLeaseAgreementType?: string;
  inventoryItemId?: string;
  warehouseId?: string;
  warehouseName?: string;
  locationId?: string;
  locationName?: string;
  unitOfMeasure?: string;
  currentQuantity?: number | null;
  availableQuantity?: number | null;
  allocatedQuantity?: number | null;
  shouldCreateSalesAllocation?: boolean;
}

export type SaleableQuickStartMode = 'order' | 'agreement';
export type SaleableAgreementIntent = 'agreement' | 'lease';

const contextParamKeys: (keyof SalesLinkedSourceContext)[] = [
  'sourceId',
  'sourceCode',
  'sourceType',
  'sourceDisplayName',
  'adapterKey',
  'sourceItemId',
  'itemCode',
  'itemName',
  'itemType',
  'customerId',
  'customerName',
  'currency',
  'propertyReference',
  'projectId',
  'projectCode',
  'projectTitle',
  'projectUnitId',
  'projectUnitCode',
  'projectUnitName',
  'suggestedOrderType',
  'suggestedAgreementType',
  'suggestedLeaseAgreementType',
  'inventoryItemId',
  'warehouseId',
  'warehouseName',
  'locationId',
  'locationName',
  'unitOfMeasure',
];

const parseNumberParam = (params: URLSearchParams, key: string) => {
  const value = params.get(key);
  if (!value) return undefined;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : undefined;
};

const normalizeContext = (context: SalesLinkedSourceContext): SalesLinkedSourceContext => {
  const isProjectUnitSource = context.adapterKey === 'project-units' || context.sourceType === 'ProjectUnits';

  return {
    ...context,
    projectUnitId: context.projectUnitId || (isProjectUnitSource ? context.sourceItemId : undefined),
    projectUnitCode: context.projectUnitCode || (isProjectUnitSource ? context.itemCode : undefined),
    projectUnitName: context.projectUnitName || (isProjectUnitSource ? context.itemName : undefined),
  };
};

export const saleableItemToContext = (
  item: SalesSaleableItemDto,
  source?: SalesSaleableSourceDto,
): SalesLinkedSourceContext =>
  normalizeContext({
    sourceId: item.sourceId,
    sourceCode: item.sourceCode,
    sourceType: item.sourceType,
    sourceDisplayName: source?.displayName,
    adapterKey: item.adapterKey,
    sourceItemId: item.sourceItemId,
    itemCode: item.itemCode,
    itemName: item.itemName,
    itemType: item.itemType,
    customerId: item.customerId,
    customerName: item.customerName,
    estimatedValue: item.estimatedValue,
    currency: item.currency,
    areaSquareMeters: item.areaSquareMeters,
    propertyReference: item.propertyReference,
    projectId: item.projectId,
    projectCode: item.projectCode,
    projectTitle: item.projectTitle,
    projectUnitId: item.projectUnitId,
    projectUnitCode: item.projectUnitCode,
    projectUnitName: item.projectUnitName,
    suggestedOrderType: item.suggestedOrderType,
    suggestedAgreementType: item.suggestedAgreementType,
    suggestedLeaseAgreementType: item.suggestedLeaseAgreementType,
    inventoryItemId: item.inventoryItemId,
    warehouseId: item.warehouseId,
    warehouseName: item.warehouseName,
    locationId: item.locationId,
    locationName: item.locationName,
    unitOfMeasure: item.unitOfMeasure,
    currentQuantity: item.currentQuantity,
    availableQuantity: item.availableQuantity,
    allocatedQuantity: item.allocatedQuantity,
    shouldCreateSalesAllocation: item.shouldCreateSalesAllocation,
  });

export const buildSaleableSourceParams = (
  item: SalesSaleableItemDto,
  extra: Record<string, string | undefined> = {},
  source?: SalesSaleableSourceDto,
) => {
  const params = new URLSearchParams();
  const context = saleableItemToContext(item, source);

  Object.entries(context).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      params.set(key, String(value));
    }
  });

  Object.entries(extra).forEach(([key, value]) => {
    if (value) {
      params.set(key, value);
    }
  });

  return params.toString();
};

export const parseSaleableSourceContextFromParams = (params: URLSearchParams) => {
  const context: SalesLinkedSourceContext = {};

  contextParamKeys.forEach((key) => {
    const value = params.get(key);
    if (value) {
      (context as Record<keyof SalesLinkedSourceContext, string | number | undefined>)[key] = value;
    }
  });

  context.estimatedValue = parseNumberParam(params, 'estimatedValue');
  context.areaSquareMeters = parseNumberParam(params, 'areaSquareMeters');
  context.currentQuantity = parseNumberParam(params, 'currentQuantity');
  context.availableQuantity = parseNumberParam(params, 'availableQuantity');
  context.allocatedQuantity = parseNumberParam(params, 'allocatedQuantity');

  const shouldCreateSalesAllocation = params.get('shouldCreateSalesAllocation');
  if (shouldCreateSalesAllocation !== null) {
    context.shouldCreateSalesAllocation = shouldCreateSalesAllocation !== 'false';
  }

  const hasContext =
    !!context.sourceItemId ||
    !!context.projectUnitId ||
    !!context.projectId ||
    !!context.itemName;

  if (!hasContext) {
    return null;
  }

  if (!context.sourceDisplayName && (context.projectId || context.projectUnitId)) {
    context.sourceDisplayName = 'Project Units';
  }

  if (!context.adapterKey && (context.projectId || context.projectUnitId)) {
    context.adapterKey = 'project-units';
  }

  return normalizeContext(context);
};

const formatAmount = (amount?: number | null, currency?: string) => {
  if (amount === undefined || amount === null) return currency || '-';
  return `${currency || ''} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`.trim();
};

interface SaleableSourceQuickStartProps {
  mode: SaleableQuickStartMode;
  linkedContext: SalesLinkedSourceContext | null;
  onSourceSelected?: (source: SalesSaleableSourceDto | null) => void;
  onUseOrder?: (item: SalesSaleableItemDto, source: SalesSaleableSourceDto) => void;
  onUseAgreement?: (item: SalesSaleableItemDto, source: SalesSaleableSourceDto, intent: SaleableAgreementIntent) => void;
}

export function SaleableSourceQuickStart({
  mode,
  linkedContext,
  onSourceSelected,
  onUseOrder,
  onUseAgreement,
}: SaleableSourceQuickStartProps) {
  const { toast } = useToast();
  const [sources, setSources] = useState<SalesSaleableSourceDto[]>([]);
  const [loadingSources, setLoadingSources] = useState(false);
  const [selectedSourceId, setSelectedSourceId] = useState('');
  const [search, setSearch] = useState('');
  const [items, setItems] = useState<SalesSaleableItemDto[]>([]);
  const [selectedItemId, setSelectedItemId] = useState('');
  const [searching, setSearching] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);
  const searchRequestId = useRef(0);

  const availableSources = useMemo(
    () =>
      sources
        .filter((source) => source.isActive && (mode === 'order' ? source.allowSalesOrders : source.allowSalesAgreements))
        .sort((a, b) => a.sortOrder - b.sortOrder),
    [mode, sources],
  );

  const selectedSource = availableSources.find((source) => source.id === selectedSourceId) || availableSources[0];
  const selectedItem = items.find((item) => item.sourceItemId === selectedItemId) || items[0];
  const title = mode === 'order' ? 'Start From Saleable Source' : 'Start From Saleable Source';
  const description =
    mode === 'order'
      ? 'Pick any active saleable source to prefill the customer, item, value, and property context for this order.'
      : 'Pick any active saleable source to prefill the customer, item, value, and property context for this agreement.';
  const linkedCardClass = mode === 'order'
    ? 'border-emerald-200 bg-emerald-50/40'
    : 'border-blue-200 bg-blue-50/40';
  const pickerCardClass = mode === 'order'
    ? 'border-dashed border-emerald-200'
    : 'border-dashed border-blue-200';
  const iconClass = mode === 'order' ? 'text-emerald-700' : 'text-blue-700';

  useEffect(() => {
    let mounted = true;
    (async () => {
      try {
        setLoadingSources(true);
        const data = await salesSetupService.getSaleableSources(false);
        if (!mounted) return;
        setSources(data);
      } catch (error: any) {
        toast({
          title: 'Error',
          description: error.message || 'Failed to load saleable sources.',
          variant: 'destructive',
        });
      } finally {
        if (mounted) setLoadingSources(false);
      }
    })();

    return () => {
      mounted = false;
    };
  }, [toast]);

  useEffect(() => {
    if (!selectedSourceId && availableSources[0]) {
      setSelectedSourceId(availableSources[0].id);
    }
  }, [availableSources, selectedSourceId]);

  useEffect(() => {
    onSourceSelected?.(selectedSource || null);
  }, [onSourceSelected, selectedSource]);

  const searchItems = async (
    source: SalesSaleableSourceDto | undefined = selectedSource,
    query = search,
  ) => {
    if (!source) return;

    const requestId = ++searchRequestId.current;

    try {
      setSearching(true);
      setHasSearched(true);
      const results = await salesSetupService.searchSaleableItems(source.id, query.trim() || undefined, 50);
      if (requestId !== searchRequestId.current) return;
      setItems(results);
      setSelectedItemId(results[0]?.sourceItemId || '');
    } catch (error: any) {
      if (requestId !== searchRequestId.current) return;
      setItems([]);
      setSelectedItemId('');
      toast({
        title: 'Error',
        description: error.message || 'Failed to search saleable items.',
        variant: 'destructive',
      });
    } finally {
      if (requestId === searchRequestId.current) setSearching(false);
    }
  };

  if (linkedContext) {
    return (
      <Card className={linkedCardClass}>
        <CardContent className="pt-6">
          <div className={`flex items-center gap-2 text-sm font-medium ${iconClass}`}>
            <Building2 className="h-4 w-4" />
            {mode === 'order' ? 'Creating Order From Saleable Source' : 'Creating Agreement From Saleable Source'}
          </div>
          <div className="mt-2 text-sm text-slate-700">
            <p className="font-semibold">
              {linkedContext.sourceDisplayName || linkedContext.sourceCode || linkedContext.sourceType || 'Saleable source'}
            </p>
            {linkedContext.projectCode || linkedContext.projectTitle ? (
              <p>
                {linkedContext.projectCode}
                {linkedContext.projectTitle ? ` - ${linkedContext.projectTitle}` : ''}
              </p>
            ) : null}
            <p>
              {linkedContext.itemCode || linkedContext.projectUnitCode || linkedContext.itemName || linkedContext.projectUnitName || 'Selected item'}
              {(linkedContext.itemCode || linkedContext.projectUnitCode) && (linkedContext.itemName || linkedContext.projectUnitName)
                ? ` - ${linkedContext.itemName || linkedContext.projectUnitName}`
                : ''}
            </p>
            <div className="mt-2 flex flex-wrap gap-1">
              {linkedContext.sourceType ? <Badge variant="secondary">{linkedContext.sourceType}</Badge> : null}
              {linkedContext.itemType ? <Badge variant="outline">{linkedContext.itemType}</Badge> : null}
              {hasSaleableNumber(linkedContext.estimatedValue) ? (
                <Badge variant="outline">{formatAmount(linkedContext.estimatedValue, linkedContext.currency)}</Badge>
              ) : null}
              {linkedContext.warehouseName ? <Badge variant="outline">{linkedContext.warehouseName}</Badge> : null}
              {linkedContext.locationName ? <Badge variant="outline">{linkedContext.locationName}</Badge> : null}
              {hasSaleableNumber(linkedContext.availableQuantity) ? (
                <Badge variant="outline">
                  {linkedContext.availableQuantity.toLocaleString()} {linkedContext.unitOfMeasure || ''}
                </Badge>
              ) : null}
            </div>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className={pickerCardClass}>
      <CardHeader>
        <CardTitle className="text-base">{title}</CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-3 md:grid-cols-[240px_minmax(0,1fr)_auto]">
          <Select
            value={selectedSource?.id || ''}
            onValueChange={(value) => {
              const source = availableSources.find((candidate) => candidate.id === value);
              setSelectedSourceId(value);
              setSearch('');
              setItems([]);
              setSelectedItemId('');
              setHasSearched(false);
              void searchItems(source, '');
            }}
            disabled={loadingSources || availableSources.length === 0}
          >
            <SelectTrigger aria-label="Saleable source">
              <SelectValue placeholder={loadingSources ? 'Loading sources' : 'Select source'} />
            </SelectTrigger>
            <SelectContent>
              {availableSources.map((source) => (
                <SelectItem key={source.id} value={source.id}>
                  {source.displayName}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Input
            placeholder="Search saleable items"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                void searchItems();
              }
            }}
            disabled={!selectedSource || searching}
          />
          <Button
            variant="outline"
            aria-label="Search saleable items"
            onClick={() => void searchItems()}
            disabled={!selectedSource || searching}
          >
            {searching ? <Loader2 className="h-4 w-4 animate-spin" /> : <Search className="h-4 w-4" />}
          </Button>
        </div>

        {!loadingSources && availableSources.length === 0 ? (
          <p className="text-sm text-muted-foreground">No active saleable sources are enabled for this transaction type.</p>
        ) : null}

        {items.length > 0 ? (
          <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_320px]">
            <div className="max-h-72 overflow-y-auto rounded-md border">
              <div className="divide-y">
                {items.map((item) => (
                  <button
                    key={item.sourceItemId}
                    type="button"
                    onClick={() => setSelectedItemId(item.sourceItemId)}
                    className={`flex w-full items-start justify-between gap-3 p-3 text-left transition hover:bg-muted/70 ${
                      selectedItem?.sourceItemId === item.sourceItemId ? 'bg-muted' : ''
                    }`}
                  >
                    <div>
                      <div className="font-medium">{item.itemName}</div>
                      <div className="mt-1 text-xs text-muted-foreground">
                        {[item.itemCode, item.projectCode, item.projectTitle].filter(Boolean).join(' - ')}
                      </div>
                      <div className="mt-2 flex flex-wrap gap-1">
                        {item.status ? <Badge variant="secondary">{item.status}</Badge> : null}
                        {item.commercialStatus ? <Badge variant="outline">{item.commercialStatus}</Badge> : null}
                        {item.hasActiveAllocation ? (
                          <Badge variant="destructive">{item.activeAllocationStatus || 'Reserved'}</Badge>
                        ) : null}
                        {item.customerName ? <Badge variant="outline">{item.customerName}</Badge> : null}
                        {item.warehouseName ? <Badge variant="outline">{item.warehouseName}</Badge> : null}
                        {item.locationName ? <Badge variant="outline">{item.locationName}</Badge> : null}
                      </div>
                    </div>
                    <div className="text-right text-sm font-semibold">
                      {formatAmount(item.estimatedValue, item.currency)}
                    </div>
                  </button>
                ))}
              </div>
            </div>

            <div className="rounded-md border p-4">
              {selectedItem ? (
                <div className="space-y-4">
                  <div>
                    <div className="text-base font-semibold">{selectedItem.itemName}</div>
                    <div className="text-sm text-muted-foreground">{selectedItem.propertyReference || selectedItem.itemCode || selectedItem.sourceItemId}</div>
                  </div>
                  <div className="grid grid-cols-2 gap-3 text-sm">
                    <div>
                      <div className="text-muted-foreground">Source</div>
                      <div className="font-medium">{selectedSource?.displayName}</div>
                    </div>
                    <div>
                      <div className="text-muted-foreground">Type</div>
                      <div className="font-medium">{selectedItem.itemType || '-'}</div>
                    </div>
                    <div>
                      <div className="text-muted-foreground">Warehouse</div>
                      <div className="font-medium">{selectedItem.warehouseName || '-'}</div>
                    </div>
                    <div>
                      <div className="text-muted-foreground">{hasSaleableNumber(selectedItem.availableQuantity) ? 'Available' : 'Area'}</div>
                      <div className="font-medium">
                        {hasSaleableNumber(selectedItem.availableQuantity)
                          ? `${selectedItem.availableQuantity.toLocaleString()} ${selectedItem.unitOfMeasure || ''}`.trim()
                          : hasSaleableNumber(selectedItem.areaSquareMeters)
                            ? `${selectedItem.areaSquareMeters.toLocaleString()} sqm`
                            : '-'}
                      </div>
                    </div>
                    <div>
                      <div className="text-muted-foreground">Customer</div>
                      <div className="font-medium">{selectedItem.customerName || selectedItem.activeAllocationCustomerName || '-'}</div>
                    </div>
                    <div>
                      <div className="text-muted-foreground">Location</div>
                      <div className="font-medium">{selectedItem.locationName || '-'}</div>
                    </div>
                  </div>
                  {selectedItem.hasActiveAllocation ? (
                    <div className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                      This item already has an active {selectedItem.activeAllocationStatus || 'reservation'}.
                      {selectedItem.activeAllocationReservedUntil ? ` Reserved until ${new Date(selectedItem.activeAllocationReservedUntil).toLocaleDateString()}.` : ''}
                    </div>
                  ) : null}
                  {mode === 'order' && !selectedItem.canCreateSalesOrder && !selectedItem.hasActiveAllocation ? (
                    <div className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                      {selectedItem.salesOrderIneligibilityReason ||
                        'This item is not currently eligible for a sales order.'}
                    </div>
                  ) : null}
                  {mode === 'order' ? (
                    <Button
                      className="w-full justify-start"
                      disabled={!selectedItem.canCreateSalesOrder || !selectedSource}
                      onClick={() => selectedSource && onUseOrder?.(selectedItem, selectedSource)}
                    >
                      <ShoppingCart className="mr-2 h-4 w-4" />
                      Use For Sales Order
                    </Button>
                  ) : (
                    <div className="space-y-2">
                      <Button
                        className="w-full justify-start"
                        variant="outline"
                        disabled={!selectedItem.canCreateSalesAgreement || !selectedSource}
                        onClick={() => selectedSource && onUseAgreement?.(selectedItem, selectedSource, 'agreement')}
                      >
                        <Package className="mr-2 h-4 w-4" />
                        Use For Agreement
                      </Button>
                      <Button
                        className="w-full justify-start"
                        variant="outline"
                        disabled={!selectedItem.canCreateLeaseAgreement || !selectedSource}
                        onClick={() => selectedSource && onUseAgreement?.(selectedItem, selectedSource, 'lease')}
                      >
                        <FileText className="mr-2 h-4 w-4" />
                        Use For Lease
                      </Button>
                    </div>
                  )}
                </div>
              ) : (
                <div className="flex min-h-48 items-center justify-center text-center text-sm text-muted-foreground">
                  Select an item to continue.
                </div>
              )}
            </div>
          </div>
        ) : (
          <div className="rounded-md border p-6 text-center text-sm text-muted-foreground">
            {searching ? 'Searching saleable items...' : hasSearched ? 'No items matched the search.' : 'Search to select a saleable item.'}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
