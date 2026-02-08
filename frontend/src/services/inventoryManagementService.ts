/**
 * Inventory Management Service
 * API service for enhanced inventory management features
 */

import axios from 'axios';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

// ============================================================================
// INTERFACES
// ============================================================================

// Unit of Measure
export interface UnitOfMeasureDto {
  id: string;
  code: string;
  name: string;
  symbol?: string;
  category?: string;
  isBaseUnit: boolean;
  isActive: boolean;
  sortOrder: number;
}

export interface CreateUnitOfMeasureDto {
  code: string;
  name: string;
  symbol?: string;
  category?: string;
  isBaseUnit: boolean;
  sortOrder: number;
}

export interface UpdateUnitOfMeasureDto {
  name: string;
  symbol?: string;
  category?: string;
  isBaseUnit: boolean;
  isActive: boolean;
  sortOrder: number;
}

export interface UnitOfMeasureConversionDto {
  id: string;
  fromUnitId: string;
  fromUnitCode: string;
  toUnitId: string;
  toUnitCode: string;
  conversionFactor: number;
  isActive: boolean;
}

export interface CreateUnitOfMeasureConversionDto {
  fromUnitId: string;
  toUnitId: string;
  conversionFactor: number;
}

// Item Unit of Measure
export interface ItemUnitOfMeasureDto {
  id: string;
  unitOfMeasureId: string;
  unitCode: string;
  unitName: string;
  conversionFactor: number;
  isBaseUnit: boolean;
  isPurchaseUnit: boolean;
  isSalesUnit: boolean;
  barcode?: string;
}

// Inventory Category
export interface InventoryCategoryDto {
  id: string;
  code: string;
  name: string;
  description?: string;
  parentCategoryId?: string;
  color?: string;
  icon?: string;
  isActive: boolean;
  defaultUnitOfMeasure?: string;
  defaultSerialTracking: boolean;
  defaultLotTracking: boolean;
  defaultRequiresInspection: boolean;
}

// Inventory Item
export interface InventoryItemDto {
  id: string;
  itemCode: string;
  name: string;
  description?: string;
  shortDescription?: string;
  categoryId: string;
  categoryName?: string;
  unitOfMeasure: string;
  unitOfMeasureId?: string;
  unitOfMeasureScheduleId?: string;
  unitOfMeasureScheduleName?: string;
  currentStock: number;
  availableStock: number;
  allocatedStock: number;
  onOrderStock: number;
  minimumLevel: number;
  maximumLevel: number;
  reorderLevel: number;
  reorderQuantity: number;
  standardCost: number;
  averageCost: number;
  currentCost: number;
  listPrice: number;
  lastPurchaseCost: number;
  sellingPrice: number;
  isSerialTracked: boolean;
  isLotTracked: boolean;
  isBatchTracked: boolean;
  itemType: number;
  status: number;
  isActive: boolean;
  brand?: string;
  manufacturer?: string;
  model?: string;
  barcode?: string;
  primarySupplierId?: string;
  primarySupplierName?: string;
  leadTimeDays: number;
  // Media
  imageUrl?: string;
  thumbnailUrl?: string;
  safetyStockDays: number;
  shelfLife?: number;
  lastStockDate?: string;
  lastPurchaseDate?: string;
  // GP-Style fields
  itemClassName?: string;
  priceGroupName?: string;
  itemClassId?: string;
  priceGroupId?: string;
  genericDescription?: string;
  quantityDecimals: number;
  currencyDecimals: number;
  purchaseTaxOption?: string;
  salesTaxOption?: string;
  substituteItem1Id?: string;
  substituteItem1Name?: string;
  substituteItem2Id?: string;
  substituteItem2Name?: string;
  substituteItem3Id?: string;
  substituteItem3Name?: string;
  substituteItem4Id?: string;
  substituteItem4Name?: string;
  lotCategory?: string;
  minimumShelfLifeDays: number;
  warnBeforeLotExpires: boolean;
  daysBeforeExpiryWarning: number;
  warrantyDays: number;
  isKit: boolean;
  isKitComponent: boolean;
  isFinishedGood: boolean;
  isFinishedGoodComponent: boolean;
  includeInQuotes: boolean;
  includeInOrders: boolean;
  includeInInvoices: boolean;
  includeInFulfillment: boolean;
  isProcurementItem: boolean;
  style?: string;
  feature?: string;
  maintainCalendarYearHistory: boolean;
  maintainFiscalYearHistory: boolean;
  maintainTransactionHistory: boolean;
  shippingWeight: number;
  allowBackorder: boolean;
  valuationMethod: number; // 1=WeightedAverage, 2=FIFO, 3=LIFO, 4=StandardCost
  isValuationLocked: boolean;
}

export interface CreateInventoryItemDto {
  // Basic Info
  itemCode: string;
  name: string;
  description?: string;
  shortDescription?: string;
  genericDescription?: string;
  categoryId: string;
  subCategoryId?: string;
  itemClassId?: string;
  priceGroupId?: string;
  brand?: string;
  manufacturer?: string;
  model?: string;
  style?: string;
  feature?: string;
  unitOfMeasure: string;
  unitOfMeasureId?: string;
  unitOfMeasureScheduleId?: string;
  valuationMethod: number; // 1=WeightedAverage, 2=FIFO, 3=LIFO, 4=StandardCost
  quantityDecimals: number;
  currencyDecimals: number;
  itemType: number;

  // Costs & Pricing
  standardCost: number;
  currentCost: number;
  listPrice: number;
  sellingPrice?: number;

  // Stock Settings
  minimumLevel: number;
  maximumLevel: number;
  reorderLevel: number;
  reorderQuantity: number;
  safetyStock: number;
  leadTimeDays: number;
  safetyLeadTimeDays: number;
  allowBackorder: boolean;
  autoReorder: boolean;

  // Tracking
  isSerialTracked: boolean;
  isLotTracked: boolean;
  isBatchTracked: boolean;
  isExpirationTracked: boolean;
  isLocationTracked: boolean;
  lotCategory?: string;
  minimumShelfLifeDays: number;
  warnBeforeLotExpires: boolean;
  daysBeforeExpiryWarning: number;

