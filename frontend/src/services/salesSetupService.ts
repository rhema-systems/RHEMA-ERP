import { apiService } from './api.service';

export interface SalesSaleableSourceDto {
  id: string;
  tenantId: string;
  code: string;
  displayName: string;
  description?: string;
  sourceType: string;
  adapterKey: string;
  isActive: boolean;
  icon: string;
  colorCode: string;
  sortOrder: number;
  supportedTransactionTypes: string;
  defaultCurrency: string;
  defaultWorkflowEntityType?: string;
  allowSalesOrders: boolean;
  allowSalesAgreements: boolean;
  allowReservations: boolean;
  requiresExternalModule: boolean;
  isSystemSource: boolean;
  settingsJson?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface SalesSaleableItemDto {
  sourceId: string;
  sourceCode: string;
  sourceType: string;
  adapterKey: string;
  sourceItemId: string;
  itemCode?: string;
  itemName: string;
  itemType?: string;
  status?: string;
  commercialStatus?: string;
  customerId?: string;
  customerName?: string;
  estimatedValue?: number;
  currency?: string;
  areaSquareMeters?: number;
  propertyReference?: string;
  canCreateSalesOrder: boolean;
  canCreateSalesAgreement: boolean;
  canCreateLeaseAgreement: boolean;
  suggestedOrderType?: string;
  suggestedAgreementType?: string;
  suggestedLeaseAgreementType?: string;
  projectId?: string;
  projectCode?: string;
  projectTitle?: string;
  projectUnitId?: string;
  projectUnitCode?: string;
  projectUnitName?: string;
  handoverStatus?: string;
  inventoryItemId?: string;
  warehouseId?: string;
  warehouseName?: string;
  locationId?: string;
  locationName?: string;
  unitOfMeasure?: string;
  currentQuantity?: number;
  availableQuantity?: number;
  allocatedQuantity?: number;
  shouldCreateSalesAllocation: boolean;
  activeAllocationId?: string;
  activeAllocationStatus?: string;
  activeAllocationReservedUntil?: string;
  activeAllocationCustomerName?: string;
  hasActiveAllocation: boolean;
}

export interface UpsertSalesSaleableSourceDto {
  code: string;
  displayName: string;
  description?: string;
  sourceType: string;
  adapterKey: string;
  isActive: boolean;
  icon: string;
  colorCode: string;
  sortOrder: number;
  supportedTransactionTypes: string;
  defaultCurrency: string;
  defaultWorkflowEntityType?: string;
  allowSalesOrders: boolean;
  allowSalesAgreements: boolean;
  allowReservations: boolean;
  requiresExternalModule: boolean;
  settingsJson?: string;
}

const endpoint = '/sales/setup/saleable-sources';

export const salesSetupService = {
  getSaleableSources(includeInactive = false) {
    return apiService.get<SalesSaleableSourceDto[]>(endpoint, { includeInactive });
  },

  searchSaleableItems(sourceId: string, search?: string, take = 50) {
    return apiService.get<SalesSaleableItemDto[]>(`${endpoint}/${sourceId}/items`, { search, take });
  },

  createSaleableSource(dto: UpsertSalesSaleableSourceDto) {
    return apiService.post<SalesSaleableSourceDto>(endpoint, dto);
  },

  updateSaleableSource(id: string, dto: UpsertSalesSaleableSourceDto) {
    return apiService.put<SalesSaleableSourceDto>(`${endpoint}/${id}`, dto);
  },

  activateSaleableSource(id: string) {
    return apiService.post<SalesSaleableSourceDto>(`${endpoint}/${id}/activate`);
  },

  deactivateSaleableSource(id: string) {
    return apiService.post<SalesSaleableSourceDto>(`${endpoint}/${id}/deactivate`);
  },

  deleteSaleableSource(id: string) {
    return apiService.delete<void>(`${endpoint}/${id}`);
  },

  seedDefaultSaleableSources() {
    return apiService.post<void>(`${endpoint}/seed-defaults`);
  },
};
