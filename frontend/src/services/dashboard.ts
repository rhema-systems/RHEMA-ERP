import { apiService } from './api.service'
import type { ProjectDashboardDto } from './projectService'
import type { FinanceDashboardData } from '../types/finance-dashboard'

export interface DashboardModuleStatus {
  module: string
  available: boolean
  accessRestricted?: boolean
  error?: string
}

export interface DashboardCurrencyReference {
  currencyCode: string
  currencyName: string
  currencySymbol: string
  decimalPlaces: number
}

export function getUnavailableDashboardModules(statuses: readonly DashboardModuleStatus[]): DashboardModuleStatus[] {
  // Restricted analytics stay absent; an access decision is not a service outage.
  return statuses.filter(status => !status.available && !status.accessRestricted)
}

export interface MaintenanceDashboardOverviewSummary {
  totalAssets?: number
  totalWorkOrders?: number
  activeWorkOrders?: number
  overdueWorkOrders?: number
  availableTechnicians?: number
  completedWorkOrders?: number
}

export interface MaintenanceDashboardOverview {
  summary?: MaintenanceDashboardOverviewSummary
  workOrdersByStatus?: Record<string, number>
}

export interface EnterpriseWorkOrderMetrics {
  totalWorkOrders: number
  completedWorkOrders: number
  pendingWorkOrders: number
  overdueWorkOrders: number
  completionRate: number
  averageCompletionTime: number
  totalCost: number
}

export interface TrendDataPointDto {
  date: string
  value: number
  metricType: string
}

export interface WorkOrderTrendsDto {
  startDate: string
  endDate: string
  creationTrend: TrendDataPointDto[]
  completionTrend: TrendDataPointDto[]
  costTrend: TrendDataPointDto[]
}

export interface MaintenanceScheduleDto {
  id: string
  assetName: string
  maintenanceTypeName?: string
  maintenanceType?: string
  name?: string
  nextDue?: string
  nextDueDate?: string
  nextScheduledDate?: string
  assignedTechnicianName?: string
}

export interface EnterpriseCrmDashboard {
  totalLeadCount: number
  qualifiedLeadCount: number
  leadsNeedingFollowUpCount: number
  openOpportunityCount: number
  activeQuoteCount: number
  activeAccountCount: number
  atRiskAccountCount: number
  pipelineAsOf: string
  funnelModel: string
  funnelRangeStart: string
  funnelRangeEnd: string
  historyCoverageStart?: string | null
  legacyHistorySnapshotCount: number
  lostOpportunityCount: number
  dataQualityIssues: string[]
  pipelineByStage: Array<{
    stageId: string
    stage: string
    stageOrder: number
    isClosed: boolean
    isWon: boolean
    isLost: boolean
    opportunityCount: number
    quoteCount: number
    percentageOfActivePipeline: number
    averageAgeDays: number
    stalledOpportunityCount: number
    overdueOpportunityCount: number
    amountsByCurrency?: Array<{ currency: string; amount: number }>
    weightedAmountsByCurrency?: Array<{ currency: string; amount: number }>
    opportunitiesWithoutCurrencyCount?: number
  }>
  accountRiskByBand: Array<{ label: string; count: number }>
  conversionFunnel: Array<{
    stageId: string
    stage: string
    stageOrder: number
    count: number
    conversionRate: number | null
    overallConversionRate: number | null
    amountsByCurrency?: Array<{ currency: string; amount: number }>
    opportunitiesWithoutCurrencyCount?: number
  }>
}

export interface EnterpriseOperationalQueue {
  pendingPurchaseRequisitionCount: number
  openPurchaseOrderCount: number
  pendingInventoryApprovalCount: number
  pendingInventoryIssueCount: number
  openTenderCount: number
  tendersClosingWithin14DaysCount: number
  openTendersByStatus: Array<{ label: string; count: number }>
}

export interface ManagementDashboardMoneyPoint {
  label: string
  currency: string
  amount: number
  count: number
}

