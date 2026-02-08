'use client';

import React, { useState, useEffect, useMemo, Suspense } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Separator } from '@/components/ui/separator';
import { Badge } from '@/components/ui/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  ArrowLeft,
  Plus,
  Save,
  Send,
  Trash2,
  Check,
  X,
  Edit,
  Loader2,
  AlertCircle,
  Package,
  Calendar,
  DollarSign,
  FileText,
  Building2,
  TruckIcon,
  AlertTriangle
} from 'lucide-react';
import { toast } from 'sonner';
import {
  purchasingService,
  CreatePurchaseOrderDto,
  CreatePurchaseOrderItemDto,
  PurchaseRequisitionDetailDto
} from '@/services/purchasingService';
import { inventoryManagementService, InventoryItemDto, WarehouseDto, ItemUnitOfMeasureDto } from '@/services/inventoryManagementService';
import { businessPartnerService, BusinessPartnerDto, BusinessPartnerDetailDto } from '@/services/businessPartnerService';
import procurementSettingsService, { ProcurementSettingsDto } from '@/services/procurementSettingsService';
import pricingService, { PriceListLookupResult } from '@/services/pricingService';
import { format } from 'date-fns';

interface POItemFormData extends CreatePurchaseOrderItemDto {
  tempId: string;
  itemCode?: string;
  itemName?: string;
  warehouseId?: string;
  warehouseName?: string;
}

function NewPurchaseOrderPageContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const fromRequisitionId = searchParams.get('fromRequisition');
  
  const [saving, setSaving] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [loadingRequisition, setLoadingRequisition] = useState(false);
  
  // Form data
  const [selectedSupplierId, setSelectedSupplierId] = useState('');
  const [selectedSupplier, setSelectedSupplier] = useState<BusinessPartnerDetailDto | null>(null);
  const [requiredDate, setRequiredDate] = useState('');
  const [promisedDate, setPromisedDate] = useState('');
  const [paymentTerms, setPaymentTerms] = useState('');
  const [shippingTerms, setShippingTerms] = useState('');
  const [terms, setTerms] = useState('');
  const [notes, setNotes] = useState('');
  const [orderType, setOrderType] = useState<'Standard' | 'Consignment'>('Standard');
  const [deliveryWarehouseId, setDeliveryWarehouseId] = useState('');
  const [deliveryAddress, setDeliveryAddress] = useState('');
  const [deliveryInstructions, setDeliveryInstructions] = useState('');
  const [referenceNumber, setReferenceNumber] = useState('');
  const [items, setItems] = useState<POItemFormData[]>([]);
  
  // Financial fields
  const [taxAmount, setTaxAmount] = useState(0);
  const [shippingCost, setShippingCost] = useState(0);
  const [miscellaneousCost, setMiscellaneousCost] = useState(0);
  const [costAllocationMethod, setCostAllocationMethod] = useState<'SpreadToItemCost' | 'GLExpense'>('SpreadToItemCost');
  const [costApportionmentBasis, setCostApportionmentBasis] = useState<'Value' | 'Weight' | 'Quantity'>('Value');
  const [expenseGLAccount, setExpenseGLAccount] = useState('');
  const [discountAmount, setDiscountAmount] = useState(0);
  
  // Reference data
  const [inventoryItems, setInventoryItems] = useState<InventoryItemDto[]>([]);
  const [suppliers, setSuppliers] = useState<BusinessPartnerDto[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [loadingData, setLoadingData] = useState(true);
  const [sourceRequisition, setSourceRequisition] = useState<PurchaseRequisitionDetailDto | null>(null);
  
  // Procurement settings
  const [procurementSettings, setProcurementSettings] = useState<ProcurementSettingsDto | null>(null);
  
  // Inline editing state
  const [editingRowIndex, setEditingRowIndex] = useState<number | null>(null);
  const [isAddingNewRow, setIsAddingNewRow] = useState(false);
  const [editingItem, setEditingItem] = useState<POItemFormData | null>(null);
  const [availableUOMs, setAvailableUOMs] = useState<ItemUnitOfMeasureDto[]>([]);
  const [loadingUOMs, setLoadingUOMs] = useState(false);
  const [loadingPrice, setLoadingPrice] = useState(false);

  // Load reference data and settings
  useEffect(() => {
    const loadData = async () => {
      try {
        setLoadingData(true);
        const [itemsData, suppliersData, warehousesData, settingsData] = await Promise.all([
          inventoryManagementService.getInventoryItems({ isActive: true }),
          businessPartnerService.getActivePartners(),
          inventoryManagementService.getWarehouses(true),
          procurementSettingsService.getSettings()
        ]);
        
        setInventoryItems(itemsData || []);
        // Filter to only show Supplier or Both types
        setSuppliers((suppliersData || []).filter(bp =>
          bp.partnerType === 'Supplier' || bp.partnerType === 'Both'
        ));
        setWarehouses(warehousesData || []);
        setProcurementSettings(settingsData);
      } catch (error) {
        console.error('Error loading reference data:', error);
        toast.error('Failed to load reference data');
      } finally {
        setLoadingData(false);
      }
    };

    loadData();
  }, []);

  // Load requisition if creating from PR
  useEffect(() => {
    if (fromRequisitionId) {
      const loadRequisition = async () => {
        try {
          setLoadingRequisition(true);
          const pr = await purchasingService.getPurchaseRequisitionById(fromRequisitionId);
          setSourceRequisition(pr);
          
          // Pre-fill form data from requisition
          if (pr.requiredDate) setRequiredDate(pr.requiredDate.split('T')[0]);
          if (pr.notes) setNotes(pr.notes);
          if (pr.department) setDeliveryInstructions(`Department: ${pr.department}`);
          
          // Convert PR items to PO items
          const poItems: POItemFormData[] = pr.items.map((prItem, index) => ({
            tempId: `pr-${index}`,
            inventoryItemId: prItem.inventoryItemId || '',
            itemCode: prItem.itemCode,
            itemName: prItem.itemName,
            supplierItemCode: '',
            itemDescription: prItem.itemDescription || '',
            orderedQuantity: prItem.quantity,
            unitOfMeasure: prItem.unitOfMeasure || 'EA',
            unitPrice: prItem.estimatedUnitPrice,
            expectedDeliveryDate: prItem.requiredDate || '',
            notes: prItem.notes || ''
          }));
          
          setItems(poItems);
          
          // If all items have the same preferred supplier, pre-select it
          const preferredSuppliers = pr.items
            .map(item => item.preferredSupplierId)
            .filter(Boolean);
          
          if (preferredSuppliers.length > 0) {
            const firstSupplier = preferredSuppliers[0];
            const allSame = preferredSuppliers.every(s => s === firstSupplier);
            if (allSame && firstSupplier) {
              setSelectedSupplierId(firstSupplier);
              loadSupplierDetails(firstSupplier);
            }
          }
          
          toast.success('Requisition data loaded');
        } catch (error: any) {
          console.error('Error loading requisition:', error);
          toast.error('Failed to load requisition data');
        } finally {
          setLoadingRequisition(false);
        }
      };
      
      loadRequisition();
    }
  }, [fromRequisitionId]);

  // Load supplier details when selected
  const loadSupplierDetails = async (supplierId: string) => {
    try {
      const supplier = await businessPartnerService.getPartnerById(supplierId);
      setSelectedSupplier(supplier);
      
      // Pre-fill supplier-related fields
      if (supplier.paymentTerms) setPaymentTerms(supplier.paymentTerms);
      if (supplier.physicalAddress) {
        setDeliveryAddress(`${supplier.physicalAddress}${supplier.city ? ', ' + supplier.city : ''}${supplier.country ? ', ' + supplier.country : ''}`);
      }
    } catch (error) {
      console.error('Error loading supplier details:', error);
    }
  };

  const handleSupplierChange = (supplierId: string) => {
    setSelectedSupplierId(supplierId);
    if (supplierId) {
      loadSupplierDetails(supplierId);
    } else {
      setSelectedSupplier(null);
    }
  };

  // Calculate totals
  const calculateLineTotal = (quantity: number, unitPrice: number) => {
    return quantity * unitPrice;
  };

  const subTotal = items.reduce((sum, item) => 
    sum + calculateLineTotal(item.orderedQuantity, item.unitPrice), 0
  );
  
  const totalAdditionalCost = shippingCost + miscellaneousCost;
  const totalAmount = subTotal + taxAmount + totalAdditionalCost - discountAmount;

  const getApportionmentWeight = (item: POItemFormData) => {
    const inventoryItem = inventoryItems.find(invItem => invItem.id === item.inventoryItemId);
    const perUnitWeight = (inventoryItem?.shippingWeight ?? 0) > 0
      ? (inventoryItem?.shippingWeight ?? 0)
      : (inventoryItem?.weight ?? 0);
    return perUnitWeight * (item.orderedQuantity || 0);
  };

  const apportionmentBasisTotal = items.reduce((sum, item) => {
    if (costApportionmentBasis === 'Weight') return sum + getApportionmentWeight(item);
    if (costApportionmentBasis === 'Quantity') return sum + (item.orderedQuantity || 0);
    return sum + calculateLineTotal(item.orderedQuantity, item.unitPrice);
  }, 0);

  const allocationPreview = useMemo(() => {
    if (costAllocationMethod !== 'SpreadToItemCost' || totalAdditionalCost <= 0) {
      return {} as Record<string, { allocated: number; landedUnit: number }>;
    }

    const previewItems = [...items];
    if (editingRowIndex !== null && editingItem) {
      previewItems[editingRowIndex] = editingItem;
    }
    if (isAddingNewRow && editingItem) {
      previewItems.push(editingItem);
    }
    if (previewItems.length === 0) {
      return {} as Record<string, { allocated: number; landedUnit: number }>;
    }

    const buildBasis = (basis: 'Value' | 'Weight' | 'Quantity') =>
      previewItems.map(item => {
        if (basis === 'Weight') return getApportionmentWeight(item);
        if (basis === 'Quantity') return item.orderedQuantity || 0;
        return calculateLineTotal(item.orderedQuantity, item.unitPrice);
      });

    let basisValues = buildBasis(costApportionmentBasis);
    let totalBasis = basisValues.reduce((sum, value) => sum + value, 0);
    if (totalBasis <= 0) {
      basisValues = buildBasis('Value');
      totalBasis = basisValues.reduce((sum, value) => sum + value, 0);
    }
    if (totalBasis <= 0) {
      basisValues = buildBasis('Quantity');
      totalBasis = basisValues.reduce((sum, value) => sum + value, 0);
    }
    if (totalBasis <= 0) {
      return {} as Record<string, { allocated: number; landedUnit: number }>;
    }

    let runningAllocated = 0;
    const preview: Record<string, { allocated: number; landedUnit: number }> = {};

    previewItems.forEach((item, index) => {
      const allocated = index === previewItems.length - 1
        ? Number((totalAdditionalCost - runningAllocated).toFixed(2))
        : Number(((totalAdditionalCost * (basisValues[index] / totalBasis))).toFixed(2));

      if (index !== previewItems.length - 1) {
        runningAllocated += allocated;
      }

      const allocatedPerUnit = item.orderedQuantity > 0
        ? Number((allocated / item.orderedQuantity).toFixed(4))
        : 0;

      preview[item.tempId] = {
        allocated,
        landedUnit: Number((item.unitPrice + allocatedPerUnit).toFixed(4))
      };
    });

    return preview;
  }, [
    costAllocationMethod,
    totalAdditionalCost,
    costApportionmentBasis,
    items,
    editingRowIndex,
    editingItem,
    isAddingNewRow
  ]);

  // Handle inventory item selection in inline editing
  const handleInlineInventoryItemSelect = async (itemId: string) => {
    if (!editingItem) return;
    
    const item = inventoryItems.find(i => i.id === itemId);
    if (!item) return;

    try {
      setLoadingUOMs(true);
      setLoadingPrice(true);
      
      // Load available UOMs
      const uoms = await inventoryManagementService.getItemUnitsOfMeasure(itemId);
      setAvailableUOMs(uoms);
      
      // Set default to purchase UOM or base UOM
      const purchaseUOM = uoms.find(u => u.isPurchaseUnit) || uoms.find(u => u.isBaseUnit);
      let finalUOM = item.unitOfMeasure || 'EA';
      let finalUOMId: string | undefined = undefined;
      
      if (purchaseUOM?.unitCode) {
        finalUOM = purchaseUOM.unitCode;
        const isRealUOM = purchaseUOM.unitOfMeasureId && purchaseUOM.unitOfMeasureId !== '00000000-0000-0000-0000-000000000000';
        finalUOMId = isRealUOM ? purchaseUOM.id : undefined;
      }
      
      // Determine the price to use
      let finalPrice = item.lastPurchaseCost || item.standardCost || item.currentCost || 0;
      
      // Load price if supplier is selected
      if (selectedSupplierId) {
        try {
          const price = await pricingService.getSupplierItemPrice(
            selectedSupplierId,
            itemId,
            editingItem.orderedQuantity || 1
          );
          
          if (price) {
            finalPrice = price.netPrice;
            if (price.unitOfMeasure) {
              finalUOM = price.unitOfMeasure;
            }
          }
        } catch (priceError) {
          console.error('Error loading price:', priceError);
        }
      }
      
      setEditingItem({
        ...editingItem,
        inventoryItemId: item.id,
        itemCode: item.itemCode,
        itemName: item.name,
        itemDescription: item.description || item.name,
        unitOfMeasure: finalUOM,
        itemUnitOfMeasureId: finalUOMId,
        unitPrice: finalPrice
      });
      
    } catch (error) {
      console.error('Error loading item details:', error);
      toast.error('Failed to load item details');
    } finally {
      setLoadingUOMs(false);
      setLoadingPrice(false);
    }
  };

  // Start adding new row
  const handleAddNewRow = () => {
    if (!selectedSupplierId) {
      toast.error('Please select a supplier first');
      return;
    }
    
    setIsAddingNewRow(true);
    setEditingRowIndex(null);
    setEditingItem({
      tempId: Date.now().toString(),
      inventoryItemId: '',
      supplierItemCode: '',
      itemDescription: '',
      orderedQuantity: 1,
      unitOfMeasure: 'EA',
      unitPrice: 0,
      expectedDeliveryDate: '',
      notes: '',
      warehouseId: '',
      warehouseName: ''
    });
    setAvailableUOMs([]);
  };

  // Start editing existing row
  const handleEditRow = async (index: number) => {
    const item = items[index];
    setEditingRowIndex(index);
    setIsAddingNewRow(false);
    setEditingItem({ ...item });
    
    // Load UOMs if item has inventoryItemId
    if (item.inventoryItemId) {
      try {
        setLoadingUOMs(true);
        const uoms = await inventoryManagementService.getItemUnitsOfMeasure(item.inventoryItemId);
        setAvailableUOMs(uoms);
      } catch (error) {
        console.error('Error loading UOMs:', error);
        setAvailableUOMs([]);
      } finally {
        setLoadingUOMs(false);
      }
    } else {
      setAvailableUOMs([]);
    }
  };

  // Save inline edit
  const handleSaveInlineEdit = () => {
    if (!editingItem) return;
    
    // Validation
    if (!editingItem.inventoryItemId && !procurementSettings?.allowNonInventoryItems) {
      toast.error('Please select an inventory item');
      return;
    }
    
    if (!editingItem.inventoryItemId && !editingItem.itemDescription) {
      toast.error('Please provide an item description');
      return;
    }
    
    if (!editingItem.warehouseId) {
      toast.error('Please select a site');
      return;
    }
    
    if (editingItem.orderedQuantity <= 0) {
      toast.error('Quantity must be greater than 0');
      return;
    }
    
    if (editingItem.unitPrice < 0) {
      toast.error('Unit price cannot be negative');
      return;
    }

    if (isAddingNewRow) {
      setItems([...items, editingItem]);
      toast.success('Item added');
    } else if (editingRowIndex !== null) {
      const updatedItems = [...items];
      updatedItems[editingRowIndex] = editingItem;
      setItems(updatedItems);
      toast.success('Item updated');
    }

    // Reset editing state
    setIsAddingNewRow(false);
    setEditingRowIndex(null);
    setEditingItem(null);
    setAvailableUOMs([]);
  };

  // Cancel inline edit
  const handleCancelInlineEdit = () => {
    setIsAddingNewRow(false);
    setEditingRowIndex(null);
    setEditingItem(null);
    setAvailableUOMs([]);
  };

  // Delete item
  const handleDeleteItem = (index: number) => {
    if (confirm('Are you sure you want to remove this item?')) {
      const updatedItems = items.filter((_, i) => i !== index);
      setItems(updatedItems);
      toast.success('Item removed');
    }
  };

  // Save as draft
  const handleSaveDraft = async () => {
    if (!selectedSupplierId) {
      toast.error('Please select a supplier');
      return;
    }
    if (items.length === 0) {
      toast.error('Please add at least one item');
      return;
    }
    if (costAllocationMethod === 'GLExpense' && !expenseGLAccount.trim()) {
      toast.error('Please enter a GL expense account for GL expense allocation');
      return;
    }
    if (orderType === 'Consignment' && (!deliveryWarehouseId || deliveryWarehouseId === '__none__')) {
      toast.error('Please select a consignment warehouse for a consignment purchase order');
      return;
    }

    try {
      setSaving(true);
      
      const userStr = localStorage.getItem('user');
      const user = userStr ? JSON.parse(userStr) : null;
      const requestedById = user?.id || user?.userId;
      
      if (!requestedById) {
        toast.error('User not authenticated');
        return;
      }

      const normalizedDeliveryWarehouseId =
        deliveryWarehouseId && deliveryWarehouseId !== '__none__' ? deliveryWarehouseId : undefined;

      const createData: CreatePurchaseOrderDto = {
        supplierId: selectedSupplierId,
        orderType,
        requiredDate: requiredDate || undefined,
        promisedDate: promisedDate || undefined,
        paymentTerms: paymentTerms || undefined,
        shippingTerms: shippingTerms || undefined,
        terms: terms || undefined,
        notes: notes || undefined,
        deliveryWarehouseId: normalizedDeliveryWarehouseId,
        deliveryAddress: deliveryAddress || undefined,
        deliveryInstructions: deliveryInstructions || undefined,
        referenceNumber: referenceNumber || undefined,
        taxAmount: taxAmount || undefined,
        shippingCost: shippingCost || undefined,
        miscellaneousCost: miscellaneousCost || undefined,
        costAllocationMethod,
        costApportionmentBasis: costAllocationMethod === 'SpreadToItemCost' ? costApportionmentBasis : undefined,
        expenseGLAccount: costAllocationMethod === 'GLExpense' ? expenseGLAccount || undefined : undefined,
        discountAmount: discountAmount || undefined,
        requestedById,
        items: items.map(item => ({
          inventoryItemId: item.inventoryItemId,
          supplierItemCode: item.supplierItemCode || undefined,
          itemDescription: item.itemDescription || undefined,
          orderedQuantity: item.orderedQuantity,
          unitOfMeasure: item.unitOfMeasure || 'EA',
          warehouseId:
            orderType === 'Consignment' ? normalizedDeliveryWarehouseId : item.warehouseId || undefined,
          unitPrice: item.unitPrice,
          priceListLineId: item.priceListLineId || undefined,
          expectedDeliveryDate: item.expectedDeliveryDate || undefined,
          notes: item.notes || undefined
        }))
      };

      const result = await purchasingService.createPurchaseOrder(createData);
      toast.success('Purchase order saved as draft');
      router.push(`/procurement/purchase-orders/${result.id}`);
    } catch (error: any) {
      console.error('Error saving purchase order:', error);
      toast.error(error.message || 'Failed to save purchase order');
    } finally {
      setSaving(false);
    }
  };

  // Submit for approval
  const handleSubmit = async () => {
    if (!selectedSupplierId) {
      toast.error('Please select a supplier');
      return;
    }
    if (items.length === 0) {
      toast.error('Please add at least one item');
      return;
    }
    if (costAllocationMethod === 'GLExpense' && !expenseGLAccount.trim()) {
      toast.error('Please enter a GL expense account for GL expense allocation');
      return;
    }
    if (orderType === 'Consignment' && (!deliveryWarehouseId || deliveryWarehouseId === '__none__')) {
      toast.error('Please select a consignment warehouse for a consignment purchase order');
      return;
    }

    try {
      setSubmitting(true);
      
      const userStr = localStorage.getItem('user');
      const user = userStr ? JSON.parse(userStr) : null;
      const requestedById = user?.id || user?.userId;
      
      if (!requestedById) {
        toast.error('User not authenticated');
        return;
      }

      const normalizedDeliveryWarehouseId =
        deliveryWarehouseId && deliveryWarehouseId !== '__none__' ? deliveryWarehouseId : undefined;

      const createData: CreatePurchaseOrderDto = {
        supplierId: selectedSupplierId,
        orderType,
        requiredDate: requiredDate || undefined,
        promisedDate: promisedDate || undefined,
        paymentTerms: paymentTerms || undefined,
        shippingTerms: shippingTerms || undefined,
        terms: terms || undefined,
        notes: notes || undefined,
        deliveryWarehouseId: normalizedDeliveryWarehouseId,
        deliveryAddress: deliveryAddress || undefined,
        deliveryInstructions: deliveryInstructions || undefined,
        referenceNumber: referenceNumber || undefined,
        taxAmount: taxAmount || undefined,
        shippingCost: shippingCost || undefined,
        miscellaneousCost: miscellaneousCost || undefined,
        costAllocationMethod,
        costApportionmentBasis: costAllocationMethod === 'SpreadToItemCost' ? costApportionmentBasis : undefined,
        expenseGLAccount: costAllocationMethod === 'GLExpense' ? expenseGLAccount || undefined : undefined,
        discountAmount: discountAmount || undefined,
        requestedById,
        items: items.map(item => ({
          inventoryItemId: item.inventoryItemId,
          supplierItemCode: item.supplierItemCode || undefined,
          itemDescription: item.itemDescription || undefined,
          orderedQuantity: item.orderedQuantity,
          unitOfMeasure: item.unitOfMeasure || 'EA',
          warehouseId:
            orderType === 'Consignment' ? normalizedDeliveryWarehouseId : item.warehouseId || undefined,
          unitPrice: item.unitPrice,
          priceListLineId: item.priceListLineId || undefined,
          expectedDeliveryDate: item.expectedDeliveryDate || undefined,
          notes: item.notes || undefined
        }))
      };

      const result = await purchasingService.createPurchaseOrder(createData);
      
      // Submit for approval
      await purchasingService.submitPurchaseOrder(result.id);
      
      toast.success('Purchase order submitted for approval');
      router.push(`/procurement/purchase-orders/${result.id}`);
    } catch (error: any) {
      console.error('Error submitting purchase order:', error);
      toast.error(error.message || 'Failed to submit purchase order');
    } finally {
      setSubmitting(false);
    }
  };

  if (loadingRequisition) {
    return (
      <div className="flex items-center justify-center h-96">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        <p className="ml-3 text-muted-foreground">Loading requisition data...</p>
      </div>
    );
  }

  return (
    <div className="fixed inset-0 z-50 bg-background/80 p-4 backdrop-blur-sm sm:p-6">
      <div className="mx-auto h-full w-full max-w-[96vw] 2xl:max-w-[1800px] overflow-y-auto rounded-xl border bg-background p-6 shadow-2xl">
        <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="outline" onClick={() => router.push('/procurement/purchase-orders')}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold">New Purchase Order</h1>
            <p className="text-muted-foreground mt-1">
              {fromRequisitionId && sourceRequisition 
                ? `Creating from Requisition ${sourceRequisition.requisitionNumber}` 
                : 'Create a new purchase order'}
            </p>
          </div>
        </div>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/procurement">Procurement</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/procurement/purchase-orders">Purchase Orders</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>New</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Source Requisition Info */}
      {sourceRequisition && (
        <Card className="border-blue-200 bg-blue-50">
          <CardContent className="pt-6">
            <div className="flex items-start gap-3">
              <FileText className="h-5 w-5 text-blue-600 mt-0.5" />
              <div>
                <p className="font-medium text-blue-900">Creating from Purchase Requisition</p>
                <p className="text-sm text-blue-700 mt-1">
                  {sourceRequisition.requisitionNumber} - {sourceRequisition.itemCount} items - 
                  ${sourceRequisition.totalAmount.toLocaleString('en-US', { minimumFractionDigits: 2 })}
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Supplier Selection */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Building2 className="h-5 w-5" />
            Supplier Selection
          </CardTitle>
          <CardDescription>Select the supplier for this purchase order</CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          <div className="space-y-2">
            <Label htmlFor="supplier">Supplier *</Label>
            <Select value={selectedSupplierId} onValueChange={handleSupplierChange}>
              <SelectTrigger>
                <SelectValue placeholder="Select a supplier" />
              </SelectTrigger>
              <SelectContent>
                {suppliers.map(supplier => (
                  <SelectItem key={supplier.id} value={supplier.id}>
                    {supplier.partnerCode} - {supplier.partnerName}
                    {supplier.isBlacklisted && ' (BLACKLISTED)'}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          
          {/* Supplier Details */}
          {selectedSupplier && (
            <div className="border rounded-lg p-4 bg-muted/50">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <Label className="text-muted-foreground">Supplier Code</Label>
                  <p className="font-medium mt-1">{selectedSupplier.partnerCode}</p>
                </div>
                <div>
                  <Label className="text-muted-foreground">Contact</Label>
                  <p className="font-medium mt-1">{selectedSupplier.email || selectedSupplier.phone || 'N/A'}</p>
                </div>
                {selectedSupplier.paymentTerms && (
                  <div>
                    <Label className="text-muted-foreground">Payment Terms</Label>
                    <p className="font-medium mt-1">{selectedSupplier.paymentTerms}</p>
                  </div>
                )}
                {selectedSupplier.isBlacklisted && (
                  <div className="md:col-span-2">
                    <Badge className="bg-red-100 text-red-800">
                      <AlertTriangle className="h-3 w-3 mr-1" />
                      BLACKLISTED - {selectedSupplier.blacklistReason || 'No reason provided'}
                    </Badge>
                  </div>
                )}
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Order Details */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <FileText className="h-5 w-5" />
            Order Details
          </CardTitle>
          <CardDescription>Enter the purchase order details</CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div className="space-y-2">
              <Label htmlFor="orderDate">Order Date</Label>
              <Input
                id="orderDate"
                type="text"
                value={format(new Date(), 'MMM dd, yyyy')}
                disabled
                className="bg-muted"
              />
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="requiredDate" className="flex items-center gap-2">
                <Calendar className="h-4 w-4" />
                Required Date
              </Label>
              <Input
                id="requiredDate"
                type="date"
                value={requiredDate}
                onChange={(e) => setRequiredDate(e.target.value)}
              />
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="promisedDate">Promised Date</Label>
              <Input
                id="promisedDate"
                type="date"
                value={promisedDate}
                onChange={(e) => setPromisedDate(e.target.value)}
              />
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="referenceNumber">Reference Number</Label>
              <Input
                id="referenceNumber"
                value={referenceNumber}
                onChange={(e) => setReferenceNumber(e.target.value)}
                placeholder="Internal reference"
              />
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="paymentTerms">Payment Terms</Label>
              <Input
                id="paymentTerms"
                value={paymentTerms}
                onChange={(e) => setPaymentTerms(e.target.value)}
                placeholder="e.g., Net 30"
              />
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="shippingTerms">Shipping Terms</Label>
              <Input
                id="shippingTerms"
                value={shippingTerms}
                onChange={(e) => setShippingTerms(e.target.value)}
                placeholder="e.g., FOB, CIF"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="orderType">Order Type</Label>
              <Select value={orderType} onValueChange={(v) => setOrderType(v as 'Standard' | 'Consignment')}>
                <SelectTrigger>
                  <SelectValue placeholder="Select order type" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Standard">Standard</SelectItem>
                  <SelectItem value="Consignment">Consignment</SelectItem>
                </SelectContent>
              </Select>
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="deliveryWarehouse" className="flex items-center gap-2">
                <TruckIcon className="h-4 w-4" />
                Delivery Warehouse
              </Label>
              <Select value={deliveryWarehouseId} onValueChange={setDeliveryWarehouseId}>
                <SelectTrigger>
                  <SelectValue placeholder="Select warehouse" />
                </SelectTrigger>
                <SelectContent>
                  {orderType !== 'Consignment' && <SelectItem value="__none__">None</SelectItem>}
                  {(orderType === 'Consignment'
                    ? warehouses.filter(w => w.isConsignmentWarehouse)
                    : warehouses
                  ).map(wh => (
                    <SelectItem key={wh.id} value={wh.id}>
                      {wh.code} - {wh.name}
                      {wh.isConsignmentWarehouse ? ' (Consignment)' : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {orderType === 'Consignment' && (
                <p className="text-xs text-muted-foreground">
                  Consignment POs must be received into a warehouse marked as consignment.
                </p>
              )}
            </div>
          </div>
          
          <Separator />
          
          {/* Delivery and Additional Info - 2x2 Grid */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div className="space-y-2">
              <Label htmlFor="deliveryAddress">Delivery Address</Label>
              <Textarea
                id="deliveryAddress"
                value={deliveryAddress}
                onChange={(e) => setDeliveryAddress(e.target.value)}
                placeholder="Enter delivery address..."
                rows={3}
              />
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="deliveryInstructions">Delivery Instructions</Label>
              <Textarea
                id="deliveryInstructions"
                value={deliveryInstructions}
                onChange={(e) => setDeliveryInstructions(e.target.value)}
                placeholder="Special delivery instructions..."
                rows={3}
              />
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="terms">Terms & Conditions</Label>
              <Textarea
                id="terms"
                value={terms}
                onChange={(e) => setTerms(e.target.value)}
                placeholder="Terms and conditions..."
                rows={3}
              />
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="notes">Notes</Label>
              <Textarea
                id="notes"
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                placeholder="Additional notes..."
                rows={3}
              />
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Items Section with Inline Editing */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle className="flex items-center gap-2">
                <Package className="h-5 w-5" />
                Order Items
              </CardTitle>
              <CardDescription>Add items to this purchase order</CardDescription>
            </div>
            <Button 
              onClick={handleAddNewRow} 
              disabled={!selectedSupplierId || isAddingNewRow || editingRowIndex !== null}
            >
              <Plus className="h-4 w-4 mr-2" />
              Add Item
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          {!selectedSupplierId ? (
            <div className="text-center py-12 border-2 border-dashed rounded-lg">
              <Building2 className="h-12 w-12 mx-auto text-muted-foreground mb-4" />
              <p className="text-muted-foreground mb-4">Please select a supplier first</p>
            </div>
          ) : (
            <div className="space-y-4">
              <div className="border rounded-lg overflow-x-auto">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-[50px]">#</TableHead>
                      <TableHead className="min-w-[250px]">Item *</TableHead>
                      <TableHead className="min-w-[120px]">Supplier Code</TableHead>
                      <TableHead className="min-w-[200px]">Description</TableHead>
                      <TableHead className="min-w-[150px]">Site *</TableHead>
                      <TableHead className="min-w-[100px]">Qty *</TableHead>
                      <TableHead className="min-w-[100px]">UOM *</TableHead>
                      <TableHead className="min-w-[120px]">Unit Cost *</TableHead>
                      <TableHead className="min-w-[120px]">Line Total</TableHead>
                      <TableHead className="min-w-[130px]">Alloc. Shipping</TableHead>
                      <TableHead className="min-w-[130px]">Landed Unit</TableHead>
                      <TableHead className="min-w-[120px]">Delivery Date</TableHead>
                      <TableHead className="w-[120px]">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {/* Existing Items */}
                    {items.map((item, index) => (
                      <TableRow key={item.tempId}>
                        {editingRowIndex === index ? (
                          // Editing Mode
                          <>
                            <TableCell className="font-medium">{index + 1}</TableCell>
                            <TableCell>
                              <Select
                                value={editingItem?.inventoryItemId || '__none__'}
                                onValueChange={handleInlineInventoryItemSelect}
                              >
                                <SelectTrigger className="w-full">
                                  <SelectValue placeholder="Select item" />
                                </SelectTrigger>
                                <SelectContent>
                                  {inventoryItems.map(invItem => (
                                    <SelectItem key={invItem.id} value={invItem.id}>
                                      {invItem.itemCode} - {invItem.name}
                                    </SelectItem>
                                  ))}
                                </SelectContent>
                              </Select>
                            </TableCell>
                            <TableCell>
                              <Input
                                value={editingItem?.supplierItemCode || ''}
                                onChange={(e) => setEditingItem(prev => prev ? { ...prev, supplierItemCode: e.target.value } : null)}
                                placeholder="Supplier code"
                              />
                            </TableCell>
                            <TableCell>
                              <Input
                                value={editingItem?.itemDescription || ''}
                                onChange={(e) => setEditingItem(prev => prev ? { ...prev, itemDescription: e.target.value } : null)}
                                placeholder="Description"
                              />
                            </TableCell>
                            <TableCell>
                              <Select
                                value={editingItem?.warehouseId || '__none__'}
                                onValueChange={(value) => {
                                  if (value === '__none__') return;
                                  const warehouse = warehouses.find(w => w.id === value);
                                  setEditingItem(prev => prev ? {
                                    ...prev,
                                    warehouseId: value,
                                    warehouseName: warehouse?.name
                                  } : null);
                                }}
                              >
                                <SelectTrigger className="w-full">
                                  <SelectValue placeholder="Select site" />
                                </SelectTrigger>
                                <SelectContent>
                                  {warehouses.map(wh => (
                                    <SelectItem key={wh.id} value={wh.id}>
                                      {wh.code} - {wh.name}
                                    </SelectItem>
                                  ))}
                                </SelectContent>
                              </Select>
                            </TableCell>
                            <TableCell>
                              <Input
                                type="number"
                                min="0.01"
                                step="0.01"
                                value={editingItem?.orderedQuantity || 0}
                                onChange={(e) => setEditingItem(prev => prev ? { ...prev, orderedQuantity: parseFloat(e.target.value) || 0 } : null)}
                              />
                            </TableCell>
                            <TableCell>
                              {availableUOMs.length > 0 ? (
                                <Select
                                  value={editingItem?.unitOfMeasure || '__none__'}
                                  onValueChange={(value) => {
                                    if (value === '__none__') return;
                                    const uom = availableUOMs.find(u => u.unitCode === value);
                                    if (uom) {
                                      const isRealUOM = uom.unitOfMeasureId && uom.unitOfMeasureId !== '00000000-0000-0000-0000-000000000000';
                                      setEditingItem(prev => prev ? {
                                        ...prev,
                                        unitOfMeasure: uom.unitCode,
                                        itemUnitOfMeasureId: isRealUOM ? uom.id : undefined
                                      } : null);
                                    }
                                  }}
                                >
                                  <SelectTrigger className="w-full">
                                    <SelectValue placeholder="UOM" />
                                  </SelectTrigger>
                                  <SelectContent>
                                    {availableUOMs.map(uom => (
                                      <SelectItem key={uom.unitCode} value={uom.unitCode}>
                                        {uom.unitCode}
                                      </SelectItem>
                                    ))}
                                  </SelectContent>
                                </Select>
                              ) : (
                                <Input
                                  value={editingItem?.unitOfMeasure || ''}
                                  onChange={(e) => setEditingItem(prev => prev ? { ...prev, unitOfMeasure: e.target.value } : null)}
                                  placeholder="UOM"
                                />
                              )}
                            </TableCell>
                            <TableCell>
                              <Input
                                type="number"
                                min="0"
                                step="0.01"
                                value={editingItem?.unitPrice || 0}
                                onChange={(e) => setEditingItem(prev => prev ? { ...prev, unitPrice: parseFloat(e.target.value) || 0 } : null)}
                              />
                            </TableCell>
                            <TableCell className="font-medium">
                              ${calculateLineTotal(editingItem?.orderedQuantity || 0, editingItem?.unitPrice || 0).toLocaleString('en-US', { minimumFractionDigits: 2 })}
                            </TableCell>
                            <TableCell className="font-medium">
                              {costAllocationMethod === 'SpreadToItemCost' && totalAdditionalCost > 0 && allocationPreview[editingItem?.tempId || '']
                                ? `$${allocationPreview[editingItem?.tempId || ''].allocated.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
                                : '-'}
                            </TableCell>
                            <TableCell className="font-medium">
                              {costAllocationMethod === 'SpreadToItemCost' && totalAdditionalCost > 0 && allocationPreview[editingItem?.tempId || '']
                                ? `$${allocationPreview[editingItem?.tempId || ''].landedUnit.toLocaleString('en-US', { minimumFractionDigits: 4, maximumFractionDigits: 4 })}`
                                : '-'}
                            </TableCell>
                            <TableCell>
                              <Input
                                type="date"
                                value={editingItem?.expectedDeliveryDate || ''}
                                onChange={(e) => setEditingItem(prev => prev ? { ...prev, expectedDeliveryDate: e.target.value } : null)}
                              />
                            </TableCell>
                            <TableCell>
                              <div className="flex items-center gap-1">
                                <Button
                                  size="sm"
                                  variant="ghost"
                                  className="text-green-600 hover:text-green-700"
                                  onClick={handleSaveInlineEdit}
                                >
                                  <Check className="h-4 w-4" />
                                </Button>
                                <Button
                                  size="sm"
                                  variant="ghost"
                                  className="text-red-600 hover:text-red-700"
                                  onClick={handleCancelInlineEdit}
                                >
                                  <X className="h-4 w-4" />
                                </Button>
                              </div>
                            </TableCell>
                          </>
                        ) : (
                          // View Mode
                          <>
                            <TableCell className="font-medium">{index + 1}</TableCell>
                            <TableCell>
                              <div className="font-medium">{item.itemCode || 'N/A'}</div>
                              <div className="text-sm text-muted-foreground">{item.itemName}</div>
                            </TableCell>
                            <TableCell>{item.supplierItemCode || '-'}</TableCell>
                            <TableCell className="max-w-[200px] truncate" title={item.itemDescription}>
                              {item.itemDescription}
                            </TableCell>
                            <TableCell>{item.warehouseName || '-'}</TableCell>
                            <TableCell>{item.orderedQuantity}</TableCell>
                            <TableCell>{item.unitOfMeasure || 'EA'}</TableCell>
                            <TableCell>
                              ${item.unitPrice.toLocaleString('en-US', { minimumFractionDigits: 2 })}
                            </TableCell>
                            <TableCell className="font-medium">
                              ${calculateLineTotal(item.orderedQuantity, item.unitPrice).toLocaleString('en-US', { minimumFractionDigits: 2 })}
                            </TableCell>
                            <TableCell className="font-medium">
                              {costAllocationMethod === 'SpreadToItemCost' && totalAdditionalCost > 0 && allocationPreview[item.tempId]
                                ? `$${allocationPreview[item.tempId].allocated.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
                                : '-'}
                            </TableCell>
                            <TableCell className="font-medium">
                              {costAllocationMethod === 'SpreadToItemCost' && totalAdditionalCost > 0 && allocationPreview[item.tempId]
                                ? `$${allocationPreview[item.tempId].landedUnit.toLocaleString('en-US', { minimumFractionDigits: 4, maximumFractionDigits: 4 })}`
                                : '-'}
                            </TableCell>
                            <TableCell>
                              {item.expectedDeliveryDate ? format(new Date(item.expectedDeliveryDate), 'MMM dd, yyyy') : '-'}
                            </TableCell>
                            <TableCell>
                              <div className="flex items-center gap-1">
                                <Button
                                  size="sm"
                                  variant="ghost"
                                  onClick={() => handleEditRow(index)}
                                  disabled={isAddingNewRow || editingRowIndex !== null}
                                >
                                  <Edit className="h-4 w-4" />
                                </Button>
                                <Button
                                  size="sm"
                                  variant="ghost"
                                  className="text-red-600 hover:text-red-700"
                                  onClick={() => handleDeleteItem(index)}
                                  disabled={isAddingNewRow || editingRowIndex !== null}
                                >
                                  <Trash2 className="h-4 w-4" />
                                </Button>
                              </div>
                            </TableCell>
                          </>
                        )}
                      </TableRow>
                    ))}
                    
                    {/* New Row Being Added */}
                    {isAddingNewRow && editingItem && (
                      <TableRow className="bg-blue-50">
                        <TableCell className="font-medium">{items.length + 1}</TableCell>
                        <TableCell>
                          <Select
                            value={editingItem.inventoryItemId || '__none__'}
                            onValueChange={handleInlineInventoryItemSelect}
                          >
                            <SelectTrigger className="w-full">
                              <SelectValue placeholder="Select item" />
                            </SelectTrigger>
                            <SelectContent>
                              {inventoryItems.map(invItem => (
                                <SelectItem key={invItem.id} value={invItem.id}>
                                  {invItem.itemCode} - {invItem.name}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </TableCell>
                        <TableCell>
                          <Input
                            value={editingItem.supplierItemCode || ''}
                            onChange={(e) => setEditingItem(prev => prev ? { ...prev, supplierItemCode: e.target.value } : null)}
                            placeholder="Supplier code"
                          />
                        </TableCell>
                        <TableCell>
                          <Input
                            value={editingItem.itemDescription || ''}
                            onChange={(e) => setEditingItem(prev => prev ? { ...prev, itemDescription: e.target.value } : null)}
                            placeholder="Description"
                          />
                        </TableCell>
                        <TableCell>
                          <Select
                            value={editingItem.warehouseId || '__none__'}
                            onValueChange={(value) => {
                              if (value === '__none__') return;
                              const warehouse = warehouses.find(w => w.id === value);
                              setEditingItem(prev => prev ? {
                                ...prev,
                                warehouseId: value,
                                warehouseName: warehouse?.name
                              } : null);
                            }}
                          >
                            <SelectTrigger className="w-full">
                              <SelectValue placeholder="Select site" />
                            </SelectTrigger>
                            <SelectContent>
                              {warehouses.map(wh => (
                                <SelectItem key={wh.id} value={wh.id}>
                                  {wh.code} - {wh.name}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </TableCell>
                        <TableCell>
                          <Input
                            type="number"
                            min="0.01"
                            step="0.01"
                            value={editingItem.orderedQuantity || 0}
                            onChange={(e) => setEditingItem(prev => prev ? { ...prev, orderedQuantity: parseFloat(e.target.value) || 0 } : null)}
                          />
                        </TableCell>
                        <TableCell>
                          {availableUOMs.length > 0 ? (
                            <Select
                              value={editingItem.unitOfMeasure || '__none__'}
                              onValueChange={(value) => {
                                if (value === '__none__') return;
                                const uom = availableUOMs.find(u => u.unitCode === value);
                                if (uom) {
                                  const isRealUOM = uom.unitOfMeasureId && uom.unitOfMeasureId !== '00000000-0000-0000-0000-000000000000';
                                  setEditingItem(prev => prev ? {
                                    ...prev,
                                    unitOfMeasure: uom.unitCode,
                                    itemUnitOfMeasureId: isRealUOM ? uom.id : undefined
                                  } : null);
                                }
                              }}
                            >
                              <SelectTrigger className="w-full">
                                <SelectValue placeholder="UOM" />
                              </SelectTrigger>
                              <SelectContent>
                                {availableUOMs.map(uom => (
                                  <SelectItem key={uom.unitCode} value={uom.unitCode}>
                                    {uom.unitCode}
                                  </SelectItem>
                                ))}
                              </SelectContent>
                            </Select>
                          ) : (
                            <Input
                              value={editingItem.unitOfMeasure || ''}
                              onChange={(e) => setEditingItem(prev => prev ? { ...prev, unitOfMeasure: e.target.value } : null)}
                              placeholder="UOM"
                            />
                          )}
                        </TableCell>
                        <TableCell>
                          <Input
                            type="number"
                            min="0"
                            step="0.01"
                            value={editingItem.unitPrice || 0}
                            onChange={(e) => setEditingItem(prev => prev ? { ...prev, unitPrice: parseFloat(e.target.value) || 0 } : null)}
                          />
                        </TableCell>
                        <TableCell className="font-medium">
                          ${calculateLineTotal(editingItem.orderedQuantity || 0, editingItem.unitPrice || 0).toLocaleString('en-US', { minimumFractionDigits: 2 })}
                        </TableCell>
                        <TableCell className="font-medium">
                          {costAllocationMethod === 'SpreadToItemCost' && totalAdditionalCost > 0 && allocationPreview[editingItem.tempId]
                            ? `$${allocationPreview[editingItem.tempId].allocated.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
                            : '-'}
                        </TableCell>
                        <TableCell className="font-medium">
                          {costAllocationMethod === 'SpreadToItemCost' && totalAdditionalCost > 0 && allocationPreview[editingItem.tempId]
                            ? `$${allocationPreview[editingItem.tempId].landedUnit.toLocaleString('en-US', { minimumFractionDigits: 4, maximumFractionDigits: 4 })}`
                            : '-'}
                        </TableCell>
                        <TableCell>
                          <Input
                            type="date"
                            value={editingItem.expectedDeliveryDate || ''}
                            onChange={(e) => setEditingItem(prev => prev ? { ...prev, expectedDeliveryDate: e.target.value } : null)}
                          />
                        </TableCell>
                        <TableCell>
                          <div className="flex items-center gap-1">
                            <Button
                              size="sm"
                              variant="ghost"
                              className="text-green-600 hover:text-green-700"
                              onClick={handleSaveInlineEdit}
                            >
                              <Check className="h-4 w-4" />
                            </Button>
                            <Button
                              size="sm"
                              variant="ghost"
                              className="text-red-600 hover:text-red-700"
                              onClick={handleCancelInlineEdit}
                            >
                              <X className="h-4 w-4" />
                            </Button>
                          </div>
                        </TableCell>
                      </TableRow>
                    )}
                    
                    {/* Empty State */}
                    {items.length === 0 && !isAddingNewRow && (
                      <TableRow>
                        <TableCell colSpan={13} className="text-center py-12">
                          <Package className="h-12 w-12 mx-auto text-muted-foreground mb-4" />
                          <p className="text-muted-foreground mb-4">No items added yet</p>
                          <Button onClick={handleAddNewRow}>
                            <Plus className="h-4 w-4 mr-2" />
                            Add First Item
                          </Button>
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
              </div>
              <p className="text-xs text-muted-foreground">
                Landed Unit formula: Unit Cost + (Allocated Additional Cost / Ordered Qty). Allocated Additional Cost is each line&apos;s share of (Shipping + Misc. Cost) based on the selected spread basis.
              </p>
              
              {/* Financial Summary */}
              {items.length > 0 && (
                <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
                  <Card className="w-full lg:max-w-xl">
                    <CardHeader className="pb-3">
                      <CardTitle className="text-base">Allocation & Additional Costs</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-3">
                      <div className="flex justify-between text-sm items-center gap-4">
                        <span className="text-muted-foreground">Shipping:</span>
                        <Input
                          type="number"
                          min="0"
                          step="0.01"
                          value={shippingCost}
                          onChange={(e) => setShippingCost(parseFloat(e.target.value) || 0)}
                          className="w-32 h-8 text-right"
                        />
                      </div>

                      <div className="flex justify-between text-sm items-center gap-4">
                        <span className="text-muted-foreground">Misc. Cost:</span>
                        <Input
                          type="number"
                          min="0"
                          step="0.01"
                          value={miscellaneousCost}
                          onChange={(e) => setMiscellaneousCost(parseFloat(e.target.value) || 0)}
                          className="w-32 h-8 text-right"
                        />
                      </div>

                      <div className="flex justify-between text-sm items-center gap-4">
                        <span className="text-muted-foreground">Allocation:</span>
                        <Select
                          value={costAllocationMethod}
                          onValueChange={(value: 'SpreadToItemCost' | 'GLExpense') => setCostAllocationMethod(value)}
                        >
                          <SelectTrigger className="w-44 h-8">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="SpreadToItemCost">Spread to item cost</SelectItem>
                            <SelectItem value="GLExpense">Post to GL expense</SelectItem>
                          </SelectContent>
                        </Select>
                      </div>

                      {costAllocationMethod === 'SpreadToItemCost' && (
                        <div className="flex justify-between text-sm items-center gap-4">
                          <span className="text-muted-foreground">Spread Basis:</span>
                          <Select
                            value={costApportionmentBasis}
                            onValueChange={(value: 'Value' | 'Weight' | 'Quantity') => setCostApportionmentBasis(value)}
                          >
                            <SelectTrigger className="w-44 h-8">
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              <SelectItem value="Value">By value</SelectItem>
                              <SelectItem value="Weight">By weight</SelectItem>
                              <SelectItem value="Quantity">By quantity</SelectItem>
                            </SelectContent>
                          </Select>
                        </div>
                      )}

                      {costAllocationMethod === 'GLExpense' && (
                        <div className="flex justify-between text-sm items-center gap-4">
                          <span className="text-muted-foreground">GL Account:</span>
                          <Input
                            value={expenseGLAccount}
                            onChange={(e) => setExpenseGLAccount(e.target.value)}
                            placeholder="Expense account"
                            className="w-44 h-8 text-right"
                          />
                        </div>
                      )}

                      <div className="flex justify-between text-sm">
                        <span className="text-muted-foreground">Total Additional:</span>
                        <span className="font-medium">
                          ${totalAdditionalCost.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                        </span>
                      </div>

                      {costAllocationMethod === 'SpreadToItemCost' && totalAdditionalCost > 0 && (
                        <p className="text-xs text-muted-foreground">
                          Additional costs will be apportioned by {costApportionmentBasis.toLowerCase()} (basis total: {apportionmentBasisTotal.toFixed(2)}).
                        </p>
                      )}
                    </CardContent>
                  </Card>

                  <Card className="w-full lg:ml-auto lg:max-w-md">
                    <CardHeader className="pb-3">
                      <CardTitle className="text-base">Financial Summary</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-3">
                      <div className="flex justify-between text-sm">
                        <span className="text-muted-foreground">Subtotal:</span>
                        <span className="font-medium">
                          ${subTotal.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                        </span>
                      </div>

                      <div className="flex justify-between text-sm items-center gap-4">
                        <span className="text-muted-foreground">Tax:</span>
                        <Input
                          type="number"
                          min="0"
                          step="0.01"
                          value={taxAmount}
                          onChange={(e) => setTaxAmount(parseFloat(e.target.value) || 0)}
                          className="w-32 h-8 text-right"
                        />
                      </div>

                      <div className="flex justify-between text-sm items-center gap-4">
                        <span className="text-muted-foreground">Discount:</span>
                        <Input
                          type="number"
                          min="0"
                          step="0.01"
                          value={discountAmount}
                          onChange={(e) => setDiscountAmount(parseFloat(e.target.value) || 0)}
                          className="w-32 h-8 text-right"
                        />
                      </div>

                      <Separator />

                      <div className="flex justify-between">
                        <span className="font-semibold flex items-center gap-2">
                          <DollarSign className="h-4 w-4" />
                          Total Amount:
                        </span>
                        <span className="text-xl font-bold text-primary">
                          ${totalAmount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                        </span>
                      </div>
                    </CardContent>
                  </Card>
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Action Buttons */}
      <div className="flex justify-end gap-4">
        <Button variant="outline" onClick={() => router.push('/procurement/purchase-orders')}>
          Cancel
        </Button>
        <Button
          variant="outline"
          onClick={handleSaveDraft}
          disabled={saving || submitting || !selectedSupplierId || items.length === 0 || isAddingNewRow || editingRowIndex !== null}
        >
          {saving ? (
            <>
              <Loader2 className="w-4 h-4 mr-2 animate-spin" />
              Saving...
            </>
          ) : (
            <>
              <Save className="w-4 h-4 mr-2" />
              Save as Draft
            </>
          )}
        </Button>
        <Button
          onClick={handleSubmit}
          disabled={saving || submitting || !selectedSupplierId || items.length === 0 || isAddingNewRow || editingRowIndex !== null}
        >
          {submitting ? (
            <>
              <Loader2 className="w-4 h-4 mr-2 animate-spin" />
              Submitting...
            </>
          ) : (
            <>
              <Send className="w-4 h-4 mr-2" />
              Submit for Approval
            </>
          )}
        </Button>
      </div>

      {/* Validation Warnings */}
      {!selectedSupplierId && (
        <Card className="border-yellow-200 bg-yellow-50">
          <CardContent className="pt-6">
            <div className="flex items-start gap-3">
              <AlertCircle className="h-5 w-5 text-yellow-600 mt-0.5" />
              <div>
                <p className="font-medium text-yellow-900">Supplier not selected</p>
                <p className="text-sm text-yellow-700">
                  Please select a supplier before adding items.
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}
      
      {selectedSupplierId && items.length === 0 && !isAddingNewRow && (
        <Card className="border-yellow-200 bg-yellow-50">
          <CardContent className="pt-6">
            <div className="flex items-start gap-3">
              <AlertCircle className="h-5 w-5 text-yellow-600 mt-0.5" />
              <div>
                <p className="font-medium text-yellow-900">No items added</p>
                <p className="text-sm text-yellow-700">
                  You must add at least one item before saving or submitting this purchase order.
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}
      
      {selectedSupplier?.isBlacklisted && (
        <Card className="border-red-200 bg-red-50">
          <CardContent className="pt-6">
            <div className="flex items-start gap-3">
              <AlertTriangle className="h-5 w-5 text-red-600 mt-0.5" />
              <div>
                <p className="font-medium text-red-900">Blacklisted Supplier Warning</p>
                <p className="text-sm text-red-700">
                  This supplier is blacklisted: {selectedSupplier.blacklistReason || 'No reason provided'}
                </p>
                <p className="text-sm text-red-700 mt-1">
                  Please verify approval before proceeding with this purchase order.
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}
        </div>
      </div>
    </div>
  );
}

export default function NewPurchaseOrderPage() {
  return (
    <Suspense fallback={<div className="flex items-center justify-center h-96"><Loader2 className="h-8 w-8 animate-spin" /></div>}>
      <NewPurchaseOrderPageContent />
    </Suspense>
  );
}
