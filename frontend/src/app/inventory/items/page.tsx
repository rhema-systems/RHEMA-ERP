'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  Plus, Search, Edit, Trash2, Package, AlertTriangle, TrendingUp,
  Users, Eye, BarChart3, Filter, DollarSign, ImageIcon, Upload, X, History
} from 'lucide-react';
import { fileUploadService, UploadedFile } from '@/services/fileUploadService';
import {
  inventoryManagementService,
  InventoryItemDto, CreateInventoryItemDto, UpdateInventoryItemDto,
  ItemSupplierDto, UnitOfMeasureDto, InventoryCategoryDto,
  UnitOfMeasureScheduleDto, InventoryItemChangeAuditDto
} from '@/services/inventoryManagementService';
import { priceListService, ItemPriceListLineDto, getPriceListTypeLabel, getPriceListStatusLabel } from '@/services/priceListService';
import { useInventoryItemLabels } from '@/hooks/useFieldLabels';
import { format } from 'date-fns';
import { toast } from 'sonner';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { InventoryCostValue } from '@/components/inventory/InventoryCostValue';
import { useInventoryCostCurrency } from '@/hooks/useInventoryCostCurrency';

const ItemTypes = [
  { value: 1, label: 'Stock Item' },
  { value: 2, label: 'Service' },
  { value: 3, label: 'Non-stock Item' },
  { value: 4, label: 'Fixed Asset' }
];

const ItemStatuses = [
  { value: 1, label: 'Active' },
  { value: 2, label: 'Inactive' },
  { value: 3, label: 'Discontinued' },
  { value: 4, label: 'Obsolete' }
];

const ValuationMethods = [
  { value: '1', label: 'Weighted Average' },
  { value: '2', label: 'FIFO Perpetual' },
  { value: '3', label: 'LIFO Perpetual' },
  { value: '4', label: 'Standard Cost' }
];

const TaxOptions = [
  { value: 'Taxable', label: 'Taxable' },
  { value: 'Nontaxable', label: 'Nontaxable' },
  { value: 'BasedOnVendor', label: 'Base on Vendor' },
  { value: 'BasedOnCustomer', label: 'Base on Customer' }
];

const ABCCodes = [
  { value: 'A', label: 'A - High Value' },
  { value: 'B', label: 'B - Medium Value' },
  { value: 'C', label: 'C - Low Value' }
];

// Helper to get full image URL (prepend backend URL if relative path)
const getFullImageUrl = (url: string | null | undefined): string | null => {
  if (!url) return null;
  if (url.startsWith('/uploads')) {
    const backendBaseUrl = (process.env.NEXT_PUBLIC_API_URL || '/api').replace('/api', '');
    return `${backendBaseUrl}${url}`;
  }
  return url;
};

