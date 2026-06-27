'use client';

import { useEffect, useMemo, useState } from 'react';
import {
  Edit,
  Loader2,
  Plus,
  RefreshCw,
  Save,
  Settings,
  Trash2,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
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
import { useToast } from '@/hooks/use-toast';
import {
  salesSetupService,
  type SalesSaleableSourceDto,
  type UpsertSalesSaleableSourceDto,
} from '@/services/salesSetupService';
import {
  inventoryWarehouseService,
  type InventoryWarehouseLocationOption,
  type InventoryWarehouseOption,
} from '@/services/inventoryWarehouseService';

const defaultForm: UpsertSalesSaleableSourceDto = {
  code: '',
  displayName: '',
  description: '',
  sourceType: 'ProjectUnits',
  adapterKey: 'project-units',
  isActive: true,
  icon: 'Package',
  colorCode: '#2563EB',
  sortOrder: 10,
  supportedTransactionTypes: 'SalesOrder',
  defaultCurrency: 'GHS',
  defaultWorkflowEntityType: 'SalesOrder',
  allowSalesOrders: true,
  allowSalesAgreements: false,
  allowReservations: true,
  requiresExternalModule: false,
  settingsJson: '',
};

const sourceTypes = [
  { value: 'ProjectUnits', label: 'Project Units', adapterKey: 'project-units', icon: 'Building2' },
  { value: 'Inventory', label: 'Inventory', adapterKey: 'inventory', icon: 'Package' },
  { value: 'PropertyRegister', label: 'Property Register', adapterKey: 'property-register', icon: 'Home' },
  { value: 'AssetRegister', label: 'Asset Register', adapterKey: 'asset-register', icon: 'Boxes' },
  { value: 'LandManagement', label: 'Land Management', adapterKey: 'land-management', icon: 'Map' },
  { value: 'Custom', label: 'Custom', adapterKey: 'custom', icon: 'Layers3' },
];

const transactionOptions = ['SalesOrder', 'SalesAgreement', 'LeaseAgreement', 'Reservation', 'PlotAllocation'];
const emptySelectValue = '__none';

type SaleableSourceFilter = {
  field?: string;
  value?: string;
};

type SaleableSourceSettings = {
  source?: string;
  warehouseId?: string;
  warehouseName?: string;
  locationId?: string;
  locationName?: string;
  filterField?: string;
  filterValue?: string;
  filters?: SaleableSourceFilter[];
  [key: string]: unknown;
};

const filterOptionsBySourceType: Record<string, { value: string; label: string }[]> = {
  Inventory: [
    { value: 'inventoryType', label: 'Inventory Type' },
    { value: 'status', label: 'Status' },
    { value: 'categoryName', label: 'Category' },
    { value: 'categoryId', label: 'Category ID' },
    { value: 'brand', label: 'Brand' },
    { value: 'manufacturer', label: 'Manufacturer' },
    { value: 'model', label: 'Model' },
    { value: 'unitOfMeasure', label: 'Unit of Measure' },
    { value: 'isSerialTracked', label: 'Serial Tracked' },
    { value: 'isLotTracked', label: 'Lot Tracked' },
    { value: 'isLocationTracked', label: 'Location Tracked' },
  ],
  ProjectUnits: [
    { value: 'status', label: 'Status' },
    { value: 'projectCode', label: 'Project Code' },
    { value: 'unitType', label: 'Unit Type' },
  ],
  PropertyRegister: [
    { value: 'status', label: 'Status' },
    { value: 'propertyType', label: 'Property Type' },
    { value: 'location', label: 'Location' },
  ],
  AssetRegister: [
    { value: 'status', label: 'Status' },
    { value: 'assetType', label: 'Asset Type' },
    { value: 'location', label: 'Location' },
  ],
  LandManagement: [
    { value: 'status', label: 'Status' },
    { value: 'plotType', label: 'Plot Type' },
    { value: 'location', label: 'Location' },
  ],
  Custom: [
    { value: 'status', label: 'Status' },
    { value: 'type', label: 'Type' },
    { value: 'category', label: 'Category' },
  ],
};

const splitTransactionTypes = (value?: string) =>
  (value || '')
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);

