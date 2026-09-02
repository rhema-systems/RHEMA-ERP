'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { ArrowLeft, Plus, Save, X } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';
import { procurementCurrencyService, type CurrencyListDto } from '@/services/financeCommonService';
import { inventoryManagementService, type InventoryCategoryDto } from '@/services/inventoryManagementService';
import {
  supplierConsolidationService,
  type CreateSupplierConsolidationDto,
  type SupplierConsolidationDetailDto,
} from '@/services/procurementPlanningService';

type SupplierConsolidationFormProps = {
  initialValue?: SupplierConsolidationDetailDto;
  mode: 'create' | 'edit';
};

const today = new Date().toISOString().slice(0, 10);
const yearEnd = new Date(new Date().getFullYear(), 11, 31).toISOString().slice(0, 10);

const emptyForm: CreateSupplierConsolidationDto = {
  title: '',
  description: '',
  itemCategory: '',
  analysisPeriodStart: today,
  analysisPeriodEnd: yearEnd,
  currentSupplierCount: 1,
  recommendedSupplierCount: 1,
  totalSpend: 0,
  potentialSavings: 0,
  currency: 'USD',
  opportunityLevel: 'Medium',
  recommendedStrategy: 'Maintain',
  strategyRationale: '',
  preferredSupplierIds: '',
  suppliersToPhaseOut: '',
  implementationPlan: '',
  notes: '',
};

const toDateInput = (value?: string) => value ? value.slice(0, 10) : '';

const parseIds = (value?: string): string[] => {
  if (!value) return [];
  try {
    const parsed = JSON.parse(value);
    return Array.isArray(parsed) ? parsed.filter(Boolean) : [];
  } catch {
    return value.split(',').map((item) => item.trim()).filter(Boolean);
  }
};

const toForm = (consolidation?: SupplierConsolidationDetailDto): CreateSupplierConsolidationDto => consolidation ? {
  title: consolidation.title,
  description: consolidation.description || '',
  itemCategory: consolidation.itemCategory || '',
  analysisPeriodStart: toDateInput(consolidation.analysisPeriodStart),
  analysisPeriodEnd: toDateInput(consolidation.analysisPeriodEnd),
  currentSupplierCount: consolidation.currentSupplierCount,
  recommendedSupplierCount: consolidation.recommendedSupplierCount,
  totalSpend: consolidation.totalSpend,
  potentialSavings: consolidation.potentialSavings,
  currency: consolidation.currency,
  opportunityLevel: consolidation.opportunityLevel,
  recommendedStrategy: consolidation.recommendedStrategy,
  strategyRationale: consolidation.strategyRationale || '',
  preferredSupplierIds: consolidation.preferredSupplierIds || '',
  suppliersToPhaseOut: consolidation.suppliersToPhaseOut || '',
  implementationPlan: consolidation.implementationPlan || '',
  notes: consolidation.notes || '',
} : emptyForm;