  // Item Options
  substituteItem1Id?: string;
  substituteItem2Id?: string;
  substituteItem3Id?: string;
  substituteItem4Id?: string;
  warrantyDays: number;
  isKit: boolean;
  isKitComponent: boolean;
  isFinishedGood: boolean;
  isFinishedGoodComponent: boolean;
  includeInQuotes: boolean;
  includeInOrders: boolean;
  includeInInvoices: boolean;
  includeInFulfillment: boolean;
  isProcurementItem: boolean;
  maintainCalendarYearHistory: boolean;
  maintainFiscalYearHistory: boolean;
  maintainTransactionHistory: boolean;

  // Tax
  taxCode?: string;
  isTaxable: boolean;
  purchaseTaxOption?: string;
  salesTaxOption?: string;
  purchaseTaxScheduleId?: string;
  salesTaxScheduleId?: string;

  // Physical Properties
  weight?: number;
  shippingWeight: number;
  length?: number;
  width?: number;
  height?: number;
  volume?: number;

  // Supplier
  primarySupplierId?: string;
  primarySupplier?: string;
  supplierItemCode?: string;
  defaultWarehouseId?: string;

  // Media
  imageUrl?: string;
  thumbnailUrl?: string;
  barcode?: string;
  qrCode?: string;
  alternateBarcode?: string;

  // Customs
  countryOfOrigin?: string;
  hsCode?: string;
}

export interface UpdateInventoryItemDto extends CreateInventoryItemDto {
  isActive: boolean;
}

// Item Class
export interface ItemClassDto {
  id: string;
  classId: string;
  description: string;
  defaultItemType: number;
  defaultValuationMethod: string;
  defaultTaxCode?: string;
  defaultIsSerialTracked: boolean;
  defaultIsLotTracked: boolean;
  defaultRequiresInspection: boolean;
  isActive: boolean;
}

// Price Group
export interface PriceGroupDto {
  id: string;
  priceGroupCode: string;
  description: string;
  currency: string;
  defaultMarkupPercent: number;
  defaultMarginPercent: number;
  isActive: boolean;
}

// Unit of Measure Schedule
export interface UnitOfMeasureScheduleDto {
  id: string;
  scheduleId: string;
  description: string;
  baseUnitOfMeasureId: string;
  baseUnitOfMeasureName?: string;
  baseUnitOfMeasureCode?: string;
  quantityDecimals: number;
  isActive: boolean;
  details: UnitOfMeasureScheduleDetailDto[];
}

export interface UnitOfMeasureScheduleDetailDto {
  id: string;
  scheduleId: string;
  unitOfMeasureId: string;
  unitOfMeasureName?: string;
  unitOfMeasureCode?: string;
  baseQuantity: number;
  sortOrder: number;
}

export interface CreateUnitOfMeasureScheduleDto {
  scheduleId: string;
  description: string;
  baseUnitOfMeasureId: string;
  quantityDecimals: number;
  details: CreateUnitOfMeasureScheduleDetailDto[];
}

export interface CreateUnitOfMeasureScheduleDetailDto {
  unitOfMeasureId: string;
  baseQuantity: number;
  sortOrder: number;
}

export interface UpdateUnitOfMeasureScheduleDto {
  description: string;
  baseUnitOfMeasureId: string;
  quantityDecimals: number;
  isActive: boolean;
  details: CreateUnitOfMeasureScheduleDetailDto[];
}

// Suggested Sales Item
export interface SuggestedSalesItemDto {
  id: string;
  inventoryItemId: string;
  suggestedItemId: string;
  suggestedItemCode?: string;
  suggestedItemName?: string;
  description?: string;
  suggestedQuantity: number;
  suggestOnQuote: boolean;
  suggestOnOrder: boolean;
  suggestOnInvoice: boolean;
  suggestOnFulfillment: boolean;
  sortOrder: number;
  isActive: boolean;
}

// Warehouse
export interface WarehouseDto {
  id: string;
  name: string;
  code: string;
  description?: string;
  address?: string;
  city?: string;
  state?: string;
  zipCode?: string;
  country?: string;
  isActive: boolean;
  isDefault: boolean;
  warehouseType: string;
  contactPerson?: string;
  phone?: string;
  email?: string;
  totalCapacitySquareFeet?: number;
  usedCapacitySquareFeet?: number;
  hasQuarantineArea: boolean;
  hasInspectionArea: boolean;
  hasReceivingDock: boolean;
  hasShippingDock: boolean;
  isTemperatureControlled: boolean;
}

export interface CreateWarehouseDto {
  name: string;
  code: string;
  description?: string;
  address?: string;
  city?: string;
  state?: string;
  zipCode?: string;
  country?: string;
  warehouseType: string;
  contactPerson?: string;
  phone?: string;
  email?: string;
  isDefault?: boolean;
}

export interface UpdateWarehouseDto extends CreateWarehouseDto {
  isActive: boolean;
}

// Warehouse Inventory Item (for requisition dialog - items with stock in a warehouse)
export interface WarehouseInventoryItemDto {
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  itemType: number;
  description?: string;
  unitOfMeasure?: string;
  currentStock: number;
  availableStock: number;
  allocatedStock: number;
  unitCost: number;
  dailyRentalRate: number;
  categoryName?: string;
}

