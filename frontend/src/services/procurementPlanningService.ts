/**
 * Procurement Planning Service
 * API service for Procurement Planning module
 */

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

// Helper function to get auth headers
const getAuthHeaders = () => {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token && { 'Authorization': `Bearer ${token}` })
  };
};

// ============================================================================
// COMMON INTERFACES
// ============================================================================

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// ============================================================================
// DEPARTMENT INTERFACE (for dropdown selection)
// ============================================================================

export interface DepartmentDto {
  id: string;
  name: string;
  code: string;
  description?: string;
  departmentType: number;
  parentDepartmentId?: string;
  parentDepartmentName?: string;
  departmentHeadId?: string;
  departmentHeadName?: string;
  budget?: number;
  isActive: boolean;
  color?: string;
  icon?: string;
  employeeCount: number;
  sectionCount: number;
}

// ============================================================================
// PROCUREMENT PLAN INTERFACES
// ============================================================================

export interface ProcurementPlanDto {
  id: string;
  planNumber: string;
  title: string;
  description?: string;
  departmentId: string;
  departmentName?: string;
  fiscalYear: number;
  planStartDate: string;
  planEndDate: string;
  planDurationYears: number;
  status: string;
  totalEstimatedBudget: number;
  approvedBudget: number;
  currency: string;
  preparedByName?: string;
  preparedDate?: string;
  approvedByName?: string;
  approvedDate?: string;
  revisionNumber: number;
  itemCount: number;
  createdAt: string;
}

export interface ProcurementPlanDetailDto extends ProcurementPlanDto {
  preparedById?: string;
  reviewedById?: string;
  reviewedByName?: string;
  reviewedDate?: string;
  reviewComments?: string;
  approvedById?: string;
  approvalComments?: string;
  previousVersionId?: string;
  notes?: string;
  items: ProcurementPlanItemDto[];
  budgets: ProcurementBudgetDto[];
  schedules: ProcurementScheduleDto[];
}

export interface CreateProcurementPlanDto {
  title: string;
  description?: string;
  departmentId: string;
  fiscalYear: number;
  planStartDate: string;
  planEndDate: string;
  planDurationYears?: number;
  totalEstimatedBudget?: number;
  currency?: string;
  notes?: string;
  items?: CreateProcurementPlanItemDto[];
}

export interface UpdateProcurementPlanDto {
  title: string;
  description?: string;
  departmentId: string;
  fiscalYear: number;
  planStartDate: string;
  planEndDate: string;
  planDurationYears?: number;
  totalEstimatedBudget?: number;
  currency?: string;
  notes?: string;
}

export interface SubmitProcurementPlanDto {
  reviewerId?: string;
  comments?: string;
}

export interface ApproveProcurementPlanDto {
  isApproved: boolean;
  approvedBudget?: number;
  comments?: string;
  autoGenerateSchedules?: boolean;
  /** Optional: Specific budget ID to link. If null, system auto-matches by department + fiscal year */
  budgetId?: string;
  /** If true, automatically links to matching budget on approval */
  autoLinkBudget?: boolean;
}

// ============================================================================
// PLAN ITEM CONVERSION INTERFACES
// ============================================================================

export interface ConvertPlanItemToTenderDto {
  planItemId: string;
  tenderTitle: string;
  tenderDescription?: string;
  tenderType: string;
  submissionDeadline?: string;
  openingDate?: string;
  notes?: string;
  createSchedule?: boolean;
}

export interface ConvertPlanItemToPurchaseOrderDto {
  planItemId: string;
  supplierId: string;
  requiredDate?: string;
  paymentTerms?: string;
  shippingTerms?: string;
  deliveryWarehouseId?: string;
  deliveryAddress?: string;
  deliveryInstructions?: string;
  notes?: string;
  createSchedule?: boolean;
}

export interface PlanItemConversionResultDto {
  planItemId: string;
  planItemDescription: string;
  conversionType: string;
  tenderId?: string;
  tenderNumber?: string;
  purchaseOrderId?: string;
  purchaseOrderNumber?: string;
  scheduleId?: string;
  scheduleCode?: string;
  newItemStatus: string;
  message: string;
}

