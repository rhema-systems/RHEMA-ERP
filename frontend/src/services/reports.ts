import { apiService } from './api.service';

export interface ReportDefinition {
  id: string;
  name: string;
  description: string;
  type: string;
  status: string;
  createdBy: string;
  createdAt: string;
  lastRun?: string;
  nextRun?: string;
  isScheduled: boolean;
  isFavorite: boolean;
  parameters?: Record<string, any>;
  query?: string;
  columns?: ReportColumn[];
  visualization?: ReportVisualization;
  tags?: string[];
}

export interface ReportColumn {
  name: string;
  dataType: string;
  displayName?: string;
  isVisible: boolean;
  format?: string;
  order: number;
  aggregationType?: string;
}

export interface ReportVisualization {
  type: string;
  chartType?: string;
  configuration?: Record<string, any>;
  xAxis?: string;
  yAxis?: string;
  series?: string[];
}

export interface CreateReportDto {
  name: string;
  description: string;
  type: string;
  parameters?: Record<string, any>;
  query?: string;
  columns?: ReportColumn[];
  visualization?: ReportVisualization;
  tags?: string[];
}

export interface UpdateReportDto {
  name?: string;
  description?: string;
  status?: string;
  parameters?: Record<string, any>;
  query?: string;
  columns?: ReportColumn[];
  visualization?: ReportVisualization;
  tags?: string[];
}

export interface ExecuteReportDto {
  parameters?: Record<string, any>;
  startDate?: string;
  endDate?: string;
  maxRows?: number;
  includeMetadata?: boolean;
}

export interface ReportResult {
  reportId: string;
  reportName: string;
  executedAt: string;
  executionTime: string;
  totalRows: number;
  columns: ReportColumn[];
  data: Record<string, any>[];
  metadata?: ReportMetadata;
  chartData?: ReportChartData[];
}

export interface ReportMetadata {
  parameters?: Record<string, any>;
  query?: string;
  dataAsOf?: string;
  dataSource?: string;
  statistics?: Record<string, any>;
}

export interface ReportChartData {
  label: string;
  value: any;
  color?: string;
  metadata?: Record<string, any>;
}

export interface ExportReportDto {
  format: 'pdf' | 'csv' | 'xlsx' | 'json';
  parameters?: Record<string, any>;
  includeCharts?: boolean;
  includeHeaders?: boolean;
  template?: string;
}

export interface CreateReportScheduleDto {
  name: string;
  frequency: 'daily' | 'weekly' | 'monthly' | 'quarterly';
  timeOfDay: string;
  dayOfWeek?: number;
  dayOfMonth?: number;
  startDate: string;
  endDate?: string;
  emailRecipients?: string[];
  exportFormat?: string;
  parameters?: Record<string, any>;
  isActive?: boolean;
}

export interface ReportSchedule {
  id: string;
  reportId: string;
  name: string;
  frequency: string;
  timeOfDay: string;
  dayOfWeek?: number;
  dayOfMonth?: number;
  startDate: string;
  endDate?: string;
  nextExecutionDate?: string;
  lastExecutionDate?: string;
  emailRecipients?: string[];
  exportFormat?: string;
  parameters?: Record<string, any>;
  isActive: boolean;
  status: string;
  createdAt: string;
}

export interface CreateReportTemplateDto {
  name: string;
  description: string;
  category: string;
  type: string;
  chartType?: string;
  isCustom?: boolean;
  tags?: string[];
  previewImage?: string;
  configuration?: Record<string, any>;
}

export interface ReportTemplate {
  id: string;
  name: string;
  description: string;
  category: string;
  type: string;
  chartType?: string;
  isCustom: boolean;
  createdBy: string;
  createdAt: string;
  lastUsed?: string;
  usageCount: number;
  tags?: string[];
  previewImage?: string;
  configuration?: Record<string, any>;
}

export interface ReportAnalytics {
  totalReports: number;
  scheduledReports: number;
  reportsRunToday: number;
  dataSourcesConnected: number;
  totalExports: number;
  avgGenerationTime: number;
  usageStats?: ReportUsageStats[];
  performanceMetrics?: ReportPerformance[];
  topReports?: TopReport[];
}

export interface ReportUsageStats {
  period: string;
  reportsRun: number;
  uniqueUsers: number;
  avgExecutionTime: number;
  date: string;
}