// Warehouse Items (Item-Warehouse assignments with quantities)
export interface WarehouseItemDto {
  id: string;
  warehouseId: string;
  warehouseName: string;
  warehouseCode: string;
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  itemType: string;
  categoryName?: string;
  unitOfMeasure?: string;
  currentStock: number;
  availableStock: number;
  allocatedStock: number;
  reorderLevel: number;
  maxStock: number;
  averageCost: number;
  lastMovementDate?: string;
  lastStockTakeDate?: string;
  notes?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface BulkAssignItemsDto {
  inventoryItemIds: string[];
  warehouseIds: string[];
  initialQuantity: number;
  reorderLevel: number;
  maxStock: number;
}

export interface UpdateWarehouseItemDto {
  currentStock: number;
  reorderLevel: number;
  maxStock: number;
  notes?: string;
}

export interface BulkAssignmentResultDto {
  created: number;
  skipped: number;
  errors: string[];
}

export interface WarehouseLocationDto {
  id: string;
  warehouseId: string;
  warehouseName?: string;
  locationCode: string;
  name?: string;
  description?: string;
  locationType: string;
  parentLocationId?: string;
  parentLocationName?: string;
  isActive: boolean;
  isPickingLocation: boolean;
  isReceivingLocation: boolean;
  maxWeight?: number;
  maxVolume?: number;
  maxItems?: number;
  currentWeight: number;
  currentVolume: number;
  currentItemCount: number;
}

export interface CreateWarehouseLocationDto {
  warehouseId: string;
  locationCode: string;
  name?: string;
  description?: string;
  locationType: string;
  parentLocationId?: string;
  isPickingLocation: boolean;
  isReceivingLocation: boolean;
  maxWeight?: number;
  maxVolume?: number;
  maxItems?: number;
}

export interface UpdateWarehouseLocationDto extends CreateWarehouseLocationDto {
  isActive: boolean;
}

// Goods Receipt Note
export interface GoodsReceiptNoteDto {
  id: string;
  grnNumber: string;
  purchaseOrderId?: string;
  purchaseOrderNumber?: string;
  supplierId?: string;
  supplierName?: string;
  warehouseId: string;
  warehouseName?: string;
  receiptDate: string;
  status: string;
  requiresInspection: boolean;
  totalItems: number;
  notes?: string;
}

export interface GoodsReceiptNoteDetailDto extends GoodsReceiptNoteDto {
  items: GoodsReceiptNoteItemDto[];
}

export interface GoodsReceiptNoteItemDto {
  id: string;
  inventoryItemId: string;
  itemCode?: string;
  itemName?: string;
  orderedQuantity: number;
  receivedQuantity: number;
  acceptedQuantity: number;
  rejectedQuantity: number;
  unitOfMeasure?: string;
  unitCost: number;
  inspectionResult: string;
  inspectionNotes?: string;
  locationId?: string;
  locationName?: string;
  batchNumber?: string;
  expiryDate?: string;
}

export interface CreateGoodsReceiptNoteDto {
  purchaseOrderId?: string;
  supplierId?: string;
  warehouseId: string;
  receiptDate: string;
  requiresInspection: boolean;
  notes?: string;
  items: CreateGoodsReceiptNoteItemDto[];
}

export interface CreateGoodsReceiptNoteItemDto {
  inventoryItemId: string;
  orderedQuantity: number;
  receivedQuantity: number;
  unitCost: number;
  locationId?: string;
  batchNumber?: string;
  expiryDate?: string;
}

export interface UpdateGRNInspectionDto {
  grnItemId: string;
  inspectionResult: string;
  acceptedQuantity: number;
  rejectedQuantity: number;
  inspectionNotes?: string;
}

// Inventory Transfer
export interface InventoryTransferDto {
  id: string;
  transferNumber: string;
  sourceWarehouseId: string;
  sourceWarehouseName?: string;
  destinationWarehouseId: string;
  destinationWarehouseName?: string;
  status: string;
  currentWorkflowStepName?: string;
  requestedDate: string;
  expectedDeliveryDate?: string;
  shippedDate?: string;
  receivedDate?: string;
  trackingNumber?: string;
  shippingCost?: number;
  miscellaneousCost?: number;
  miscellaneousCostDescription?: string;
  totalAdditionalCost?: number;
  costAllocationMethod?: 'SpreadToItemCost' | 'GLExpense';
  costApportionmentBasis?: 'Value' | 'Weight' | 'Quantity';
  expenseGLAccount?: string;
  costsAllocated?: boolean;
  totalItems: number;
  notes?: string;
}

export interface InventoryTransferDetailDto extends InventoryTransferDto {
  items: InventoryTransferItemDto[];
}

export interface InventoryTransferItemDto {
  id: string;
  inventoryItemId: string;
  itemCode?: string;
  itemName?: string;
  requestedQuantity: number;
  shippedQuantity: number;
  receivedQuantity: number;
  unitOfMeasure?: string;
  unitCost?: number;
  totalCost?: number;
  allocatedCostPerUnit?: number;
  totalAllocatedCost?: number;
  landedUnitCost?: number;
  lotNumber?: string;
  serialNumber?: string;
  sourceLocationId?: string;
  sourceLocationName?: string;
  destinationLocationId?: string;
  destinationLocationName?: string;
  notes?: string;
}

export interface CreateInventoryTransferDto {
  sourceWarehouseId: string;
  destinationWarehouseId: string;
  expectedDeliveryDate?: string;
  notes?: string;
  items: CreateInventoryTransferItemDto[];
}

export interface CreateInventoryTransferItemDto {
  inventoryItemId: string;
  requestedQuantity: number;
  notes?: string;
}

export interface UpdateInventoryTransferDto {
  sourceWarehouseId?: string;
  destinationWarehouseId?: string;
  requiredDate?: string;
  reason?: string;
  notes?: string;
}

export interface AddTransferItemDto {
  inventoryItemId: string;
  requestedQuantity: number;
  sourceLocationId?: string;
  destinationLocationId?: string;
  lotNumber?: string;
  serialNumber?: string;
  notes?: string;
}

export interface UpdateTransferItemDto {
  requestedQuantity: number;
  sourceLocationId?: string;
  destinationLocationId?: string;
  lotNumber?: string;
  serialNumber?: string;
  notes?: string;
}

export interface ShipTransferItemDto {
  itemId: string;
  shippedQuantity: number;
}

export interface ShipTransferWithCostsDto {
  trackingNumber?: string;
  carrierName?: string;
  shippingCost: number;
  miscellaneousCost: number;
  miscellaneousCostDescription?: string;
  costAllocationMethod: 'SpreadToItemCost' | 'GLExpense';
  costApportionmentBasis?: 'Value' | 'Weight' | 'Quantity';
  expenseGLAccount?: string;
  items?: ShipTransferItemDto[];
}

export interface ReceiveTransferItemDto {
  id: string;  // Backend expects Id (transfer item id)
  receivedQuantity: number;
}

// Physical Count
export interface PhysicalCountDto {
  id: string;
  countNumber: string;
  warehouseId: string;
  warehouseName?: string;
  countType: string;
  status: string;
  countDate: string;
  startedDate?: string;
  completedDate?: string;
  locationId?: string;
  locationName?: string;
  categoryId?: string;
  categoryName?: string;
  freezeInventory: boolean;
  totalItems: number;
  countedItems: number;
  itemsWithVariance: number;
  totalVarianceValue: number;
  initiatedByName?: string;
  notes?: string;
  createdAtFormatted?: string;
}

export interface PhysicalCountDetailDto extends PhysicalCountDto {
  approvedByName?: string;
  approvedDate?: string;
  items: PhysicalCountItemDto[];
}

export interface PhysicalCountItemDto {
  id: string;
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  locationId?: string;
  locationName?: string;
  systemQuantity: number;
  countedQuantity: number;
  varianceQuantity: number;
  varianceValue: number;
  variancePercent: number;
  unitOfMeasure: string;
  lotNumber?: string;
  serialNumber?: string;
  isCounted: boolean;
  countedAt?: string;
  countedByName?: string;
  notes?: string;
}

export interface CreatePhysicalCountDto {
  warehouseId: string;
  countType: string;  // FullCount, CycleCount, SpotCheck, ABCCount
  locationId?: string;
  categoryId?: string;
  freezeInventory?: boolean;
  notes?: string;
}

export interface UpdatePhysicalCountDto {
  notes?: string;
  freezeInventory?: boolean;
  blindCount?: boolean;
  includeZeroStock?: boolean;
}

export interface AddCountItemDto {
  inventoryItemId: string;
  locationId?: string;
  systemQuantity?: number;
  lotNumber?: string;
  serialNumber?: string;
}

export interface RecordCountItemDto {
  physicalCountItemId: string;
  countedQuantity: number;
  lotNumber?: string;
  serialNumber?: string;
  notes?: string;
}

export interface PhysicalCountFilterDto {
  warehouseId?: string;
  status?: string;
  countType?: string;
  fromDate?: string;
  toDate?: string;
  countNumber?: string;
  page?: number;
  pageSize?: number;
}

export interface PhysicalCountExportDto {
  id: string;
  countNumber: string;
  warehouseName: string;
  countType: string;
  status: string;
  countDateFormatted: string;
  items: PhysicalCountItemExportDto[];
}

export interface PhysicalCountItemExportDto {
  itemCode: string;
  itemName: string;
  unitOfMeasure: string;
  locationName?: string;
  systemQuantity: number;
  countedQuantity: number;
  varianceQuantity: number;
  varianceValue: number;
  lotNumber?: string;
  serialNumber?: string;
  isCounted: boolean;
  notes?: string;
}

export interface ImportCountItemDto {
  itemCode: string;
  countedQuantity: number;
  lotNumber?: string;
  serialNumber?: string;
  notes?: string;
}

export interface ImportCountResultDto {
  totalRows: number;
  successCount: number;
  errorCount: number;
  errors: string[];
}

export interface VarianceReportDto {
  countNumber: string;
  warehouseName: string;
  countType: string;
  countDateFormatted: string;
  totalItems: number;
  itemsWithVariance: number;
  totalSystemValue: number;
  totalCountedValue: number;
  totalVarianceValue: number;
  variancePercentage: number;
  items: VarianceItemDto[];
}

export interface VarianceItemDto {
  itemCode: string;
  itemName: string;
  locationName?: string;
  systemQuantity: number;
  countedQuantity: number;
  varianceQuantity: number;
  unitCost: number;
  varianceValue: number;
  variancePercent: number;
  notes?: string;
}

// Item Supplier
export interface ItemSupplierDto {
  id: string;
  inventoryItemId: string;
  itemCode?: string;
  itemName?: string;
  supplierId: string;
  supplierCode?: string;
  supplierName?: string;
  supplierItemCode?: string;
  supplierItemName?: string;
  unitPrice: number;
  currency?: string;
  leadTimeDays: number;
  minimumOrderQuantity: number;
  orderMultiple?: number;
  priority?: number;
  isPreferred: boolean;
  isActive: boolean;
  lastPurchaseDate?: string;
  lastPurchasePrice?: number;
  notes?: string;
}

export interface CreateItemSupplierDto {
  inventoryItemId: string;
  supplierId: string;
  supplierItemCode?: string;
  supplierItemName?: string;
  unitPrice: number;
  currency?: string;
  leadTimeDays: number;
  minimumOrderQuantity: number;
  orderMultiple?: number;
  priority?: number;
  isPreferred: boolean;
  notes?: string;
}

export interface UpdateItemSupplierDto {
  supplierItemCode?: string;
  supplierItemName?: string;
  unitPrice: number;
  currency?: string;
  leadTimeDays: number;
  minimumOrderQuantity: number;
  orderMultiple?: number;
  priority?: number;
  isPreferred: boolean;
  isActive: boolean;
  notes?: string;
}

// Stock Movement
export interface StockMovementDto {
  id: string;
  movementNumber: string;
  inventoryItemId: string;
  itemCode?: string;
  itemName?: string;
  movementType: string;
  quantity: number;
  unitCost: number;
  totalCost: number;
  sourceLocationId?: string;
  sourceLocationName?: string;
  destinationLocationId?: string;
  destinationLocationName?: string;
  referenceType?: string;
  referenceId?: string;
  referenceNumber?: string;
  movementDate: string;
  notes?: string;
  createdByName?: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export interface BinStockDto {
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  unitOfMeasure?: string;

  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;

  locationId: string;
  locationCode: string;

  quantity: number;
  availableQuantity: number;
  allocatedQuantity: number;
  averageCost: number;
  lastMovementDate?: string;
}

// ============================================================================
// SERVICE CLASS
// ============================================================================

class InventoryManagementService {
  private getAuthHeaders() {
    const token = localStorage.getItem('token') || localStorage.getItem('authToken');
    return {
      'Content-Type': 'application/json',
      'Authorization': token ? `Bearer ${token}` : ''
    };
  }