const parseSourceSettings = (settingsJson?: string): SaleableSourceSettings => {
  if (!settingsJson?.trim()) {
    return {};
  }

  try {
    const parsed = JSON.parse(settingsJson) as SaleableSourceSettings;
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
};

const stringifySourceSettings = (settings: SaleableSourceSettings) => {
  const cleaned = Object.fromEntries(
    Object.entries(settings).filter(([, value]) => {
      if (value === undefined || value === null || value === '') return false;
      if (Array.isArray(value)) return value.length > 0;
      return true;
    }),
  );

  return Object.keys(cleaned).length > 0 ? JSON.stringify(cleaned) : '';
};

const sourceSettingName = (sourceType: string) => {
  const selected = sourceTypes.find((type) => type.value === sourceType);
  return selected?.adapterKey || sourceType;
};

const toForm = (source: SalesSaleableSourceDto): UpsertSalesSaleableSourceDto => ({
  code: source.code,
  displayName: source.displayName,
  description: source.description || '',
  sourceType: source.sourceType,
  adapterKey: source.adapterKey,
  isActive: source.isActive,
  icon: source.icon || 'Package',
  colorCode: source.colorCode || '#2563EB',
  sortOrder: source.sortOrder,
  supportedTransactionTypes: source.supportedTransactionTypes || 'SalesOrder',
  defaultCurrency: source.defaultCurrency || 'GHS',
  defaultWorkflowEntityType: source.defaultWorkflowEntityType || '',
  allowSalesOrders: source.allowSalesOrders,
  allowSalesAgreements: source.allowSalesAgreements,
  allowReservations: source.allowReservations,
  requiresExternalModule: source.requiresExternalModule,
  settingsJson: source.settingsJson || '',
});

export default function SalesAdministrationPage() {
  const { toast } = useToast();
  const [sources, setSources] = useState<SalesSaleableSourceDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingSource, setEditingSource] = useState<SalesSaleableSourceDto | null>(null);
  const [form, setForm] = useState<UpsertSalesSaleableSourceDto>(defaultForm);
  const [warehouses, setWarehouses] = useState<InventoryWarehouseOption[]>([]);
  const [locations, setLocations] = useState<InventoryWarehouseLocationOption[]>([]);
  const [loadingInventoryScope, setLoadingInventoryScope] = useState(false);

  const loadSources = async () => {
    try {
      setLoading(true);
      const data = await salesSetupService.getSaleableSources(true);
      setSources(data);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to load sales setup.',
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadSources();
  }, []);

  useEffect(() => {
    let mounted = true;
    (async () => {
      try {
        const data = await inventoryWarehouseService.getActiveWarehouses();
        if (mounted) {
          setWarehouses(data.filter((warehouse) => warehouse.isActive));
        }
      } catch {
        if (mounted) {
          setWarehouses([]);
        }
      }
    })();

    return () => {
      mounted = false;
    };
  }, []);

  const sourceSettings = useMemo(() => parseSourceSettings(form.settingsJson), [form.settingsJson]);
  const selectedWarehouseId = typeof sourceSettings.warehouseId === 'string' ? sourceSettings.warehouseId : '';
  const selectedLocationId = typeof sourceSettings.locationId === 'string' ? sourceSettings.locationId : '';
  const selectedFilterField = typeof sourceSettings.filterField === 'string'
    ? sourceSettings.filterField
    : sourceSettings.filters?.[0]?.field || '';
  const selectedFilterValue = typeof sourceSettings.filterValue === 'string'
    ? sourceSettings.filterValue
    : sourceSettings.filters?.[0]?.value || '';
  const filterOptions = filterOptionsBySourceType[form.sourceType] || filterOptionsBySourceType.Custom;

  useEffect(() => {
    if (!dialogOpen || form.sourceType !== 'Inventory' || !selectedWarehouseId) {
      setLocations([]);
      return;
    }

    let mounted = true;
    (async () => {
      try {
        setLoadingInventoryScope(true);
        const data = await inventoryWarehouseService.getWarehouseLocations(selectedWarehouseId);
        if (mounted) {
          setLocations(data.filter((location) => location.isActive));
        }
      } catch {
        if (mounted) {
          setLocations([]);
        }
      } finally {
        if (mounted) {
          setLoadingInventoryScope(false);
        }
      }
    })();

    return () => {
      mounted = false;
    };
  }, [dialogOpen, form.sourceType, selectedWarehouseId]);

  const filteredSources = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase();
    return sources.filter((source) => {
      const matchesSearch = !normalizedSearch
        || source.displayName.toLowerCase().includes(normalizedSearch)
        || source.code.toLowerCase().includes(normalizedSearch)
        || source.sourceType.toLowerCase().includes(normalizedSearch)
        || source.adapterKey.toLowerCase().includes(normalizedSearch);
      const matchesStatus = statusFilter === 'all'
        || (statusFilter === 'active' && source.isActive)
        || (statusFilter === 'inactive' && !source.isActive);

      return matchesSearch && matchesStatus;
    });
  }, [search, sources, statusFilter]);

  const openCreateDialog = () => {
    setEditingSource(null);
    setForm(defaultForm);
    setDialogOpen(true);
  };

  const openEditDialog = (source: SalesSaleableSourceDto) => {
    setEditingSource(source);
    setForm(toForm(source));
    setDialogOpen(true);
  };

  const updateSourceSettings = (updater: (settings: SaleableSourceSettings) => SaleableSourceSettings) => {
    setForm((current) => {
      const currentSettings = parseSourceSettings(current.settingsJson);
      const nextSettings = updater(currentSettings);
      return {
        ...current,
        settingsJson: stringifySourceSettings(nextSettings),
      };
    });
  };

  const updateFilterSetting = (field: string, value: string) => {
    updateSourceSettings((settings) => ({
      ...settings,
      source: settings.source || sourceSettingName(form.sourceType),
      filterField: field || undefined,
      filterValue: value || undefined,
      filters: field && value ? [{ field, value }] : [],
    }));
  };

  const updateInventoryWarehouse = (warehouseId: string) => {
    const selected = warehouses.find((warehouse) => warehouse.id === warehouseId);
    updateSourceSettings((settings) => ({
      ...settings,
      source: 'inventory',
      warehouseId: selected ? selected.id : undefined,
      warehouseName: selected ? selected.name : undefined,
      locationId: undefined,
      locationName: undefined,
    }));
  };

  const updateInventoryLocation = (locationId: string) => {
    const selected = locations.find((location) => location.id === locationId);
    updateSourceSettings((settings) => ({
      ...settings,
      source: 'inventory',
      locationId: selected ? selected.id : undefined,
      locationName: selected ? `${selected.locationCode}${selected.name ? ` - ${selected.name}` : ''}` : undefined,
    }));
  };

  const handleSourceTypeChange = (value: string) => {
    const selected = sourceTypes.find((type) => type.value === value);
    setForm((current) => ({
      ...current,
      sourceType: value,
      adapterKey: selected?.adapterKey || current.adapterKey,
      icon: selected?.icon || current.icon,
      allowSalesOrders: value === 'Inventory' ? true : current.allowSalesOrders,
      allowSalesAgreements: value === 'Inventory' ? false : current.allowSalesAgreements,
      allowReservations: value === 'Inventory' ? true : current.allowReservations,
      supportedTransactionTypes: value === 'Inventory' ? 'SalesOrder,Reservation' : current.supportedTransactionTypes,
      settingsJson: stringifySourceSettings({
        ...parseSourceSettings(current.settingsJson),
        source: value === 'Inventory' ? 'inventory' : sourceSettingName(value),
        warehouseId: value === 'Inventory' ? parseSourceSettings(current.settingsJson).warehouseId : undefined,
        warehouseName: value === 'Inventory' ? parseSourceSettings(current.settingsJson).warehouseName : undefined,
        locationId: value === 'Inventory' ? parseSourceSettings(current.settingsJson).locationId : undefined,
        locationName: value === 'Inventory' ? parseSourceSettings(current.settingsJson).locationName : undefined,
      }),
    }));
  };

  const toggleTransactionType = (type: string, checked: boolean) => {
    setForm((current) => {
      const values = new Set(splitTransactionTypes(current.supportedTransactionTypes));
      if (checked) {
        values.add(type);
      } else {
        values.delete(type);
      }

      return {
        ...current,
        supportedTransactionTypes: Array.from(values).join(','),
      };
    });
  };

  const saveSource = async () => {
    if (!form.code.trim() || !form.displayName.trim() || !form.adapterKey.trim()) {
      toast({
        title: 'Validation Error',
        description: 'Code, display name, and adapter key are required.',
        variant: 'destructive',
      });
      return;
    }

    try {
      setSaving(true);
      if (editingSource) {
        await salesSetupService.updateSaleableSource(editingSource.id, form);
      } else {
        await salesSetupService.createSaleableSource(form);
      }

      toast({
        title: 'Success',
        description: editingSource ? 'Saleable source updated.' : 'Saleable source created.',
      });
      setDialogOpen(false);
      await loadSources();
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to save saleable source.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const seedDefaults = async () => {
    try {
      setSaving(true);
      await salesSetupService.seedDefaultSaleableSources();
      await loadSources();
      toast({ title: 'Success', description: 'Default sales sources refreshed.' });
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to seed default sources.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const toggleActive = async (source: SalesSaleableSourceDto) => {
    try {
      setSaving(true);
      if (source.isActive) {
        await salesSetupService.deactivateSaleableSource(source.id);
      } else {
        await salesSetupService.activateSaleableSource(source.id);
      }
      await loadSources();
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to update source status.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const deleteSource = async (source: SalesSaleableSourceDto) => {
    if (!confirm(`Delete ${source.displayName}?`)) {
      return;
    }

    try {
      setSaving(true);
      await salesSetupService.deleteSaleableSource(source.id);
      await loadSources();
      toast({ title: 'Success', description: 'Saleable source deleted.' });
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to delete saleable source.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const activeCount = sources.filter((source) => source.isActive).length;

  return (
    <div className="container mx-auto py-6 space-y-5">
      <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div>
          <h1 className="flex items-center gap-2 text-3xl font-bold tracking-tight">
            <Settings className="h-7 w-7 text-slate-700" />
            Sales Setup
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Configure saleable inventory sources for Sales transactions.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={seedDefaults} disabled={saving}>
            {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-2 h-4 w-4" />}
            Seed Defaults
          </Button>
          <Button onClick={openCreateDialog}>
            <Plus className="mr-2 h-4 w-4" />
            Add Source
          </Button>
        </div>
      </div>

      <div className="grid gap-3 md:grid-cols-3">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Active Sources</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-semibold">{activeCount}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Configured Sources</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-semibold">{sources.length}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">External Placeholders</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-semibold">
              {sources.filter((source) => source.requiresExternalModule).length}
            </div>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader className="space-y-4">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
            <CardTitle>Saleable Sources</CardTitle>
            <div className="flex flex-col gap-2 sm:flex-row">
              <Input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Search sources"
                className="w-full sm:w-72"
              />
              <Select value={statusFilter} onValueChange={setStatusFilter}>
                <SelectTrigger className="w-full sm:w-40">
                  <SelectValue placeholder="Status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All statuses</SelectItem>
                  <SelectItem value="active">Active</SelectItem>
                  <SelectItem value="inactive">Inactive</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex h-48 items-center justify-center">
              <Loader2 className="h-7 w-7 animate-spin text-slate-600" />
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Source</TableHead>
                  <TableHead>Adapter</TableHead>
                  <TableHead>Transactions</TableHead>
                  <TableHead>Currency</TableHead>
                  <TableHead className="w-28 text-center">Active</TableHead>
                  <TableHead className="w-28 text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredSources.map((source) => (
                  <TableRow key={source.id}>
                    <TableCell>
                      <div className="flex items-center gap-3">
                        <span
                          className="h-3 w-3 rounded-full"
                          style={{ backgroundColor: source.colorCode || '#2563EB' }}
                        />
                        <div>
                          <div className="font-medium">{source.displayName}</div>
                          <div className="text-xs text-muted-foreground">{source.code}</div>
                        </div>
                      </div>
                    </TableCell>
                    <TableCell>
                      <div className="font-medium">{source.sourceType}</div>
                      <div className="text-xs text-muted-foreground">{source.adapterKey}</div>
                    </TableCell>
                    <TableCell>
                      <div className="flex flex-wrap gap-1">
                        {splitTransactionTypes(source.supportedTransactionTypes).map((type) => (
                          <Badge key={type} variant="secondary" className="text-xs">
                            {type}
                          </Badge>
                        ))}
                        {source.requiresExternalModule ? (
                          <Badge variant="outline" className="text-xs">
                            External
                          </Badge>
                        ) : null}
                      </div>
                    </TableCell>
                    <TableCell>{source.defaultCurrency}</TableCell>
                    <TableCell className="text-center">
                      <Switch
                        checked={source.isActive}
                        onCheckedChange={() => toggleActive(source)}
                        disabled={saving}
                        aria-label={`Toggle ${source.displayName}`}
                      />
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-1">
                        <Button variant="ghost" size="icon" onClick={() => openEditDialog(source)}>
                          <Edit className="h-4 w-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="icon"
                          onClick={() => deleteSource(source)}
                          disabled={source.isSystemSource || saving}
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
                {filteredSources.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6} className="h-24 text-center text-sm text-muted-foreground">
                      No saleable sources found.
                    </TableCell>
                  </TableRow>
                ) : null}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-h-[90vh] max-w-4xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{editingSource ? 'Edit Saleable Source' : 'Add Saleable Source'}</DialogTitle>
          </DialogHeader>

          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="displayName">Display Name</Label>
              <Input
                id="displayName"
                value={form.displayName}
                onChange={(event) => setForm((current) => ({ ...current, displayName: event.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="code">Code</Label>
              <Input
                id="code"
                value={form.code}
                onChange={(event) => setForm((current) => ({ ...current, code: event.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Source Type</Label>
              <Select value={form.sourceType} onValueChange={handleSourceTypeChange}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {sourceTypes.map((type) => (
                    <SelectItem key={type.value} value={type.value}>
                      {type.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="adapterKey">Adapter Key</Label>
              <Input
                id="adapterKey"
                value={form.adapterKey}
                onChange={(event) => setForm((current) => ({ ...current, adapterKey: event.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="defaultCurrency">Default Currency</Label>
              <Input
                id="defaultCurrency"
                value={form.defaultCurrency}
                onChange={(event) => setForm((current) => ({ ...current, defaultCurrency: event.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="defaultWorkflowEntityType">Workflow Entity</Label>
              <Input
                id="defaultWorkflowEntityType"
                value={form.defaultWorkflowEntityType || ''}
                onChange={(event) => setForm((current) => ({ ...current, defaultWorkflowEntityType: event.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="sortOrder">Sort Order</Label>
              <Input
                id="sortOrder"
                type="number"
                value={form.sortOrder}
                onChange={(event) => setForm((current) => ({ ...current, sortOrder: Number(event.target.value || 0) }))}
              />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label htmlFor="icon">Icon</Label>
                <Input
                  id="icon"
                  value={form.icon}
                  onChange={(event) => setForm((current) => ({ ...current, icon: event.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="colorCode">Color</Label>
                <Input
                  id="colorCode"
                  type="color"
                  value={form.colorCode || '#2563EB'}
                  onChange={(event) => setForm((current) => ({ ...current, colorCode: event.target.value }))}
                />
              </div>
            </div>
          </div>

          <div className="space-y-4 rounded-md border p-4">
            <div>
              <Label>Source Record Filter</Label>
              <p className="text-xs text-muted-foreground">
                Optional filter applied by the selected source adapter when saleable records are listed.
              </p>
            </div>
            <div className="grid gap-3 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Filter Parameter</Label>
                <Select
                  value={selectedFilterField || emptySelectValue}
                  onValueChange={(value) => updateFilterSetting(value === emptySelectValue ? '' : value, selectedFilterValue)}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="No filter" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={emptySelectValue}>No filter</SelectItem>
                    {filterOptions.map((option) => (
                      <SelectItem key={option.value} value={option.value}>
                        {option.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="filterValue">Filter Value</Label>
                <Input
                  id="filterValue"
                  value={selectedFilterValue}
                  onChange={(event) => updateFilterSetting(selectedFilterField, event.target.value)}
                  placeholder={form.sourceType === 'Inventory' ? 'Example: stocks, Active, ICT' : 'Optional value'}
                  disabled={!selectedFilterField}
                />
              </div>
            </div>

            {form.sourceType === 'Inventory' ? (
              <div className="grid gap-3 md:grid-cols-2">
                <div className="space-y-2">
                  <Label>Warehouse / Location</Label>
                  <Select
                    value={selectedWarehouseId || emptySelectValue}
                    onValueChange={(value) => updateInventoryWarehouse(value === emptySelectValue ? '' : value)}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="All warehouses" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={emptySelectValue}>All warehouses</SelectItem>
                      {warehouses.map((warehouse) => (
                        <SelectItem key={warehouse.id} value={warehouse.id}>
                          {warehouse.code ? `${warehouse.code} - ${warehouse.name}` : warehouse.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Bin / Location</Label>
                  <Select
                    value={selectedLocationId || emptySelectValue}
                    onValueChange={(value) => updateInventoryLocation(value === emptySelectValue ? '' : value)}
                    disabled={!selectedWarehouseId || loadingInventoryScope}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder={loadingInventoryScope ? 'Loading locations' : 'All locations'} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={emptySelectValue}>All locations</SelectItem>
                      {locations.map((location) => (
                        <SelectItem key={location.id} value={location.id}>
                          {location.locationCode}
                          {location.name ? ` - ${location.name}` : ''}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
            ) : null}
          </div>

          <div className="grid gap-4 md:grid-cols-[1fr_1.2fr]">
            <div className="space-y-3 rounded-md border p-4">
              <div className="flex items-center justify-between gap-3">
                <Label htmlFor="isActive">Active</Label>
                <Switch id="isActive" checked={form.isActive} onCheckedChange={(checked) => setForm((current) => ({ ...current, isActive: checked }))} />
              </div>
              <div className="flex items-center justify-between gap-3">
                <Label htmlFor="allowSalesOrders">Sales Orders</Label>
                <Switch id="allowSalesOrders" checked={form.allowSalesOrders} onCheckedChange={(checked) => setForm((current) => ({ ...current, allowSalesOrders: checked }))} />
              </div>
              <div className="flex items-center justify-between gap-3">
                <Label htmlFor="allowSalesAgreements">Agreements</Label>
                <Switch id="allowSalesAgreements" checked={form.allowSalesAgreements} onCheckedChange={(checked) => setForm((current) => ({ ...current, allowSalesAgreements: checked }))} />
              </div>
              <div className="flex items-center justify-between gap-3">
                <Label htmlFor="allowReservations">Reservations</Label>
                <Switch id="allowReservations" checked={form.allowReservations} onCheckedChange={(checked) => setForm((current) => ({ ...current, allowReservations: checked }))} />
              </div>
              <div className="flex items-center justify-between gap-3">
                <Label htmlFor="requiresExternalModule">External Module</Label>
                <Switch id="requiresExternalModule" checked={form.requiresExternalModule} onCheckedChange={(checked) => setForm((current) => ({ ...current, requiresExternalModule: checked }))} />
              </div>
            </div>

            <div className="space-y-3 rounded-md border p-4">
              <Label>Transaction Types</Label>
              <div className="grid grid-cols-2 gap-2">
                {transactionOptions.map((type) => (
                  <label key={type} className="flex items-center gap-2 rounded-md border px-3 py-2 text-sm">
                    <input
                      type="checkbox"
                      checked={splitTransactionTypes(form.supportedTransactionTypes).includes(type)}
                      onChange={(event) => toggleTransactionType(type, event.target.checked)}
                    />
                    {type}
                  </label>
                ))}
              </div>
            </div>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="description">Description</Label>
              <Textarea
                id="description"
                value={form.description || ''}
                rows={4}
                onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="settingsJson">Settings JSON (Advanced)</Label>
              <Textarea
                id="settingsJson"
                value={form.settingsJson || ''}
                rows={4}
                onChange={(event) => setForm((current) => ({ ...current, settingsJson: event.target.value }))}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)} disabled={saving}>
              Cancel
            </Button>
            <Button onClick={saveSource} disabled={saving}>
              {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
              Save Source
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