export interface BudgetValidationResultDto {
  isValid: boolean;
  hasBudget: boolean;
  budgetCode?: string;
  allocatedAmount: number;
  utilizedAmount: number;
  committedAmount: number;
  remainingAmount: number;
  requestedAmount: number;
  currency?: string;
  controlLevel?: string;
  message?: string;
  warnings: string[];
}

// ============================================================================
// PROCUREMENT PLAN ITEM INTERFACES
// ============================================================================

export interface ProcurementPlanItemDto {
  id: string;
  procurementPlanId: string;
  inventoryItemId?: string;
  inventoryItemCode?: string;
  inventoryItemName?: string;
  itemDescription: string;
  specifications?: string;
  itemCategory?: string;
  estimatedQuantity: number;
  unitOfMeasure: string;
  estimatedUnitPrice: number;
  estimatedTotalCost: number;
  currency: string;
  priority: string;
  isCritical: boolean;
  requiredDate?: string;
  plannedProcurementMonth?: number;
  plannedQuarter?: string;
  preferredSupplierId?: string;
  preferredSupplierName?: string;
  alternativeSuppliers?: string;
  justification?: string;
  status: string;
  procurementMethod?: string;
  purchaseOrderId?: string;
  tenderId?: string;
  notes?: string;
  itemSuppliers: ProcurementPlanItemSupplierDto[];
}

export interface ProcurementPlanItemSupplierDto {
  id: string;
  procurementPlanItemId: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  isPreferred: boolean;
  priority: number;
  quotedUnitPrice?: number;
  leadTimeDays?: number;
  supplierItemCode?: string;
  notes?: string;
}

export interface CreateProcurementPlanItemSupplierDto {
  supplierId: string;
  isPreferred?: boolean;
  priority?: number;
  quotedUnitPrice?: number;
  leadTimeDays?: number;
  supplierItemCode?: string;
  notes?: string;
}

export interface CreateProcurementPlanItemDto {
  inventoryItemId?: string;
  itemDescription: string;
  specifications?: string;
  itemCategory?: string;
  estimatedQuantity: number;
  unitOfMeasure?: string;
  estimatedUnitPrice?: number;
  priority?: string;
  isCritical?: boolean;
  requiredDate?: string;
  plannedProcurementMonth?: number;
  plannedQuarter?: string;
  preferredSupplierId?: string;
  preferredSupplierName?: string;
  alternativeSuppliers?: string;
  justification?: string;
  procurementMethod?: string;
  notes?: string;
  itemSuppliers?: CreateProcurementPlanItemSupplierDto[];
}

export interface UpdateProcurementPlanItemDto extends CreateProcurementPlanItemDto {
  id: string;
}

// ============================================================================
// PROCUREMENT BUDGET INTERFACES
// ============================================================================

export interface ProcurementBudgetDto {
  id: string;
  budgetCode: string;
  title: string;
  description?: string;
  departmentId: string;
  departmentName?: string;
  procurementPlanId?: string;
  fiscalYear: number;
  allocatedAmount: number;
  utilizedAmount: number;
  committedAmount: number;
  remainingAmount: number;
  currency: string;
  status: string;
  controlLevel: string;
  warningThresholdPercent: number;
  effectiveDate?: string;
  expiryDate?: string;
  approvedByName?: string;
  approvedDate?: string;
  utilizationPercent: number;
  createdAt: string;
}

export interface ProcurementBudgetDetailDto extends ProcurementBudgetDto {
  approvedById?: string;
  notes?: string;
  allocations: ProcurementBudgetAllocationDto[];
  revisions: ProcurementBudgetRevisionDto[];
}

export interface CreateProcurementBudgetDto {
  title: string;
  description?: string;
  departmentId: string;
  procurementPlanId?: string;
  fiscalYear: number;
  allocatedAmount: number;
  currency?: string;
  controlLevel?: string;
  warningThresholdPercent?: number;
  effectiveDate?: string;
  expiryDate?: string;
  notes?: string;
  allocations?: CreateProcurementBudgetAllocationDto[];
}