export default function InventoryItemsPage() {
  const costCurrency = useInventoryCostCurrency();
  // Get configurable field labels
  const { getLabel } = useInventoryItemLabels();
  
  const [items, setItems] = useState<InventoryItemDto[]>([]);
  const [filteredItems, setFilteredItems] = useState<InventoryItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [typeFilter, setTypeFilter] = useState('all');
  const [statusFilter, setStatusFilter] = useState('all');
  const [selectedItem, setSelectedItem] = useState<InventoryItemDto | null>(null);
  const [suppliers, setSuppliers] = useState<ItemSupplierDto[]>([]);
  const [unitsOfMeasure, setUnitsOfMeasure] = useState<UnitOfMeasureDto[]>([]);
  const [uomSchedules, setUomSchedules] = useState<UnitOfMeasureScheduleDto[]>([]);
  const [categories, setCategories] = useState<InventoryCategoryDto[]>([]);

  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [isSupplierDialogOpen, setIsSupplierDialogOpen] = useState(false);
  const [isImportDialogOpen, setIsImportDialogOpen] = useState(false);
  const [isHistoryDialogOpen, setIsHistoryDialogOpen] = useState(false);
  const [historyEntries, setHistoryEntries] = useState<InventoryItemChangeAuditDto[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [importJson, setImportJson] = useState('[]');
  const [isImporting, setIsImporting] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<InventoryItemDto | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  // For edit mode - track isActive separately
  const [editIsActive, setEditIsActive] = useState(true);

  // Price list lines for the selected item
  const [itemPriceListLines, setItemPriceListLines] = useState<ItemPriceListLineDto[]>([]);
  const [loadingPriceLines, setLoadingPriceLines] = useState(false);
  
  const [formData, setFormData] = useState<CreateInventoryItemDto>({
    // Basic Info
    itemCode: '', name: '', description: '', categoryId: '', unitOfMeasure: 'EA',
    unitOfMeasureScheduleId: undefined,
    valuationMethod: 1, // WeightedAverage
    itemType: 1, status: 1, quantityDecimals: 2, currencyDecimals: 2,
    isProjectApplicable: false, isCostCentreApplicable: false,
    // Costs & Pricing
    standardCost: 0, currentCost: 0, listPrice: 0,
    // Stock Settings
    minimumLevel: 0, maximumLevel: 1000, reorderLevel: 10, reorderQuantity: 50,
    safetyStock: 0, leadTimeDays: 7, safetyLeadTimeDays: 0, allowBackorder: false, autoReorder: false,
    // Tracking
    isSerialTracked: false, isLotTracked: false, isBatchTracked: false, isManufactureDateTracked: false, isExpirationTracked: false, isLocationTracked: false,
    minimumShelfLifeDays: 0, warnBeforeLotExpires: false, daysBeforeExpiryWarning: 30,
    // Item Options
    substituteItem1Id: undefined, substituteItem2Id: undefined, substituteItem3Id: undefined, substituteItem4Id: undefined,
    warrantyDays: 0, isKit: false, isKitComponent: false, isFinishedGood: false, isFinishedGoodComponent: false,
    includeInQuotes: true, includeInOrders: true, includeInInvoices: true, includeInFulfillment: true, isProcurementItem: false,
    maintainCalendarYearHistory: true, maintainFiscalYearHistory: true, maintainTransactionHistory: true,
    // Tax
    isTaxable: true,
    // Physical Properties
    shippingWeight: 0,
    // Media
    imageUrl: undefined, thumbnailUrl: undefined
  });
  const [imageUploading, setImageUploading] = useState(false);
  const [imagePreview, setImagePreview] = useState<string | null>(null);

  const fetchItems = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await inventoryManagementService.getInventoryItems();
      setItems(data);
    } catch (err: any) {
      console.error('Error fetching items:', err);
      setError('Failed to load inventory items');
    } finally {
      setLoading(false);
    }
  };

  const fetchUnitsOfMeasure = async () => {
    try {
      const data = await inventoryManagementService.getUnitsOfMeasure(true); // Active only
      setUnitsOfMeasure(data);
    } catch (err) {
      console.error('Error fetching units of measure:', err);
    }
  };

  const fetchUomSchedules = async () => {
    try {
      const data = await inventoryManagementService.getUomSchedules(true); // Active only
      setUomSchedules(data);
    } catch (err) {
      console.error('Error fetching UoM schedules:', err);
    }
  };

  const fetchCategories = async () => {
    try {
      const data = await inventoryManagementService.getActiveInventoryCategories();
      setCategories(data);
    } catch (err) {
      console.error('Error fetching categories:', err);
    }
  };

  useEffect(() => {
    fetchItems();
    fetchUnitsOfMeasure();
    fetchUomSchedules();
    fetchCategories();
  }, []);

  useEffect(() => {
    let filtered = items;
    if (searchTerm) {
      filtered = filtered.filter(i =>
        i.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        i.itemCode.toLowerCase().includes(searchTerm.toLowerCase()) ||
        i.description?.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }
    if (typeFilter !== 'all') filtered = filtered.filter(i => i.itemType === parseInt(typeFilter));
    if (statusFilter !== 'all') filtered = filtered.filter(i => i.status === parseInt(statusFilter));
    setFilteredItems(filtered);
  }, [searchTerm, typeFilter, statusFilter, items]);

  const handleCreate = async () => {
    try {
      const newItem = await inventoryManagementService.createInventoryItem(formData);
      setItems(prev => [...prev, newItem]);
      setIsCreateDialogOpen(false);
      resetForm();
    } catch (err) {
      console.error('Error creating item:', err);
      toast.error('Failed to create inventory item');
    }
  };

  const handleImport = async () => {
    try {
      setIsImporting(true);
      const parsed: unknown = JSON.parse(importJson);
      if (!Array.isArray(parsed) || parsed.length === 0) {
        throw new Error('Enter a JSON array containing at least one item profile.');
      }
      const result = await inventoryManagementService.importInventoryItems(parsed as CreateInventoryItemDto[]);
      setItems(prev => [...prev, ...result.items]);
      setIsImportDialogOpen(false);
      setImportJson('[]');
      toast.success(`${result.importedCount} item profile(s) imported`);
    } catch (err) {
      console.error('Error importing item profiles:', err);
      toast.error(err instanceof Error ? err.message : 'Failed to import item profiles');
    } finally {
      setIsImporting(false);
    }
  };

  const openHistory = async (item: InventoryItemDto) => {
    setSelectedItem(item);
    setIsHistoryDialogOpen(true);
    setHistoryLoading(true);
    try {
      setHistoryEntries(await inventoryManagementService.getInventoryItemHistory(item.id));
    } catch (err) {
      console.error('Error loading item history:', err);
      setHistoryEntries([]);
      toast.error('Failed to load item change history');
    } finally {
      setHistoryLoading(false);
    }
  };

  const handleEdit = async (item: InventoryItemDto) => {
    setSelectedItem(item);
    
    // Convert valuation method from string to number if needed
    let valuationMethodValue = 1; // Default to Weighted Average
    if (typeof item.valuationMethod === 'string') {
      const methodMap: Record<string, number> = {
        'WeightedAverage': 1,
        'FIFO': 2,
        'FIFOPerpetual': 2,
        'LIFO': 3,
        'LIFOPerpetual': 3,
        'StandardCost': 4
      };
      valuationMethodValue = methodMap[item.valuationMethod] || 1;
    } else {
      valuationMethodValue = item.valuationMethod || 1;
    }
    
    setFormData({
      // Basic Info
      itemCode: item.itemCode, name: item.name, description: item.description || '',
      shortDescription: item.shortDescription, genericDescription: item.genericDescription,
      categoryId: item.categoryId, unitOfMeasure: item.unitOfMeasure,
      unitOfMeasureScheduleId: (item as any).unitOfMeasureScheduleId,
      valuationMethod: valuationMethodValue,
      itemType: item.itemType, status: item.status, quantityDecimals: item.quantityDecimals || 2, currencyDecimals: item.currencyDecimals || 2,
      isProjectApplicable: item.isProjectApplicable, isCostCentreApplicable: item.isCostCentreApplicable,
      brand: item.brand, manufacturer: item.manufacturer, model: item.model,
      style: item.style, feature: item.feature,
      itemClassId: item.itemClassId, priceGroupId: item.priceGroupId,
      // Costs & Pricing
      standardCost: item.standardCost, currentCost: item.currentCost || 0, listPrice: item.listPrice || 0,
      // Stock Settings
      minimumLevel: item.minimumLevel, maximumLevel: item.maximumLevel,
      reorderLevel: item.reorderLevel, reorderQuantity: item.reorderQuantity,
      safetyStock: item.safetyStock || 0, leadTimeDays: item.leadTimeDays, safetyLeadTimeDays: 0,
      allowBackorder: item.allowBackorder || false, autoReorder: false,
      // Tracking
      isSerialTracked: item.isSerialTracked, isLotTracked: item.isLotTracked, isBatchTracked: item.isBatchTracked,
      isManufactureDateTracked: item.isManufactureDateTracked,
      isExpirationTracked: item.isExpirationTracked, isLocationTracked: false,
      lotCategory: item.lotCategory, minimumShelfLifeDays: item.minimumShelfLifeDays || 0,
      warnBeforeLotExpires: item.warnBeforeLotExpires || false, daysBeforeExpiryWarning: item.daysBeforeExpiryWarning || 30,
      // Item Options
      substituteItem1Id: item.substituteItem1Id, substituteItem2Id: item.substituteItem2Id,
      substituteItem3Id: item.substituteItem3Id, substituteItem4Id: item.substituteItem4Id,
      warrantyDays: item.warrantyDays || 0,
      isKit: item.isKit || false, isKitComponent: item.isKitComponent || false,
      isFinishedGood: item.isFinishedGood || false, isFinishedGoodComponent: item.isFinishedGoodComponent || false,
      includeInQuotes: item.includeInQuotes ?? true, includeInOrders: item.includeInOrders ?? true,
      includeInInvoices: item.includeInInvoices ?? true, includeInFulfillment: item.includeInFulfillment ?? true,
      isProcurementItem: item.isProcurementItem || false,
      maintainCalendarYearHistory: item.maintainCalendarYearHistory ?? true,
      maintainFiscalYearHistory: item.maintainFiscalYearHistory ?? true,
      maintainTransactionHistory: item.maintainTransactionHistory ?? true,
      // Tax
      isTaxable: true, purchaseTaxOption: item.purchaseTaxOption, salesTaxOption: item.salesTaxOption,
      // Physical Properties
      shippingWeight: item.shippingWeight || 0,
      // Media
      barcode: item.barcode,
      imageUrl: item.imageUrl,
      thumbnailUrl: item.thumbnailUrl
    });
    setEditIsActive(item.isActive);
    setImagePreview(getFullImageUrl(item.imageUrl));
    setIsEditDialogOpen(true);

    // Load price list lines for this item
    loadItemPriceListLines(item.id);
  };

  const loadItemPriceListLines = async (itemId: string) => {
    try {
      setLoadingPriceLines(true);
      const lines = await priceListService.getLinesByInventoryItem(itemId);
      setItemPriceListLines(lines);
    } catch (error) {
      console.error('Error loading price list lines:', error);
      setItemPriceListLines([]);
    } finally {
      setLoadingPriceLines(false);
    }
  };

  const handleUpdate = async () => {
    if (!selectedItem) return;
    try {
      const updateData: UpdateInventoryItemDto = {
        ...formData,
        isActive: editIsActive,
        status: formData.status,
        rowVersion: selectedItem.rowVersion
      };
      const updated = await inventoryManagementService.updateInventoryItem(selectedItem.id, updateData);
      setItems(prev => prev.map(i => i.id === selectedItem.id ? updated : i));
      setIsEditDialogOpen(false);
      resetForm();
    } catch (err) {
      console.error('Error updating item:', err);
      toast.error('Failed to update inventory item');
    }
  };

  const handleDelete = async (): Promise<boolean> => {
    if (!deleteTarget) return false;

    try {
      setIsDeleting(true);
      await inventoryManagementService.deleteInventoryItem(deleteTarget.id);
      setItems(prev => prev.filter(i => i.id !== deleteTarget.id));
      toast.success(`Inventory item ${deleteTarget.itemCode} deleted`);
      setDeleteTarget(null);
      return true;
    } catch (err: any) {
      console.error('Error deleting item:', err);
      const problem = err?.response?.data;
      const detail = problem?.detail || problem?.message || 'Failed to delete inventory item';
      const code = problem?.code || problem?.extensions?.code;
      toast.error(code ? `${detail} (${code})` : detail);
      return false;
    } finally {
      setIsDeleting(false);
    }
  };

  const handleImageUpload = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    // Validate file type
    const validation = fileUploadService.validateFile(file, 5 * 1024 * 1024, ['image/jpeg', 'image/png', 'image/gif', 'image/webp']);
    if (!validation.isValid) {
      toast.error(validation.error || 'Invalid file');
      return;
    }

    try {
      setImageUploading(true);
      const uploadedFile = await fileUploadService.uploadFile(file, 'inventory-items', selectedItem?.id || 'new');
      setFormData(prev => ({ ...prev, imageUrl: uploadedFile.url, thumbnailUrl: uploadedFile.thumbnailUrl || uploadedFile.url }));
      setImagePreview(uploadedFile.url);
    } catch (err) {
      console.error('Error uploading image:', err);
      toast.error('Failed to upload image');
    } finally {
      setImageUploading(false);
    }
  };

  const handleRemoveImage = () => {
    setFormData(prev => ({ ...prev, imageUrl: undefined, thumbnailUrl: undefined }));
    setImagePreview(null);
  };

  const openSuppliers = async (item: InventoryItemDto) => {
    setSelectedItem(item);
    try {
      const supplierData = await inventoryManagementService.getItemSuppliersByItem(item.id);
      setSuppliers(supplierData);
      setIsSupplierDialogOpen(true);
    } catch (err) {
      console.error('Error fetching suppliers:', err);
    }
  };

  const resetForm = () => {
    setFormData({
      // Basic Info
      itemCode: '', name: '', description: '', categoryId: '', unitOfMeasure: 'EA',
      unitOfMeasureScheduleId: undefined,
      valuationMethod: 1, // WeightedAverage
      itemType: 1, status: 1, quantityDecimals: 2, currencyDecimals: 2,
      isProjectApplicable: false, isCostCentreApplicable: false,
      // Costs & Pricing
      standardCost: 0, currentCost: 0, listPrice: 0,
      // Stock Settings
      minimumLevel: 0, maximumLevel: 1000, reorderLevel: 10, reorderQuantity: 50,
      safetyStock: 0, leadTimeDays: 7, safetyLeadTimeDays: 0, allowBackorder: false, autoReorder: false,
      // Tracking
      isSerialTracked: false, isLotTracked: false, isBatchTracked: false, isManufactureDateTracked: false, isExpirationTracked: false, isLocationTracked: false,
      minimumShelfLifeDays: 0, warnBeforeLotExpires: false, daysBeforeExpiryWarning: 30,
      // Item Options
      substituteItem1Id: undefined, substituteItem2Id: undefined, substituteItem3Id: undefined, substituteItem4Id: undefined,
      warrantyDays: 0, isKit: false, isKitComponent: false, isFinishedGood: false, isFinishedGoodComponent: false,
      includeInQuotes: true, includeInOrders: true, includeInInvoices: true, includeInFulfillment: true, isProcurementItem: false,
      maintainCalendarYearHistory: true, maintainFiscalYearHistory: true, maintainTransactionHistory: true,
      // Tax
      isTaxable: true,
      // Physical Properties
      shippingWeight: 0,
      // Media
      imageUrl: undefined, thumbnailUrl: undefined
    });
    setSelectedItem(null);
    setImagePreview(null);
  };

  const getItemTypeName = (type: number) => ItemTypes.find(t => t.value === type)?.label || 'Unknown';
  const getStatusName = (status: number) => ItemStatuses.find(s => s.value === status)?.label || 'Unknown';
  const lowStockItems = items.filter(i => i.availableStock <= i.reorderLevel);

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Inventory Items</h1>
          <p className="text-muted-foreground">Manage products, parts, and materials</p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" onClick={() => setIsImportDialogOpen(true)}>
            <Upload className="mr-2 h-4 w-4" />Import profiles
          </Button>
          <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button><Plus className="mr-2 h-4 w-4" />Add Item</Button>
          </DialogTrigger>
          <DialogContent className="w-[1100px] max-w-[95vw] h-[85vh] max-h-[85vh] flex flex-col">
            <DialogHeader>
              <DialogTitle>Add Inventory Item</DialogTitle>
              <DialogDescription>Create a new inventory item.</DialogDescription>
            </DialogHeader>
            <Tabs defaultValue="basic" className="flex-1 flex flex-col min-h-0">
              <TabsList className="grid w-full grid-cols-5 shrink-0">
                <TabsTrigger value="basic">Basic Info</TabsTrigger>
                <TabsTrigger value="costs">Costs & Pricing</TabsTrigger>
                <TabsTrigger value="stock">Stock Settings</TabsTrigger>
                <TabsTrigger value="options">Options</TabsTrigger>
                <TabsTrigger value="tracking">Tracking</TabsTrigger>
              </TabsList>
              {/* BASIC INFO TAB - REDESIGNED */}
              <TabsContent value="basic" className="flex-1 overflow-y-auto pt-4">
                <div className="max-w-5xl mx-auto space-y-6 pb-4">
                  
                  {/* Hero Section - Product Image & Core Identity */}
                  <div className="rounded-xl p-6 border">
                    <div className="flex items-start gap-6">
                      {/* Product Image */}
                      <div className="shrink-0">
                        <div className="w-40 h-40 border-2 border-dashed border-blue-200 dark:border-blue-800 rounded-xl flex items-center justify-center bg-white dark:bg-gray-900 overflow-hidden shadow-sm">
                          {imagePreview ? (
                            <img src={imagePreview} alt="Product" className="w-full h-full object-cover" />
                          ) : (
                            <div className="text-center text-muted-foreground">
                              <ImageIcon className="h-12 w-12 mx-auto mb-2 opacity-40" />
                              <p className="text-xs">No image</p>
                            </div>
                          )}
                        </div>
                        <div className="mt-3 space-y-2">
                          <div className="flex items-center gap-2">
                            <Input
                              type="file"
                              accept="image/jpeg,image/png,image/gif,image/webp"
                              onChange={handleImageUpload}
                              disabled={imageUploading}
                              className="text-xs h-8"
                            />
                            {imagePreview && (
                              <Button variant="ghost" size="icon" className="h-8 w-8" onClick={handleRemoveImage} title="Remove image">
                                <X className="h-3 w-3" />
                              </Button>
                            )}
                          </div>
                          {imageUploading && (
                            <div className="flex items-center gap-2 text-xs text-muted-foreground">
                              <Upload className="h-3 w-3 animate-pulse" />
                              Uploading...
                            </div>
                          )}
                        </div>
                      </div>

                      {/* Core Identity Fields */}
                      <div className="flex-1 space-y-4">
                        <div className="grid grid-cols-2 gap-4">
                          <div className="space-y-2">
                            <Label className="text-sm font-semibold flex items-center gap-1">
                              Item Code <span className="text-red-500">*</span>
                            </Label>
                            <Input
                              value={formData.itemCode}
                              onChange={(e) => setFormData({...formData, itemCode: e.target.value.toUpperCase()})}
                              placeholder="e.g., SKU-001"
                              className="font-mono text-base h-11"
                            />
                          </div>
                          <div className="space-y-2">
                            <Label className="text-sm font-semibold flex items-center gap-1">
                              Item Name <span className="text-red-500">*</span>
                            </Label>
                            <Input
                              value={formData.name}
                              onChange={(e) => setFormData({...formData, name: e.target.value})}
                              placeholder="e.g., Premium Widget"
                              className="text-base h-11"
                            />
                          </div>
                        </div>

                        <div className="space-y-2">
                          <Label className="text-sm font-semibold">Short Description</Label>
                          <Input
                            value={formData.shortDescription || ''}
                            onChange={(e) => setFormData({...formData, shortDescription: e.target.value})}
                            placeholder="Brief description for lists and reports"
                            className="h-10"
                          />
                        </div>

                        <div className="space-y-2">
                          <Label className="text-sm font-semibold">Full Description</Label>
                          <Textarea
                            value={formData.description}
                            onChange={(e) => setFormData({...formData, description: e.target.value})}
                            placeholder="Detailed description of the item..."
                            rows={3}
                            className="resize-none"
                          />
                        </div>
                      </div>
                    </div>
                  </div>

                  {/* Classification Section */}
                  <div className="bg-white dark:bg-gray-900 rounded-xl border shadow-sm">
                    <div className="px-6 py-4 border-b bg-gray-50 dark:bg-gray-800/50">
                      <h3 className="font-semibold text-base flex items-center gap-2">
                        <Package className="h-4 w-4 text-blue-600" />
                        Classification & Type
                      </h3>
                      <p className="text-xs text-muted-foreground mt-1">Define how this item is categorized and measured</p>
                    </div>
                    <div className="p-6 space-y-4">
                      <div className="grid grid-cols-3 gap-4">
                        <div className="space-y-2">
                          <Label className="text-sm font-medium">Item Type</Label>
                          <Select value={formData.itemType.toString()} onValueChange={(v) => setFormData({...formData, itemType: parseInt(v)})}>
                            <SelectTrigger className="h-10">
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              {ItemTypes.map(t => (
                                <SelectItem key={t.value} value={t.value.toString()}>
                                  {t.label}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </div>
                        <div className="space-y-2">
                          <Label className="text-sm font-medium flex items-center gap-1">
                            Category <span className="text-red-500">*</span>
                          </Label>
                          <Select value={formData.categoryId || 'none'} onValueChange={(v) => setFormData({...formData, categoryId: v === 'none' ? '' : v})}>
                            <SelectTrigger className="h-10">
                              <SelectValue placeholder="Select Category" />
                            </SelectTrigger>
                            <SelectContent>
                              <SelectItem value="none">Select Category...</SelectItem>
                              {categories.map(c => (
                                <SelectItem key={c.id} value={c.id}>
                                  {c.code ? `${c.code} - ` : ''}{c.name}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </div>
                        <div className="space-y-2">
                          <Label className="text-sm font-medium">Barcode</Label>
                          <Input
                            value={formData.barcode || ''}
                            onChange={(e) => setFormData({...formData, barcode: e.target.value})}
                            placeholder="Scan or enter barcode"
                            className="h-10 font-mono"
                          />
                        </div>
                      </div>
                    </div>
                  </div>

                  {/* Measurement & Valuation Section */}
                  <div className="bg-white dark:bg-gray-900 rounded-xl border shadow-sm">
                    <div className="px-6 py-4 border-b bg-gray-50 dark:bg-gray-800/50">
                      <h3 className="font-semibold text-base flex items-center gap-2">
                        <BarChart3 className="h-4 w-4 text-green-600" />
                        Measurement & Valuation
                      </h3>
                      <p className="text-xs text-muted-foreground mt-1">Configure units of measure and inventory valuation</p>
                    </div>
                    <div className="p-6 space-y-4">
                      <div className="grid grid-cols-2 gap-4">
                        <div className="space-y-2">
                          <Label className="text-sm font-medium">Unit of Measure Schedule</Label>
                          <Select
                            value={formData.unitOfMeasureScheduleId || 'none'}
                            onValueChange={(v) => {
                              const schedule = uomSchedules.find(s => s.id === v);
                              setFormData({
                                ...formData,
                                unitOfMeasureScheduleId: v === 'none' ? undefined : v,
                                unitOfMeasure: schedule?.baseUnitOfMeasureName || formData.unitOfMeasure
                              });
                            }}
                          >
                            <SelectTrigger className="h-10">
                              <SelectValue placeholder="Select UoM Schedule" />
                            </SelectTrigger>
                            <SelectContent>
                              <SelectItem value="none">No Schedule (Single Unit)</SelectItem>
                              {uomSchedules.map(s => (
                                <SelectItem key={s.id} value={s.id}>
                                  {s.scheduleId} - {s.description}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                          {formData.unitOfMeasureScheduleId && (
                            <p className="text-xs text-blue-600 dark:text-blue-400 flex items-center gap-1 mt-1">
                              <span className="font-medium">Base Unit:</span>
                              {uomSchedules.find(s => s.id === formData.unitOfMeasureScheduleId)?.baseUnitOfMeasureName || 'N/A'}
                            </p>
                          )}
                        </div>
                        <div className="space-y-2">
                          <Label className="text-sm font-medium">Valuation Method</Label>
                          <Select
                            value={formData.valuationMethod?.toString() || '1'}
                            onValueChange={(v) => setFormData({...formData, valuationMethod: parseInt(v)})}
                          >
                            <SelectTrigger className="h-10">
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              {ValuationMethods.map(m => (
                                <SelectItem key={m.value} value={m.value}>
                                  {m.label}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                          <p className="text-xs text-amber-600 dark:text-amber-400 flex items-center gap-1 mt-1">
                            <AlertTriangle className="h-3 w-3" />
                            Cannot be changed after first transaction
                          </p>
                        </div>
                      </div>

                      <div className="grid grid-cols-2 gap-4">
                        <div className="space-y-2">
                          <Label className="text-sm font-medium">Quantity Decimals</Label>
                          <Select value={formData.quantityDecimals.toString()} onValueChange={(v) => setFormData({...formData, quantityDecimals: parseInt(v)})}>
                            <SelectTrigger className="h-10">
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              {[0,1,2,3,4,5].map(d => (
                                <SelectItem key={d} value={d.toString()}>
                                  {d} decimal place{d !== 1 ? 's' : ''}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </div>
                        <div className="space-y-2">
                          <Label className="text-sm font-medium">Currency Decimals</Label>
                          <Select value={formData.currencyDecimals.toString()} onValueChange={(v) => setFormData({...formData, currencyDecimals: parseInt(v)})}>
                            <SelectTrigger className="h-10">
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              {[0,1,2,3,4,5].map(d => (
                                <SelectItem key={d} value={d.toString()}>
                                  {d} decimal place{d !== 1 ? 's' : ''}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </div>
                      </div>
                    </div>
                  </div>

                  {/* Product Details Section */}
                  <div className="bg-white dark:bg-gray-900 rounded-xl border shadow-sm">
                    <div className="px-6 py-4 border-b bg-gray-50 dark:bg-gray-800/50">
                      <h3 className="font-semibold text-base flex items-center gap-2">
                        <Package className="h-4 w-4 text-purple-600" />
                        Product Details
                      </h3>
                      <p className="text-xs text-muted-foreground mt-1">Additional product information and attributes</p>
                    </div>
                    <div className="p-6 space-y-4">
                      <div className="grid grid-cols-2 gap-4">
                        <div className="space-y-2">
                          <Label className="text-sm font-medium">{getLabel('Brand')}</Label>
                          <Input
                            value={formData.brand || ''}
                            onChange={(e) => setFormData({...formData, brand: e.target.value})}
                            placeholder="e.g., Acme Corp"
                            className="h-10"
                          />
                        </div>
                        <div className="space-y-2">
                          <Label className="text-sm font-medium">{getLabel('Manufacturer')}</Label>
                          <Input
                            value={formData.manufacturer || ''}
                            onChange={(e) => setFormData({...formData, manufacturer: e.target.value})}
                            placeholder="e.g., Global Manufacturing"
                            className="h-10"
                          />
                        </div>
                      </div>

                      <div className="grid grid-cols-3 gap-4">
                        <div className="space-y-2">
                          <Label className="text-sm font-medium">Model</Label>
                          <Input
                            value={formData.model || ''}
                            onChange={(e) => setFormData({...formData, model: e.target.value})}
                            placeholder="Model number"
                            className="h-10"
                          />
                        </div>
                        <div className="space-y-2">
                          <Label className="text-sm font-medium">{getLabel('Style')}</Label>
                          <Input
                            value={formData.style || ''}
                            onChange={(e) => setFormData({...formData, style: e.target.value})}
                            placeholder="Style variant"
                            className="h-10"
                          />
                        </div>
                        <div className="space-y-2">
                          <Label className="text-sm font-medium">{getLabel('Feature')}</Label>
                          <Input
                            value={formData.feature || ''}
                            onChange={(e) => setFormData({...formData, feature: e.target.value})}
                            placeholder="Key feature"
                            className="h-10"
                          />
                        </div>
                      </div>

                      <div className="space-y-2">
                        <Label className="text-sm font-medium">Generic Description</Label>
                        <Input
                          value={formData.genericDescription || ''}
                          onChange={(e) => setFormData({...formData, genericDescription: e.target.value})}
                          placeholder="Generic or alternative description"
                          className="h-10"
                        />
                      </div>
                    </div>
                  </div>

                </div>
              </TabsContent>
              {/* COSTS & PRICING TAB */}
              <TabsContent value="costs" className="flex-1 overflow-y-auto space-y-4 pt-4">
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2"><Label>Standard Cost</Label>
                    <Input type="number" step="0.01" value={formData.standardCost} onChange={(e) => setFormData({...formData, standardCost: parseFloat(e.target.value) || 0})} />
                  </div>
                  <div className="space-y-2"><Label>Current Cost</Label>
                    <Input type="number" step="0.01" value={formData.currentCost} onChange={(e) => setFormData({...formData, currentCost: parseFloat(e.target.value) || 0})} />
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2"><Label>List Price</Label>
                    <Input type="number" step="0.01" value={formData.listPrice} onChange={(e) => setFormData({...formData, listPrice: parseFloat(e.target.value) || 0})} />
                  </div>
                  <div className="space-y-2"><Label>Selling Price</Label>
                    <Input type="number" step="0.01" value={formData.sellingPrice || 0} onChange={(e) => setFormData({...formData, sellingPrice: parseFloat(e.target.value) || 0})} />
                  </div>
                </div>
                <div className="border-t pt-4">
                  <h4 className="font-medium mb-3">Tax Settings</h4>
                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-2"><Label>Purchase Tax Option</Label>
                      <Select value={formData.purchaseTaxOption || ''} onValueChange={(v) => setFormData({...formData, purchaseTaxOption: v})}>
                        <SelectTrigger><SelectValue placeholder="Select..." /></SelectTrigger>
                        <SelectContent>{TaxOptions.map(t => <SelectItem key={t.value} value={t.value}>{t.label}</SelectItem>)}</SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-2"><Label>Sales Tax Option</Label>
                      <Select value={formData.salesTaxOption || ''} onValueChange={(v) => setFormData({...formData, salesTaxOption: v})}>
                        <SelectTrigger><SelectValue placeholder="Select..." /></SelectTrigger>
                        <SelectContent>{TaxOptions.map(t => <SelectItem key={t.value} value={t.value}>{t.label}</SelectItem>)}</SelectContent>
                      </Select>
                    </div>
                  </div>
                  <div className="flex items-center space-x-2 mt-4">
                    <Switch checked={formData.isTaxable} onCheckedChange={(v) => setFormData({...formData, isTaxable: v})} />
                    <Label>Item is Taxable</Label>
                  </div>
                </div>
              </TabsContent>
              {/* STOCK SETTINGS TAB */}
              <TabsContent value="stock" className="flex-1 overflow-y-auto space-y-4 pt-4">
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2"><Label>Minimum Level</Label>
                    <Input type="number" value={formData.minimumLevel} onChange={(e) => setFormData({...formData, minimumLevel: parseFloat(e.target.value) || 0})} />
                  </div>
                  <div className="space-y-2"><Label>Maximum Level</Label>
                    <Input type="number" value={formData.maximumLevel} onChange={(e) => setFormData({...formData, maximumLevel: parseFloat(e.target.value) || 0})} />
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2"><Label>Reorder Level</Label>
                    <Input type="number" value={formData.reorderLevel} onChange={(e) => setFormData({...formData, reorderLevel: parseFloat(e.target.value) || 0})} />
                  </div>
                  <div className="space-y-2"><Label>Reorder Quantity</Label>
                    <Input type="number" value={formData.reorderQuantity} onChange={(e) => setFormData({...formData, reorderQuantity: parseFloat(e.target.value) || 0})} />
                  </div>
                </div>
                <div className="grid grid-cols-3 gap-4">
                  <div className="space-y-2"><Label>Lead Time (Days)</Label>
                    <Input type="number" value={formData.leadTimeDays} onChange={(e) => setFormData({...formData, leadTimeDays: parseInt(e.target.value) || 0})} />
                  </div>
                  <div className="space-y-2"><Label>Safety Lead Time (Days)</Label>
                    <Input type="number" value={formData.safetyLeadTimeDays} onChange={(e) => setFormData({...formData, safetyLeadTimeDays: parseInt(e.target.value) || 0})} />
                  </div>
                  <div className="space-y-2"><Label>Safety Stock</Label>
                    <Input type="number" value={formData.safetyStock} onChange={(e) => setFormData({...formData, safetyStock: parseFloat(e.target.value) || 0})} />
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2"><Label>Shipping Weight</Label>
                    <Input type="number" step="0.01" value={formData.shippingWeight} onChange={(e) => setFormData({...formData, shippingWeight: parseFloat(e.target.value) || 0})} />
                  </div>
                  <div className="space-y-2"><Label>Warranty Days</Label>
                    <Input type="number" value={formData.warrantyDays} onChange={(e) => setFormData({...formData, warrantyDays: parseInt(e.target.value) || 0})} />
                  </div>
                </div>
                <div className="flex items-center space-x-6 pt-2">
                  <div className="flex items-center space-x-2">
                    <Switch checked={formData.allowBackorder} onCheckedChange={(v) => setFormData({...formData, allowBackorder: v})} />
                    <Label>Allow Backorder</Label>
                  </div>
                  <div className="flex items-center space-x-2">
                    <Switch checked={formData.autoReorder} onCheckedChange={(v) => setFormData({...formData, autoReorder: v})} />
                    <Label>Auto Reorder</Label>
                  </div>
                </div>
              </TabsContent>
              {/* OPTIONS TAB */}
              <TabsContent value="options" className="flex-1 overflow-y-auto space-y-4 pt-4">
                <div className="border rounded-lg p-4">
                  <h4 className="font-medium mb-3">Item Classification</h4>
                  <div className="grid grid-cols-2 gap-4">
                    <div className="flex items-center space-x-2">
                      <Switch checked={formData.isKit} onCheckedChange={(v) => setFormData({...formData, isKit: v})} />
                      <Label>Kit</Label>
                    </div>
                    <div className="flex items-center space-x-2">
                      <Switch checked={formData.isKitComponent} onCheckedChange={(v) => setFormData({...formData, isKitComponent: v})} />
                      <Label>Kit Component</Label>
                    </div>
                    <div className="flex items-center space-x-2">
                      <Switch checked={formData.isFinishedGood} onCheckedChange={(v) => setFormData({...formData, isFinishedGood: v})} />
                      <Label>Finished Good</Label>
                    </div>
                    <div className="flex items-center space-x-2">
                      <Switch checked={formData.isFinishedGoodComponent} onCheckedChange={(v) => setFormData({...formData, isFinishedGoodComponent: v})} />
                      <Label>Finished Good Component</Label>
                    </div>
                    <div className="flex items-center space-x-2">
                      <Switch checked={formData.isProcurementItem} onCheckedChange={(v) => setFormData({...formData, isProcurementItem: v})} />
                      <Label>Non-Saleable Item</Label>
                    </div>
                    <div className="flex items-center space-x-2">
                      <Switch checked={formData.isProjectApplicable} onCheckedChange={(v) => setFormData({...formData, isProjectApplicable: v})} />
                      <Label>Project applicable</Label>
                    </div>
                    <div className="flex items-center space-x-2">
                      <Switch checked={formData.isCostCentreApplicable} onCheckedChange={(v) => setFormData({...formData, isCostCentreApplicable: v})} />
                      <Label>Cost-centre applicable</Label>
                    </div>
                  </div>
                </div>
                <div className="border rounded-lg p-4">
                  <h4 className="font-medium mb-3">Include in Catalog</h4>
                  <div className="grid grid-cols-2 gap-4">
                    <div className="flex items-center space-x-2">
                      <Switch checked={formData.includeInQuotes} onCheckedChange={(v) => setFormData({...formData, includeInQuotes: v})} />
                      <Label>Quotes</Label>
                    </div>
                    <div className="flex items-center space-x-2">
                      <Switch checked={formData.includeInOrders} onCheckedChange={(v) => setFormData({...formData, includeInOrders: v})} />
                      <Label>Orders</Label>
                    </div>
                    <div className="flex items-center space-x-2">
                      <Switch checked={formData.includeInInvoices} onCheckedChange={(v) => setFormData({...formData, includeInInvoices: v})} />
                      <Label>Invoices</Label>
                    </div>
                    <div className="flex items-center space-x-2">
                      <Switch checked={formData.includeInFulfillment} onCheckedChange={(v) => setFormData({...formData, includeInFulfillment: v})} />
                      <Label>Fulfillment</Label>
                    </div>
                  </div>
                </div>
                <div className="border rounded-lg p-4">
                  <h4 className="font-medium mb-3">Maintain History</h4>
                  <div className="grid grid-cols-3 gap-4">
                    <div className="flex items-center space-x-2">
                      <Switch checked={formData.maintainCalendarYearHistory} onCheckedChange={(v) => setFormData({...formData, maintainCalendarYearHistory: v})} />
                      <Label>Calendar Year</Label>
                    </div>
                    <div className="flex items-center space-x-2">
                      <Switch checked={formData.maintainFiscalYearHistory} onCheckedChange={(v) => setFormData({...formData, maintainFiscalYearHistory: v})} />
                      <Label>Fiscal Year</Label>
                    </div>
                    <div className="flex items-center space-x-2">
                      <Switch checked={formData.maintainTransactionHistory} onCheckedChange={(v) => setFormData({...formData, maintainTransactionHistory: v})} />
                      <Label>Transaction</Label>
                    </div>
                  </div>
                </div>
                <div className="border rounded-lg p-4">
                  <h4 className="font-medium mb-3">Substitute Items</h4>
                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-2"><Label>Substitute Item 1</Label>
                      <Select value={formData.substituteItem1Id || 'none'} onValueChange={(v) => setFormData({...formData, substituteItem1Id: v === 'none' ? undefined : v})}>
                        <SelectTrigger><SelectValue placeholder="Select substitute..." /></SelectTrigger>
                        <SelectContent>
                          <SelectItem value="none">None</SelectItem>
                          {items.filter(i => i.itemCode !== formData.itemCode).map(i => <SelectItem key={i.id} value={i.id}>{i.itemCode} - {i.name}</SelectItem>)}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-2"><Label>Substitute Item 2</Label>
                      <Select value={formData.substituteItem2Id || 'none'} onValueChange={(v) => setFormData({...formData, substituteItem2Id: v === 'none' ? undefined : v})}>
                        <SelectTrigger><SelectValue placeholder="Select substitute..." /></SelectTrigger>
                        <SelectContent>
                          <SelectItem value="none">None</SelectItem>
                          {items.filter(i => i.itemCode !== formData.itemCode).map(i => <SelectItem key={i.id} value={i.id}>{i.itemCode} - {i.name}</SelectItem>)}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-2"><Label>Substitute Item 3</Label>
                      <Select value={formData.substituteItem3Id || 'none'} onValueChange={(v) => setFormData({...formData, substituteItem3Id: v === 'none' ? undefined : v})}>
                        <SelectTrigger><SelectValue placeholder="Select substitute..." /></SelectTrigger>
                        <SelectContent>
                          <SelectItem value="none">None</SelectItem>
                          {items.filter(i => i.itemCode !== formData.itemCode).map(i => <SelectItem key={i.id} value={i.id}>{i.itemCode} - {i.name}</SelectItem>)}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-2"><Label>Substitute Item 4</Label>
                      <Select value={formData.substituteItem4Id || 'none'} onValueChange={(v) => setFormData({...formData, substituteItem4Id: v === 'none' ? undefined : v})}>
                        <SelectTrigger><SelectValue placeholder="Select substitute..." /></SelectTrigger>
                        <SelectContent>
                          <SelectItem value="none">None</SelectItem>
                          {items.filter(i => i.itemCode !== formData.itemCode).map(i => <SelectItem key={i.id} value={i.id}>{i.itemCode} - {i.name}</SelectItem>)}
                        </SelectContent>
                      </Select>
                    </div>
                  </div>
                </div>
              </TabsContent>
              {/* TRACKING TAB */}
              <TabsContent value="tracking" className="flex-1 overflow-y-auto space-y-4 pt-4">
                <div className="space-y-3">
                  <div className="flex items-center justify-between p-3 border rounded-lg">
                    <div><Label>Serial Number Tracking</Label><p className="text-xs text-muted-foreground">Track individual items by serial number</p></div>
                    <Switch checked={formData.isSerialTracked} onCheckedChange={(v) => setFormData({...formData, isSerialTracked: v})} />
                  </div>
                  <div className="flex items-center justify-between p-3 border rounded-lg">
                    <div><Label>Lot Tracking</Label><p className="text-xs text-muted-foreground">Track items by lot/batch number</p></div>
                    <Switch checked={formData.isLotTracked} onCheckedChange={(v) => setFormData({...formData, isLotTracked: v})} />
                  </div>
                  <div className="flex items-center justify-between p-3 border rounded-lg">
                    <div><Label>Batch Tracking</Label><p className="text-xs text-muted-foreground">Require a distinct production batch identifier</p></div>
                    <Switch checked={formData.isBatchTracked} onCheckedChange={(v) => setFormData({...formData, isBatchTracked: v})} />
                  </div>
                  <div className="flex items-center justify-between p-3 border rounded-lg">
                    <div><Label>Manufacture Date Tracking</Label><p className="text-xs text-muted-foreground">Require manufacture date capture on receipt</p></div>
                    <Switch checked={formData.isManufactureDateTracked} onCheckedChange={(v) => setFormData({...formData, isManufactureDateTracked: v})} />
                  </div>
                  <div className="flex items-center justify-between p-3 border rounded-lg">
                    <div><Label>Expiration Tracking</Label><p className="text-xs text-muted-foreground">Track items by expiration date</p></div>
                    <Switch checked={formData.isExpirationTracked} onCheckedChange={(v) => setFormData({...formData, isExpirationTracked: v})} />
                  </div>
                  <div className="flex items-center justify-between p-3 border rounded-lg">
                    <div><Label>Location Tracking</Label><p className="text-xs text-muted-foreground">Track items by warehouse location</p></div>
                    <Switch checked={formData.isLocationTracked} onCheckedChange={(v) => setFormData({...formData, isLocationTracked: v})} />
                  </div>
                </div>
                {formData.isLotTracked && (
                  <div className="border rounded-lg p-4 mt-4">
                    <h4 className="font-medium mb-3">Lot Settings</h4>
                    <div className="grid grid-cols-2 gap-4">
                      <div className="space-y-2"><Label>Lot Category</Label>
                        <Input value={formData.lotCategory || ''} onChange={(e) => setFormData({...formData, lotCategory: e.target.value})} />
                      </div>
                      <div className="space-y-2"><Label>Minimum Shelf Life (Days)</Label>
                        <Input type="number" value={formData.minimumShelfLifeDays} onChange={(e) => setFormData({...formData, minimumShelfLifeDays: parseInt(e.target.value) || 0})} />
                      </div>
                    </div>
                    <div className="flex items-center space-x-4 mt-4">
                      <div className="flex items-center space-x-2">
                        <Switch checked={formData.warnBeforeLotExpires} onCheckedChange={(v) => setFormData({...formData, warnBeforeLotExpires: v})} />
                        <Label>Warn Before Expiry</Label>
                      </div>
                      {formData.warnBeforeLotExpires && (
                        <div className="flex items-center space-x-2">
                          <Label className="text-sm">Days:</Label>
                          <Input type="number" className="w-20" value={formData.daysBeforeExpiryWarning} onChange={(e) => setFormData({...formData, daysBeforeExpiryWarning: parseInt(e.target.value) || 30})} />
                        </div>
                      )}
                    </div>
                  </div>
                )}
              </TabsContent>
            </Tabs>
            <DialogFooter className="shrink-0 pt-4">
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>Cancel</Button>
              <Button onClick={handleCreate}>Create</Button>
            </DialogFooter>
          </DialogContent>
          </Dialog>
        </div>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/inventory">Inventory</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>Items</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold">{items.length}</p><p className="text-sm text-muted-foreground">Total Items</p></div>
            <Package className="h-8 w-8 text-blue-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold">{items.filter(i => i.isActive).length}</p><p className="text-sm text-muted-foreground">Active Items</p></div>
            <TrendingUp className="h-8 w-8 text-green-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold text-orange-600">{lowStockItems.length}</p><p className="text-sm text-muted-foreground">Low Stock</p></div>
            <AlertTriangle className="h-8 w-8 text-orange-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold">{items.filter(i => i.isSerialTracked || i.isLotTracked).length}</p><p className="text-sm text-muted-foreground">Tracked Items</p></div>
            <BarChart3 className="h-8 w-8 text-purple-500" />
          </div>
        </CardContent></Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader><CardTitle className="flex items-center"><Filter className="h-4 w-4 mr-2" />Filters</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input placeholder="Search items..." className="pl-8" value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} />
            </div>
            <Select value={typeFilter} onValueChange={setTypeFilter}>
              <SelectTrigger><SelectValue placeholder="Item Type" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                {ItemTypes.map(t => <SelectItem key={t.value} value={t.value.toString()}>{t.label}</SelectItem>)}
              </SelectContent>
            </Select>
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger><SelectValue placeholder="Status" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Status</SelectItem>
                {ItemStatuses.map(s => <SelectItem key={s.value} value={s.value.toString()}>{s.label}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Items List */}
      <Card>
        <CardHeader>
          <CardTitle>Inventory Items</CardTitle>
          <CardDescription>{loading ? 'Loading...' : `${filteredItems.length} item(s) found`}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading && <div className="text-center py-8 text-muted-foreground">Loading...</div>}
          {error && <div className="text-center py-8 text-red-600">{error}</div>}
          {!loading && !error && (
            <div className="space-y-3">
              {filteredItems.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">No items found.</div>
              ) : (
                filteredItems.map((item) => (
                  <div key={item.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center space-x-4">
                        <div className={`w-12 h-12 rounded-lg flex items-center justify-center ${item.availableStock <= item.reorderLevel ? 'bg-orange-100' : 'bg-blue-100'}`}>
                          <Package className={`h-6 w-6 ${item.availableStock <= item.reorderLevel ? 'text-orange-600' : 'text-blue-600'}`} />
                        </div>
                        <div>
                          <div className="flex items-center space-x-2">
                            <h3 className="font-semibold">{item.name}</h3>
                            <Badge variant="outline">{item.itemCode}</Badge>
                            <Badge className={item.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>{getStatusName(item.status)}</Badge>
                            <Badge variant="secondary">{getItemTypeName(item.itemType)}</Badge>
                            {item.isSerialTracked && <Badge className="bg-purple-100 text-purple-800">Serial</Badge>}
                            {item.isLotTracked && <Badge className="bg-indigo-100 text-indigo-800">Lot</Badge>}
                          </div>
                          <p className="text-sm text-muted-foreground">
                            Stock: {item.availableStock} / {item.currentStock} • Standard cost: <InventoryCostValue value={item.standardCost} kind="standard" currencyCode={costCurrency} /> • Item-wide average cost: <InventoryCostValue value={item.averageCost} kind="item" currencyCode={costCurrency} /> • {item.categoryName || 'No Category'}
                          </p>
                        </div>
                      </div>
                      <div className="flex items-center space-x-2">
                        <Button size="sm" variant="outline" onClick={() => openSuppliers(item)}><Users className="h-4 w-4 mr-1" />Suppliers</Button>
                        <Button size="sm" variant="outline" onClick={() => openHistory(item)}><History className="h-4 w-4 mr-1" />History</Button>
                        <Button size="sm" variant="outline" onClick={() => handleEdit(item)}><Edit className="h-4 w-4" /></Button>
                        <Button
                          size="sm"
                          variant="outline"
                          className="text-red-600"
                          onClick={() => setDeleteTarget(item)}
                          aria-label={`Delete ${item.itemCode}`}
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </div>
                    </div>
                  </div>
                ))
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Edit Dialog - Same comprehensive form as Create */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="w-[1100px] max-w-[95vw] h-[85vh] max-h-[85vh] flex flex-col">
          <DialogHeader>
            <DialogTitle>Edit Inventory Item</DialogTitle>
            <DialogDescription>Editing: {selectedItem?.itemCode} - {selectedItem?.name}</DialogDescription>
          </DialogHeader>
          <Tabs defaultValue="basic" className="flex-1 flex flex-col min-h-0">
            <TabsList className="grid w-full grid-cols-6 shrink-0">
              <TabsTrigger value="basic">Basic Info</TabsTrigger>
              <TabsTrigger value="costs">Costs & Pricing</TabsTrigger>
              <TabsTrigger value="stock">Stock Settings</TabsTrigger>
              <TabsTrigger value="options">Options</TabsTrigger>
              <TabsTrigger value="tracking">Tracking</TabsTrigger>
              <TabsTrigger value="pricelists">Price Lists</TabsTrigger>
            </TabsList>
            <TabsContent value="basic" className="flex-1 overflow-y-auto pt-4">
              <div className="max-w-5xl mx-auto space-y-6 pb-4">
                
                {/* Status Bar - Edit Mode Only */}
                <div className="flex items-center justify-between p-4 border rounded-xl bg-gradient-to-r from-gray-50 to-gray-100 dark:from-gray-800 dark:to-gray-900 shadow-sm">
                  <div className="flex items-center space-x-4">
                    <div className="flex items-center space-x-2">
                      <Switch 
                        checked={editIsActive} 
                        onCheckedChange={(v) => {
                          setEditIsActive(v);
                          setFormData({...formData, status: v ? 1 : 2});
                        }}
                        className="data-[state=checked]:bg-green-600"
                      />
                      <Label className="font-semibold">
                        {editIsActive ? (
                          <span className="text-green-600 dark:text-green-400">● Active</span>
                        ) : (
                          <span className="text-gray-500">○ Inactive</span>
                        )}
                      </Label>
                    </div>
                    <Select
                      value={formData.status.toString()}
                      onValueChange={(value) => {
                        const status = Number(value);
                        setFormData({...formData, status});
                        setEditIsActive(status === 1);
                      }}
                    >
                      <SelectTrigger className="w-44"><SelectValue /></SelectTrigger>
                      <SelectContent>
                        {ItemStatuses.map(status => <SelectItem key={status.value} value={status.value.toString()}>{status.label}</SelectItem>)}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="flex items-center gap-6 text-sm">
                    <div className="flex items-center gap-2 px-3 py-1.5 bg-white dark:bg-gray-800 rounded-lg border">
                      <span className="text-muted-foreground">On Hand:</span> 
                      <span className="font-bold text-blue-600 dark:text-blue-400">{selectedItem?.currentStock?.toLocaleString() ?? 0}</span>
                    </div>
                    <div className="flex items-center gap-2 px-3 py-1.5 bg-white dark:bg-gray-800 rounded-lg border">
                      <span className="text-muted-foreground">Available:</span> 
                      <span className="font-bold text-green-600 dark:text-green-400">{selectedItem?.availableStock?.toLocaleString() ?? 0}</span>
                    </div>
                  </div>
                </div>

                {/* Hero Section - Product Image & Core Identity */}
                <div className="rounded-xl p-6 border">
                  <div className="flex items-start gap-6">
                    {/* Product Image */}
                    <div className="shrink-0">
                      <div className="w-40 h-40 border-2 border-dashed border-blue-200 dark:border-blue-800 rounded-xl flex items-center justify-center bg-white dark:bg-gray-900 overflow-hidden shadow-sm">
                        {imagePreview ? (
                          <img src={imagePreview} alt="Product" className="w-full h-full object-cover" />
                        ) : (
                          <div className="text-center text-muted-foreground">
                            <ImageIcon className="h-12 w-12 mx-auto mb-2 opacity-40" />
                            <p className="text-xs">No image</p>
                          </div>
                        )}
                      </div>
                      <div className="mt-3 space-y-2">
                        <div className="flex items-center gap-2">
                          <Input 
                            type="file" 
                            accept="image/jpeg,image/png,image/gif,image/webp" 
                            onChange={handleImageUpload} 
                            disabled={imageUploading} 
                            className="text-xs h-8"
                          />
                          {imagePreview && (
                            <Button variant="ghost" size="icon" className="h-8 w-8" onClick={handleRemoveImage} title="Remove image">
                              <X className="h-3 w-3" />
                            </Button>
                          )}
                        </div>
                        {imageUploading && (
                          <div className="flex items-center gap-2 text-xs text-muted-foreground">
                            <Upload className="h-3 w-3 animate-pulse" />
                            Uploading...
                          </div>
                        )}
                      </div>
                    </div>

                    {/* Core Identity Fields */}
                    <div className="flex-1 space-y-4">
                      <div className="grid grid-cols-2 gap-4">
                        <div className="space-y-2">
                          <Label className="text-sm font-semibold flex items-center gap-1">
                            Item Code
                          </Label>
                          <Input 
                            value={formData.itemCode} 
                            disabled 
                            className="bg-muted/50 font-mono text-base h-11 cursor-not-allowed"
                          />
                          <p className="text-xs text-muted-foreground">Item code cannot be changed</p>
                        </div>
                        <div className="space-y-2">
                          <Label className="text-sm font-semibold flex items-center gap-1">
                            Item Name <span className="text-red-500">*</span>
                          </Label>
                          <Input 
                            value={formData.name} 
                            onChange={(e) => setFormData({...formData, name: e.target.value})} 
                            placeholder="e.g., Premium Widget"
                            className="text-base h-11"
                          />
                        </div>
                      </div>

                      <div className="space-y-2">
                        <Label className="text-sm font-semibold">Short Description</Label>
                        <Input 
                          value={formData.shortDescription || ''} 
                          onChange={(e) => setFormData({...formData, shortDescription: e.target.value})} 
                          placeholder="Brief description for lists and reports"
                          className="h-10"
                        />
                      </div>

                      <div className="space-y-2">
                        <Label className="text-sm font-semibold">Full Description</Label>
                        <Textarea 
                          value={formData.description} 
                          onChange={(e) => setFormData({...formData, description: e.target.value})} 
                          placeholder="Detailed description of the item..."
                          rows={3} 
                          className="resize-none"
                        />
                      </div>
                    </div>
                  </div>
                </div>

                {/* Classification Section */}
                <div className="bg-white dark:bg-gray-900 rounded-xl border shadow-sm">
                  <div className="px-6 py-4 border-b bg-gray-50 dark:bg-gray-800/50">
                    <h3 className="font-semibold text-base flex items-center gap-2">
                      <Package className="h-4 w-4 text-blue-600" />
                      Classification & Type
                    </h3>
                    <p className="text-xs text-muted-foreground mt-1">Define how this item is categorized and measured</p>
                  </div>
                  <div className="p-6 space-y-4">
                    <div className="grid grid-cols-3 gap-4">
                      <div className="space-y-2">
                        <Label className="text-sm font-medium">Item Type</Label>
                        <Select value={formData.itemType.toString()} onValueChange={(v) => setFormData({...formData, itemType: parseInt(v)})}>
                          <SelectTrigger className="h-10">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {ItemTypes.map(t => (
                              <SelectItem key={t.value} value={t.value.toString()}>
                                {t.label}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                      <div className="space-y-2">
                        <Label className="text-sm font-medium flex items-center gap-1">
                          Category <span className="text-red-500">*</span>
                        </Label>
                        <Select value={formData.categoryId || 'none'} onValueChange={(v) => setFormData({...formData, categoryId: v === 'none' ? '' : v})}>
                          <SelectTrigger className="h-10">
                            <SelectValue placeholder="Select Category" />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="none">Select Category...</SelectItem>
                            {categories.map(c => (
                              <SelectItem key={c.id} value={c.id}>
                                {c.code ? `${c.code} - ` : ''}{c.name}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                      <div className="space-y-2">
                        <Label className="text-sm font-medium">Barcode</Label>
                        <Input 
                          value={formData.barcode || ''} 
                          onChange={(e) => setFormData({...formData, barcode: e.target.value})} 
                          placeholder="Scan or enter barcode"
                          className="h-10 font-mono"
                        />
                      </div>
                    </div>
                  </div>
                </div>

                {/* Measurement & Valuation Section */}
                <div className="bg-white dark:bg-gray-900 rounded-xl border shadow-sm">
                  <div className="px-6 py-4 border-b bg-gray-50 dark:bg-gray-800/50">
                    <h3 className="font-semibold text-base flex items-center gap-2">
                      <BarChart3 className="h-4 w-4 text-green-600" />
                      Measurement & Valuation
                    </h3>
                    <p className="text-xs text-muted-foreground mt-1">Configure units of measure and inventory valuation</p>
                  </div>
                  <div className="p-6 space-y-4">
                    <div className="grid grid-cols-2 gap-4">
                      <div className="space-y-2">
                        <Label className="text-sm font-medium">Unit of Measure Schedule</Label>
                        <Select
                          value={formData.unitOfMeasureScheduleId || 'none'}
                          onValueChange={(v) => {
                            const schedule = uomSchedules.find(s => s.id === v);
                            setFormData({
                              ...formData,
                              unitOfMeasureScheduleId: v === 'none' ? undefined : v,
                              unitOfMeasure: schedule?.baseUnitOfMeasureName || formData.unitOfMeasure
                            });
                          }}
                        >
                          <SelectTrigger className="h-10">
                            <SelectValue placeholder="Select UoM Schedule" />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="none">No Schedule (Single Unit)</SelectItem>
                            {uomSchedules.map(s => (
                              <SelectItem key={s.id} value={s.id}>
                                {s.scheduleId} - {s.description}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                        {formData.unitOfMeasureScheduleId && (
                          <p className="text-xs text-blue-600 dark:text-blue-400 flex items-center gap-1 mt-1">
                            <span className="font-medium">Base Unit:</span> 
                            {uomSchedules.find(s => s.id === formData.unitOfMeasureScheduleId)?.baseUnitOfMeasureName || 'N/A'}
                          </p>
                        )}
                      </div>
                      <div className="space-y-2">
                        <Label className="text-sm font-medium">Valuation Method</Label>
                        <Select
                          value={formData.valuationMethod?.toString() || '1'}
                          onValueChange={(v) => setFormData({...formData, valuationMethod: parseInt(v)})}
                          disabled={selectedItem?.isValuationLocked}
                        >
                          <SelectTrigger className="h-10">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {ValuationMethods.map(m => (
                              <SelectItem key={m.value} value={m.value}>
                                {m.label}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                        <p className={`text-xs flex items-center gap-1 mt-1 ${selectedItem?.isValuationLocked ? 'text-red-600 dark:text-red-400' : 'text-amber-600 dark:text-amber-400'}`}>
                          <AlertTriangle className="h-3 w-3" />
                          {selectedItem?.isValuationLocked ? 'Locked - has transactions' : 'Cannot be changed after first transaction'}
                        </p>
                      </div>
                    </div>

                    <div className="grid grid-cols-2 gap-4">
                      <div className="space-y-2">
                        <Label className="text-sm font-medium">Quantity Decimals</Label>
                        <Select value={(formData.quantityDecimals ?? 2).toString()} onValueChange={(v) => setFormData({...formData, quantityDecimals: parseInt(v)})}>
                          <SelectTrigger className="h-10">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {[0,1,2,3,4,5].map(d => (
                              <SelectItem key={d} value={d.toString()}>
                                {d} decimal place{d !== 1 ? 's' : ''}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                      <div className="space-y-2">
                        <Label className="text-sm font-medium">Currency Decimals</Label>
                        <Select value={(formData.currencyDecimals ?? 2).toString()} onValueChange={(v) => setFormData({...formData, currencyDecimals: parseInt(v)})}>
                          <SelectTrigger className="h-10">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {[0,1,2,3,4,5].map(d => (
                              <SelectItem key={d} value={d.toString()}>
                                {d} decimal place{d !== 1 ? 's' : ''}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                    </div>
                  </div>
                </div>

                {/* Product Details Section */}
                <div className="bg-white dark:bg-gray-900 rounded-xl border shadow-sm">
                  <div className="px-6 py-4 border-b bg-gray-50 dark:bg-gray-800/50">
                    <h3 className="font-semibold text-base flex items-center gap-2">
                      <Package className="h-4 w-4 text-purple-600" />
                      Product Details
                    </h3>
                    <p className="text-xs text-muted-foreground mt-1">Additional product information and attributes</p>
                  </div>
                  <div className="p-6 space-y-4">
                    <div className="grid grid-cols-2 gap-4">
                      <div className="space-y-2">
                        <Label className="text-sm font-medium">{getLabel('Brand')}</Label>
                        <Input 
                          value={formData.brand || ''} 
                          onChange={(e) => setFormData({...formData, brand: e.target.value})} 
                          placeholder="e.g., Acme Corp"
                          className="h-10"
                        />
                      </div>
                      <div className="space-y-2">
                        <Label className="text-sm font-medium">{getLabel('Manufacturer')}</Label>
                        <Input 
                          value={formData.manufacturer || ''} 
                          onChange={(e) => setFormData({...formData, manufacturer: e.target.value})} 
                          placeholder="e.g., Global Manufacturing"
                          className="h-10"
                        />
                      </div>
                    </div>

                    <div className="grid grid-cols-3 gap-4">
                      <div className="space-y-2">
                        <Label className="text-sm font-medium">Model</Label>
                        <Input 
                          value={formData.model || ''} 
                          onChange={(e) => setFormData({...formData, model: e.target.value})} 
                          placeholder="Model number"
                          className="h-10"
                        />
                      </div>
                      <div className="space-y-2">
                        <Label className="text-sm font-medium">{getLabel('Style')}</Label>
                        <Input 
                          value={formData.style || ''} 
                          onChange={(e) => setFormData({...formData, style: e.target.value})} 
                          placeholder="Style variant"
                          className="h-10"
                        />
                      </div>
                      <div className="space-y-2">
                        <Label className="text-sm font-medium">{getLabel('Feature')}</Label>
                        <Input 
                          value={formData.feature || ''} 
                          onChange={(e) => setFormData({...formData, feature: e.target.value})} 
                          placeholder="Key feature"
                          className="h-10"
                        />
                      </div>
                    </div>

                    <div className="space-y-2">
                      <Label className="text-sm font-medium">Generic Description</Label>
                      <Input 
                        value={formData.genericDescription || ''} 
                        onChange={(e) => setFormData({...formData, genericDescription: e.target.value})} 
                        placeholder="Generic or alternative description"
                        className="h-10"
                      />
                    </div>
                  </div>
                </div>

              </div>
            </TabsContent>
            <TabsContent value="costs" className="flex-1 overflow-y-auto space-y-4 pt-4">
              <div className="rounded-md border p-3">
                <Label>Item-wide average cost (read-only)</Label>
                <div className="mt-1 font-semibold"><InventoryCostValue value={selectedItem?.averageCost} kind="item" currencyCode={costCurrency} /></div>
                <p className="mt-1 text-xs text-muted-foreground">Stored across warehouses. Count posting uses its saved valuation cost, not this reference value.</p>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2"><Label>Standard Cost</Label><Input type="number" step="0.01" value={formData.standardCost} onChange={(e) => setFormData({...formData, standardCost: parseFloat(e.target.value) || 0})} /></div>
                <div className="space-y-2"><Label>Current Cost</Label><Input type="number" step="0.01" value={formData.currentCost} onChange={(e) => setFormData({...formData, currentCost: parseFloat(e.target.value) || 0})} /></div>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2"><Label>List Price</Label><Input type="number" step="0.01" value={formData.listPrice} onChange={(e) => setFormData({...formData, listPrice: parseFloat(e.target.value) || 0})} /></div>
                <div className="space-y-2"><Label>Selling Price</Label><Input type="number" step="0.01" value={formData.sellingPrice || 0} onChange={(e) => setFormData({...formData, sellingPrice: parseFloat(e.target.value) || 0})} /></div>
              </div>
              <div className="border-t pt-4">
                <h4 className="font-medium mb-3">Tax Settings</h4>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2"><Label>Purchase Tax Option</Label>
                    <Select value={formData.purchaseTaxOption || ''} onValueChange={(v) => setFormData({...formData, purchaseTaxOption: v})}>
                      <SelectTrigger><SelectValue placeholder="Select..." /></SelectTrigger>
                      <SelectContent>{TaxOptions.map(t => <SelectItem key={t.value} value={t.value}>{t.label}</SelectItem>)}</SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2"><Label>Sales Tax Option</Label>
                    <Select value={formData.salesTaxOption || ''} onValueChange={(v) => setFormData({...formData, salesTaxOption: v})}>
                      <SelectTrigger><SelectValue placeholder="Select..." /></SelectTrigger>
                      <SelectContent>{TaxOptions.map(t => <SelectItem key={t.value} value={t.value}>{t.label}</SelectItem>)}</SelectContent>
                    </Select>
                  </div>
                </div>
                <div className="flex items-center space-x-2 mt-4">
                  <Switch checked={formData.isTaxable} onCheckedChange={(v) => setFormData({...formData, isTaxable: v})} />
                  <Label>Item is Taxable</Label>
                </div>
              </div>
            </TabsContent>
            <TabsContent value="stock" className="flex-1 overflow-y-auto space-y-4 pt-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2"><Label>Minimum Level</Label><Input type="number" value={formData.minimumLevel} onChange={(e) => setFormData({...formData, minimumLevel: parseFloat(e.target.value) || 0})} /></div>
                <div className="space-y-2"><Label>Maximum Level</Label><Input type="number" value={formData.maximumLevel} onChange={(e) => setFormData({...formData, maximumLevel: parseFloat(e.target.value) || 0})} /></div>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2"><Label>Reorder Level</Label><Input type="number" value={formData.reorderLevel} onChange={(e) => setFormData({...formData, reorderLevel: parseFloat(e.target.value) || 0})} /></div>
                <div className="space-y-2"><Label>Reorder Quantity</Label><Input type="number" value={formData.reorderQuantity} onChange={(e) => setFormData({...formData, reorderQuantity: parseFloat(e.target.value) || 0})} /></div>
              </div>
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2"><Label>Lead Time (Days)</Label><Input type="number" value={formData.leadTimeDays} onChange={(e) => setFormData({...formData, leadTimeDays: parseInt(e.target.value) || 0})} /></div>
                <div className="space-y-2"><Label>Safety Lead Time (Days)</Label><Input type="number" value={formData.safetyLeadTimeDays} onChange={(e) => setFormData({...formData, safetyLeadTimeDays: parseInt(e.target.value) || 0})} /></div>
                <div className="space-y-2"><Label>Safety Stock</Label><Input type="number" value={formData.safetyStock} onChange={(e) => setFormData({...formData, safetyStock: parseFloat(e.target.value) || 0})} /></div>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2"><Label>Shipping Weight</Label><Input type="number" step="0.01" value={formData.shippingWeight} onChange={(e) => setFormData({...formData, shippingWeight: parseFloat(e.target.value) || 0})} /></div>
                <div className="space-y-2"><Label>Warranty Days</Label><Input type="number" value={formData.warrantyDays} onChange={(e) => setFormData({...formData, warrantyDays: parseInt(e.target.value) || 0})} /></div>
              </div>
              <div className="flex items-center space-x-6 pt-2">
                <div className="flex items-center space-x-2"><Switch checked={formData.allowBackorder} onCheckedChange={(v) => setFormData({...formData, allowBackorder: v})} /><Label>Allow Backorder</Label></div>
                <div className="flex items-center space-x-2"><Switch checked={formData.autoReorder} onCheckedChange={(v) => setFormData({...formData, autoReorder: v})} /><Label>Auto Reorder</Label></div>
              </div>
            </TabsContent>
            <TabsContent value="options" className="flex-1 overflow-y-auto space-y-4 pt-4">
              <div className="border rounded-lg p-4">
                <h4 className="font-medium mb-3">Item Classification</h4>
                <div className="grid grid-cols-2 gap-4">
                  <div className="flex items-center space-x-2"><Switch checked={formData.isKit} onCheckedChange={(v) => setFormData({...formData, isKit: v})} /><Label>Kit</Label></div>
                  <div className="flex items-center space-x-2"><Switch checked={formData.isKitComponent} onCheckedChange={(v) => setFormData({...formData, isKitComponent: v})} /><Label>Kit Component</Label></div>
                  <div className="flex items-center space-x-2"><Switch checked={formData.isFinishedGood} onCheckedChange={(v) => setFormData({...formData, isFinishedGood: v})} /><Label>Finished Good</Label></div>
                  <div className="flex items-center space-x-2"><Switch checked={formData.isFinishedGoodComponent} onCheckedChange={(v) => setFormData({...formData, isFinishedGoodComponent: v})} /><Label>Finished Good Component</Label></div>
                  <div className="flex items-center space-x-2"><Switch checked={formData.isProcurementItem} onCheckedChange={(v) => setFormData({...formData, isProcurementItem: v})} /><Label>Non-Saleable Item</Label></div>
                  <div className="flex items-center space-x-2"><Switch checked={formData.isProjectApplicable} onCheckedChange={(v) => setFormData({...formData, isProjectApplicable: v})} /><Label>Project applicable</Label></div>
                  <div className="flex items-center space-x-2"><Switch checked={formData.isCostCentreApplicable} onCheckedChange={(v) => setFormData({...formData, isCostCentreApplicable: v})} /><Label>Cost-centre applicable</Label></div>
                </div>
              </div>
              <div className="border rounded-lg p-4">
                <h4 className="font-medium mb-3">Include in Catalog</h4>
                <div className="grid grid-cols-2 gap-4">
                  <div className="flex items-center space-x-2"><Switch checked={formData.includeInQuotes} onCheckedChange={(v) => setFormData({...formData, includeInQuotes: v})} /><Label>Quotes</Label></div>
                  <div className="flex items-center space-x-2"><Switch checked={formData.includeInOrders} onCheckedChange={(v) => setFormData({...formData, includeInOrders: v})} /><Label>Orders</Label></div>
                  <div className="flex items-center space-x-2"><Switch checked={formData.includeInInvoices} onCheckedChange={(v) => setFormData({...formData, includeInInvoices: v})} /><Label>Invoices</Label></div>
                  <div className="flex items-center space-x-2"><Switch checked={formData.includeInFulfillment} onCheckedChange={(v) => setFormData({...formData, includeInFulfillment: v})} /><Label>Fulfillment</Label></div>
                </div>
              </div>
              <div className="border rounded-lg p-4">
                <h4 className="font-medium mb-3">Maintain History</h4>
                <div className="grid grid-cols-3 gap-4">
                  <div className="flex items-center space-x-2"><Switch checked={formData.maintainCalendarYearHistory} onCheckedChange={(v) => setFormData({...formData, maintainCalendarYearHistory: v})} /><Label>Calendar Year</Label></div>
                  <div className="flex items-center space-x-2"><Switch checked={formData.maintainFiscalYearHistory} onCheckedChange={(v) => setFormData({...formData, maintainFiscalYearHistory: v})} /><Label>Fiscal Year</Label></div>
                  <div className="flex items-center space-x-2"><Switch checked={formData.maintainTransactionHistory} onCheckedChange={(v) => setFormData({...formData, maintainTransactionHistory: v})} /><Label>Transaction</Label></div>
                </div>
              </div>
              <div className="border rounded-lg p-4">
                <h4 className="font-medium mb-3">Substitute Items</h4>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2"><Label>Substitute Item 1</Label>
                    <Select value={formData.substituteItem1Id || 'none'} onValueChange={(v) => setFormData({...formData, substituteItem1Id: v === 'none' ? undefined : v})}>
                      <SelectTrigger><SelectValue placeholder="Select substitute..." /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">None</SelectItem>
                        {items.filter(i => i.id !== selectedItem?.id).map(i => <SelectItem key={i.id} value={i.id}>{i.itemCode} - {i.name}</SelectItem>)}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2"><Label>Substitute Item 2</Label>
                    <Select value={formData.substituteItem2Id || 'none'} onValueChange={(v) => setFormData({...formData, substituteItem2Id: v === 'none' ? undefined : v})}>
                      <SelectTrigger><SelectValue placeholder="Select substitute..." /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">None</SelectItem>
                        {items.filter(i => i.id !== selectedItem?.id).map(i => <SelectItem key={i.id} value={i.id}>{i.itemCode} - {i.name}</SelectItem>)}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2"><Label>Substitute Item 3</Label>
                    <Select value={formData.substituteItem3Id || 'none'} onValueChange={(v) => setFormData({...formData, substituteItem3Id: v === 'none' ? undefined : v})}>
                      <SelectTrigger><SelectValue placeholder="Select substitute..." /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">None</SelectItem>
                        {items.filter(i => i.id !== selectedItem?.id).map(i => <SelectItem key={i.id} value={i.id}>{i.itemCode} - {i.name}</SelectItem>)}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2"><Label>Substitute Item 4</Label>
                    <Select value={formData.substituteItem4Id || 'none'} onValueChange={(v) => setFormData({...formData, substituteItem4Id: v === 'none' ? undefined : v})}>
                      <SelectTrigger><SelectValue placeholder="Select substitute..." /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">None</SelectItem>
                        {items.filter(i => i.id !== selectedItem?.id).map(i => <SelectItem key={i.id} value={i.id}>{i.itemCode} - {i.name}</SelectItem>)}
                      </SelectContent>
                    </Select>
                  </div>
                </div>
              </div>
            </TabsContent>
            <TabsContent value="tracking" className="flex-1 overflow-y-auto space-y-4 pt-4">
              <div className="space-y-3">
                <div className="flex items-center justify-between p-3 border rounded-lg">
                  <div><Label>Serial Number Tracking</Label><p className="text-xs text-muted-foreground">Track individual items by serial number</p></div>
                  <Switch checked={formData.isSerialTracked} onCheckedChange={(v) => setFormData({...formData, isSerialTracked: v})} />
                </div>
                <div className="flex items-center justify-between p-3 border rounded-lg">
                  <div><Label>Lot Tracking</Label><p className="text-xs text-muted-foreground">Track items by lot/batch number</p></div>
                  <Switch checked={formData.isLotTracked} onCheckedChange={(v) => setFormData({...formData, isLotTracked: v})} />
                </div>
                <div className="flex items-center justify-between p-3 border rounded-lg">
                  <div><Label>Batch Tracking</Label><p className="text-xs text-muted-foreground">Require a distinct production batch identifier</p></div>
                  <Switch checked={formData.isBatchTracked} onCheckedChange={(v) => setFormData({...formData, isBatchTracked: v})} />
                </div>
                <div className="flex items-center justify-between p-3 border rounded-lg">
                  <div><Label>Manufacture Date Tracking</Label><p className="text-xs text-muted-foreground">Require manufacture date capture on receipt</p></div>
                  <Switch checked={formData.isManufactureDateTracked} onCheckedChange={(v) => setFormData({...formData, isManufactureDateTracked: v})} />
                </div>
                <div className="flex items-center justify-between p-3 border rounded-lg">
                  <div><Label>Expiration Tracking</Label><p className="text-xs text-muted-foreground">Track items by expiration date</p></div>
                  <Switch checked={formData.isExpirationTracked} onCheckedChange={(v) => setFormData({...formData, isExpirationTracked: v})} />
                </div>
                <div className="flex items-center justify-between p-3 border rounded-lg">
                  <div><Label>Location Tracking</Label><p className="text-xs text-muted-foreground">Track items by warehouse location</p></div>
                  <Switch checked={formData.isLocationTracked} onCheckedChange={(v) => setFormData({...formData, isLocationTracked: v})} />
                </div>
              </div>
              {formData.isLotTracked && (
                <div className="border rounded-lg p-4 mt-4">
                  <h4 className="font-medium mb-3">Lot Settings</h4>
                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-2"><Label>Lot Category</Label><Input value={formData.lotCategory || ''} onChange={(e) => setFormData({...formData, lotCategory: e.target.value})} /></div>
                    <div className="space-y-2"><Label>Minimum Shelf Life (Days)</Label><Input type="number" value={formData.minimumShelfLifeDays} onChange={(e) => setFormData({...formData, minimumShelfLifeDays: parseInt(e.target.value) || 0})} /></div>
                  </div>
                  <div className="flex items-center space-x-4 mt-4">
                    <div className="flex items-center space-x-2"><Switch checked={formData.warnBeforeLotExpires} onCheckedChange={(v) => setFormData({...formData, warnBeforeLotExpires: v})} /><Label>Warn Before Expiry</Label></div>
                    {formData.warnBeforeLotExpires && (
                      <div className="flex items-center space-x-2"><Label className="text-sm">Days:</Label><Input type="number" className="w-20" value={formData.daysBeforeExpiryWarning} onChange={(e) => setFormData({...formData, daysBeforeExpiryWarning: parseInt(e.target.value) || 30})} /></div>
                    )}
                  </div>
                </div>
              )}
            </TabsContent>
            <TabsContent value="pricelists" className="flex-1 overflow-y-auto space-y-4 pt-4">
              <div className="flex items-center justify-between">
                <div>
                  <h4 className="font-medium">Price Lists</h4>
                  <p className="text-sm text-muted-foreground">Price lists that include this item</p>
                </div>
                <Badge variant="outline"><DollarSign className="h-3 w-3 mr-1" />{itemPriceListLines.length} price list(s)</Badge>
              </div>
              {loadingPriceLines ? (
                <div className="flex items-center justify-center py-8 text-muted-foreground">Loading price lists...</div>
              ) : itemPriceListLines.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-8 text-muted-foreground">
                  <DollarSign className="h-12 w-12 mb-2 opacity-50" />
                  <p>This item is not in any price lists</p>
                </div>
              ) : (
                <div className="border rounded-lg">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Price List</TableHead>
                        <TableHead>Type</TableHead>
                        <TableHead>Status</TableHead>
                        <TableHead className="text-right">Base Price</TableHead>
                        <TableHead className="text-right">Discount</TableHead>
                        <TableHead className="text-right">Net Price</TableHead>
                        <TableHead>Effective</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {itemPriceListLines.map(line => (
                        <TableRow key={line.id}>
                          <TableCell>
                            <div className="font-medium">{line.priceListCode}</div>
                            <div className="text-xs text-muted-foreground">{line.priceListName}</div>
                          </TableCell>
                          <TableCell><Badge variant="outline">{getPriceListTypeLabel(line.priceListType)}</Badge></TableCell>
                          <TableCell><Badge variant={line.priceListStatus === 1 ? 'default' : 'secondary'}>{getPriceListStatusLabel(line.priceListStatus)}</Badge></TableCell>
                          <TableCell className="text-right font-medium">{line.currency} {line.basePrice.toFixed(2)}</TableCell>
                          <TableCell className="text-right">{line.discountPercent > 0 ? `${line.discountPercent.toFixed(1)}%` : '-'}</TableCell>
                          <TableCell className="text-right font-medium text-primary">{line.currency} {line.netPrice.toFixed(2)}</TableCell>
                          <TableCell className="text-xs">
                            {line.effectiveFrom ? format(new Date(line.effectiveFrom), 'dd MMM yyyy') : '-'}
                            {line.effectiveTo && <span className="text-muted-foreground"> to {format(new Date(line.effectiveTo), 'dd MMM yyyy')}</span>}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </TabsContent>
          </Tabs>
          <DialogFooter className="shrink-0 pt-4">
            <Button variant="outline" onClick={() => setIsEditDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleUpdate}>Save Changes</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Validated item-profile import */}
      <Dialog open={isImportDialogOpen} onOpenChange={setIsImportDialogOpen}>
        <DialogContent className="sm:max-w-[760px]">
          <DialogHeader>
            <DialogTitle>Import TDC item profiles</DialogTitle>
            <DialogDescription>
              Paste a JSON array of item profiles. The complete batch is validated and committed atomically through the same rules as create, edit and maker-checker changes.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div className="rounded-md border bg-muted/30 p-3 text-xs text-muted-foreground">
              Required fields include itemCode, name, categoryId and unitOfMeasure. Weighted Average (`valuationMethod: 1`) is the default; transaction-derived stock and WAC values are never imported.
            </div>
            <Textarea
              className="min-h-64 font-mono text-xs"
              value={importJson}
              onChange={(event) => setImportJson(event.target.value)}
              aria-label="Item profile import JSON"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsImportDialogOpen(false)} disabled={isImporting}>Cancel</Button>
            <Button onClick={handleImport} disabled={isImporting}>{isImporting ? 'Importing…' : 'Validate and import'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Immutable change history */}
      <Dialog open={isHistoryDialogOpen} onOpenChange={setIsHistoryDialogOpen}>
        <DialogContent className="sm:max-w-[900px] max-h-[85vh] overflow-hidden flex flex-col">
          <DialogHeader>
            <DialogTitle>Item change history</DialogTitle>
            <DialogDescription>{selectedItem?.itemCode} — {selectedItem?.name}</DialogDescription>
          </DialogHeader>
          <div className="overflow-y-auto border rounded-md">
            {historyLoading ? (
              <div className="p-8 text-center text-muted-foreground">Loading history…</div>
            ) : historyEntries.length === 0 ? (
              <div className="p-8 text-center text-muted-foreground">No recorded changes.</div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow><TableHead>When</TableHead><TableHead>Actor</TableHead><TableHead>Action</TableHead><TableHead>Profile snapshot</TableHead></TableRow>
                </TableHeader>
                <TableBody>
                  {historyEntries.map(entry => (
                    <TableRow key={entry.id}>
                      <TableCell className="whitespace-nowrap text-xs">{format(new Date(entry.occurredAtUtc), 'dd MMM yyyy HH:mm')}</TableCell>
                      <TableCell>{entry.username}</TableCell>
                      <TableCell><Badge variant="outline">{entry.action}</Badge></TableCell>
                      <TableCell><pre className="max-w-[420px] whitespace-pre-wrap break-all text-[11px] text-muted-foreground">{entry.newValues || '—'}</pre></TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </div>
          <DialogFooter><Button variant="outline" onClick={() => setIsHistoryDialogOpen(false)}>Close</Button></DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Suppliers Dialog */}
      <Dialog open={isSupplierDialogOpen} onOpenChange={setIsSupplierDialogOpen}>
        <DialogContent className="sm:max-w-[600px]">
          <DialogHeader>
            <DialogTitle>Suppliers - {selectedItem?.name}</DialogTitle>
            <DialogDescription>Manage suppliers for this item</DialogDescription>
          </DialogHeader>
          <div className="border rounded-lg divide-y max-h-60 overflow-y-auto">
            {suppliers.length === 0 ? (
              <div className="p-4 text-center text-muted-foreground">No suppliers linked</div>
            ) : (
              suppliers.map(s => (
                <div key={s.id} className="p-3 flex items-center justify-between">
                  <div>
                    <div className="flex items-center space-x-2">
                      <span className="font-medium">{s.supplierName}</span>
                      {s.isPreferred && <Badge className="bg-yellow-100 text-yellow-800">Preferred</Badge>}
                    </div>
                    <p className="text-sm text-muted-foreground">
                      Code: {s.supplierItemCode || '-'} • Cost: ${(s.unitPrice ?? 0).toFixed(2)} • Lead: {s.leadTimeDays ?? 0} days
                    </p>
                  </div>
                </div>
              ))
            )}
          </div>
          <DialogFooter><Button variant="outline" onClick={() => setIsSupplierDialogOpen(false)}>Close</Button></DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={deleteTarget !== null}
        onOpenChange={(open) => {
          if (!open && !isDeleting) setDeleteTarget(null);
        }}
        title="Delete inventory item"
        description={deleteTarget
          ? `Delete ${deleteTarget.itemCode} — ${deleteTarget.name}? This action is allowed only when the item has no protected transaction history.`
          : undefined}
        confirmText="Delete item"
        variant="destructive"
        onConfirm={handleDelete}
        isLoading={isDeleting}
      />
    </div>
  );
}
