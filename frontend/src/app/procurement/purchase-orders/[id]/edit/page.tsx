'use client';

import React, { useState, useEffect, useMemo } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Separator } from '@/components/ui/separator';
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
  CreditCard,
  MapPin
} from 'lucide-react';
import { toast } from 'sonner';
import {
  purchasingService,
  PurchaseOrderDetailDto,
  CreatePurchaseOrderDto,
  CreatePurchaseOrderItemDto,
  LandedCostAllocationMethod,
  ProcurementPurchaseOrderSourceType
} from '@/services/purchasingService';
import { inventoryManagementService, InventoryItemDto, WarehouseDto, ItemUnitOfMeasureDto, WarehouseItemDto } from '@/services/inventoryManagementService';
import { businessPartnerService, BusinessPartnerDto } from '@/services/businessPartnerService';
import pricingService from '@/services/pricingService';
import { formatProcurementMoney, normalizeProcurementCurrency } from '@/lib/procurement-currency';
import { format } from 'date-fns';

interface POItemFormData extends CreatePurchaseOrderItemDto {
  tempId: string;
  itemCode?: string;
  itemName?: string;
  itemUnitOfMeasureId?: string;
  warehouseId?: string;
  warehouseName?: string;
}

interface POLandedCostPlanLineFormData {
  tempId: string;
  costType: number;
  description: string;
  amount: number;
  currency: string;
  exchangeRate: number;
  allocationMethod: LandedCostAllocationMethod;
  supplierId?: string;
  referenceNumber?: string;
}

const LANDED_COST_TYPES: Array<{ value: number; label: string }> = [
  { value: 1, label: 'Freight / Shipping' },
  { value: 2, label: 'Customs Duty' },
  { value: 3, label: 'Insurance' },
  { value: 4, label: 'Handling' },
  { value: 5, label: 'Brokerage' },
  { value: 6, label: 'Storage / Warehousing' },
  { value: 7, label: 'Other' },
];

const LANDED_COST_METHODS: Array<{ value: LandedCostAllocationMethod; label: string }> = [
  { value: 'ByValue', label: 'By value' },
  { value: 'ByQuantity', label: 'By qty' },
  { value: 'ByWeight', label: 'By weight' },
  { value: 'ByVolume', label: 'By volume' },
  { value: 'Equal', label: 'Equal' },
  { value: 'Manual', label: 'Manual (amounts later on GRN)' },
];

const getLandedCostTypeLabel = (costType: number) =>
  LANDED_COST_TYPES.find(t => t.value === costType)?.label || 'Other';

