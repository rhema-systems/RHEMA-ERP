'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { ArrowLeft, Save } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { currencyService, type CurrencyListDto } from '@/services/financeCommonService';
import {
  inventoryManagementService,
  type InventoryCategoryDto,
  type InventoryItemDto,
} from '@/services/inventoryManagementService';
import {
  marketAnalysisService,
  type CreateMarketAnalysisDto,
  type MarketAnalysisDetailDto,
} from '@/services/procurementPlanningService';

type MarketAnalysisFormProps = {
  initialValue?: MarketAnalysisDetailDto;
  mode: 'create' | 'edit';
};

const today = new Date().toISOString().slice(0, 10);
const yearEnd = new Date(new Date().getFullYear(), 11, 31).toISOString().slice(0, 10);

const emptyForm: CreateMarketAnalysisDto = {
  title: '',
  description: '',
  itemCategory: '',
  itemDescription: '',
  analysisPeriodStart: today,
  analysisPeriodEnd: yearEnd,
  historicalAveragePrice: 0,
  previousPrice: undefined,
  currentMarketPrice: 0,
  forecastedPrice: 0,
  priceTrend: 'Stable',
  priceChangePercent: 0,
  priceVariancePercent: undefined,
  currency: '',
  leadTimeDays: undefined,
  marketAvailability: 'Medium',
  supplyRiskLevel: 'Medium',
  inflationImpactPercent: 0,
  recommendedBudgetAdjustmentPercent: 0,
  marketRiskLevel: 'Medium',
  riskFactors: '',
  opportunities: '',
  recommendedStrategy: '',
  strategyRationale: '',
  seasonalPattern: '',
  notes: '',
};

const toDateInput = (value?: string) => value ? value.slice(0, 10) : '';

const toForm = (analysis?: MarketAnalysisDetailDto): CreateMarketAnalysisDto => analysis ? {
  title: analysis.title,
  description: analysis.description || '',
  itemCategory: analysis.itemCategory || '',
  itemDescription: analysis.itemDescription || '',
  analysisPeriodStart: toDateInput(analysis.analysisPeriodStart),
  analysisPeriodEnd: toDateInput(analysis.analysisPeriodEnd),
  historicalAveragePrice: analysis.historicalAveragePrice,
  previousPrice: analysis.previousPrice,
  currentMarketPrice: analysis.currentMarketPrice,
  forecastedPrice: analysis.forecastedPrice,
  priceTrend: analysis.priceTrend,
  priceChangePercent: analysis.priceChangePercent,
  priceVariancePercent: analysis.priceVariancePercent,
  currency: analysis.currency,
  leadTimeDays: analysis.leadTimeDays,
  marketAvailability: analysis.marketAvailability,
  supplyRiskLevel: analysis.supplyRiskLevel,
  inflationImpactPercent: analysis.inflationImpactPercent,
  recommendedBudgetAdjustmentPercent: analysis.recommendedBudgetAdjustmentPercent,
  marketRiskLevel: analysis.marketRiskLevel,
  riskFactors: analysis.riskFactors || '',
  opportunities: analysis.opportunities || '',
  recommendedStrategy: analysis.recommendedStrategy || '',
  strategyRationale: analysis.strategyRationale || '',
  optimalPurchaseMonth: analysis.optimalPurchaseMonth,
  seasonalPattern: analysis.seasonalPattern || '',
  notes: analysis.notes || '',
} : emptyForm;

