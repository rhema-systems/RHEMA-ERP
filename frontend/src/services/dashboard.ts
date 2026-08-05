import { apiService } from './api.service'
import type { CrmConversionsDto, CrmOverviewDto, CrmReportingDto } from './crmService'
import type { InventoryRequisitionDto } from './inventoryRequisitionService'
import type { ProjectDashboardDto } from './projectService'
import type { PurchaseOrderSummaryDto, PurchaseRequisitionSummaryDto } from './purchasingService'
import type { TenderDto } from './tenderService'

export interface DashboardModuleStatus {
  module: string
  available: boolean
  error?: string
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
  crmOverview: CrmOverviewDto | null
  crmReporting: CrmReportingDto | null
  crmConversions: CrmConversionsDto | null
  projectDashboard: ProjectDashboardDto | null
  pendingPurchaseRequisitions: PurchaseRequisitionSummaryDto[]
  openPurchaseOrders: PurchaseOrderSummaryDto[]
  pendingInventoryApprovals: InventoryRequisitionDto[]
  pendingInventoryIssues: InventoryRequisitionDto[]
  maintenanceOverview: MaintenanceDashboardOverview | null
  maintenanceMetrics: EnterpriseWorkOrderMetrics | null
  maintenanceTrends: WorkOrderTrendsDto | null
  upcomingMaintenance: MaintenanceScheduleDto[]
  tenders: TenderDto[]
  procurementInventoryManagement: ProcurementInventoryManagementDashboard | null
  moduleStatus: DashboardModuleStatus[]
  rangeStartDate: string
  rangeEndDate: string
  lastUpdated: string
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