export function SupplierConsolidationForm({ initialValue, mode }: SupplierConsolidationFormProps) {
  const router = useRouter();
  const [form, setForm] = useState<CreateSupplierConsolidationDto>(() => toForm(initialValue));
  const [suppliers, setSuppliers] = useState<BusinessPartnerDto[]>([]);
  const [currencies, setCurrencies] = useState<CurrencyListDto[]>([]);
  const [categories, setCategories] = useState<InventoryCategoryDto[]>([]);
  const [selectedSupplierId, setSelectedSupplierId] = useState('');
  const [preferredSupplierIds, setPreferredSupplierIds] = useState<string[]>(() => parseIds(initialValue?.preferredSupplierIds));
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    Promise.all([
      businessPartnerService.getActivePartners('Supplier').catch(() => []),
      procurementCurrencyService.getActive().catch(() => []),
      inventoryManagementService.getActiveInventoryCategories().catch(() => []),
    ]).then(([supplierData, currencyData, categoryData]) => {
      setSuppliers(supplierData);
      setCurrencies(currencyData.filter((currency) => Boolean(currency.code)));
      setCategories(categoryData);
    });
  }, []);

  const selectedSuppliers = useMemo(
    () => preferredSupplierIds
      .map((id) => suppliers.find((supplier) => supplier.id === id))
      .filter((supplier): supplier is BusinessPartnerDto => Boolean(supplier)),
    [preferredSupplierIds, suppliers]
  );

  const update = (field: keyof CreateSupplierConsolidationDto, value: string | number | undefined) => {
    setForm((current) => ({ ...current, [field]: value }));
  };

  const currencyOptions = useMemo(() => {
    const currentCurrency = form.currency || 'USD';
    return currencies.some((currency) => currency.code === currentCurrency)
      ? currencies
      : [
          ...currencies,
          {
            id: currentCurrency,
            code: currentCurrency,
            name: currentCurrency,
            symbol: currentCurrency,
            decimalPlaces: 2,
            exchangeRate: 1,
            isBaseCurrency: false,
            isActive: true,
            displayOrder: 999,
          },
        ];
  }, [currencies, form.currency]);

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

  const addPreferredSupplier = () => {
    if (!selectedSupplierId || preferredSupplierIds.includes(selectedSupplierId)) return;
    setPreferredSupplierIds((current) => [...current, selectedSupplierId]);
    setSelectedSupplierId('');
  };

  const removePreferredSupplier = (supplierId: string) => {
    setPreferredSupplierIds((current) => current.filter((id) => id !== supplierId));
  };

  const submit = async () => {
    if (!form.title.trim()) {
      toast.error('Title is required');
      return;
    }

    try {
      setSaving(true);
      const payload: CreateSupplierConsolidationDto = {
        ...form,
        currentSupplierCount: Number(form.currentSupplierCount || 0),
        recommendedSupplierCount: Number(form.recommendedSupplierCount || 0),
        totalSpend: Number(form.totalSpend || 0),
        potentialSavings: Number(form.potentialSavings || 0),
        preferredSupplierIds: JSON.stringify(preferredSupplierIds),
      };
      const saved = mode === 'edit' && initialValue
        ? await supplierConsolidationService.updateConsolidation(initialValue.id, payload)
        : await supplierConsolidationService.createConsolidation(payload);
      toast.success(mode === 'edit' ? 'Consolidation updated' : 'Consolidation created');
      router.push(`/procurement/planning/supplier-consolidation/${saved.id}`);
    } catch (error) {
      console.error('Error saving consolidation:', error);
      toast.error('Failed to save consolidation');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold tracking-tight">{mode === 'edit' ? 'Edit Supplier Consolidation' : 'New Supplier Consolidation'}</h1>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => router.push('/procurement/planning/supplier-consolidation')}>
            <ArrowLeft className="mr-2 h-4 w-4" />
            Back
          </Button>
          <Button onClick={submit} disabled={saving}>
            <Save className="mr-2 h-4 w-4" />
            {saving ? 'Saving...' : 'Save'}
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader><CardTitle>Consolidation Strategy</CardTitle></CardHeader>
        <CardContent className="space-y-5">
          <div className="grid gap-4 lg:grid-cols-3">
            <div className="space-y-2 lg:col-span-2">
              <Label>Title</Label>
              <Input value={form.title} onChange={(event) => update('title', event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Currency</Label>
              <Select value={form.currency || 'USD'} onValueChange={(value) => update('currency', value)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {currencyOptions.map((currency) => (
                    <SelectItem key={currency.id || currency.code} value={currency.code}>
                      {currency.code} - {currency.name || currency.code}
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
              <Label>Analysis Start</Label>
              <Input type="date" value={form.analysisPeriodStart} onChange={(event) => update('analysisPeriodStart', event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Analysis End</Label>
              <Input type="date" value={form.analysisPeriodEnd} onChange={(event) => update('analysisPeriodEnd', event.target.value)} />
            </div>
          </div>

          <div className="grid gap-4 lg:grid-cols-4">
            <div className="space-y-2">
              <Label>Current Suppliers</Label>
              <Input type="number" min={0} value={form.currentSupplierCount ?? 0} onChange={(event) => update('currentSupplierCount', Number(event.target.value))} />
            </div>
            <div className="space-y-2">
              <Label>Recommended Suppliers</Label>
              <Input type="number" min={0} value={form.recommendedSupplierCount ?? 0} onChange={(event) => update('recommendedSupplierCount', Number(event.target.value))} />
            </div>
            <div className="space-y-2">
              <Label>Total Spend</Label>
              <Input type="number" min={0} step="0.01" value={form.totalSpend ?? 0} onChange={(event) => update('totalSpend', Number(event.target.value))} />
            </div>
            <div className="space-y-2">
              <Label>Potential Savings</Label>
              <Input type="number" min={0} step="0.01" value={form.potentialSavings ?? 0} onChange={(event) => update('potentialSavings', Number(event.target.value))} />
            </div>
          </div>

          <div className="grid gap-4 lg:grid-cols-3">
            <div className="space-y-2">
              <Label>Opportunity Level</Label>
              <Select value={form.opportunityLevel || 'Medium'} onValueChange={(value) => update('opportunityLevel', value)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="Low">Low</SelectItem>
                  <SelectItem value="Medium">Medium</SelectItem>
                  <SelectItem value="High">High</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Strategy</Label>
              <Select value={form.recommendedStrategy || 'Maintain'} onValueChange={(value) => update('recommendedStrategy', value)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="Maintain">Maintain</SelectItem>
                  <SelectItem value="Consolidate">Consolidate</SelectItem>
                  <SelectItem value="Diversify">Diversify</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Add Preferred Supplier</Label>
              <div className="flex gap-2">
                <Select value={selectedSupplierId} onValueChange={setSelectedSupplierId}>
                  <SelectTrigger><SelectValue placeholder="Select supplier" /></SelectTrigger>
                  <SelectContent>
                    {suppliers.map((supplier) => (
                      <SelectItem key={supplier.id} value={supplier.id}>{supplier.partnerName}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <Button type="button" variant="outline" size="icon" onClick={addPreferredSupplier}>
                  <Plus className="h-4 w-4" />
                </Button>
              </div>
            </div>
          </div>

          {selectedSuppliers.length > 0 && (
            <div className="flex flex-wrap gap-2">
              {selectedSuppliers.map((supplier) => (
                <Badge key={supplier.id} variant="secondary" className="gap-1">
                  {supplier.partnerName}
                  <button type="button" onClick={() => removePreferredSupplier(supplier.id)} aria-label={`Remove ${supplier.partnerName}`}>
                    <X className="h-3 w-3" />
                  </button>
                </Badge>
              ))}
            </div>
          )}

          <div className="grid gap-4 lg:grid-cols-2">
            <div className="space-y-2">
              <Label>Strategy Rationale</Label>
              <Textarea rows={3} value={form.strategyRationale || ''} onChange={(event) => update('strategyRationale', event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Implementation Plan</Label>
              <Textarea rows={3} value={form.implementationPlan || ''} onChange={(event) => update('implementationPlan', event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Suppliers To Phase Out</Label>
              <Textarea rows={2} value={form.suppliersToPhaseOut || ''} onChange={(event) => update('suppliersToPhaseOut', event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Notes</Label>
              <Textarea rows={2} value={form.notes || ''} onChange={(event) => update('notes', event.target.value)} />
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