  // ========== UNITS OF MEASURE ==========

  async getUnitsOfMeasure(activeOnly: boolean = false): Promise<UnitOfMeasureDto[]> {
    const response = await axios.get(`${API_URL}/inventory/units-of-measure`, {
      params: { activeOnly },
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getUnitOfMeasureById(id: string): Promise<UnitOfMeasureDto> {
    const response = await axios.get(`${API_URL}/inventory/units-of-measure/${id}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getUnitsByCategory(category: string): Promise<UnitOfMeasureDto[]> {
    const response = await axios.get(`${API_URL}/inventory/units-of-measure/by-category/${category}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getBaseUnits(): Promise<UnitOfMeasureDto[]> {
    const response = await axios.get(`${API_URL}/inventory/units-of-measure/base-units`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async createUnitOfMeasure(data: CreateUnitOfMeasureDto): Promise<UnitOfMeasureDto> {
    const response = await axios.post(`${API_URL}/inventory/units-of-measure`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async updateUnitOfMeasure(id: string, data: UpdateUnitOfMeasureDto): Promise<UnitOfMeasureDto> {
    const response = await axios.put(`${API_URL}/inventory/units-of-measure/${id}`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async deleteUnitOfMeasure(id: string): Promise<void> {
    await axios.delete(`${API_URL}/inventory/units-of-measure/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  async getUnitConversions(unitId: string): Promise<UnitOfMeasureConversionDto[]> {
    const response = await axios.get(`${API_URL}/inventory/units-of-measure/${unitId}/conversions`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async createUnitConversion(data: CreateUnitOfMeasureConversionDto): Promise<UnitOfMeasureConversionDto> {
    const response = await axios.post(`${API_URL}/inventory/units-of-measure/conversions`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async deleteUnitConversion(id: string): Promise<void> {
    await axios.delete(`${API_URL}/inventory/units-of-measure/conversions/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  // ========== UOM SCHEDULES ==========

  async getUomSchedules(activeOnly: boolean = false): Promise<UnitOfMeasureScheduleDto[]> {
    const response = await axios.get(`${API_URL}/inventory/uom-schedules`, {
      params: { activeOnly },
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getUomScheduleById(id: string): Promise<UnitOfMeasureScheduleDto> {
    const response = await axios.get(`${API_URL}/inventory/uom-schedules/${id}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async createUomSchedule(data: CreateUnitOfMeasureScheduleDto): Promise<UnitOfMeasureScheduleDto> {
    const response = await axios.post(`${API_URL}/inventory/uom-schedules`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async updateUomSchedule(id: string, data: UpdateUnitOfMeasureScheduleDto): Promise<UnitOfMeasureScheduleDto> {
    const response = await axios.put(`${API_URL}/inventory/uom-schedules/${id}`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async deleteUomSchedule(id: string): Promise<void> {
    await axios.delete(`${API_URL}/inventory/uom-schedules/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  // ========== INVENTORY ITEMS ==========

  async getInventoryItems(params?: { itemType?: number; categoryId?: string; search?: string; isActive?: boolean }): Promise<InventoryItemDto[]> {
    const response = await axios.get(`${API_URL}/InventoryItems`, {
      params,
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getBinStock(params?: {
    warehouseId?: string;
    locationId?: string;
    search?: string;
    includeZero?: boolean;
    page?: number;
    pageSize?: number;
  }): Promise<PagedResult<BinStockDto>> {
    const response = await axios.get(`${API_URL}/InventoryItems/bin-stock`, {
      params,
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getInventoryItemById(id: string): Promise<InventoryItemDto> {
    const response = await axios.get(`${API_URL}/InventoryItems/${id}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async createInventoryItem(data: CreateInventoryItemDto): Promise<InventoryItemDto> {
    const response = await axios.post(`${API_URL}/InventoryItems`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async updateInventoryItem(id: string, data: UpdateInventoryItemDto): Promise<InventoryItemDto> {
    const response = await axios.put(`${API_URL}/InventoryItems/${id}`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async deleteInventoryItem(id: string): Promise<void> {
    await axios.delete(`${API_URL}/InventoryItems/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  async getInventoryByWarehouse(warehouseId: string, itemType?: number): Promise<InventoryItemDto[]> {
    const response = await axios.get(`${API_URL}/InventoryItems/by-warehouse/${warehouseId}`, {
      params: { itemType },
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  /**
   * Get warehouse inventory items with stock information
   * This method is used by RequisitionDialog to get items available in a specific warehouse
   */
  async getWarehouseInventoryItems(warehouseId: string, itemType?: number): Promise<WarehouseInventoryItemDto[]> {
    const response = await axios.get(`${API_URL}/InventoryItems/by-warehouse/${warehouseId}`, {
      params: { itemType },
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  /**
   * Get available units of measure for an inventory item
   */
  async getItemUnitsOfMeasure(itemId: string): Promise<ItemUnitOfMeasureDto[]> {
    const response = await axios.get(`${API_URL}/InventoryItems/${itemId}/units`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // ========== WAREHOUSES ==========

  async getWarehouses(activeOnly: boolean = false): Promise<WarehouseDto[]> {
    const response = await axios.get(`${API_URL}/InventoryItems/warehouses`, {
      params: { activeOnly },
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getWarehouseById(id: string): Promise<WarehouseDto> {
    const response = await axios.get(`${API_URL}/inventory/warehouses/${id}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async createWarehouse(data: CreateWarehouseDto): Promise<WarehouseDto> {
    const response = await axios.post(`${API_URL}/inventory/warehouses`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async updateWarehouse(id: string, data: UpdateWarehouseDto): Promise<WarehouseDto> {
    const response = await axios.put(`${API_URL}/inventory/warehouses/${id}`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async deleteWarehouse(id: string): Promise<void> {
    await axios.delete(`${API_URL}/inventory/warehouses/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  // ========== WAREHOUSE LOCATIONS ==========

  async getWarehouseLocations(warehouseId?: string): Promise<WarehouseLocationDto[]> {
    const response = await axios.get(`${API_URL}/InventoryItems/warehouse-locations`, {
      params: { warehouseId },
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getWarehouseLocationById(id: string): Promise<WarehouseLocationDto> {
    const response = await axios.get(`${API_URL}/inventory/warehouse-locations/${id}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async createWarehouseLocation(data: CreateWarehouseLocationDto): Promise<WarehouseLocationDto> {
    const response = await axios.post(`${API_URL}/inventory/warehouse-locations`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async updateWarehouseLocation(id: string, data: UpdateWarehouseLocationDto): Promise<WarehouseLocationDto> {
    const response = await axios.put(`${API_URL}/inventory/warehouse-locations/${id}`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async deleteWarehouseLocation(id: string): Promise<void> {
    await axios.delete(`${API_URL}/inventory/warehouse-locations/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  // ========== WAREHOUSE ITEMS ==========

  async getWarehouseItems(warehouseId?: string): Promise<WarehouseItemDto[]> {
    const response = await axios.get(`${API_URL}/inventory/warehouse-items`, {
      params: { warehouseId },
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getWarehouseItemById(id: string): Promise<WarehouseItemDto> {
    const response = await axios.get(`${API_URL}/inventory/warehouse-items/${id}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getWarehouseItemsByInventoryItem(inventoryItemId: string): Promise<WarehouseItemDto[]> {
    const response = await axios.get(`${API_URL}/inventory/warehouse-items/by-item/${inventoryItemId}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async assignItemsToWarehouses(data: BulkAssignItemsDto): Promise<BulkAssignmentResultDto> {
    const response = await axios.post(`${API_URL}/inventory/warehouse-items/assign`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async updateWarehouseItem(id: string, data: UpdateWarehouseItemDto): Promise<WarehouseItemDto> {
    const response = await axios.put(`${API_URL}/inventory/warehouse-items/${id}`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async deleteWarehouseItem(id: string): Promise<void> {
    await axios.delete(`${API_URL}/inventory/warehouse-items/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  // ========== GOODS RECEIPT NOTES ==========

  async getGoodsReceiptNotes(fromDate?: string, toDate?: string): Promise<GoodsReceiptNoteDto[]> {
    const response = await axios.get(`${API_URL}/inventory/goods-receipt-notes`, {
      params: { fromDate, toDate },
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getGoodsReceiptNoteById(id: string): Promise<GoodsReceiptNoteDetailDto> {
    const response = await axios.get(`${API_URL}/inventory/goods-receipt-notes/${id}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getGoodsReceiptNoteByNumber(grnNumber: string): Promise<GoodsReceiptNoteDetailDto> {
    const response = await axios.get(`${API_URL}/inventory/goods-receipt-notes/by-number/${grnNumber}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getGRNsByWarehouse(warehouseId: string): Promise<GoodsReceiptNoteDto[]> {
    const response = await axios.get(`${API_URL}/inventory/goods-receipt-notes/by-warehouse/${warehouseId}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getGRNsBySupplier(supplierId: string): Promise<GoodsReceiptNoteDto[]> {
    const response = await axios.get(`${API_URL}/inventory/goods-receipt-notes/by-supplier/${supplierId}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getGRNsByPurchaseOrder(purchaseOrderId: string): Promise<GoodsReceiptNoteDto[]> {
    const response = await axios.get(`${API_URL}/inventory/goods-receipt-notes/by-purchase-order/${purchaseOrderId}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getPendingInspectionGRNs(): Promise<GoodsReceiptNoteDto[]> {
    const response = await axios.get(`${API_URL}/inventory/goods-receipt-notes/pending-inspection`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async createGoodsReceiptNote(data: CreateGoodsReceiptNoteDto): Promise<GoodsReceiptNoteDto> {
    const response = await axios.post(`${API_URL}/inventory/goods-receipt-notes`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async submitGRNForInspection(id: string): Promise<void> {
    await axios.post(`${API_URL}/inventory/goods-receipt-notes/${id}/submit-for-inspection`, {}, {
      headers: this.getAuthHeaders()
    });
  }

  async updateGRNInspection(id: string, data: UpdateGRNInspectionDto): Promise<void> {
    await axios.post(`${API_URL}/inventory/goods-receipt-notes/${id}/update-inspection`, data, {
      headers: this.getAuthHeaders()
    });
  }

  async completeGRNInspection(id: string): Promise<void> {
    await axios.post(`${API_URL}/inventory/goods-receipt-notes/${id}/complete-inspection`, {}, {
      headers: this.getAuthHeaders()
    });
  }

  async postGRNToInventory(id: string): Promise<void> {
    await axios.post(`${API_URL}/inventory/goods-receipt-notes/${id}/post-to-inventory`, {}, {
      headers: this.getAuthHeaders()
    });
  }

  async cancelGRN(id: string, reason: string): Promise<void> {
    await axios.post(`${API_URL}/inventory/goods-receipt-notes/${id}/cancel`, { reason }, {
      headers: this.getAuthHeaders()
    });
  }

  // ========== INVENTORY TRANSFERS ==========

  async getInventoryTransfers(fromDate?: string, toDate?: string): Promise<InventoryTransferDto[]> {
    const response = await axios.get(`${API_URL}/inventory/transfers`, {
      params: { fromDate, toDate },
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getInventoryTransferById(id: string): Promise<InventoryTransferDetailDto> {
    const response = await axios.get(`${API_URL}/inventory/transfers/${id}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getInventoryTransferByNumber(transferNumber: string): Promise<InventoryTransferDetailDto> {
    const response = await axios.get(`${API_URL}/inventory/transfers/by-number/${transferNumber}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getTransfersByWarehouse(warehouseId: string, isSource: boolean = true): Promise<InventoryTransferDto[]> {
    const response = await axios.get(`${API_URL}/inventory/transfers/by-warehouse/${warehouseId}`, {
      params: { isSource },
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getInTransitTransfers(): Promise<InventoryTransferDto[]> {
    const response = await axios.get(`${API_URL}/inventory/transfers/in-transit`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getPendingApprovalTransfers(): Promise<InventoryTransferDto[]> {
    const response = await axios.get(`${API_URL}/inventory/transfers/pending-approval`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async createInventoryTransfer(data: CreateInventoryTransferDto): Promise<InventoryTransferDto> {
    const response = await axios.post(`${API_URL}/inventory/transfers`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async updateInventoryTransfer(id: string, data: UpdateInventoryTransferDto): Promise<InventoryTransferDto> {
    const response = await axios.put(`${API_URL}/inventory/transfers/${id}`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async submitTransferForApproval(id: string): Promise<void> {
    await axios.post(`${API_URL}/inventory/transfers/${id}/submit-for-approval`, {}, {
      headers: this.getAuthHeaders()
    });
  }

  async approveTransfer(id: string, comments?: string): Promise<void> {
    await axios.post(`${API_URL}/inventory/transfers/${id}/approve`, { comments }, {
      headers: this.getAuthHeaders()
    });
  }

  async rejectTransfer(id: string, reason: string): Promise<void> {
    await axios.post(`${API_URL}/inventory/transfers/${id}/reject`, { reason }, {
      headers: this.getAuthHeaders()
    });
  }

  async shipTransfer(id: string, trackingNumber?: string, items?: ShipTransferItemDto[]): Promise<void> {
    await axios.post(`${API_URL}/inventory/transfers/${id}/ship`, { trackingNumber, items }, {
      headers: this.getAuthHeaders()
    });
  }

  async shipTransferWithCosts(id: string, costsDto: ShipTransferWithCostsDto): Promise<void> {
    await axios.post(`${API_URL}/inventory/transfers/${id}/ship-with-costs`, costsDto, {
      headers: this.getAuthHeaders()
    });
  }

  async saveShippingCosts(id: string, costsDto: ShipTransferWithCostsDto): Promise<void> {
    await axios.post(`${API_URL}/inventory/transfers/${id}/save-shipping-costs`, costsDto, {
      headers: this.getAuthHeaders()
    });
  }

  async receiveTransfer(id: string, receivedItems?: ReceiveTransferItemDto[]): Promise<void> {
    await axios.post(`${API_URL}/inventory/transfers/${id}/receive`, { receivedItems }, {
      headers: this.getAuthHeaders()
    });
  }

  async cancelTransfer(id: string, reason: string): Promise<void> {
    await axios.post(`${API_URL}/inventory/transfers/${id}/cancel`, { reason }, {
      headers: this.getAuthHeaders()
    });
  }

  async reverseShipment(id: string, reason: string): Promise<void> {
    await axios.post(`${API_URL}/inventory/transfers/${id}/reverse-shipment`, { reason }, {
      headers: this.getAuthHeaders()
    });
  }

  async getShipmentNotePdf(id: string): Promise<Blob> {
    const response = await axios.get(`${API_URL}/inventory/transfers/${id}/shipment-note`, {
      headers: this.getAuthHeaders(),
      responseType: 'blob'
    });
    return response.data;
  }

  async getGoodsReceivedNotePdf(id: string): Promise<Blob> {
    const response = await axios.get(`${API_URL}/inventory/transfers/${id}/grn`, {
      headers: this.getAuthHeaders(),
      responseType: 'blob'
    });
    return response.data;
  }

  async addTransferItem(transferId: string, dto: AddTransferItemDto): Promise<InventoryTransferItemDto> {
    const response = await axios.post(`${API_URL}/inventory/transfers/${transferId}/items`, dto, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async updateTransferItem(transferId: string, itemId: string, dto: UpdateTransferItemDto): Promise<InventoryTransferItemDto> {
    const response = await axios.put(`${API_URL}/inventory/transfers/${transferId}/items/${itemId}`, dto, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async removeTransferItem(transferId: string, itemId: string): Promise<void> {
    await axios.delete(`${API_URL}/inventory/transfers/${transferId}/items/${itemId}`, {
      headers: this.getAuthHeaders()
    });
  }

  // ========== PHYSICAL COUNTS ==========

  async getPhysicalCounts(fromDate?: string, toDate?: string): Promise<PhysicalCountDto[]> {
    const response = await axios.get(`${API_URL}/inventory/physical-counts`, {
      params: { fromDate, toDate },
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getPhysicalCountsFiltered(filter: PhysicalCountFilterDto): Promise<PhysicalCountDto[]> {
    const response = await axios.get(`${API_URL}/inventory/physical-counts/filter`, {
      params: filter,
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getPhysicalCountById(id: string): Promise<PhysicalCountDetailDto> {
    const response = await axios.get(`${API_URL}/inventory/physical-counts/${id}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getPhysicalCountByNumber(countNumber: string): Promise<PhysicalCountDetailDto> {
    const response = await axios.get(`${API_URL}/inventory/physical-counts/by-number/${countNumber}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getPhysicalCountsByWarehouse(warehouseId: string): Promise<PhysicalCountDto[]> {
    const response = await axios.get(`${API_URL}/inventory/physical-counts/by-warehouse/${warehouseId}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getInProgressPhysicalCounts(): Promise<PhysicalCountDto[]> {
    const response = await axios.get(`${API_URL}/inventory/physical-counts/in-progress`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getItemsWithVariance(countId: string): Promise<PhysicalCountItemDto[]> {
    const response = await axios.get(`${API_URL}/inventory/physical-counts/${countId}/items-with-variance`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async createPhysicalCount(data: CreatePhysicalCountDto): Promise<PhysicalCountDto> {
    const response = await axios.post(`${API_URL}/inventory/physical-counts`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async updatePhysicalCount(id: string, data: UpdatePhysicalCountDto): Promise<PhysicalCountDto> {
    const response = await axios.put(`${API_URL}/inventory/physical-counts/${id}`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async startPhysicalCount(id: string): Promise<void> {
    await axios.post(`${API_URL}/inventory/physical-counts/${id}/start`, {}, {
      headers: this.getAuthHeaders()
    });
  }

  async addCountItem(countId: string, data: AddCountItemDto): Promise<PhysicalCountItemDto> {
    const response = await axios.post(`${API_URL}/inventory/physical-counts/${countId}/items`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async removeCountItem(countId: string, itemId: string): Promise<void> {
    await axios.delete(`${API_URL}/inventory/physical-counts/${countId}/items/${itemId}`, {
      headers: this.getAuthHeaders()
    });
  }

  async recordCountItem(id: string, data: RecordCountItemDto): Promise<void> {
    await axios.post(`${API_URL}/inventory/physical-counts/${id}/record-item`, data, {
      headers: this.getAuthHeaders()
    });
  }

  async recordCountItems(id: string, items: RecordCountItemDto[]): Promise<void> {
    await axios.post(`${API_URL}/inventory/physical-counts/${id}/record-items`, items, {
      headers: this.getAuthHeaders()
    });
  }

  async completePhysicalCount(id: string): Promise<void> {
    await axios.post(`${API_URL}/inventory/physical-counts/${id}/complete`, {}, {
      headers: this.getAuthHeaders()
    });
  }

  async approveVariances(id: string): Promise<void> {
    await axios.post(`${API_URL}/inventory/physical-counts/${id}/approve-variances`, {}, {
      headers: this.getAuthHeaders()
    });
  }

  async rejectVariances(id: string, reason: string): Promise<void> {
    await axios.post(`${API_URL}/inventory/physical-counts/${id}/reject-variances`, { reason }, {
      headers: this.getAuthHeaders()
    });
  }

  async postAdjustments(id: string): Promise<void> {
    await axios.post(`${API_URL}/inventory/physical-counts/${id}/post-adjustments`, {}, {
      headers: this.getAuthHeaders()
    });
  }

  async cancelPhysicalCount(id: string, reason: string): Promise<void> {
    await axios.post(`${API_URL}/inventory/physical-counts/${id}/cancel`, { reason }, {
      headers: this.getAuthHeaders()
    });
  }

  async exportCountSheet(countId: string): Promise<PhysicalCountExportDto> {
    const response = await axios.get(`${API_URL}/inventory/physical-counts/${countId}/export`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async importCountSheet(countId: string, items: ImportCountItemDto[]): Promise<ImportCountResultDto> {
    const response = await axios.post(`${API_URL}/inventory/physical-counts/${countId}/import`, items, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getVarianceReport(countId: string): Promise<VarianceReportDto> {
    const response = await axios.get(`${API_URL}/inventory/physical-counts/${countId}/variance-report`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // ========== ITEM SUPPLIERS ==========

  async getItemSuppliersByItem(inventoryItemId: string): Promise<ItemSupplierDto[]> {
    const response = await axios.get(`${API_URL}/inventory/item-suppliers/by-item/${inventoryItemId}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getItemSuppliersBySupplier(supplierId: string): Promise<ItemSupplierDto[]> {
    const response = await axios.get(`${API_URL}/inventory/item-suppliers/by-supplier/${supplierId}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getPreferredSupplier(inventoryItemId: string): Promise<ItemSupplierDto | null> {
    try {
      const response = await axios.get(`${API_URL}/inventory/item-suppliers/preferred/${inventoryItemId}`, {
        headers: this.getAuthHeaders()
      });
      return response.data;
    } catch (error: any) {
      if (error.response?.status === 404) return null;
      throw error;
    }
  }

  async createItemSupplier(data: CreateItemSupplierDto): Promise<ItemSupplierDto> {
    const response = await axios.post(`${API_URL}/inventory/item-suppliers`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async updateItemSupplier(id: string, data: UpdateItemSupplierDto): Promise<ItemSupplierDto> {
    const response = await axios.put(`${API_URL}/inventory/item-suppliers/${id}`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async setPreferredSupplier(id: string): Promise<void> {
    await axios.post(`${API_URL}/inventory/item-suppliers/${id}/set-preferred`, {}, {
      headers: this.getAuthHeaders()
    });
  }

  async deleteItemSupplier(id: string): Promise<void> {
    await axios.delete(`${API_URL}/inventory/item-suppliers/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  // ========== STOCK MOVEMENTS ==========

  async getStockMovements(params?: {
    inventoryItemId?: string;
    warehouseId?: string;
    movementType?: string;
    startDate?: string;
    endDate?: string;
    referenceNumber?: string;
    limit?: number;
  }): Promise<StockMovementDto[]> {
    const response = await axios.get(`${API_URL}/inventory/stock-movements`, {
      params,
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getStockMovementsByItem(inventoryItemId: string, limit: number = 50): Promise<StockMovementDto[]> {
    const response = await axios.get(`${API_URL}/inventory/stock-movements/by-item/${inventoryItemId}`, {
      params: { limit },
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getStockMovementsByWarehouse(warehouseId: string, limit: number = 50): Promise<StockMovementDto[]> {
    const response = await axios.get(`${API_URL}/inventory/stock-movements/by-warehouse/${warehouseId}`, {
      params: { limit },
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getStockMovementTypes(): Promise<string[]> {
    const response = await axios.get(`${API_URL}/inventory/stock-movements/movement-types`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // ========== INVENTORY CATEGORIES ==========

  async getInventoryCategories(activeOnly: boolean = false): Promise<InventoryCategoryDto[]> {
    const response = await axios.get(`${API_URL}/inventory/categories`, {
      params: { activeOnly },
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getActiveInventoryCategories(): Promise<InventoryCategoryDto[]> {
    const response = await axios.get(`${API_URL}/inventory/categories/active`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }
}

// Export singleton instance
export const inventoryManagementService = new InventoryManagementService();