export default function EditPurchaseOrderPage() {
  const router = useRouter();
  const params = useParams();
  const id = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';
  
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  
  // Form data
  const [orderNumber, setOrderNumber] = useState('');
  const [orderDate, setOrderDate] = useState('');
  const [supplierId, setSupplierId] = useState('');
  const [supplierName, setSupplierName] = useState('');
  const [sourceType, setSourceType] =
    useState<ProcurementPurchaseOrderSourceType>('HistoricalMigration');
  const [sourceId, setSourceId] = useState('');
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
  const [currency, setCurrency] = useState('GHS');
  const [items, setItems] = useState<POItemFormData[]>([]);
  
  // Financial fields
  const [taxAmount, setTaxAmount] = useState(0);
  const [shippingCost, setShippingCost] = useState(0);
  const [miscellaneousCost, setMiscellaneousCost] = useState(0);
  const [costAllocationMethod, setCostAllocationMethod] = useState<'SpreadToItemCost' | 'GLExpense'>('SpreadToItemCost');
  const [costApportionmentBasis, setCostApportionmentBasis] = useState<'Value' | 'Weight' | 'Quantity'>('Value');
  const [expenseGLAccount, setExpenseGLAccount] = useState('');
  const [discountAmount, setDiscountAmount] = useState(0);

  // Planned landed cost plan (captured at PO stage, carried to GRN LC voucher)
  const [landedCostPlanCurrency, setLandedCostPlanCurrency] = useState('');
  const [landedCostPlanNotes, setLandedCostPlanNotes] = useState('');
  const [landedCostPlanItems, setLandedCostPlanItems] = useState<POLandedCostPlanLineFormData[]>([]);
  
  // Reference data
  const [inventoryItems, setInventoryItems] = useState<InventoryItemDto[]>([]);
  const [suppliers, setSuppliers] = useState<BusinessPartnerDto[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [warehouseItemsByWarehouseId, setWarehouseItemsByWarehouseId] = useState<Record<string, WarehouseItemDto[]>>({});
  const [warehouseItemsByInventoryItemId, setWarehouseItemsByInventoryItemId] = useState<Record<string, WarehouseItemDto[]>>({});
  const [loadingData, setLoadingData] = useState(true);
  
  // Inline editing state
  const [editingRowIndex, setEditingRowIndex] = useState<number | null>(null);
  const [isAddingNewRow, setIsAddingNewRow] = useState(false);
  const [editingItem, setEditingItem] = useState<POItemFormData | null>(null);
  const [availableUOMs, setAvailableUOMs] = useState<ItemUnitOfMeasureDto[]>([]);
  const [loadingUOMs, setLoadingUOMs] = useState(false);
  const [loadingPrice, setLoadingPrice] = useState(false);

  const normalizeSelectedWarehouseId = (warehouseId?: string) =>
    warehouseId && warehouseId !== '__none__' ? warehouseId : '';

  const getEffectiveWarehouseIdForLine = (lineWarehouseId?: string) => {
    if (orderType === 'Consignment') {
      return normalizeSelectedWarehouseId(deliveryWarehouseId);
    }
    return normalizeSelectedWarehouseId(lineWarehouseId);
  };

  const ensureWarehouseItemsLoaded = async (warehouseId: string) => {
    const normalized = normalizeSelectedWarehouseId(warehouseId);
    if (!normalized || warehouseItemsByWarehouseId[normalized]) {
      return;
    }

    const items = await inventoryManagementService.getWarehouseItems(normalized);
    setWarehouseItemsByWarehouseId(prev => ({ ...prev, [normalized]: items || [] }));
  };

  const ensureWarehouseItemsByInventoryItemLoaded = async (inventoryItemId: string) => {
    if (!inventoryItemId || warehouseItemsByInventoryItemId[inventoryItemId]) {
      return;
    }

    const items = await inventoryManagementService.getWarehouseItemsByInventoryItem(inventoryItemId);
    setWarehouseItemsByInventoryItemId(prev => ({ ...prev, [inventoryItemId]: items || [] }));
  };

  // Load existing purchase order
  useEffect(() => {
    const loadPurchaseOrder = async () => {
      try {
        setLoading(true);
        const po = await purchasingService.getPurchaseOrderById(id);
        
        // Check if can edit
        if (po.status !== 'Draft') {
          toast.error('Only draft purchase orders can be edited');
          router.push(`/procurement/purchase-orders/${id}`);
          return;
        }
        
        // Load form data
        setOrderNumber(po.orderNumber);
        setOrderDate(po.orderDate);
        setSupplierId(po.supplierId);
        setSupplierName(po.supplierName);
        setSourceType(po.procurementSourceType || 'HistoricalMigration');
        setSourceId(po.procurementSourceId || '');
        setRequiredDate(po.requiredDate ? po.requiredDate.split('T')[0] : '');
        setPromisedDate(po.promisedDate ? po.promisedDate.split('T')[0] : '');
        setPaymentTerms(po.paymentTerms || '');
        setShippingTerms(po.shippingTerms || '');
        setOrderType(po.orderType === 'Consignment' ? 'Consignment' : 'Standard');
        setTerms(po.terms || '');
        setNotes(po.notes || '');
        setDeliveryWarehouseId(po.deliveryWarehouseId || '__none__');
        setDeliveryAddress(po.deliveryAddress || '');
        setDeliveryInstructions(po.deliveryInstructions || '');
        setReferenceNumber(po.referenceNumber || '');
        setCurrency(normalizeProcurementCurrency(po.currency));
        setLandedCostPlanCurrency(normalizeProcurementCurrency(po.currency));
        
        // Load financial fields
        setTaxAmount(po.taxAmount || 0);
        setShippingCost(po.shippingCost || 0);
        setMiscellaneousCost(po.miscellaneousCost || 0);
        setCostAllocationMethod(po.costAllocationMethod || 'SpreadToItemCost');
        setCostApportionmentBasis(po.costApportionmentBasis || 'Value');
        setExpenseGLAccount(po.expenseGLAccount || '');
        setDiscountAmount(po.discountAmount || 0);
        
        // Load items
        const loadedItems: POItemFormData[] = po.items.map((item, index) => ({
          tempId: `existing-${index}`,
          inventoryItemId: item.inventoryItemId,
          itemCode: item.itemCode,
          itemName: item.itemName,
          supplierItemCode: item.supplierItemCode || '',
          itemDescription: item.itemDescription || '',
          orderedQuantity: item.orderedQuantity,
          unitOfMeasure: item.unitOfMeasure || 'EA',
          itemUnitOfMeasureId: item.itemUnitOfMeasureId,
          warehouseId: item.warehouseId,
          warehouseName: item.warehouseName,
          unitPrice: item.unitPrice,
          expectedDeliveryDate: item.expectedDeliveryDate ? item.expectedDeliveryDate.split('T')[0] : '',
          notes: item.notes || ''
        }));
        
        setItems(loadedItems);

        try {
          const plan = await purchasingService.getPurchaseOrderLandedCostPlan(id);
          if (plan) {
            setLandedCostPlanCurrency(plan.currency || po.currency);
            setLandedCostPlanNotes(plan.notes || '');
            setLandedCostPlanItems(
              (plan.items || []).map((i) => ({
                tempId: i.id || crypto.randomUUID(),
                costType: i.costType,
                description: i.description || '',
                amount: i.amount || 0,
                currency: i.currency || plan.currency || po.currency,
                exchangeRate: i.exchangeRate || 1,
                allocationMethod: (i.allocationMethod as LandedCostAllocationMethod) || 'ByValue',
                supplierId: i.supplierId,
                referenceNumber: i.referenceNumber || ''
              }))
            );
          } else {
            setLandedCostPlanItems([]);
          }
        } catch {
          // ignore plan load errors; user can still edit PO
        }
      } catch (error: any) {
        console.error('Error loading purchase order:', error);
        toast.error('Failed to load purchase order');
        router.push('/procurement/purchase-orders');
      } finally {
        setLoading(false);
      }
    };
    
    loadPurchaseOrder();
  }, [id]);

  // Load reference data
  useEffect(() => {
    const loadData = async () => {
      try {
        setLoadingData(true);
        const [itemsData, suppliersData, warehousesData] = await Promise.all([
          inventoryManagementService.getInventoryItems({ isActive: true }),
          businessPartnerService.getActivePartners(),
          inventoryManagementService.getWarehouses(true)
        ]);
        
        setInventoryItems(itemsData || []);
        setSuppliers((suppliersData || []).filter(bp =>
          bp.partnerType === 'Supplier' || bp.partnerType === 'Both'
        ));
        setWarehouses(warehousesData || []);
      } catch (error) {
        console.error('Error loading reference data:', error);
        toast.error('Failed to load reference data');
      } finally {
        setLoadingData(false);
      }
    };

    loadData();
  }, []);

  // Calculate totals
  const calculateLineTotal = (quantity: number, unitPrice: number) => {
    return quantity * unitPrice;
  };

  const subTotal = items.reduce((sum, item) =>
    sum + calculateLineTotal(item.orderedQuantity, item.unitPrice), 0
  );
  
  const totalAdditionalCost = shippingCost + miscellaneousCost;
  const totalAmount = subTotal + taxAmount + totalAdditionalCost - discountAmount;
  const documentCurrency = normalizeProcurementCurrency(currency);
  const showLegacyAllocationAndAdditionalCosts = totalAdditionalCost > 0 || costAllocationMethod === 'GLExpense';

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

  const filteredInventoryItemsForEditing = useMemo(() => {
    if (!editingItem) return inventoryItems;

    const effectiveWarehouseId = getEffectiveWarehouseIdForLine(editingItem.warehouseId);
    if (!effectiveWarehouseId) return inventoryItems;

    const allowed = warehouseItemsByWarehouseId[effectiveWarehouseId];
    if (!allowed) return inventoryItems;

    const allowedIds = new Set(allowed.map(a => a.inventoryItemId));
    return inventoryItems.filter(i => allowedIds.has(i.id));
  }, [editingItem, inventoryItems, warehouseItemsByWarehouseId, orderType, deliveryWarehouseId]);

  const filteredWarehousesForEditing = useMemo(() => {
    if (!editingItem?.inventoryItemId) return warehouses;
    const linked = warehouseItemsByInventoryItemId[editingItem.inventoryItemId];
    if (!linked) return warehouses;
    const allowedIds = new Set(linked.map(l => l.warehouseId));
    return warehouses.filter(w => allowedIds.has(w.id));
  }, [editingItem?.inventoryItemId, warehouses, warehouseItemsByInventoryItemId]);

  // Handle inventory item selection in inline editing
  const handleInlineInventoryItemSelect = async (itemId: string) => {
    if (!editingItem) return;
    
    const item = inventoryItems.find(i => i.id === itemId);
    if (!item) return;

    try {
      setLoadingUOMs(true);
      setLoadingPrice(true);

      const effectiveWarehouseId = getEffectiveWarehouseIdForLine(editingItem.warehouseId);
      if (effectiveWarehouseId) {
        await ensureWarehouseItemsLoaded(effectiveWarehouseId);
        const allowedItems = warehouseItemsByWarehouseId[effectiveWarehouseId] || [];
        if (!allowedItems.some(wi => wi.inventoryItemId === itemId)) {
          toast.error('This item is not assigned to the selected warehouse');
          return;
        }
      }

      await ensureWarehouseItemsByInventoryItemLoaded(itemId);
      
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
      if (supplierId) {
        try {
          const price = await pricingService.getSupplierItemPrice(
            supplierId,
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

      // If no warehouse selected yet, and the item is assigned to exactly one warehouse, auto-select it.
      if (orderType !== 'Consignment' && !normalizeSelectedWarehouseId(editingItem.warehouseId)) {
        const linked = warehouseItemsByInventoryItemId[itemId] || [];
        const uniqueWarehouseIds = Array.from(new Set(linked.map(l => l.warehouseId))).filter(Boolean);
        if (uniqueWarehouseIds.length === 1) {
          const wh = warehouses.find(w => w.id === uniqueWarehouseIds[0]);
          if (wh) {
            setEditingItem(prev => prev ? { ...prev, warehouseId: wh.id, warehouseName: wh.name } : null);
          }
        }
      }
      
    } catch (error) {
      console.error('Error loading item details:', error);
      toast.error('Failed to load item details');
    } finally {
      setLoadingUOMs(false);
      setLoadingPrice(false);
    }
  };

  const handleInlineWarehouseSelect = async (warehouseId: string) => {
    if (!editingItem) return;

    const normalized = normalizeSelectedWarehouseId(warehouseId);
    if (!normalized) return;

    try {
      await ensureWarehouseItemsLoaded(normalized);
      const warehouse = warehouses.find(w => w.id === normalized);

      if (editingItem.inventoryItemId) {
        const allowedItems = warehouseItemsByWarehouseId[normalized] || [];
        if (!allowedItems.some(wi => wi.inventoryItemId === editingItem.inventoryItemId)) {
          toast.error('Selected item is not assigned to this warehouse');
          setEditingItem(prev => prev ? {
            ...prev,
            warehouseId: normalized,
            warehouseName: warehouse?.name,
            inventoryItemId: '',
            itemCode: '',
            itemName: '',
            itemDescription: '',
            itemUnitOfMeasureId: undefined,
            unitOfMeasure: 'EA',
            unitPrice: 0
          } : null);
          setAvailableUOMs([]);
          return;
        }
      }

      setEditingItem(prev => prev ? { ...prev, warehouseId: normalized, warehouseName: warehouse?.name } : null);
    } catch (e) {
      console.error('Error loading warehouse items:', e);
      toast.error('Failed to load warehouse items');
    }
  };

  // Start adding new row
  const handleAddNewRow = () => {
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
        const effectiveWarehouseId = getEffectiveWarehouseIdForLine(item.warehouseId);
        if (effectiveWarehouseId) {
          await ensureWarehouseItemsLoaded(effectiveWarehouseId);
        }
        await ensureWarehouseItemsByInventoryItemLoaded(item.inventoryItemId);

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
    if (!editingItem.inventoryItemId) {
      toast.error('Please select an inventory item');
      return;
    }
    
    if (!editingItem.warehouseId) {
      toast.error('Please select a warehouse');
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

  const addLandedCostPlanLine = () => {
    setLandedCostPlanItems(prev => ([
      ...prev,
      {
        tempId: crypto.randomUUID(),
        costType: 1,
        description: getLandedCostTypeLabel(1),
        amount: 0,
        currency: landedCostPlanCurrency || documentCurrency,
        exchangeRate: 1,
        allocationMethod: 'ByValue',
        supplierId: undefined,
        referenceNumber: ''
      }
    ]));
  };

  const updateLandedCostPlanLine = (tempId: string, patch: Partial<POLandedCostPlanLineFormData>) => {
    setLandedCostPlanItems(prev => prev.map(l => (l.tempId === tempId ? { ...l, ...patch } : l)));
  };

  const removeLandedCostPlanLine = (tempId: string) => {
    setLandedCostPlanItems(prev => prev.filter(l => l.tempId !== tempId));
  };

  const upsertLandedCostPlanIfAny = async (purchaseOrderId: string) => {
    if (landedCostPlanItems.length === 0) return;

    const normalizedItems = landedCostPlanItems
      .map(i => ({
        ...i,
        description: ((i.description || '').trim() || getLandedCostTypeLabel(i.costType)).trim(),
        referenceNumber: (i.referenceNumber || '').trim(),
        currency: (i.currency || landedCostPlanCurrency || documentCurrency).trim().toUpperCase(),
        exchangeRate: Number.isFinite(i.exchangeRate) && i.exchangeRate > 0 ? i.exchangeRate : 1,
        amount: Number.isFinite(i.amount) ? i.amount : 0
      }));

    const invalidLines = normalizedItems.filter(i => i.amount <= 0);
    if (invalidLines.length > 0)
      throw new Error('Please enter an Amount for every planned landed cost line (or delete the empty lines).');

    await purchasingService.upsertPurchaseOrderLandedCostPlan(purchaseOrderId, {
      currency: (landedCostPlanCurrency || '').trim().toUpperCase() || undefined,
      notes: (landedCostPlanNotes || '').trim() || undefined,
      items: normalizedItems.map(i => ({
        costType: i.costType,
        description: i.description,
        amount: i.amount,
        currency: i.currency,
        exchangeRate: i.exchangeRate,
        allocationMethod: i.allocationMethod,
        supplierId: i.supplierId || undefined,
        referenceNumber: i.referenceNumber || undefined
      }))
    });
  };

  // Save changes
  const handleSave = async () => {
    if (!supplierId) {
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

      // Filter out items with empty/invalid inventory item IDs
      const validItems = items.filter(item => {
        const id = item.inventoryItemId;
        // Check if it's a valid non-empty GUID
        return id && id.length > 0 && id !== '00000000-0000-0000-0000-000000000000';
      });
      
      if (validItems.length === 0) {
        toast.error('No valid items to save. Please ensure all items have a valid inventory item selected.');
        return;
      }
      
      const normalizedDeliveryWarehouseId =
        deliveryWarehouseId && deliveryWarehouseId !== '__none__' ? deliveryWarehouseId : undefined;

      const updateData: CreatePurchaseOrderDto = {
        sourceType,
        sourceId,
        supplierId,
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
        items: validItems.map(item => ({
          inventoryItemId: item.inventoryItemId,
          supplierItemCode: item.supplierItemCode || undefined,
          itemDescription: item.itemDescription || undefined,
          orderedQuantity: item.orderedQuantity,
          unitOfMeasure: item.unitOfMeasure || 'EA',
          warehouseId:
            orderType === 'Consignment' ? normalizedDeliveryWarehouseId : item.warehouseId || undefined,
          unitPrice: item.unitPrice,
          expectedDeliveryDate: item.expectedDeliveryDate || undefined,
          notes: item.notes || undefined
        }))
      };

      // Use the update endpoint for existing purchase orders
      await purchasingService.updatePurchaseOrder(id, updateData);
      let plannedLandedCostsSaved = true;
      try {
        await upsertLandedCostPlanIfAny(id);
      } catch (e: any) {
        console.error('Failed to save planned landed costs:', e);
        plannedLandedCostsSaved = false;
        toast.error(e?.message || 'Failed to save planned landed costs');
      }
      
      if (!plannedLandedCostsSaved) {
        toast.error('Purchase order updated, but planned landed costs were not saved. Fix the errors and click Save Changes again.');
        return;
      }

      toast.success('Purchase order updated');
      router.push(`/procurement/purchase-orders/${id}`);
    } catch (error: any) {
      console.error('Error updating purchase order:', error);
      toast.error(error.message || 'Failed to update purchase order');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-96">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        <p className="ml-3 text-muted-foreground">Loading purchase order...</p>
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
          <Button variant="outline" onClick={() => router.push(`/procurement/purchase-orders/${id}`)}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold">Edit Purchase Order</h1>
            <p className="text-muted-foreground mt-1">{orderNumber}</p>
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
            <BreadcrumbLink href={`/procurement/purchase-orders/${id}`}>{orderNumber}</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Edit</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Basic Information */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <FileText className="h-5 w-5" />
            Order Information
          </CardTitle>
          <CardDescription>Update the basic details for this purchase order</CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div className="space-y-2">
              <Label htmlFor="orderDate">Order Date</Label>
              <Input
                id="orderDate"
                type="text"
                value={orderDate ? format(new Date(orderDate), 'MMM dd, yyyy') : ''}
                disabled
                className="bg-muted"
              />
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="supplier" className="flex items-center gap-2">
                <Building2 className="h-4 w-4" />
                Supplier *
              </Label>
              <Input
                id="supplier"
                type="text"
                value={supplierName}
                disabled
                className="bg-muted"
              />
              <p className="text-xs text-muted-foreground">Supplier cannot be changed after creation</p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="currency">PO Currency</Label>
              <Input id="currency" value={documentCurrency} disabled className="bg-muted" />
              <p className="text-xs text-muted-foreground">Inherited from the approved procurement source.</p>
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
              <Label htmlFor="paymentTerms" className="flex items-center gap-2">
                <CreditCard className="h-4 w-4" />
                Payment Terms
              </Label>
              <Input
                id="paymentTerms"
                value={paymentTerms}
                onChange={(e) => setPaymentTerms(e.target.value)}
                placeholder="e.g., Net 30, Net 60"
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
            
            <div className="space-y-2">
              <Label htmlFor="referenceNumber">Reference Number</Label>
              <Input
                id="referenceNumber"
                value={referenceNumber}
                onChange={(e) => setReferenceNumber(e.target.value)}
                placeholder="Internal reference"
              />
            </div>
          </div>
          
          <Separator />
          
          {/* Delivery and Additional Info - 2x2 Grid */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div className="space-y-2">
              <Label htmlFor="deliveryAddress" className="flex items-center gap-2">
                <MapPin className="h-4 w-4" />
                Delivery Address
              </Label>
              <Textarea
                id="deliveryAddress"
                value={deliveryAddress}
                onChange={(e) => setDeliveryAddress(e.target.value)}
                placeholder="Enter delivery address..."
                rows={3}
              />
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="deliveryInstructions" className="flex items-center gap-2">
                <TruckIcon className="h-4 w-4" />
                Delivery Instructions
              </Label>
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
              <Label htmlFor="notes">Additional Notes</Label>
              <Textarea
                id="notes"
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                placeholder="Any additional information..."
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
              <CardDescription>Manage items in this purchase order</CardDescription>
            </div>
            <Button 
              onClick={handleAddNewRow}
              disabled={isAddingNewRow || editingRowIndex !== null}
            >
              <Plus className="h-4 w-4 mr-2" />
              Add Item
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            <div className="border rounded-lg overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-[50px]">#</TableHead>
                    <TableHead className="min-w-[250px]">Item *</TableHead>
                    <TableHead className="min-w-[200px]">Description</TableHead>
                    <TableHead className="min-w-[150px]">Warehouse *</TableHead>
                    <TableHead className="min-w-[100px]">Qty *</TableHead>
                    <TableHead className="min-w-[100px]">UOM *</TableHead>
                    <TableHead className="min-w-[120px]">Unit Cost *</TableHead>
                    <TableHead className="min-w-[120px]">Line Total</TableHead>
                    {showLegacyAllocationAndAdditionalCosts && (
                      <TableHead className="min-w-[130px]">Alloc. Shipping</TableHead>
                    )}
                    {showLegacyAllocationAndAdditionalCosts && (
                      <TableHead className="min-w-[130px]">Landed Unit</TableHead>
                    )}
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
                                {filteredInventoryItemsForEditing.map(invItem => (
                                  <SelectItem key={invItem.id} value={invItem.id}>
                                    {invItem.itemCode} - {invItem.name}
                                  </SelectItem>
                                ))}
                              </SelectContent>
                            </Select>
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
                                handleInlineWarehouseSelect(value);
                              }}
                            >
                              <SelectTrigger className="w-full">
                                <SelectValue placeholder="Select warehouse" />
                              </SelectTrigger>
                              <SelectContent>
                                {filteredWarehousesForEditing.map(wh => (
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
                            {formatProcurementMoney(calculateLineTotal(editingItem?.orderedQuantity || 0, editingItem?.unitPrice || 0), documentCurrency)}
                          </TableCell>
                          {showLegacyAllocationAndAdditionalCosts && (
                            <TableCell className="font-medium">
                              {costAllocationMethod === 'SpreadToItemCost' && totalAdditionalCost > 0 && allocationPreview[editingItem?.tempId || '']
                                ? formatProcurementMoney(allocationPreview[editingItem?.tempId || ''].allocated, documentCurrency)
                                : '-'}
                            </TableCell>
                          )}
                          {showLegacyAllocationAndAdditionalCosts && (
                            <TableCell className="font-medium">
                              {costAllocationMethod === 'SpreadToItemCost' && totalAdditionalCost > 0 && allocationPreview[editingItem?.tempId || '']
                                ? formatProcurementMoney(allocationPreview[editingItem?.tempId || ''].landedUnit, documentCurrency, 4)
                                : '-'}
                            </TableCell>
                          )}
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
                          <TableCell className="max-w-[200px] truncate" title={item.itemDescription}>
                            {item.itemDescription}
                          </TableCell>
                          <TableCell>{item.warehouseName || '-'}</TableCell>
                          <TableCell>{item.orderedQuantity}</TableCell>
                          <TableCell>{item.unitOfMeasure || 'EA'}</TableCell>
                          <TableCell>
                            {formatProcurementMoney(item.unitPrice, documentCurrency)}
                          </TableCell>
                          <TableCell className="font-medium">
                            {formatProcurementMoney(calculateLineTotal(item.orderedQuantity, item.unitPrice), documentCurrency)}
                          </TableCell>
                          {showLegacyAllocationAndAdditionalCosts && (
                            <TableCell className="font-medium">
                              {costAllocationMethod === 'SpreadToItemCost' && totalAdditionalCost > 0 && allocationPreview[item.tempId]
                                ? formatProcurementMoney(allocationPreview[item.tempId].allocated, documentCurrency)
                                : '-'}
                            </TableCell>
                          )}
                          {showLegacyAllocationAndAdditionalCosts && (
                            <TableCell className="font-medium">
                              {costAllocationMethod === 'SpreadToItemCost' && totalAdditionalCost > 0 && allocationPreview[item.tempId]
                                ? formatProcurementMoney(allocationPreview[item.tempId].landedUnit, documentCurrency, 4)
                                : '-'}
                            </TableCell>
                          )}
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
                              {filteredInventoryItemsForEditing.map(invItem => (
                                <SelectItem key={invItem.id} value={invItem.id}>
                                  {invItem.itemCode} - {invItem.name}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
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
                              handleInlineWarehouseSelect(value);
                            }}
                          >
                            <SelectTrigger className="w-full">
                              <SelectValue placeholder="Select warehouse" />
                            </SelectTrigger>
                            <SelectContent>
                              {filteredWarehousesForEditing.map(wh => (
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
                        {formatProcurementMoney(calculateLineTotal(editingItem.orderedQuantity || 0, editingItem.unitPrice || 0), documentCurrency)}
                      </TableCell>
                      {showLegacyAllocationAndAdditionalCosts && (
                        <TableCell className="font-medium">
                          {costAllocationMethod === 'SpreadToItemCost' && totalAdditionalCost > 0 && allocationPreview[editingItem.tempId]
                            ? formatProcurementMoney(allocationPreview[editingItem.tempId].allocated, documentCurrency)
                            : '-'}
                        </TableCell>
                      )}
                      {showLegacyAllocationAndAdditionalCosts && (
                        <TableCell className="font-medium">
                          {costAllocationMethod === 'SpreadToItemCost' && totalAdditionalCost > 0 && allocationPreview[editingItem.tempId]
                            ? formatProcurementMoney(allocationPreview[editingItem.tempId].landedUnit, documentCurrency, 4)
                            : '-'}
                        </TableCell>
                      )}
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
                      <TableCell colSpan={showLegacyAllocationAndAdditionalCosts ? 13 : 11} className="text-center py-12">
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
            {showLegacyAllocationAndAdditionalCosts && (
              <p className="text-xs text-muted-foreground">
                Landed Unit formula: Unit Cost + (Allocated Additional Cost / Ordered Qty). Allocated Additional Cost is each line&apos;s share of (Shipping + Misc. Cost) based on the selected spread basis.
              </p>
            )}
            
            {/* Financial Summary */}
            {items.length > 0 && (
              <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
                {showLegacyAllocationAndAdditionalCosts && (
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
                          {formatProcurementMoney(totalAdditionalCost, documentCurrency)}
                        </span>
                      </div>

                      {costAllocationMethod === 'SpreadToItemCost' && totalAdditionalCost > 0 && (
                        <p className="text-xs text-muted-foreground">
                          Additional costs will be apportioned by {costApportionmentBasis.toLowerCase()} (basis total: {apportionmentBasisTotal.toFixed(2)}).
                        </p>
                      )}
                    </CardContent>
                  </Card>
                )}

                <Card
                  className={`w-full lg:ml-auto lg:max-w-md ${!showLegacyAllocationAndAdditionalCosts ? 'lg:col-start-2' : ''}`}
                >
                  <CardHeader className="pb-3">
                    <CardTitle className="text-base">Financial Summary</CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-3">
                    <div className="flex justify-between text-sm">
                      <span className="text-muted-foreground">Subtotal:</span>
                      <span className="font-medium">
                        {formatProcurementMoney(subTotal, documentCurrency)}
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
                        {formatProcurementMoney(totalAmount, documentCurrency)}
                      </span>
                    </div>
                  </CardContent>
                </Card>

                <Card className="w-full lg:col-span-2">
                  <CardHeader className="pb-3">
                    <CardTitle className="text-base">Planned Landed Costs (carried to GRN)</CardTitle>
                    <CardDescription className="text-xs">
                      Optional. Capture freight/duty/insurance/handling etc now, then copy into the GRN landed cost voucher when receiving.
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-3">
                    <div className="flex flex-wrap items-end gap-3">
                      <div className="space-y-1">
                        <Label className="text-xs">Plan Currency</Label>
                        <Input
                          value={landedCostPlanCurrency}
                          onChange={(e) => setLandedCostPlanCurrency(e.target.value)}
                          className="w-28 h-8 uppercase"
                          placeholder={documentCurrency}
                        />
                      </div>
                      <div className="flex-1 min-w-[240px] space-y-1">
                        <Label className="text-xs">Notes</Label>
                        <Input
                          value={landedCostPlanNotes}
                          onChange={(e) => setLandedCostPlanNotes(e.target.value)}
                          className="h-8"
                          placeholder="Optional notes"
                        />
                      </div>
                      <Button type="button" variant="outline" className="h-8" onClick={addLandedCostPlanLine}>
                        <Plus className="h-4 w-4 mr-2" />
                        Add line
                      </Button>
                    </div>

                    <div className="rounded-md border">
                      <Table>
                        <TableHeader>
                          <TableRow>
                            <TableHead className="min-w-[180px]">Type</TableHead>
                            <TableHead className="min-w-[220px]">Description</TableHead>
                            <TableHead className="min-w-[220px]">Service Supplier</TableHead>
                            <TableHead className="min-w-[150px]">Allocation</TableHead>
                            <TableHead className="min-w-[140px] text-right">Amount</TableHead>
                            <TableHead className="min-w-[90px]">Curr</TableHead>
                            <TableHead className="min-w-[110px] text-right">Rate</TableHead>
                            <TableHead className="min-w-[160px]">Ref</TableHead>
                            <TableHead className="w-[60px]"></TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {landedCostPlanItems.length === 0 ? (
                            <TableRow>
                              <TableCell colSpan={9} className="text-center py-6 text-sm text-muted-foreground">
                                No planned landed cost lines
                              </TableCell>
                            </TableRow>
                          ) : (
                            landedCostPlanItems.map((line) => (
                              <TableRow key={line.tempId}>
                                <TableCell>
                                  <Select
                                    value={String(line.costType)}
                                    onValueChange={(v) => {
                                      const nextCostType = parseInt(v, 10);
                                      const currentDescription = (line.description || '').trim();
                                      const previousAutoDescription = getLandedCostTypeLabel(line.costType);

                                      const shouldAutoUpdateDescription =
                                        !currentDescription || currentDescription === previousAutoDescription;

                                      updateLandedCostPlanLine(line.tempId, {
                                        costType: nextCostType,
                                        ...(shouldAutoUpdateDescription
                                          ? { description: getLandedCostTypeLabel(nextCostType) }
                                          : {})
                                      });
                                    }}
                                  >
                                    <SelectTrigger className="h-8">
                                      <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent>
                                      {LANDED_COST_TYPES.map(t => (
                                        <SelectItem key={t.value} value={String(t.value)}>{t.label}</SelectItem>
                                      ))}
                                    </SelectContent>
                                  </Select>
                                </TableCell>
                                <TableCell>
                                  <Input
                                    value={line.description}
                                    onChange={(e) => updateLandedCostPlanLine(line.tempId, { description: e.target.value })}
                                    className="h-8"
                                    placeholder="e.g. Freight invoice estimate"
                                  />
                                </TableCell>
                                <TableCell>
                                  <Select
                                    value={line.supplierId || '__none__'}
                                    onValueChange={(v) => updateLandedCostPlanLine(line.tempId, { supplierId: v === '__none__' ? undefined : v })}
                                  >
                                    <SelectTrigger className="h-8">
                                      <SelectValue placeholder="Select supplier" />
                                    </SelectTrigger>
                                    <SelectContent>
                                      <SelectItem value="__none__">None</SelectItem>
                                      {suppliers.map(s => (
                                        <SelectItem key={s.id} value={s.id}>{s.partnerName}</SelectItem>
                                      ))}
                                    </SelectContent>
                                  </Select>
                                </TableCell>
                                <TableCell>
                                  <Select
                                    value={line.allocationMethod}
                                    onValueChange={(v: LandedCostAllocationMethod) => updateLandedCostPlanLine(line.tempId, { allocationMethod: v })}
                                  >
                                    <SelectTrigger className="h-8">
                                      <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent>
                                      {LANDED_COST_METHODS.map(m => (
                                        <SelectItem key={m.value} value={m.value}>{m.label}</SelectItem>
                                      ))}
                                    </SelectContent>
                                  </Select>
                                </TableCell>
                                <TableCell className="text-right">
                                  <Input
                                    type="number"
                                    min="0.01"
                                    step="0.01"
                                    value={line.amount}
                                    onChange={(e) => updateLandedCostPlanLine(line.tempId, { amount: parseFloat(e.target.value) || 0 })}
                                    className="h-8 text-right"
                                  />
                                </TableCell>
                                <TableCell>
                                  <Input
                                    value={line.currency}
                                    onChange={(e) => updateLandedCostPlanLine(line.tempId, { currency: e.target.value })}
                                    className="h-8 uppercase"
                                    placeholder={landedCostPlanCurrency}
                                  />
                                </TableCell>
                                <TableCell className="text-right">
                                  <Input
                                    type="number"
                                    min="0"
                                    step="0.0001"
                                    value={line.exchangeRate}
                                    onChange={(e) => updateLandedCostPlanLine(line.tempId, { exchangeRate: parseFloat(e.target.value) || 1 })}
                                    className="h-8 text-right"
                                  />
                                </TableCell>
                                <TableCell>
                                  <Input
                                    value={line.referenceNumber || ''}
                                    onChange={(e) => updateLandedCostPlanLine(line.tempId, { referenceNumber: e.target.value })}
                                    className="h-8"
                                    placeholder="Invoice/Ref"
                                  />
                                </TableCell>
                                <TableCell>
                                  <Button
                                    type="button"
                                    size="sm"
                                    variant="ghost"
                                    className="text-red-600 hover:text-red-700"
                                    onClick={() => removeLandedCostPlanLine(line.tempId)}
                                  >
                                    <Trash2 className="h-4 w-4" />
                                    </Button>
                                  </TableCell>
                                </TableRow>
                              ))
                          )}
                        </TableBody>
                      </Table>
                    </div>

                    <div className="flex justify-between text-sm">
                      <span className="text-muted-foreground">Planned landed cost total ({(landedCostPlanCurrency || documentCurrency).toUpperCase()}):</span>
                      <span className="font-medium">
                        {formatProcurementMoney(
                          landedCostPlanItems.reduce((sum, i) => sum + ((i.amount || 0) * (i.exchangeRate || 1)), 0),
                          landedCostPlanCurrency || documentCurrency
                        )}
                      </span>
                    </div>
                  </CardContent>
                </Card>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Action Buttons */}
      <div className="flex justify-end gap-4">
        <Button variant="outline" onClick={() => router.push(`/procurement/purchase-orders/${id}`)}>
          Cancel
        </Button>
        <Button
          onClick={handleSave}
          disabled={saving || items.length === 0 || isAddingNewRow || editingRowIndex !== null}
        >
          {saving ? (
            <>
              <Loader2 className="w-4 h-4 mr-2 animate-spin" />
              Saving...
            </>
          ) : (
            <>
              <Save className="w-4 h-4 mr-2" />
              Save Changes
            </>
          )}
        </Button>
      </div>

      {/* Validation Warning */}
      {items.length === 0 && !isAddingNewRow && (
        <Card className="border-yellow-200 bg-yellow-50">
          <CardContent className="pt-6">
            <div className="flex items-start gap-3">
              <AlertCircle className="h-5 w-5 text-yellow-600 mt-0.5" />
              <div>
                <p className="font-medium text-yellow-900">No items added</p>
                <p className="text-sm text-yellow-700">
                  You must add at least one item before saving this purchase order.
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
