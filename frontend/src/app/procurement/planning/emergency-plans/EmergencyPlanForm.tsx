'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { ArrowLeft, Plus, Save, Search, Trash2 } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';
import { procurementCurrencyService, type CurrencyListDto } from '@/services/financeCommonService';
import {
  inventoryManagementService,
  type InventoryCategoryDto,
  type InventoryItemDto,
  type UnitOfMeasureDto,
} from '@/services/inventoryManagementService';
import {
  commonService,
  emergencyProcurementPlanService,
  type CreateEmergencyProcurementItemDto,
  type CreateEmergencyProcurementPlanDto,
  type CreateEmergencySupplierDto,
  type DepartmentDto,
  type EmergencyProcurementItemDto,
  type EmergencyProcurementPlanDetailDto,
  type EmergencySupplierDto,
} from '@/services/procurementPlanningService';

type EmergencyPlanFormProps = {
  initialValue?: EmergencyProcurementPlanDetailDto;
  mode: 'create' | 'edit';
};

const NO_DEPARTMENT = '__none__';
const today = new Date().toISOString().slice(0, 10);
const yearEnd = new Date(new Date().getFullYear(), 11, 31).toISOString().slice(0, 10);

const emergencyTypes = [
  'SupplyShortage',
  'CriticalShortage',
  'SupplyChainDisruption',
  'NaturalDisaster',
  'PandemicResponse',
  'EquipmentFailure',
  'Other',
];

const criticalityLevels = ['Medium', 'High', 'Critical'];

const emptyPlan: CreateEmergencyProcurementPlanDto = {
  title: '',
  description: '',
  departmentId: undefined,
  emergencyType: 'SupplyShortage',
  criticalityLevel: 'High',
  budgetReserve: 0,
  currency: 'USD',
  maxApprovalLimit: 0,
  rapidProcurementProcess: '',
  escalationContacts: '',
  effectiveDate: today,
  expiryDate: yearEnd,
  nextReviewDate: '',
  notes: '',
  criticalItems: [],
  emergencySuppliers: [],
};

const emptyItem: CreateEmergencyProcurementItemDto = {
  itemDescription: '',
  specifications: '',
  itemCategory: '',
  minimumStockLevel: 0,
  currentStockLevel: 0,
  emergencyOrderQuantity: 1,
  unitOfMeasure: 'EA',
  maxLeadTimeDays: 3,
  criticalityLevel: 'Critical',
  alternativeItems: '',
  notes: '',
};

const emptySupplier: CreateEmergencySupplierDto = {
  supplierId: undefined,
  supplierName: '',
  contactPerson: '',
  contactPhone: '',
  contactEmail: '',
  address: '',
  itemsProvided: '',
  responseTimeHours: 24,
  priority: 1,
  hasEmergencyContract: false,
  contractExpiryDate: '',
  paymentTerms: '',
  notes: '',
};

const toDateInput = (value?: string) => value ? value.slice(0, 10) : '';

const toPlanForm = (plan?: EmergencyProcurementPlanDetailDto): CreateEmergencyProcurementPlanDto => plan ? {
  title: plan.title,
  description: plan.description || '',
  departmentId: plan.departmentId,
  emergencyType: plan.emergencyType,
  criticalityLevel: plan.criticalityLevel,
  budgetReserve: plan.budgetReserve,
  currency: plan.currency,
  maxApprovalLimit: plan.maxApprovalLimit,
  rapidProcurementProcess: plan.rapidProcurementProcess || '',
  escalationContacts: plan.escalationContacts || '',
  effectiveDate: toDateInput(plan.effectiveDate || plan.validFrom),
  expiryDate: toDateInput(plan.expiryDate || plan.validTo),
  nextReviewDate: toDateInput(plan.nextReviewDate),
  notes: plan.notes || '',
  criticalItems: [],
  emergencySuppliers: [],
} : emptyPlan;

const formatDate = (value?: string) => value ? value.slice(0, 10) : '';

const getUnitOfMeasureValue = (unit: UnitOfMeasureDto) => unit.code?.trim() || unit.name?.trim() || unit.id;