export interface ProcurementBudgetAllocationDto {
  id: string;
  procurementBudgetId: string;
  categoryName: string;
  categoryDescription?: string;
  allocatedAmount: number;
  utilizedAmount: number;
  remainingAmount: number;
  utilizationPercent: number;
  notes?: string;
}

export interface CreateProcurementBudgetAllocationDto {
  categoryName: string;
  categoryDescription?: string;
  allocatedAmount: number;
  notes?: string;
}

export interface ProcurementBudgetRevisionDto {
  id: string;
  procurementBudgetId: string;
  revisionNumber: number;
  revisionType: string;
  previousAmount: number;
  newAmount: number;
  changeAmount: number;
  reason?: string;
  approvedByName?: string;
  approvedDate?: string;
  status: string;
  createdAt: string;
}

export interface CreateProcurementBudgetRevisionDto {
  revisionType: string;
  newAmount: number;
  reason?: string;
}

// ============================================================================
// PROCUREMENT SCHEDULE INTERFACES
// ============================================================================

export interface ProcurementScheduleDto {
  id: string;
  scheduleCode: string;
  title: string;
  description?: string;
  procurementPlanId?: string;
  procurementPlanNumber?: string;
  procurementPlanItemId?: string;
  departmentId?: string;
  departmentName?: string;
  scheduleType: string;
  plannedStartDate: string;
  plannedEndDate: string;
  actualStartDate?: string;
  actualEndDate?: string;
  isOptimalTiming: boolean;
  timingRationale?: string;
  considerSeasonalPricing: boolean;
  seasonalNotes?: string;
  considerCashFlow: boolean;
  cashFlowNotes?: string;
  status: string;
  storageLimitations?: string;
  consolidationOpportunity: boolean;
  consolidationNotes?: string;
  notes?: string;
  createdAt: string;
}

export interface ProcurementScheduleDetailDto extends ProcurementScheduleDto {
  seasonalNotes?: string;
  cashFlowNotes?: string;
  storageLimitations?: string;
  consolidationNotes?: string;
  notes?: string;
}

export interface CreateProcurementScheduleDto {
  title: string;
  description?: string;
  procurementPlanId?: string;
  procurementPlanItemId?: string;
  departmentId?: string;
  scheduleType?: string;
  plannedStartDate: string;
  plannedEndDate: string;
  isOptimalTiming?: boolean;
  timingRationale?: string;
  considerSeasonalPricing?: boolean;
  seasonalNotes?: string;
  considerCashFlow?: boolean;
  cashFlowNotes?: string;
  storageLimitations?: string;
  consolidationOpportunity?: boolean;
  consolidationNotes?: string;
  notes?: string;
}

// ============================================================================
// MARKET ANALYSIS INTERFACES
// ============================================================================

export interface MarketAnalysisDto {
  id: string;
  analysisCode: string;
  analysisNumber?: string;
  title: string;
  description?: string;
  itemCategory?: string;
  itemDescription?: string;
  itemName?: string;
  analysisPeriodStart: string;
  analysisPeriodEnd: string;
  historicalAveragePrice: number;
  averagePrice?: number;
  currentMarketPrice: number;
  minimumPrice?: number;
  maximumPrice?: number;
  forecastedPrice: number;
  priceTrend: string;
  priceChangePercent: number;
  currency: string;
  marketRiskLevel: string;
  riskFactors?: string;
  opportunities?: string;
  recommendedStrategy?: string;
  strategyRationale?: string;
  optimalPurchaseMonth?: number;
  seasonalPattern?: string;
  preparedByName?: string;
  preparedDate?: string;
  analysisDate?: string;
  status: string;
  createdAt: string;
}

export interface MarketAnalysisDetailDto extends MarketAnalysisDto {
  preparedById?: string;
  notes?: string;
  priceHistories: PriceHistoryDto[];
}

export interface CreateMarketAnalysisDto {
  title: string;
  description?: string;
  itemCategory?: string;
  itemDescription?: string;
  analysisPeriodStart: string;
  analysisPeriodEnd: string;
  historicalAveragePrice?: number;
  currentMarketPrice?: number;
  forecastedPrice?: number;
  priceTrend?: string;
  priceChangePercent?: number;
  currency?: string;
  marketRiskLevel?: string;
  riskFactors?: string;
  opportunities?: string;
  recommendedStrategy?: string;
  strategyRationale?: string;
  optimalPurchaseMonth?: number;
  seasonalPattern?: string;
  notes?: string;
}

