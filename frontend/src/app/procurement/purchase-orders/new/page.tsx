'use client';
import { PurchaseOrderLineActions, PurchaseOrderPlannedCosts } from '@/components/procurement/PurchaseOrderPlannedCosts';
import { buildPlannedCostPayload, PlannedCostLine } from '@/lib/purchase-order-landed-costs';

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
  PurchaseRequisitionDetailDto,
  LandedCostAllocationMethod,
  ProcurementPurchaseOrderSourceOptionDto,
  ProcurementPurchaseOrderSourceStatusDto
} from '@/services/purchasingService';
import { inventoryManagementService, InventoryItemDto, WarehouseDto, ItemUnitOfMeasureDto, WarehouseItemDto } from '@/services/inventoryManagementService';
import { businessPartnerService, BusinessPartnerDto, BusinessPartnerDetailDto } from '@/services/businessPartnerService';
import procurementSettingsService, { ProcurementSettingsDto } from '@/services/procurementSettingsService';
import pricingService, { PriceListLookupResult } from '@/services/pricingService';
import { procurementCurrencyService, type CurrencyListDto } from '@/services/financeCommonService';
import {
  formatProcurementMoney,
  getProcurementBaseCurrency,
  normalizeProcurementCurrency,
} from '@/lib/procurement-currency';
import { format } from 'date-fns';
import { getPurchaseOrderItemMappingError } from '@/lib/purchase-order-item-mapping';
import { PurchaseOrderLineTypeSelect } from '@/components/procurement/PurchaseOrderLineTypeSelect';
import { purchaseOrderLineType, requiresPurchaseOrderStock, type PurchaseOrderLineType } from '@/lib/purchase-order-line-types';
import {
  createApprovedPurchaseOrderItems,
  findApprovedPurchaseOrderLine,
  retainApprovedPurchaseOrderTerms,
  supportsApprovedPurchaseOrderUnit,
} from '@/lib/purchase-order-approved-lines';
import { ApprovedPurchaseOrderUnit } from '@/components/procurement/ApprovedPurchaseOrderUnit';

interface POItemFormData extends CreatePurchaseOrderItemDto {
  tempId: string;
  approvedSourceLineId?: string;
  itemCode?: string;
  itemName?: string;
  warehouseId?: string;
  warehouseName?: string;
}

type POLandedCostPlanLineFormData = PlannedCostLine;

function NewPurchaseOrderPageContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const fromRequisitionId = searchParams?.get('fromRequisition');
  
  const [saving, setSaving] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [loadingRequisition, setLoadingRequisition] = useState(false);
  const [loadingSources, setLoadingSources] = useState(true);
  const [sourceStatus, setSourceStatus] =
    useState<ProcurementPurchaseOrderSourceStatusDto | null>(null);
  const [selectedSourceKey, setSelectedSourceKey] = useState('');
  
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

  // Planned landed cost plan (captured at PO stage, carried to GRN LC voucher)
  const [landedCostPlanCurrency, setLandedCostPlanCurrency] = useState('');
  const [landedCostPlanNotes, setLandedCostPlanNotes] = useState('');
  const [landedCostPlanItems, setLandedCostPlanItems] = useState<POLandedCostPlanLineFormData[]>([]);
  const [costLineKey, setCostLineKey] = useState<string | null>(null);
  
  // Reference data
  const [inventoryItems, setInventoryItems] = useState<InventoryItemDto[]>([]);
  const [suppliers, setSuppliers] = useState<BusinessPartnerDto[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [warehouseItemsByWarehouseId, setWarehouseItemsByWarehouseId] = useState<Record<string, WarehouseItemDto[]>>({});
  const [warehouseItemsByInventoryItemId, setWarehouseItemsByInventoryItemId] = useState<Record<string, WarehouseItemDto[]>>({});
  const [loadingData, setLoadingData] = useState(true);
  const [currencies, setCurrencies] = useState<CurrencyListDto[]>([]);
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
    if (!normalized) return [];
    if (warehouseItemsByWarehouseId[normalized]) return warehouseItemsByWarehouseId[normalized];

    const items = await inventoryManagementService.getWarehouseItems(normalized);
    setWarehouseItemsByWarehouseId(prev => ({ ...prev, [normalized]: items || [] }));
    return items || [];
  };

  const ensureWarehouseItemsByInventoryItemLoaded = async (inventoryItemId: string) => {
    if (!inventoryItemId || warehouseItemsByInventoryItemId[inventoryItemId]) {
      return;
    }

    const items = await inventoryManagementService.getWarehouseItemsByInventoryItem(inventoryItemId);
    setWarehouseItemsByInventoryItemId(prev => ({ ...prev, [inventoryItemId]: items || [] }));
  };

  // Load reference data and settings
  useEffect(() => {
    const loadData = async () => {
      try {
        setLoadingData(true);
        const [itemsData, suppliersData, warehousesData, settingsData, currencyData] = await Promise.all([
          inventoryManagementService.getInventoryItems({ isActive: true }),
          businessPartnerService.getActivePartners(),
          inventoryManagementService.getWarehouses(true),
          procurementSettingsService.getSettings(),
          procurementCurrencyService.getActive()
        ]);
        
        setInventoryItems(itemsData || []);
        // Filter to only show Supplier or Both types
        setSuppliers((suppliersData || []).filter(bp =>
          bp.partnerType === 'Supplier' || bp.partnerType === 'Both'
        ));
        setWarehouses(warehousesData || []);
        setProcurementSettings(settingsData);
        const activeCurrencies = (currencyData || []).filter((currency) => currency.isActive);
        setCurrencies(activeCurrencies);
        setLandedCostPlanCurrency((current) => current || getProcurementBaseCurrency(activeCurrencies));
      } catch (error) {
        console.error('Error loading reference data:', error);
        toast.error('Failed to load reference data');
      } finally {
        setLoadingData(false);
      }
    };

    loadData();
  }, []);

  useEffect(() => {
    let cancelled = false;
    const loadSources = async () => {
      try {
        setLoadingSources(true);
        const status = await purchasingService.getPurchaseOrderSourceOptions(
          fromRequisitionId || undefined
        );
        if (!cancelled) setSourceStatus(status);
      } catch (error) {
        console.error('Error loading approved PO sources:', error);
        if (!cancelled) {
          setSourceStatus({
            ready: false,
            permission: 'procurement.purchase-order.create',
            candidateCount: 0,
            blockedReasons: [
              error instanceof Error
                ? error.message
                : 'Approved source options could not be loaded.'
            ],
            sources: [],
            frameworkCallOffRoute: '/procurement/framework-call-offs'
          });
        }
      } finally {
        if (!cancelled) setLoadingSources(false);
      }
    };
    loadSources();
    return () => {
      cancelled = true;
    };
  }, [fromRequisitionId]);

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

  const handleLineTypeChange = (lineType: PurchaseOrderLineType) => {
    setEditingItem(prev => prev ? { ...prev, lineType, inventoryItemId: '', itemCode: '',
      warehouseId: '', warehouseName: '', itemUnitOfMeasureId: undefined, priceListLineId: undefined } : null);
    setAvailableUOMs([]);
  };

  // Handle inventory item selection in inline editing
  const handleInlineInventoryItemSelect = async (itemId: string) => {
    if (!editingItem) return;
    
    if (itemId === '__none__') {
      setEditingItem(prev => prev ? { ...prev, inventoryItemId: '', itemCode: '', itemUnitOfMeasureId: undefined, priceListLineId: undefined } : null);
      setAvailableUOMs([]);
      return;
    }
    const item = inventoryItems.find(i => i.id === itemId);
    if (!item) return;
    const approvedLine = findApprovedPurchaseOrderLine(selectedSource?.approvedLines || [], editingItem);

    try {
      setLoadingUOMs(true);
      setLoadingPrice(true);

      const effectiveWarehouseId = getEffectiveWarehouseIdForLine(editingItem.warehouseId);
      if (effectiveWarehouseId) {
        const allowedItems = await ensureWarehouseItemsLoaded(effectiveWarehouseId);
        if (!allowedItems.some(wi => wi.inventoryItemId === itemId)) {
          toast.error('This item is not assigned to the selected warehouse');
          return;
        }
      }

      await ensureWarehouseItemsByInventoryItemLoaded(itemId);
      
      // Load available UOMs
      const uoms = await inventoryManagementService.getItemUnitsOfMeasure(itemId);
      if (approvedLine && !supportsApprovedPurchaseOrderUnit(approvedLine, item.unitOfMeasure, uoms)) {
        toast.error(`The selected stock item does not support the approved unit ${approvedLine.unitOfMeasure}. Select the matching stock item or have its units configured.`);
        return;
      }
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
      if (selectedSupplierId && !approvedLine) {
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
      
      setEditingItem(retainApprovedPurchaseOrderTerms({
        ...editingItem,
        inventoryItemId: item.id,
        lineType: purchaseOrderLineType(item.itemType as PurchaseOrderLineType),
        itemCode: item.itemCode,
        itemName: item.name,
        itemDescription: item.description || item.name,
        unitOfMeasure: finalUOM,
        itemUnitOfMeasureId: finalUOMId,
        unitPrice: finalPrice
      }, approvedLine, uoms));

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

  const selectedSource = useMemo<ProcurementPurchaseOrderSourceOptionDto | null>(
    () =>
      sourceStatus?.sources.find(
        source => `${source.sourceType}:${source.sourceId}` === selectedSourceKey
      ) ?? null,
    [selectedSourceKey, sourceStatus]
  );
  const editingApprovedLine = findApprovedPurchaseOrderLine(selectedSource?.approvedLines || [], editingItem);

  const documentCurrency = normalizeProcurementCurrency(
    selectedSource?.currencyCode,
    getProcurementBaseCurrency(currencies)
  );

  const handleSourceChange = (key: string) => {
    setSelectedSourceKey(key);
    const source = sourceStatus?.sources.find(
      item => `${item.sourceType}:${item.sourceId}` === key
    );
    if (!source) return;
    setLandedCostPlanCurrency(source.currencyCode);
    setSelectedSupplierId(source.businessPartnerId);
    loadSupplierDetails(source.businessPartnerId);
    if (
      source.sourceType === 'RfqAward' ||
      source.sourceType === 'TenderAward' ||
      source.sourceType === 'Contract' ||
      source.sourceType === 'ApprovedException'
    ) {
      const authoritativeLines: POItemFormData[] = createApprovedPurchaseOrderItems(
        source.approvedLines || [], requiredDate || '',
      );
      setItems(authoritativeLines);
      setEditingRowIndex(null);
      setIsAddingNewRow(false);
      setEditingItem(null);
      if (authoritativeLines.length > 0) {
        toast.success('Approved source lines loaded. Map each line to its stock item and warehouse.');
      } else {
        toast.error('The selected source has no authoritative commercial lines');
      }
    }
  };

  const handleInlineWarehouseSelect = async (warehouseId: string) => {
    if (!editingItem) return;

    const normalized = normalizeSelectedWarehouseId(warehouseId);
    if (!normalized) return;

    try {
      const allowedItems = await ensureWarehouseItemsLoaded(normalized);
      const warehouse = warehouses.find(w => w.id === normalized);

      if (editingItem.inventoryItemId) {
        if (!allowedItems.some(wi => wi.inventoryItemId === editingItem.inventoryItemId)) {
          toast.error('Selected item is not assigned to this warehouse');
          setEditingItem(prev => prev ? retainApprovedPurchaseOrderTerms({
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
          }, findApprovedPurchaseOrderLine(selectedSource?.approvedLines || [], prev)) : null);
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
    const approvedLine = findApprovedPurchaseOrderLine(selectedSource?.approvedLines || [], item);
    setEditingRowIndex(index);
    setIsAddingNewRow(false);
    setEditingItem(retainApprovedPurchaseOrderTerms({ ...item }, approvedLine));
    
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
        setEditingItem(current => current?.tempId === item.tempId
          ? retainApprovedPurchaseOrderTerms(current, approvedLine, uoms)
          : current);
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
    if (!editingItem.inventoryItemId && !editingItem.itemDescription) {
      toast.error('Please provide an item description');
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

    const savedItem = retainApprovedPurchaseOrderTerms(editingItem, editingApprovedLine, availableUOMs);
    const mappedInventoryItem = inventoryItems.find(item => item.id === savedItem.inventoryItemId);
    if (editingApprovedLine && mappedInventoryItem && !supportsApprovedPurchaseOrderUnit(
      editingApprovedLine, mappedInventoryItem.unitOfMeasure, availableUOMs,
    )) {
      toast.error(`The selected stock item does not support the approved unit ${editingApprovedLine.unitOfMeasure}. Select the matching stock item or have its units configured.`);
      return;
    }
    if (isAddingNewRow) {
      setItems([...items, savedItem]);
      toast.success('Item added');
    } else if (editingRowIndex !== null) {
      const updatedItems = [...items];
      updatedItems[editingRowIndex] = savedItem;
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
    const key = items[index].tempId;
    setLandedCostPlanItems(prev => prev.filter(c => c.purchaseOrderLineKey !== key));
    setItems(prev => prev.filter((_, i) => i !== index));
    toast.success('Item removed from the form');
  };


  // Save as draft
  const handleSaveDraft = async () => {
    const mappingError = getPurchaseOrderItemMappingError(items);
    if (mappingError) { toast.error(mappingError); return; }
    if (!selectedSource) {
      toast.error('Select an approved procurement source before creating the purchase order');
      return;
    }
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
        sourceType: selectedSource.sourceType,
        sourceId: selectedSource.sourceId,
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
        plannedLandedCostPlan: buildPlannedCostPayload(landedCostPlanItems, items, landedCostPlanCurrency || documentCurrency, landedCostPlanNotes),
        items: items.map(item => ({
          inventoryItemId: item.inventoryItemId || undefined,
          lineType: purchaseOrderLineType(item.lineType),
          supplierItemCode: item.supplierItemCode || undefined,
          itemDescription: item.itemDescription || undefined,
          orderedQuantity: item.orderedQuantity,
          unitOfMeasure: item.unitOfMeasure || 'EA',
          itemUnitOfMeasureId: item.itemUnitOfMeasureId || undefined,
          warehouseId:
            requiresPurchaseOrderStock(item.lineType) ? (orderType === 'Consignment' ? normalizedDeliveryWarehouseId : item.warehouseId || undefined) : undefined,
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
    const mappingError = getPurchaseOrderItemMappingError(items);
    if (mappingError) { toast.error(mappingError); return; }
    if (!selectedSource) {
      toast.error('Select an approved procurement source before creating the purchase order');
      return;
    }
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
        sourceType: selectedSource.sourceType,
        sourceId: selectedSource.sourceId,
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
        plannedLandedCostPlan: buildPlannedCostPayload(landedCostPlanItems, items, landedCostPlanCurrency || documentCurrency, landedCostPlanNotes),
        items: items.map(item => ({
          inventoryItemId: item.inventoryItemId || undefined,
          lineType: purchaseOrderLineType(item.lineType),
          supplierItemCode: item.supplierItemCode || undefined,
          itemDescription: item.itemDescription || undefined,
          orderedQuantity: item.orderedQuantity,
          unitOfMeasure: item.unitOfMeasure || 'EA',
          itemUnitOfMeasureId: item.itemUnitOfMeasureId || undefined,
          warehouseId:
            requiresPurchaseOrderStock(item.lineType) ? (orderType === 'Consignment' ? normalizedDeliveryWarehouseId : item.warehouseId || undefined) : undefined,
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

      <Card className="border-amber-300 bg-amber-50/60">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Check className="h-5 w-5 text-amber-700" />
            Approved Procurement Source
          </CardTitle>
          <CardDescription>
            Every purchase order must retain an approved requisition, sourcing case,
            award-readiness decision, and award, contract, or approved exception.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {loadingSources ? (
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              Checking approved sources...
            </div>
          ) : sourceStatus?.ready ? (
            <>
              <div className="space-y-2">
                <Label htmlFor="approvedSource">Approved source *</Label>
                <Select value={selectedSourceKey} onValueChange={handleSourceChange}>
                  <SelectTrigger id="approvedSource">
                    <SelectValue placeholder="Select an approved source" />
                  </SelectTrigger>
                  <SelectContent>
                    {sourceStatus.sources.map(source => (
                      <SelectItem
                        key={`${source.sourceType}:${source.sourceId}`}
                        value={`${source.sourceType}:${source.sourceId}`}
                      >
                        {source.sourceLabel} · PR {source.purchaseRequisitionNumber} ·{' '}
                        {source.businessPartnerName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              {selectedSource && (
                <div className="grid gap-3 rounded-lg border bg-background p-4 text-sm md:grid-cols-4">
                  <div>
                    <p className="text-muted-foreground">Requisition</p>
                    <p className="font-medium">{selectedSource.purchaseRequisitionNumber}</p>
                  </div>
                  <div>
                    <p className="text-muted-foreground">Supplier</p>
                    <p className="font-medium">{selectedSource.businessPartnerName}</p>
                  </div>
                  <div>
                    <p className="text-muted-foreground">Approved value</p>
                    <p className="font-medium">
                      {selectedSource.approvedAmount == null
                        ? 'Source-controlled'
                        : `${selectedSource.currencyCode} ${selectedSource.approvedAmount.toLocaleString()}`}
                    </p>
                  </div>
                  <div>
                    <p className="text-muted-foreground">PO currency</p>
                    <p className="font-medium">{documentCurrency}</p>
                    <p className="text-xs text-muted-foreground">Inherited from approved source</p>
                  </div>
                  <div className="md:col-span-4">
                    <p className="text-muted-foreground">Immutable lineage</p>
                    <p className="break-all font-mono text-xs">
                      Case {selectedSource.sourcingCaseId} · Readiness{' '}
                      {selectedSource.awardReadinessDecisionId}
                    </p>
                  </div>
                </div>
              )}
            </>
          ) : (
            <div className="space-y-3">
              <div className="rounded-lg border border-amber-300 bg-amber-100 p-3 text-sm text-amber-950">
                {(sourceStatus?.blockedReasons ?? ['No approved source is available.']).map(
                  reason => <p key={reason}>{reason}</p>
                )}
              </div>
              <Button
                type="button"
                variant="outline"
                onClick={() =>
                  router.push(
                    sourceStatus?.frameworkCallOffRoute ||
                      '/procurement/framework-call-offs'
                  )
                }
              >
                Open governed framework call-offs
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

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
                  {formatProcurementMoney(sourceRequisition.totalAmount, sourceRequisition.currency)}
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
            <Select
              value={selectedSupplierId}
              onValueChange={handleSupplierChange}
              disabled={Boolean(selectedSource)}
            >
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
            {selectedSource && (
              <p className="text-xs text-muted-foreground">
                Supplier is locked to the approved source.
              </p>
            )}
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
              <CardDescription>Catalogue selection is optional. Enter stock goods, non-stock goods or services; stock items must be mapped before receiving.</CardDescription>
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
                              <PurchaseOrderLineTypeSelect value={editingItem?.lineType} onChange={handleLineTypeChange} />
                              <Select
                                value={editingItem?.inventoryItemId || '__none__'}
                                onValueChange={handleInlineInventoryItemSelect}
                              >
                                <SelectTrigger className="w-full">
                                  <SelectValue placeholder="Select item" />
                                </SelectTrigger>
                                <SelectContent>
                                  <SelectItem value="__none__">Ad hoc — no catalogue item</SelectItem>
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
                                readOnly={Boolean(editingApprovedLine)}
                                onChange={(e) => setEditingItem(prev => prev ? { ...prev, itemDescription: e.target.value } : null)}
                                placeholder="Description"
                              />
                            </TableCell>
                            <TableCell>
                              <Select
                                disabled={!requiresPurchaseOrderStock(editingItem?.lineType)}
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
                              {editingApprovedLine ? (
                                <ApprovedPurchaseOrderUnit unit={editingApprovedLine.unitOfMeasure} />
                              ) : availableUOMs.length > 0 ? (
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
                                readOnly={Boolean(editingApprovedLine)}
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
                              <PurchaseOrderLineActions
                              name={item.itemName || item.itemDescription || `Line ${index + 1}`}
                              disabled={isAddingNewRow || editingRowIndex !== null}
                              costCount={landedCostPlanItems.filter(c => c.purchaseOrderLineKey === item.tempId).length}
                              onEdit={() => handleEditRow(index)}
                              onCosts={() => setCostLineKey(item.tempId)}
                              onDelete={() => handleDeleteItem(index)}
                            />
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
                          <PurchaseOrderLineTypeSelect value={editingItem.lineType} onChange={handleLineTypeChange} />
                          <Select
                            value={editingItem.inventoryItemId || '__none__'}
                            onValueChange={handleInlineInventoryItemSelect}
                          >
                            <SelectTrigger className="w-full">
                              <SelectValue placeholder="Select item" />
                            </SelectTrigger>
                            <SelectContent>
                              <SelectItem value="__none__">Ad hoc — no catalogue item</SelectItem>
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
                            readOnly={Boolean(editingApprovedLine)}
                            onChange={(e) => setEditingItem(prev => prev ? { ...prev, itemDescription: e.target.value } : null)}
                            placeholder="Description"
                          />
                        </TableCell>
                        <TableCell>
                          <Select
                            disabled={!requiresPurchaseOrderStock(editingItem.lineType)}
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
                          {editingApprovedLine ? (
                            <ApprovedPurchaseOrderUnit unit={editingApprovedLine.unitOfMeasure} />
                          ) : availableUOMs.length > 0 ? (
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
                            readOnly={Boolean(editingApprovedLine)}
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

                  <PurchaseOrderPlannedCosts costs={landedCostPlanItems} onChange={setLandedCostPlanItems}
                  currency={landedCostPlanCurrency || documentCurrency} onCurrencyChange={setLandedCostPlanCurrency}
                  notes={landedCostPlanNotes} onNotesChange={setLandedCostPlanNotes} suppliers={suppliers}
                  lines={items} selectedLineKey={costLineKey} onCloseLine={() => setCostLineKey(null)} />
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