const getUnitOfMeasureLabel = (unit: UnitOfMeasureDto) => {
  const value = getUnitOfMeasureValue(unit);
  const name = unit.name?.trim();
  const symbol = unit.symbol?.trim();

  if (name && name !== value) return symbol ? `${value} - ${name} (${symbol})` : `${value} - ${name}`;
  return symbol && symbol !== value ? `${value} (${symbol})` : value;
};

export function EmergencyPlanForm({ initialValue, mode }: EmergencyPlanFormProps) {
  const router = useRouter();
  const [form, setForm] = useState<CreateEmergencyProcurementPlanDto>(() => toPlanForm(initialValue));
  const [itemForm, setItemForm] = useState<CreateEmergencyProcurementItemDto>(emptyItem);
  const [supplierForm, setSupplierForm] = useState<CreateEmergencySupplierDto>(emptySupplier);
  const [departments, setDepartments] = useState<DepartmentDto[]>([]);
  const [currencies, setCurrencies] = useState<CurrencyListDto[]>([]);
  const [inventoryItems, setInventoryItems] = useState<InventoryItemDto[]>([]);
  const [categories, setCategories] = useState<InventoryCategoryDto[]>([]);
  const [unitsOfMeasure, setUnitsOfMeasure] = useState<UnitOfMeasureDto[]>([]);
  const [itemSearchTerm, setItemSearchTerm] = useState('');
  const [suppliers, setSuppliers] = useState<BusinessPartnerDto[]>([]);
  const [selectedInventoryItemId, setSelectedInventoryItemId] = useState('');
  const [selectedSupplierId, setSelectedSupplierId] = useState('');
  const [queuedItems, setQueuedItems] = useState<CreateEmergencyProcurementItemDto[]>([]);
  const [queuedSuppliers, setQueuedSuppliers] = useState<CreateEmergencySupplierDto[]>([]);
  const [existingItems, setExistingItems] = useState<EmergencyProcurementItemDto[]>(initialValue?.criticalItems || []);
  const [existingSuppliers, setExistingSuppliers] = useState<EmergencySupplierDto[]>(initialValue?.emergencySuppliers || []);
  const [saving, setSaving] = useState(false);
  const [rowActionId, setRowActionId] = useState<string | null>(null);

  useEffect(() => {
    Promise.all([
      commonService.getDepartments().catch(() => []),
      procurementCurrencyService.getActive().catch(() => []),
      inventoryManagementService.getInventoryItems({ isActive: true }).catch(() => []),
      inventoryManagementService.getActiveInventoryCategories().catch(() => []),
      inventoryManagementService.getUnitsOfMeasure(true).catch(() => []),
      businessPartnerService.getActivePartners('Supplier').catch(() => []),
    ]).then(([departmentData, currencyData, itemData, categoryData, unitData, supplierData]) => {
      setDepartments(departmentData.filter((department) => department.isActive !== false));
      setCurrencies(currencyData.filter((currency) => Boolean(currency.code)));
      setInventoryItems(itemData);
      setCategories(categoryData);
      setUnitsOfMeasure(unitData);
      setSuppliers(supplierData);
    });
  }, []);

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
    const currentCategory = itemForm.itemCategory?.trim();
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
  }, [categories, itemForm.itemCategory]);

  const unitOfMeasureOptions = useMemo(() => {
    const seen = new Set<string>();
    const options = unitsOfMeasure
      .map((unit) => ({ value: getUnitOfMeasureValue(unit), label: getUnitOfMeasureLabel(unit) }))
      .filter((option) => {
        if (!option.value || seen.has(option.value)) return false;
        seen.add(option.value);
        return true;
      });

    const currentUnit = itemForm.unitOfMeasure?.trim();
    if (currentUnit && !seen.has(currentUnit)) {
      options.unshift({ value: currentUnit, label: `${currentUnit} (saved value)` });
    }

    return options;
  }, [itemForm.unitOfMeasure, unitsOfMeasure]);

  const itemSearchQuery = itemSearchTerm.trim().toLowerCase();
  const filteredInventoryItems = useMemo(() => {
    if (itemSearchQuery.length < 2) return [];
    return inventoryItems
      .filter((item) =>
        item.name.toLowerCase().includes(itemSearchQuery) ||
        item.itemCode.toLowerCase().includes(itemSearchQuery) ||
        (item.categoryName || '').toLowerCase().includes(itemSearchQuery)
      )
      .slice(0, 12);
  }, [inventoryItems, itemSearchQuery]);

  const updatePlan = (field: keyof CreateEmergencyProcurementPlanDto, value: string | number | undefined) => {
    setForm((current) => ({ ...current, [field]: value }));
  };

  const updateItem = (field: keyof CreateEmergencyProcurementItemDto, value: string | number | undefined) => {
    setItemForm((current) => ({ ...current, [field]: value }));
  };

  const updateSupplier = (field: keyof CreateEmergencySupplierDto, value: string | number | boolean | undefined) => {
    setSupplierForm((current) => ({ ...current, [field]: value }));
  };

  const handleInventoryItemSelect = (itemId: string) => {
    const item = inventoryItems.find((entry) => entry.id === itemId);
    if (!item) return;

    setSelectedInventoryItemId(item.id);
    setItemSearchTerm(`${item.itemCode} - ${item.name}`);
    setItemForm((current) => ({
      ...current,
      itemDescription: item.name,
      specifications: item.description || item.shortDescription || current.specifications,
      itemCategory: item.categoryName || current.itemCategory,
      minimumStockLevel: item.minimumLevel || current.minimumStockLevel || 0,
      currentStockLevel: item.currentStock || current.currentStockLevel || 0,
      emergencyOrderQuantity: Math.max(item.reorderQuantity || item.reorderLevel || 1, 1),
      unitOfMeasure: item.unitOfMeasure || current.unitOfMeasure || 'EA',
      maxLeadTimeDays: Math.max(item.leadTimeDays || current.maxLeadTimeDays || 3, 1),
    }));
  };

  const handleSupplierSelect = (supplierId: string) => {
    const supplier = suppliers.find((entry) => entry.id === supplierId);
    if (!supplier) return;

    setSelectedSupplierId(supplier.id);
    setSupplierForm((current) => ({
      ...current,
      supplierId: supplier.id,
      supplierName: supplier.partnerName,
      contactPhone: supplier.phone || current.contactPhone,
      contactEmail: supplier.email || current.contactEmail,
      address: supplier.physicalAddress || current.address,
    }));
  };

  const resetItemForm = () => {
    setItemForm(emptyItem);
    setSelectedInventoryItemId('');
    setItemSearchTerm('');
  };

  const resetSupplierForm = () => {
    setSupplierForm(emptySupplier);
    setSelectedSupplierId('');
  };

  const addItem = async () => {
    if (!itemForm.itemDescription?.trim()) {
      toast.error('Item description is required');
      return;
    }

    if (!itemForm.unitOfMeasure?.trim()) {
      toast.error('Unit of measure is required');
      return;
    }

    if (Number(itemForm.emergencyOrderQuantity || 0) <= 0) {
      toast.error('Emergency quantity must be greater than 0');
      return;
    }

    const payload: CreateEmergencyProcurementItemDto = {
      ...itemForm,
      minimumStockLevel: Number(itemForm.minimumStockLevel || 0),
      currentStockLevel: Number(itemForm.currentStockLevel || 0),
      emergencyOrderQuantity: Number(itemForm.emergencyOrderQuantity || 0),
      maxLeadTimeDays: Number(itemForm.maxLeadTimeDays || 1),
    };

    if (mode === 'edit' && initialValue) {
      try {
        setRowActionId('item-new');
        const saved = await emergencyProcurementPlanService.addCriticalItem(initialValue.id, payload);
        setExistingItems((current) => [...current, saved]);
        toast.success('Emergency item added');
      } catch (error) {
        console.error('Error adding emergency item:', error);
        toast.error('Failed to add emergency item');
      } finally {
        setRowActionId(null);
      }
    } else {
      setQueuedItems((current) => [...current, payload]);
    }

    resetItemForm();
  };

  const addSupplier = async () => {
    if (!supplierForm.supplierName?.trim()) {
      toast.error('Select a supplier before adding it to the emergency plan');
      return;
    }

    const payload: CreateEmergencySupplierDto = {
      ...supplierForm,
      responseTimeHours: Number(supplierForm.responseTimeHours || 24),
      priority: Number(supplierForm.priority || 1),
      hasEmergencyContract: Boolean(supplierForm.hasEmergencyContract),
      contractExpiryDate: supplierForm.contractExpiryDate || undefined,
    };

    if (mode === 'edit' && initialValue) {
      try {
        setRowActionId('supplier-new');
        const saved = await emergencyProcurementPlanService.addEmergencySupplier(initialValue.id, payload);
        setExistingSuppliers((current) => [...current, saved]);
        toast.success('Emergency supplier added');
      } catch (error) {
        console.error('Error adding emergency supplier:', error);
        toast.error('Failed to add emergency supplier');
      } finally {
        setRowActionId(null);
      }
    } else {
      setQueuedSuppliers((current) => [...current, payload]);
    }

    resetSupplierForm();
  };

  const removeQueuedItem = (index: number) => {
    setQueuedItems((current) => current.filter((_, itemIndex) => itemIndex !== index));
  };

  const removeQueuedSupplier = (index: number) => {
    setQueuedSuppliers((current) => current.filter((_, supplierIndex) => supplierIndex !== index));
  };

  const removeExistingItem = async (itemId: string) => {
    if (!initialValue || !confirm('Remove this emergency item?')) return;
    try {
      setRowActionId(itemId);
      await emergencyProcurementPlanService.removeCriticalItem(initialValue.id, itemId);
      setExistingItems((current) => current.filter((item) => item.id !== itemId));
      toast.success('Emergency item removed');
    } catch (error) {
      console.error('Error removing emergency item:', error);
      toast.error('Failed to remove emergency item');
    } finally {
      setRowActionId(null);
    }
  };

  const removeExistingSupplier = async (supplierId: string) => {
    if (!initialValue || !confirm('Remove this emergency supplier?')) return;
    try {
      setRowActionId(supplierId);
      await emergencyProcurementPlanService.removeEmergencySupplier(initialValue.id, supplierId);
      setExistingSuppliers((current) => current.filter((supplier) => supplier.id !== supplierId));
      toast.success('Emergency supplier removed');
    } catch (error) {
      console.error('Error removing emergency supplier:', error);
      toast.error('Failed to remove emergency supplier');
    } finally {
      setRowActionId(null);
    }
  };

  const submit = async () => {
    if (!form.title?.trim()) {
      toast.error('Title is required');
      return;
    }

    try {
      setSaving(true);
      const payload: CreateEmergencyProcurementPlanDto = {
        ...form,
        departmentId: form.departmentId || undefined,
        budgetReserve: Number(form.budgetReserve || 0),
        maxApprovalLimit: Number(form.maxApprovalLimit || 0),
        effectiveDate: form.effectiveDate || undefined,
        expiryDate: form.expiryDate || undefined,
        nextReviewDate: form.nextReviewDate || undefined,
        criticalItems: mode === 'create' ? queuedItems : [],
        emergencySuppliers: mode === 'create' ? queuedSuppliers : [],
      };

      const saved = mode === 'edit' && initialValue
        ? await emergencyProcurementPlanService.updatePlan(initialValue.id, payload)
        : await emergencyProcurementPlanService.createPlan(payload);
      toast.success(mode === 'edit' ? 'Emergency plan updated' : 'Emergency plan created');
      router.push(`/procurement/planning/emergency-plans/${saved.id}`);
    } catch (error) {
      console.error('Error saving emergency plan:', error);
      toast.error('Failed to save emergency plan');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold tracking-tight">{mode === 'edit' ? 'Edit Emergency Plan' : 'New Emergency Plan'}</h1>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => router.push('/procurement/planning/emergency-plans')}>
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
        <CardHeader><CardTitle>Emergency Plan</CardTitle></CardHeader>
        <CardContent className="space-y-5">
          <div className="grid gap-4 lg:grid-cols-4">
            <div className="space-y-2 lg:col-span-2">
              <Label>Title</Label>
              <Input value={form.title || ''} onChange={(event) => updatePlan('title', event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Department</Label>
              <Select value={form.departmentId || NO_DEPARTMENT} onValueChange={(value) => updatePlan('departmentId', value === NO_DEPARTMENT ? undefined : value)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={NO_DEPARTMENT}>No department</SelectItem>
                  {departments.map((department) => (
                    <SelectItem key={department.id} value={department.id}>
                      {department.code ? `${department.code} - ${department.name}` : department.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Currency</Label>
              <Select value={form.currency || 'USD'} onValueChange={(value) => updatePlan('currency', value)}>
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
              <Label>Emergency Type</Label>
              <Select value={form.emergencyType || 'SupplyShortage'} onValueChange={(value) => updatePlan('emergencyType', value)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {emergencyTypes.map((type) => <SelectItem key={type} value={type}>{type.replace(/([A-Z])/g, ' $1').trim()}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Criticality</Label>
              <Select value={form.criticalityLevel || 'High'} onValueChange={(value) => updatePlan('criticalityLevel', value)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {criticalityLevels.map((level) => <SelectItem key={level} value={level}>{level}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Budget Reserve</Label>
              <Input type="number" min={0} step="0.01" value={form.budgetReserve ?? 0} onChange={(event) => updatePlan('budgetReserve', Number(event.target.value))} />
            </div>
            <div className="space-y-2">
              <Label>Max Approval Limit</Label>
              <Input type="number" min={0} step="0.01" value={form.maxApprovalLimit ?? 0} onChange={(event) => updatePlan('maxApprovalLimit', Number(event.target.value))} />
            </div>
          </div>

          <div className="grid gap-4 lg:grid-cols-3">
            <div className="space-y-2">
              <Label>Effective Date</Label>
              <Input type="date" value={form.effectiveDate || ''} onChange={(event) => updatePlan('effectiveDate', event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Expiry Date</Label>
              <Input type="date" value={form.expiryDate || ''} onChange={(event) => updatePlan('expiryDate', event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Next Review Date</Label>
              <Input type="date" value={form.nextReviewDate || ''} onChange={(event) => updatePlan('nextReviewDate', event.target.value)} />
            </div>
          </div>

          <div className="grid gap-4 lg:grid-cols-2">
            <div className="space-y-2 lg:col-span-2">
              <Label>Description</Label>
              <Textarea rows={2} value={form.description || ''} onChange={(event) => updatePlan('description', event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Rapid Procurement Process</Label>
              <Textarea rows={3} value={form.rapidProcurementProcess || ''} onChange={(event) => updatePlan('rapidProcurementProcess', event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Escalation Contacts</Label>
              <Textarea rows={3} value={form.escalationContacts || ''} onChange={(event) => updatePlan('escalationContacts', event.target.value)} />
            </div>
            <div className="space-y-2 lg:col-span-2">
              <Label>Notes</Label>
              <Textarea rows={2} value={form.notes || ''} onChange={(event) => updatePlan('notes', event.target.value)} />
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Critical Items</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 lg:grid-cols-6">
            <div className="relative space-y-2 lg:col-span-2">
              <Label>Product Search</Label>
              <div className="relative">
                <Search className="absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  className="pl-9"
                  value={itemSearchTerm}
                  onChange={(event) => setItemSearchTerm(event.target.value)}
                  placeholder="Search inventory item"
                />
              </div>
              {itemSearchTerm.trim().length >= 2 && (
                <div className="absolute left-0 right-0 top-full z-50 mt-1 max-h-56 overflow-y-auto rounded-md border bg-background shadow-lg">
                  {filteredInventoryItems.length === 0 ? (
                    <div className="px-3 py-4 text-center text-sm text-muted-foreground">No items found</div>
                  ) : (
                    <div className="divide-y">
                      {filteredInventoryItems.map((item) => (
                        <button
                          key={item.id}
                          type="button"
                          className={`block w-full px-3 py-2 text-left hover:bg-muted ${
                            selectedInventoryItemId === item.id ? 'bg-muted' : ''
                          }`}
                          onMouseDown={(event) => {
                            event.preventDefault();
                            handleInventoryItemSelect(item.id);
                          }}
                        >
                          <span className="block truncate text-sm font-medium">{item.name}</span>
                          <span className="block truncate text-xs text-muted-foreground">
                            {item.itemCode} | {item.categoryName || 'No Category'} | {item.unitOfMeasure || 'No UOM'}
                          </span>
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>
            <div className="space-y-2 lg:col-span-2">
              <Label>Item Description *</Label>
              <Input
                value={itemForm.itemDescription || ''}
                onChange={(event) => updateItem('itemDescription', event.target.value)}
                placeholder="Enter item description"
              />
            </div>
            <div className="space-y-2">
              <Label>Category</Label>
              <Select value={itemForm.itemCategory || undefined} onValueChange={(value) => updateItem('itemCategory', value)}>
                <SelectTrigger><SelectValue placeholder="Select category" /></SelectTrigger>
                <SelectContent>
                  {categoryOptions.map((category) => (
                    <SelectItem key={category.id} value={category.name}>{category.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>UOM *</Label>
              <Select value={itemForm.unitOfMeasure || undefined} onValueChange={(value) => updateItem('unitOfMeasure', value)}>
                <SelectTrigger><SelectValue placeholder="Select UOM" /></SelectTrigger>
                <SelectContent>
                  {unitOfMeasureOptions.map((unit) => (
                    <SelectItem key={unit.value} value={unit.value}>{unit.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Current Stock</Label>
              <Input type="number" min={0} step="0.01" value={itemForm.currentStockLevel ?? 0} onChange={(event) => updateItem('currentStockLevel', Number(event.target.value))} />
            </div>
            <div className="space-y-2">
              <Label>Minimum Stock</Label>
              <Input type="number" min={0} step="0.01" value={itemForm.minimumStockLevel ?? 0} onChange={(event) => updateItem('minimumStockLevel', Number(event.target.value))} />
            </div>
            <div className="space-y-2">
              <Label>Emergency Qty</Label>
              <Input type="number" min={0} step="0.01" value={itemForm.emergencyOrderQuantity ?? 1} onChange={(event) => updateItem('emergencyOrderQuantity', Number(event.target.value))} />
            </div>
            <div className="space-y-2">
              <Label>Max Lead Days</Label>
              <Input type="number" min={1} value={itemForm.maxLeadTimeDays ?? 3} onChange={(event) => updateItem('maxLeadTimeDays', Number(event.target.value))} />
            </div>
            <div className="space-y-2">
              <Label>Criticality</Label>
              <Select value={itemForm.criticalityLevel || 'Critical'} onValueChange={(value) => updateItem('criticalityLevel', value)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {criticalityLevels.map((level) => <SelectItem key={level} value={level}>{level}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2 lg:col-span-3">
              <Label>Specifications</Label>
              <Input value={itemForm.specifications || ''} onChange={(event) => updateItem('specifications', event.target.value)} />
            </div>
            <div className="flex items-end">
              <Button type="button" variant="outline" onClick={addItem} disabled={rowActionId === 'item-new'} className="w-full">
                <Plus className="mr-2 h-4 w-4" />
                Add
              </Button>
            </div>
          </div>

          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Item</TableHead>
                <TableHead>Category</TableHead>
                <TableHead>Stock</TableHead>
                <TableHead>Emergency Qty</TableHead>
                <TableHead>Lead Time</TableHead>
                <TableHead className="w-16"></TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {existingItems.map((item) => (
                <TableRow key={item.id}>
                  <TableCell className="font-medium">{item.itemDescription}</TableCell>
                  <TableCell>{item.itemCategory || '-'}</TableCell>
                  <TableCell>{item.currentStockLevel} / {item.minimumStockLevel} {item.unitOfMeasure}</TableCell>
                  <TableCell>{item.emergencyOrderQuantity} {item.unitOfMeasure}</TableCell>
                  <TableCell>{item.maxLeadTimeDays} days</TableCell>
                  <TableCell>
                    <Button variant="ghost" size="icon" onClick={() => removeExistingItem(item.id)} disabled={rowActionId === item.id} title="Remove item">
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
              {queuedItems.map((item, index) => (
                <TableRow key={`${item.itemDescription}-${index}`}>
                  <TableCell className="font-medium">{item.itemDescription}</TableCell>
                  <TableCell>{item.itemCategory || '-'}</TableCell>
                  <TableCell>{item.currentStockLevel || 0} / {item.minimumStockLevel || 0} {item.unitOfMeasure}</TableCell>
                  <TableCell>{item.emergencyOrderQuantity || 0} {item.unitOfMeasure}</TableCell>
                  <TableCell>{item.maxLeadTimeDays || 0} days</TableCell>
                  <TableCell>
                    <Button variant="ghost" size="icon" onClick={() => removeQueuedItem(index)} title="Remove item">
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
              {existingItems.length === 0 && queuedItems.length === 0 && (
                <TableRow><TableCell colSpan={6} className="text-center text-muted-foreground">No critical items added</TableCell></TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Emergency Suppliers</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 lg:grid-cols-6">
            <div className="space-y-2 lg:col-span-2">
              <Label>Supplier</Label>
              <Select value={selectedSupplierId || undefined} onValueChange={handleSupplierSelect}>
                <SelectTrigger><SelectValue placeholder={supplierForm.supplierName || 'Select supplier'} /></SelectTrigger>
                <SelectContent>
                  {suppliers.map((supplier) => (
                    <SelectItem key={supplier.id} value={supplier.id}>
                      {supplier.partnerName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Phone</Label>
              <Input value={supplierForm.contactPhone || ''} onChange={(event) => updateSupplier('contactPhone', event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Email</Label>
              <Input value={supplierForm.contactEmail || ''} onChange={(event) => updateSupplier('contactEmail', event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Response Hours</Label>
              <Input type="number" min={1} value={supplierForm.responseTimeHours ?? 24} onChange={(event) => updateSupplier('responseTimeHours', Number(event.target.value))} />
            </div>
            <div className="space-y-2">
              <Label>Priority</Label>
              <Input type="number" min={1} value={supplierForm.priority ?? 1} onChange={(event) => updateSupplier('priority', Number(event.target.value))} />
            </div>
            <div className="space-y-2 lg:col-span-2">
              <Label>Items Provided</Label>
              <Input value={supplierForm.itemsProvided || ''} onChange={(event) => updateSupplier('itemsProvided', event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Contract Expiry</Label>
              <Input type="date" value={formatDate(supplierForm.contractExpiryDate)} onChange={(event) => updateSupplier('contractExpiryDate', event.target.value)} />
            </div>
            <div className="flex items-center gap-2 pt-7">
              <Checkbox
                checked={Boolean(supplierForm.hasEmergencyContract)}
                onCheckedChange={(checked) => updateSupplier('hasEmergencyContract', checked === true)}
              />
              <Label>Emergency contract</Label>
            </div>
            <div className="flex items-end">
              <Button type="button" variant="outline" onClick={addSupplier} disabled={rowActionId === 'supplier-new'} className="w-full">
                <Plus className="mr-2 h-4 w-4" />
                Add
              </Button>
            </div>
          </div>

          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Supplier</TableHead>
                <TableHead>Contact</TableHead>
                <TableHead>Items</TableHead>
                <TableHead>Response</TableHead>
                <TableHead>Contract</TableHead>
                <TableHead className="w-16"></TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {existingSuppliers.map((supplier) => (
                <TableRow key={supplier.id}>
                  <TableCell className="font-medium">{supplier.supplierName}</TableCell>
                  <TableCell>{supplier.contactPhone || supplier.contactEmail || '-'}</TableCell>
                  <TableCell>{supplier.itemsProvided || '-'}</TableCell>
                  <TableCell>{supplier.responseTimeHours} hours</TableCell>
                  <TableCell>{supplier.hasEmergencyContract ? 'Yes' : 'No'}</TableCell>
                  <TableCell>
                    <Button variant="ghost" size="icon" onClick={() => removeExistingSupplier(supplier.id)} disabled={rowActionId === supplier.id} title="Remove supplier">
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
              {queuedSuppliers.map((supplier, index) => (
                <TableRow key={`${supplier.supplierName}-${index}`}>
                  <TableCell className="font-medium">{supplier.supplierName}</TableCell>
                  <TableCell>{supplier.contactPhone || supplier.contactEmail || '-'}</TableCell>
                  <TableCell>{supplier.itemsProvided || '-'}</TableCell>
                  <TableCell>{supplier.responseTimeHours || 24} hours</TableCell>
                  <TableCell>{supplier.hasEmergencyContract ? 'Yes' : 'No'}</TableCell>
                  <TableCell>
                    <Button variant="ghost" size="icon" onClick={() => removeQueuedSupplier(index)} title="Remove supplier">
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
              {existingSuppliers.length === 0 && queuedSuppliers.length === 0 && (
                <TableRow><TableCell colSpan={6} className="text-center text-muted-foreground">No emergency suppliers added</TableCell></TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}