export interface PriceHistoryDto {
  id: string;
  marketAnalysisId?: string;
  itemCategory?: string;
  itemDescription?: string;
  supplierId?: string;
  supplierName?: string;
  priceDate: string;
  unitPrice: number;
  currency: string;
  unitOfMeasure: string;
  priceSource: string;
  purchaseOrderId?: string;
  tenderId?: string;
  notes?: string;
}

export interface CreatePriceHistoryDto {
  marketAnalysisId?: string;
  itemCategory?: string;
  itemDescription?: string;
  supplierId?: string;
  supplierName?: string;
  priceDate: string;
  unitPrice: number;
  currency?: string;
  unitOfMeasure?: string;
  priceSource?: string;
  purchaseOrderId?: string;
  tenderId?: string;
  notes?: string;
}

// ============================================================================
// SUPPLIER CONSOLIDATION INTERFACES
// ============================================================================

export interface SupplierConsolidationDto {
  id: string;
  consolidationCode: string;
  consolidationNumber?: string;
  title: string;
  description?: string;
  itemCategory?: string;
  analysisPeriodStart: string;
  analysisPeriodEnd: string;
  currentSupplierCount: number;
  targetSupplierCount?: number;
  recommendedSupplierCount: number;
  consolidationStrategy?: string;
  currentAnnualSpend?: number;
  totalSpend: number;
  projectedSavings?: number;
  potentialSavings: number;
  currency: string;
  opportunityLevel: string;
  recommendedStrategy: string;
  strategyRationale?: string;
  preparedByName?: string;
  preparedDate?: string;
  status: string;
  createdAt: string;
}

export interface SupplierConsolidationDetailDto extends SupplierConsolidationDto {
  preparedById?: string;
  preferredSupplierIds?: string;
  suppliersToPhaseOut?: string;
  implementationPlan?: string;
  notes?: string;
}

export interface CreateSupplierConsolidationDto {
  title: string;
  description?: string;
  itemCategory?: string;
  analysisPeriodStart: string;
  analysisPeriodEnd: string;
  currentSupplierCount?: number;
  recommendedSupplierCount?: number;
  totalSpend?: number;
  potentialSavings?: number;
  currency?: string;
  opportunityLevel?: string;
  recommendedStrategy?: string;
  strategyRationale?: string;
  preferredSupplierIds?: string;
  suppliersToPhaseOut?: string;
  implementationPlan?: string;
  notes?: string;
}

// ============================================================================
// EMERGENCY PROCUREMENT PLAN INTERFACES
// ============================================================================

export interface EmergencyProcurementPlanDto {
  id: string;
  planCode: string;
  planNumber?: string;
  title: string;
  description?: string;
  departmentId?: string;
  departmentName?: string;
  emergencyType: string;
  criticalityLevel: string;
  emergencyBudgetReserve?: number;
  budgetReserve: number;
  utilizedReserve: number;
  remainingReserve: number;
  currency: string;
  maxApprovalLimit: number;
  validFrom?: string;
  effectiveDate?: string;
  validTo?: string;
  expiryDate?: string;
  lastReviewDate?: string;
  nextReviewDate?: string;
  approvedByName?: string;
  approvedDate?: string;
  status: string;
  criticalItemCount: number;
  emergencySupplierCount: number;
  createdAt: string;
}

export interface EmergencyProcurementPlanDetailDto extends EmergencyProcurementPlanDto {
  approvedById?: string;
  rapidProcurementProcess?: string;
  escalationContacts?: string;
  notes?: string;
  criticalItems: EmergencyProcurementItemDto[];
  emergencySuppliers: EmergencySupplierDto[];
}

