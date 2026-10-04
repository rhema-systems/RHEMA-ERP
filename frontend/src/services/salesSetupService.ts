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
  estimatedValue?: number | null;
  currency?: string;
  areaSquareMeters?: number | null;
  propertyReference?: string;
  canCreateSalesOrder: boolean;
  salesOrderIneligibilityReason?: string;
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
  currentQuantity?: number | null;
  availableQuantity?: number | null;
  allocatedQuantity?: number | null;
  shouldCreateSalesAllocation: boolean;
  activeAllocationId?: string;
  activeAllocationStatus?: string;
  activeAllocationReservedUntil?: string;
  activeAllocationBusinessPartnerId?: string;
  activeAllocationOpportunityId?: string;
  activeAllocationSalesOrderId?: string;
  activeAllocationCustomerName?: string;
  hasActiveAllocation: boolean;
}

export interface SalesSaleableSourceFilterDefinitionDto {
  field: string;
  displayName: string;
  valueType: 'text' | 'boolean' | 'select' | string;
  isRequired: boolean;
  defaultValue?: string;
  helpText?: string;
  options: string[];
}

export interface SalesSaleableSourceAdapterDefinitionDto {
  adapterKey: string;
  filters: SalesSaleableSourceFilterDefinitionDto[];
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

  getSaleableSourceAdapters() {
    return apiService.get<SalesSaleableSourceAdapterDefinitionDto[]>('/sales/setup/saleable-source-adapters');
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
