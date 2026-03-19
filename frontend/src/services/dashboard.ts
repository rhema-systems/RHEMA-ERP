import { apiService } from './api.service'
import { crmService, type CrmOverviewDto, type CrmReportingDto } from './crmService'
import { inventoryRequisitionService, type InventoryRequisitionDto } from './inventoryRequisitionService'
import { projectService, type ProjectDashboardDto } from './projectService'
import { purchasingService, type PurchaseOrderSummaryDto, type PurchaseRequisitionSummaryDto } from './purchasingService'
import { tenderService, type TenderDto } from './tenderService'
import { workOrderService, type WorkOrderMetrics } from './workOrderService'

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
  private toErrorMessage(error: unknown, fallback: string): string {
    if (error instanceof Error && error.message) {
      return error.message
    }

    return fallback
  }

  private async loadModule<T>(
    module: string,
    action: () => Promise<T>,
    fallback: T,
    fallbackMessage: string
  ): Promise<{ data: T; status: DashboardModuleStatus }> {
    try {
      const data = await action()
      return {
        data,
        status: {
          module,
          available: true,
        },
      }
    } catch (error) {
      return {
        data: fallback,
        status: {
          module,
          available: false,
          error: this.toErrorMessage(error, fallbackMessage),
        },
      }
    }
  }

  async getEnterpriseDashboard(): Promise<EnterpriseDashboardData> {
    const endDate = new Date()
    const startDate = new Date()
    startDate.setMonth(endDate.getMonth() - 5)

    const [
      crmOverviewResult,
      crmReportingResult,
      projectDashboardResult,
      purchaseRequisitionsResult,
      purchaseOrdersResult,
      inventoryApprovalResult,
      inventoryIssueResult,
      maintenanceOverviewResult,
      maintenanceMetricsResult,
      maintenanceTrendsResult,
      upcomingMaintenanceResult,
      tendersResult,
    ] = await Promise.all([
      this.loadModule('CRM Overview', () => crmService.getOverview(8), null, 'CRM overview is unavailable'),
      this.loadModule('CRM Reporting', () => crmService.getReporting(8), null, 'CRM reporting is unavailable'),
      this.loadModule('Projects', () => projectService.getDashboard(), null, 'Project dashboard is unavailable'),
      this.loadModule(
        'Purchase Requisitions',
        () => purchasingService.getPendingApprovalRequisitions(),
        [] as PurchaseRequisitionSummaryDto[],
        'Pending purchase requisitions are unavailable'
      ),
      this.loadModule(
        'Purchase Orders',
        async () => {
          const response = await purchasingService.getPurchaseOrders({ page: 1, pageSize: 25 })
          return response.items.filter((order) => !['completed', 'cancelled', 'closed'].includes(order.status.toLowerCase()))
        },
        [] as PurchaseOrderSummaryDto[],
        'Purchase order data is unavailable'
      ),
      this.loadModule(
        'Inventory Approval Queue',
        () => inventoryRequisitionService.getPendingApproval(),
        [] as InventoryRequisitionDto[],
        'Pending inventory approvals are unavailable'
      ),
      this.loadModule(
        'Inventory Issue Queue',
        () => inventoryRequisitionService.getPendingIssue(),
        [] as InventoryRequisitionDto[],
        'Pending inventory issues are unavailable'
      ),
      this.loadModule(
        'Maintenance Overview',
        () => apiService.get<MaintenanceDashboardOverview>('/maintenance/dashboard/overview'),
        null,
        'Maintenance overview is unavailable'
      ),
      this.loadModule(
        'Maintenance Metrics',
        () => workOrderService.getWorkOrderMetrics(),
        null,
        'Maintenance work order metrics are unavailable'
      ),
      this.loadModule(
        'Maintenance Trends',
        () =>
          apiService.get<WorkOrderTrendsDto>(
            `/maintenance/dashboard/work-order-trends?startDate=${startDate.toISOString()}&endDate=${endDate.toISOString()}`,
          ),
        null,
        'Maintenance trends are unavailable'
      ),
      this.loadModule(
        'Upcoming Maintenance',
        () => apiService.get<MaintenanceScheduleDto[]>('/maintenance/schedules/due-in-days/14'),
        [] as MaintenanceScheduleDto[],
        'Upcoming maintenance is unavailable'
      ),
      this.loadModule(
        'Tenders',
        async () => {
          const response = await tenderService.getTenders({ page: 1, pageSize: 50 })
          return response.items
        },
        [] as TenderDto[],
        'Tender data is unavailable'
      ),
    ])

    return {
      crmOverview: crmOverviewResult.data,
      crmReporting: crmReportingResult.data,
      projectDashboard: projectDashboardResult.data,
      pendingPurchaseRequisitions: purchaseRequisitionsResult.data,
      openPurchaseOrders: purchaseOrdersResult.data,
      pendingInventoryApprovals: inventoryApprovalResult.data,
      pendingInventoryIssues: inventoryIssueResult.data,
      maintenanceOverview: maintenanceOverviewResult.data,
      maintenanceMetrics: maintenanceMetricsResult.data,
      maintenanceTrends: maintenanceTrendsResult.data,
      upcomingMaintenance: upcomingMaintenanceResult.data,
      tenders: tendersResult.data,
      moduleStatus: [
        crmOverviewResult.status,
        crmReportingResult.status,
        projectDashboardResult.status,
        purchaseRequisitionsResult.status,
        purchaseOrdersResult.status,
        inventoryApprovalResult.status,
        inventoryIssueResult.status,
        maintenanceOverviewResult.status,
        maintenanceMetricsResult.status,
        maintenanceTrendsResult.status,
        upcomingMaintenanceResult.status,
        tendersResult.status,
      ],
      lastUpdated: new Date().toISOString(),
    }
  }
}

export const dashboardService = new DashboardService()