export interface CreateEmergencyProcurementPlanDto {
  title: string;
  description?: string;
  departmentId?: string;
  emergencyType?: string;
  criticalityLevel?: string;
  budgetReserve?: number;
  currency?: string;
  maxApprovalLimit?: number;
  rapidProcurementProcess?: string;
  escalationContacts?: string;
  effectiveDate?: string;
  expiryDate?: string;
  nextReviewDate?: string;
  notes?: string;
  criticalItems?: CreateEmergencyProcurementItemDto[];
  emergencySuppliers?: CreateEmergencySupplierDto[];
}

export interface EmergencyProcurementItemDto {
  id: string;
  emergencyProcurementPlanId: string;
  itemDescription: string;
  specifications?: string;
  itemCategory?: string;
  minimumStockLevel: number;
  currentStockLevel: number;
  emergencyOrderQuantity: number;
  unitOfMeasure: string;
  maxLeadTimeDays: number;
  criticalityLevel: string;
  alternativeItems?: string;
  notes?: string;
  isStockLow: boolean;
}

export interface CreateEmergencyProcurementItemDto {
  itemDescription: string;
  specifications?: string;
  itemCategory?: string;
  minimumStockLevel?: number;
  currentStockLevel?: number;
  emergencyOrderQuantity?: number;
  unitOfMeasure?: string;
  maxLeadTimeDays?: number;
  criticalityLevel?: string;
  alternativeItems?: string;
  notes?: string;
}

export interface EmergencySupplierDto {
  id: string;
  emergencyProcurementPlanId: string;
  supplierId?: string;
  supplierName: string;
  contactPerson?: string;
  contactPhone?: string;
  contactEmail?: string;
  address?: string;
  itemsProvided?: string;
  responseTimeHours: number;
  priority: number;
  hasEmergencyContract: boolean;
  contractExpiryDate?: string;
  paymentTerms?: string;
  lastVerifiedDate?: string;
  isActive: boolean;
  notes?: string;
}

export interface CreateEmergencySupplierDto {
  supplierId?: string;
  supplierName: string;
  contactPerson?: string;
  contactPhone?: string;
  contactEmail?: string;
  address?: string;
  itemsProvided?: string;
  responseTimeHours?: number;
  priority?: number;
  hasEmergencyContract?: boolean;
  contractExpiryDate?: string;
  paymentTerms?: string;
  notes?: string;
}

// ============================================================================
// PROCUREMENT PLAN SERVICE
// ============================================================================