export interface ReportPerformance {
  reportId: string;
  reportName: string;
  avgExecutionTime: number;
  executionCount: number;
  lastRun: string;
  status: string;
}

export interface TopReport {
  reportId: string;
  reportName: string;
  type: string;
  executionCount: number;
  uniqueUsers: number;
  lastRun: string;
  avgRating: number;
}

// Role Assignment interfaces
export interface ReportRoleAssignment {
  id: string;
  reportId: string;
  reportName: string;
  roleId: string;
  roleName: string;
  canRead: boolean;
  canExecute: boolean;
  canExport: boolean;
  canEdit: boolean;
  canSchedule: boolean;
  assignedAt: string;
  assignedBy: string;
}

export interface CreateReportRoleAssignmentDto {
  reportId: string;
  roleId: string;
  canRead: boolean;
  canExecute: boolean;
  canExport: boolean;
  canEdit: boolean;
  canSchedule: boolean;
}

export interface UpdateReportRoleAssignmentDto {
  canRead: boolean;
  canExecute: boolean;
  canExport: boolean;
  canEdit: boolean;
  canSchedule: boolean;
}

export interface BulkAssignRolesToReportDto {
  reportId: string;
  roleAssignments: CreateReportRoleAssignmentDto[];
}

export interface ReportAccessDto {
  reportId: string;
  reportName: string;
  hasAccess: boolean;
  canRead: boolean;
  canExecute: boolean;
  canExport: boolean;
  canEdit: boolean;
  canSchedule: boolean;
  accessibleRoles: string[];
}

class ReportsService {

  // Report CRUD operations
  async getReports(type?: string, status?: string, favoriteOnly?: boolean): Promise<ReportDefinition[]> {
    try {
      const params = new URLSearchParams();
      if (type) params.append('type', type);
      if (status) params.append('status', status);
      if (favoriteOnly !== undefined) params.append('favoriteOnly', favoriteOnly.toString());

      return await apiService.request<ReportDefinition[]>(`/reports?${params.toString()}`);
    } catch (error) {
      console.error('Error fetching reports:', error);
      throw error;
    }
  }

  // Admin-specific method to get ALL reports for the tenant (bypasses role filtering)
  async getReportsForAdmin(type?: string, status?: string): Promise<ReportDefinition[]> {
    try {
      const params = new URLSearchParams();
      if (type) params.append('type', type);
      if (status) params.append('status', status);

      return await apiService.request<ReportDefinition[]>(`/reports/admin?${params.toString()}`);
    } catch (error) {
      console.error('Error fetching admin reports:', error);
      throw error;
    }
  }

  async getReport(reportId: string): Promise<ReportDefinition> {
    try {
      return await apiService.request<ReportDefinition>(`/reports/${reportId}`);
    } catch (error) {
      console.error('Error fetching report:', error);
      throw error;
    }
  }

  async createReport(createReportDto: CreateReportDto): Promise<ReportDefinition> {
    try {
      return await apiService.request<ReportDefinition>('/reports', {
        method: 'POST',
        body: JSON.stringify(createReportDto),
      });
    } catch (error) {
      console.error('Error creating report:', error);
      throw error;
    }
  }

  async updateReport(reportId: string, updateReportDto: UpdateReportDto): Promise<ReportDefinition> {
    try {
      return await apiService.request<ReportDefinition>(`/reports/${reportId}`, {
        method: 'PUT',
        body: JSON.stringify(updateReportDto),
      });
    } catch (error) {
      console.error('Error updating report:', error);
      throw error;
    }
  }

  async deleteReport(reportId: string): Promise<void> {
    try {
      await apiService.request<void>(`/reports/${reportId}`, {
        method: 'DELETE',
      });
    } catch (error) {
      console.error('Error deleting report:', error);
      throw error;
    }
  }

  // Report execution
  async executeReport(reportId: string, executeReportDto: ExecuteReportDto): Promise<ReportResult> {
    try {
      return await apiService.request<ReportResult>(`/reports/${reportId}/execute`, {
        method: 'POST',
        body: JSON.stringify(executeReportDto),
      });
    } catch (error) {
      console.error('Error executing report:', error);
      throw error;
    }
  }

