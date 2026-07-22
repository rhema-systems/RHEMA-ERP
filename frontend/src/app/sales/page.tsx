'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import {
  ArrowRight,
  BarChart3,
  Boxes,
  Building2,
  ClipboardList,
  FileText,
  Home,
  Layers3,
  Loader2,
  Map,
  Package,
  ReceiptText,
  Search,
  Settings,
  ShoppingCart,
  TrendingUp,
  Truck,
  Users,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { useToast } from '@/hooks/use-toast';
import {
  salesSetupService,
  type SalesSaleableItemDto,
  type SalesSaleableSourceDto,
} from '@/services/salesSetupService';
import { buildSaleableSourceParams as buildSourceParams } from './components/SaleableSourceQuickStart';

const sourceIcons = {
  Building2,
  Package,
  Home,
  Boxes,
  Map,
  Layers3,
};

const salesShortcuts = [
  { title: 'Sales Orders', href: '/sales/orders', icon: ShoppingCart },
  { title: 'Delivery Notes', href: '/sales/deliveries', icon: Truck },
  { title: 'Agreements', href: '/sales/agreements', icon: FileText },
  { title: 'Refunds', href: '/sales/refunds', icon: ReceiptText },
  { title: 'Competitors', href: '/sales/competitors', icon: Users },
  { title: 'Reports', href: '/sales/reports', icon: BarChart3 },
];

const splitTransactionTypes = (value?: string) =>
  (value || '')
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);