export const procurementPlanService = {
  async getPlans(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    status?: string;
    departmentId?: string;
    fiscalYear?: number;
  }): Promise<PagedResult<ProcurementPlanDto>> {
    const queryParams = new URLSearchParams();
    if (params?.page) queryParams.append('page', params.page.toString());
    if (params?.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params?.search) queryParams.append('search', params.search);
    if (params?.status) queryParams.append('status', params.status);
    if (params?.departmentId) queryParams.append('departmentId', params.departmentId);
    if (params?.fiscalYear) queryParams.append('fiscalYear', params.fiscalYear.toString());

    const response = await fetch(`${API_BASE_URL}/procurement/procurementplans?${queryParams}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch procurement plans');
    return response.json();
  },

  async getPlanById(id: string): Promise<ProcurementPlanDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementplans/${id}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch procurement plan');
    return response.json();
  },

  async createPlan(data: CreateProcurementPlanDto): Promise<ProcurementPlanDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementplans`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error('Failed to create procurement plan');
    return response.json();
  },

  async updatePlan(id: string, data: UpdateProcurementPlanDto): Promise<ProcurementPlanDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementplans/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error('Failed to update procurement plan');
    return response.json();
  },

  async deletePlan(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementplans/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to delete procurement plan');
  },

  async submitForApproval(id: string, data: SubmitProcurementPlanDto): Promise<ProcurementPlanDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementplans/${id}/submit`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error('Failed to submit procurement plan');
    return response.json();
  },

  async approvePlan(id: string, data: ApproveProcurementPlanDto): Promise<ProcurementPlanDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementplans/${id}/approve`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error('Failed to approve/reject procurement plan');
    return response.json();
  },

  async addItem(planId: string, data: CreateProcurementPlanItemDto): Promise<ProcurementPlanItemDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementplans/${planId}/items`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error('Failed to add item');
    return response.json();
  },

  async removeItem(planId: string, itemId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementplans/items/${itemId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to remove item');
  },

  async updateItem(itemId: string, dto: UpdateProcurementPlanItemDto): Promise<ProcurementPlanItemDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementplans/items/${itemId}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error('Failed to update item');
    return response.json();
  },

  async convertItemToTender(dto: ConvertPlanItemToTenderDto): Promise<PlanItemConversionResultDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementplans/items/convert-to-tender`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to convert plan item to tender');
    }
    return response.json();
  },

  async convertItemToPurchaseOrder(dto: ConvertPlanItemToPurchaseOrderDto): Promise<PlanItemConversionResultDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementplans/items/convert-to-purchase-order`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to convert plan item to purchase order');
    }
    return response.json();
  },

  async convertItemToRfq(dto: ConvertPlanItemToTenderDto): Promise<PlanItemConversionResultDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementplans/items/convert-to-rfq`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to convert plan item to RFQ');
    }
    return response.json();
  },

  async updateItemStatus(itemId: string, status: string): Promise<ProcurementPlanItemDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementplans/items/${itemId}/status`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify({ status }),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update item status');
    }
    return response.json();
  },

  async validateBudgetForItem(itemId: string, amount?: number): Promise<BudgetValidationResultDto> {
    const queryParams = amount ? `?amount=${amount}` : '';
    const response = await fetch(`${API_BASE_URL}/procurement/procurementplans/items/${itemId}/validate-budget${queryParams}`, {
      method: 'GET',
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to validate budget');
    }
    return response.json();
  },
};

// ============================================================================
// PROCUREMENT BUDGET SERVICE
// ============================================================================

export const procurementBudgetService = {
  async getBudgets(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    status?: string;
    departmentId?: string;
    fiscalYear?: number;
  }): Promise<PagedResult<ProcurementBudgetDto>> {
    const queryParams = new URLSearchParams();
    if (params?.page) queryParams.append('page', params.page.toString());
    if (params?.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params?.search) queryParams.append('search', params.search);
    if (params?.status) queryParams.append('status', params.status);
    if (params?.departmentId) queryParams.append('departmentId', params.departmentId);
    if (params?.fiscalYear) queryParams.append('fiscalYear', params.fiscalYear.toString());

    const response = await fetch(`${API_BASE_URL}/procurement/procurementbudgets?${queryParams}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch procurement budgets');
    return response.json();
  },

  async getBudgetById(id: string): Promise<ProcurementBudgetDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementbudgets/${id}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch procurement budget');
    return response.json();
  },

  async createBudget(data: CreateProcurementBudgetDto): Promise<ProcurementBudgetDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementbudgets`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error('Failed to create procurement budget');
    return response.json();
  },

  async updateBudget(id: string, data: CreateProcurementBudgetDto): Promise<ProcurementBudgetDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementbudgets/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error('Failed to update procurement budget');
    return response.json();
  },

  async deleteBudget(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementbudgets/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to delete procurement budget');
  },

  async approveBudget(id: string): Promise<ProcurementBudgetDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementbudgets/${id}/approve`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to approve budget');
    return response.json();
  },

  async getAvailableBudgetsForLinking(departmentId: string, fiscalYear: number): Promise<ProcurementBudgetDto[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementbudgets/available-for-linking?departmentId=${departmentId}&fiscalYear=${fiscalYear}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to get available budgets');
    return response.json();
  },

  async createRevision(budgetId: string, data: CreateProcurementBudgetRevisionDto): Promise<ProcurementBudgetRevisionDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementbudgets/${budgetId}/revisions`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error('Failed to create budget revision');
    return response.json();
  },
};

// ============================================================================
// PROCUREMENT SCHEDULE SERVICE
// ============================================================================

export const procurementScheduleService = {
  async getSchedules(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    status?: string;
    departmentId?: string;
    procurementPlanId?: string;
  }): Promise<PagedResult<ProcurementScheduleDto>> {
    const queryParams = new URLSearchParams();
    if (params?.page) queryParams.append('page', params.page.toString());
    if (params?.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params?.search) queryParams.append('search', params.search);
    if (params?.status) queryParams.append('status', params.status);
    if (params?.departmentId) queryParams.append('departmentId', params.departmentId);
    if (params?.procurementPlanId) queryParams.append('procurementPlanId', params.procurementPlanId);

    const response = await fetch(`${API_BASE_URL}/procurement/procurementschedules?${queryParams}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch procurement schedules');
    return response.json();
  },

  async getScheduleById(id: string): Promise<ProcurementScheduleDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementschedules/${id}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch procurement schedule');
    return response.json();
  },

  async createSchedule(data: CreateProcurementScheduleDto): Promise<ProcurementScheduleDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementschedules`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error('Failed to create procurement schedule');
    return response.json();
  },

  async updateSchedule(id: string, data: CreateProcurementScheduleDto): Promise<ProcurementScheduleDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementschedules/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error('Failed to update procurement schedule');
    return response.json();
  },

  async deleteSchedule(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementschedules/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to delete procurement schedule');
  },

  async startSchedule(id: string): Promise<ProcurementScheduleDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementschedules/${id}/start`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to start schedule');
    return response.json();
  },

  async completeSchedule(id: string): Promise<ProcurementScheduleDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/procurementschedules/${id}/complete`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to complete schedule');
    return response.json();
  },
};

// ============================================================================
// MARKET ANALYSIS SERVICE
// ============================================================================

export const marketAnalysisService = {
  async getAnalyses(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    status?: string;
    itemCategory?: string;
  }): Promise<PagedResult<MarketAnalysisDto>> {
    const queryParams = new URLSearchParams();
    if (params?.page) queryParams.append('page', params.page.toString());
    if (params?.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params?.search) queryParams.append('search', params.search);
    if (params?.status) queryParams.append('status', params.status);
    if (params?.itemCategory) queryParams.append('itemCategory', params.itemCategory);

    const response = await fetch(`${API_BASE_URL}/procurement/marketanalyses?${queryParams}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch market analyses');
    return response.json();
  },

  async getAnalysisById(id: string): Promise<MarketAnalysisDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/marketanalyses/${id}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch market analysis');
    return response.json();
  },

  async createAnalysis(data: CreateMarketAnalysisDto): Promise<MarketAnalysisDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/marketanalyses`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error('Failed to create market analysis');
    return response.json();
  },

  async updateAnalysis(id: string, data: CreateMarketAnalysisDto): Promise<MarketAnalysisDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/marketanalyses/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error('Failed to update market analysis');
    return response.json();
  },

  async deleteAnalysis(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/marketanalyses/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to delete market analysis');
  },

  async addPriceHistory(analysisId: string, data: CreatePriceHistoryDto): Promise<PriceHistoryDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/marketanalyses/${analysisId}/price-history`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error('Failed to add price history');
    return response.json();
  },
};