export function MarketAnalysisForm({ initialValue, mode }: MarketAnalysisFormProps) {
  const router = useRouter();
  const [form, setForm] = useState<CreateMarketAnalysisDto>(() => toForm(initialValue));
  const [currencies, setCurrencies] = useState<CurrencyListDto[]>([]);
  const [inventoryItems, setInventoryItems] = useState<InventoryItemDto[]>([]);
  const [categories, setCategories] = useState<InventoryCategoryDto[]>([]);
  const [selectedItemId, setSelectedItemId] = useState('');
  const [saving, setSaving] = useState(false);
  const [loadingOptions, setLoadingOptions] = useState(true);
  const [currencyError, setCurrencyError] = useState<string>();

  useEffect(() => {
    let mounted = true;
    void (async () => {
      setLoadingOptions(true);
      const [currencyResult, itemResult, categoryResult] = await Promise.allSettled([
        currencyService.getActive(),
        inventoryManagementService.getInventoryItems({ isActive: true }),
        inventoryManagementService.getActiveInventoryCategories(),
      ]);
      if (!mounted) return;

      if (currencyResult.status === 'fulfilled') {
        const activeCurrencies = currencyResult.value.filter((currency) => Boolean(currency.code) && currency.isActive !== false);
        setCurrencies(activeCurrencies);
        setCurrencyError(activeCurrencies.length === 0
          ? 'No active Finance currency is configured for this tenant.'
          : undefined);
        if (mode === 'create') {
          const defaultCurrency = activeCurrencies.find((currency) => currency.isBaseCurrency) || activeCurrencies[0];
          setForm((current) => ({
            ...current,
            currency: activeCurrencies.some((currency) => currency.code.toUpperCase() === current.currency?.toUpperCase())
              ? current.currency
              : defaultCurrency?.code || '',
          }));
        }
      } else {
        const message = currencyResult.reason instanceof Error
          ? currencyResult.reason.message
          : 'Unable to load Finance currencies.';
        setCurrencies([]);
        setCurrencyError(message);
        toast.error('Unable to load currencies', { description: message });
      }

      const itemData = itemResult.status === 'fulfilled' ? itemResult.value : [];
      const categoryData = categoryResult.status === 'fulfilled' ? categoryResult.value : [];
      setInventoryItems(itemData);
      setCategories(categoryData);

      if (itemResult.status === 'rejected') {
        toast.error('Unable to load inventory items', {
          description: itemResult.reason instanceof Error ? itemResult.reason.message : undefined,
        });
      }
      if (categoryResult.status === 'rejected') {
        toast.error('Unable to load inventory categories', {
          description: categoryResult.reason instanceof Error ? categoryResult.reason.message : undefined,
        });
      }

      if (initialValue?.itemDescription) {
        const matchedItem = itemData.find((item) =>
          item.name === initialValue.itemDescription ||
          item.itemCode === initialValue.itemDescription ||
          `${item.itemCode} - ${item.name}` === initialValue.itemDescription
        );
        if (matchedItem) setSelectedItemId(matchedItem.id);
      }
      setLoadingOptions(false);
    })();

    return () => { mounted = false; };
  }, [initialValue?.itemDescription, mode]);

  const variancePercent = useMemo(() => {
    const previous = Number(form.previousPrice || form.historicalAveragePrice || 0);
    const current = Number(form.currentMarketPrice || 0);
    return previous > 0 ? Number((((current - previous) / previous) * 100).toFixed(2)) : 0;
  }, [form.currentMarketPrice, form.historicalAveragePrice, form.previousPrice]);

  const update = (field: keyof CreateMarketAnalysisDto, value: string | number | undefined) => {
    setForm((current) => ({ ...current, [field]: value }));
  };

  const currencyOptions = useMemo(() => {
    const currentCurrency = form.currency?.trim();
    if (mode !== 'edit' || !currentCurrency || currencies.some((currency) => currency.code.toUpperCase() === currentCurrency.toUpperCase())) {
      return currencies;
    }

    return [...currencies, {
      id: `historical-${currentCurrency}`,
      code: currentCurrency,
      name: `${currentCurrency} (inactive historical value)`,
      symbol: currentCurrency,
      decimalPlaces: 2,
      exchangeRate: 1,
      isBaseCurrency: false,
      isActive: false,
      displayOrder: 999,
    }];
  }, [currencies, form.currency, mode]);

  const categoryOptions = useMemo(() => {
    const currentCategory = form.itemCategory?.trim();
    if (!currentCategory || categories.some((category) => category.name === currentCategory)) {
      return categories;
    }

    return [
      ...categories,
      {
        id: currentCategory,
        code: currentCategory,
        name: currentCategory,
        isActive: true,
        defaultSerialTracking: false,
        defaultLotTracking: false,
        defaultRequiresInspection: false,
      },
    ];
  }, [categories, form.itemCategory]);

  const handleItemSelect = (itemId: string) => {
    const item = inventoryItems.find((entry) => entry.id === itemId);
    if (!item) return;

    setSelectedItemId(item.id);
    setForm((current) => ({
      ...current,
      itemDescription: item.name,
      itemCategory: item.categoryName || current.itemCategory,
      currentMarketPrice: Number(item.lastPurchaseCost || item.currentCost || item.averageCost || current.currentMarketPrice || 0),
      historicalAveragePrice: Number(item.averageCost || item.standardCost || current.historicalAveragePrice || 0),
      previousPrice: Number(item.lastPurchaseCost || current.previousPrice || 0) || undefined,
      forecastedPrice: Number(item.lastPurchaseCost || item.currentCost || current.forecastedPrice || 0),
      leadTimeDays: Number.isFinite(item.leadTimeDays) ? item.leadTimeDays : current.leadTimeDays,
    }));
  };

  const submit = async () => {
    if (!form.title.trim()) {
      toast.error('Title is required');
      return;
    }
    if (!form.currency?.trim()) {
      toast.error('Select an active Finance currency');
      return;
    }

    try {
      setSaving(true);
      const payload: CreateMarketAnalysisDto = {
        ...form,
        priceVariancePercent: variancePercent,
        priceChangePercent: Number(form.priceChangePercent || variancePercent || 0),
        currentMarketPrice: Number(form.currentMarketPrice || 0),
        historicalAveragePrice: Number(form.historicalAveragePrice || 0),
        forecastedPrice: Number(form.forecastedPrice || form.currentMarketPrice || 0),
        previousPrice: form.previousPrice === undefined || form.previousPrice === null ? undefined : Number(form.previousPrice),
        leadTimeDays: form.leadTimeDays === undefined || form.leadTimeDays === null ? undefined : Number(form.leadTimeDays),
        inflationImpactPercent: Number(form.inflationImpactPercent || 0),
        recommendedBudgetAdjustmentPercent: Number(form.recommendedBudgetAdjustmentPercent || 0),
      };
      const saved = mode === 'edit' && initialValue
        ? await marketAnalysisService.updateAnalysis(initialValue.id, payload)
        : await marketAnalysisService.createAnalysis(payload);
      toast.success(mode === 'edit' ? 'Market analysis updated' : 'Market analysis created');
      router.push(`/procurement/planning/market-analysis/${saved.id}`);
    } catch (error) {
      console.error('Error saving market analysis:', error);
      toast.error('Failed to save market analysis', {
        description: error instanceof Error ? error.message : undefined,
      });
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">{mode === 'edit' ? 'Edit Market Analysis' : 'New Market Analysis'}</h1>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => router.push('/procurement/planning/market-analysis')}>
            <ArrowLeft className="mr-2 h-4 w-4" />
            Back
          </Button>
          <Button onClick={submit} disabled={saving || loadingOptions || currencyOptions.length === 0 || !form.currency}>
            <Save className="mr-2 h-4 w-4" />
            {saving ? 'Saving...' : 'Save'}
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Market Intelligence</CardTitle>
        </CardHeader>
        <CardContent className="space-y-5">
          <div className="grid gap-4 lg:grid-cols-3">
            <div className="space-y-2 lg:col-span-2">
              <Label>Title</Label>
              <Input value={form.title} onChange={(event) => update('title', event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Currency</Label>
              <Select value={form.currency || undefined} onValueChange={(value) => update('currency', value)} disabled={loadingOptions || currencyOptions.length === 0}>
                <SelectTrigger><SelectValue placeholder={loadingOptions ? 'Loading currencies...' : 'Select currency'} /></SelectTrigger>
                <SelectContent>
                  {currencyOptions.map((currency) => (
                    <SelectItem key={currency.id || currency.code} value={currency.code}>
                      {currency.code} - {currency.name || currency.code}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {currencyError && <p className="text-xs text-destructive">{currencyError}</p>}
            </div>
            <div className="space-y-2">
              <Label>Product</Label>
              <Select value={selectedItemId || undefined} onValueChange={handleItemSelect}>
                <SelectTrigger><SelectValue placeholder={form.itemDescription || 'Select product'} /></SelectTrigger>
                <SelectContent>
                  {inventoryItems.map((item) => (
                    <SelectItem key={item.id} value={item.id}>
                      {item.itemCode} - {item.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Item Category</Label>
              <Select value={form.itemCategory || undefined} onValueChange={(value) => update('itemCategory', value)}>
                <SelectTrigger><SelectValue placeholder="Select category" /></SelectTrigger>
                <SelectContent>
                  {categoryOptions.map((category) => (
                    <SelectItem key={category.id} value={category.name}>
                      {category.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Selected Item</Label>
              <Input value={form.itemDescription || ''} readOnly />
            </div>
            <div className="space-y-2">
              <Label>Period Start</Label>
              <Input type="date" value={form.analysisPeriodStart} onChange={(event) => update('analysisPeriodStart', event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Period End</Label>
              <Input type="date" value={form.analysisPeriodEnd} onChange={(event) => update('analysisPeriodEnd', event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Lead Time Days</Label>
              <Input type="number" min={0} value={form.leadTimeDays ?? ''} onChange={(event) => update('leadTimeDays', event.target.value === '' ? undefined : Number(event.target.value))} />
            </div>
          </div>

          <div className="grid gap-4 lg:grid-cols-4">
            <div className="space-y-2">
              <Label>Previous Price</Label>
              <Input type="number" min={0} step="0.01" value={form.previousPrice ?? ''} onChange={(event) => update('previousPrice', event.target.value === '' ? undefined : Number(event.target.value))} />
            </div>
            <div className="space-y-2">
              <Label>Historical Avg</Label>
              <Input type="number" min={0} step="0.01" value={form.historicalAveragePrice ?? 0} onChange={(event) => update('historicalAveragePrice', Number(event.target.value))} />
            </div>
            <div className="space-y-2">
              <Label>Current Market Price</Label>
              <Input type="number" min={0} step="0.01" value={form.currentMarketPrice ?? 0} onChange={(event) => update('currentMarketPrice', Number(event.target.value))} />
            </div>
            <div className="space-y-2">
              <Label>Variance %</Label>
              <Input value={`${variancePercent.toFixed(2)}%`} readOnly />
            </div>
            <div className="space-y-2">
              <Label>Forecasted Price</Label>
              <Input type="number" min={0} step="0.01" value={form.forecastedPrice ?? 0} onChange={(event) => update('forecastedPrice', Number(event.target.value))} />
            </div>
            <div className="space-y-2">
              <Label>Price Trend</Label>
              <Select value={form.priceTrend || 'Stable'} onValueChange={(value) => update('priceTrend', value)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="Stable">Stable</SelectItem>
                  <SelectItem value="Increasing">Increasing</SelectItem>
                  <SelectItem value="Decreasing">Decreasing</SelectItem>
                  <SelectItem value="Volatile">Volatile</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Inflation Impact %</Label>
              <Input type="number" step="0.01" value={form.inflationImpactPercent ?? 0} onChange={(event) => update('inflationImpactPercent', Number(event.target.value))} />
            </div>
            <div className="space-y-2">
              <Label>Budget Adjustment %</Label>
              <Input type="number" step="0.01" value={form.recommendedBudgetAdjustmentPercent ?? 0} onChange={(event) => update('recommendedBudgetAdjustmentPercent', Number(event.target.value))} />
            </div>
          </div>

          <div className="grid gap-4 lg:grid-cols-3">
            <div className="space-y-2">
              <Label>Market Availability</Label>
              <Select value={form.marketAvailability || 'Medium'} onValueChange={(value) => update('marketAvailability', value)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="High">High</SelectItem>
                  <SelectItem value="Medium">Medium</SelectItem>
                  <SelectItem value="Low">Low</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Supply Risk</Label>
              <Select value={form.supplyRiskLevel || 'Medium'} onValueChange={(value) => update('supplyRiskLevel', value)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="Low">Low</SelectItem>
                  <SelectItem value="Medium">Medium</SelectItem>
                  <SelectItem value="High">High</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Market Risk</Label>
              <Select value={form.marketRiskLevel || 'Medium'} onValueChange={(value) => update('marketRiskLevel', value)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="Low">Low</SelectItem>
                  <SelectItem value="Medium">Medium</SelectItem>
                  <SelectItem value="High">High</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid gap-4 lg:grid-cols-2">
            <div className="space-y-2">
              <Label>Risk Factors</Label>
              <Textarea rows={3} value={form.riskFactors || ''} onChange={(event) => update('riskFactors', event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Market Analysis Notes</Label>
              <Textarea rows={3} value={form.notes || ''} onChange={(event) => update('notes', event.target.value)} />
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