  async exportReport(reportId: string, exportReportDto: ExportReportDto): Promise<{ fileName: string; blob: Blob }> {
    try {
      const baseUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';
      const url = `${baseUrl}/reports/${reportId}/export`;
      
      // Get auth token for headers
      const token = typeof window !== 'undefined' ? localStorage.getItem('authToken') : null;
      
      const response = await fetch(url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token && { 'Authorization': `Bearer ${token}` })
        },
        body: JSON.stringify(exportReportDto),
      });

      if (!response.ok) {
        throw new Error(`Export failed with status ${response.status}`);
      }

      // Get filename from Content-Disposition header or create a default one
      const contentDisposition = response.headers.get('Content-Disposition');
      let fileName = 'report-export';
      
      if (contentDisposition) {
        const fileNameMatch = contentDisposition.match(/filename[^;=\n]*=(['"]*)([^'"\n]*(['"]*))/i);
        if (fileNameMatch && fileNameMatch[2]) {
          fileName = fileNameMatch[2];
        }
      } else {
        // Create filename based on format
        const timestamp = new Date().toISOString().split('T')[0];
        fileName = `report-${timestamp}.${exportReportDto.format}`;
      }

      const blob = await response.blob();
      return { fileName, blob };
    } catch (error) {
      console.error('Error exporting report:', error);
      throw error;
    }
  }

  // Report scheduling
  async scheduleReport(reportId: string, scheduleDto: CreateReportScheduleDto): Promise<ReportSchedule> {
    try {
      return await apiService.request<ReportSchedule>(`/reports/${reportId}/schedule`, {
        method: 'POST',
        body: JSON.stringify(scheduleDto),
      });
    } catch (error) {
      console.error('Error scheduling report:', error);
      throw error;
    }
  }

  async getReportSchedule(reportId: string, scheduleId: string): Promise<ReportSchedule> {
    try {
      return await apiService.request<ReportSchedule>(`/reports/${reportId}/schedule/${scheduleId}`);
    } catch (error) {
      console.error('Error fetching report schedule:', error);
      throw error;
    }
  }

  // Templates
  async getReportTemplates(category?: string): Promise<ReportTemplate[]> {
    try {
      const params = new URLSearchParams();
      if (category) params.append('category', category);

      return await apiService.request<ReportTemplate[]>(`/reports/templates?${params.toString()}`);
    } catch (error) {
      console.error('Error fetching report templates:', error);
      throw error;
    }
  }

  async createReportTemplate(createTemplateDto: CreateReportTemplateDto): Promise<ReportTemplate> {
    try {
      return await apiService.request<ReportTemplate>('/reports/templates', {
        method: 'POST',
        body: JSON.stringify(createTemplateDto),
      });
    } catch (error) {
      console.error('Error creating report template:', error);
      throw error;
    }
  }

  // Analytics
  async getReportAnalytics(period = 'last-30-days', tenantFilter?: string): Promise<ReportAnalytics> {
    try {
      const params = new URLSearchParams();
      params.append('period', period);
      if (tenantFilter) params.append('tenantFilter', tenantFilter);

      return await apiService.request<ReportAnalytics>(`/reports/analytics?${params.toString()}`);
    } catch (error) {
      console.error('Error fetching report analytics:', error);
      throw error;
    }
  }

  // Favorites
  async toggleFavorite(reportId: string): Promise<{ isFavorite: boolean }> {
    try {
      return await apiService.request<{ isFavorite: boolean }>(`/reports/${reportId}/favorite`, {
        method: 'POST',
        body: '{}',
      });
    } catch (error) {
      console.error('Error toggling favorite:', error);
      throw error;
    }
  }

  // Role Assignment methods
  async getReportRoleAssignments(reportId: string): Promise<ReportRoleAssignment[]> {
      try {
        console.log('🚀 ReportsService: Starting to fetch role assignments for reportId:', reportId);
        const endpoint = `/reportroleassignment/report/${reportId}/assignments`;
        console.log('🔗 ReportsService: Calling endpoint:', endpoint);
        
        // Use the new accessible endpoint that doesn't require admin permissions
        const result = await apiService.request<ReportRoleAssignment[]>(endpoint);
        
        console.log('📦 ReportsService: Raw API response:', result);
        console.log('📁 ReportsService: Response length:', Array.isArray(result) ? result.length : 'Not an array');
        
        if (Array.isArray(result) && result.length > 0) {
          console.log('📋 ReportsService: First assignment sample:', {
            id: result[0].id,
            reportId: result[0].reportId,
            roleId: result[0].roleId,
            roleName: result[0].roleName,
            canRead: result[0].canRead
          });
        }
        
        return result;
      } catch (error) {
        console.error('❌ ReportsService: Error fetching report role assignments:', error);
        console.error('❌ ReportsService: Error details:', {
          message: error instanceof Error ? error.message : 'Unknown error',
          status: (error as any)?.response?.status,
          statusText: (error as any)?.response?.statusText,
          data: (error as any)?.response?.data
        });
        throw error;
      }
    }

  async getAllTenantRoleAssignments(): Promise<ReportRoleAssignment[]> {
    try {
      // Get all role assignments for the current tenant
      return await apiService.request<ReportRoleAssignment[]>('/reportroleassignment/tenant/assignments');
    } catch (error) {
      console.error('Error fetching tenant role assignments:', error);
      throw error;
    }
  }

  async getRoleReportAssignments(roleId: string): Promise<ReportRoleAssignment[]> {
    try {
      return await apiService.request<ReportRoleAssignment[]>(`/reportroleassignment/role/${roleId}`);
    } catch (error) {
      console.error('Error fetching role report assignments:', error);
      throw error;
    }
  }

  async createRoleAssignment(assignmentDto: CreateReportRoleAssignmentDto): Promise<ReportRoleAssignment> {
    try {
      return await apiService.request<ReportRoleAssignment>('/reportroleassignment', {
        method: 'POST',
        body: JSON.stringify(assignmentDto),
      });
    } catch (error) {
      console.error('Error creating role assignment:', error);
      throw error;
    }
  }

  async updateRoleAssignment(assignmentId: string, updateDto: UpdateReportRoleAssignmentDto): Promise<ReportRoleAssignment> {
    try {
      return await apiService.request<ReportRoleAssignment>(`/reportroleassignment/${assignmentId}`, {
        method: 'PUT',
        body: JSON.stringify(updateDto),
      });
    } catch (error) {
      console.error('Error updating role assignment:', error);
      throw error;
    }
  }

  async deleteRoleAssignment(assignmentId: string): Promise<void> {
    try {
      await apiService.request<void>(`/reportroleassignment/${assignmentId}`, {
        method: 'DELETE',
      });
    } catch (error) {
      console.error('Error deleting role assignment:', error);
      throw error;
    }
  }

  // Report publishing
  async publishReport(reportId: string): Promise<{ reportId: string; publishedAt: string }> {
    try {
      return await apiService.request<{ reportId: string; publishedAt: string }>(`/reports/${reportId}/publish`, {
        method: 'POST',
        body: JSON.stringify({}),
      });
    } catch (error) {
      console.error('Error publishing report:', error);
      throw error;
    }
  }

  async unpublishReport(reportId: string): Promise<{ reportId: string; unpublishedAt: string }> {
    try {
      return await apiService.request<{ reportId: string; unpublishedAt: string }>(`/reports/${reportId}/unpublish`, {
        method: 'POST',
        body: JSON.stringify({}),
      });
    } catch (error) {
      console.error('Error unpublishing report:', error);
      throw error;
    }
  }

  // Module assignment
  async assignReportToModule(reportId: string, moduleId: string): Promise<{ reportId: string; moduleId: string; assignedAt: string }> {
    try {
      return await apiService.request<{ reportId: string; moduleId: string; assignedAt: string }>(`/reports/${reportId}/assign-module`, {
        method: 'POST',
        body: JSON.stringify({ moduleId }),
      });
    } catch (error) {
      console.error('Error assigning report to module:', error);
      throw error;
    }
  }

  async unassignReportFromModule(reportId: string): Promise<{ reportId: string; unassignedAt: string }> {
    try {
      return await apiService.request<{ reportId: string; unassignedAt: string }>(`/reports/${reportId}/unassign-module`, {
        method: 'POST',
        body: JSON.stringify({}),
      });
    } catch (error) {
      console.error('Error unassigning report from module:', error);
      throw error;
    }
  }
}

export const reportsService = new ReportsService();