export default function SalesPage() {
  const router = useRouter();
  const { toast } = useToast();
  const [sources, setSources] = useState<SalesSaleableSourceDto[]>([]);
  const [loadingSources, setLoadingSources] = useState(true);
  const [selectedSource, setSelectedSource] = useState<SalesSaleableSourceDto | null>(null);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [unitSearch, setUnitSearch] = useState('');
  const [unitResults, setUnitResults] = useState<SalesSaleableItemDto[]>([]);
  const [selectedUnit, setSelectedUnit] = useState<SalesSaleableItemDto | null>(null);
  const [searchingUnits, setSearchingUnits] = useState(false);
  const [hasSearchedUnits, setHasSearchedUnits] = useState(false);

  const activeSources = useMemo(
    () => sources.filter((source) => source.isActive).sort((a, b) => a.sortOrder - b.sortOrder),
    [sources],
  );

  const loadSources = async () => {
    try {
      setLoadingSources(true);
      const data = await salesSetupService.getSaleableSources(false);
      setSources(data);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to load sales sources.',
        variant: 'destructive',
      });
    } finally {
      setLoadingSources(false);
    }
  };

  useEffect(() => {
    loadSources();
  }, []);

  const searchSaleableItems = async (sourceOverride?: SalesSaleableSourceDto | null, searchOverride?: string) => {
    const source = sourceOverride || selectedSource;
    if (!source) {
      return;
    }

    try {
      setSearchingUnits(true);
      setHasSearchedUnits(true);
      const results = await salesSetupService.searchSaleableItems(source.id, (searchOverride ?? unitSearch).trim() || undefined, 50);
      setUnitResults(results);
      setSelectedUnit(results[0] || null);
    } catch (error: any) {
      setUnitResults([]);
      setSelectedUnit(null);
      toast({
        title: 'Error',
        description: error.message || `Failed to search ${source.displayName}.`,
        variant: 'destructive',
      });
    } finally {
      setSearchingUnits(false);
    }
  };

  const openSource = (source: SalesSaleableSourceDto) => {
    setSelectedSource(source);
    setUnitSearch('');
    setUnitResults([]);
    setSelectedUnit(null);
    setHasSearchedUnits(false);
    setPickerOpen(true);
    void searchSaleableItems(source, '');
  };

  const createOrder = () => {
    if (!selectedUnit) {
      return;
    }

    const params = buildSourceParams(selectedUnit, {
      orderType: selectedUnit.suggestedOrderType || 'PropertySale',
    }, selectedSource || undefined);
    router.push(`/sales/orders/create?${params}`);
  };

  const createAgreement = (lease = false) => {
    if (!selectedUnit) {
      return;
    }

    const params = buildSourceParams(selectedUnit, {
      agreementType: lease
        ? selectedUnit.suggestedLeaseAgreementType || 'LeaseAgreement'
        : selectedUnit.suggestedAgreementType || 'PlotAllocation',
    }, selectedSource || undefined);
    router.push(`/sales/agreements/create?${params}`);
  };

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div>
          <h1 className="flex items-center gap-2 text-3xl font-bold tracking-tight">
            <TrendingUp className="h-8 w-8 text-slate-700" />
            Sales & Distribution
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Select a configured source, then continue into orders, agreements, leases, or reservations.
          </p>
        </div>
        <Button variant="outline" onClick={() => router.push('/administration/sales')}>
          <Settings className="mr-2 h-4 w-4" />
          Sales Setup
        </Button>
      </div>

      <section className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold">Saleable Sources</h2>
          {loadingSources ? <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" /> : null}
        </div>

        {loadingSources ? (
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            {Array.from({ length: 4 }).map((_, index) => (
              <div key={index} className="h-36 rounded-md border bg-muted/30" />
            ))}
          </div>
        ) : activeSources.length > 0 ? (
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            {activeSources.map((source) => {
              const Icon = sourceIcons[source.icon as keyof typeof sourceIcons] || Package;
              return (
                <button
                  key={source.id}
                  type="button"
                  onClick={() => openSource(source)}
                  className="flex min-h-36 flex-col rounded-md border bg-card p-4 text-left transition hover:border-slate-400 hover:shadow-sm"
                >
                  <div className="flex items-start justify-between gap-3">
                    <span
                      className="flex h-10 w-10 items-center justify-center rounded-md text-white"
                      style={{ backgroundColor: source.colorCode || '#2563EB' }}
                    >
                      <Icon className="h-5 w-5" />
                    </span>
                    <ArrowRight className="h-4 w-4 text-muted-foreground" />
                  </div>
                  <div className="mt-4">
                    <div className="font-semibold">{source.displayName}</div>
                    <div className="mt-1 text-xs text-muted-foreground">{source.sourceType}</div>
                  </div>
                  <div className="mt-auto flex flex-wrap gap-1 pt-3">
                    {splitTransactionTypes(source.supportedTransactionTypes).slice(0, 3).map((type) => (
                      <Badge key={type} variant="secondary" className="text-[11px]">
                        {type}
                      </Badge>
                    ))}
                  </div>
                </button>
              );
            })}
          </div>
        ) : (
          <div className="rounded-md border bg-card p-6 text-center">
            <p className="text-sm text-muted-foreground">No active sales sources are configured.</p>
            <Button className="mt-4" onClick={() => router.push('/administration/sales')}>
              <Settings className="mr-2 h-4 w-4" />
              Open Sales Setup
            </Button>
          </div>
        )}
      </section>

      <section className="space-y-3">
        <h2 className="text-lg font-semibold">Sales Workbench</h2>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
          {salesShortcuts.map((shortcut) => {
            const Icon = shortcut.icon;
            return (
              <Card
                key={shortcut.title}
                className="cursor-pointer transition hover:border-slate-400 hover:shadow-sm"
                onClick={() => router.push(shortcut.href)}
              >
                <CardHeader className="space-y-3 p-4">
                  <span className="flex h-9 w-9 items-center justify-center rounded-md bg-slate-100 text-slate-700">
                    <Icon className="h-4 w-4" />
                  </span>
                  <CardTitle className="text-sm">{shortcut.title}</CardTitle>
                </CardHeader>
              </Card>
            );
          })}
        </div>
      </section>

      <Dialog open={pickerOpen} onOpenChange={setPickerOpen}>
        <DialogContent className="max-h-[90vh] max-w-5xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{selectedSource?.displayName || 'Saleable Items'}</DialogTitle>
          </DialogHeader>

          <div className="grid gap-4 lg:grid-cols-[1.1fr_0.9fr]">
            <div className="space-y-3">
              <div className="flex gap-2">
                <Input
                  value={unitSearch}
                  onChange={(event) => setUnitSearch(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter') {
                      void searchSaleableItems();
                    }
                  }}
                  placeholder={`Search ${selectedSource?.displayName || 'saleable items'}`}
                />
                <Button onClick={() => void searchSaleableItems()} disabled={searchingUnits}>
                  {searchingUnits ? <Loader2 className="h-4 w-4 animate-spin" /> : <Search className="h-4 w-4" />}
                </Button>
              </div>

              <div className="max-h-[420px] overflow-y-auto rounded-md border">
                {searchingUnits ? (
                  <div className="flex h-40 items-center justify-center">
                    <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                  </div>
                ) : unitResults.length > 0 ? (
                  <div className="divide-y">
                    {unitResults.map((unit) => (
                      <button
                        key={unit.projectUnitId || unit.sourceItemId}
                        type="button"
                        onClick={() => setSelectedUnit(unit)}
                        className={`flex w-full items-start justify-between gap-3 p-3 text-left transition hover:bg-muted/70 ${
                          (selectedUnit?.projectUnitId || selectedUnit?.sourceItemId) === (unit.projectUnitId || unit.sourceItemId) ? 'bg-muted' : ''
                        }`}
                      >
                        <div>
                          <div className="font-medium">{unit.projectUnitName || unit.itemName}</div>
                          <div className="mt-1 text-xs text-muted-foreground">
                            {[unit.itemCode, unit.projectCode, unit.projectTitle, unit.warehouseName, unit.locationName]
                              .filter(Boolean)
                              .join(' - ')}
                          </div>
                          <div className="mt-2 flex flex-wrap gap-1">
                            {unit.status ? <Badge variant="secondary">{unit.status}</Badge> : null}
                            {unit.commercialStatus ? <Badge variant="outline">{unit.commercialStatus}</Badge> : null}
                            {unit.customerName ? <Badge variant="outline">{unit.customerName}</Badge> : null}
                            {unit.availableQuantity !== undefined ? (
                              <Badge variant="outline">
                                {unit.availableQuantity.toLocaleString()} {unit.unitOfMeasure || ''}
                              </Badge>
                            ) : null}
                          </div>
                        </div>
                        <div className="text-right text-sm">
                          <div className="font-semibold">
                            {unit.estimatedValue ? `${unit.currency || ''} ${unit.estimatedValue.toLocaleString()}` : unit.currency}
                          </div>
                          {unit.areaSquareMeters ? (
                            <div className="text-xs text-muted-foreground">{unit.areaSquareMeters.toLocaleString()} sqm</div>
                          ) : null}
                        </div>
                      </button>
                    ))}
                  </div>
                ) : (
                  <div className="flex h-40 items-center justify-center px-4 text-center text-sm text-muted-foreground">
                    {hasSearchedUnits ? 'No saleable items matched the search.' : 'Search to select a saleable item.'}
                  </div>
                )}
              </div>
            </div>

            <div className="rounded-md border p-4">
              {selectedUnit ? (
                <div className="space-y-4">
                  <div>
                    <div className="text-lg font-semibold">{selectedUnit.projectUnitName || selectedUnit.itemName}</div>
                    <div className="text-sm text-muted-foreground">{selectedUnit.propertyReference || selectedUnit.projectUnitCode || selectedUnit.itemCode}</div>
                  </div>
                  <div className="grid grid-cols-2 gap-3 text-sm">
                    <div>
                      <div className="text-muted-foreground">Source</div>
                      <div className="font-medium">{selectedSource?.displayName || '-'}</div>
                    </div>
                    <div>
                      <div className="text-muted-foreground">Type</div>
                      <div className="font-medium">{selectedUnit.itemType || '-'}</div>
                    </div>
                    <div>
                      <div className="text-muted-foreground">Warehouse</div>
                      <div className="font-medium">{selectedUnit.warehouseName || selectedUnit.projectCode || '-'}</div>
                    </div>
                    <div>
                      <div className="text-muted-foreground">Available</div>
                      <div className="font-medium">
                        {selectedUnit.availableQuantity !== undefined
                          ? `${selectedUnit.availableQuantity.toLocaleString()} ${selectedUnit.unitOfMeasure || ''}`.trim()
                          : selectedUnit.areaSquareMeters ? `${selectedUnit.areaSquareMeters.toLocaleString()} sqm` : '-'}
                      </div>
                    </div>
                  </div>
                  <div className="space-y-2">
                    <Button className="w-full justify-start" onClick={createOrder} disabled={!selectedUnit.canCreateSalesOrder}>
                      <ShoppingCart className="mr-2 h-4 w-4" />
                      Create Sales Order
                    </Button>
                    <Button className="w-full justify-start" variant="outline" onClick={() => createAgreement(false)} disabled={!selectedUnit.canCreateSalesAgreement}>
                      <ClipboardList className="mr-2 h-4 w-4" />
                      Create Agreement
                    </Button>
                    <Button className="w-full justify-start" variant="outline" onClick={() => createAgreement(true)} disabled={!selectedUnit.canCreateLeaseAgreement}>
                      <FileText className="mr-2 h-4 w-4" />
                      Create Lease
                    </Button>
                  </div>
                </div>
              ) : (
                <div className="flex h-full min-h-72 items-center justify-center text-center text-sm text-muted-foreground">
                  Select an item to continue.
                </div>
              )}
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setPickerOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