// ============================================================================
// SUPPLIER CONSOLIDATION SERVICE
// ============================================================================

export const supplierConsolidationService = {
  async getConsolidations(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    status?: string;
    itemCategory?: string;
  }): Promise<PagedResult<SupplierConsolidationDto>> {
    const queryParams = new URLSearchParams();
    if (params?.page) queryParams.append('page', params.page.toString());
    if (params?.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params?.search) queryParams.append('search', params.search);
    if (params?.status) queryParams.append('status', params.status);
    if (params?.itemCategory) queryParams.append('itemCategory', params.itemCategory);

    const response = await fetch(`${API_BASE_URL}/procurement/supplierconsolidations?${queryParams}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch supplier consolidations');
    return response.json();
  },

  async getConsolidationById(id: string): Promise<SupplierConsolidationDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/supplierconsolidations/${id}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch supplier consolidation');
    return response.json();
  },

  async createConsolidation(data: CreateSupplierConsolidationDto): Promise<SupplierConsolidationDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/supplierconsolidations`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error('Failed to create supplier consolidation');
    return response.json();
  },

  async updateConsolidation(id: string, data: CreateSupplierConsolidationDto): Promise<SupplierConsolidationDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/supplierconsolidations/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error('Failed to update supplier consolidation');
    return response.json();
  },

  async deleteConsolidation(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/supplierconsolidations/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to delete supplier consolidation');
  },

  async approveConsolidation(id: string): Promise<SupplierConsolidationDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/supplierconsolidations/${id}/approve`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to approve consolidation');
    return response.json();
  },
};

// ============================================================================
// EMERGENCY PROCUREMENT PLAN SERVICE
// ============================================================================

export const emergencyProcurementPlanService = {
  async getPlans(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    status?: string;
    emergencyType?: string;
    departmentId?: string;
  }): Promise<PagedResult<EmergencyProcurementPlanDto>> {
    const queryParams = new URLSearchParams();
    if (params?.page) queryParams.append('page', params.page.toString());
    if (params?.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params?.search) queryParams.append('search', params.search);
    if (params?.status) queryParams.append('status', params.status);
    if (params?.emergencyType) queryParams.append('emergencyType', params.emergencyType);
    if (params?.departmentId) queryParams.append('departmentId', params.departmentId);

    const response = await fetch(`${API_BASE_URL}/procurement/emergencyprocurementplans?${queryParams}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch emergency procurement plans');
    return response.json();
  },

  async getPlanById(id: string): Promise<EmergencyProcurementPlanDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/emergencyprocurementplans/${id}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch emergency procurement plan');
    return response.json();
  },

  async createPlan(data: CreateEmergencyProcurementPlanDto): Promise<EmergencyProcurementPlanDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/emergencyprocurementplans`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error('Failed to create emergency procurement plan');
    return response.json();
  },

  async updatePlan(id: string, data: CreateEmergencyProcurementPlanDto): Promise<EmergencyProcurementPlanDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/emergencyprocurementplans/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error('Failed to update emergency procurement plan');
    return response.json();
  },

  async deletePlan(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/emergencyprocurementplans/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to delete emergency procurement plan');
  },

  async activatePlan(id: string): Promise<EmergencyProcurementPlanDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/emergencyprocurementplans/${id}/activate`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to activate plan');
    return response.json();
  },

  async deactivatePlan(id: string): Promise<EmergencyProcurementPlanDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/emergencyprocurementplans/${id}/deactivate`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to deactivate plan');
    return response.json();
  },

  async addCriticalItem(planId: string, data: CreateEmergencyProcurementItemDto): Promise<EmergencyProcurementItemDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/emergencyprocurementplans/${planId}/items`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error('Failed to add critical item');
    return response.json();
  },

  async removeCriticalItem(planId: string, itemId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/emergencyprocurementplans/${planId}/items/${itemId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to remove critical item');
  },

  async addEmergencySupplier(planId: string, data: CreateEmergencySupplierDto): Promise<EmergencySupplierDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/emergencyprocurementplans/${planId}/suppliers`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error('Failed to add emergency supplier');
    return response.json();
  },

  async removeEmergencySupplier(planId: string, supplierId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/emergencyprocurementplans/${planId}/suppliers/${supplierId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to remove emergency supplier');
  },
};

// ============================================================================
// COMMON HELPER SERVICES
// ============================================================================

export interface InventoryItemDto {
  id: string;
  itemCode: string;
  name: string;
  description?: string;
  unitOfMeasure: string;
  currentStock: number;
  availableStock: number;
  standardCost: number;
  averageCost: number;
  isSerialTracked: boolean;
  isLotTracked: boolean;
  itemType: number;
  status: number;
  categoryName: string;
}

export const commonService = {
  async getDepartments(): Promise<DepartmentDto[]> {
    const response = await fetch(`${API_BASE_URL}/employees/departments`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch departments');
    return response.json();
  },

  async getInventoryItems(): Promise<InventoryItemDto[]> {
    try {
      const response = await fetch(`${API_BASE_URL}/inventoryitems`, {
        headers: getAuthHeaders(),
      });
      if (!response.ok) {
        console.error('Failed to fetch inventory items:', response.status, response.statusText);
        return [];
      }
      const data = await response.json();
      return data || [];
    } catch (error) {
      console.error('Error fetching inventory items:', error);
      return [];
    }
  },
};
