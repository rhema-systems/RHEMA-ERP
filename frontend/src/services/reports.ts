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

  async exportReport(reportId: string, exportReportDto: ExportReportDto): Promise<Blob> {
    try {
      // Note: For blob responses, we might need to handle this differently with the apiService
      // For now, this is a simplified version - may need adjustment based on apiService implementation
      return await apiService.request<Blob>(`/reports/${reportId}/export`, {
        method: 'POST',
        body: JSON.stringify(exportReportDto),
      });
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
}

export const reportsService = new ReportsService();