export interface ProcurementInventoryManagementDashboard {
  rangeStartDate: string
  rangeEndDate: string
  warehouseId: string | null
  locationId: string | null
  inventoryAsOfUtc: string
  spendByCurrency: ManagementDashboardMoneyPoint[]
  spendByCategory: ManagementDashboardMoneyPoint[]
  spendByDepartment: ManagementDashboardMoneyPoint[]
  openPurchaseOrders: {
    count: number
    overdueCount: number
    orderedValueByCurrency: ManagementDashboardMoneyPoint[]
    remainingValueByCurrency: ManagementDashboardMoneyPoint[]
  }
  contracts: {
    activeCount: number
    expiringWithin90DaysCount: number
    averageUtilizationPercent: number
    contractValueByCurrency: ManagementDashboardMoneyPoint[]
    utilizedValueByCurrency: ManagementDashboardMoneyPoint[]
    expiringContracts: Array<{
      contractId: string
      contractNumber: string
      contractTitle: string
      supplierName: string
      endDate: string
      daysToExpiry: number
      utilizationPercent: number
    }>
  }
  inventory: {
    stockValue: number
    quantityOnHand: number
    itemLocationCount: number
    stockoutCount: number
    valueByCategory: Array<{ label: string; value: number; quantityOnHand: number; itemLocationCount: number }>
    valueByWarehouse: Array<{ label: string; value: number; quantityOnHand: number; itemLocationCount: number }>
  }
  cycleTime: {
    requisitionToPurchaseOrderSampleCount: number
    averageRequisitionToPurchaseOrderDays: number | null
    purchaseOrderToReceiptSampleCount: number
    averagePurchaseOrderToReceiptDays: number | null
  }
  serviceLevel: {
    eligibleOrderCount: number
    onTimeOrderCount: number
    onTimeDeliveryPercent: number | null
    acceptedFillRatePercent: number | null
  }
  supplierRisk: {
    assessedSupplierCount: number
    highOrCriticalSupplierCount: number
    awardBlockedSupplierCount: number
    openAlertCount: number
    overdueAssessmentCount: number
    byRiskBand: Array<{ label: string; count: number }>
  }
}

export interface EnterpriseDashboardData {
  reportingCurrency: DashboardCurrencyReference
  financeOverview: FinanceDashboardData | null
  crm: EnterpriseCrmDashboard | null
  projectDashboard: ProjectDashboardDto | null
  operationalQueues: EnterpriseOperationalQueue
  maintenanceOverview: MaintenanceDashboardOverview | null
  maintenanceMetrics: EnterpriseWorkOrderMetrics | null
  maintenanceTrends: WorkOrderTrendsDto | null
  procurementInventoryManagement: ProcurementInventoryManagementDashboard | null
  moduleStatus: DashboardModuleStatus[]
  rangeStartDate: string
  rangeEndDate: string
  lastUpdated: string
}

export function resolveDashboardReportingCurrency(
  data: Pick<EnterpriseDashboardData, 'reportingCurrency' | 'financeOverview'>,
): { currencyCode: string; decimalPlaces: number } {
  const configuredCode = data.reportingCurrency?.currencyCode?.trim().toUpperCase()
  if (configuredCode && /^[A-Z]{3}$/.test(configuredCode)) {
    return {
      currencyCode: configuredCode,
      decimalPlaces: data.reportingCurrency.decimalPlaces,
    }
  }

  const financeCode = data.financeOverview?.currencyCode?.trim().toUpperCase()
  if (financeCode && /^[A-Z]{3}$/.test(financeCode)) {
    return {
      currencyCode: financeCode,
      decimalPlaces: data.financeOverview?.currencyDecimalPlaces ?? 0,
    }
  }

  return { currencyCode: '', decimalPlaces: 0 }
}

class DashboardService {
  async getEnterpriseDashboard(
    startDate?: string,
    endDate?: string,
    warehouseId?: string,
    locationId?: string,
  ): Promise<EnterpriseDashboardData> {
    const parameters = new URLSearchParams()
    if (startDate && endDate) {
      parameters.set('startDate', startDate)
      parameters.set('endDate', endDate)
    }
    if (warehouseId) parameters.set('warehouseId', warehouseId)
    if (locationId) parameters.set('locationId', locationId)
    const query = parameters.size > 0 ? `?${parameters.toString()}` : ''

    return apiService.get<EnterpriseDashboardData>(`/dashboard/enterprise${query}`)
  }
}

export const dashboardService = new DashboardService()
