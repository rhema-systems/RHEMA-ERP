import { apiService } from './api.service'
import type { CrmConversionsDto, CrmOverviewDto, CrmReportingDto } from './crmService'
import type { InventoryRequisitionDto } from './inventoryRequisitionService'
import type { ProjectDashboardDto } from './projectService'
import type { PurchaseOrderSummaryDto, PurchaseRequisitionSummaryDto } from './purchasingService'
import type { TenderDto } from './tenderService'
import type { WorkOrderMetrics } from './workOrderService'

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
  maintenanceMetrics: WorkOrderMetrics | null
  maintenanceTrends: WorkOrderTrendsDto | null
  upcomingMaintenance: MaintenanceScheduleDto[]
  tenders: TenderDto[]
  moduleStatus: DashboardModuleStatus[]
  lastUpdated: string
}

class DashboardService {
  async getEnterpriseDashboard(): Promise<EnterpriseDashboardData> {
    return apiService.get<EnterpriseDashboardData>('/dashboard/enterprise')
  }
}

export const dashboardService = new DashboardService()
